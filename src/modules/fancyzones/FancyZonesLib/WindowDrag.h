#pragma once

#include <FancyZonesLib/HighlightedZones.h>

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
        // True if from the styles the window looks like a standard window
        bool isStandardWindow = false;
        // True if the window is a top-level window that does not have a visible owner
        bool hasNoVisibleOwner = false;
        // Properties to restore after dragging
        long exstyle = 0;
        COLORREF crKey = RGB(0, 0, 0);
        DWORD dwFlags = 0;
        BYTE alpha = 0;
        bool transparencySet{false};
    };

    const HWND m_window;
    WindowProperties m_windowProperties; // MoveSizeWindowInfo of the window at the moment when dragging started
    
    const std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>>& m_activeWorkAreas; // all WorkAreas on current virtual desktop, mapped with monitors
    WorkArea* m_currentWorkArea; // "Active" WorkArea, where the move/size is happening. Will update as drag moves between monitors.
    
    bool m_snappingMode{ false };

    HighlightedZones m_highlightedZones;
};
