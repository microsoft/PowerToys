#pragma once

#include "DxgiAPI.h"
#include "D2DState.h"

#include "ToolState.h"

#include <common/display/monitors.h>
#include <common/utils/serialized.h>

#include <atomic>
#include <chrono>
#include <condition_variable>
#include <mutex>

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
    std::atomic_bool _toolbarRegionUpdatePending = false;
    std::atomic_bool _stopRequested = false;
    wil::unique_event _ownerThreadWakeEvent{ wil::EventOptions::ManualReset };
    std::condition_variable _uiLoopStartCondition;
    std::mutex _uiLoopStartMutex;
    bool _uiLoopCanStart = false;

    bool ProcessPendingOwnerSignals();
    bool ProcessPendingWindowMessages();
    bool WaitForNextFrame(bool cursorOnScreen, std::chrono::steady_clock::time_point deadline);
    void WaitForUILoopStart();

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
    void StartUILoop();

    // Coalesces toolbar-bound changes into an HWND-independent event. The owning overlay thread
    // consumes the signal and applies SetWindowRgn there, so the WinUI drag loop never performs
    // cross-thread synchronous window operations or posts to a potentially recycled HWND.
    void RequestToolbarExclusionRegionUpdate();
};
