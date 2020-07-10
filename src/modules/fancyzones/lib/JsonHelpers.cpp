#include "pch.h"

#include "JsonHelpers.h"
#include "FancyZonesDataTypes.h"

#include <vector>

namespace JSONHelpers
{
    json::JsonObject GetPersistFancyZonesJSON(const std::wstring& zonesSettingsFilePath, const std::wstring& appZoneHistoryFilePath)
    {
        auto result = json::from_file(zonesSettingsFilePath);
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

    TAppZoneHistoryMap ParseAppZoneHistory(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TAppZoneHistoryMap appZoneHistoryMap{};
            auto appLastZones = fancyZonesDataJSON.GetNamedArray(L"app-zone-history");

            for (uint32_t i = 0; i < appLastZones.Size(); ++i)
            {
                json::JsonObject appLastZone = appLastZones.GetObjectAt(i);
                if (auto appZoneHistory = FancyZonesDataTypes::AppZoneHistoryJSON::FromJson(appLastZone); appZoneHistory.has_value())
                {
                    appZoneHistoryMap[appZoneHistory->appPath] = std::move(appZoneHistory->data);
                }
            }

            return std::move(appZoneHistoryMap);
        }
        catch (const winrt::hresult_error&)
        {
            return {};
        }
    }

    json::JsonArray SerializeAppZoneHistory(const TAppZoneHistoryMap& appZoneHistoryMap)
    {
        json::JsonArray appHistoryArray;

        for (const auto& [appPath, appZoneHistoryData] : appZoneHistoryMap)
        {
            appHistoryArray.Append(FancyZonesDataTypes::AppZoneHistoryJSON::ToJson(FancyZonesDataTypes::AppZoneHistoryJSON{ appPath, appZoneHistoryData }));
        }

        return appHistoryArray;
    }

    TDeviceInfoMap ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TDeviceInfoMap deviceInfoMap{};
            auto devices = fancyZonesDataJSON.GetNamedArray(L"devices");

            for (uint32_t i = 0; i < devices.Size(); ++i)
            {
                if (auto device = FancyZonesDataTypes::DeviceInfoJSON::DeviceInfoJSON::FromJson(devices.GetObjectAt(i)); device.has_value())
                {
                    deviceInfoMap[device->deviceId] = std::move(device->data);
                }
            }

            return std::move(deviceInfoMap);
        }
        catch (const winrt::hresult_error&)
        {
            return {};
        }
    }

    json::JsonArray SerializeDeviceInfos(const TDeviceInfoMap& deviceInfoMap)
    {
        json::JsonArray DeviceInfosJSON{};

        for (const auto& [deviceID, deviceData] : deviceInfoMap)
        {
            if (deviceData.activeZoneSet.type != FancyZonesDataTypes::ZoneSetLayoutType::Blank)
            {
                DeviceInfosJSON.Append(FancyZonesDataTypes::DeviceInfoJSON::DeviceInfoJSON::ToJson(FancyZonesDataTypes::DeviceInfoJSON{ deviceID, deviceData }));
            }
        }

        return DeviceInfosJSON;
    }

    TCustomZoneSetsMap ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TCustomZoneSetsMap customZoneSetsMap{};
            auto customZoneSets = fancyZonesDataJSON.GetNamedArray(L"custom-zone-sets");

            for (uint32_t i = 0; i < customZoneSets.Size(); ++i)
            {
                if (auto zoneSet = FancyZonesDataTypes::CustomZoneSetJSON::FromJson(customZoneSets.GetObjectAt(i)); zoneSet.has_value())
                {
                    customZoneSetsMap[zoneSet->uuid] = std::move(zoneSet->data);
                }
            }

            return std::move(customZoneSetsMap);
        }
        catch (const winrt::hresult_error&)
        {
            return {};
        }
    }

    json::JsonArray SerializeCustomZoneSets(const TCustomZoneSetsMap& customZoneSetsMap)
    {
        json::JsonArray customZoneSetsJSON{};

        for (const auto& [zoneSetId, zoneSetData] : customZoneSetsMap)
        {
            customZoneSetsJSON.Append(FancyZonesDataTypes::CustomZoneSetJSON::ToJson(FancyZonesDataTypes::CustomZoneSetJSON{ zoneSetId, zoneSetData }));
        }

        return customZoneSetsJSON;
    }

}