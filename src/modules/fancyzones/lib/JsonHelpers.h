#pragma once

#include <common/json.h>

#include <string>
#include <vector>
#include <unordered_map>

namespace FancyZonesDataTypes
{
    struct AppZoneHistoryData;
    struct CustomZoneSetData;
    struct DeviceInfoData;
}

namespace JSONHelpers
{
    json::JsonObject GetPersistFancyZonesJSON(const std::wstring& zonesSettingsFilePath, const std::wstring& appZoneHistoryFilePath);

    using TAppZoneHistoryMap = std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>>;
    using TDeviceInfoMap = std::unordered_map<std::wstring, FancyZonesDataTypes::DeviceInfoData>;
    using TCustomZoneSetsMap = std::unordered_map<std::wstring, FancyZonesDataTypes::CustomZoneSetData>;

    TAppZoneHistoryMap ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeAppZoneHistory(const TAppZoneHistoryMap& appZoneHistoryMap);

    TDeviceInfoMap ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeDeviceInfos(const TDeviceInfoMap& deviceInfoMap);

    TCustomZoneSetsMap ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON);
    json::JsonArray SerializeCustomZoneSets(const TCustomZoneSetsMap& customZoneSetsMap);


}