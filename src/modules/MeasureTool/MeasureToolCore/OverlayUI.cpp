#include "pch.h"

#include "BoundsToolOverlayUI.h"
#include "constants.h"
#include "MeasureToolOverlayUI.h"
#include "MeasurementTooltipStyle.h"
#include "OverlayUI.h"

#include <common/Display/dpi_aware.h>
#include <common/Display/monitors.h>
#include <common/logger/logger.h>
#include <common/Themes/windows_colors.h>
#include <common/utils/window.h>

namespace NonLocalizable
{
    const wchar_t MeasureToolOverlayWindowName[] = L"PowerToys.MeasureToolOverlayWindow";
    const wchar_t BoundsToolOverlayWindowName[] = L"PowerToys.BoundsToolOverlayWindow";
}

void CreateOverlayWindowClasses()
{
    WNDCLASSEXW wcex{ .cbSize = sizeof(WNDCLASSEX), .hInstance = GetModuleHandleW(nullptr) };

    wcex.lpfnWndProc = MeasureToolWndProc;
    wcex.lpszClassName = NonLocalizable::MeasureToolOverlayWindowName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_CROSS);
    RegisterClassExW(&wcex);

    wcex.lpfnWndProc = BoundsToolWndProc;
    wcex.lpszClassName = NonLocalizable::BoundsToolOverlayWindowName;
    RegisterClassExW(&wcex);
}

// Punches (or removes) a hole matching the toolbar's current bounding box out of `window`'s region,
// so the toolbar - a separate, always-on-top WinUI window that can now be on ANY monitor and can be
// dragged live by the user - stays visible and clickable instead of this full-monitor overlay
// painting/hit-testing over it. Applied per-overlay-window (one per monitor), so it naturally only
// has an effect on whichever monitor(s) the toolbar currently intersects; on every other monitor the
// intersection is empty and the region is simply reset to the full window rect.
//
// Safe to call repeatedly (idempotent) - both once at overlay creation and again on every toolbar
// move - since SetWindowRgn always replaces the previous region wholesale.
void ApplyToolbarExclusionRegion(HWND window, const CommonState& commonState)
{
    RECT windowRect{};
    if (!GetWindowRect(window, &windowRect))
    {
        return;
    }

    const int windowWidth = windowRect.right - windowRect.left;
    const int windowHeight = windowRect.bottom - windowRect.top;
    wil::unique_hrgn windowRegion{ CreateRectRgn(0, 0, windowWidth, windowHeight) };
    if (!windowRegion)
    {
        return;
    }

    const Box toolbarBox = commonState.GetToolbarBoundingBox();
    const RECT toolbarRect{ toolbarBox.left(), toolbarBox.top(), toolbarBox.right(), toolbarBox.bottom() };

    RECT intersection{};
    if (IntersectRect(&intersection, &windowRect, &toolbarRect))
    {
        if (wil::unique_hrgn toolbarRegion{ CreateRectRgn(intersection.left - windowRect.left,
                                                         intersection.top - windowRect.top,
                                                         intersection.right - windowRect.left,
                                                         intersection.bottom - windowRect.top) })
        {
            CombineRgn(windowRegion.get(), windowRegion.get(), toolbarRegion.get(), RGN_DIFF);
        }
    }

    // SetWindowRgn takes ownership of the HRGN only if it succeeds; on failure the caller (us) must
    // still own/free it, so keep it in the wil handle until we know the outcome.
    if (SetWindowRgn(window, windowRegion.get(), true) != 0)
    {
        windowRegion.release();
    }
}

HWND CreateOverlayUIWindow(const CommonState& commonState,
                           const MonitorInfo& monitor,
                           const bool excludeFromCapture,
                           const wchar_t* windowClass,
                           void* extraParam)
{
    static std::once_flag windowClassesCreatedFlag;
    std::call_once(windowClassesCreatedFlag, CreateOverlayWindowClasses);

    const auto screenArea = monitor.GetScreenSize(true);
    DWORD windowStyle = WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW;
#if !defined(DEBUG_OVERLAY)
    windowStyle |= WS_EX_TOPMOST;
#endif
    HWND window{
        CreateWindowExW(windowStyle,
                        windowClass,
                        L"PowerToys.MeasureToolOverlay",
                        WS_POPUP | CS_HREDRAW | CS_VREDRAW,
                        screenArea.left(),
                        screenArea.top(),
                        screenArea.width(),
                        screenArea.height(),
                        HWND_DESKTOP,
                        nullptr,
                        GetModuleHandleW(nullptr),
                        extraParam)
    };
    winrt::check_bool(window);

    // Exclude overlay window from displaying in WIN+TAB preview, since WS_EX_TOOLWINDOW windows are displayed simultaneously on all virtual desktops.
    // We can't remove WS_EX_TOOLWINDOW/WS_EX_NOACTIVATE flag, since we want to exclude the window from taskbar
    BOOL val = TRUE;
    DwmSetWindowAttribute(window, DWMWA_EXCLUDED_FROM_PEEK, &val, sizeof(val));

    // We want to receive input events as soon as possible to prevent issues with touch input
    RegisterTouchWindow(window, TWF_WANTPALM);

    ShowWindow(window, SW_SHOWNORMAL);
    UpdateWindow(window);
    if (excludeFromCapture)
    {
        SetWindowDisplayAffinity(window, WDA_EXCLUDEFROMCAPTURE);
    }
#if !defined(DEBUG_OVERLAY)
    SetWindowPos(window, HWND_TOPMOST, {}, {}, {}, {}, SWP_NOMOVE | SWP_NOSIZE);
#else
    (void)window;
#endif

    const int pos = -GetSystemMetrics(SM_CXVIRTUALSCREEN) - 8;
    if (wil::unique_hrgn hrgn{ CreateRectRgn(pos, 0, (pos + 1), 1) })
    {
        DWM_BLURBEHIND bh = { DWM_BB_ENABLE | DWM_BB_BLURREGION, TRUE, hrgn.get(), FALSE };
        DwmEnableBlurBehindWindow(window, &bh);
    }

    // Exclude the toolbar from this overlay's region up-front so it's clickable/visible from the
    // very first frame. Not gated on monitor.IsPrimary() any more - the toolbar can be summoned on
    // (and dragged to) any monitor, so every overlay window checks its own intersection.
    ApplyToolbarExclusionRegion(window, commonState);

    return window;
}

std::vector<D2D1::ColorF> AppendCommonOverlayUIColors(const D2D1::ColorF& lineColor)
{
    const auto palette = MeasurementTooltipStyle::PaletteForTheme(WindowsColors::is_dark_mode());
    const auto toD2DColor = [](const MeasurementTooltipStyle::Color& color) {
        return D2D1::ColorF(color.red, color.green, color.blue, color.alpha);
    };
    return {
        lineColor,
        toD2DColor(palette.foreground),
        toD2DColor(palette.background),
        toD2DColor(palette.border),
    };
}

void OverlayUIState::RunUILoop()
{
    WaitForUILoopStart();

    bool cursorOnScreen = false;

    while (IsWindow(_window) && !_commonState.closeOnOtherMonitors && !_stopRequested.load(std::memory_order_acquire))
    {
        if (!ProcessPendingOwnerSignals() || !ProcessPendingWindowMessages() || !IsWindow(_window))
        {
            break;
        }

        const auto frameStarted = std::chrono::steady_clock::now();
        const auto cursor = _commonState.cursorPosSystemSpace;
        const bool cursorOverToolbar = _commonState.GetToolbarBoundingBox().inside(cursor);
        auto& dxgi = _d2dState.dxgiWindowState;
        if (_monitorArea.inside(cursor) != cursorOnScreen)
        {
            cursorOnScreen = !cursorOnScreen;
            if (!cursorOnScreen)
            {
                PostMessageW(_window, WM_CURSOR_LEFT_MONITOR, {}, {});
            }
        }

        dxgi.rt->BeginDraw();
        dxgi.rt->Clear();

        if (!cursorOverToolbar)
            _tickFunc();

        dxgi.rt->EndDraw();
        dxgi.swapChain->Present(0, 0);

        auto nextFrame = frameStarted + std::chrono::milliseconds{ 200 };
        if (cursorOnScreen)
        {
            nextFrame = frameStarted + consts::TARGET_FRAME_DURATION;
        }

        if (!WaitForNextFrame(cursorOnScreen, nextFrame))
        {
            break;
        }
    }

    if (IsWindow(_window))
    {
        DestroyWindow(_window);
    }
}

bool OverlayUIState::ProcessPendingOwnerSignals()
{
    // Reset before consuming the atomics. A concurrent producer either has its pending flag observed
    // below or sets the event again after this reset, so no final toolbar position can lose its wake.
    _ownerThreadWakeEvent.ResetEvent();

    if (_stopRequested.load(std::memory_order_acquire))
    {
        return false;
    }

    if (_toolbarRegionUpdatePending.exchange(false, std::memory_order_acq_rel))
    {
        ApplyToolbarExclusionRegion(_window, _commonState);
    }

    return !_stopRequested.load(std::memory_order_acquire);
}

bool OverlayUIState::ProcessPendingWindowMessages()
{
    MSG message{};
    while (PeekMessageW(&message, nullptr, 0, 0, PM_REMOVE))
    {
        if (message.message == WM_QUIT)
        {
            return false;
        }

        TranslateMessage(&message);
        DispatchMessageW(&message);
    }

    return true;
}

bool OverlayUIState::WaitForNextFrame(bool cursorOnScreen, std::chrono::steady_clock::time_point deadline)
{
    while (IsWindow(_window) &&
           !_commonState.closeOnOtherMonitors &&
           !_stopRequested.load(std::memory_order_acquire))
    {
        const auto now = std::chrono::steady_clock::now();
        if (now >= deadline)
        {
            return true;
        }

        const auto wait = std::chrono::ceil<std::chrono::milliseconds>(deadline - now);
        const HANDLE ownerWakeEvent = _ownerThreadWakeEvent.get();
        MsgWaitForMultipleObjectsEx(
            1,
            &ownerWakeEvent,
            static_cast<DWORD>(wait.count()),
            QS_ALLINPUT,
            MWMO_INPUTAVAILABLE);

        if (!ProcessPendingOwnerSignals() || !ProcessPendingWindowMessages() || !IsWindow(_window))
        {
            return false;
        }

        // A toolbar-region or close message should be handled promptly without forcing another
        // render. Only a monitor transition changes which frame cadence should apply.
        if (_monitorArea.inside(_commonState.cursorPosSystemSpace) != cursorOnScreen)
        {
            return true;
        }
    }

    return false;
}

void OverlayUIState::RequestToolbarExclusionRegionUpdate()
{
    if (_stopRequested.load(std::memory_order_acquire))
    {
        return;
    }

    if (_toolbarRegionUpdatePending.exchange(true, std::memory_order_acq_rel))
    {
        return;
    }

    _ownerThreadWakeEvent.SetEvent();
}

void OverlayUIState::WaitForUILoopStart()
{
    std::unique_lock lock{ _uiLoopStartMutex };
    _uiLoopStartCondition.wait(lock, [this] {
        return _uiLoopCanStart;
    });
}

void OverlayUIState::StartUILoop()
{
    {
        std::scoped_lock lock{ _uiLoopStartMutex };
        _uiLoopCanStart = true;
    }
    _uiLoopStartCondition.notify_all();
}

template<typename StateT, typename TickFuncT>
OverlayUIState::OverlayUIState(const DxgiAPI* dxgiAPI,
                               StateT& toolState,
                               TickFuncT tickFunc,
                               const CommonState& commonState,
                               HWND window) :
    _window{ window },
    _commonState{ commonState },
    _d2dState{ dxgiAPI, window, AppendCommonOverlayUIColors(commonState.lineColor) },
    _tickFunc{ [this, tickFunc, &toolState] {
        tickFunc(_commonState, toolState, _window, _d2dState);
    } }
{
}

OverlayUIState::~OverlayUIState()
{
    _stopRequested.store(true, std::memory_order_release);
    StartUILoop();
    _ownerThreadWakeEvent.SetEvent();
    try
    {
        if (_uiThread.joinable())
            _uiThread.join();
    }
    catch (...)
    {
    }
}

// Returning unique_ptr, since we need to pin ui state in memory
template<typename ToolT, typename TickFuncT>
inline std::unique_ptr<OverlayUIState> OverlayUIState::CreateInternal(const DxgiAPI* dxgi,
                                                                      ToolT& toolState,
                                                                      TickFuncT tickFunc,
                                                                      CommonState& commonState,
                                                                      const wchar_t* toolWindowClassName,
                                                                      void* windowParam,
                                                                      const MonitorInfo& monitor,
                                                                      const bool excludeFromCapture)
{
    wil::shared_event uiCreatedEvent(wil::EventOptions::ManualReset);
    std::unique_ptr<OverlayUIState> uiState;
    std::thread threadHandle = SpawnLoggedThread(L"OverlayUI thread", [&] {
        OverlayUIState* state = nullptr;
        {
            auto sinalUICreatedEvent = wil::scope_exit([&] { uiCreatedEvent.SetEvent(); });

            const HWND window = CreateOverlayUIWindow(commonState, monitor, excludeFromCapture, toolWindowClassName, windowParam);

            uiState = std::unique_ptr<OverlayUIState>{ new OverlayUIState{ dxgi, toolState, tickFunc, commonState, window } };
            uiState->_monitorArea = monitor.GetScreenSize(true);
            // we must create window + d2d state in the same thread, then store thread handle in uiState, thus
            // lifetime is ok here, since we join the thread in destructor
            state = uiState.get();
        }

        state->RunUILoop();

        commonState.closeOnOtherMonitors = true;
        commonState.sessionCompletedCallback();
    });

    uiCreatedEvent.wait();
    if (uiState)
        uiState->_uiThread = std::move(threadHandle);
    else if (threadHandle.joinable())
        threadHandle.join();

    return uiState;
}

std::unique_ptr<OverlayUIState> OverlayUIState::Create(const DxgiAPI* dxgi,
                                                       Serialized<MeasureToolState>& toolState,
                                                       CommonState& commonState,
                                                       const MonitorInfo& monitor)
{
    bool excludeFromCapture = false;
    toolState.Read([&](const MeasureToolState& s) {
        excludeFromCapture = s.global.continuousCapture;
    });
    auto overlay = OverlayUIState::CreateInternal(dxgi,
                                                  toolState,
                                                  DrawMeasureToolTick,
                                                  commonState,
                                                  NonLocalizable::MeasureToolOverlayWindowName,
                                                  &toolState,
                                                  monitor,
                                                  excludeFromCapture);
    if (overlay)
    {
        overlay->StartUILoop();
    }
    return overlay;
}

std::unique_ptr<OverlayUIState> OverlayUIState::Create(const DxgiAPI* dxgi,
                                                       BoundsToolState& toolState,
                                                       CommonState& commonState,
                                                       const MonitorInfo& monitor)
{
    return OverlayUIState::CreateInternal(dxgi,
                                          toolState,
                                          DrawBoundsToolTick,
                                          commonState,
                                          NonLocalizable::BoundsToolOverlayWindowName,
                                          &toolState,
                                          monitor,
                                          false);
}
