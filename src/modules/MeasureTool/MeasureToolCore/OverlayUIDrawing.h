#pragma once

#include "D2DState.h"
#include "ToolState.h"

class OverlayUIState final
{
    OverlayUIState(BoundsToolState& toolState,
                   CommonState& commonState,
                   HWND window);

    OverlayUIState(MeasureToolState& toolState,
                   CommonState& commonState,
                   HWND window);

    HWND _window = {};
    D2DState _d2dState;
    std::function<void()> _tickFunc;

    template<typename ToolT>
    static std::unique_ptr<OverlayUIState> CreateInternal(ToolT& toolState,
                                                          CommonState& commonState,
                                                          const wchar_t* toolWindowClassName);

public:
    inline HWND overlayWindowHandle() const
    {
        return _window;
    }

    static std::unique_ptr<OverlayUIState> Create(BoundsToolState& toolState,
                                                  CommonState& commonState);

    static std::unique_ptr<OverlayUIState> Create(MeasureToolState& toolState,
                                                  CommonState& commonState);

    void RunUILoop();

    ~OverlayUIState();
};
