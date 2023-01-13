#pragma once

#include "FancyZonesDataTypes.h"

#include <common/utils/json.h>

#include <string>
#include <vector>
#include <unordered_map>

namespace BackwardsCompatibility
{
    struct DeviceIdData
    {
        std::wstring deviceName = L"FallbackDevice";
        int width{};
        int height{};
        GUID virtualDesktopId{};
        std::wstring monitorId;

        static std::optional<DeviceIdData> ParseDeviceId(const std::wstring& str);
        static bool IsValidDeviceId(const std::wstring& str);
    };

    inline bool operator==(const BackwardsCompatibility::DeviceIdData& lhs, const BackwardsCompatibility::DeviceIdData& rhs)
    {
        return lhs.deviceName.compare(rhs.deviceName) == 0 && lhs.width == rhs.width && lhs.height == rhs.height && (lhs.virtualDesktopId == rhs.virtualDesktopId || lhs.virtualDesktopId == GUID_NULL || rhs.virtualDesktopId == GUID_NULL) && lhs.monitorId.compare(rhs.monitorId) == 0;
    }

    inline bool operator!=(const BackwardsCompatibility::DeviceIdData& lhs, const BackwardsCompatibility::DeviceIdData& rhs)
    {
        return !(lhs == rhs);
    }

    inline bool operator<(const BackwardsCompatibility::DeviceIdData& lhs, const BackwardsCompatibility::DeviceIdData& rhs)
    {
        return lhs.deviceName.compare(rhs.deviceName) < 0 || lhs.width < rhs.width || lhs.height < rhs.height || lhs.monitorId.compare(rhs.monitorId) < 0;
    }
}

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
        std::optional<FancyZonesDataTypes::ZoneSetData> FromJson(const json::JsonObject& zoneSet);
    };

    struct DeviceInfoJSON
    {
        BackwardsCompatibility::DeviceIdData deviceId;
        FancyZonesDataTypes::DeviceInfoData data;

        static std::optional<DeviceInfoJSON> FromJson(const json::JsonObject& device);
    };

    struct LayoutQuickKeyJSON
    {
        std::wstring layoutUuid;
        int key{};

        static std::optional<LayoutQuickKeyJSON> FromJson(const json::JsonObject& device);
    };

    using TDeviceInfoMap = std::unordered_map<BackwardsCompatibility::DeviceIdData, FancyZonesDataTypes::DeviceInfoData>;
    using TCustomZoneSetsMap = std::unordered_map<std::wstring, FancyZonesDataTypes::CustomLayoutData>;
    using TLayoutQuickKeysMap = std::unordered_map<std::wstring, int>;

    json::JsonObject GetPersistFancyZonesJSON(const std::wstring& zonesSettingsFileName, const std::wstring& appZoneHistoryFileName);

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

namespace std
{
    template<>
    struct hash<BackwardsCompatibility::DeviceIdData>
    {
        size_t operator()(const BackwardsCompatibility::DeviceIdData& /*Value*/) const
        {
            return 0;
        }
    };
}