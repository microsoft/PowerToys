#pragma once

#include <FancyZonesLib/Zone.h>

class LayoutAssignedWindows
{
public:
    struct ExtendWindowModeData
    {
        std::map<HWND, ZoneIndexSet> m_windowInitialIndexSet;
        std::map<HWND, ZoneIndex> m_windowFinalIndex;
    };

public :
    LayoutAssignedWindows(){};
    ~LayoutAssignedWindows() = default;

    void Assign(HWND window, const ZoneIndexSet& zones);
    void Dismiss(HWND window);

    ZoneIndexSet GetZoneIndexSetFromWindow(HWND window) const noexcept;
    bool IsZoneEmpty(ZoneIndex zoneIndex) const noexcept;
    
    void CycleWindows(HWND window, bool reverse);

    const std::unique_ptr<ExtendWindowModeData>& ExtendWindowMode();
    void FinishExtendWindowMode();

private:
    std::map<HWND, ZoneIndexSet> m_windowIndexSet{};
    std::map<ZoneIndexSet, std::vector<HWND>> m_windowsByIndexSets{};
    std::unique_ptr<ExtendWindowModeData> m_extendMode{nullptr}; // Needed for ExtendWindowByDirectionAndPosition

    void InsertWindowIntoZone(HWND window, std::optional<size_t> tabSortKeyWithinZone, const ZoneIndexSet& indexSet);
    HWND GetNextZoneWindow(ZoneIndexSet indexSet, HWND current, bool reverse) noexcept;
};
