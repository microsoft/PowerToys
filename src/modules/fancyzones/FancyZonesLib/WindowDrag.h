#pragma once

#include <FancyZonesLib/HighlightedZones.h>
#include <common/utils/window.h>

class WorkArea;

class WindowDrag
{
    WindowDrag(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);

public:
    static std::unique_ptr<WindowDrag> Create(HWND window, const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& activeWorkAreas);
    ~WindowDrag();

    bool MoveSizeStart(HMONITOR monitor, bool isSnapping);
    void MoveSizeUpdate(HMONITOR monitor, POINT const& ptScreen, bool isSnapping, bool isSelectManyZonesState);
    void MoveSizeEnd();

private:
    void SwitchSnappingMode(bool isSnapping);

    void SetWindowTransparency();
    void ResetWindowTransparency();

    struct WindowProperties
    {
        // True if the window is a top-level window that does not have a visible owner
        bool hasNoVisibleOwner = false;
        // True if the window is a standard window
        bool isStandardWindow = false;
        // Transparency properties for restoration
        WindowTransparencyProperties transparency;
    };

    const HWND m_window;
    WindowProperties m_windowProperties; // MoveSizeWindowInfo of the window at the moment when dragging started
    
    const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& m_activeWorkAreas; // all WorkAreas on current virtual desktop, mapped with monitors
    WorkArea* m_currentWorkArea; // "Active" WorkArea, where the move/size is happening. Will update as drag moves between monitors.
    
    bool m_snappingMode{ false };

    HighlightedZones m_highlightedZones;
};
