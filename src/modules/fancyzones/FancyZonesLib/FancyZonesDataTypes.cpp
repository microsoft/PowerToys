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

    std::optional<DeviceIdData> DeviceIdData::ParseDeviceId(const std::wstring& str)
    {
        FancyZonesDataTypes::DeviceIdData data;

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

    std::wstring DeviceIdData::toString() const
    {
        wil::unique_cotaskmem_string virtualDesktopIdStr;
        if (!SUCCEEDED(StringFromCLSID(virtualDesktopId, &virtualDesktopIdStr)))
        {
            return std::wstring();
        }

        std::wstring result = deviceName + L"_" + std::to_wstring(width) + L"_" + std::to_wstring(height) + L"_" + virtualDesktopIdStr.get();
        if (!monitorId.empty())
        {
            result += L"_" + monitorId;
        }

        return result;
    }
}
