#include "pch.h"

#include "JsonHelpers.h"
#include "FancyZonesDataTypes.h"
#include "trace.h"
#include "util.h"

#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/MonitorUtils.h>

#include <common/logger/logger.h>

#include <filesystem>
#include <optional>
#include <utility>
#include <vector>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t ActiveZoneSetStr[] = L"active-zoneset";
    const wchar_t AppPathStr[] = L"app-path";
    const wchar_t AppZoneHistoryStr[] = L"app-zone-history";
    const wchar_t CanvasStr[] = L"canvas";
    const wchar_t CellChildMapStr[] = L"cell-child-map";
    const wchar_t ColumnsPercentageStr[] = L"columns-percentage";
    const wchar_t ColumnsStr[] = L"columns";
    const wchar_t CustomZoneSetsStr[] = L"custom-zone-sets";
    const wchar_t DeviceIdStr[] = L"device-id";
    const wchar_t DevicesStr[] = L"devices";
    const wchar_t EditorShowSpacingStr[] = L"editor-show-spacing";
    const wchar_t EditorSpacingStr[] = L"editor-spacing";
    const wchar_t EditorZoneCountStr[] = L"editor-zone-count";
    const wchar_t EditorSensitivityRadiusStr[] = L"editor-sensitivity-radius";
    const wchar_t GridStr[] = L"grid";
    const wchar_t HeightStr[] = L"height";
    const wchar_t HistoryStr[] = L"history";
    const wchar_t InfoStr[] = L"info";
    const wchar_t NameStr[] = L"name";
    const wchar_t QuickAccessKey[] = L"key";
    const wchar_t QuickAccessUuid[] = L"uuid";
    const wchar_t QuickLayoutKeys[] = L"quick-layout-keys";
    const wchar_t RefHeightStr[] = L"ref-height";
    const wchar_t RefWidthStr[] = L"ref-width";
    const wchar_t RowsPercentageStr[] = L"rows-percentage";
    const wchar_t RowsStr[] = L"rows";
    const wchar_t SensitivityRadius[] = L"sensitivity-radius";
    const wchar_t ShowSpacing[] = L"show-spacing";
    const wchar_t Spacing[] = L"spacing";
    const wchar_t Templates[] = L"templates";
    const wchar_t TypeStr[] = L"type";
    const wchar_t UuidStr[] = L"uuid";
    const wchar_t WidthStr[] = L"width";
    const wchar_t XStr[] = L"X";
    const wchar_t YStr[] = L"Y";
    const wchar_t ZoneIndexSetStr[] = L"zone-index-set";
    const wchar_t ZoneIndexStr[] = L"zone-index";
    const wchar_t ZoneSetUuidStr[] = L"zoneset-uuid";
    const wchar_t ZonesStr[] = L"zones";
}

namespace BackwardsCompatibility
{
    std::optional<DeviceIdData> DeviceIdData::ParseDeviceId(const std::wstring& str)
    {
        DeviceIdData data;

        std::wstring temp;
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

            data.deviceName = temp;

            if (!std::getline(wss, temp, L'_'))
            {
                return std::nullopt;
            }

            data.deviceName += L"#" + temp;
        }
        else if (std::getline(wss, temp, L'_') && !temp.empty())
        {
            data.deviceName = temp;
        }
        else
        {
            return std::nullopt;
        }

        // Step 2: parse the rest of the id
        std::vector<std::wstring> parts;
        while (std::getline(wss, temp, L'_'))
        {
            parts.push_back(temp);
        }

        if (parts.size() != 3 && parts.size() != 4)
        {
            return std::nullopt;
        }

        /*
        Refer to FancyZonesUtils::GenerateUniqueId parts contain:
        1. monitor id [string]
        2. width of device [int]
        3. height of device [int]
        4. virtual desktop id (GUID) [string]
        */
        try
        {
            for (const auto& c : parts[0])
            {
                std::ignore = std::stoi(std::wstring(&c));
            }

            for (const auto& c : parts[1])
            {
                std::ignore = std::stoi(std::wstring(&c));
            }

            data.width = std::stoi(parts[0]);
            data.height = std::stoi(parts[1]);
        }
        catch (const std::exception&)
        {
            return std::nullopt;
        }

        if (!SUCCEEDED(CLSIDFromString(parts[2].c_str(), &data.virtualDesktopId)))
        {
            return std::nullopt;
        }

        if (parts.size() == 4)
        {
            data.monitorId = parts[3]; //could be empty
        }

        return data;
    }
    
    bool DeviceIdData::IsValidDeviceId(const std::wstring& str)
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
        Refer to FancyZonesUtils::GenerateUniqueId parts contain:
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
                std::ignore = std::stoi(std::wstring(&c));
            }
            for (const auto& c : parts[2])
            {
                std::ignore = std::stoi(std::wstring(&c));
            }
        }
        catch (const std::exception&)
        {
            return false;
        }

        if (!FancyZonesUtils::IsValidGuid(parts[3]) || parts[0].empty())
        {
            return false;
        }

        return true;
    }
}
    
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
        if (json.HasKey(NonLocalizable::ZoneIndexSetStr))
        {
            data.zoneIndexSet = {};
            for (const auto& value : json.GetNamedArray(NonLocalizable::ZoneIndexSetStr))
            {
                data.zoneIndexSet.push_back(static_cast<ZoneIndex>(value.GetNumber()));
            }
        }
        else if (json.HasKey(NonLocalizable::ZoneIndexStr))
        {
            data.zoneIndexSet = { static_cast<ZoneIndex>(json.GetNamedNumber(NonLocalizable::ZoneIndexStr)) };
        }

        std::wstring deviceIdStr = json.GetNamedString(NonLocalizable::DeviceIdStr).c_str();
        auto deviceId = BackwardsCompatibility::DeviceIdData::ParseDeviceId(deviceIdStr);
        if (!deviceId.has_value())
        {
            return std::nullopt;
        }

        data.workAreaId = FancyZonesDataTypes::WorkAreaId{
            .monitorId = { .deviceId = MonitorUtils::Display::ConvertObsoleteDeviceId(deviceId->deviceName) },
            .virtualDesktopId = deviceId->virtualDesktopId
        };

        std::wstring layoutIdStr = json.GetNamedString(NonLocalizable::ZoneSetUuidStr).c_str();
        auto layoutIdOpt = FancyZonesUtils::GuidFromString(layoutIdStr);
        if (!layoutIdOpt.has_value())
        {
            return std::nullopt;
        }

        data.layoutId = layoutIdOpt.value();

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
        infoJson.SetNamedValue(NonLocalizable::RefWidthStr, json::value(canvasInfo.lastWorkAreaWidth));
        infoJson.SetNamedValue(NonLocalizable::RefHeightStr, json::value(canvasInfo.lastWorkAreaHeight));

        json::JsonArray zonesJson;

        for (const auto& [x, y, width, height] : canvasInfo.zones)
        {
            json::JsonObject zoneJson;
            zoneJson.SetNamedValue(NonLocalizable::XStr, json::value(x));
            zoneJson.SetNamedValue(NonLocalizable::YStr, json::value(y));
            zoneJson.SetNamedValue(NonLocalizable::WidthStr, json::value(width));
            zoneJson.SetNamedValue(NonLocalizable::HeightStr, json::value(height));
            zonesJson.Append(zoneJson);
        }
        infoJson.SetNamedValue(NonLocalizable::ZonesStr, zonesJson);
        infoJson.SetNamedValue(NonLocalizable::SensitivityRadius, json::value(canvasInfo.sensitivityRadius));
        return infoJson;
    }

    std::optional<FancyZonesDataTypes::CanvasLayoutInfo> CanvasLayoutInfoJSON::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            FancyZonesDataTypes::CanvasLayoutInfo info;
            info.lastWorkAreaWidth = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::RefWidthStr));
            info.lastWorkAreaHeight = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::RefHeightStr));

            json::JsonArray zonesJson = infoJson.GetNamedArray(NonLocalizable::ZonesStr);
            uint32_t size = zonesJson.Size();
            info.zones.reserve(size);
            for (uint32_t i = 0; i < size; ++i)
            {
                json::JsonObject zoneJson = zonesJson.GetObjectAt(i);
                const int x = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::XStr));
                const int y = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::YStr));
                const int width = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::WidthStr));
                const int height = static_cast<int>(zoneJson.GetNamedNumber(NonLocalizable::HeightStr));
                FancyZonesDataTypes::CanvasLayoutInfo::Rect zone{ x, y, width, height };
                info.zones.push_back(zone);
            }

            info.sensitivityRadius = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::SensitivityRadius, DefaultValues::SensitivityRadius));
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
        infoJson.SetNamedValue(NonLocalizable::RowsStr, json::value(gridInfo.m_rows));
        infoJson.SetNamedValue(NonLocalizable::ColumnsStr, json::value(gridInfo.m_columns));
        infoJson.SetNamedValue(NonLocalizable::RowsPercentageStr, NumVecToJsonArray(gridInfo.m_rowsPercents));
        infoJson.SetNamedValue(NonLocalizable::ColumnsPercentageStr, NumVecToJsonArray(gridInfo.m_columnsPercents));

        json::JsonArray cellChildMapJson;
        for (int i = 0; i < gridInfo.m_cellChildMap.size(); ++i)
        {
            cellChildMapJson.Append(NumVecToJsonArray(gridInfo.m_cellChildMap[i]));
        }
        infoJson.SetNamedValue(NonLocalizable::CellChildMapStr, cellChildMapJson);
        
        infoJson.SetNamedValue(NonLocalizable::SensitivityRadius, json::value(gridInfo.m_sensitivityRadius));
        infoJson.SetNamedValue(NonLocalizable::ShowSpacing, json::value(gridInfo.m_showSpacing));
        infoJson.SetNamedValue(NonLocalizable::Spacing, json::value(gridInfo.m_spacing));

        return infoJson;
    }

    std::optional<FancyZonesDataTypes::GridLayoutInfo> GridLayoutInfoJSON::FromJson(const json::JsonObject& infoJson)
    {
        try
        {
            FancyZonesDataTypes::GridLayoutInfo info(FancyZonesDataTypes::GridLayoutInfo::Minimal{});

            info.m_rows = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::RowsStr));
            info.m_columns = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::ColumnsStr));

            json::JsonArray rowsPercentage = infoJson.GetNamedArray(NonLocalizable::RowsPercentageStr);
            json::JsonArray columnsPercentage = infoJson.GetNamedArray(NonLocalizable::ColumnsPercentageStr);
            json::JsonArray cellChildMap = infoJson.GetNamedArray(NonLocalizable::CellChildMapStr);

            if (static_cast<int>(rowsPercentage.Size()) != info.m_rows || static_cast<int>(columnsPercentage.Size()) != info.m_columns || static_cast<int>(cellChildMap.Size()) != info.m_rows)
            {
                return std::nullopt;
            }

            info.m_rowsPercents = JsonArrayToNumVec(rowsPercentage);
            info.m_columnsPercents = JsonArrayToNumVec(columnsPercentage);
            for (const auto& cellsRow : cellChildMap)
            {
                const auto cellsArray = cellsRow.GetArray();
                if (static_cast<int>(cellsArray.Size()) != info.m_columns)
                {
                    return std::nullopt;
                }
                info.cellChildMap().push_back(JsonArrayToNumVec(cellsArray));
            }

            info.m_showSpacing = infoJson.GetNamedBoolean(NonLocalizable::ShowSpacing, DefaultValues::ShowSpacing);
            info.m_spacing = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::Spacing, DefaultValues::Spacing));
            info.m_sensitivityRadius = static_cast<int>(infoJson.GetNamedNumber(NonLocalizable::SensitivityRadius, DefaultValues::SensitivityRadius));

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

        result.SetNamedValue(NonLocalizable::UuidStr, json::value(customZoneSet.uuid));
        result.SetNamedValue(NonLocalizable::NameStr, json::value(customZoneSet.data.name));
        switch (customZoneSet.data.type)
        {
        case FancyZonesDataTypes::CustomLayoutType::Canvas:
        {
            result.SetNamedValue(NonLocalizable::TypeStr, json::value(NonLocalizable::CanvasStr));

            FancyZonesDataTypes::CanvasLayoutInfo info = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(NonLocalizable::InfoStr, CanvasLayoutInfoJSON::ToJson(info));

            break;
        }
        case FancyZonesDataTypes::CustomLayoutType::Grid:
        {
            result.SetNamedValue(NonLocalizable::TypeStr, json::value(NonLocalizable::GridStr));

            FancyZonesDataTypes::GridLayoutInfo gridInfo = std::get<FancyZonesDataTypes::GridLayoutInfo>(customZoneSet.data.info);
            result.SetNamedValue(NonLocalizable::InfoStr, GridLayoutInfoJSON::ToJson(gridInfo));

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

            result.uuid = customZoneSet.GetNamedString(NonLocalizable::UuidStr);
            if (!FancyZonesUtils::IsValidGuid(result.uuid))
            {
                return std::nullopt;
            }

            result.data.name = customZoneSet.GetNamedString(NonLocalizable::NameStr);

            json::JsonObject infoJson = customZoneSet.GetNamedObject(NonLocalizable::InfoStr);
            std::wstring zoneSetType = std::wstring{ customZoneSet.GetNamedString(NonLocalizable::TypeStr) };
            if (zoneSetType.compare(NonLocalizable::CanvasStr) == 0)
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
            else if (zoneSetType.compare(NonLocalizable::GridStr) == 0)
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

    std::optional<FancyZonesDataTypes::ZoneSetData> ZoneSetDataJSON::FromJson(const json::JsonObject& zoneSet)
    {
        try
        {
            FancyZonesDataTypes::ZoneSetData zoneSetData;
            zoneSetData.uuid = zoneSet.GetNamedString(NonLocalizable::UuidStr);
            zoneSetData.type = FancyZonesDataTypes::TypeFromString(std::wstring{ zoneSet.GetNamedString(NonLocalizable::TypeStr) });

            if (!FancyZonesUtils::IsValidGuid(zoneSetData.uuid))
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

    std::optional<DeviceInfoJSON> DeviceInfoJSON::FromJson(const json::JsonObject& device)
    {
        try
        {
            DeviceInfoJSON result;

            std::wstring deviceIdStr = device.GetNamedString(NonLocalizable::DeviceIdStr).c_str();
            auto deviceId = BackwardsCompatibility::DeviceIdData::ParseDeviceId(deviceIdStr);
            if (!deviceId.has_value())
            {
                return std::nullopt;
            }

            result.deviceId = *deviceId;

            if (auto zoneSet = JSONHelpers::ZoneSetDataJSON::FromJson(device.GetNamedObject(NonLocalizable::ActiveZoneSetStr)); zoneSet.has_value())
            {
                result.data.activeZoneSet = std::move(zoneSet.value());
            }
            else
            {
                return std::nullopt;
            }

            result.data.showSpacing = device.GetNamedBoolean(NonLocalizable::EditorShowSpacingStr);
            result.data.spacing = static_cast<int>(device.GetNamedNumber(NonLocalizable::EditorSpacingStr));
            result.data.zoneCount = static_cast<int>(device.GetNamedNumber(NonLocalizable::EditorZoneCountStr));
            result.data.sensitivityRadius = static_cast<int>(device.GetNamedNumber(NonLocalizable::EditorSensitivityRadiusStr, DefaultValues::SensitivityRadius));

            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }
    
    std::optional<LayoutQuickKeyJSON> LayoutQuickKeyJSON::FromJson(const json::JsonObject& layoutQuickKey)
    {
        try
        {
            LayoutQuickKeyJSON result;

            result.layoutUuid = layoutQuickKey.GetNamedString(NonLocalizable::QuickAccessUuid);
            if (!FancyZonesUtils::IsValidGuid(result.layoutUuid))
            {
                return std::nullopt;
            }

            result.key = static_cast<int>(layoutQuickKey.GetNamedNumber(NonLocalizable::QuickAccessKey));
            
            return result;
        }
        catch (const winrt::hresult_error&)
        {
            return std::nullopt;
        }
    }

    json::JsonObject GetPersistFancyZonesJSON(const std::wstring& zonesSettingsFileName, const std::wstring& appZoneHistoryFileName)
    {
        auto result = json::from_file(zonesSettingsFileName);
        if (result)
        {
            if (!result->HasKey(NonLocalizable::AppZoneHistoryStr))
            {
                auto appZoneHistory = json::from_file(appZoneHistoryFileName);
                if (appZoneHistory)
                {
                    result->SetNamedValue(NonLocalizable::AppZoneHistoryStr, appZoneHistory->GetNamedArray(NonLocalizable::AppZoneHistoryStr));
                }
                else
                {
                    result->SetNamedValue(NonLocalizable::AppZoneHistoryStr, json::JsonArray());
                }
            }
            return *result;
        }
        else
        {
            return json::JsonObject();
        }
    }

    std::optional<TDeviceInfoMap> ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TDeviceInfoMap deviceInfoMap{};
            auto devices = fancyZonesDataJSON.GetNamedArray(NonLocalizable::DevicesStr);

            for (uint32_t i = 0; i < devices.Size(); ++i)
            {
                if (auto device = DeviceInfoJSON::DeviceInfoJSON::FromJson(devices.GetObjectAt(i)); device.has_value())
                {
                    deviceInfoMap[device->deviceId] = std::move(device->data);
                }
            }

            return std::move(deviceInfoMap);
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error(L"Parsing device info error: {}", e.message());
            return std::nullopt;
        }
    }

    void SaveAppliedLayouts(const TDeviceInfoMap& deviceInfoMap)
    {
        json::JsonObject root{};
        json::JsonArray layoutsArray{};

        for (const auto& [deviceID, data] : deviceInfoMap)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(data.activeZoneSet.uuid));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(data.activeZoneSet.type)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(data.showSpacing));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(data.spacing));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(data.zoneCount));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(data.sensitivityRadius));

            json::JsonObject obj{};
            json::JsonObject device{};
            device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(deviceID.deviceName));

            auto virtualDesktopStr = FancyZonesUtils::GuidToString(deviceID.virtualDesktopId);
            if (virtualDesktopStr)
            {
                device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(virtualDesktopStr.value()));
            }

            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, device);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            layoutsArray.Append(obj);
        }

        root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
        json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);
    }
    
    std::optional<TLayoutQuickKeysMap> ParseQuickKeys(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TLayoutQuickKeysMap quickKeysMap{};
            auto quickKeys = fancyZonesDataJSON.GetNamedArray(NonLocalizable::QuickLayoutKeys);

            for (uint32_t i = 0; i < quickKeys.Size(); ++i)
            {
                if (auto quickKey = LayoutQuickKeyJSON::FromJson(quickKeys.GetObjectAt(i)); quickKey.has_value())
                {
                    quickKeysMap[quickKey->layoutUuid] = std::move(quickKey->key);
                }
            }

            return std::move(quickKeysMap);
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error(L"Parsing quick keys error: {}", e.message());
            return std::nullopt;
        }
    }

    void SaveLayoutHotkeys(const TLayoutQuickKeysMap& quickKeysMap)
    {
        json::JsonObject root{};
        json::JsonArray keysArray{};

        for (const auto& [uuid, key] : quickKeysMap)
        {
            json::JsonObject keyJson{};

            keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutUuidID, json::value(uuid));
            keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::KeyID, json::value(key));

            keysArray.Append(keyJson);
        }

        root.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutHotkeysArrayID, keysArray);
        json::to_file(LayoutHotkeys::LayoutHotkeysFileName(), root);
    }

    std::optional<json::JsonArray> ParseLayoutTemplates(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            return fancyZonesDataJSON.GetNamedArray(NonLocalizable::Templates);
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error(L"Parsing layout templates error: {}", e.message());
        }

        return std::nullopt;
    }

    void SaveLayoutTemplates(const json::JsonArray& templates)
    {
        json::JsonObject root{};
        root.SetNamedValue(NonLocalizable::LayoutTemplatesIds::LayoutTemplatesArrayID, templates);
        json::to_file(LayoutTemplates::LayoutTemplatesFileName(), root);
    }

    std::optional<TCustomZoneSetsMap> ParseCustomZoneSets(const json::JsonObject& fancyZonesDataJSON)
    {
        try
        {
            TCustomZoneSetsMap customZoneSetsMap{};
            auto customZoneSets = fancyZonesDataJSON.GetNamedArray(NonLocalizable::CustomZoneSetsStr);

            for (uint32_t i = 0; i < customZoneSets.Size(); ++i)
            {
                if (auto zoneSet = CustomZoneSetJSON::FromJson(customZoneSets.GetObjectAt(i)); zoneSet.has_value())
                {
                    customZoneSetsMap[zoneSet->uuid] = std::move(zoneSet->data);
                }
            }

            return std::move(customZoneSetsMap);
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error(L"Parsing custom layouts error: {}", e.message());
            return std::nullopt;
        }
    }

    void SaveCustomLayouts(const TCustomZoneSetsMap& map)
    {
        json::JsonObject root{};
        json::JsonArray layoutsArray{};

        for (const auto& [uuid, data] : map)
        {
            json::JsonObject layoutJson{};
            layoutsArray.Append(CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ uuid, data }));
        }

        root.SetNamedValue(NonLocalizable::CustomLayoutsIds::CustomLayoutsArrayID, layoutsArray);
        json::to_file(CustomLayouts::CustomLayoutsFileName(), root);
    }
}