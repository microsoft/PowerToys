#include "pch.h"

#include "util.h"
#include "lib/ZoneSet.h"
#include "Settings.h"

#include <common/dpi_aware.h>

namespace
{
    constexpr int C_MULTIPLIER = 10000;

    /*
      struct GridLayoutInfo {
        int rows;
        int columns;
        int rowsPercents[MAX_ZONE_COUNT];
        int columnsPercents[MAX_ZONE_COUNT];
        int cellChildMap[MAX_ZONE_COUNT][MAX_ZONE_COUNT];
      };
    */

    auto l = JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Minimal{ .rows = 1, .columns = 1 });
    // PriorityGrid layout is unique for zoneCount <= 11. For zoneCount > 11 PriorityGrid is same as Grid
    JSONHelpers::GridLayoutInfo predefinedPriorityGridLayouts[11] = {
        /* 1 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 1,
            .rowsPercents = { 10000 },
            .columnsPercents = { 10000 },
            .cellChildMap = { { 0 } } }),
        /* 2 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 2,
            .rowsPercents = { 10000 },
            .columnsPercents = { 6667, 3333 },
            .cellChildMap = { { 0, 1 } } }),
        /* 3 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 3,
            .rowsPercents = { 10000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 } } }),
        /* 4 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 } } }),
        /* 5 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 } } }),
        /* 6 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 }, { 4, 1, 5 } } }),
        /* 7 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 }, { 5, 1, 6 } } }),
        /* 8 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 2, 7 } } }),
        /* 9 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 7, 8 } } }),
        /* 10 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 1, 8, 9 } } }),
        /* 11 */
        JSONHelpers::GridLayoutInfo(JSONHelpers::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 8, 9, 10 } } }),
    };
}

struct ZoneSet : winrt::implements<ZoneSet, IZoneSet>
{
public:
    ZoneSet(ZoneSetConfig const& config) :
        m_config(config)
    {
    }

    ZoneSet(ZoneSetConfig const& config, std::vector<winrt::com_ptr<IZone>> zones) :
        m_config(config),
        m_zones(zones)
    {
    }

    IFACEMETHODIMP_(GUID)
    Id() noexcept { return m_config.Id; }
    IFACEMETHODIMP_(JSONHelpers::ZoneSetLayoutType)
    LayoutType() noexcept { return m_config.LayoutType; }
    IFACEMETHODIMP AddZone(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(std::vector<int>)
    ZonesFromPoint(POINT pt) noexcept;
    IFACEMETHODIMP_(std::vector<int>)
    GetZoneIndexSetFromWindow(HWND window) noexcept;
    IFACEMETHODIMP_(std::vector<winrt::com_ptr<IZone>>)
    GetZones() noexcept { return m_zones; }
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, HWND zoneWindow, int index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, HWND windowZone, const std::vector<int>& indexSet) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirection(HWND window, HWND zoneWindow, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByPoint(HWND window, HWND zoneWindow, POINT ptClient) noexcept;
    IFACEMETHODIMP_(bool)
    CalculateZones(MONITORINFO monitorInfo, int zoneCount, int spacing) noexcept;
    IFACEMETHODIMP_(bool)
    IsZoneEmpty(int zoneIndex) noexcept;

private:
    bool CalculateFocusLayout(Rect workArea, int zoneCount) noexcept;
    bool CalculateColumnsAndRowsLayout(Rect workArea, JSONHelpers::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateGridLayout(Rect workArea, JSONHelpers::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateUniquePriorityGridLayout(Rect workArea, int zoneCount, int spacing) noexcept;
    bool CalculateCustomLayout(Rect workArea, int spacing) noexcept;
    bool CalculateGridZones(Rect workArea, JSONHelpers::GridLayoutInfo gridLayoutInfo, int spacing);
    void StampWindow(HWND window, size_t bitmask) noexcept;

    std::vector<winrt::com_ptr<IZone>> m_zones;
    std::map<HWND, std::vector<int>> m_windowIndexSet;
    ZoneSetConfig m_config;
};

IFACEMETHODIMP ZoneSet::AddZone(winrt::com_ptr<IZone> zone) noexcept
{
    m_zones.emplace_back(zone);

    // Important not to set Id 0 since we store it in the HWND using SetProp.
    // SetProp(0) doesn't really work.
    zone->SetId(m_zones.size());
    return S_OK;
}

IFACEMETHODIMP_(std::vector<int>)
ZoneSet::ZonesFromPoint(POINT pt) noexcept
{
    const int SENSITIVITY_RADIUS = 20;
    std::vector<int> capturedZones;
    std::vector<int> strictlyCapturedZones;
    for (size_t i = 0; i < m_zones.size(); i++)
    {
        auto zone = m_zones[i];
        RECT newZoneRect = zone->GetZoneRect();
        if (newZoneRect.left < newZoneRect.right && newZoneRect.top < newZoneRect.bottom) // proper zone
        {
            if (newZoneRect.left - SENSITIVITY_RADIUS <= pt.x && pt.x <= newZoneRect.right + SENSITIVITY_RADIUS &&
                newZoneRect.top - SENSITIVITY_RADIUS <= pt.y && pt.y <= newZoneRect.bottom + SENSITIVITY_RADIUS)
            {
                capturedZones.emplace_back(static_cast<int>(i));
            }
            
            if (newZoneRect.left <= pt.x && pt.x < newZoneRect.right &&
                newZoneRect.top <= pt.y && pt.y < newZoneRect.bottom)
            {
                strictlyCapturedZones.emplace_back(static_cast<int>(i));
            }
        }
    }

    // If only one zone is captured, but it's not strictly captured
    // don't consider it as captured
    if (capturedZones.size() == 1 && strictlyCapturedZones.size() == 0)
    {
        return {};
    }

    // If captured zones do not overlap, return all of them
    // Otherwise, return the smallest one

    bool overlap = false;
    for (size_t i = 0; i < capturedZones.size(); ++i)
    {
        for (size_t j = i + 1; j < capturedZones.size(); ++j)
        {
            auto rectI = m_zones[capturedZones[i]]->GetZoneRect();
            auto rectJ = m_zones[capturedZones[j]]->GetZoneRect();
            if (max(rectI.top, rectJ.top) < min(rectI.bottom, rectJ.bottom) &&
                max(rectI.left, rectJ.left) < min(rectI.right, rectJ.right))
            {
                overlap = true;
                i = capturedZones.size() - 1;
                break;
            }
        }
    }

    if (overlap)
    {
        size_t smallestIdx = 0;
        for (size_t i = 1; i < capturedZones.size(); ++i)
        {
            auto rectS = m_zones[capturedZones[smallestIdx]]->GetZoneRect();
            auto rectI = m_zones[capturedZones[i]]->GetZoneRect();
            int smallestSize = (rectS.bottom - rectS.top) * (rectS.right - rectS.left);
            int iSize = (rectI.bottom - rectI.top) * (rectI.right - rectI.left);

            if (iSize <= smallestSize)
            {
                smallestIdx = i;
            }
        }

        capturedZones = { capturedZones[smallestIdx] };
    }

    return capturedZones;
}

std::vector<int> ZoneSet::GetZoneIndexSetFromWindow(HWND window) noexcept
{
    auto it = m_windowIndexSet.find(window);
    if (it == m_windowIndexSet.end())
    {
        return {};
    }
    else
    {
        return it->second;
    }
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByIndex(HWND window, HWND windowZone, int index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, windowZone, { index });
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByIndexSet(HWND window, HWND windowZone, const std::vector<int>& indexSet) noexcept
{
    if (m_zones.empty())
    {
        return;
    }

    RECT size;
    bool sizeEmpty = true;
    size_t bitmask = 0;

    auto& storedIndexSet = m_windowIndexSet[window];
    storedIndexSet = {};

    for (int index : indexSet)
    {
        if (index < static_cast<int>(m_zones.size()))
        {
            RECT newSize = m_zones.at(index)->ComputeActualZoneRect(window, windowZone);
            if (!sizeEmpty)
            {
                size.left = min(size.left, newSize.left);
                size.top = min(size.top, newSize.top);
                size.right = max(size.right, newSize.right);
                size.bottom = max(size.bottom, newSize.bottom);
            }
            else
            {
                size = newSize;
                sizeEmpty = false;
            }

            storedIndexSet.push_back(index);
        }

        if (index < std::numeric_limits<size_t>::digits)
        {
            bitmask |= 1ull << index;
        }
    }

    if (!sizeEmpty)
    {
        SizeWindowToRect(window, size);
        StampWindow(window, bitmask);
    }
}

IFACEMETHODIMP_(bool)
ZoneSet::MoveWindowIntoZoneByDirection(HWND window, HWND windowZone, DWORD vkCode, bool cycle) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    auto indexSet = GetZoneIndexSetFromWindow(window);
    int numZones = static_cast<int>(m_zones.size());

    // The window was not assigned to any zone here
    if (indexSet.size() == 0)
    {
        MoveWindowIntoZoneByIndexSet(window, windowZone, { vkCode == VK_LEFT ? numZones - 1 : 0 });
        return true;
    }

    int oldIndex = indexSet[0];

    // We reached the edge
    if ((vkCode == VK_LEFT && oldIndex == 0) || (vkCode == VK_RIGHT && oldIndex == numZones - 1))
    {
        if (!cycle)
        {
            MoveWindowIntoZoneByIndexSet(window, windowZone, {});
            return false;
        }
        else
        {
            MoveWindowIntoZoneByIndexSet(window, windowZone, { vkCode == VK_LEFT ? numZones - 1 : 0 });
            return true;
        }
    }

    // We didn't reach the edge
    if (vkCode == VK_LEFT)
    {
        MoveWindowIntoZoneByIndexSet(window, windowZone, { oldIndex - 1 });
    }
    else
    {
        MoveWindowIntoZoneByIndexSet(window, windowZone, { oldIndex + 1 });
    }
    return true;
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByPoint(HWND window, HWND zoneWindow, POINT ptClient) noexcept
{
    auto zones = ZonesFromPoint(ptClient);
    MoveWindowIntoZoneByIndexSet(window, zoneWindow, zones);
}

IFACEMETHODIMP_(bool)
ZoneSet::CalculateZones(MONITORINFO monitorInfo, int zoneCount, int spacing) noexcept
{
    Rect const workArea(monitorInfo.rcWork);
    //invalid work area
    if (workArea.width() == 0 || workArea.height() == 0)
    {
        return false;
    }

    //invalid zoneCount, may cause division by zero
    if (zoneCount <= 0 && m_config.LayoutType != JSONHelpers::ZoneSetLayoutType::Custom)
    {
        return false;
    }

    bool success = true;
    switch (m_config.LayoutType)
    {
    case JSONHelpers::ZoneSetLayoutType::Focus:
        success = CalculateFocusLayout(workArea, zoneCount);
        break;
    case JSONHelpers::ZoneSetLayoutType::Columns:
    case JSONHelpers::ZoneSetLayoutType::Rows:
        success = CalculateColumnsAndRowsLayout(workArea, m_config.LayoutType, zoneCount, spacing);
        break;
    case JSONHelpers::ZoneSetLayoutType::Grid:
    case JSONHelpers::ZoneSetLayoutType::PriorityGrid:
        success = CalculateGridLayout(workArea, m_config.LayoutType, zoneCount, spacing);
        break;
    case JSONHelpers::ZoneSetLayoutType::Custom:
        success = CalculateCustomLayout(workArea, spacing);
        break;
    }

    return success;
}

bool ZoneSet::IsZoneEmpty(int zoneIndex) noexcept
{
    for (auto& [window, zones] : m_windowIndexSet)
    {
        if (find(begin(zones), end(zones), zoneIndex) != end(zones))
        {
            return false;
        }
    }

    return true;
}

bool ZoneSet::CalculateFocusLayout(Rect workArea, int zoneCount) noexcept
{
    bool success = true;

    long left{ long(workArea.width() * 0.1) };
    long top{ long(workArea.height() * 0.1) };
    long right{ long(workArea.width() * 0.6) };
    long bottom{ long(workArea.height() * 0.6) };

    RECT focusZoneRect{ left, top, right, bottom };

    long focusRectXIncrement = (zoneCount <= 1) ? 0 : (int)(workArea.width() * 0.2) / (zoneCount - 1);
    long focusRectYIncrement = (zoneCount <= 1) ? 0 : (int)(workArea.height() * 0.2) / (zoneCount - 1);

    if (left >= right || top >= bottom || left < 0 || right < 0 || top < 0 || bottom < 0)
    {
        success = false;
    }

    for (int i = 0; i < zoneCount; i++)
    {
        AddZone(MakeZone(focusZoneRect));
        focusZoneRect.left += focusRectXIncrement;
        focusZoneRect.right += focusRectXIncrement;
        focusZoneRect.bottom += focusRectYIncrement;
        focusZoneRect.top += focusRectYIncrement;
    }

    return success;
}

bool ZoneSet::CalculateColumnsAndRowsLayout(Rect workArea, JSONHelpers::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept
{
    bool success = true;

    long totalWidth;
    long totalHeight;

    if (type == JSONHelpers::ZoneSetLayoutType::Columns)
    {
        totalWidth = workArea.width() - (spacing * (zoneCount + 1));
        totalHeight = workArea.height() - (spacing * 2);
    }
    else
    { //Rows
        totalWidth = workArea.width() - (spacing * 2);
        totalHeight = workArea.height() - (spacing * (zoneCount + 1));
    }

    long top = spacing;
    long left = spacing;
    long bottom;
    long right;

    // Note: The expressions below are NOT equal to total{Width|Height} / zoneCount and are done
    // like this to make the sum of all zones' sizes exactly total{Width|Height}.
    for (int zone = 0; zone < zoneCount; zone++)
    {
        if (type == JSONHelpers::ZoneSetLayoutType::Columns)
        {
            right = left + (zone + 1) * totalWidth / zoneCount - zone * totalWidth / zoneCount;
            bottom = totalHeight + spacing;
        }
        else
        { //Rows
            right = totalWidth + spacing;
            bottom = top + (zone + 1) * totalHeight / zoneCount - zone * totalHeight / zoneCount;
        }
        
        if (left >= right || top >= bottom || left < 0 || right < 0 || top < 0 || bottom < 0)
        {
            success = false;
        }

        RECT focusZoneRect{ left, top, right, bottom };
        AddZone(MakeZone(focusZoneRect));

        if (type == JSONHelpers::ZoneSetLayoutType::Columns)
        {
            left = right + spacing;
        }
        else
        { //Rows
            top = bottom + spacing;
        }
    }

    return success;
}

bool ZoneSet::CalculateGridLayout(Rect workArea, JSONHelpers::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept
{
    const auto count = sizeof(predefinedPriorityGridLayouts) / sizeof(JSONHelpers::GridLayoutInfo);
    if (type == JSONHelpers::ZoneSetLayoutType::PriorityGrid && zoneCount < count)
    {
        return CalculateUniquePriorityGridLayout(workArea, zoneCount, spacing);
    }

    int rows = 1, columns = 1;
    while (zoneCount / rows >= rows)
    {
        rows++;
    }
    rows--;
    columns = zoneCount / rows;
    if (zoneCount % rows == 0)
    {
        // even grid
    }
    else
    {
        columns++;
    }

    JSONHelpers::GridLayoutInfo gridLayoutInfo(JSONHelpers::GridLayoutInfo::Minimal{ .rows = rows, .columns = columns });

    // Note: The expressions below are NOT equal to C_MULTIPLIER / {rows|columns} and are done
    // like this to make the sum of all percents exactly C_MULTIPLIER
    for (int row = 0; row < rows; row++)
    {
        gridLayoutInfo.rowsPercents()[row] = C_MULTIPLIER * (row + 1) / rows - C_MULTIPLIER * row / rows;
    }
    for (int col = 0; col < columns; col++)
    {
        gridLayoutInfo.columnsPercents()[col] = C_MULTIPLIER * (col + 1) / columns - C_MULTIPLIER * col / columns;
    }

    for (int i = 0; i < rows; ++i)
    {
        gridLayoutInfo.cellChildMap()[i] = std::vector<int>(columns);
    }

    int index = 0;
    for (int col = columns - 1; col >= 0; col--)
    {
        for (int row = rows - 1; row >= 0; row--)
        {
            gridLayoutInfo.cellChildMap()[row][col] = index++;
            if (index == zoneCount)
            {
                index--;
            }
        }
    }
    return CalculateGridZones(workArea, gridLayoutInfo, spacing);
}

bool ZoneSet::CalculateUniquePriorityGridLayout(Rect workArea, int zoneCount, int spacing) noexcept
{
    if (zoneCount <= 0 || zoneCount >= sizeof(predefinedPriorityGridLayouts))
    {
        return false;
    }

    return CalculateGridZones(workArea, predefinedPriorityGridLayouts[zoneCount - 1], spacing);
}

bool ZoneSet::CalculateCustomLayout(Rect workArea, int spacing) noexcept
{
    wil::unique_cotaskmem_string guidStr;
    if (SUCCEEDED(StringFromCLSID(m_config.Id, &guidStr)))
    {
        const std::wstring guid = guidStr.get();

        const auto zoneSetSearchResult = JSONHelpers::FancyZonesDataInstance().FindCustomZoneSet(guid);

        if (!zoneSetSearchResult.has_value())
        {
            return false;
        }

        const auto& zoneSet = *zoneSetSearchResult;
        if (zoneSet.type == JSONHelpers::CustomLayoutType::Canvas && std::holds_alternative<JSONHelpers::CanvasLayoutInfo>(zoneSet.info))
        {
            const auto& zoneSetInfo = std::get<JSONHelpers::CanvasLayoutInfo>(zoneSet.info);
            for (const auto& zone : zoneSetInfo.zones)
            {
                int x = zone.x;
                int y = zone.y;
                int width = zone.width;
                int height = zone.height;

                if (x < 0 || y < 0 || width < 0 || height < 0)
                {
                    return false;
                }

                DPIAware::Convert(m_config.Monitor, x, y);
                DPIAware::Convert(m_config.Monitor, width, height);

                AddZone(MakeZone(RECT{ x, y, x + width, y + height }));
            }

            return true;
        }
        else if (zoneSet.type == JSONHelpers::CustomLayoutType::Grid && std::holds_alternative<JSONHelpers::GridLayoutInfo>(zoneSet.info))
        {
            const auto& info = std::get<JSONHelpers::GridLayoutInfo>(zoneSet.info);
            return CalculateGridZones(workArea, info, spacing);
        }
    }

    return false;
}

bool ZoneSet::CalculateGridZones(Rect workArea, JSONHelpers::GridLayoutInfo gridLayoutInfo, int spacing)
{
    bool success = true;

    long totalWidth = workArea.width() - (spacing * (gridLayoutInfo.columns() + 1));
    long totalHeight = workArea.height() - (spacing * (gridLayoutInfo.rows() + 1));
    struct Info
    {
        long Extent;
        long Start;
        long End;
    };
    std::vector<Info> rowInfo(gridLayoutInfo.rows());
    std::vector<Info> columnInfo(gridLayoutInfo.columns());

    // Note: The expressions below are carefully written to 
    // make the sum of all zones' sizes exactly total{Width|Height}
    int totalPercents = 0;
    for (int row = 0; row < gridLayoutInfo.rows(); row++)
    {
        rowInfo[row].Start = totalPercents * totalHeight / C_MULTIPLIER + (row + 1) * spacing;
        totalPercents += gridLayoutInfo.rowsPercents()[row];
        rowInfo[row].End = totalPercents * totalHeight / C_MULTIPLIER + (row + 1) * spacing;
        rowInfo[row].Extent = rowInfo[row].End - rowInfo[row].Start;
    }

    totalPercents = 0;
    for (int col = 0; col < gridLayoutInfo.columns(); col++)
    {
        columnInfo[col].Start = totalPercents * totalWidth / C_MULTIPLIER + (col + 1) * spacing;
        totalPercents += gridLayoutInfo.columnsPercents()[col];
        columnInfo[col].End = totalPercents * totalWidth / C_MULTIPLIER + (col + 1) * spacing;
        columnInfo[col].Extent = columnInfo[col].End - columnInfo[col].Start;
    }

    for (int row = 0; row < gridLayoutInfo.rows(); row++)
    {
        for (int col = 0; col < gridLayoutInfo.columns(); col++)
        {
            int i = gridLayoutInfo.cellChildMap()[row][col];
            if (((row == 0) || (gridLayoutInfo.cellChildMap()[row - 1][col] != i)) &&
                ((col == 0) || (gridLayoutInfo.cellChildMap()[row][col - 1] != i)))
            {
                long left = columnInfo[col].Start;
                long top = rowInfo[row].Start;

                int maxRow = row;
                while (((maxRow + 1) < gridLayoutInfo.rows()) && (gridLayoutInfo.cellChildMap()[maxRow + 1][col] == i))
                {
                    maxRow++;
                }
                int maxCol = col;
                while (((maxCol + 1) < gridLayoutInfo.columns()) && (gridLayoutInfo.cellChildMap()[row][maxCol + 1] == i))
                {
                    maxCol++;
                }

                long right = columnInfo[maxCol].End;
                long bottom = rowInfo[maxRow].End;

                if (left >= right || top >= bottom || left < 0 || right < 0 || top < 0 || bottom < 0)
                {
                    success = false;
                }

                AddZone(MakeZone(RECT{ left, top, right, bottom }));
            }
        }
    }

    return success;
}

void ZoneSet::StampWindow(HWND window, size_t bitmask) noexcept
{
    SetProp(window, MULTI_ZONE_STAMP, reinterpret_cast<HANDLE>(bitmask));
}

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept
{
    return winrt::make_self<ZoneSet>(config);
}
