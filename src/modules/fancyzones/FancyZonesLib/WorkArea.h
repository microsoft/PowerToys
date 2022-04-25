#pragma once
#include "FancyZones.h"
#include "FancyZonesLib/ZoneSet.h"
#include "FancyZonesLib/FancyZonesDataTypes.h"

class ZonesOverlay;

class WorkArea
{
public:
    WorkArea(HINSTANCE hinstance);
    ~WorkArea();

public:
    bool Init(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId);

    HRESULT MoveSizeEnter(HWND window) noexcept;
    HRESULT MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept;
    HRESULT MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    void MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept;
    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool suppressMove = false) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
    FancyZonesDataTypes::DeviceIdData UniqueId() const noexcept { return { m_uniqueId }; }
    void SaveWindowProcessToZoneIndex(HWND window) noexcept;
    IZoneSet* ZoneSet() const noexcept { return m_zoneSet.get(); }
    ZoneIndexSet GetWindowZoneIndexes(HWND window) const noexcept;
    void ShowZonesOverlay() noexcept;
    void HideZonesOverlay() noexcept;
    void UpdateActiveZoneSet() noexcept;
    void CycleTabs(HWND window, bool reverse) noexcept;
    void ClearSelectedZones() noexcept;
    void FlashZones() noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void InitializeZoneSets(const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept;
    void CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    ZoneIndexSet ZonesFromPoint(POINT pt) noexcept;
    void SetAsTopmostWindow() noexcept;

    HMONITOR m_monitor{};
    FancyZonesDataTypes::DeviceIdData m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    winrt::com_ptr<IZoneSet> m_zoneSet;
    ZoneIndexSet m_initialHighlightZone;
    ZoneIndexSet m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    std::unique_ptr<ZonesOverlay> m_zonesOverlay;
};

std::shared_ptr<WorkArea> MakeWorkArea(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::DeviceIdData& uniqueId, const FancyZonesDataTypes::DeviceIdData& parentUniqueId) noexcept;
