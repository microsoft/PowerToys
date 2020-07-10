#pragma once

#include <common/json.h>

#include <string>
#include <vector>
#include <optional>
#include <variant>
#include <unordered_map>

#include <windef.h>

namespace FancyZonesDataTypes
{
    bool IsValidGuid(const std::wstring& str);
    bool IsValidDeviceId(const std::wstring& str);


    enum class ZoneSetLayoutType : int
    {
        Blank = -1,
        Focus,
        Columns,
        Rows,
        Grid,
        PriorityGrid,
        Custom
    };

    std::wstring TypeToString(ZoneSetLayoutType type);
    ZoneSetLayoutType TypeFromString(const std::wstring& typeStr);

    enum class CustomLayoutType : int
    {
        Grid = 0,
        Canvas
    };

    struct CanvasLayoutInfo
    {
        int lastWorkAreaWidth;
        int lastWorkAreaHeight;

        struct Rect
        {
            int x;
            int y;
            int width;
            int height;
        };
        std::vector<CanvasLayoutInfo::Rect> zones;

        static json::JsonObject ToJson(const CanvasLayoutInfo& canvasInfo);
        static std::optional<CanvasLayoutInfo> FromJson(const json::JsonObject& infoJson);
    };

    class GridLayoutInfo
    {
    public:
        struct Minimal
        {
            int rows;
            int columns;
        };

        struct Full
        {
            int rows;
            int columns;
            const std::vector<int>& rowsPercents;
            const std::vector<int>& columnsPercents;
            const std::vector<std::vector<int>>& cellChildMap;
        };

        GridLayoutInfo(const Minimal& info);
        GridLayoutInfo(const Full& info);
        ~GridLayoutInfo() = default;

        static json::JsonObject ToJson(const GridLayoutInfo& gridInfo);
        static std::optional<GridLayoutInfo> FromJson(const json::JsonObject& infoJson);

        inline std::vector<int>& rowsPercents() { return m_rowsPercents; };
        inline std::vector<int>& columnsPercents() { return m_columnsPercents; };
        inline std::vector<std::vector<int>>& cellChildMap() { return m_cellChildMap; };

        inline int rows() const { return m_rows; }
        inline int columns() const { return m_columns; }

        inline const std::vector<int>& rowsPercents() const { return m_rowsPercents; };
        inline const std::vector<int>& columnsPercents() const { return m_columnsPercents; };
        inline const std::vector<std::vector<int>>& cellChildMap() const { return m_cellChildMap; };

    private:
        int m_rows;
        int m_columns;
        std::vector<int> m_rowsPercents;
        std::vector<int> m_columnsPercents;
        std::vector<std::vector<int>> m_cellChildMap;
    };

    struct CustomZoneSetData
    {
        std::wstring name;
        CustomLayoutType type;
        std::variant<CanvasLayoutInfo, GridLayoutInfo> info;
    };

    struct CustomZoneSetJSON
    {
        std::wstring uuid;
        CustomZoneSetData data;

        static json::JsonObject ToJson(const CustomZoneSetJSON& device);
        static std::optional<CustomZoneSetJSON> FromJson(const json::JsonObject& customZoneSet);
    };


    struct ZoneSetData
    {
        std::wstring uuid;
        ZoneSetLayoutType type;

        static json::JsonObject ToJson(const ZoneSetData& zoneSet);
        static std::optional<ZoneSetData> FromJson(const json::JsonObject& zoneSet);
    };

    struct AppZoneHistoryData
    {
        std::unordered_map<DWORD, HWND> processIdToHandleMap; // Maps process id(DWORD) of application to zoned window handle(HWND)

        std::wstring zoneSetUuid;
        std::wstring deviceId;
        std::vector<int> zoneIndexSet;
    };

    struct AppZoneHistoryJSON
    {
        std::wstring appPath;
        std::vector<AppZoneHistoryData> data;

        static json::JsonObject ToJson(const AppZoneHistoryJSON& appZoneHistory);
        static std::optional<AppZoneHistoryJSON> FromJson(const json::JsonObject& zoneSet);
    };

    struct DeviceInfoData
    {
        ZoneSetData activeZoneSet;
        bool showSpacing;
        int spacing;
        int zoneCount;
    };

    struct DeviceInfoJSON
    {
        std::wstring deviceId;
        DeviceInfoData data;

        static json::JsonObject ToJson(const DeviceInfoJSON& device);
        static std::optional<DeviceInfoJSON> FromJson(const json::JsonObject& device);
    };

}