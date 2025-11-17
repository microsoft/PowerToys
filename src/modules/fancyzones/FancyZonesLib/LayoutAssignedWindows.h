#pragma once

#include <FancyZonesLib/Zone.h>

class LayoutAssignedWindows
{
public :
    LayoutAssignedWindows() = default;
    ~LayoutAssignedWindows() = default;

    void Assign(HWND window, const ZoneIndexSet& zones);
    void Dismiss(HWND window);

    std::map<HWND, ZoneIndexSet> SnappedWindows() const noexcept;
    ZoneIndexSet GetZoneIndexSetFromWindow(HWND window) const noexcept;
    bool IsZoneEmpty(ZoneIndex zoneIndex) const noexcept;
    
    void CycleWindows(HWND window, bool reverse);

private:
    std::map<HWND, ZoneIndexSet> m_windowIndexSet{};
    std::map<ZoneIndexSet, std::vector<HWND>> m_windowsByIndexSets{};

    void InsertWindowIntoZone(HWND window, std::optional<size_t> tabSortKeyWithinZone, const ZoneIndexSet& indexSet);
    HWND GetNextZoneWindow(ZoneIndexSet indexSet, HWND current, bool reverse) noexcept;
};
