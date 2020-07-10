#include "pch.h"
#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"
#include "JsonHelpers.h"
#include "ZoneSet.h"
#include "trace.h"
#include "Settings.h"

#include <common/common.h>

#include <shlwapi.h>
#include <filesystem>
#include <fstream>
#include <regex>
#include <sstream>
#include <unordered_set>

namespace
{
    const wchar_t* FANCY_ZONES_DATA_FILE = L"zones-settings.json";
    const wchar_t* FANCY_ZONES_APP_ZONE_HISTORY_FILE = L"app-zone-history.json";
    const wchar_t* DEFAULT_GUID = L"{00000000-0000-0000-0000-000000000000}";
    const wchar_t* REG_SETTINGS = L"Software\\SuperFancyZones";

    std::wstring ExtractVirtualDesktopId(const std::wstring& deviceId)
    {
        // Format: <device-id>_<resolution>_<virtual-desktop-id>
        return deviceId.substr(deviceId.rfind('_') + 1);
    }
}

namespace FancyZonesDataNS
{


    FancyZonesData& FancyZonesDataInstance()
    {
        static FancyZonesData instance;
        return instance;
    }

    FancyZonesData::FancyZonesData()
    {
        std::wstring result = PTSettingsHelper::get_module_save_folder_location(L"FancyZones");
        zonesSettingsFilePath = result + L"\\" + std::wstring(FANCY_ZONES_DATA_FILE);
        appZoneHistoryFilePath = result + L"\\" + std::wstring(FANCY_ZONES_APP_ZONE_HISTORY_FILE);
    }

    std::optional<FancyZonesDataTypes::DeviceInfoData> FancyZonesData::FindDeviceInfo(const std::wstring& zoneWindowId) const
    {
        std::scoped_lock lock{ dataLock };
        auto it = deviceInfoMap.find(zoneWindowId);
        return it != end(deviceInfoMap) ? std::optional{ it->second } : std::nullopt;
    }

    std::optional<FancyZonesDataTypes::CustomZoneSetData> FancyZonesData::FindCustomZoneSet(const std::wstring& guid) const
    {
        std::scoped_lock lock{ dataLock };
        auto it = customZoneSetsMap.find(guid);
        return it != end(customZoneSetsMap) ? std::optional{ it->second } : std::nullopt;
    }

    void FancyZonesData::SetDeviceInfo(const std::wstring& deviceId, FancyZonesDataTypes::DeviceInfoData data)
    {
        deviceInfoMap[deviceId] = data;
    }

    void FancyZonesData::AddDevice(const std::wstring& deviceId)
    {
        std::scoped_lock lock{ dataLock };
        if (!deviceInfoMap.contains(deviceId))
        {
            // Creates default entry in map when ZoneWindow is created
            deviceInfoMap[deviceId] = FancyZonesDataTypes::DeviceInfoData{ FancyZonesDataTypes::ZoneSetData{ L"null", FancyZonesDataTypes::ZoneSetLayoutType::Blank } };
        }
    }

    void FancyZonesData::CloneDeviceInfo(const std::wstring& source, const std::wstring& destination)
    {
        if (source == destination)
        {
            return;
        }
        std::scoped_lock lock{ dataLock };

        // The source virtual desktop is deleted, simply ignore it.
        if (!deviceInfoMap.contains(source))
        {
            return;
        }

        // Clone information from source device if destination device is uninitialized (Blank).
        auto& destInfo = deviceInfoMap[destination];
        if (destInfo.activeZoneSet.type == FancyZonesDataTypes::ZoneSetLayoutType::Blank)
        {
            destInfo = deviceInfoMap[source];
        }
    }

    void FancyZonesData::UpdatePrimaryDesktopData(const std::wstring& desktopId)
    {
        // Explorer persists current virtual desktop identifier to registry on a per session basis,
        // but only after first virtual desktop switch happens. If the user hasn't switched virtual
        // desktops in this session value in registry will be empty and we will use default GUID in
        // that case (00000000-0000-0000-0000-000000000000).
        // This method will go through all our persisted data with default GUID and update it with
        // valid one.
        auto replaceDesktopId = [&desktopId](const std::wstring& deviceId) {
            return deviceId.substr(0, deviceId.rfind('_') + 1) + desktopId;
        };
        std::scoped_lock lock{ dataLock };
        for (auto& [path, perDesktopData] : appZoneHistoryMap)
        {
            for (auto& data : perDesktopData)
            {
                if (ExtractVirtualDesktopId(data.deviceId) == DEFAULT_GUID)
                {
                    data.deviceId = replaceDesktopId(data.deviceId);
                }
            }
        }
        std::vector<std::wstring> toReplace{};
        for (const auto& [id, data] : deviceInfoMap)
        {
            if (ExtractVirtualDesktopId(id) == DEFAULT_GUID)
            {
                toReplace.push_back(id);
            }
        }
        for (const auto& id : toReplace)
        {
            auto mapEntry = deviceInfoMap.extract(id);
            mapEntry.key() = replaceDesktopId(id);
            deviceInfoMap.insert(std::move(mapEntry));
        }
        SaveFancyZonesData();
    }

    void FancyZonesData::RemoveDeletedDesktops(const std::vector<std::wstring>& activeDesktops)
    {
        std::unordered_set<std::wstring> active(std::begin(activeDesktops), std::end(activeDesktops));
        std::scoped_lock lock{ dataLock };
        for (auto it = std::begin(deviceInfoMap); it != std::end(deviceInfoMap);)
        {
            std::wstring desktopId = ExtractVirtualDesktopId(it->first);
            auto foundId = active.find(desktopId);
            if (foundId == std::end(active))
            {
                RemoveDesktopAppZoneHistory(desktopId);
                it = deviceInfoMap.erase(it);
            }
            else
            {
                ++it;
            }
        }
        SaveFancyZonesData();
    }

    bool FancyZonesData::IsAnotherWindowOfApplicationInstanceZoned(HWND window, const std::wstring_view& deviceId) const
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != std::end(appZoneHistoryMap))
            {
                auto& perDesktopData = history->second;
                for (auto& data : perDesktopData)
                {
                    if (data.deviceId == deviceId)
                    {
                        DWORD processId = 0;
                        GetWindowThreadProcessId(window, &processId);

                        auto processIdIt = data.processIdToHandleMap.find(processId);

                        if (processIdIt == std::end(data.processIdToHandleMap))
                        {
                            return false;
                        }
                        else if (processIdIt->second != window && IsWindow(processIdIt->second))
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    void FancyZonesData::UpdateProcessIdToHandleMap(HWND window, const std::wstring_view& deviceId)
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != std::end(appZoneHistoryMap))
            {
                auto& perDesktopData = history->second;
                for (auto& data : perDesktopData)
                {
                    if (data.deviceId == deviceId)
                    {
                        DWORD processId = 0;
                        GetWindowThreadProcessId(window, &processId);
                        data.processIdToHandleMap[processId] = window;
                        break;
                    }
                }
            }
        }
    }

    std::vector<int> FancyZonesData::GetAppLastZoneIndexSet(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId) const
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != std::end(appZoneHistoryMap))
            {
                const auto& perDesktopData = history->second;
                for (const auto& data : perDesktopData)
                {
                    if (data.zoneSetUuid == zoneSetId && data.deviceId == deviceId)
                    {
                        return data.zoneIndexSet;
                    }
                }
            }
        }

        return {};
    }

    bool FancyZonesData::RemoveAppLastZone(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId)
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != std::end(appZoneHistoryMap))
            {
                auto& perDesktopData = history->second;
                for (auto data = std::begin(perDesktopData); data != std::end(perDesktopData);)
                {
                    if (data->deviceId == deviceId && data->zoneSetUuid == zoneSetId)
                    {
                        if (!IsAnotherWindowOfApplicationInstanceZoned(window, deviceId))
                        {
                            DWORD processId = 0;
                            GetWindowThreadProcessId(window, &processId);

                            data->processIdToHandleMap.erase(processId);
                        }

                        // if there is another instance of same application placed in the same zone don't erase history
                        size_t windowZoneStamp = reinterpret_cast<size_t>(::GetProp(window, MULTI_ZONE_STAMP));
                        for (auto placedWindow : data->processIdToHandleMap)
                        {
                            size_t placedWindowZoneStamp = reinterpret_cast<size_t>(::GetProp(placedWindow.second, MULTI_ZONE_STAMP));
                            if (IsWindow(placedWindow.second) && (windowZoneStamp == placedWindowZoneStamp))
                            {
                                return false;
                            }
                        }

                        data = perDesktopData.erase(data);
                        if (perDesktopData.empty())
                        {
                            appZoneHistoryMap.erase(processPath);
                        }
                        SaveFancyZonesData();
                        return true;
                    }
                    else
                    {
                        ++data;
                    }
                }
            }
        }

        return false;
    }

    bool FancyZonesData::SetAppLastZones(HWND window, const std::wstring& deviceId, const std::wstring& zoneSetId, const std::vector<int>& zoneIndexSet)
    {
        std::scoped_lock lock{ dataLock };

        if (IsAnotherWindowOfApplicationInstanceZoned(window, deviceId))
        {
            return false;
        }

        auto processPath = get_process_path(window);
        if (processPath.empty())
        {
            return false;
        }

        DWORD processId = 0;
        GetWindowThreadProcessId(window, &processId);

        auto history = appZoneHistoryMap.find(processPath);
        if (history != std::end(appZoneHistoryMap))
        {
            auto& perDesktopData = history->second;
            for (auto& data : perDesktopData)
            {
                if (data.deviceId == deviceId)
                {
                    // application already has history on this work area, update it with new window position
                    data.processIdToHandleMap[processId] = window;
                    data.zoneSetUuid = zoneSetId;
                    data.zoneIndexSet = zoneIndexSet;
                    SaveFancyZonesData();
                    return true;
                }
            }
        }

        std::unordered_map<DWORD, HWND> processIdToHandleMap{};
        processIdToHandleMap[processId] = window;
        FancyZonesDataTypes::AppZoneHistoryData data{ .processIdToHandleMap = processIdToHandleMap,
                                 .zoneSetUuid = zoneSetId,
                                 .deviceId = deviceId,
                                 .zoneIndexSet = zoneIndexSet };

        if (appZoneHistoryMap.contains(processPath))
        {
            // application already has history but on other desktop, add with new desktop info
            appZoneHistoryMap[processPath].push_back(data);
        }
        else
        {
            // new application, create entry in app zone history map
            appZoneHistoryMap[processPath] = std::vector<FancyZonesDataTypes::AppZoneHistoryData>{ data };
        }

        SaveFancyZonesData();
        return true;
    }

    void FancyZonesData::SetActiveZoneSet(const std::wstring& deviceId, const FancyZonesDataTypes::ZoneSetData& data)
    {
        std::scoped_lock lock{ dataLock };
        auto it = deviceInfoMap.find(deviceId);
        if (it != deviceInfoMap.end())
        {
            it->second.activeZoneSet = data;
        }
    }

    void FancyZonesData::SerializeDeviceInfoToTmpFile(const FancyZonesDataTypes::DeviceInfoJSON& deviceInfo, std::wstring_view tmpFilePath) const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonObject deviceInfoJson = FancyZonesDataTypes::DeviceInfoJSON::ToJson(deviceInfo);
        json::to_file(tmpFilePath, deviceInfoJson);
    }

    void FancyZonesData::ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::scoped_lock lock{ dataLock };
        if (std::filesystem::exists(tmpFilePath))
        {
            if (auto zoneSetJson = json::from_file(tmpFilePath); zoneSetJson.has_value())
            {
                if (auto deviceInfo = FancyZonesDataTypes::DeviceInfoJSON::FromJson(zoneSetJson.value()); deviceInfo.has_value())
                {
                    deviceInfoMap[deviceInfo->deviceId] = std::move(deviceInfo->data);
                    DeleteTmpFile(tmpFilePath);
                }
            }
        }
    }

    bool FancyZonesData::ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::scoped_lock lock{ dataLock };
        bool res = true;
        if (std::filesystem::exists(tmpFilePath))
        {
            try
            {
                if (auto customZoneSetJson = json::from_file(tmpFilePath); customZoneSetJson.has_value())
                {
                    if (auto customZoneSet = FancyZonesDataTypes::CustomZoneSetJSON::FromJson(customZoneSetJson.value()); customZoneSet.has_value())
                    {
                        customZoneSetsMap[customZoneSet->uuid] = std::move(customZoneSet->data);
                    }
                }
            }
            catch (const winrt::hresult_error&)
            {
                res = false;
            }

            DeleteTmpFile(tmpFilePath);
        }
        return res;
    }

    bool FancyZonesData::ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::scoped_lock lock{ dataLock };
        bool res = true;
        if (std::filesystem::exists(tmpFilePath))
        {
            auto deletedZoneSetsJson = json::from_file(tmpFilePath);
            try
            {
                auto deletedCustomZoneSets = deletedZoneSetsJson->GetNamedArray(L"deleted-custom-zone-sets");
                for (auto zoneSet : deletedCustomZoneSets)
                {
                    std::wstring uuid = L"{" + std::wstring{ zoneSet.GetString() } + L"}";
                    customZoneSetsMap.erase(std::wstring{ uuid });
                }
            }
            catch (const winrt::hresult_error&)
            {
                res = false;
            }

            DeleteTmpFile(tmpFilePath);
        }

        return res;
    }

    void FancyZonesData::LoadFancyZonesData()
    {
        std::wstring jsonFilePath = GetPersistFancyZonesJSONPath();

        if (!std::filesystem::exists(jsonFilePath))
        {
            MigrateCustomZoneSetsFromRegistry();

            SaveFancyZonesData();
        }
        else
        {
            json::JsonObject fancyZonesDataJSON = JSONHelpers::GetPersistFancyZonesJSON(zonesSettingsFilePath, appZoneHistoryFilePath);

            appZoneHistoryMap = JSONHelpers::ParseAppZoneHistory(fancyZonesDataJSON);
            deviceInfoMap = JSONHelpers::ParseDeviceInfos(fancyZonesDataJSON);
            customZoneSetsMap = JSONHelpers::ParseCustomZoneSets(fancyZonesDataJSON);
        }
    }

    void FancyZonesData::SaveFancyZonesData() const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonObject root{};
        json::JsonObject appZoneHistoryRoot{};

        appZoneHistoryRoot.SetNamedValue(L"app-zone-history", JSONHelpers::SerializeAppZoneHistory(appZoneHistoryMap));
        root.SetNamedValue(L"devices", JSONHelpers::SerializeDeviceInfos(deviceInfoMap));
        root.SetNamedValue(L"custom-zone-sets", JSONHelpers::SerializeCustomZoneSets(customZoneSetsMap));

        auto before = json::from_file(zonesSettingsFilePath);
        if (!before.has_value() || before.value().Stringify() != root.Stringify())
        {
            Trace::FancyZones::DataChanged();
        }

        json::to_file(zonesSettingsFilePath, root);
        json::to_file(appZoneHistoryFilePath, appZoneHistoryRoot);
    }

    void FancyZonesData::MigrateCustomZoneSetsFromRegistry()
    {
        std::scoped_lock lock{ dataLock };
        wchar_t key[256];
        StringCchPrintf(key, ARRAYSIZE(key), L"%s\\%s", REG_SETTINGS, L"Layouts");
        HKEY hkey;
        if (RegOpenKeyExW(HKEY_CURRENT_USER, key, 0, KEY_ALL_ACCESS, &hkey) == ERROR_SUCCESS)
        {
            BYTE data[256];
            DWORD dataSize = ARRAYSIZE(data);
            wchar_t value[256]{};
            DWORD valueLength = ARRAYSIZE(value);
            DWORD i = 0;
            while (RegEnumValueW(hkey, i++, value, &valueLength, nullptr, nullptr, reinterpret_cast<BYTE*>(&data), &dataSize) == ERROR_SUCCESS)
            {
                FancyZonesDataTypes::CustomZoneSetData zoneSetData;
                zoneSetData.name = std::wstring{ value };
                zoneSetData.type = static_cast<FancyZonesDataTypes::CustomLayoutType>(data[2]);
                // int version =  data[0] * 256 + data[1]; - Not used anymore

                GUID guid;
                auto result = CoCreateGuid(&guid);
                if (result != S_OK)
                {
                    continue;
                }
                wil::unique_cotaskmem_string guidString;
                if (!SUCCEEDED_LOG(StringFromCLSID(guid, &guidString)))
                {
                    continue;
                }

                std::wstring uuid = guidString.get();

                switch (zoneSetData.type)
                {
                case FancyZonesDataTypes::CustomLayoutType::Grid:
                {
                    int j = 5;
                    FancyZonesDataTypes::GridLayoutInfo zoneSetInfo(FancyZonesDataTypes::GridLayoutInfo::Minimal{ .rows = data[j++], .columns = data[j++] });

                    for (int row = 0; row < zoneSetInfo.rows(); row++, j += 2)
                    {
                        zoneSetInfo.rowsPercents()[row] = data[j] * 256 + data[j + 1];
                    }

                    for (int col = 0; col < zoneSetInfo.columns(); col++, j += 2)
                    {
                        zoneSetInfo.columnsPercents()[col] = data[j] * 256 + data[j + 1];
                    }

                    for (int row = 0; row < zoneSetInfo.rows(); row++)
                    {
                        for (int col = 0; col < zoneSetInfo.columns(); col++)
                        {
                            zoneSetInfo.cellChildMap()[row][col] = data[j++];
                        }
                    }
                    zoneSetData.info = zoneSetInfo;
                    break;
                }
                case FancyZonesDataTypes::CustomLayoutType::Canvas:
                {
                    FancyZonesDataTypes::CanvasLayoutInfo info;

                    int j = 5;
                    info.lastWorkAreaWidth = data[j] * 256 + data[j + 1];
                    j += 2;
                    info.lastWorkAreaHeight = data[j] * 256 + data[j + 1];
                    j += 2;

                    int count = data[j++];
                    info.zones.reserve(count);
                    while (count-- > 0)
                    {
                        int x = data[j] * 256 + data[j + 1];
                        j += 2;
                        int y = data[j] * 256 + data[j + 1];
                        j += 2;
                        int width = data[j] * 256 + data[j + 1];
                        j += 2;
                        int height = data[j] * 256 + data[j + 1];
                        j += 2;
                        info.zones.push_back(FancyZonesDataTypes::CanvasLayoutInfo::Rect{
                            x, y, width, height });
                    }
                    zoneSetData.info = info;
                    break;
                }
                default:
                    continue;
                }
                customZoneSetsMap[uuid] = zoneSetData;

                valueLength = ARRAYSIZE(value);
                dataSize = ARRAYSIZE(data);
            }
        }
    }

    void FancyZonesData::RemoveDesktopAppZoneHistory(const std::wstring& desktopId)
    {
        for (auto it = std::begin(appZoneHistoryMap); it != std::end(appZoneHistoryMap);)
        {
            auto& perDesktopData = it->second;
            for (auto desktopIt = std::begin(perDesktopData); desktopIt != std::end(perDesktopData);)
            {
                if (ExtractVirtualDesktopId(desktopIt->deviceId) == desktopId)
                {
                    desktopIt = perDesktopData.erase(desktopIt);
                }
                else
                {
                    ++desktopIt;
                }
            }

            if (perDesktopData.empty())
            {
                it = appZoneHistoryMap.erase(it);
            }
            else
            {
                ++it;
            }
        }
    }
}
