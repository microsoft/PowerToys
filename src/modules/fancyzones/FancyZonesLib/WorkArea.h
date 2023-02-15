#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/Layout.h>
#include <FancyZonesLib/LayoutAssignedWindows.h>

class ZonesOverlay;

class WorkArea
{
    WorkArea(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesUtils::Rect& workAreaRect);

public:
    ~WorkArea();

    static std::unique_ptr<WorkArea> Create(HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& uniqueId, const FancyZonesDataTypes::WorkAreaId& parentUniqueId, const FancyZonesUtils::Rect& workAreaRect)
    {
        auto self = std::unique_ptr<WorkArea>(new WorkArea(hinstance, uniqueId, workAreaRect));
        if (!self->Init(hinstance, parentUniqueId))
        {
            return nullptr;
        }

        return self;
    }

    inline bool Init([[maybe_unused]] HINSTANCE hinstance, const FancyZonesDataTypes::WorkAreaId& parentUniqueId)
    {
#ifndef UNIT_TESTS
        if (!InitWindow(hinstance))
        {
            return false;
        }
#endif
        InitLayout(parentUniqueId);
        InitSnappedWindows();

        return true;
    }
    
    FancyZonesDataTypes::WorkAreaId UniqueId() const noexcept { return { m_uniqueId }; }
    const std::unique_ptr<Layout>& GetLayout() const noexcept { return m_layout; }
    const std::unique_ptr<LayoutAssignedWindows>& GetLayoutWindows() const noexcept { return m_layoutWindows; }
    const HWND GetWorkAreaWindow() const noexcept { return m_window; }
    
    ZoneIndexSet GetWindowZoneIndexes(HWND window) const;

    void MoveWindowIntoZoneByIndex(HWND window, ZoneIndex index);
    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, bool updatePosition = true);
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle);
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle);
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode);

    void SnapWindow(HWND window, const ZoneIndexSet& zones, bool extend = false);
    void UnsnapWindow(HWND window);

    void UpdateActiveZoneSet();
    void UpdateWindowPositions();

    void ShowZonesOverlay(const ZoneIndexSet& highlight, HWND draggedWindow = nullptr);
    void HideZonesOverlay();
    void FlashZones();
    
    void CycleWindows(HWND window, bool reverse);

protected:
    static LRESULT CALLBACK s_WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept;

private:
    bool InitWindow(HINSTANCE hinstance);
    void InitLayout(const FancyZonesDataTypes::WorkAreaId& parentUniqueId);
    void InitSnappedWindows();

    void CalculateZoneSet();
    void SetWorkAreaWindowAsTopmost(HWND draggedWindow) noexcept;

    LRESULT WndProc(UINT message, WPARAM wparam, LPARAM lparam) noexcept;
    
    const FancyZonesUtils::Rect m_workAreaRect{};
    const FancyZonesDataTypes::WorkAreaId m_uniqueId;
    HWND m_window{}; // Hidden tool window used to represent current monitor desktop work area.
    std::unique_ptr<Layout> m_layout;
    std::unique_ptr<LayoutAssignedWindows> m_layoutWindows;
    std::unique_ptr<ZonesOverlay> m_zonesOverlay;
};
