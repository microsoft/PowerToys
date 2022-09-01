#include "pch.h"

#include "BoundsToolOverlayUI.h"
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
    RegisterClassExW(&wcex);

    wcex.lpfnWndProc = BoundsToolWndProc;
    wcex.lpszClassName = NonLocalizable::BoundsToolOverlayWindowName;
    wcex.hCursor = LoadCursorW(nullptr, IDC_CROSS);
    RegisterClassExW(&wcex);
}

HWND CreateOverlayUIWindow(const CommonState& commonState,
                           const MonitorInfo& monitor,
                           const wchar_t* windowClass,
                           void* extraParam)
{
    static std::once_flag windowClassesCreatedFlag;
    std::call_once(windowClassesCreatedFlag, CreateOverlayWindowClasses);

    const auto screenArea = monitor.GetScreenSize(true);
    DWORD windowStyle = WS_EX_TOOLWINDOW;
#if !defined(DEBUG_OVERLAY)
    windowStyle |= WS_EX_TOPMOST;
#endif
    HWND window{
        CreateWindowExW(windowStyle,
                        windowClass,
                        L"PowerToys.MeasureToolOverlay",
                        WS_POPUP,
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
    ShowWindow(window, SW_SHOWNORMAL);
    SetWindowDisplayAffinity(window, WDA_EXCLUDEFROMCAPTURE);
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
    D2D1::ColorF background = D2D1::ColorF(0.96f, 0.96f, 0.96f, 1.0f);
    D2D1::ColorF border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);

    if (WindowsColors::is_dark_mode())
    {
        foreground = D2D1::ColorF::White;
        background = D2D1::ColorF(0.17f, 0.17f, 0.17f, 1.0f);
        border = D2D1::ColorF(0.44f, 0.44f, 0.44f, 0.4f);
    }

    return { lineColor, foreground, background, border };
}

void OverlayUIState::RunUILoop()
{
    while (IsWindow(_window) && !_commonState.closeOnOtherMonitors)
    {
        const auto cursor = _commonState.cursorPosSystemSpace;
        const bool cursorOnScreen = _monitorArea.inside(cursor);
        const bool cursorOverToolbar = _commonState.toolbarBoundingBox.inside(cursor);

        if (cursorOnScreen != _cursorOnScreen)
        {
            _cursorOnScreen = cursorOnScreen;
            if (!cursorOnScreen)
            {
                if (_clearOnCursorLeavingScreen)
                {
                    _d2dState.rt->BeginDraw();
                    _d2dState.rt->Clear();
                    _d2dState.rt->EndDraw();
                }
                PostMessageW(_window, WM_CURSOR_LEFT_MONITOR, {}, {});
            }
        }
        if (cursorOnScreen)
        {
            _d2dState.rt->BeginDraw();
            if (!cursorOverToolbar)
                _tickFunc();
            else
                _d2dState.rt->Clear();

            _d2dState.rt->EndDraw();
        }

        run_message_loop(true, 1);
    }

    DestroyWindow(_window);
}

template<typename StateT, typename TickFuncT>
OverlayUIState::OverlayUIState(StateT& toolState,
                               TickFuncT tickFunc,
                               const CommonState& commonState,
                               HWND window) :
    _window{ window },
    _commonState{ commonState },
    _d2dState{ window, AppendCommonOverlayUIColors(commonState.lineColor) },
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
inline std::unique_ptr<OverlayUIState> OverlayUIState::CreateInternal(ToolT& toolState,
                                                                      TickFuncT tickFunc,
                                                                      CommonState& commonState,
                                                                      const wchar_t* toolWindowClassName,
                                                                      void* windowParam,
                                                                      const MonitorInfo& monitor,
                                                                      const bool clearOnCursorLeavingScreen)
{
    wil::shared_event uiCreatedEvent(wil::EventOptions::ManualReset);
    std::unique_ptr<OverlayUIState> uiState;
    auto threadHandle = SpawnLoggedThread(L"OverlayUI thread", [&] {
        const HWND window = CreateOverlayUIWindow(commonState, monitor, toolWindowClassName, windowParam);
        uiState = std::unique_ptr<OverlayUIState>{ new OverlayUIState{ toolState, tickFunc, commonState, window } };
        uiState->_monitorArea = monitor.GetScreenSize(true);
        uiState->_clearOnCursorLeavingScreen = clearOnCursorLeavingScreen;
        // we must create window + d2d state in the same thread, then store thread handle in uiState, thus
        // lifetime is ok here, since we join the thread in destructor
        auto* state = uiState.get();
        uiCreatedEvent.SetEvent();

        state->RunUILoop();

        commonState.closeOnOtherMonitors = true;
        commonState.sessionCompletedCallback();
    });

    uiCreatedEvent.wait();
    uiState->_uiThread = std::move(threadHandle);
    return uiState;
}

std::unique_ptr<OverlayUIState> OverlayUIState::Create(Serialized<MeasureToolState>& toolState,
                                                       CommonState& commonState,
                                                       const MonitorInfo& monitor)
{
    return OverlayUIState::CreateInternal(toolState,
                                          DrawMeasureToolTick,
                                          commonState,
                                          NonLocalizable::MeasureToolOverlayWindowName,
                                          &toolState,
                                          monitor,
                                          true);
}

std::unique_ptr<OverlayUIState> OverlayUIState::Create(BoundsToolState& toolState,
                                                       CommonState& commonState,
                                                       const MonitorInfo& monitor)
{
    return OverlayUIState::CreateInternal(toolState,
                                          DrawBoundsToolTick,
                                          commonState,
                                          NonLocalizable::BoundsToolOverlayWindowName,
                                          &toolState,
                                          monitor,
                                          false);
}
