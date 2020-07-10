#include "pch.h"

#include "FancyZonesDataTypes.h"

#include <sstream>

namespace
{
    // From Settings.cs
    constexpr int c_focusModelId = 0xFFFF;
    constexpr int c_rowsModelId = 0xFFFE;
    constexpr int c_columnsModelId = 0xFFFD;
    constexpr int c_gridModelId = 0xFFFC;
    constexpr int c_priorityGridModelId = 0xFFFB;
    constexpr int c_blankCustomModelId = 0xFFFA;


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
}

namespace FancyZonesDataTypes
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
            return ZoneSetLayoutType::Focus;
        }
        else if (typeStr == L"columns")
        {
            return ZoneSetLayoutType::Columns;
        }
        else if (typeStr == L"rows")
        {
            return ZoneSetLayoutType::Rows;
        }
        else if (typeStr == L"grid")
        {
            return ZoneSetLayoutType::Grid;
        }
        else if (typeStr == L"priority-grid")
        {
            return ZoneSetLayoutType::PriorityGrid;
        }
        else if (typeStr == L"custom")
        {
            return ZoneSetLayoutType::Custom;
        }
        else
        {
            return ZoneSetLayoutType::Blank;
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
}