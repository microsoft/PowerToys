#include "pch.h"

#include "JsonHelpers.h"
#include "FancyZonesDataTypes.h"
#include "util.h"

#include <filesystem>
#include <optional>
#include <utility>
#include <vector>


namespace
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

    std::optional<FancyZonesDataTypes::AppZoneHistoryData> ParseSingleAppZoneHistoryItem(const json::JsonObject& json)
    {
        FancyZonesDataTypes::AppZoneHistoryData data;
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

        if (!IsValidGuid(data.zoneSetUuid) || !IsValidDeviceId(data.deviceId))
        {
            return std::nullopt;
        }

        return data;
    }

	inline bool DeleteTmpFile(std::wstring_view tmpFilePath)
    {
        return DeleteFileW(tmpFilePath.data());
    }

}

namespace JSONHelpers
{
    json::JsonObject CanvasLayoutInfoJSON::ToJson(const FancyZonesDataTypes::CanvasLayoutInfo& canvasInfo)
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

    std::optional<FancyZonesDataTypes::CanvasLayoutInfo> CanvasLayoutInfoJSON::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            FancyZonesDataTypes::CanvasLayoutInfo info;
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
                FancyZonesDataTypes::CanvasLayoutInfo::Rect zone{ x, y, width, height };
                info.zones.push_back(zone);
            }
            return info;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject GridLayoutInfoJSON::ToJson(const FancyZonesDataTypes::GridLayoutInfo& gridInfo)
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

    std::optional<FancyZonesDataTypes::GridLayoutInfo> GridLayoutInfoJSON::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            FancyZonesDataTypes::GridLayoutInfo info(FancyZonesDataTypes::GridLayoutInfo::Minimal{});

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
        case FancyZonesDataTypes::CustomLayoutType::Canvas:
        {
            result.SetNamedValue(L"type", json::value(L"canvas"));

            FancyZonesDataTypes::CanvasLayoutInfo info = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(L"info", CanvasLayoutInfoJSON::ToJson(info));

            break;
        }
        case FancyZonesDataTypes::CustomLayoutType::Grid:
        {
            result.SetNamedValue(L"type", json::value(L"grid"));

            FancyZonesDataTypes::GridLayoutInfo gridInfo = std::get<FancyZonesDataTypes::GridLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(L"info", GridLayoutInfoJSON::ToJson(gridInfo));

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
            if (!IsValidGuid(result.uuid))
            {
                return std::nullopt;
            }

            result.data.name = customZoneSet.GetNamedString(L"name");

            json::JsonObject infoJson = customZoneSet.GetNamedObject(L"info");
            std::wstring zoneSetType = std::wstring{ customZoneSet.GetNamedString(L"type") };
            if (zoneSetType.compare(L"canvas") == 0)
            {
                if (auto info = CanvasLayoutInfoJSON::FromJson(infoJson); info.has_value())
                {
                    result.data.type = FancyZonesDataTypes::CustomLayoutType::Canvas;
                    result.data.info = std::move(info.value());
                }
                else
                {
                    return std::nullopt;
                }
            }
            else if (zoneSetType.compare(L"grid") == 0)
            {
                if (auto info = GridLayoutInfoJSON::FromJson(infoJson); info.has_value())
                {
                    result.data.type = FancyZonesDataTypes::CustomLayoutType::Grid;
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

    json::JsonObject ZoneSetDataJSON::ToJson(const FancyZonesDataTypes::ZoneSetData& zoneSet)
    {
        json::JsonObject result{};

        result.SetNamedValue(L"uuid", json::value(zoneSet.uuid));
        result.SetNamedValue(L"type", json::value(TypeToString(zoneSet.type)));

        return result;
    }

    std::optional<FancyZonesDataTypes::ZoneSetData> ZoneSetDataJSON::FromJson(const json::JsonObject& zoneSet)
    {
        try
        {
            FancyZonesDataTypes::ZoneSetData zoneSetData;
            zoneSetData.uuid = zoneSet.GetNamedString(L"uuid");
            zoneSetData.type = FancyZonesDataTypes::TypeFromString(std::wstring{ zoneSet.GetNamedString(L"type") });

            if (!IsValidGuid(zoneSetData.uuid))
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
        result.SetNamedValue(L"active-zoneset", JSONHelpers::ZoneSetDataJSON::ToJson(device.data.activeZoneSet));
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
            if (!IsValidDeviceId(result.deviceId))
            {
                return std::nullopt;
            }

            if (auto zoneSet = JSONHelpers::ZoneSetDataJSON::FromJson(device.GetNamedObject(L"active-zoneset")); zoneSet.has_value())
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
                if (auto appZoneHistory = AppZoneHistoryJSON::FromJson(appLastZone); appZoneHistory.has_value())
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
            appHistoryArray.Append(AppZoneHistoryJSON::ToJson(AppZoneHistoryJSON{ appPath, appZoneHistoryData }));
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
                if (auto device = DeviceInfoJSON::DeviceInfoJSON::FromJson(devices.GetObjectAt(i)); device.has_value())
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
                DeviceInfosJSON.Append(DeviceInfoJSON::DeviceInfoJSON::ToJson(DeviceInfoJSON{ deviceID, deviceData }));
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
                if (auto zoneSet = CustomZoneSetJSON::FromJson(customZoneSets.GetObjectAt(i)); zoneSet.has_value())
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
            customZoneSetsJSON.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ zoneSetId, zoneSetData }));
        }

        return customZoneSetsJSON;
    }

    void SerializeDeviceInfoToTmpFile(const JSONHelpers::DeviceInfoJSON& deviceInfo, std::wstring_view tmpFilePath)
    {
        json::JsonObject deviceInfoJson = JSONHelpers::DeviceInfoJSON::ToJson(deviceInfo);
        json::to_file(tmpFilePath, deviceInfoJson);
    }

    std::optional<DeviceInfoJSON> ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::optional<DeviceInfoJSON> result{ std::nullopt };
		if (std::filesystem::exists(tmpFilePath))
        {
            if (auto zoneSetJson = json::from_file(tmpFilePath); zoneSetJson.has_value())
            {
                if (auto deviceInfo = JSONHelpers::DeviceInfoJSON::FromJson(zoneSetJson.value()); deviceInfo.has_value())
                {
                    result = std::move(deviceInfo);
                }
            }
        }
        // TODO(stefan): Check why this was inside triple if above
		DeleteTmpFile(tmpFilePath);
        return result;
    }

    std::optional<CustomZoneSetJSON> ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::optional<CustomZoneSetJSON> result{ std::nullopt };
        if (std::filesystem::exists(tmpFilePath))
        {
            try
            {
                if (auto customZoneSetJson = json::from_file(tmpFilePath); customZoneSetJson.has_value())
                {
                    if (auto customZoneSet = JSONHelpers::CustomZoneSetJSON::FromJson(customZoneSetJson.value()); customZoneSet.has_value())
                    {
                        result = std::move(customZoneSet);
                    }
                }
            }
            catch (const winrt::hresult_error&)
            {
                result = std::nullopt;
            }

            DeleteTmpFile(tmpFilePath);
        }
        return result;
    }

    std::vector<std::wstring> ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath)
    {
        std::vector<std::wstring> result{};
        if (std::filesystem::exists(tmpFilePath))
        {
            auto deletedZoneSetsJson = json::from_file(tmpFilePath);
            try
            {
                auto deletedCustomZoneSets = deletedZoneSetsJson->GetNamedArray(L"deleted-custom-zone-sets");
                for (auto zoneSet : deletedCustomZoneSets)
                {
                    std::wstring uuid = L"{" + std::wstring{ zoneSet.GetString() } + L"}";
                    result.push_back(uuid);
                }
            }
            catch (const winrt::hresult_error&)
            {
            }

            DeleteTmpFile(tmpFilePath);
        }

        return result;
    }
}
