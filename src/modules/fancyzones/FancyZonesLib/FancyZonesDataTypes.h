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
        int lastWorkAreaWidth{};
        int lastWorkAreaHeight{};

        struct Rect
        {
            int x;
            int y;
            int width;
            int height;
        };
        std::vector<CanvasLayoutInfo::Rect> zones;
        int sensitivityRadius{};
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
        bool m_showSpacing{};
        int m_spacing{};
        int m_sensitivityRadius{};
    };

    struct CustomLayoutData
    {
        std::wstring name;
        CustomLayoutType type{};
        std::variant<CanvasLayoutInfo, GridLayoutInfo> info;
    };

    struct ZoneSetData
    {
        std::wstring uuid;
        ZoneSetLayoutType type{};
    };

    struct DeviceId
    {
        std::wstring id;
        std::wstring instanceId;
        int number{};

        bool isDefault() const noexcept;
        std::wstring toString() const noexcept;
    };

    struct MonitorId
    {
        HMONITOR monitor{};
        DeviceId deviceId;
        std::wstring serialNumber;

        std::wstring toString() const noexcept;
    };

    struct WorkAreaId
    {
        MonitorId monitorId;
        GUID virtualDesktopId{};

        std::wstring toString() const noexcept;
    };

    struct AppZoneHistoryData
    {
        std::unordered_map<DWORD, HWND> processIdToHandleMap; // Maps process id(DWORD) of application to zoned window handle(HWND)

        std::wstring zoneSetUuid;
        WorkAreaId workAreaId;
        ZoneIndexSet zoneIndexSet;
    };

    struct DeviceInfoData
    {
        ZoneSetData activeZoneSet;
        bool showSpacing{};
        int spacing{};
        int zoneCount{};
        int sensitivityRadius{};
    };

    inline bool operator==(const ZoneSetData& lhs, const ZoneSetData& rhs)
    {
        return lhs.type == rhs.type && lhs.uuid == rhs.uuid;
    }

    inline bool operator==(const DeviceInfoData& lhs, const DeviceInfoData& rhs)
    {
        return lhs.activeZoneSet == rhs.activeZoneSet && lhs.showSpacing == rhs.showSpacing && lhs.spacing == rhs.spacing && lhs.zoneCount == rhs.zoneCount && lhs.sensitivityRadius == rhs.sensitivityRadius;
    }

    inline bool operator==(const DeviceId& lhs, const DeviceId& rhs)
    {
        if (lhs.id != rhs.id)
        {
            return false;
        }

        if (lhs.instanceId != rhs.instanceId)
        {
            return lhs.number == rhs.number;
        }

        return true;
    }

    inline bool operator<(const DeviceId& lhs, const DeviceId& rhs)
    {
        return lhs.id < rhs.id || lhs.instanceId < rhs.instanceId;
    }

    inline bool operator==(const MonitorId& lhs, const MonitorId& rhs)
    {
        if (lhs.monitor && rhs.monitor)
        {
            return lhs.monitor == rhs.monitor;
        }

        if (!lhs.serialNumber.empty() && !rhs.serialNumber.empty())
        {
            bool serialNumbersEqual = lhs.serialNumber == rhs.serialNumber;
            if (!serialNumbersEqual)
            {
                return false;
            }
        }

        return lhs.deviceId == rhs.deviceId;
    }

    inline bool operator==(const WorkAreaId& lhs, const WorkAreaId& rhs)
    {
        bool vdEqual = (lhs.virtualDesktopId == rhs.virtualDesktopId || lhs.virtualDesktopId == GUID_NULL || rhs.virtualDesktopId == GUID_NULL);
        return vdEqual && lhs.monitorId == rhs.monitorId;
    }

    inline bool operator!=(const WorkAreaId& lhs, const WorkAreaId& rhs)
    {
        bool vdEqual = (lhs.virtualDesktopId == rhs.virtualDesktopId || lhs.virtualDesktopId == GUID_NULL || rhs.virtualDesktopId == GUID_NULL);
        return !vdEqual || lhs.monitorId != rhs.monitorId;
    }

    inline bool operator<(const WorkAreaId& lhs, const WorkAreaId& rhs)
    {
        if (lhs.monitorId.monitor && rhs.monitorId.monitor)
        {
            return lhs.monitorId.monitor < rhs.monitorId.monitor;
        }

        if (lhs.virtualDesktopId != GUID_NULL || rhs.virtualDesktopId != GUID_NULL)
        {
            return lhs.virtualDesktopId.Data1 < rhs.virtualDesktopId.Data1 ||
                   lhs.virtualDesktopId.Data2 < rhs.virtualDesktopId.Data2 ||
                   lhs.virtualDesktopId.Data3 < rhs.virtualDesktopId.Data3;
        }

        if (!lhs.monitorId.serialNumber.empty() || rhs.monitorId.serialNumber.empty())
        {
            return lhs.monitorId.serialNumber < rhs.monitorId.serialNumber;
        }

        return lhs.monitorId.deviceId < rhs.monitorId.deviceId;
    }
}

namespace std
{
    template<>
    struct hash<FancyZonesDataTypes::WorkAreaId>
    {
        size_t operator()(const FancyZonesDataTypes::WorkAreaId& /*Value*/) const
        {
            return 0;
        }
    };
}
