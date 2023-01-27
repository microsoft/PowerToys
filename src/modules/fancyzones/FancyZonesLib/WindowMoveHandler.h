#pragma once

#include "FancyZonesWindowProperties.h"
#include "KeyState.h"
#include "SecondaryMouseButtonsHook.h"

#include <functional>

interface IFancyZonesSettings;
class WorkArea;

class WindowMoveHandler
{
public:
    WindowMoveHandler();

    void MoveSizeStart(HWND window, HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap, bool dragEnabled) noexcept;
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap, bool dragEnabled, bool multipleZones) noexcept;
    void MoveSizeEnd(HWND window, const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& workAreaMap) noexcept;

    void MoveWindowIntoZoneByIndexSet(HWND window, const ZoneIndexSet& indexSet, std::shared_ptr<WorkArea> workArea) noexcept;
    bool MoveWindowIntoZoneByDirectionAndIndex(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept;
    bool MoveWindowIntoZoneByDirectionAndPosition(HWND window, DWORD vkCode, bool cycle, std::shared_ptr<WorkArea> workArea) noexcept;
    bool ExtendWindowByDirectionAndPosition(HWND window, DWORD vkCode, std::shared_ptr<WorkArea> workArea) noexcept;

    void AssignWindowsToZones(const std::unordered_map<HMONITOR, std::shared_ptr<WorkArea>>& activeWorkAreas, bool updatePositions) noexcept;
    
private:
    struct WindowTransparencyProperties
    {
        HWND draggedWindow = nullptr;
        long draggedWindowExstyle = 0;
        COLORREF draggedWindowCrKey = RGB(0, 0, 0);
        DWORD draggedWindowDwFlags = 0;
        BYTE draggedWindowInitialAlpha = 0;
    };

    // MoveSize related window properties
    struct MoveSizeWindowInfo
    {
        // True if from the styles the window looks like a standard window
        bool isStandardWindow = false;
        // True if the window is a top-level window that does not have a visible owner
        bool hasNoVisibleOwner = false;
    };

    void SetWindowTransparency(HWND window) noexcept;
    void ResetWindowTransparency() noexcept;

    bool m_inDragging{}; // Whether or not a move/size operation is currently active
    HWND m_draggedWindow{}; // The window that is being moved/sized
    MoveSizeWindowInfo m_draggedWindowInfo; // MoveSizeWindowInfo of the window at the moment when dragging started
    std::shared_ptr<WorkArea> m_draggedWindowWorkArea; // "Active" WorkArea, where the move/size is happening. Will update as drag moves between monitors.

    WindowTransparencyProperties m_windowTransparencyProperties;

};
