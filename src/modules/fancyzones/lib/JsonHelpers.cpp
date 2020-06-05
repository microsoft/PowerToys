#include "pch.h"
#include "JsonHelpers.h"
#include "ZoneSet.h"
#include "trace.h"

#include <common/common.h>

#include <shlwapi.h>
#include <filesystem>
#include <fstream>
#include <regex>
#include <sstream>
#include <unordered_set>

namespace
{
    // From Settings.cs
    constexpr int c_focusModelId = 0xFFFF;
    constexpr int c_rowsModelId = 0xFFFE;
    constexpr int c_columnsModelId = 0xFFFD;
    constexpr int c_gridModelId = 0xFFFC;
    constexpr int c_priorityGridModelId = 0xFFFB;
    constexpr int c_blankCustomModelId = 0xFFFA;

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

namespace JSONHelpers
{
    bool isValidGuid(const std::wstring& str)
    {
        GUID id;
        return SUCCEEDED(CLSIDFromString(str.c_str(), &id));
    }

    bool isValidDeviceId(const std::wstring& str)
    {
        std::wstring monitorName;
        std::wstring temp;
        std::vector<std::wstring> parts;
        std::wstringstream wss(str);

        /* 
         Important fix for device info that contains a '_' in the name:
         1. first search for '#'
         2. Then split the remaining string by '_' 
        */

        // Step 1: parse the name until the #, then to the '_'
        if (str.find(L'#') != std::string::npos)
        {
            std::getline(wss, temp, L'#');

            monitorName = temp;

            if (!std::getline(wss, temp, L'_'))
            {
                return false;
            }

            monitorName += L"#" + temp;
            parts.push_back(monitorName);
        }

        // Step 2: parse the rest of the id
        while (std::getline(wss, temp, L'_'))
        {
            parts.push_back(temp);
        }

        if (parts.size() != 4)
        {
            return false;
        }

        /*
         Refer to ZoneWindowUtils::GenerateUniqueId parts contain:
         1. monitor id [string]
         2. width of device [int]
         3. height of device [int]
         4. virtual desktop id (GUID) [string]
        */
        try
        {
            //check if resolution contain only digits
            for (const auto& c : parts[1])
            {
                std::stoi(std::wstring(&c));
            }
            for (const auto& c : parts[2])
            {
                std::stoi(std::wstring(&c));
            }
        }
        catch (const std::exception&)
        {
            return false;
        }

        if (!isValidGuid(parts[3]) || parts[0].empty())
        {
            return false;
        }

        return true;
    }

    json::JsonArray NumVecToJsonArray(const std::vector<int>& vec)
    {
        json::JsonArray arr;
        for (const auto& val : vec)
        {
            arr.Append(json::JsonValue::CreateNumberValue(val));
        }

        return arr;
    }

    std::vector<int> JsonArrayToNumVec(const json::JsonArray& arr)
    {
        std::vector<int> vec;
        for (const auto& val : arr)
        {
            vec.emplace_back(static_cast<int>(val.GetNumber()));
        }

        return vec;
    }

    ZoneSetLayoutType TypeFromLayoutId(int layoutID)
    {
        switch (layoutID)
        {
        case c_focusModelId:
            return ZoneSetLayoutType::Focus;
        case c_columnsModelId:
            return ZoneSetLayoutType::Columns;
        case c_rowsModelId:
            return ZoneSetLayoutType::Rows;
        case c_gridModelId:
            return ZoneSetLayoutType::Grid;
        case c_priorityGridModelId:
            return ZoneSetLayoutType::PriorityGrid;
        case c_blankCustomModelId:
            return ZoneSetLayoutType::Blank;
        default:
            return ZoneSetLayoutType::Custom;
        }
    }

    std::wstring TypeToString(ZoneSetLayoutType type)
    {
        switch (type)
        {
        case ZoneSetLayoutType::Blank:
            return L"blank";
        case ZoneSetLayoutType::Focus:
            return L"focus";
        case ZoneSetLayoutType::Columns:
            return L"columns";
        case ZoneSetLayoutType::Rows:
            return L"rows";
        case ZoneSetLayoutType::Grid:
            return L"grid";
        case ZoneSetLayoutType::PriorityGrid:
            return L"priority-grid";
        case ZoneSetLayoutType::Custom:
            return L"custom";
        default:
            return L"TypeToString_ERROR";
        }
    }

    ZoneSetLayoutType TypeFromString(const std::wstring& typeStr)
    {
        if (typeStr == L"focus")
        {
            return JSONHelpers::ZoneSetLayoutType::Focus;
        }
        else if (typeStr == L"columns")
        {
            return JSONHelpers::ZoneSetLayoutType::Columns;
        }
        else if (typeStr == L"rows")
        {
            return JSONHelpers::ZoneSetLayoutType::Rows;
        }
        else if (typeStr == L"grid")
        {
            return JSONHelpers::ZoneSetLayoutType::Grid;
        }
        else if (typeStr == L"priority-grid")
        {
            return JSONHelpers::ZoneSetLayoutType::PriorityGrid;
        }
        else if (typeStr == L"custom")
        {
            return JSONHelpers::ZoneSetLayoutType::Custom;
        }
        else
        {
            return JSONHelpers::ZoneSetLayoutType::Blank;
        }
    }

    FancyZonesData& FancyZonesDataInstance()
    {
        static FancyZonesData instance;
        return instance;
    }

    FancyZonesData::FancyZonesData()
    {
        std::wstring result = PTSettingsHelper::get_module_save_folder_location(L"FancyZones");
        jsonFilePath = result + L"\\" + std::wstring(FANCY_ZONES_DATA_FILE);
        appZoneHistoryFilePath = result + L"\\" + std::wstring(FANCY_ZONES_APP_ZONE_HISTORY_FILE);
    }

    json::JsonObject FancyZonesData::GetPersistFancyZonesJSON()
    {
        std::scoped_lock lock{ dataLock };

        std::wstring save_file_path = GetPersistFancyZonesJSONPath();

        auto result = json::from_file(save_file_path);
        if (result)
        {
            if (!result->HasKey(L"app-zone-history"))
            {
                auto appZoneHistory = json::from_file(appZoneHistoryFilePath);
                if (appZoneHistory)
                {
                    result->SetNamedValue(L"app-zone-history", appZoneHistory->GetNamedArray(L"app-zone-history"));
                }
                else
                {
                    result->SetNamedValue(L"app-zone-history", json::JsonArray());
                }
            }
            return *result;
        }
        else
        {
            return json::JsonObject();
        }
    }

    std::optional<DeviceInfoData> FancyZonesData::FindDeviceInfo(const std::wstring& zoneWindowId) const
    {
        std::scoped_lock lock{ dataLock };
        auto it = deviceInfoMap.find(zoneWindowId);
        return it != end(deviceInfoMap) ? std::optional{ it->second } : std::nullopt;
    }

    std::optional<CustomZoneSetData> FancyZonesData::FindCustomZoneSet(const std::wstring& guid) const
    {
        std::scoped_lock lock{ dataLock };
        auto it = customZoneSetsMap.find(guid);
        return it != end(customZoneSetsMap) ? std::optional{ it->second } : std::nullopt;
    }

    void FancyZonesData::AddDevice(const std::wstring& deviceId)
    {
        std::scoped_lock lock{ dataLock };
        if (!deviceInfoMap.contains(deviceId))
        {
            // Creates default entry in map when ZoneWindow is created
            deviceInfoMap[deviceId] = DeviceInfoData{ ZoneSetData{ L"null", ZoneSetLayoutType::Blank } };
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
        if (destInfo.activeZoneSet.type == ZoneSetLayoutType::Blank)
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

                        // if there is another instance placed don't erase history
                        for (auto placedWindow : data->processIdToHandleMap)
                        {
                            if (IsWindow(placedWindow.second))
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
                    for (auto placedWindow : data.processIdToHandleMap)
                    {
                        if (IsWindow(placedWindow.second) && processId != placedWindow.first)
                        {
                            return false;
                        }
                    }
                    // application already has history on this desktop, but zone (or zone layout) has changed
                    data.processIdToHandleMap[processId] = window;
                    data.zoneSetUuid = zoneSetId;
                    data.zoneIndexSet = zoneIndexSet;
                    SaveFancyZonesData();
                    return true;
                }
            }
        }

        std::map<DWORD, HWND> processIdToHandleMap{};
        processIdToHandleMap[processId] = window;
        AppZoneHistoryData data{ .processIdToHandleMap = processIdToHandleMap,
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
            appZoneHistoryMap[processPath] = std::vector<AppZoneHistoryData>{ data };
        }

        SaveFancyZonesData();
        return true;
    }

    void FancyZonesData::SetActiveZoneSet(const std::wstring& deviceId, const ZoneSetData& data)
    {
        std::scoped_lock lock{ dataLock };
        auto it = deviceInfoMap.find(deviceId);
        if (it != deviceInfoMap.end())
        {
            it->second.activeZoneSet = data;
        }
    }

    void FancyZonesData::SerializeDeviceInfoToTmpFile(const DeviceInfoJSON& deviceInfo, std::wstring_view tmpFilePath) const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonObject deviceInfoJson = DeviceInfoJSON::ToJson(deviceInfo);
        json::to_file(tmpFilePath, deviceInfoJson);
    }

    void FancyZonesData::ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::scoped_lock lock{ dataLock };
        if (std::filesystem::exists(tmpFilePath))
        {
            if (auto zoneSetJson = json::from_file(tmpFilePath); zoneSetJson.has_value())
            {
                if (auto deviceInfo = DeviceInfoJSON::FromJson(zoneSetJson.value()); deviceInfo.has_value())
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
                    if (auto customZoneSet = CustomZoneSetJSON::FromJson(customZoneSetJson.value()); customZoneSet.has_value())
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

    bool FancyZonesData::ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON)
    {
        std::scoped_lock lock{ dataLock };
        try
        {
            auto appLastZones = fancyZonesDataJSON.GetNamedArray(L"app-zone-history");

            for (uint32_t i = 0; i < appLastZones.Size(); ++i)
            {
                json::JsonObject appLastZone = appLastZones.GetObjectAt(i);
                if (auto appZoneHistory = AppZoneHistoryJSON::FromJson(appLastZone); appZoneHistory.has_value())
                {
                    appZoneHistoryMap[appZoneHistory->appPath] = std::move(appZoneHistory->data);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    json::JsonArray FancyZonesData::SerializeAppZoneHistory() const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonArray appHistoryArray;

        for (const auto& [appPath, appZoneHistoryData] : appZoneHistoryMap)
        {
            appHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, appZoneHistoryData }));
        }

        return appHistoryArray;
    }

    bool FancyZonesData::ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON)
    {
        std::scoped_lock lock{ dataLock };
        try
        {
            auto devices = fancyZonesDataJSON.GetNamedArray(L"devices");

            for (uint32_t i = 0; i < devices.Size(); ++i)
            {
                if (auto device = DeviceInfoJSON::DeviceInfoJSON::FromJson(devices.GetObjectAt(i)); device.has_value())
                {
                    deviceInfoMap[device->deviceId] = std::move(device->data);
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    json::JsonArray FancyZonesData::SerializeDeviceInfos() const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonArray DeviceInfosJSON{};

        for (const auto& [deviceID, deviceData] : deviceInfoMap)
        {
            if (deviceData.activeZoneSet.type != ZoneSetLayoutType::Blank)
            {
                DeviceInfosJSON.Append(DeviceInfoJSON::DeviceInfoJSON::ToJson(DeviceInfoJSON{ deviceID, deviceData }));
            }
        }

        return DeviceInfosJSON;
    }

    bool FancyZonesData::ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON)
    {
        std::scoped_lock lock{ dataLock };
        try
        {
            auto customZoneSets = fancyZonesDataJSON.GetNamedArray(L"custom-zone-sets");

            for (uint32_t i = 0; i < customZoneSets.Size(); ++i)
            {
                if (auto zoneSet = CustomZoneSetJSON::FromJson(customZoneSets.GetObjectAt(i)); zoneSet.has_value())
                {
                    customZoneSetsMap[zoneSet->uuid] = std::move(zoneSet->data);
                }
            }

            return true;
        }
        catch (const winrt::hresult_error&)
        {
            return false;
        }
    }

    json::JsonArray FancyZonesData::SerializeCustomZoneSets() const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonArray customZoneSetsJSON{};

        for (const auto& [zoneSetId, zoneSetData] : customZoneSetsMap)
        {
            customZoneSetsJSON.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ zoneSetId, zoneSetData }));
        }

        return customZoneSetsJSON;
    }

    void FancyZonesData::CustomZoneSetsToJsonFile(std::wstring_view filePath) const
    {
        std::scoped_lock lock{ dataLock };
        const auto& customZoneSetsJson = SerializeCustomZoneSets();
        json::JsonObject root{};
        root.SetNamedValue(L"custom-zone-sets", customZoneSetsJson);
        json::to_file(filePath, root);
    }

    void FancyZonesData::LoadFancyZonesData()
    {
        std::scoped_lock lock{ dataLock };
        std::wstring jsonFilePath = GetPersistFancyZonesJSONPath();

        if (!std::filesystem::exists(jsonFilePath))
        {
            MigrateCustomZoneSetsFromRegistry();

            SaveFancyZonesData();
        }
        else
        {
            json::JsonObject fancyZonesDataJSON = GetPersistFancyZonesJSON();

            ParseAppZoneHistory(fancyZonesDataJSON);
            ParseDeviceInfos(fancyZonesDataJSON);
            ParseCustomZoneSets(fancyZonesDataJSON);
        }
    }

    void FancyZonesData::SaveFancyZonesData() const
    {
        std::scoped_lock lock{ dataLock };
        json::JsonObject root{};
        json::JsonObject appZoneHistoryRoot{};

        appZoneHistoryRoot.SetNamedValue(L"app-zone-history", SerializeAppZoneHistory());
        root.SetNamedValue(L"devices", SerializeDeviceInfos());
        root.SetNamedValue(L"custom-zone-sets", SerializeCustomZoneSets());

        auto before = json::from_file(jsonFilePath);
        if (!before.has_value() || before.value().Stringify() != root.Stringify())
        {
            Trace::FancyZones::DataChanged();
        }

        json::to_file(jsonFilePath, root);
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
                CustomZoneSetData zoneSetData;
                zoneSetData.name = std::wstring{ value };
                zoneSetData.type = static_cast<CustomLayoutType>(data[2]);
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
                case CustomLayoutType::Grid:
                {
                    int j = 5;
                    GridLayoutInfo zoneSetInfo(GridLayoutInfo::Minimal{ .rows = data[j++], .columns = data[j++] });

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
                case CustomLayoutType::Canvas:
                {
                    CanvasLayoutInfo info;

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
                        info.zones.push_back(CanvasLayoutInfo::Rect{
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

    json::JsonObject ZoneSetData::ToJson(const ZoneSetData& zoneSet)
    {
        json::JsonObject result{};

        result.SetNamedValue(L"uuid", json::value(zoneSet.uuid));
        result.SetNamedValue(L"type", json::value(TypeToString(zoneSet.type)));

        return result;
    }

    std::optional<ZoneSetData> ZoneSetData::FromJson(const json::JsonObject& zoneSet)
    {
        try
        {
            ZoneSetData zoneSetData;
            zoneSetData.uuid = zoneSet.GetNamedString(L"uuid");
            zoneSetData.type = TypeFromString(std::wstring{ zoneSet.GetNamedString(L"type") });

            if (!isValidGuid(zoneSetData.uuid))
            {
                return std::nullopt;
            }

            return zoneSetData;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject AppZoneHistoryJSON::ToJson(const AppZoneHistoryJSON& appZoneHistory)
    {
        json::JsonObject result{};

        result.SetNamedValue(L"app-path", json::value(appZoneHistory.appPath));

        json::JsonArray appHistoryArray;
        for (const auto& data : appZoneHistory.data)
        {
            json::JsonObject desktopData;
            json::JsonArray jsonIndexSet;
            for (int index : data.zoneIndexSet)
            {
                jsonIndexSet.Append(json::value(index));
            }

            desktopData.SetNamedValue(L"zone-index-set", jsonIndexSet);
            desktopData.SetNamedValue(L"device-id", json::value(data.deviceId));
            desktopData.SetNamedValue(L"zoneset-uuid", json::value(data.zoneSetUuid));

            appHistoryArray.Append(desktopData);
        }

        result.SetNamedValue(L"history", appHistoryArray);

        return result;
    }

    std::optional<AppZoneHistoryData> ParseSingleAppZoneHistoryItem(const json::JsonObject& json)
    {
        AppZoneHistoryData data;
        if (json.HasKey(L"zone-index-set"))
        {
            data.zoneIndexSet = {};
            for (auto& value : json.GetNamedArray(L"zone-index-set"))
            {
                data.zoneIndexSet.push_back(static_cast<int>(value.GetNumber()));
            }
        }
        else if (json.HasKey(L"zone-index"))
        {
            data.zoneIndexSet = { static_cast<int>(json.GetNamedNumber(L"zone-index")) };
        }

        data.deviceId = json.GetNamedString(L"device-id");
        data.zoneSetUuid = json.GetNamedString(L"zoneset-uuid");

        if (!isValidGuid(data.zoneSetUuid) || !isValidDeviceId(data.deviceId))
        {
            return std::nullopt;
        }

        return data;
    }

    std::optional<AppZoneHistoryJSON> AppZoneHistoryJSON::FromJson(const json::JsonObject& zoneSet)
    {
        try
        {
            AppZoneHistoryJSON result;

            result.appPath = zoneSet.GetNamedString(L"app-path");
            if (zoneSet.HasKey(L"history"))
            {
                auto appHistoryArray = zoneSet.GetNamedArray(L"history");
                for (uint32_t i = 0; i < appHistoryArray.Size(); ++i)
                {
                    json::JsonObject json = appHistoryArray.GetObjectAt(i);
                    if (auto data = ParseSingleAppZoneHistoryItem(json); data.has_value())
                    {
                        result.data.push_back(std::move(data.value()));
                    }
                }
            }
            else
            {
                // handle previous file format, with single desktop layout information per application
                if (auto data = ParseSingleAppZoneHistoryItem(zoneSet); data.has_value())
                {
                    result.data.push_back(std::move(data.value()));
                }
            }
            if (result.data.empty())
            {
                return std::nullopt;
            }

            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject DeviceInfoJSON::ToJson(const DeviceInfoJSON& device)
    {
        json::JsonObject result{};

        result.SetNamedValue(L"device-id", json::value(device.deviceId));
        result.SetNamedValue(L"active-zoneset", ZoneSetData::ToJson(device.data.activeZoneSet));
        result.SetNamedValue(L"editor-show-spacing", json::value(device.data.showSpacing));
        result.SetNamedValue(L"editor-spacing", json::value(device.data.spacing));
        result.SetNamedValue(L"editor-zone-count", json::value(device.data.zoneCount));

        return result;
    }

    std::optional<DeviceInfoJSON> DeviceInfoJSON::FromJson(const json::JsonObject& device)
    {
        try
        {
            DeviceInfoJSON result;

            result.deviceId = device.GetNamedString(L"device-id");
            if (!isValidDeviceId(result.deviceId))
            {
                return std::nullopt;
            }

            if (auto zoneSet = ZoneSetData::FromJson(device.GetNamedObject(L"active-zoneset")); zoneSet.has_value())
            {
                result.data.activeZoneSet = std::move(zoneSet.value());
            }
            else
            {
                return std::nullopt;
            }

            result.data.showSpacing = device.GetNamedBoolean(L"editor-show-spacing");
            result.data.spacing = static_cast<int>(device.GetNamedNumber(L"editor-spacing"));
            result.data.zoneCount = static_cast<int>(
                device.GetNamedNumber(L"editor-zone-count"));

            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject CanvasLayoutInfo::ToJson(const CanvasLayoutInfo& canvasInfo)
    {
        json::JsonObject infoJson{};
        infoJson.SetNamedValue(L"ref-width", json::value(canvasInfo.lastWorkAreaWidth));
        infoJson.SetNamedValue(L"ref-height", json::value(canvasInfo.lastWorkAreaHeight));

        json::JsonArray zonesJson;

        for (const auto& [x, y, width, height] : canvasInfo.zones)
        {
            json::JsonObject zoneJson;
            zoneJson.SetNamedValue(L"X", json::value(x));
            zoneJson.SetNamedValue(L"Y", json::value(y));
            zoneJson.SetNamedValue(L"width", json::value(width));
            zoneJson.SetNamedValue(L"height", json::value(height));
            zonesJson.Append(zoneJson);
        }
        infoJson.SetNamedValue(L"zones", zonesJson);
        return infoJson;
    }

    std::optional<CanvasLayoutInfo> CanvasLayoutInfo::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            CanvasLayoutInfo info;
            info.lastWorkAreaWidth = static_cast<int>(infoJson.GetNamedNumber(L"ref-width"));
            info.lastWorkAreaHeight = static_cast<int>(infoJson.GetNamedNumber(L"ref-height"));

            json::JsonArray zonesJson = infoJson.GetNamedArray(L"zones");
            uint32_t size = zonesJson.Size();
            info.zones.reserve(size);
            for (uint32_t i = 0; i < size; ++i)
            {
                json::JsonObject zoneJson = zonesJson.GetObjectAt(i);
                const int x = static_cast<int>(zoneJson.GetNamedNumber(L"X"));
                const int y = static_cast<int>(zoneJson.GetNamedNumber(L"Y"));
                const int width = static_cast<int>(zoneJson.GetNamedNumber(L"width"));
                const int height = static_cast<int>(zoneJson.GetNamedNumber(L"height"));
                CanvasLayoutInfo::Rect zone{ x, y, width, height };
                info.zones.push_back(zone);
            }
            return info;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    GridLayoutInfo::GridLayoutInfo(const Minimal& info) :
        m_rows(info.rows),
        m_columns(info.columns)
    {
        m_rowsPercents.resize(m_rows, 0);
        m_columnsPercents.resize(m_columns, 0);
        m_cellChildMap.resize(m_rows, {});
        for (auto& cellRow : m_cellChildMap)
        {
            cellRow.resize(m_columns, 0);
        }
    }

    GridLayoutInfo::GridLayoutInfo(const Full& info) :
        m_rows(info.rows),
        m_columns(info.columns),
        m_rowsPercents(info.rowsPercents),
        m_columnsPercents(info.columnsPercents),
        m_cellChildMap(info.cellChildMap)
    {
        m_rowsPercents.resize(m_rows, 0);
        m_columnsPercents.resize(m_columns, 0);
        m_cellChildMap.resize(m_rows, {});
        for (auto& cellRow : m_cellChildMap)
        {
            cellRow.resize(m_columns, 0);
        }
    }

    json::JsonObject GridLayoutInfo::ToJson(const GridLayoutInfo& gridInfo)
    {
        json::JsonObject infoJson;
        infoJson.SetNamedValue(L"rows", json::value(gridInfo.m_rows));
        infoJson.SetNamedValue(L"columns", json::value(gridInfo.m_columns));
        infoJson.SetNamedValue(L"rows-percentage", NumVecToJsonArray(gridInfo.m_rowsPercents));
        infoJson.SetNamedValue(L"columns-percentage", NumVecToJsonArray(gridInfo.m_columnsPercents));

        json::JsonArray cellChildMapJson;
        for (int i = 0; i < gridInfo.m_cellChildMap.size(); ++i)
        {
            cellChildMapJson.Append(NumVecToJsonArray(gridInfo.m_cellChildMap[i]));
        }
        infoJson.SetNamedValue(L"cell-child-map", cellChildMapJson);

        return infoJson;
    }

    std::optional<GridLayoutInfo> GridLayoutInfo::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            GridLayoutInfo info(GridLayoutInfo::Minimal{});

            info.m_rows = static_cast<int>(infoJson.GetNamedNumber(L"rows"));
            info.m_columns = static_cast<int>(infoJson.GetNamedNumber(L"columns"));

            json::JsonArray rowsPercentage = infoJson.GetNamedArray(L"rows-percentage");
            json::JsonArray columnsPercentage = infoJson.GetNamedArray(L"columns-percentage");
            json::JsonArray cellChildMap = infoJson.GetNamedArray(L"cell-child-map");

            if (rowsPercentage.Size() != info.m_rows || columnsPercentage.Size() != info.m_columns || cellChildMap.Size() != info.m_rows)
            {
                return std::nullopt;
            }

            info.m_rowsPercents = JsonArrayToNumVec(rowsPercentage);
            info.m_columnsPercents = JsonArrayToNumVec(columnsPercentage);
            for (const auto& cellsRow : cellChildMap)
            {
                const auto cellsArray = cellsRow.GetArray();
                if (cellsArray.Size() != info.m_columns)
                {
                    return std::nullopt;
                }
                info.cellChildMap().push_back(JsonArrayToNumVec(cellsArray));
            }

            return info;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject CustomZoneSetJSON::ToJson(const CustomZoneSetJSON& customZoneSet)
    {
        json::JsonObject result{};

        result.SetNamedValue(L"uuid", json::value(customZoneSet.uuid));
        result.SetNamedValue(L"name", json::value(customZoneSet.data.name));
        switch (customZoneSet.data.type)
        {
        case CustomLayoutType::Canvas:
        {
            result.SetNamedValue(L"type", json::value(L"canvas"));

            CanvasLayoutInfo info = std::get<CanvasLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(L"info", CanvasLayoutInfo::ToJson(info));

            break;
        }
        case CustomLayoutType::Grid:
        {
            result.SetNamedValue(L"type", json::value(L"grid"));

            GridLayoutInfo gridInfo = std::get<GridLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(L"info", GridLayoutInfo::ToJson(gridInfo));

            break;
        }
        }

        return result;
    }

    std::optional<CustomZoneSetJSON> CustomZoneSetJSON::FromJson(const json::JsonObject& customZoneSet)
    {
        try
        {
            CustomZoneSetJSON result;

            result.uuid = customZoneSet.GetNamedString(L"uuid");
            if (!isValidGuid(result.uuid))
            {
                return std::nullopt;
            }

            result.data.name = customZoneSet.GetNamedString(L"name");

            json::JsonObject infoJson = customZoneSet.GetNamedObject(L"info");
            std::wstring zoneSetType = std::wstring{ customZoneSet.GetNamedString(L"type") };
            if (zoneSetType.compare(L"canvas") == 0)
            {
                if (auto info = CanvasLayoutInfo::FromJson(infoJson); info.has_value())
                {
                    result.data.type = CustomLayoutType::Canvas;
                    result.data.info = std::move(info.value());
                }
                else
                {
                    return std::nullopt;
                }
            }
            else if (zoneSetType.compare(L"grid") == 0)
            {
                if (auto info = GridLayoutInfo::FromJson(infoJson); info.has_value())
                {
                    result.data.type = CustomLayoutType::Grid;
                    result.data.info = std::move(info.value());
                }
                else
                {
                    return std::nullopt;
                }
            }
            else
            {
                return std::nullopt;
            }

            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }
}
