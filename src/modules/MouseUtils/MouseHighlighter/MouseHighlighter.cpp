// MouseHighlighter.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "MouseHighlighter.h"
#include "trace.h"
#include <cmath>
#include <algorithm>

#ifdef COMPOSITION
namespace winrt
{
    using namespace winrt::Windows::System;
    using namespace winrt::Windows::UI::Composition;
}

namespace ABI
{
    using namespace ABI::Windows::System;
    using namespace ABI::Windows::UI::Composition::Desktop;
}
#endif

struct Highlighter
{
    bool MyRegisterClass(HINSTANCE hInstance);
    static Highlighter* instance;
    void Terminate();
    void SwitchActivationMode();
    void ApplySettings(MouseHighlighterSettings settings);

private:
    enum class MouseButton
    {
        Left,
        Right,
        None
    };

    void DestroyHighlighter();
    static LRESULT CALLBACK WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept;
    void StartDrawing();
    void StopDrawing();
    bool CreateHighlighter();
    void AddDrawingPoint(MouseButton button);
    void UpdateDrawingPointPosition(MouseButton button);
    void StartDrawingPointFading(MouseButton button);
    void ClearDrawingPoint();
    void ClearDrawing();
    void BringToFront();
    HHOOK m_mouseHook = NULL;
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept;
    // Helpers for spotlight overlay
    float GetDpiScale() const;
    void UpdateSpotlightMask(float cx, float cy, float radius, bool show);

    static constexpr auto m_className = L"MouseHighlighter";
    static constexpr auto m_windowTitle = L"PowerToys Mouse Highlighter";
    HWND m_hwndOwner = NULL;
    HWND m_hwnd = NULL;
    HINSTANCE m_hinstance = NULL;
    static constexpr DWORD WM_SWITCH_ACTIVATION_MODE = WM_APP;

    winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };
    winrt::Compositor m_compositor{ nullptr };
    winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::ContainerVisual m_root{ nullptr };
    winrt::LayerVisual m_layer{ nullptr };
    winrt::ShapeVisual m_shape{ nullptr };

    winrt::CompositionSpriteShape m_leftPointer{ nullptr };
    winrt::CompositionSpriteShape m_rightPointer{ nullptr };
    winrt::CompositionSpriteShape m_alwaysPointer{ nullptr };
    // Spotlight overlay (mask with soft feathered edge)
    winrt::SpriteVisual m_overlay{ nullptr };
    winrt::CompositionMaskBrush m_spotlightMask{ nullptr };
    winrt::CompositionRadialGradientBrush m_spotlightMaskGradient{ nullptr };
    winrt::CompositionColorBrush m_spotlightSource{ nullptr };
    winrt::CompositionColorGradientStop m_maskStopCenter{ nullptr };
    winrt::CompositionColorGradientStop m_maskStopInner{ nullptr };
    winrt::CompositionColorGradientStop m_maskStopOuter{ nullptr };

    bool m_leftPointerEnabled = true;
    bool m_rightPointerEnabled = true;
    bool m_alwaysPointerEnabled = true;
    bool m_spotlightMode = false;

    bool m_leftButtonPressed = false;
    bool m_rightButtonPressed = false;
    UINT_PTR m_timer_id = 0;

    bool m_visible = false;

    // Possible configurable settings
    float m_radius = MOUSE_HIGHLIGHTER_DEFAULT_RADIUS;

    int m_fadeDelay_ms = MOUSE_HIGHLIGHTER_DEFAULT_DELAY_MS;
    int m_fadeDuration_ms = MOUSE_HIGHLIGHTER_DEFAULT_DURATION_MS;

    winrt::Windows::UI::Color m_leftClickColor = MOUSE_HIGHLIGHTER_DEFAULT_LEFT_BUTTON_COLOR;
    winrt::Windows::UI::Color m_rightClickColor = MOUSE_HIGHLIGHTER_DEFAULT_RIGHT_BUTTON_COLOR;
    winrt::Windows::UI::Color m_alwaysColor = MOUSE_HIGHLIGHTER_DEFAULT_ALWAYS_COLOR;
};
static const uint32_t BRING_TO_FRONT_TIMER_ID = 123;
Highlighter* Highlighter::instance = nullptr;

bool Highlighter::CreateHighlighter()
{
    try
    {
        // We need a dispatcher queue.
        DispatcherQueueOptions options = {
            sizeof(options),
            DQTYPE_THREAD_CURRENT,
            DQTAT_COM_ASTA,
        };
        ABI::IDispatcherQueueController* controller;
        winrt::check_hresult(CreateDispatcherQueueController(options, &controller));
        *winrt::put_abi(m_dispatcherQueueController) = controller;

        // Create the compositor for our window.
        m_compositor = winrt::Compositor();
        ABI::IDesktopWindowTarget* target;
        winrt::check_hresult(m_compositor.as<ABI::ICompositorDesktopInterop>()->CreateDesktopWindowTarget(m_hwnd, false, &target));
        *winrt::put_abi(m_target) = target;

        // Create visual root
        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_target.Root(m_root);

        // Create the shapes container visual and add it to root.
        m_shape = m_compositor.CreateShapeVisual();
        m_shape.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(m_shape);

        // Create spotlight overlay (soft feather, DPI-aware)
        m_overlay = m_compositor.CreateSpriteVisual();
        m_overlay.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_spotlightSource = m_compositor.CreateColorBrush(m_alwaysColor);
        m_spotlightMaskGradient = m_compositor.CreateRadialGradientBrush();
        m_spotlightMaskGradient.MappingMode(winrt::CompositionMappingMode::Absolute);
        // Center region fully transparent
        m_maskStopCenter = m_compositor.CreateColorGradientStop();
        m_maskStopCenter.Offset(0.0f);
        m_maskStopCenter.Color(winrt::Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0));
        // Inner edge of feather (still transparent)
        m_maskStopInner = m_compositor.CreateColorGradientStop();
        m_maskStopInner.Offset(0.995f); // will be updated per-radius
        m_maskStopInner.Color(winrt::Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0));
        // Outer edge (opaque mask -> overlay visible)
        m_maskStopOuter = m_compositor.CreateColorGradientStop();
        m_maskStopOuter.Offset(1.0f);
        m_maskStopOuter.Color(winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 255, 255));
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopCenter);
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopInner);
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopOuter);

        m_spotlightMask = m_compositor.CreateMaskBrush();
        m_spotlightMask.Source(m_spotlightSource);
        m_spotlightMask.Mask(m_spotlightMaskGradient);
        m_overlay.Brush(m_spotlightMask);
        m_overlay.IsVisible(false);
        m_root.Children().InsertAtTop(m_overlay);

        return true;
    }
    catch (...)
    {
        return false;
    }
}

void Highlighter::AddDrawingPoint(MouseButton button)
{
    if (!m_compositor)
        return;

    POINT pt;

    // Applies DPIs.
    GetCursorPos(&pt);

    // Converts to client area of the Windows.
    ScreenToClient(m_hwnd, &pt);

    // Create circle and add it.
    auto circleGeometry = m_compositor.CreateEllipseGeometry();
    circleGeometry.Radius({ m_radius, m_radius });

    auto circleShape = m_compositor.CreateSpriteShape(circleGeometry);
    circleShape.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
    if (button == MouseButton::Left)
    {
        circleShape.FillBrush(m_compositor.CreateColorBrush(m_leftClickColor));
        m_leftPointer = circleShape;
    }
    else if (button == MouseButton::Right)
    {
        circleShape.FillBrush(m_compositor.CreateColorBrush(m_rightClickColor));
        m_rightPointer = circleShape;
    }
    else
    {
        // always
        if (m_spotlightMode)
        {
            UpdateSpotlightMask(static_cast<float>(pt.x), static_cast<float>(pt.y), m_radius, true);
            return;
        }
        else
        {
            circleShape.FillBrush(m_compositor.CreateColorBrush(m_alwaysColor));
            m_alwaysPointer = circleShape;
        }
    }

    m_shape.Shapes().Append(circleShape);

    // TODO: We're leaking shapes for long drawing sessions.
    // Perhaps add a task to the Dispatcher every X circles to clean up.

    // Get back on top in case other Window is now the topmost.
    // HACK: Draw with 1 pixel off. Otherwise, Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);
}

void Highlighter::UpdateDrawingPointPosition(MouseButton button)
{
    POINT pt;

    // Applies DPIs.
    GetCursorPos(&pt);

    // Converts to client area of the Windows.
    ScreenToClient(m_hwnd, &pt);

    if (button == MouseButton::Left)
    {
        m_leftPointer.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
    }
    else if (button == MouseButton::Right)
    {
        m_rightPointer.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
    }
    else
    {
        // always / spotlight idle
        if (m_spotlightMode)
        {
            UpdateSpotlightMask(static_cast<float>(pt.x), static_cast<float>(pt.y), m_radius, true);
        }
        else if (m_alwaysPointer)
        {
            m_alwaysPointer.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
        }
    }
}
void Highlighter::StartDrawingPointFading(MouseButton button)
{
    winrt::Windows::UI::Composition::CompositionSpriteShape circleShape{ nullptr };
    if (button == MouseButton::Left)
    {
        circleShape = m_leftPointer;
    }
    else
    {
        // right
        circleShape = m_rightPointer;
    }

    auto brushColor = circleShape.FillBrush().as<winrt::Windows::UI::Composition::CompositionColorBrush>().Color();

    // Animate opacity to simulate a fade away effect.
    auto animation = m_compositor.CreateColorKeyFrameAnimation();
    animation.InsertKeyFrame(1, winrt::Windows::UI::ColorHelper::FromArgb(0, brushColor.R, brushColor.G, brushColor.B));
    using timeSpan = std::chrono::duration<int, std::ratio<1, 1000>>;
    // HACK: If user sets these durations to 0, the fade won't work. Setting them to 1ms instead to avoid this.
    if (m_fadeDuration_ms == 0)
    {
        m_fadeDuration_ms = 1;
    }
    if (m_fadeDelay_ms == 0)
    {
        m_fadeDelay_ms = 1;
    }
    std::chrono::milliseconds duration(m_fadeDuration_ms);
    std::chrono::milliseconds delay(m_fadeDelay_ms);
    animation.Duration(timeSpan(duration));
    animation.DelayTime(timeSpan(delay));

    circleShape.FillBrush().StartAnimation(L"Color", animation);
}

void Highlighter::ClearDrawingPoint()
{
    if (m_spotlightMode)
    {
        if (m_overlay)
        {
            m_overlay.IsVisible(false);
        }
    }
    else
    {
        if (m_alwaysPointer)
        {
            m_alwaysPointer.FillBrush().as<winrt::Windows::UI::Composition::CompositionColorBrush>().Color(winrt::Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0));
        }
    }
}

void Highlighter::ClearDrawing()
{
    if (nullptr == m_shape || nullptr == m_shape.Shapes())
    {
        // Guard against m_shape not being initialized.
        return;
    }

    m_shape.Shapes().Clear();
}

LRESULT CALLBACK Highlighter::MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam) noexcept
{
    if (nCode >= 0)
    {
        MSLLHOOKSTRUCT* hookData = reinterpret_cast<MSLLHOOKSTRUCT*>(lParam);
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
            if (instance->m_leftPointerEnabled)
            {
                if (instance->m_alwaysPointerEnabled && !instance->m_rightButtonPressed)
                {
                    // Clear AlwaysPointer only when it's enabled and RightPointer is not active
                    instance->ClearDrawingPoint();
                }
                if (instance->m_leftButtonPressed)
                {
                    // There might be a stray point from the user releasing the mouse button on an elevated window, which wasn't caught by us.
                    instance->StartDrawingPointFading(MouseButton::Left);
                }

                instance->AddDrawingPoint(MouseButton::Left);
                instance->m_leftButtonPressed = true;
                // start a timer for the scenario, when the user clicks a pinned window which has no focus.
                // after we drow the highlighting circle the pinned window will jump in front of us,
                // we have to bring our window back to topmost position
                if (instance->m_timer_id == 0)
                {
                    instance->m_timer_id = SetTimer(instance->m_hwnd, BRING_TO_FRONT_TIMER_ID, 10, nullptr);
                }
            }
            break;
        case WM_RBUTTONDOWN:
            if (instance->m_rightPointerEnabled)
            {
                if (instance->m_alwaysPointerEnabled && !instance->m_leftButtonPressed)
                {
                    // Clear AlwaysPointer only when it's enabled and LeftPointer is not active
                    instance->ClearDrawingPoint();
                }
                if (instance->m_rightButtonPressed)
                {
                    // There might be a stray point from the user releasing the mouse button on an elevated window, which wasn't caught by us.
                    instance->StartDrawingPointFading(MouseButton::Right);
                }
                instance->AddDrawingPoint(MouseButton::Right);
                instance->m_rightButtonPressed = true;
                // same as for the left button, start a timer for reposition ourselves to topmost position
                if (instance->m_timer_id == 0)
                {
                    instance->m_timer_id = SetTimer(instance->m_hwnd, BRING_TO_FRONT_TIMER_ID, 10, nullptr);
                }
            }
            break;
        case WM_MOUSEMOVE:
            if (instance->m_leftButtonPressed)
            {
                instance->UpdateDrawingPointPosition(MouseButton::Left);
            }
            if (instance->m_rightButtonPressed)
            {
                instance->UpdateDrawingPointPosition(MouseButton::Right);
            }
            if (instance->m_alwaysPointerEnabled && !instance->m_leftButtonPressed && !instance->m_rightButtonPressed)
            {
                instance->UpdateDrawingPointPosition(MouseButton::None);
            }
            break;
        case WM_LBUTTONUP:
            if (instance->m_leftButtonPressed)
            {
                instance->StartDrawingPointFading(MouseButton::Left);
                instance->m_leftButtonPressed = false;
                if (instance->m_alwaysPointerEnabled && !instance->m_rightButtonPressed)
                {
                    // Add AlwaysPointer only when it's enabled and RightPointer is not active
                    instance->AddDrawingPoint(MouseButton::None);
                }
            }
            break;
        case WM_RBUTTONUP:
            if (instance->m_rightButtonPressed)
            {
                instance->StartDrawingPointFading(MouseButton::Right);
                instance->m_rightButtonPressed = false;
                if (instance->m_alwaysPointerEnabled && !instance->m_leftButtonPressed)
                {
                    // Add AlwaysPointer only when it's enabled and LeftPointer is not active
                    instance->AddDrawingPoint(MouseButton::None);
                }
            }
            break;
        default:
            break;
        }
    }
    return CallNextHookEx(0, nCode, wParam, lParam);
}

void Highlighter::StartDrawing()
{
    Logger::info("Starting draw mode.");
    Trace::StartHighlightingSession();

    if (m_spotlightMode && m_alwaysColor.A != 0)
    {
        Trace::StartSpotlightSession();
    }

    m_visible = true;

    // HACK: Draw with 1 pixel off. Otherwise, Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);
    ClearDrawing();
    ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);

    instance->AddDrawingPoint(Highlighter::MouseButton::None);

    m_mouseHook = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, m_hinstance, 0);
}

void Highlighter::StopDrawing()
{
    Logger::info("Stopping draw mode.");
    m_visible = false;
    m_leftButtonPressed = false;
    m_rightButtonPressed = false;
    m_leftPointer = nullptr;
    m_rightPointer = nullptr;
    m_alwaysPointer = nullptr;
    if (m_overlay)
    {
        m_overlay.IsVisible(false);
    }
    ShowWindow(m_hwnd, SW_HIDE);
    UnhookWindowsHookEx(m_mouseHook);
    ClearDrawing();
    m_mouseHook = NULL;
}

void Highlighter::SwitchActivationMode()
{
    PostMessage(m_hwnd, WM_SWITCH_ACTIVATION_MODE, 0, 0);
}

void Highlighter::ApplySettings(MouseHighlighterSettings settings)
{
    m_radius = static_cast<float>(settings.radius);
    m_fadeDelay_ms = settings.fadeDelayMs;
    m_fadeDuration_ms = settings.fadeDurationMs;
    m_leftClickColor = settings.leftButtonColor;
    m_rightClickColor = settings.rightButtonColor;
    m_alwaysColor = settings.alwaysColor;
    m_leftPointerEnabled = settings.leftButtonColor.A != 0;
    m_rightPointerEnabled = settings.rightButtonColor.A != 0;
    m_alwaysPointerEnabled = settings.alwaysColor.A != 0;
    m_spotlightMode = settings.spotlightMode && settings.alwaysColor.A != 0;

    if (m_spotlightMode)
    {
        m_leftPointerEnabled = false;
        m_rightPointerEnabled = false;
    }

    // Keep spotlight overlay color updated
    if (m_spotlightSource)
    {
        m_spotlightSource.Color(m_alwaysColor);
    }
    if (!m_spotlightMode && m_overlay)
    {
        m_overlay.IsVisible(false);
    }

    if (instance->m_visible)
    {
        instance->StopDrawing();
        instance->StartDrawing();
    }
}

void Highlighter::BringToFront()
{
    // HACK: Draw with 1 pixel off. Otherwise, Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);
}

void Highlighter::DestroyHighlighter()
{
    StopDrawing();
    PostQuitMessage(0);
}

LRESULT CALLBACK Highlighter::WndProc(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_NCCREATE:
        instance->m_hwnd = hWnd;
        return DefWindowProc(hWnd, message, wParam, lParam);
    case WM_CREATE:
        return instance->CreateHighlighter() ? 0 : -1;
    case WM_NCHITTEST:
        return HTTRANSPARENT;
    case WM_SWITCH_ACTIVATION_MODE:
        if (instance->m_visible)
        {
            instance->StopDrawing();
        }
        else
        {
            instance->StartDrawing();
        }
        break;
    case WM_DESTROY:
        instance->DestroyHighlighter();
        break;
    case WM_TIMER:
    {
        switch (wParam)
        {
            // when the bring-to-front-timer expires (every 10 ms), we are repositioning our window to topmost Z order position
            // As we experience that it takes 0-30 ms that the pinned window hides our window,
            // we await 5 timer ticks (50 ms together) and then we stop the timer.
            // If we would use a timer with a 50 ms period, there would be a flickering on the UI, as in most of the cases
            // the pinned window hides our window in a few milliseconds.
        case BRING_TO_FRONT_TIMER_ID:
            static int fireCount = 0;
            if (fireCount++ >= 4)
            {
                KillTimer(instance->m_hwnd, instance->m_timer_id);
                instance->m_timer_id = 0;
                fireCount = 0;
            }
            instance->BringToFront();
            break;
        }
        break;
    }
    default:
        return DefWindowProc(hWnd, message, wParam, lParam);
    }
    return 0;
}

bool Highlighter::MyRegisterClass(HINSTANCE hInstance)
{
    WNDCLASS wc{};

    m_hinstance = hInstance;

    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    if (!GetClassInfoW(hInstance, m_className, &wc))
    {
        wc.lpfnWndProc = WndProc;
        wc.hInstance = hInstance;
        wc.hIcon = LoadIcon(hInstance, IDI_APPLICATION);
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
        wc.lpszClassName = m_className;

        if (!RegisterClassW(&wc))
        {
            return false;
        }
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hInstance, nullptr);

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_NOREDIRECTIONBITMAP | WS_EX_TOOLWINDOW;
    return CreateWindowExW(exStyle, m_className, m_windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hInstance, nullptr) != nullptr;
}

void Highlighter::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
        DestroyWindow(m_hwndOwner);
    });
    if (!enqueueSucceeded)
    {
        Logger::error("Couldn't enqueue message to destroy the window.");
    }
}

float Highlighter::GetDpiScale() const
{
    return static_cast<float>(GetDpiForWindow(m_hwnd)) / 96.0f;
}

// Update spotlight radial mask center/radius with DPI-aware feather
void Highlighter::UpdateSpotlightMask(float cx, float cy, float radius, bool show)
{
    if (!m_spotlightMaskGradient)
    {
        return;
    }

    m_spotlightMaskGradient.EllipseCenter({ cx, cy });
    m_spotlightMaskGradient.EllipseRadius({ radius, radius });

    const float dpiScale = GetDpiScale();
    // Target a very fine edge: ~1 physical pixel, convert to DIPs: 1 / dpiScale
    const float featherDip = 1.0f / (dpiScale > 0.0f ? dpiScale : 1.0f);
    const float safeRadius = (std::max)(radius, 1.0f);
    const float featherRel = (std::min)(0.25f, featherDip / safeRadius);

    if (m_maskStopInner)
    {
        m_maskStopInner.Offset((std::max)(0.0f, 1.0f - featherRel));
    }

    if (m_spotlightSource)
    {
        m_spotlightSource.Color(m_alwaysColor);
    }
    if (m_overlay)
    {
        m_overlay.IsVisible(show);
    }
}

#pragma region MouseHighlighter_API

void MouseHighlighterApplySettings(MouseHighlighterSettings settings)
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Applying settings.");
        Highlighter::instance->ApplySettings(settings);
    }
}

void MouseHighlighterSwitch()
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Switching activation mode.");
        Highlighter::instance->SwitchActivationMode();
    }
}

void MouseHighlighterDisable()
{
    if (Highlighter::instance != nullptr)
    {
        Logger::info("Terminating the highlighter instance.");
        Highlighter::instance->Terminate();
    }
}

bool MouseHighlighterIsEnabled()
{
    return (Highlighter::instance != nullptr);
}

int MouseHighlighterMain(HINSTANCE hInstance, MouseHighlighterSettings settings)
{
    Logger::info("Starting a highlighter instance.");
    if (Highlighter::instance != nullptr)
    {
        Logger::error("A highlighter instance was still working when trying to start a new one.");
        return 0;
    }

    // Perform application initialization:
    Highlighter highlighter;
    Highlighter::instance = &highlighter;
    highlighter.ApplySettings(settings);
    if (!highlighter.MyRegisterClass(hInstance))
    {
        Logger::error("Couldn't initialize a highlighter instance.");
        Highlighter::instance = nullptr;
        return FALSE;
    }
    Logger::info("Initialized the highlighter instance.");

    if (settings.autoActivate)
    {
        highlighter.SwitchActivationMode();
    }

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Mouse highlighter message loop ended.");
    Highlighter::instance = nullptr;

    return (int)msg.wParam;
}

#pragma endregion MouseHighlighter_API
