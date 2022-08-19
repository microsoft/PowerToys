#pragma once

#include "D2DState.h"
#include "ToolState.h"

#include <common/utils/serialized.h>

class OverlayUIState final
{
    template<typename StateT, typename TickFuncT>
    OverlayUIState(StateT& toolState,
                   TickFuncT tickFunc,
                   const CommonState& commonState,
                   HWND window);

    HWND _window = {};
    const CommonState& _commonState;
    D2DState _d2dState;
    std::function<void()> _tickFunc;
    std::thread _uiThread;

    template<typename ToolT, typename TickFuncT>
    static std::unique_ptr<OverlayUIState> CreateInternal(ToolT& toolState,
                                                          TickFuncT tickFunc,
                                                          const CommonState& commonState,
                                                          const wchar_t* toolWindowClassName,
                                                          void* windowParam,
                                                          HMONITOR monitor);

public:
    ~OverlayUIState();

    static std::unique_ptr<OverlayUIState> Create(BoundsToolState& toolState,
                                                  const CommonState& commonState,
                                                  HMONITOR monitor);
    static std::unique_ptr<OverlayUIState> Create(Serialized<MeasureToolState>& toolState,
                                                  const CommonState& commonState,
                                                  HMONITOR monitor);
    inline HWND overlayWindowHandle() const
    {
        return _window;
    }

    void RunUILoop();
};
