#include "pch.h"
#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"
#include "JsonHelpers.h"
#include "ZoneSet.h"
#include "Settings.h"

#include <common/common.h>
#include <common/json.h>

#include <shlwapi.h>
#include <filesystem>
#include <fstream>
#include <optional>
#include <regex>
#include <sstream>
#include <unordered_set>

// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t FancyZonesStr[] = L"FancyZones";
    const wchar_t LayoutsStr[] = L"Layouts";
    const wchar_t NullStr[] = L"null";

    const wchar_t FancyZonesDataFile[] = L"zones-settings.json";
    const wchar_t FancyZonesAppZoneHistoryFile[] = L"app-zone-history.json";
    const wchar_t DefaultGuid[] = L"{00000000-0000-0000-0000-000000000000}";
    const wchar_t RegistryPath[] = L"Software\\SuperFancyZones";

    const wchar_t ActiveZoneSetsTmpFileName[] = L"FancyZonesActiveZoneSets.json";
    const wchar_t AppliedZoneSetsTmpFileName[] = L"FancyZonesAppliedZoneSets.json";
    const wchar_t DeletedCustomZoneSetsTmpFileName[] = L"FancyZonesDeletedCustomZoneSets.json";
}

namespace
{
    std::wstring ExtractVirtualDesktopId(const std::wstring& deviceId)
    {
        // Format: <device-id>_<resolution>_<virtual-desktop-id>
        return deviceId.substr(deviceId.rfind('_') + 1);
    }

    const std::wstring& GetTempDirPath()
    {
        static std::wstring tmpDirPath;
        static std::once_flag flag;

        std::call_once(flag, []() {
            wchar_t buffer[MAX_PATH];

            auto charsWritten = GetTempPath(MAX_PATH, buffer);
            if (charsWritten > MAX_PATH || (charsWritten == 0))
            {
                abort();
            }

            tmpDirPath = std::wstring{ buffer };
        });

        return tmpDirPath;
    }
}

FancyZonesData& FancyZonesDataInstance()
{
    static FancyZonesData instance;
    return instance;
}

FancyZonesData::FancyZonesData()
{
    std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::FancyZonesStr);
    zonesSettingsFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesDataFile);
    appZoneHistoryFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesAppZoneHistoryFile);

    activeZoneSetTmpFileName = GetTempDirPath() + NonLocalizable::ActiveZoneSetsTmpFileName;
    appliedZoneSetTmpFileName = GetTempDirPath() + NonLocalizable::AppliedZoneSetsTmpFileName;
    deletedCustomZoneSetsTmpFileName = GetTempDirPath() + NonLocalizable::DeletedCustomZoneSetsTmpFileName;
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

void FancyZonesData::AddDevice(const std::wstring& deviceId)
{
    using namespace FancyZonesDataTypes;

    std::scoped_lock lock{ dataLock };
    if (!deviceInfoMap.contains(deviceId))
    {
        // Creates default entry in map when ZoneWindow is created
        GUID guid;
        auto result{ CoCreateGuid(&guid) };
        wil::unique_cotaskmem_string guidString;
        if (result == S_OK && SUCCEEDED(StringFromCLSID(guid, &guidString)))
        {
            DeviceInfoData defaultDeviceInfoData{ ZoneSetData{ guidString.get(), ZoneSetLayoutType::PriorityGrid }, true, 16, 3 };
            deviceInfoMap[deviceId] = std::move(defaultDeviceInfoData);
        }
        else
        {
            deviceInfoMap[deviceId] = DeviceInfoData{ ZoneSetData{ NonLocalizable::NullStr, ZoneSetLayoutType::Blank } };
        }
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
    deviceInfoMap[destination] = deviceInfoMap[source];
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
            if (ExtractVirtualDesktopId(data.deviceId) == NonLocalizable::DefaultGuid)
            {
                data.deviceId = replaceDesktopId(data.deviceId);
            }
        }
    }
    std::vector<std::wstring> toReplace{};
    for (const auto& [id, data] : deviceInfoMap)
    {
        if (ExtractVirtualDesktopId(id) == NonLocalizable::DefaultGuid)
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

std::vector<size_t> FancyZonesData::GetAppLastZoneIndexSet(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId) const
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
                    size_t windowZoneStamp = reinterpret_cast<size_t>(::GetProp(window, ZonedWindowProperties::PropertyMultipleZoneID));
                    for (auto placedWindow : data->processIdToHandleMap)
                    {
                        size_t placedWindowZoneStamp = reinterpret_cast<size_t>(::GetProp(placedWindow.second, ZonedWindowProperties::PropertyMultipleZoneID));
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

bool FancyZonesData::SetAppLastZones(HWND window, const std::wstring& deviceId, const std::wstring& zoneSetId, const std::vector<size_t>& zoneIndexSet)
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

bool FancyZonesData::SerializeDeviceInfoToTmpFile(const std::wstring& uniqueId) const
{
    const auto deviceInfo = FindDeviceInfo(uniqueId);
    if (!deviceInfo.has_value())
    {
        return false;
    }

    JSONHelpers::DeviceInfoJSON deviceInfoJson{ uniqueId, *deviceInfo };
    JSONHelpers::SerializeDeviceInfoToTmpFile(deviceInfoJson, activeZoneSetTmpFileName);

    return true;
}

void FancyZonesData::ParseDataFromTmpFiles()
{
    ParseDeviceInfoFromTmpFile(activeZoneSetTmpFileName);
    ParseDeletedCustomZoneSetsFromTmpFile(deletedCustomZoneSetsTmpFileName);
    ParseCustomZoneSetFromTmpFile(appliedZoneSetTmpFileName);
    SaveFancyZonesData();
}

void FancyZonesData::ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath)
{
    std::scoped_lock lock{ dataLock };
    const auto& deviceInfo = JSONHelpers::ParseDeviceInfoFromTmpFile(tmpFilePath);

    if (deviceInfo)
    {
        deviceInfoMap[deviceInfo->deviceId] = std::move(deviceInfo->data);
    }
}

void FancyZonesData::ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath)
{
    std::scoped_lock lock{ dataLock };
    const auto& customZoneSet = JSONHelpers::ParseCustomZoneSetFromTmpFile(tmpFilePath);

    if (customZoneSet)
    {
        customZoneSetsMap[customZoneSet->uuid] = std::move(customZoneSet->data);
    }
}

void FancyZonesData::ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath)
{
    std::scoped_lock lock{ dataLock };
    const auto& deletedCustomZoneSets = JSONHelpers::ParseDeletedCustomZoneSetsFromTmpFile(tmpFilePath);
    for (const auto& zoneSet : deletedCustomZoneSets)
    {
        customZoneSetsMap.erase(zoneSet);
    }
}

json::JsonObject FancyZonesData::GetPersistFancyZonesJSON()
{
    return JSONHelpers::GetPersistFancyZonesJSON(zonesSettingsFileName, appZoneHistoryFileName);
}

void FancyZonesData::LoadFancyZonesData()
{
    if (!std::filesystem::exists(zonesSettingsFileName))
    {
        MigrateCustomZoneSetsFromRegistry();
        SaveFancyZonesData();
    }
    else
    {
        json::JsonObject fancyZonesDataJSON = GetPersistFancyZonesJSON();

        appZoneHistoryMap = JSONHelpers::ParseAppZoneHistory(fancyZonesDataJSON);
        deviceInfoMap = JSONHelpers::ParseDeviceInfos(fancyZonesDataJSON);
        customZoneSetsMap = JSONHelpers::ParseCustomZoneSets(fancyZonesDataJSON);
    }
}

void FancyZonesData::SaveFancyZonesData() const
{
    std::scoped_lock lock{ dataLock };
    JSONHelpers::SaveFancyZonesData(zonesSettingsFileName,
									appZoneHistoryFileName,
									deviceInfoMap,
									customZoneSetsMap,
									appZoneHistoryMap);
}

void FancyZonesData::MigrateCustomZoneSetsFromRegistry()
{
    std::scoped_lock lock{ dataLock };
    wchar_t key[256];
    StringCchPrintf(key, ARRAYSIZE(key), L"%s\\%s", NonLocalizable::RegistryPath, NonLocalizable::LayoutsStr);
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

            GUID guid;
            auto result = CoCreateGuid(&guid);
            if (result != S_OK)
            {
                continue;
            }
            wil::unique_cotaskmem_string guidString;
            if (!SUCCEEDED(StringFromCLSID(guid, &guidString)))
            {
                continue;
            }

            std::wstring uuid = guidString.get();

            switch (zoneSetData.type)
            {
            case FancyZonesDataTypes::CustomLayoutType::Grid:
            {
                // Visit https://github.com/microsoft/PowerToys/blob/v0.14.0/src/modules/fancyzones/editor/FancyZonesEditor/Models/GridLayoutModel.cs#L183
                // To see how custom Grid layout was packed in registry
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
                // Visit https://github.com/microsoft/PowerToys/blob/v0.14.0/src/modules/fancyzones/editor/FancyZonesEditor/Models/CanvasLayoutModel.cs#L128
                // To see how custom Canvas layout was packed in registry
                int j = 5;
                FancyZonesDataTypes::CanvasLayoutInfo info;
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
