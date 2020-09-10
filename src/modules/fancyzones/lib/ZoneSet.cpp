#include "pch.h"

#include "util.h"
#include "lib/ZoneSet.h"
#include "Settings.h"
#include "FancyZonesData.h"
#include "FancyZonesDataTypes.h"

#include <common/dpi_aware.h>

#include <utility>

using namespace FancyZonesUtils;

namespace
{
    constexpr int C_MULTIPLIER = 10000;
    constexpr int MAX_ZONE_COUNT = 50;

    /*
      struct GridLayoutInfo {
        int rows;
        int columns;
        int rowsPercents[MAX_ZONE_COUNT];
        int columnsPercents[MAX_ZONE_COUNT];
        int cellChildMap[MAX_ZONE_COUNT][MAX_ZONE_COUNT];
      };
    */

    auto l = FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Minimal{ .rows = 1, .columns = 1 });
    // PriorityGrid layout is unique for zoneCount <= 11. For zoneCount > 11 PriorityGrid is same as Grid
    FancyZonesDataTypes::GridLayoutInfo predefinedPriorityGridLayouts[11] = {
        /* 1 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 1,
            .rowsPercents = { 10000 },
            .columnsPercents = { 10000 },
            .cellChildMap = { { 0 } } }),
        /* 2 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 2,
            .rowsPercents = { 10000 },
            .columnsPercents = { 6667, 3333 },
            .cellChildMap = { { 0, 1 } } }),
        /* 3 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 1,
            .columns = 3,
            .rowsPercents = { 10000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 } } }),
        /* 4 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 } } }),
        /* 5 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 2,
            .columns = 3,
            .rowsPercents = { 5000, 5000 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 } } }),
        /* 6 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 0, 1, 3 }, { 4, 1, 5 } } }),
        /* 7 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 3,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 5000, 2500 },
            .cellChildMap = { { 0, 1, 2 }, { 3, 1, 4 }, { 5, 1, 6 } } }),
        /* 8 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 2, 7 } } }),
        /* 9 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 2, 5 }, { 6, 1, 7, 8 } } }),
        /* 10 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
            .rows = 3,
            .columns = 4,
            .rowsPercents = { 3333, 3334, 3333 },
            .columnsPercents = { 2500, 2500, 2500, 2500 },
            .cellChildMap = { { 0, 1, 2, 3 }, { 4, 1, 5, 6 }, { 7, 1, 8, 9 } } }),
        /* 11 */
        FancyZonesDataTypes::GridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Full{
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
    IFACEMETHODIMP_(FancyZonesDataTypes::ZoneSetLayoutType)
    LayoutType() noexcept { return m_config.LayoutType; }
    IFACEMETHODIMP AddZone(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(std::vector<size_t>)
    ZonesFromPoint(POINT pt) noexcept;
    IFACEMETHODIMP_(std::vector<size_t>)
    GetZoneIndexSetFromWindow(HWND window) noexcept;
    IFACEMETHODIMP_(std::vector<winrt::com_ptr<IZone>>)
    GetZones() noexcept { return m_zones; }
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, HWND zoneWindow, size_t index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, HWND windowZone, const std::vector<size_t>& indexSet) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndIndex(HWND window, HWND zoneWindow, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndPosition(HWND window, HWND zoneWindow, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    ExtendWindowByDirectionAndPosition(HWND window, HWND windowZone, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByPoint(HWND window, HWND zoneWindow, POINT ptClient) noexcept;
    IFACEMETHODIMP_(bool)
    CalculateZones(RECT workArea, int zoneCount, int spacing) noexcept;
    IFACEMETHODIMP_(bool)
    IsZoneEmpty(int zoneIndex) noexcept;
    IFACEMETHODIMP_(std::vector<size_t>)
    GetCombinedZoneRange(const std::vector<size_t>& initialZones, const std::vector<size_t>& finalZones) noexcept;

private:
    bool CalculateFocusLayout(Rect workArea, int zoneCount) noexcept;
    bool CalculateColumnsAndRowsLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateGridLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateUniquePriorityGridLayout(Rect workArea, int zoneCount, int spacing) noexcept;
    bool CalculateCustomLayout(Rect workArea, int spacing) noexcept;
    bool CalculateGridZones(Rect workArea, FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo, int spacing);
    void StampWindow(HWND window, size_t bitmask) noexcept;

    std::vector<winrt::com_ptr<IZone>> m_zones;
    std::map<HWND, std::vector<size_t>> m_windowIndexSet;

    // Needed for ExtendWindowByDirectionAndPosition
    std::map<HWND, std::vector<size_t>> m_windowInitialIndexSet;
    std::map<HWND, size_t> m_windowFinalIndex;
    bool m_inExtendWindow = false;

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

IFACEMETHODIMP_(std::vector<size_t>)
ZoneSet::ZonesFromPoint(POINT pt) noexcept
{
    const int SENSITIVITY_RADIUS = 20;
    std::vector<size_t> capturedZones;
    std::vector<size_t> strictlyCapturedZones;
    for (size_t i = 0; i < m_zones.size(); i++)
    {
        auto zone = m_zones[i];
        RECT newZoneRect = zone->GetZoneRect();
        if (newZoneRect.left < newZoneRect.right && newZoneRect.top < newZoneRect.bottom) // proper zone
        {
            if (newZoneRect.left - SENSITIVITY_RADIUS <= pt.x && pt.x <= newZoneRect.right + SENSITIVITY_RADIUS &&
                newZoneRect.top - SENSITIVITY_RADIUS <= pt.y && pt.y <= newZoneRect.bottom + SENSITIVITY_RADIUS)
            {
                capturedZones.emplace_back(i);
            }
            
            if (newZoneRect.left <= pt.x && pt.x < newZoneRect.right &&
                newZoneRect.top <= pt.y && pt.y < newZoneRect.bottom)
            {
                strictlyCapturedZones.emplace_back(i);
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
            if (max(rectI.top, rectJ.top) + SENSITIVITY_RADIUS < min(rectI.bottom, rectJ.bottom) &&
                max(rectI.left, rectJ.left) + SENSITIVITY_RADIUS < min(rectI.right, rectJ.right))
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

std::vector<size_t> ZoneSet::GetZoneIndexSetFromWindow(HWND window) noexcept
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
ZoneSet::MoveWindowIntoZoneByIndex(HWND window, HWND windowZone, size_t index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, windowZone, { index });
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByIndexSet(HWND window, HWND windowZone, const std::vector<size_t>& indexSet) noexcept
{
    if (m_zones.empty())
    {
        return;
    }

    // Always clear the info related to SelectManyZones if it's not being used
    if (!m_inExtendWindow)
    {
        m_windowFinalIndex.erase(window);
        m_windowInitialIndexSet.erase(window);
    }

    RECT size;
    bool sizeEmpty = true;
    size_t bitmask = 0;

    auto& storedIndexSet = m_windowIndexSet[window];
    storedIndexSet = {};

    for (size_t index : indexSet)
    {
        if (index < m_zones.size())
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
        SaveWindowSizeAndOrigin(window);
        SizeWindowToRect(window, size);
        StampWindow(window, bitmask);
    }
}

IFACEMETHODIMP_(bool)
ZoneSet::MoveWindowIntoZoneByDirectionAndIndex(HWND window, HWND windowZone, DWORD vkCode, bool cycle) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    auto indexSet = GetZoneIndexSetFromWindow(window);
    size_t numZones = m_zones.size();

    // The window was not assigned to any zone here
    if (indexSet.size() == 0)
    {
        MoveWindowIntoZoneByIndexSet(window, windowZone, { vkCode == VK_LEFT ? numZones - 1 : 0 });
        return true;
    }

    size_t oldIndex = indexSet[0];

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

IFACEMETHODIMP_(bool)
ZoneSet::MoveWindowIntoZoneByDirectionAndPosition(HWND window, HWND windowZone, DWORD vkCode, bool cycle) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    
    RECT windowRect, windowZoneRect;
    if (GetWindowRect(window, &windowRect) && GetWindowRect(windowZone, &windowZoneRect))
    {
        auto zoneObjects = GetZones();
        auto oldZones = GetZoneIndexSetFromWindow(window);
        std::vector<bool> usedZoneIndices(zoneObjects.size(), false);
        std::vector<RECT> zoneRects;
        std::vector<size_t> freeZoneIndices;
        
        // If selectManyZones = true for the second time, use the last zone into which we moved
        // instead of the window rect and enable moving to all zones except the old one
        auto finalIndexIt = m_windowFinalIndex.find(window);
        if (false && finalIndexIt != m_windowFinalIndex.end())
        {
            usedZoneIndices[finalIndexIt->second] = true;
            windowRect = zoneObjects[finalIndexIt->second]->GetZoneRect();
        }
        else
        {
            for (size_t idx : oldZones)
            {
                usedZoneIndices[idx] = true;
            }
            // Move to coordinates relative to windowZone
            windowRect.top -= windowZoneRect.top;
            windowRect.bottom -= windowZoneRect.top;
            windowRect.left -= windowZoneRect.left;
            windowRect.right -= windowZoneRect.left;
        }

        for (size_t i = 0; i < zoneObjects.size(); i++)
        {
            if (!usedZoneIndices[i])
            {
                zoneRects.emplace_back(zoneObjects[i]->GetZoneRect());
                freeZoneIndices.emplace_back(i);
            }
        }

        size_t result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (result < zoneRects.size())
        {
            size_t targetZone = freeZoneIndices[result];
            std::vector<size_t> resultIndexSet;
            if (false)
            {
                // First time with selectManyZones = true for this window?
                if (finalIndexIt == m_windowFinalIndex.end())
                {
                    // Already zoned?
                    if (oldZones.size())
                    {
                        m_windowInitialIndexSet[window] = oldZones;
                        m_windowFinalIndex[window] = targetZone;
                        resultIndexSet = GetCombinedZoneRange(oldZones, { targetZone });
                    }
                    else
                    {
                        m_windowInitialIndexSet[window] = { targetZone };
                        m_windowFinalIndex[window] = targetZone;
                        resultIndexSet = { targetZone };
                    }
                }
                else
                {
                    auto deletethis = m_windowInitialIndexSet[window];
                    m_windowFinalIndex[window] = targetZone;
                    resultIndexSet = GetCombinedZoneRange(m_windowInitialIndexSet[window], { targetZone });
                }
            }
            else
            {
                resultIndexSet = { targetZone };
            }
            m_inExtendWindow = false;
            MoveWindowIntoZoneByIndexSet(window, windowZone, resultIndexSet);
            m_inExtendWindow = false;
            return true;
        }
        else if (cycle)
        {
            // Try again from the position off the screen in the opposite direction to vkCode
            windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, windowZoneRect, vkCode);
            result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

            if (result < zoneRects.size())
            {
                MoveWindowIntoZoneByIndex(window, windowZone, freeZoneIndices[result]);
                return true;
            }
        }
    }

    return false;
}

IFACEMETHODIMP_(bool)
ZoneSet::ExtendWindowByDirectionAndPosition(HWND window, HWND windowZone, DWORD vkCode) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    RECT windowRect, windowZoneRect;
    if (GetWindowRect(window, &windowRect) && GetWindowRect(windowZone, &windowZoneRect))
    {
        auto zoneObjects = GetZones();
        auto oldZones = GetZoneIndexSetFromWindow(window);
        std::vector<bool> usedZoneIndices(zoneObjects.size(), false);
        std::vector<RECT> zoneRects;
        std::vector<size_t> freeZoneIndices;

        // If selectManyZones = true for the second time, use the last zone into which we moved
        // instead of the window rect and enable moving to all zones except the old one
        auto finalIndexIt = m_windowFinalIndex.find(window);
        if (true && finalIndexIt != m_windowFinalIndex.end())
        {
            usedZoneIndices[finalIndexIt->second] = true;
            windowRect = zoneObjects[finalIndexIt->second]->GetZoneRect();
        }
        else
        {
            for (size_t idx : oldZones)
            {
                usedZoneIndices[idx] = true;
            }
            // Move to coordinates relative to windowZone
            windowRect.top -= windowZoneRect.top;
            windowRect.bottom -= windowZoneRect.top;
            windowRect.left -= windowZoneRect.left;
            windowRect.right -= windowZoneRect.left;
        }

        for (size_t i = 0; i < zoneObjects.size(); i++)
        {
            if (!usedZoneIndices[i])
            {
                zoneRects.emplace_back(zoneObjects[i]->GetZoneRect());
                freeZoneIndices.emplace_back(i);
            }
        }

        size_t result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (result < zoneRects.size())
        {
            size_t targetZone = freeZoneIndices[result];
            std::vector<size_t> resultIndexSet;
            if (true)
            {
                // First time with selectManyZones = true for this window?
                if (finalIndexIt == m_windowFinalIndex.end())
                {
                    // Already zoned?
                    if (oldZones.size())
                    {
                        m_windowInitialIndexSet[window] = oldZones;
                        m_windowFinalIndex[window] = targetZone;
                        resultIndexSet = GetCombinedZoneRange(oldZones, { targetZone });
                    }
                    else
                    {
                        m_windowInitialIndexSet[window] = { targetZone };
                        m_windowFinalIndex[window] = targetZone;
                        resultIndexSet = { targetZone };
                    }
                }
                else
                {
                    auto deletethis = m_windowInitialIndexSet[window];
                    m_windowFinalIndex[window] = targetZone;
                    resultIndexSet = GetCombinedZoneRange(m_windowInitialIndexSet[window], { targetZone });
                }
            }
            else
            {
                resultIndexSet = { targetZone };
            }
            m_inExtendWindow = true;
            MoveWindowIntoZoneByIndexSet(window, windowZone, resultIndexSet);
            m_inExtendWindow = false;
            return true;
        }
        else if (false)
        {
            // Try again from the position off the screen in the opposite direction to vkCode
            windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, windowZoneRect, vkCode);
            result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

            if (result < zoneRects.size())
            {
                MoveWindowIntoZoneByIndex(window, windowZone, freeZoneIndices[result]);
                return true;
            }
        }
    }

    return false;
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByPoint(HWND window, HWND zoneWindow, POINT ptClient) noexcept
{
    auto zones = ZonesFromPoint(ptClient);
    MoveWindowIntoZoneByIndexSet(window, zoneWindow, zones);
}

IFACEMETHODIMP_(bool)
ZoneSet::CalculateZones(RECT workAreaRect, int zoneCount, int spacing) noexcept
{
    Rect workArea(workAreaRect);
    //invalid work area
    if (workArea.width() == 0 || workArea.height() == 0)
    {
        return false;
    }

    //invalid zoneCount, may cause division by zero
    if (zoneCount <= 0 && m_config.LayoutType != FancyZonesDataTypes::ZoneSetLayoutType::Custom)
    {
        return false;
    }

    bool success = true;
    switch (m_config.LayoutType)
    {
    case FancyZonesDataTypes::ZoneSetLayoutType::Focus:
        success = CalculateFocusLayout(workArea, zoneCount);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Columns:
    case FancyZonesDataTypes::ZoneSetLayoutType::Rows:
        success = CalculateColumnsAndRowsLayout(workArea, m_config.LayoutType, zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Grid:
    case FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid:
        success = CalculateGridLayout(workArea, m_config.LayoutType, zoneCount, spacing);
        break;
    case FancyZonesDataTypes::ZoneSetLayoutType::Custom:
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

    long left{ 100 };
    long top{ 100 };
    long right{ left + long(workArea.width() * 0.4) };
    long bottom{ top + long(workArea.height() * 0.4) };

    RECT focusZoneRect{ left, top, right, bottom };

    long focusRectXIncrement = (zoneCount <= 1) ? 0 : 50;
    long focusRectYIncrement = (zoneCount <= 1) ? 0 : 50;

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

bool ZoneSet::CalculateColumnsAndRowsLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept
{
    bool success = true;

    long totalWidth;
    long totalHeight;

    if (type == FancyZonesDataTypes::ZoneSetLayoutType::Columns)
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
        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Columns)
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

        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Columns)
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

bool ZoneSet::CalculateGridLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept
{
    const auto count = sizeof(predefinedPriorityGridLayouts) / sizeof(FancyZonesDataTypes::GridLayoutInfo);
    if (type == FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid && zoneCount < count)
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

    FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo(FancyZonesDataTypes::GridLayoutInfo::Minimal{ .rows = rows, .columns = columns });

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

        const auto zoneSetSearchResult = FancyZonesDataInstance().FindCustomZoneSet(guid);

        if (!zoneSetSearchResult.has_value())
        {
            return false;
        }

        const auto& zoneSet = *zoneSetSearchResult;
        if (zoneSet.type == FancyZonesDataTypes::CustomLayoutType::Canvas && std::holds_alternative<FancyZonesDataTypes::CanvasLayoutInfo>(zoneSet.info))
        {
            const auto& zoneSetInfo = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(zoneSet.info);
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
        else if (zoneSet.type == FancyZonesDataTypes::CustomLayoutType::Grid && std::holds_alternative<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info))
        {
            const auto& info = std::get<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info);
            return CalculateGridZones(workArea, info, spacing);
        }
    }

    return false;
}

bool ZoneSet::CalculateGridZones(Rect workArea, FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo, int spacing)
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
    SetProp(window, ZonedWindowProperties::PropertyMultipleZoneID, reinterpret_cast<HANDLE>(bitmask));
}

std::vector<size_t> ZoneSet::GetCombinedZoneRange(const std::vector<size_t>& initialZones, const std::vector<size_t>& finalZones) noexcept
{
    std::vector<size_t> combinedZones, result;
    std::set_union(begin(initialZones), end(initialZones), begin(finalZones), end(finalZones), std::back_inserter(combinedZones));

    RECT boundingRect;
    bool boundingRectEmpty = true;
    auto zones = GetZones();

    for (size_t zoneId : combinedZones)
    {
        RECT rect = zones[zoneId]->GetZoneRect();
        if (boundingRectEmpty)
        {
            boundingRect = rect;
            boundingRectEmpty = false;
        }
        else
        {
            boundingRect.left = min(boundingRect.left, rect.left);
            boundingRect.top = min(boundingRect.top, rect.top);
            boundingRect.right = max(boundingRect.right, rect.right);
            boundingRect.bottom = max(boundingRect.bottom, rect.bottom);
        }
    }

    if (!boundingRectEmpty)
    {
        for (size_t zoneId = 0; zoneId < zones.size(); zoneId++)
        {
            RECT rect = zones[zoneId]->GetZoneRect();
            if (boundingRect.left <= rect.left && rect.right <= boundingRect.right &&
                boundingRect.top <= rect.top && rect.bottom <= boundingRect.bottom)
            {
                result.push_back(zoneId);
            }
        }
    }

    return result;
}

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept
{
    return winrt::make_self<ZoneSet>(config);
}

