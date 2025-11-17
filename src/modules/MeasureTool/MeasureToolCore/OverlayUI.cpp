#include "pch.h"

#include "BoundsToolOverlayUI.h"
#include "constants.h"
#include "MeasureToolOverlayUI.h"
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

    RECT windowRect = {};
    // Exclude toolbar from the window's region to be able to use toolbar during tool usage.
    if (monitor.IsPrimary() && GetWindowRect(window, &windowRect))
    {
        // will be freed during SetWindowRgn call
        const HRGN windowRegion{ CreateRectRgn(windowRect.left, windowRect.top, windowRect.right, windowRect.bottom) };
        wil::unique_hrgn toolbarRegion{ CreateRectRgn(commonState.toolbarBoundingBox.left(),
                                                      commonState.toolbarBoundingBox.top(),
                                                      commonState.toolbarBoundingBox.right(),
                                                      commonState.toolbarBoundingBox.bottom()) };
        const auto res = CombineRgn(windowRegion, windowRegion, toolbarRegion.get(), RGN_DIFF);
        if (res != ERROR)
            SetWindowRgn(window, windowRegion, true);
    }

    return window;
}

std::vector<D2D1::ColorF> AppendCommonOverlayUIColors(const D2D1::ColorF& lineColor)
{
    D2D1::ColorF foreground = D2D1::ColorF::Black;
    D2D1::ColorF background = D2D1::ColorF(0.96f, 0.96f, 0.96f, .93f);
    D2D1::ColorF border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);

    if (WindowsColors::is_dark_mode())
    {
        foreground = D2D1::ColorF::White;
        background = D2D1::ColorF(0.17f, 0.17f, 0.17f, .93f);
        border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);
    }

    return { lineColor, foreground, background, border };
}

void OverlayUIState::RunUILoop()
{
    bool cursorOnScreen = false;

    while (IsWindow(_window) && !_commonState.closeOnOtherMonitors)
    {
        const auto now = std::chrono::high_resolution_clock::now();
        const auto cursor = _commonState.cursorPosSystemSpace;
        const bool cursorOverToolbar = _commonState.toolbarBoundingBox.inside(cursor);
        auto& dxgi = _d2dState.dxgiWindowState;
        if (_monitorArea.inside(cursor) != cursorOnScreen)
        {
            cursorOnScreen = !cursorOnScreen;
            if (!cursorOnScreen)
            {
                PostMessageW(_window, WM_CURSOR_LEFT_MONITOR, {}, {});
            }
        }
        run_message_loop(true, 1);

        dxgi.rt->BeginDraw();
        dxgi.rt->Clear();

        if (!cursorOverToolbar)
            _tickFunc();

        dxgi.rt->EndDraw();
        dxgi.swapChain->Present(0, 0);

        if (cursorOnScreen)
        {
            const auto frameTime = std::chrono::high_resolution_clock::now() - now;
            if (frameTime < consts::TARGET_FRAME_DURATION)
            {
                std::this_thread::sleep_for(consts::TARGET_FRAME_DURATION - frameTime);
            }
        }
        else
        {
            // Don't consume resources while nothing could be updated
            std::this_thread::sleep_for(std::chrono::milliseconds{ 200 });
        }
    }

    DestroyWindow(_window);
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
    PostMessageW(_window, WM_CLOSE, {}, {});
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
    return OverlayUIState::CreateInternal(dxgi,
                                          toolState,
                                          DrawMeasureToolTick,
                                          commonState,
                                          NonLocalizable::MeasureToolOverlayWindowName,
                                          &toolState,
                                          monitor,
                                          excludeFromCapture);
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
