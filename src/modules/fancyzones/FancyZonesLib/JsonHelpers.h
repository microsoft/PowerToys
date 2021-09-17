#pragma once

#include "FancyZonesDataTypes.h"

#include <common/utils/json.h>

#include <string>
#include <vector>
#include <unordered_map>

namespace JSONHelpers
{
    namespace CanvasLayoutInfoJSON
    {
        json::JsonObject ToJson(const FancyZonesDataTypes::CanvasLayoutInfo& canvasInfo);
        std::optional<FancyZonesDataTypes::CanvasLayoutInfo> FromJson(const json::JsonObject& infoJson);
    }

    namespace GridLayoutInfoJSON
    {
        json::JsonObject ToJson(const FancyZonesDataTypes::GridLayoutInfo& gridInfo);
        std::optional<FancyZonesDataTypes::GridLayoutInfo> FromJson(const json::JsonObject& infoJson);
    }

    struct CustomZoneSetJSON
    {
        std::wstring uuid;
        FancyZonesDataTypes::CustomZoneSetData data;

        static json::JsonObject ToJson(const CustomZoneSetJSON& device);
        static std::optional<CustomZoneSetJSON> FromJson(const json::JsonObject& customZoneSet);
    };

    namespace ZoneSetDataJSON
    {
        json::JsonObject ToJson(const FancyZonesDataTypes::ZoneSetData& zoneSet);
        std::optional<FancyZonesDataTypes::ZoneSetData> FromJson(const json::JsonObject& zoneSet);
    };

    struct AppZoneHistoryJSON
    {
        std::wstring appPath;
        std::vector<FancyZonesDataTypes::AppZoneHistoryData> data;

        static json::JsonObject ToJson(const AppZoneHistoryJSON& appZoneHistory);
        static std::optional<AppZoneHistoryJSON> FromJson(const json::JsonObject& zoneSet);
    };

    struct DeviceInfoJSON
    {
        FancyZonesDataTypes::DeviceIdData deviceId;
        FancyZonesDataTypes::DeviceInfoData data;

        static json::JsonObject ToJson(const DeviceInfoJSON& device);
        static std::optional<DeviceInfoJSON> FromJson(const json::JsonObject& device);
    };

    struct LayoutQuickKeyJSON
    {
        std::wstring layoutUuid;
        int key;

        static json::JsonObject ToJson(const LayoutQuickKeyJSON& device);
        static std::optional<LayoutQuickKeyJSON> FromJson(const json::JsonObject& device);
    };

    using TAppZoneHistoryMap = std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>>;
    using TDeviceInfoMap = std::unordered_map<FancyZonesDataTypes::DeviceIdData, FancyZonesDataTypes::DeviceInfoData>;
    using TCustomZoneSetsMap = std::unordered_map<std::wstring, FancyZonesDataTypes::CustomZoneSetData>;
    using TLayoutQuickKeysMap = std::unordered_map<std::wstring, int>;

    struct MonitorInfo
    {
        int dpi;
        std::wstring id;
        int top;
        int left;
        int width;
        int height;
        bool isSelected = false;

        static json::JsonObject ToJson(const MonitorInfo& monitor);
    };
    
    struct EditorArgs
    {
        DWORD processId;
        bool spanZonesAcrossMonitors;
        std::vector<MonitorInfo> monitors;

        static json::JsonObject ToJson(const EditorArgs& args);
    };

    json::JsonObject GetPersistFancyZonesJSON(const std::wstring& zonesSettingsFileName, const std::wstring& appZoneHistoryFileName);

    void SaveZoneSettings(const std::wstring& zonesSettingsFileName, const TDeviceInfoMap& deviceInfoMap, const TCustomZoneSetsMap& customZoneSetsMap, const TLayoutQuickKeysMap& quickKeysMap);
    void SaveAppZoneHistory(const std::wstring& appZoneHistoryFileName, const TAppZoneHistoryMap& appZoneHistoryMap);

    TAppZoneHistoryMap ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeAppZoneHistory(const TAppZoneHistoryMap& appZoneHistoryMap);

    TDeviceInfoMap ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeDeviceInfos(const TDeviceInfoMap& deviceInfoMap);

    TCustomZoneSetsMap ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeCustomZoneSets(const TCustomZoneSetsMap& customZoneSetsMap);

    TLayoutQuickKeysMap ParseQuickKeys(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeQuickKeys(const TLayoutQuickKeysMap& quickKeysMap);
}
