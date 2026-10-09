#include "pch.h"
#include "util.h"

#include <sstream>

#include "FancyZonesDataTypes.h"

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t BlankStr[] = L"blank";
    const wchar_t ColumnsStr[] = L"columns";
    const wchar_t CustomStr[] = L"custom";
    const wchar_t FocusStr[] = L"focus";
    const wchar_t GridStr[] = L"grid";
    const wchar_t PriorityGridStr[] = L"priority-grid";
    const wchar_t RowsStr[] = L"rows";
    const wchar_t TypeToStringErrorStr[] = L"TypeToString_ERROR";
}

namespace
{
    // From Settings.cs
    constexpr int c_focusModelId = 0xFFFF;
    constexpr int c_rowsModelId = 0xFFFE;
    constexpr int c_columnsModelId = 0xFFFD;
    constexpr int c_gridModelId = 0xFFFC;
    constexpr int c_priorityGridModelId = 0xFFFB;
    constexpr int c_blankCustomModelId = 0xFFFA;
}

namespace FancyZonesDataTypes
{
    std::wstring TypeToString(ZoneSetLayoutType type)
    {
        switch (type)
        {
        case ZoneSetLayoutType::Blank:
            return NonLocalizable::BlankStr;
        case ZoneSetLayoutType::Focus:
            return NonLocalizable::FocusStr;
        case ZoneSetLayoutType::Columns:
            return NonLocalizable::ColumnsStr;
        case ZoneSetLayoutType::Rows:
            return NonLocalizable::RowsStr;
        case ZoneSetLayoutType::Grid:
            return NonLocalizable::GridStr;
        case ZoneSetLayoutType::PriorityGrid:
            return NonLocalizable::PriorityGridStr;
        case ZoneSetLayoutType::Custom:
            return NonLocalizable::CustomStr;
        default:
            return NonLocalizable::TypeToStringErrorStr;
        }
    }

    ZoneSetLayoutType TypeFromString(const std::wstring& typeStr)
    {
        if (typeStr == NonLocalizable::FocusStr)
        {
            return ZoneSetLayoutType::Focus;
        }
        else if (typeStr == NonLocalizable::ColumnsStr)
        {
            return ZoneSetLayoutType::Columns;
        }
        else if (typeStr == NonLocalizable::RowsStr)
        {
            return ZoneSetLayoutType::Rows;
        }
        else if (typeStr == NonLocalizable::GridStr)
        {
            return ZoneSetLayoutType::Grid;
        }
        else if (typeStr == NonLocalizable::PriorityGridStr)
        {
            return ZoneSetLayoutType::PriorityGrid;
        }
        else if (typeStr == NonLocalizable::CustomStr)
        {
            return ZoneSetLayoutType::Custom;
        }
        else
        {
            return ZoneSetLayoutType::Blank;
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

    int GridLayoutInfo::zoneCount() const
    {
        int high = 0;
        for (const auto& row : m_cellChildMap)
        {
            for (int val : row)
            {
                high = max(high, val);
            }
        }

        return high + 1;
    }
    
    std::wstring DeviceId::toString() const noexcept
    {
        return id + L"_" + instanceId + L"_" + std::to_wstring(number);
    }

    bool DeviceId::isDefault() const noexcept
    {
        static const std::wstring defaultMonitorId = L"Default_Monitor";
        return id == defaultMonitorId;
    }
    
    std::wstring MonitorId::toString() const noexcept
    {
        return deviceId.toString() + L"_" + serialNumber;
    }

    std::wstring WorkAreaId::toString() const noexcept
    {
        wil::unique_cotaskmem_string virtualDesktopIdStr;
        if (!SUCCEEDED(StringFromCLSID(virtualDesktopId, &virtualDesktopIdStr)))
        {
            return std::wstring();
        }

        std::wstring result = monitorId.toString() + L"_" + virtualDesktopIdStr.get();

        return result;
    }
}
