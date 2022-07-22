#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/ZoneSet.h>
#include <FancyZonesLib/util.h>

class ZonesOverlay;

class WorkArea
{
public:
    WorkArea(HINSTANCE hinstance);
    ~WorkArea();

public:
    bool Init(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesDataTypes::WorkAreaId& parentUniqueId);
    inline bool InitWorkAreaRect(HMONITOR monitor)
    {
        m_monitor = monitor;

#if defined(UNIT_TESTS)
        m_workAreaRect = FancyZonesUtils::Rect({ 0, 0, 1920, 1080 });
        return true;
#endif

        if (monitor)
        {
            MONITORINFO mi{};
            mi.cbSize = sizeof(mi);
            if (!GetMonitorInfoW(monitor, &mi))
            {
                return false;
            }

            m_workAreaRect = FancyZonesUtils::Rect(mi.rcWork);
        }
        else
        {
            m_workAreaRect = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFO::rcWork>();
        }

        return true;
    }

    FancyZonesDataTypes::WorkAreaId UniqueId() const noexcept { return { m_uniqueId }; }
    IZoneSet* ZoneSet() const noexcept { return m_zoneSet.get(); }
    
    ZoneIndexSet GetWindowZoneIndexes(HWND window) const noexcept;
    
    HRESULT MoveSizeEnter(HWND window) noexcept;
    HRESULT MoveSizeUpdate(POINT const& ptScreen, bool dragEnabled, bool selectManyZones) noexcept;
    HRESULT MoveSizeEnd(HWND window, POINT const& ptScreen) noexcept;
    void MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept;
    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
    void SaveWindowProcessToZoneIndex(HWND window) noexcept;
    
    void UpdateActiveZoneSet() noexcept;

    void ShowZonesOverlay() noexcept;
    void HideZonesOverlay() noexcept;
    void FlashZones() noexcept;
    void ClearSelectedZones() noexcept;
    
    void CycleTabs(HWND window, bool reverse) noexcept;
    
    void LogInitializationError();

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    void InitializeZoneSets(const FancyZonesDataTypes::WorkAreaId& parentUniqueId) noexcept;
    void CalculateZoneSet(OverlappingZonesAlgorithm overlappingAlgorithm) noexcept;
    void UpdateActiveZoneSet(_In_opt_ IZoneSet* zoneSet) noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    ZoneIndexSet ZonesFromPoint(POINT pt) noexcept;
    void SetAsTopmostWindow() noexcept;

    HMONITOR m_monitor{};
    FancyZonesUtils::Rect m_workAreaRect{};

    FancyZonesDataTypes::WorkAreaId m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    HWND m_windowMoveSize{};
    winrt::com_ptr<IZoneSet> m_zoneSet;
    ZoneIndexSet m_initialHighlightZone;
    ZoneIndexSet m_highlightZone;
    WPARAM m_keyLast{};
    size_t m_keyCycle{};
    std::unique_ptr<ZonesOverlay> m_zonesOverlay;
};

inline std::shared_ptr<WorkArea> MakeWorkArea(HINSTANCE hinstance, HMONITOR monitor, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesDataTypes::WorkAreaId& parentUniqueId) noexcept
{
    auto self = std::make_shared<WorkArea>(hinstance);
    if (!self->InitWorkAreaRect(monitor))
    {
        self->LogInitializationError();
        return nullptr;
    }
    
    if (!self->Init(hinstance, uniqueId, parentUniqueId))
    {
        return nullptr;
    }

    return self;
}
