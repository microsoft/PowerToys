#include "pch.h"
#include "util.h"

#include "FancyZonesDataTypes.h"
#include "JsonHelpers.h"

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
}
