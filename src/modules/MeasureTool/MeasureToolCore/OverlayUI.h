#pragma once

#include "DxgiAPI.h"
#include "D2DState.h"

#include "ToolState.h"

#include <common/display/monitors.h>
#include <common/utils/serialized.h>

class OverlayUIState final
{
    template<typename StateT, typename TickFuncT>
    OverlayUIState(const DxgiAPI* dxgiAPI,
                   StateT& toolState,
                   TickFuncT tickFunc,
                   const CommonState& commonState,
                   HWND window);

    Box _monitorArea;
    HWND _window = {};
    const CommonState& _commonState;
    D2DState _d2dState;
    std::function<void()> _tickFunc;
    std::thread _uiThread;

    template<typename ToolT, typename TickFuncT>
    static std::unique_ptr<OverlayUIState> CreateInternal(const DxgiAPI* dxgi,
                                                          ToolT& toolState,
                                                          TickFuncT tickFunc,
                                                          CommonState& commonState,
                                                          const wchar_t* toolWindowClassName,
                                                          void* windowParam,
                                                          const MonitorInfo& monitor,
                                                          const bool excludeFromCapture);

public:
    OverlayUIState(OverlayUIState&&) noexcept = default;
    ~OverlayUIState();

    static std::unique_ptr<OverlayUIState> Create(const DxgiAPI* dxgi,
                                                  BoundsToolState& toolState,
                                                  CommonState& commonState,
                                                  const MonitorInfo& monitor);
    static std::unique_ptr<OverlayUIState> Create(const DxgiAPI* dxgi,
                                                  Serialized<MeasureToolState>& toolState,
                                                  CommonState& commonState,
                                                  const MonitorInfo& monitor);
    inline HWND overlayWindowHandle() const
    {
        return _window;
    }

    void RunUILoop();
};
