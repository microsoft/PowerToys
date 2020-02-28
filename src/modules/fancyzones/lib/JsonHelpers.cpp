#include "pch.h"
#include "JsonHelpers.h"
#include "RegistryHelpers.h"
#include "ZoneSet.h"
#include "trace.h"

#include <common/common.h>

#include <shlwapi.h>
#include <filesystem>
#include <fstream>
#include <regex>

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
    const wchar_t* DEFAULT_GUID = L"{00000000-0000-0000-0000-000000000000}";

    std::wstring ExtractVirtualDesktopId(const std::wstring& deviceId)
    {
        // Format: <device-id>_<resolution>_<virtual-desktop-id>
        return deviceId.substr(deviceId.rfind('_') + 1);
    }
}

namespace JSONHelpers
{
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
    }

    json::JsonObject FancyZonesData::GetPersistFancyZonesJSON()
    {
        std::scoped_lock lock{ dataLock };

        std::wstring save_file_path = GetPersistFancyZonesJSONPath();

        auto result = json::from_file(save_file_path);
        if (result)
        {
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

    std::optional<CustomZoneSetData> FancyZonesData::FindCustomZoneSet(const std::wstring& guuid) const
    {
        std::scoped_lock lock{ dataLock };
        auto it = customZoneSetsMap.find(guuid);
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

    bool FancyZonesData::RemoveDevicesByVirtualDesktopId(const std::wstring& virtualDesktopId)
    {
        std::scoped_lock lock{ dataLock };
        if (virtualDesktopId == DEFAULT_GUID)
        {
            return false;
        }
        bool modified{ false };
        for (auto it = deviceInfoMap.begin(); it != deviceInfoMap.end();)
        {
            if (ExtractVirtualDesktopId(it->first) == virtualDesktopId)
            {
                it = deviceInfoMap.erase(it);
                modified = true;
            }
            else
            {
                ++it;
            }
        }
        return modified;
    }

    void FancyZonesData::CloneDeviceInfo(const std::wstring& source, const std::wstring& destination)
    {
        std::scoped_lock lock{ dataLock };
        // Clone information from source device if destination device is uninitialized (Blank).
        auto& destInfo = deviceInfoMap[destination];
        if (destInfo.activeZoneSet.type == ZoneSetLayoutType::Blank)
        {
            destInfo = deviceInfoMap[source];
        }
    }

    int FancyZonesData::GetAppLastZoneIndex(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId) const
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != appZoneHistoryMap.end())
            {
                const auto& data = history->second;
                if (data.zoneSetUuid == zoneSetId && data.deviceId == deviceId)
                {
                    return history->second.zoneIndex;
                }
            }
        }

        return -1;
    }

    bool FancyZonesData::RemoveAppLastZone(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId)
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (!processPath.empty())
        {
            auto history = appZoneHistoryMap.find(processPath);
            if (history != appZoneHistoryMap.end())
            {
                const auto& data = history->second;
                if (data.zoneSetUuid == zoneSetId && data.deviceId == deviceId)
                {
                    appZoneHistoryMap.erase(processPath);
                    SaveFancyZonesData();
                    return true;
                }
            }
        }

        return false;
    }

    bool FancyZonesData::SetAppLastZone(HWND window, const std::wstring& deviceId, const std::wstring& zoneSetId, int zoneIndex)
    {
        std::scoped_lock lock{ dataLock };
        auto processPath = get_process_path(window);
        if (processPath.empty())
        {
            return false;
        }

        appZoneHistoryMap[processPath] = AppZoneHistoryData{ .zoneSetUuid = zoneSetId, .deviceId = deviceId, .zoneIndex = zoneIndex };
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
                    activeDeviceId = deviceInfo->deviceId;
                    deviceInfoMap[activeDeviceId] = std::move(deviceInfo->data);
                    DeleteTmpFile(tmpFilePath);
                }
            }
        }
        else
        {
            activeDeviceId.clear();
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
            TmpMigrateAppliedZoneSetsFromRegistry();

            // Custom zone sets have to be migrated after applied zone sets!
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

        root.SetNamedValue(L"app-zone-history", SerializeAppZoneHistory());
        root.SetNamedValue(L"devices", SerializeDeviceInfos());
        root.SetNamedValue(L"custom-zone-sets", SerializeCustomZoneSets());

        auto before = json::from_file(jsonFilePath);
        if (!before.has_value() || before.value().Stringify() != root.Stringify())
        {
            Trace::FancyZones::DataChanged();
        }

        json::to_file(jsonFilePath, root);
    }

    void FancyZonesData::TmpMigrateAppliedZoneSetsFromRegistry()
    {
        std::wregex ex(L"^[0-9]{3,4}_[0-9]{3,4}$");

        std::scoped_lock lock{ dataLock };
        wchar_t key[256];
        StringCchPrintf(key, ARRAYSIZE(key), L"%s", RegistryHelpers::REG_SETTINGS);
        HKEY hkey;
        if (RegOpenKeyExW(HKEY_CURRENT_USER, key, 0, KEY_ALL_ACCESS, &hkey) == ERROR_SUCCESS)
        {
            wchar_t resolutionKey[256]{};
            DWORD resolutionKeyLength = ARRAYSIZE(resolutionKey);
            DWORD i = 0;
            while (RegEnumKeyW(hkey, i++, resolutionKey, resolutionKeyLength) == ERROR_SUCCESS)
            {
                std::wstring resolution{ resolutionKey };
                wchar_t appliedZoneSetskey[256];
                StringCchPrintf(appliedZoneSetskey, ARRAYSIZE(appliedZoneSetskey), L"%s\\%s", RegistryHelpers::REG_SETTINGS, resolutionKey);
                HKEY appliedZoneSetsHkey;
                if (std::regex_match(resolution, ex) && RegOpenKeyExW(HKEY_CURRENT_USER, appliedZoneSetskey, 0, KEY_ALL_ACCESS, &appliedZoneSetsHkey) == ERROR_SUCCESS)
                {
                    ZoneSetPersistedDataOLD data;
                    DWORD dataSize = sizeof(data);
                    wchar_t value[256]{};
                    DWORD valueLength = ARRAYSIZE(value);
                    DWORD i = 0;

                    while (RegEnumValueW(appliedZoneSetsHkey, i++, value, &valueLength, nullptr, nullptr, reinterpret_cast<BYTE*>(&data), &dataSize) == ERROR_SUCCESS)
                    {
                        ZoneSetData appliedZoneSetData;
                        appliedZoneSetData.type = TypeFromLayoutId(data.LayoutId);
                        if (appliedZoneSetData.type != ZoneSetLayoutType::Custom)
                        {
                            appliedZoneSetData.uuid = std::wstring{ value };
                        }
                        else
                        {
                            // uuid is changed later to actual uuid when migrating custom zone sets
                            appliedZoneSetData.uuid = std::to_wstring(data.LayoutId);
                        }
                        appliedZoneSetsMap[value] = appliedZoneSetData;
                        dataSize = sizeof(data);
                        valueLength = ARRAYSIZE(value);
                    }
                }
                resolutionKeyLength = ARRAYSIZE(resolutionKey);
            }
        }
    }

    void FancyZonesData::MigrateCustomZoneSetsFromRegistry()
    {
        std::scoped_lock lock{ dataLock };
        wchar_t key[256];
        StringCchPrintf(key, ARRAYSIZE(key), L"%s\\%s", RegistryHelpers::REG_SETTINGS, L"Layouts");
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

                std::wstring uuid = std::to_wstring(data[3] * 256 + data[4]);
                auto it = std::find_if(appliedZoneSetsMap.begin(), appliedZoneSetsMap.end(), [&uuid](std::pair<std::wstring, ZoneSetData> zoneSetMap) {
                    return zoneSetMap.second.uuid.compare(uuid) == 0;
                });

                if (it != appliedZoneSetsMap.end())
                {
                    it->second.uuid = uuid = it->first;
                }
                switch (zoneSetData.type)
                {
                case CustomLayoutType::Grid: {
                    int j = 5;
                    GridLayoutInfo zoneSetInfo(GridLayoutInfo::Minimal{ .rows = data[j++], .columns = data[j++] });

                    for (int row = 0; row < zoneSetInfo.rows(); row++)
                    {
                        zoneSetInfo.rowsPercents()[row] = data[j++] * 256 + data[j++];
                    }

                    for (int col = 0; col < zoneSetInfo.columns(); col++)
                    {
                        zoneSetInfo.columnsPercents()[col] = data[j++] * 256 + data[j++];
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
                case CustomLayoutType::Canvas: {
                    CanvasLayoutInfo info;

                    int j = 5;
                    info.referenceWidth = data[j] * 256 + data[j + 1];
                    j += 2;
                    info.referenceHeight = data[j] * 256 + data[j + 1];
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
                    abort(); // TODO(stefan): Exception safety
                }
                customZoneSetsMap[uuid] = zoneSetData;

                valueLength = ARRAYSIZE(value);
                dataSize = ARRAYSIZE(data);
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
        result.SetNamedValue(L"zone-index", json::value(appZoneHistory.data.zoneIndex));
        result.SetNamedValue(L"device-id", json::value(appZoneHistory.data.deviceId));
        result.SetNamedValue(L"zoneset-uuid", json::value(appZoneHistory.data.zoneSetUuid));

        return result;
    }

    std::optional<AppZoneHistoryJSON> AppZoneHistoryJSON::FromJson(const json::JsonObject& zoneSet)
    {
        try
        {
            AppZoneHistoryJSON result;

            result.appPath = zoneSet.GetNamedString(L"app-path");
            result.data.zoneIndex = static_cast<int>(zoneSet.GetNamedNumber(L"zone-index"));
            result.data.deviceId = zoneSet.GetNamedString(L"device-id");
            result.data.zoneSetUuid = zoneSet.GetNamedString(L"zoneset-uuid");

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
        infoJson.SetNamedValue(L"ref-width", json::value(canvasInfo.referenceWidth));
        infoJson.SetNamedValue(L"ref-height", json::value(canvasInfo.referenceHeight));
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
            info.referenceWidth = static_cast<int>(infoJson.GetNamedNumber(L"ref-width"));
            info.referenceHeight = static_cast<int>(infoJson.GetNamedNumber(L"ref-height"));
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
        case CustomLayoutType::Canvas: {
            result.SetNamedValue(L"type", json::value(L"canvas"));

            CanvasLayoutInfo info = std::get<CanvasLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(L"info", CanvasLayoutInfo::ToJson(info));

            break;
        }
        case CustomLayoutType::Grid: {
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
