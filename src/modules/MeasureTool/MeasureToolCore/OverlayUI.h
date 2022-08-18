#pragma once

#include "D2DState.h"
#include "ToolState.h"

#include <common/utils/serialized.h>

class OverlayUIState final
{
    OverlayUIState(BoundsToolState& toolState,
                   CommonState& commonState,
                   HWND window);

    OverlayUIState(Serialized<MeasureToolState>& toolState,
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
    ~OverlayUIState();

    static std::unique_ptr<OverlayUIState> Create(BoundsToolState& toolState,
                                                  CommonState& commonState);
    static std::unique_ptr<OverlayUIState> Create(Serialized<MeasureToolState>& toolState,
                                                  CommonState& commonState);
    inline HWND overlayWindowHandle() const
    {
        return _window;
    }
    
    void RunUILoop();
};
