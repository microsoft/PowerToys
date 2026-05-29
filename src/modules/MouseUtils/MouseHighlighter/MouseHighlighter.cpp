// MouseHighlighter.cpp : Defines the entry point for the application.
//

#include "pch.h"
#include "MouseHighlighter.h"
#include "trace.h"
#include <cmath>
#include <algorithm>
#include <memory>
#include <vector>

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
    // Ripple pulse variants. Press = expanding ring+glow+dot, Release = contracting
    // dimmer echo on button-up, Drag = small fading dot emitted along a held drag.
    enum class RippleKind
    {
        Press,
        Release,
        Drag
    };
    // Spawn a ClickLight-style ripple pulse at the given client-area point.
    void SpawnRipplePulse(POINT clientPt, MouseButton button, RippleKind kind);
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
    bool m_rippleMode = false;
    bool m_rippleShowDragTrail = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_DRAG_TRAIL;
    bool m_rippleShowReleasePulse = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_RELEASE_PULSE;
    float m_rippleSize = static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SIZE);
    float m_rippleIntensity = static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_INTENSITY);
    int m_rippleDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_DURATION_MS;

    // Tracking for ripple-mode drag emission (independent of the legacy
    // m_leftButtonPressed / m_rightButtonPressed which drive the circle mode).
    bool m_rippleLeftPressed = false;
    bool m_rippleRightPressed = false;
    POINT m_lastRippleDragPt = { 0, 0 };

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
            if (instance->m_rippleMode)
            {
                if (instance->m_leftPointerEnabled)
                {
                    POINT pt = hookData->pt;
                    ScreenToClient(instance->m_hwnd, &pt);
                    instance->SpawnRipplePulse(pt, MouseButton::Left, Highlighter::RippleKind::Press);
                    instance->m_rippleLeftPressed = true;
                    instance->m_lastRippleDragPt = hookData->pt;
                    if (instance->m_timer_id == 0)
                    {
                        instance->m_timer_id = SetTimer(instance->m_hwnd, BRING_TO_FRONT_TIMER_ID, 10, nullptr);
                    }
                }
                break;
            }
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
            if (instance->m_rippleMode)
            {
                if (instance->m_rightPointerEnabled)
                {
                    POINT pt = hookData->pt;
                    ScreenToClient(instance->m_hwnd, &pt);
                    instance->SpawnRipplePulse(pt, MouseButton::Right, Highlighter::RippleKind::Press);
                    instance->m_rippleRightPressed = true;
                    instance->m_lastRippleDragPt = hookData->pt;
                    if (instance->m_timer_id == 0)
                    {
                        instance->m_timer_id = SetTimer(instance->m_hwnd, BRING_TO_FRONT_TIMER_ID, 10, nullptr);
                    }
                }
                break;
            }
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
                // same as for the left button, start a timer to reposition ourselves to topmost position
                if (instance->m_timer_id == 0)
                {
                    instance->m_timer_id = SetTimer(instance->m_hwnd, BRING_TO_FRONT_TIMER_ID, 10, nullptr);
                }
            }
            break;
        case WM_MOUSEMOVE:
            if (instance->m_rippleMode)
            {
                if (instance->m_rippleShowDragTrail && (instance->m_rippleLeftPressed || instance->m_rippleRightPressed))
                {
                    // Throttle: only emit a drag dot when the cursor has moved a
                    // few pixels — matches ClickLight's ~2.5 px append threshold.
                    LONG dx = hookData->pt.x - instance->m_lastRippleDragPt.x;
                    LONG dy = hookData->pt.y - instance->m_lastRippleDragPt.y;
                    if ((dx * dx + dy * dy) >= 9)
                    {
                        MouseButton heldButton = instance->m_rippleLeftPressed ? MouseButton::Left : MouseButton::Right;
                        POINT pt = hookData->pt;
                        ScreenToClient(instance->m_hwnd, &pt);
                        instance->SpawnRipplePulse(pt, heldButton, Highlighter::RippleKind::Drag);
                        instance->m_lastRippleDragPt = hookData->pt;
                    }
                }
                break;
            }
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
            if (instance->m_rippleMode)
            {
                if (instance->m_rippleLeftPressed)
                {
                    if (instance->m_rippleShowReleasePulse && instance->m_leftPointerEnabled)
                    {
                        POINT pt = hookData->pt;
                        ScreenToClient(instance->m_hwnd, &pt);
                        instance->SpawnRipplePulse(pt, MouseButton::Left, Highlighter::RippleKind::Release);
                    }
                    instance->m_rippleLeftPressed = false;
                }
                break;
            }
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
            if (instance->m_rippleMode)
            {
                if (instance->m_rippleRightPressed)
                {
                    if (instance->m_rippleShowReleasePulse && instance->m_rightPointerEnabled)
                    {
                        POINT pt = hookData->pt;
                        ScreenToClient(instance->m_hwnd, &pt);
                        instance->SpawnRipplePulse(pt, MouseButton::Right, Highlighter::RippleKind::Release);
                    }
                    instance->m_rippleRightPressed = false;
                }
                break;
            }
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
    m_rippleLeftPressed = false;
    m_rippleRightPressed = false;
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
    m_rippleMode = settings.rippleMode && !m_spotlightMode;
    m_rippleSize = (settings.rippleSize > 0) ? static_cast<float>(settings.rippleSize) : static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SIZE);
    m_rippleIntensity = (settings.rippleIntensity > 0.0) ? static_cast<float>(settings.rippleIntensity) : static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_INTENSITY);
    m_rippleDurationMs = (settings.rippleDurationMs > 0) ? settings.rippleDurationMs : MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_DURATION_MS;
    m_rippleShowDragTrail = settings.rippleShowDragTrail;
    m_rippleShowReleasePulse = settings.rippleShowReleasePulse;

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

// Spawn a ClickLight-inspired expanding ring + glow pulse at the cursor.
// Each click emits a transient, self-cleaning pulse — pulses can overlap.
void Highlighter::SpawnRipplePulse(POINT clientPt, MouseButton button, RippleKind kind)
{
    if (!m_compositor || !m_shape)
    {
        return;
    }

    winrt::Windows::UI::Color color;
    if (button == MouseButton::Left)
    {
        color = m_leftClickColor;
    }
    else if (button == MouseButton::Right)
    {
        color = m_rightClickColor;
    }
    else
    {
        color = m_alwaysColor;
    }

    if (color.A == 0)
    {
        return;
    }

    // Resolve sizing/intensity/duration from the ripple-specific settings so
    // they're independent of the legacy "always-on dot" controls.
    const float baseSize = (m_rippleSize > 1.0f) ? m_rippleSize : 1.0f;
    float intensity = m_rippleIntensity;
    if (intensity < 0.15f) intensity = 0.15f;
    if (intensity > 1.35f) intensity = 1.35f;

    int durationMs = m_rippleDurationMs;
    if (durationMs < 60) durationMs = 60;
    if (durationMs > 2000) durationMs = 2000;
    if (kind == RippleKind::Release)
    {
        durationMs = static_cast<int>(durationMs * 0.78f);
    }
    else if (kind == RippleKind::Drag)
    {
        durationMs = (std::min)(380, static_cast<int>(durationMs * 0.82f));
    }
    if (durationMs < 40) durationMs = 40;
    auto dur = std::chrono::milliseconds(durationMs);

    // Cubic ease-out (matches ClickLight's 1 - (1 - p)^3 feel).
    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.215f, 0.61f }, { 0.355f, 1.0f });

    // ClickLight visual recipe: alpha = (0.18 + intensity * 0.78) * baseColor.A,
    // line width = max(2.25, baseSize * (0.035 + intensity * 0.045)).
    const float alphaMul = 0.18f + intensity * 0.78f;

    auto makeColor = [&](float mul) {
        float a = static_cast<float>(color.A) * mul;
        if (a < 0.0f) a = 0.0f;
        if (a > 255.0f) a = 255.0f;
        return winrt::Windows::UI::ColorHelper::FromArgb(static_cast<uint8_t>(a), color.R, color.G, color.B);
    };
    auto transparent = winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B);

    const float fx = static_cast<float>(clientPt.x);
    const float fy = static_cast<float>(clientPt.y);

    // Track shapes spawned by this pulse so the completion callback can remove them.
    auto spawned = std::make_shared<std::vector<winrt::CompositionSpriteShape>>();

    auto addEllipseShape = [&](float startRadius,
                               float endRadius,
                               winrt::Windows::UI::Color startColor,
                               bool stroked,
                               float strokeWidth) {
        auto geom = m_compositor.CreateEllipseGeometry();
        geom.Radius({ startRadius, startRadius });
        auto brush = m_compositor.CreateColorBrush(startColor);
        auto shape = m_compositor.CreateSpriteShape(geom);
        shape.Offset({ fx, fy });
        if (stroked)
        {
            shape.StrokeBrush(brush);
            shape.StrokeThickness(strokeWidth);
            shape.IsStrokeNonScaling(true);
        }
        else
        {
            shape.FillBrush(brush);
        }
        m_shape.Shapes().Append(shape);
        spawned->push_back(shape);

        auto sizeAnim = m_compositor.CreateVector2KeyFrameAnimation();
        sizeAnim.InsertKeyFrame(0.0f, { startRadius, startRadius });
        sizeAnim.InsertKeyFrame(1.0f, { endRadius, endRadius }, ease);
        sizeAnim.Duration(dur);
        geom.StartAnimation(L"Radius", sizeAnim);

        auto colorAnim = m_compositor.CreateColorKeyFrameAnimation();
        colorAnim.InsertKeyFrame(0.0f, startColor);
        colorAnim.InsertKeyFrame(1.0f, transparent, ease);
        colorAnim.Duration(dur);
        brush.StartAnimation(L"Color", colorAnim);
    };

    auto batch = m_compositor.CreateScopedBatch(winrt::CompositionBatchTypes::Animation);

    if (kind == RippleKind::Press)
    {
        const float ringStart = baseSize * 0.18f;
        const float ringEnd = baseSize * 0.80f;
        const float glowStart = baseSize * 0.28f;
        const float glowEnd = baseSize * 1.06f;
        const float lineWidth = (std::max)(2.25f, baseSize * (0.035f + intensity * 0.045f));

        // Optional outer glow (only at higher intensities, like ClickLight).
        if (intensity >= 0.7f)
        {
            const float glowMul = (intensity >= 1.2f) ? 0.18f : 0.08f;
            addEllipseShape(glowStart, glowEnd, makeColor(glowMul * intensity), /*stroked*/ false, 0.0f);
        }
        // Expanding stroked ring.
        addEllipseShape(ringStart, ringEnd, makeColor(alphaMul), /*stroked*/ true, lineWidth);
        // Small filled center dot for extra punch.
        const float dotRadius = baseSize * 0.085f;
        addEllipseShape(dotRadius, dotRadius, makeColor(alphaMul * 0.75f), /*stroked*/ false, 0.0f);
    }
    else if (kind == RippleKind::Release)
    {
        // Contracting echo: the ring starts large and shrinks inward at lower alpha.
        const float relSize = baseSize * 0.82f;
        const float ringStart = relSize * 0.76f;
        const float ringEnd = relSize * 0.34f;
        const float glowStart = relSize * 0.76f * 1.25f;
        const float glowEnd = relSize * 0.34f * 1.25f;
        const float lineWidth = (std::max)(2.25f, relSize * (0.035f + intensity * 0.045f)) * 0.55f;
        const float releaseAlpha = alphaMul * 0.55f;

        if (intensity >= 0.7f)
        {
            const float glowMul = (intensity >= 1.2f) ? 0.18f : 0.08f;
            addEllipseShape(glowStart, glowEnd, makeColor(glowMul * intensity * 0.45f), /*stroked*/ false, 0.0f);
        }
        addEllipseShape(ringStart, ringEnd, makeColor(releaseAlpha), /*stroked*/ true, lineWidth);
    }
    else // RippleKind::Drag
    {
        // Small fading dot along the drag path.
        const float dragBase = baseSize * 0.6f;
        const float dotRadius = dragBase * (0.08f + 0.065f * intensity);
        if (dotRadius >= 0.5f)
        {
            addEllipseShape(dotRadius, dotRadius, makeColor(alphaMul * 0.78f), /*stroked*/ false, 0.0f);
        }
    }

    batch.End();

    if (spawned->empty())
    {
        return;
    }

    auto dispatcher = m_dispatcherQueueController.DispatcherQueue();
    batch.Completed([dispatcher, spawned](auto&&, auto&&) {
        // Marshal shape removal back to the dispatcher thread.
        dispatcher.TryEnqueue([spawned]() {
            try
            {
                if (Highlighter::instance == nullptr || Highlighter::instance->m_shape == nullptr)
                {
                    return;
                }
                auto shapes = Highlighter::instance->m_shape.Shapes();
                for (auto const& s : *spawned)
                {
                    uint32_t index = 0;
                    if (shapes.IndexOf(s, index))
                    {
                        shapes.RemoveAt(index);
                    }
                }
            }
            catch (...)
            {
                // Highlighter may have torn down between batch completion and dispatch — ignore.
            }
        });
    });
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
