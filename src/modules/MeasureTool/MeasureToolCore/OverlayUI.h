#pragma once

#include "D2DState.h"
#include "latch.h"
#include "ToolState.h"

#include <common/display/monitors.h>
#include <common/utils/serialized.h>

class OverlayUIState final
{
    template<typename StateT, typename TickFuncT>
    OverlayUIState(StateT& toolState,
                   TickFuncT tickFunc,
                   const CommonState& commonState,
                   HWND window);

    Box _monitorArea;
    HWND _window = {};
    const CommonState& _commonState;
    D2DState _d2dState;
    std::function<void()> _tickFunc;
    std::thread _uiThread;
    bool _cursorOnScreen = true;
    bool _clearOnCursorLeavingScreen = false;

    template<typename ToolT, typename TickFuncT>
    static std::unique_ptr<OverlayUIState> CreateInternal(ToolT& toolState,
                                                          TickFuncT tickFunc,
                                                          CommonState& commonState,
                                                          Latch& creationLatch,
                                                          const wchar_t* toolWindowClassName,
                                                          void* windowParam,
                                                          const MonitorInfo& monitor,
                                                          const bool clearOnCursorLeavingScreen);

public:
    OverlayUIState(OverlayUIState&&) noexcept = default;
    ~OverlayUIState();

    static std::unique_ptr<OverlayUIState> Create(BoundsToolState& toolState,
                                                  Latch& creationLatch,
                                                  CommonState& commonState,
                                                  const MonitorInfo& monitor);
    static std::unique_ptr<OverlayUIState> Create(Serialized<MeasureToolState>& toolState,
                                                  Latch& creationLatch,
                                                  CommonState& commonState,
                                                  const MonitorInfo& monitor);
    inline HWND overlayWindowHandle() const
    {
        return _window;
    }

    void RunUILoop();
};
