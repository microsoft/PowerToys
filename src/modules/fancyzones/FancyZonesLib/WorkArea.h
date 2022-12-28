#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/LayoutAssignedWindows.h>
#include <FancyZonesLib/util.h>

class ZonesOverlay;

class WorkArea
{
public:
    WorkArea(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesUtils::Rect& workAreaRect);
    ~WorkArea();

public:
    inline bool Init([[maybe_unused]] HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& parentUniqueId)
    {
#ifndef UNIT_TESTS
        if (!InitWindow(hinstance))
        {
            return false;
        }
#endif
        InitLayout(parentUniqueId);
        return true;
    }

    FancyZonesDataTypes::WorkAreaId UniqueId() const noexcept { return { m_uniqueId }; }
    const std::unique_ptr<Layout>& GetLayout() const noexcept { return m_layout; }
    const std::unique_ptr<LayoutAssignedWindows>& GetLayoutWindows() const noexcept { return m_layoutWindows; }
    const HWND GetWorkAreaWindow() const noexcept { return m_window; }
    
    ZoneIndexSet GetWindowZoneIndexes(HWND window) const noexcept;

    void MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index) noexcept;
    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool updatePosition = true) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode) noexcept;
    void SaveWindowProcessToZoneIndex(HWND window) noexcept;
    bool UnsnapWindow(HWND window) noexcept;

    void UpdateActiveZoneSet() noexcept;

    void ShowZonesOverlay(const ZoneIndexSet& highlight, HWND draggedWindow = nullptr);
    void HideZonesOverlay();
    void FlashZones();
    
    void CycleWindows(HWND window, bool reverse) noexcept;

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    bool InitWindow(HINSTANCE hinstance) noexcept;
    void InitLayout(const FancyZonesDataTypes::WorkAreaId& parentUniqueId) noexcept;
    void CalculateZoneSet() noexcept;
    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    void SetWorkAreaWindowAsTopmost(HWND draggedWindow) noexcept;

    const FancyZonesUtils::Rect m_workAreaRect{};
    const FancyZonesDataTypes::WorkAreaId m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    std::unique_ptr<Layout> m_layout;
    std::unique_ptr<LayoutAssignedWindows> m_layoutWindows;
    std::unique_ptr<ZonesOverlay> m_zonesOverlay;
};

inline std::shared_ptr<WorkArea> MakeWorkArea(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesDataTypes::WorkAreaId& parentUniqueId, const FancyZonesUtils::Rect& workAreaRect)
{
    auto self = std::make_shared<WorkArea>(hinstance, uniqueId, workAreaRect);
    if (!self->Init(hinstance, parentUniqueId))
    {
        return nullptr;
    }

    return self;
}
