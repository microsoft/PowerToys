#include "pch.h"

#include "ZoneSet.h"

#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include "FancyZonesDataTypes.h"
#include "FancyZonesWindowProperties.h"
#include "Settings.h"
#include "Zone.h"
#include <FancyZonesLib/util.h>
#include <FancyZonesLib/WindowUtils.h>

#include <common/logger/logger.h>
#include <common/display/dpi_aware.h>

#include <limits>
#include <map>
#include <utility>

using namespace FancyZonesUtils;

namespace
{
    constexpr int C_MULTIPLIER = 10000;
    constexpr int OVERLAPPING_CENTERS_SENSITIVITY = 75;

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

    ZoneSet(ZoneSetConfig const& config, ZonesMap zones) :
        m_config(config),
        m_zones(zones)
    {
    }

    IFACEMETHODIMP_(GUID)
    Id() const noexcept { return m_config.Id; }
    IFACEMETHODIMP_(FancyZonesDataTypes::ZoneSetLayoutType)
    LayoutType() const noexcept { return m_config.LayoutType; }
    IFACEMETHODIMP AddZone(winrt::com_ptr<IZone> zone) noexcept;
    IFACEMETHODIMP_(ZoneIndexSet) ZonesFromPoint(POINT pt) const noexcept;
    IFACEMETHODIMP_(ZoneIndexSet) GetZoneIndexSetFromWindow(HWND window) const noexcept;
    IFACEMETHODIMP_(ZonesMap) GetZones()const noexcept override { return m_zones; }
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndex(HWND window, HWND workAreaWindow, ZoneIndex index) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByIndexSet(HWND window, HWND workAreaWindow, const ZoneIndexSet& indexSet, bool suppressMove = false) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndIndex(HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    MoveWindowIntoZoneByDirectionAndPosition(HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) noexcept;
    IFACEMETHODIMP_(bool)
    ExtendWindowByDirectionAndPosition(HWND window, HWND workAreaWindow, DWORD vkCode) noexcept;
    IFACEMETHODIMP_(void)
    MoveWindowIntoZoneByPoint(HWND window, HWND workAreaWindow, POINT ptClient) noexcept;
    IFACEMETHODIMP_(void)
    DismissWindow(HWND window) noexcept;
    IFACEMETHODIMP_(void)
    CycleTabs(HWND window, bool reverse) noexcept;
    IFACEMETHODIMP_(bool)
    CalculateZones(RECT workArea, int zoneCount, int spacing) noexcept;
    IFACEMETHODIMP_(bool) IsZoneEmpty(ZoneIndex zoneIndex) const noexcept;
    IFACEMETHODIMP_(ZoneIndexSet) GetCombinedZoneRange(const ZoneIndexSet& initialZones, const ZoneIndexSet& finalZones) const noexcept;

private:
    bool CalculateFocusLayout(Rect workArea, int zoneCount) noexcept;
    bool CalculateColumnsAndRowsLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateGridLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept;
    bool CalculateUniquePriorityGridLayout(Rect workArea, int zoneCount, int spacing) noexcept;
    bool CalculateCustomLayout(Rect workArea, int spacing) noexcept;
    bool CalculateGridZones(Rect workArea, FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo, int spacing);
    HWND GetNextTab(ZoneIndexSet indexSet, HWND current, bool reverse) noexcept;
    void InsertTabIntoZone(HWND window, std::optional<size_t> tabSortKeyWithinZone, const ZoneIndexSet& indexSet);
    ZoneIndexSet ZoneSelectSubregion(const ZoneIndexSet& capturedZones, POINT pt) const;
    ZoneIndexSet ZoneSelectClosestCenter(const ZoneIndexSet& capturedZones, POINT pt) const;

    // `compare` should return true if the first argument is a better choice than the second argument.
    template<class CompareF>
    ZoneIndexSet ZoneSelectPriority(const ZoneIndexSet& capturedZones, CompareF compare) const;

    ZonesMap m_zones;
    std::map<HWND, ZoneIndexSet> m_windowIndexSet;
    std::map<ZoneIndexSet, std::vector<HWND>> m_windowsByIndexSets;

    // Needed for ExtendWindowByDirectionAndPosition
    std::map<HWND, ZoneIndexSet> m_windowInitialIndexSet;
    std::map<HWND, ZoneIndex> m_windowFinalIndex;
    bool m_inExtendWindow = false;

    ZoneSetConfig m_config;
};

IFACEMETHODIMP ZoneSet::AddZone(winrt::com_ptr<IZone> zone) noexcept
{
    auto zoneId = zone->Id();
    if (m_zones.contains(zoneId))
    {
        return S_FALSE;
    }
    m_zones[zoneId] = zone;

    return S_OK;
}

IFACEMETHODIMP_(ZoneIndexSet)
ZoneSet::ZonesFromPoint(POINT pt) const noexcept
{
    ZoneIndexSet capturedZones;
    ZoneIndexSet strictlyCapturedZones;
    for (const auto& [zoneId, zone] : m_zones)
    {
        const RECT& zoneRect = zone->GetZoneRect();
        if (zoneRect.left - m_config.SensitivityRadius <= pt.x && pt.x <= zoneRect.right + m_config.SensitivityRadius &&
            zoneRect.top - m_config.SensitivityRadius <= pt.y && pt.y <= zoneRect.bottom + m_config.SensitivityRadius)
        {
            capturedZones.emplace_back(zoneId);
        }
            
        if (zoneRect.left <= pt.x && pt.x < zoneRect.right &&
            zoneRect.top <= pt.y && pt.y < zoneRect.bottom)
        {
            strictlyCapturedZones.emplace_back(zoneId);
        }
    }

    // If only one zone is captured, but it's not strictly captured
    // don't consider it as captured
    if (capturedZones.size() == 1 && strictlyCapturedZones.size() == 0)
    {
        return {};
    }

    // If captured zones do not overlap, return all of them
    // Otherwise, return one of them based on the chosen selection algorithm.
    bool overlap = false;
    for (size_t i = 0; i < capturedZones.size(); ++i)
    {
        for (size_t j = i + 1; j < capturedZones.size(); ++j)
        {
            RECT rectI;
            RECT rectJ;
            try
            {
                rectI = m_zones.at(capturedZones[i])->GetZoneRect();
                rectJ = m_zones.at(capturedZones[j])->GetZoneRect();
            }
            catch (std::out_of_range)
            {
                return {};
            }

            if (max(rectI.top, rectJ.top) + m_config.SensitivityRadius < min(rectI.bottom, rectJ.bottom) &&
                max(rectI.left, rectJ.left) + m_config.SensitivityRadius < min(rectI.right, rectJ.right))
            {
                overlap = true;
                break;
            }
        }
        if (overlap)
        {
            break;
        }
    }

    if (overlap)
    {
        try
        {
            using Algorithm = OverlappingZonesAlgorithm;

            switch (m_config.SelectionAlgorithm)
            {
            case Algorithm::Smallest:
                return ZoneSelectPriority(capturedZones, [&](auto zone1, auto zone2) { return zone1->GetZoneArea() < zone2->GetZoneArea(); });
            case Algorithm::Largest:
                return ZoneSelectPriority(capturedZones, [&](auto zone1, auto zone2) { return zone1->GetZoneArea() > zone2->GetZoneArea(); });
            case Algorithm::Positional:
                return ZoneSelectSubregion(capturedZones, pt);
            case Algorithm::ClosestCenter:
                return ZoneSelectClosestCenter(capturedZones, pt);
            }
        }
        catch (std::out_of_range)
        {
            Logger::error("Exception out_of_range was thrown in ZoneSet::ZonesFromPoint");
            return { capturedZones[0] };
        }
    }

    return capturedZones;
}

ZoneIndexSet ZoneSet::GetZoneIndexSetFromWindow(HWND window) const noexcept
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
ZoneSet::MoveWindowIntoZoneByIndex(HWND window, HWND workAreaWindow, ZoneIndex index) noexcept
{
    MoveWindowIntoZoneByIndexSet(window, workAreaWindow, { index });
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByIndexSet(HWND window, HWND workAreaWindow, const ZoneIndexSet& zoneIds, bool suppressMove) noexcept
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

    auto tabSortKeyWithinZone = FancyZonesWindowProperties::GetTabSortKeyWithinZone(window);
    DismissWindow(window);

    RECT size;
    bool sizeEmpty = true;
    auto& indexSet = m_windowIndexSet[window];

    for (ZoneIndex id : zoneIds)
    {
        if (m_zones.contains(id))
        {
            const auto& zone = m_zones.at(id);
            const RECT newSize = zone->GetZoneRect();
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

            indexSet.push_back(id);
        }
    }

    if (!sizeEmpty)
    {
        if (!suppressMove)
        {
            FancyZonesWindowUtils::SaveWindowSizeAndOrigin(window);

            auto rect = FancyZonesWindowUtils::AdjustRectForSizeWindowToRect(window, size, workAreaWindow);
            FancyZonesWindowUtils::SizeWindowToRect(window, rect);
        }

        FancyZonesWindowProperties::StampZoneIndexProperty(window, indexSet);
        InsertTabIntoZone(window, tabSortKeyWithinZone, indexSet);
    }
}

IFACEMETHODIMP_(bool)
ZoneSet::MoveWindowIntoZoneByDirectionAndIndex(HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    auto indexSet = GetZoneIndexSetFromWindow(window);
    auto numZones = m_zones.size();

    // The window was not assigned to any zone here
    if (indexSet.size() == 0)
    {
        MoveWindowIntoZoneByIndex(window, workAreaWindow, vkCode == VK_LEFT ? numZones - 1 : 0);
        return true;
    }

    ZoneIndex oldId = indexSet[0];

    // We reached the edge
    if ((vkCode == VK_LEFT && oldId == 0) || (vkCode == VK_RIGHT && oldId == numZones - 1))
    {
        if (!cycle)
        {
            MoveWindowIntoZoneByIndexSet(window, workAreaWindow, {});
            return false;
        }
        else
        {
            MoveWindowIntoZoneByIndex(window, workAreaWindow, vkCode == VK_LEFT ? numZones - 1 : 0);
            return true;
        }
    }

    // We didn't reach the edge
    if (vkCode == VK_LEFT)
    {
        MoveWindowIntoZoneByIndex(window, workAreaWindow, oldId - 1);
    }
    else
    {
        MoveWindowIntoZoneByIndex(window, workAreaWindow, oldId + 1);
    }
    return true;
}

IFACEMETHODIMP_(bool)
ZoneSet::MoveWindowIntoZoneByDirectionAndPosition(HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    std::vector<bool> usedZoneIndices(m_zones.size(), false);
    for (ZoneIndex id : GetZoneIndexSetFromWindow(window))
    {
        usedZoneIndices[id] = true;
    }

    std::vector<RECT> zoneRects;
    ZoneIndexSet freeZoneIndices;

    for (const auto& [zoneId, zone] : m_zones)
    {
        if (!usedZoneIndices[zoneId])
        {
            zoneRects.emplace_back(m_zones[zoneId]->GetZoneRect());
            freeZoneIndices.emplace_back(zoneId);
        }
    }

    RECT windowRect, workAreaRect;
    if (GetWindowRect(window, &windowRect) && GetWindowRect(workAreaWindow, &workAreaRect))
    {
        // Move to coordinates relative to windowZone
        windowRect.top -= workAreaRect.top;
        windowRect.bottom -= workAreaRect.top;
        windowRect.left -= workAreaRect.left;
        windowRect.right -= workAreaRect.left;

        auto result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (result < zoneRects.size())
        {
            MoveWindowIntoZoneByIndex(window, workAreaWindow, freeZoneIndices[result]);
            return true;
        }
        else if (cycle)
        {
            // Try again from the position off the screen in the opposite direction to vkCode
            // Consider all zones as available
            zoneRects.resize(m_zones.size());
            std::transform(m_zones.begin(), m_zones.end(), zoneRects.begin(), [](auto zone) { return zone.second->GetZoneRect(); });
            windowRect = FancyZonesUtils::PrepareRectForCycling(windowRect, workAreaRect, vkCode);
            result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);

            if (result < zoneRects.size())
            {
                MoveWindowIntoZoneByIndex(window, workAreaWindow, result);
                return true;
            }
        }
    }

    return false;
}

IFACEMETHODIMP_(bool)
ZoneSet::ExtendWindowByDirectionAndPosition(HWND window, HWND workAreaWindow, DWORD vkCode) noexcept
{
    if (m_zones.empty())
    {
        return false;
    }

    RECT windowRect, windowZoneRect;
    if (GetWindowRect(window, &windowRect) && GetWindowRect(workAreaWindow, &windowZoneRect))
    {
        auto oldZones = GetZoneIndexSetFromWindow(window);
        std::vector<bool> usedZoneIndices(m_zones.size(), false);
        std::vector<RECT> zoneRects;
        ZoneIndexSet freeZoneIndices;

        // If selectManyZones = true for the second time, use the last zone into which we moved
        // instead of the window rect and enable moving to all zones except the old one
        auto finalIndexIt = m_windowFinalIndex.find(window);
        if (finalIndexIt != m_windowFinalIndex.end())
        {
            usedZoneIndices[finalIndexIt->second] = true;
            windowRect = m_zones[finalIndexIt->second]->GetZoneRect();
        }
        else
        {
            for (ZoneIndex idx : oldZones)
            {
                usedZoneIndices[idx] = true;
            }
            // Move to coordinates relative to windowZone
            windowRect.top -= windowZoneRect.top;
            windowRect.bottom -= windowZoneRect.top;
            windowRect.left -= windowZoneRect.left;
            windowRect.right -= windowZoneRect.left;
        }

        for (size_t i = 0; i < m_zones.size(); i++)
        {
            if (!usedZoneIndices[i])
            {
                zoneRects.emplace_back(m_zones[i]->GetZoneRect());
                freeZoneIndices.emplace_back(i);
            }
        }

        auto result = FancyZonesUtils::ChooseNextZoneByPosition(vkCode, windowRect, zoneRects);
        if (result < zoneRects.size())
        {
            ZoneIndex targetZone = freeZoneIndices[result];
            ZoneIndexSet resultIndexSet;

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

            m_inExtendWindow = true;
            MoveWindowIntoZoneByIndexSet(window, workAreaWindow, resultIndexSet);
            m_inExtendWindow = false;
            return true;
        }
    }

    return false;
}

IFACEMETHODIMP_(void)
ZoneSet::MoveWindowIntoZoneByPoint(HWND window, HWND workAreaWindow, POINT ptClient) noexcept
{
    const auto& zones = ZonesFromPoint(ptClient);
    MoveWindowIntoZoneByIndexSet(window, workAreaWindow, zones);
}

void ZoneSet::DismissWindow(HWND window) noexcept
{
    auto& indexSet = m_windowIndexSet[window];
    if (!indexSet.empty())
    {
        auto& windows = m_windowsByIndexSets[indexSet];
        windows.erase(find(begin(windows), end(windows), window));
        if (windows.empty())
        {
            m_windowsByIndexSets.erase(indexSet);
        }

        indexSet.clear();
    }

    FancyZonesWindowProperties::SetTabSortKeyWithinZone(window, std::nullopt);
}

IFACEMETHODIMP_(void)
ZoneSet::CycleTabs(HWND window, bool reverse) noexcept
{
    auto indexSet = GetZoneIndexSetFromWindow(window);

    // Do nothing in case the window is not recognized
    if (indexSet.empty())
    {
        return;
    }

    for (;;)
    {
        auto next = GetNextTab(indexSet, window, reverse);

        // Determine whether the window still exists
        if (!IsWindow(next))
        {
            // Dismiss the encountered window since it was probably closed
            DismissWindow(next);
            continue;
        }

        FancyZonesWindowUtils::SwitchToWindow(next);

        break;
    }
}

HWND ZoneSet::GetNextTab(ZoneIndexSet indexSet, HWND current, bool reverse) noexcept
{
    const auto& tabs = m_windowsByIndexSets[indexSet];
    auto tabIt = std::find(tabs.begin(), tabs.end(), current);
    if (!reverse)
    {
        ++tabIt;
        return tabIt == tabs.end() ? tabs.front() : *tabIt;
    }
    else
    {
        return tabIt == tabs.begin() ? tabs.back() : *(--tabIt);
    }
}

void ZoneSet::InsertTabIntoZone(HWND window, std::optional<size_t> tabSortKeyWithinZone, const ZoneIndexSet& indexSet)
{
    if (tabSortKeyWithinZone.has_value())
    {
        // Insert the tab using the provided sort key
        auto predicate = [tabSortKeyWithinZone](HWND tab) {
            auto currentTabSortKeyWithinZone = FancyZonesWindowProperties::GetTabSortKeyWithinZone(tab);
            if (currentTabSortKeyWithinZone.has_value())
            {
                return currentTabSortKeyWithinZone.value() > tabSortKeyWithinZone;
            }
            else
            {
                return false;
            }
        };

        auto position = std::find_if(m_windowsByIndexSets[indexSet].begin(), m_windowsByIndexSets[indexSet].end(), predicate);
        m_windowsByIndexSets[indexSet].insert(position, window);
    }
    else
    {
        // Insert the tab at the end
        tabSortKeyWithinZone = 0;
        if (!m_windowsByIndexSets[indexSet].empty())
        {
            auto prevTab = m_windowsByIndexSets[indexSet].back();
            auto prevTabSortKeyWithinZone = FancyZonesWindowProperties::GetTabSortKeyWithinZone(prevTab);
            if (prevTabSortKeyWithinZone.has_value())
            {
                tabSortKeyWithinZone = prevTabSortKeyWithinZone.value() + 1;
            }
        }

        m_windowsByIndexSets[indexSet].push_back(window);
    }

    FancyZonesWindowProperties::SetTabSortKeyWithinZone(window, tabSortKeyWithinZone);
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

bool ZoneSet::IsZoneEmpty(ZoneIndex zoneIndex) const noexcept
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
    long left{ 100 };
    long top{ 100 };
    long right{ left + long(workArea.width() * 0.4) };
    long bottom{ top + long(workArea.height() * 0.4) };

    RECT focusZoneRect{ left, top, right, bottom };

    long focusRectXIncrement = (zoneCount <= 1) ? 0 : 50;
    long focusRectYIncrement = (zoneCount <= 1) ? 0 : 50;

    for (int i = 0; i < zoneCount; i++)
    {
        auto zone = MakeZone(focusZoneRect, m_zones.size());
        if (zone)
        {
            AddZone(zone);
        }
        else
        {
            // All zones within zone set should be valid in order to use its functionality.
            m_zones.clear();
            return false;
        }
        focusZoneRect.left += focusRectXIncrement;
        focusZoneRect.right += focusRectXIncrement;
        focusZoneRect.bottom += focusRectYIncrement;
        focusZoneRect.top += focusRectYIncrement;
    }

    return true;
}

bool ZoneSet::CalculateColumnsAndRowsLayout(Rect workArea, FancyZonesDataTypes::ZoneSetLayoutType type, int zoneCount, int spacing) noexcept
{
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
    for (int zoneIndex = 0; zoneIndex < zoneCount; ++zoneIndex)
    {
        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Columns)
        {
            right = left + (zoneIndex + 1) * totalWidth / zoneCount - zoneIndex * totalWidth / zoneCount;
            bottom = totalHeight + spacing;
        }
        else
        { //Rows
            right = totalWidth + spacing;
            bottom = top + (zoneIndex + 1) * totalHeight / zoneCount - zoneIndex * totalHeight / zoneCount;
        }


        auto zone = MakeZone(RECT{ left, top, right, bottom }, m_zones.size());
        if (zone)
        {
            AddZone(zone);
        }
        else
        {
            // All zones within zone set should be valid in order to use its functionality.
            m_zones.clear();
            return false;
        }

        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Columns)
        {
            left = right + spacing;
        }
        else
        { //Rows
            top = bottom + spacing;
        }
    }

    return true;
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
    for (int row = 0; row < rows; row++)
    {
        for (int col = 0; col < columns; col++)
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
    const auto zoneSetSearchResult = CustomLayouts::instance().GetLayout(m_config.Id);
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

            DPIAware::Convert(m_config.Monitor, x, y);
            DPIAware::Convert(m_config.Monitor, width, height);

            auto zone = MakeZone(RECT{ x, y, x + width, y + height }, m_zones.size());
            if (zone)
            {
                AddZone(zone);
            }
            else
            {
                // All zones within zone set should be valid in order to use its functionality.
                m_zones.clear();
                return false;
            }
        }

        return true;
    }
    else if (zoneSet.type == FancyZonesDataTypes::CustomLayoutType::Grid && std::holds_alternative<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info))
    {
        const auto& info = std::get<FancyZonesDataTypes::GridLayoutInfo>(zoneSet.info);
        return CalculateGridZones(workArea, info, spacing);
    }

    return false;
}

bool ZoneSet::CalculateGridZones(Rect workArea, FancyZonesDataTypes::GridLayoutInfo gridLayoutInfo, int spacing)
{
    long totalWidth = workArea.width();
    long totalHeight = workArea.height();
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
        rowInfo[row].Start = totalPercents * totalHeight / C_MULTIPLIER;
        totalPercents += gridLayoutInfo.rowsPercents()[row];
        rowInfo[row].End = totalPercents * totalHeight / C_MULTIPLIER;
        rowInfo[row].Extent = rowInfo[row].End - rowInfo[row].Start;
    }

    totalPercents = 0;
    for (int col = 0; col < gridLayoutInfo.columns(); col++)
    {
        columnInfo[col].Start = totalPercents * totalWidth / C_MULTIPLIER;
        totalPercents += gridLayoutInfo.columnsPercents()[col];
        columnInfo[col].End = totalPercents * totalWidth / C_MULTIPLIER;
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

                top += row == 0 ? spacing : spacing / 2;
                bottom -= maxRow == gridLayoutInfo.rows() - 1 ? spacing : spacing / 2;
                left += col == 0 ? spacing : spacing / 2;
                right -= maxCol == gridLayoutInfo.columns() - 1 ? spacing : spacing / 2;

                auto zone = MakeZone(RECT{ left, top, right, bottom }, i);
                if (zone)
                {
                    AddZone(zone);
                }
                else
                {
                    // All zones within zone set should be valid in order to use its functionality.
                    m_zones.clear();
                    return false;
                }
            }
        }
    }

    return true;
}

ZoneIndexSet ZoneSet::GetCombinedZoneRange(const ZoneIndexSet& initialZones, const ZoneIndexSet& finalZones) const noexcept
{
    ZoneIndexSet combinedZones, result;
    std::set_union(begin(initialZones), end(initialZones), begin(finalZones), end(finalZones), std::back_inserter(combinedZones));

    RECT boundingRect;
    bool boundingRectEmpty = true;

    for (ZoneIndex zoneId : combinedZones)
    {
        if (m_zones.contains(zoneId))
        {
            const RECT rect = m_zones.at(zoneId)->GetZoneRect();
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
    }

    if (!boundingRectEmpty)
    {
        for (const auto& [zoneId, zone] : m_zones)
        {
            const RECT rect = zone->GetZoneRect();
            if (boundingRect.left <= rect.left && rect.right <= boundingRect.right &&
                boundingRect.top <= rect.top && rect.bottom <= boundingRect.bottom)
            {
                result.push_back(zoneId);
            }
        }
    }

    return result;
}

ZoneIndexSet ZoneSet::ZoneSelectSubregion(const ZoneIndexSet& capturedZones, POINT pt) const
{
    auto expand = [&](RECT& rect) {
        rect.top -= m_config.SensitivityRadius / 2;
        rect.bottom += m_config.SensitivityRadius / 2;
        rect.left -= m_config.SensitivityRadius / 2;
        rect.right += m_config.SensitivityRadius / 2;
    };

    // Compute the overlapped rectangle.
    RECT overlap = m_zones.at(capturedZones[0])->GetZoneRect();
    expand(overlap);

    for (size_t i = 1; i < capturedZones.size(); ++i)
    {
        RECT current = m_zones.at(capturedZones[i])->GetZoneRect();
        expand(current);

        overlap.top = max(overlap.top, current.top);
        overlap.left = max(overlap.left, current.left);
        overlap.bottom = min(overlap.bottom, current.bottom);
        overlap.right = min(overlap.right, current.right);
    }

    // Avoid division by zero
    int width = max(overlap.right - overlap.left, 1);
    int height = max(overlap.bottom - overlap.top, 1);

    bool verticalSplit = height > width;
    ZoneIndex zoneIndex;

    if (verticalSplit)
    {
        zoneIndex = (pt.y - overlap.top) * capturedZones.size() / height;
    }
    else
    {
        zoneIndex = (pt.x - overlap.left) * capturedZones.size() / width;
    }

    zoneIndex = std::clamp(zoneIndex, ZoneIndex(0), static_cast<ZoneIndex>(capturedZones.size()) - 1);

    return { capturedZones[zoneIndex] };
}

ZoneIndexSet ZoneSet::ZoneSelectClosestCenter(const ZoneIndexSet& capturedZones, POINT pt) const
{
    auto getCenter = [](auto zone) {
        RECT rect = zone->GetZoneRect();
        return POINT{ (rect.right + rect.left) / 2, (rect.top + rect.bottom) / 2 };
    };
    auto pointDifference = [](POINT pt1, POINT pt2) {
        return (pt1.x - pt2.x) * (pt1.x - pt2.x) + (pt1.y - pt2.y) * (pt1.y - pt2.y);
    };
    auto distanceFromCenter = [&](auto zone) {
        POINT center = getCenter(zone);
        return pointDifference(center, pt);
    };
    auto closerToCenter = [&](auto zone1, auto zone2) {
        if (pointDifference(getCenter(zone1), getCenter(zone2)) > OVERLAPPING_CENTERS_SENSITIVITY)
        {
            return distanceFromCenter(zone1) < distanceFromCenter(zone2);
        }
        else
        {
            return zone1->GetZoneArea() < zone2->GetZoneArea();
        };
    };
    return ZoneSelectPriority(capturedZones, closerToCenter);
}

template<class CompareF>
ZoneIndexSet ZoneSet::ZoneSelectPriority(const ZoneIndexSet& capturedZones, CompareF compare) const
{
    size_t chosen = 0;

    for (size_t i = 1; i < capturedZones.size(); ++i)
    {
        if (compare(m_zones.at(capturedZones[i]), m_zones.at(capturedZones[chosen])))
        {
            chosen = i;
        }
    }

    return { capturedZones[chosen] };
}

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept
{
    return winrt::make_self<ZoneSet>(config);
}

