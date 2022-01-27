#pragma once

#include <common/utils/json.h>

#include <string>
#include <vector>
#include <optional>
#include <variant>
#include <unordered_map>

#include <windef.h>

#include <FancyZonesLib/Zone.h>

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

    struct CustomLayoutData
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

    struct DeviceIdData
    {
        std::wstring deviceName = L"FallbackDevice";
        int width;
        int height;
        GUID virtualDesktopId;
        std::wstring monitorId;

        static std::optional<DeviceIdData> ParseDeviceId(const std::wstring& str);
        static bool IsValidDeviceId(const std::wstring& str);
        
        std::wstring toString() const;
    };

    struct AppZoneHistoryData
    {
        std::unordered_map<DWORD, HWND> processIdToHandleMap; // Maps process id(DWORD) of application to zoned window handle(HWND)

        std::wstring zoneSetUuid;
        DeviceIdData deviceId;
        ZoneIndexSet zoneIndexSet;
    };

    struct DeviceInfoData
    {
        ZoneSetData activeZoneSet;
        bool showSpacing;
        int spacing;
        int zoneCount;
        int sensitivityRadius;
    };

    inline bool operator==(const ZoneSetData& lhs, const ZoneSetData& rhs)
    {
        return lhs.type == rhs.type && lhs.uuid == rhs.uuid;
    }

    inline bool operator==(const DeviceIdData& lhs, const DeviceIdData& rhs)
    {
        return lhs.deviceName.compare(rhs.deviceName) == 0 && lhs.width == rhs.width && lhs.height == rhs.height && (lhs.virtualDesktopId == rhs.virtualDesktopId || lhs.virtualDesktopId == GUID_NULL || rhs.virtualDesktopId == GUID_NULL) && lhs.monitorId.compare(rhs.monitorId) == 0;
    }

    inline bool operator!=(const DeviceIdData& lhs, const DeviceIdData& rhs)
    {
        return !(lhs == rhs);
    }

    inline bool operator<(const DeviceIdData& lhs, const DeviceIdData& rhs)
    {
        return lhs.deviceName.compare(rhs.deviceName) < 0 || lhs.width < rhs.width || lhs.height < rhs.height || lhs.monitorId.compare(rhs.monitorId) < 0;
    }

    inline bool operator==(const DeviceInfoData& lhs, const DeviceInfoData& rhs)
    {
        return lhs.activeZoneSet == rhs.activeZoneSet && lhs.showSpacing == rhs.showSpacing && lhs.spacing == rhs.spacing && lhs.zoneCount == rhs.zoneCount && lhs.sensitivityRadius == rhs.sensitivityRadius;
    }
}

namespace std
{
    template<>
    struct hash<FancyZonesDataTypes::DeviceIdData>
    {
        size_t operator()(const FancyZonesDataTypes::DeviceIdData& Value) const
        {
            return 0;
        }
    };
}
