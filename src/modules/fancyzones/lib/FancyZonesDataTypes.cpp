#include "pch.h"
#include "util.h"

#include "FancyZonesDataTypes.h"

#include <sstream>

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

    bool DeviceIdData::empty() const
    {
        return *this == DeviceIdData{};
    }

    std::wstring DeviceIdData::Serialize() const
    {
        std::wstring vdId = L"{00000000-0000-0000-0000-000000000000}";
        wil::unique_cotaskmem_string virtualDesktopIdStr;
        if (SUCCEEDED(StringFromCLSID(virtualDesktopId, &virtualDesktopIdStr)))
        {
            vdId = std::wstring{ virtualDesktopIdStr.get() };
        }

        return deviceName + L"_" + std::to_wstring(width) + L"_" + std::to_wstring(height) + L"_" + vdId;
    }

    DeviceIdData DeviceIdData::Parse(const std::wstring& deviceId)
    {
        DeviceIdData data;

        std::wstring temp;
        std::wstringstream wss(deviceId);

        /*
        Important fix for device info that contains a '_' in the name:
        1. first search for '#'
        2. Then split the remaining string by '_'
        */

        // Step 1: parse the name until the #, then to the '_'
        if (deviceId.find(L'#') != std::string::npos)
        {
            std::getline(wss, temp, L'#');

            data.deviceName = temp;

            if (!std::getline(wss, temp, L'_'))
            {
                return {};
            }

            data.deviceName += L"#" + temp;
        }
        else if (std::getline(wss, temp, L'_') && !temp.empty())
        {
            data.deviceName = temp;
        }
        else
        {
            return {};
        }

        // Step 2: parse the rest of the id
        std::vector<std::wstring> parts;
        while (std::getline(wss, temp, L'_'))
        {
            parts.push_back(temp);
        }

        if (parts.size() != 3 && parts.size() != 4)
        {
            return {};
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
                std::stoi(std::wstring(&c));
            }

            for (const auto& c : parts[1])
            {
                std::stoi(std::wstring(&c));
            }

            data.width = std::stoi(parts[0]);
            data.height = std::stoi(parts[1]);
        }
        catch (const std::exception&)
        {
            return {};
        }

        if (!SUCCEEDED(CLSIDFromString(parts[2].c_str(), &data.virtualDesktopId)))
        {
            return {};
        }

        if (parts.size() == 4)
        {
            data.monitorId = parts[3]; //could be empty
        }

        return data;
    }
}
