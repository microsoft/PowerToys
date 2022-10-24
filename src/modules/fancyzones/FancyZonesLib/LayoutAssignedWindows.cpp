#include "pch.h"
#include "LayoutAssignedWindows.h"

#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WindowUtils.h>

void LayoutAssignedWindows::Assign(HWND window, const ZoneIndexSet& zones)
{
    Dismiss(window);

    for (const auto& index : zones)
    {
        m_windowIndexSet[window].push_back(index);
    }

    if (FancyZonesSettings::settings().disableRoundCorners)
    {
        FancyZonesWindowUtils::DisableRoundCorners(window);
    }

    auto tabSortKeyWithinZone = FancyZonesWindowProperties::GetTabSortKeyWithinZone(window);
    InsertWindowIntoZone(window, tabSortKeyWithinZone, zones);
}

void LayoutAssignedWindows::Dismiss(HWND window)
{
    if (m_windowIndexSet.contains(window))
    {
        const auto& indexSet = m_windowIndexSet.at(window);
        auto& windows = m_windowsByIndexSets[indexSet];
        windows.erase(find(begin(windows), end(windows), window));
        if (windows.empty())
        {
            m_windowsByIndexSets.erase(m_windowIndexSet[window]);
        }
        
        m_windowIndexSet.erase(window);
    }
    
    FancyZonesWindowProperties::SetTabSortKeyWithinZone(window, std::nullopt);
}

ZoneIndexSet LayoutAssignedWindows::GetZoneIndexSetFromWindow(HWND window) const noexcept
{
    auto it = m_windowIndexSet.find(window);
    if (it != m_windowIndexSet.end())
    {
        return it->second;
    }
    
    return {};
}

bool LayoutAssignedWindows::IsZoneEmpty(ZoneIndex zoneIndex) const noexcept
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

void LayoutAssignedWindows::CycleWindows(HWND window, bool reverse)
{
    auto indexSet = GetZoneIndexSetFromWindow(window);

    // Do nothing in case the window is not recognized
    if (indexSet.empty())
    {
        return;
    }

    for (;;)
    {
        auto next = GetNextZoneWindow(indexSet, window, reverse);

        // Determine whether the window still exists
        if (!IsWindow(next))
        {
            // Dismiss the encountered window since it was probably closed
            Dismiss(next);
            continue;
        }

        FancyZonesWindowUtils::SwitchToWindow(next);
        break;
    }
}

const std::unique_ptr<LayoutAssignedWindows::ExtendWindowModeData>& LayoutAssignedWindows::ExtendWindowMode()
{
    if (!m_extendMode)
    {
        m_extendMode = std::make_unique<ExtendWindowModeData>();
    }

    return m_extendMode;
}

void LayoutAssignedWindows::FinishExtendWindowMode()
{
    m_extendMode = nullptr;
}

void LayoutAssignedWindows::InsertWindowIntoZone(HWND window, std::optional<size_t> tabSortKeyWithinZone, const ZoneIndexSet& indexSet)
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

HWND LayoutAssignedWindows::GetNextZoneWindow(ZoneIndexSet indexSet, HWND current, bool reverse) noexcept
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
