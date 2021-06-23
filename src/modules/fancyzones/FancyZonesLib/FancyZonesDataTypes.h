#pragma once

#include <common/utils/json.h>

#include <string>
#include <vector>
#include <optional>
#include <variant>
#include <unordered_map>

#include <windef.h>

namespace FancyZonesDataTypes
{
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
        int sensitivityRadius;
    };

    struct GridLayoutInfo
    {
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
            bool showSpacing;
            int spacing;
            int sensitivityRadius;
        };

        GridLayoutInfo(const Minimal& info);
        GridLayoutInfo(const Full& info);
        ~GridLayoutInfo() = default;

        inline std::vector<int>& rowsPercents() { return m_rowsPercents; };
        inline std::vector<int>& columnsPercents() { return m_columnsPercents; };
        inline std::vector<std::vector<int>>& cellChildMap() { return m_cellChildMap; };

        inline int rows() const { return m_rows; }
        inline int columns() const { return m_columns; }
        inline const std::vector<int>& rowsPercents() const { return m_rowsPercents; };
        inline const std::vector<int>& columnsPercents() const { return m_columnsPercents; };
        inline const std::vector<std::vector<int>>& cellChildMap() const { return m_cellChildMap; };

        inline bool showSpacing() const { return m_showSpacing; }
        inline int spacing() const { return m_spacing; }
        inline int sensitivityRadius() const { return m_sensitivityRadius; }

        int zoneCount() const;

        int m_rows;
        int m_columns;
        std::vector<int> m_rowsPercents;
        std::vector<int> m_columnsPercents;
        std::vector<std::vector<int>> m_cellChildMap;
        bool m_showSpacing;
        int m_spacing;
        int m_sensitivityRadius;
    };

    struct CustomZoneSetData
    {
        std::wstring name;
        CustomLayoutType type;
        std::variant<CanvasLayoutInfo, GridLayoutInfo> info;
    };

    struct ZoneSetData
    {
        std::wstring uuid;
        ZoneSetLayoutType type;
    };

    struct AppZoneHistoryData
    {
        std::unordered_map<DWORD, HWND> processIdToHandleMap; // Maps process id(DWORD) of application to zoned window handle(HWND)

        std::wstring zoneSetUuid;
        std::wstring deviceId;
        std::vector<size_t> zoneIndexSet;
    };

    struct DeviceIdData
    {
        std::wstring deviceName;
        int width;
        int height;
        GUID virtualDesktopId;
        std::wstring monitorId;
    };

    struct DeviceInfoData
    {
        ZoneSetData activeZoneSet;
        bool showSpacing;
        int spacing;
        int zoneCount;
        int sensitivityRadius;
    };
}
