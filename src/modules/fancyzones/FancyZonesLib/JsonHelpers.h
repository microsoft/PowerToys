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
        FancyZonesDataTypes::CustomLayoutData data;

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
    using TCustomZoneSetsMap = std::unordered_map<std::wstring, FancyZonesDataTypes::CustomLayoutData>;
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

    TAppZoneHistoryMap ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeAppZoneHistory(const TAppZoneHistoryMap& appZoneHistoryMap);
    void SaveAppZoneHistory(const std::wstring& appZoneHistoryFileName, const TAppZoneHistoryMap& appZoneHistoryMap);

    // replace zones-settings: applied layouts
    std::optional<TDeviceInfoMap> ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON);
    void SaveAppliedLayouts(const TDeviceInfoMap& deviceInfoMap);

    // replace zones-settings: layout hotkeys
    std::optional<TLayoutQuickKeysMap> ParseQuickKeys(const json::JsonObject& fancyZonesDataJSON);
    void SaveLayoutHotkeys(const TLayoutQuickKeysMap& quickKeysMap);

    // replace zones-settings: layout templates
    std::optional<json::JsonArray> ParseLayoutTemplates(const json::JsonObject& fancyZonesDataJSON);
    void SaveLayoutTemplates(const json::JsonArray& templates);

    // replace zones-settings: custom layouts
    std::optional<TCustomZoneSetsMap> ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON);
    void SaveCustomLayouts(const TCustomZoneSetsMap& map);
}
