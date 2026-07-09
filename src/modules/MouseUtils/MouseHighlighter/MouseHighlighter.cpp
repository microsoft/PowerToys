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
    // Ripple mode: spawn the press/hold ring + glow at the click point and
    // continue the animation into a fade-out on release. The held ring may
    // optionally follow the cursor while held (gated by m_rippleShowDragTrail).
    void SpawnRippleHoldDot(MouseButton button);
    void FadeRippleHoldDot(MouseButton button);
    // Ripple mode: emit a single self-contained ripple (grow + fade) for a quick
    // click, independent of any held indicator.
    void EmitSingleRipple(MouseButton button);
    // Spotlight mode: pressed-state animation that shrinks the mask while
    // a mouse button is held and restores it on release.
    void SpotlightAnimatePress();
    void SpotlightAnimateRelease();
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
    // Ellipse geometries kept alongside the pointer shapes so press-down /
    // release animations can target the radius directly.
    winrt::CompositionEllipseGeometry m_leftGeometry{ nullptr };
    winrt::CompositionEllipseGeometry m_rightGeometry{ nullptr };
    // Ripple-mode held glow (the soft halo behind the ring) — paired with
    // m_left/rightPointer (which holds the ring shape) while a button is held.
    winrt::CompositionSpriteShape m_leftRippleGlow{ nullptr };
    winrt::CompositionSpriteShape m_rightRippleGlow{ nullptr };
    winrt::CompositionEllipseGeometry m_leftGlowGeometry{ nullptr };
    winrt::CompositionEllipseGeometry m_rightGlowGeometry{ nullptr };
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
    bool m_spotlightPressed = false;
    bool m_rippleMode = true;
    bool m_rippleShowDragTrail = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_DRAG_TRAIL;
    bool m_rippleShowReleasePulse = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SHOW_RELEASE_PULSE;
    float m_rippleSize = static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_SIZE);
    float m_rippleIntensity = static_cast<float>(MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_INTENSITY);
    int m_rippleDurationMs = MOUSE_HIGHLIGHTER_DEFAULT_RIPPLE_DURATION_MS;

    bool m_leftButtonPressed = false;
    bool m_rightButtonPressed = false;
    // Pending hold-detection timers. A ripple "held indicator" is only spawned
    // once the button has been held past a short threshold; a quick click that
    // releases before then emits a single self-contained ripple instead. This
    // prevents a single click from rendering two ripples (press + release).
    UINT_PTR m_leftHoldTimer = 0;
    UINT_PTR m_rightHoldTimer = 0;
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
static const uint32_t HOLD_RIPPLE_TIMER_LEFT = 124;
static const uint32_t HOLD_RIPPLE_TIMER_RIGHT = 125;
// How long a ripple button must be held before the persistent "held indicator"
// is shown. Releasing before this is treated as a quick click (single ripple).
static const uint32_t HOLD_RIPPLE_THRESHOLD_MS = 180;
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
        m_leftGeometry = circleGeometry;

        // Niels-style press-down shrink: holding the button squeezes the
        // circle to 70% over 180ms after a 150ms delay so quick clicks skip
        // it. StartDrawingPointFading stops this animation on release.
        const float pressedRadius = m_radius * 0.70f;
        auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.2f, 0.0f }, { 0.4f, 1.0f });
        auto anim = m_compositor.CreateVector2KeyFrameAnimation();
        anim.InsertKeyFrame(0.0f, { m_radius, m_radius });
        anim.InsertKeyFrame(1.0f, { pressedRadius, pressedRadius }, ease);
        anim.Duration(std::chrono::milliseconds(180));
        anim.DelayTime(std::chrono::milliseconds(150));
        circleGeometry.StartAnimation(L"Radius", anim);
    }
    else if (button == MouseButton::Right)
    {
        circleShape.FillBrush(m_compositor.CreateColorBrush(m_rightClickColor));
        m_rightPointer = circleShape;
        m_rightGeometry = circleGeometry;

        const float pressedRadius = m_radius * 0.70f;
        auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.2f, 0.0f }, { 0.4f, 1.0f });
        auto anim = m_compositor.CreateVector2KeyFrameAnimation();
        anim.InsertKeyFrame(0.0f, { m_radius, m_radius });
        anim.InsertKeyFrame(1.0f, { pressedRadius, pressedRadius }, ease);
        anim.Duration(std::chrono::milliseconds(180));
        anim.DelayTime(std::chrono::milliseconds(150));
        circleGeometry.StartAnimation(L"Radius", anim);
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
        if (m_leftRippleGlow)
        {
            m_leftRippleGlow.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
        }
    }
    else if (button == MouseButton::Right)
    {
        m_rightPointer.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
        if (m_rightRippleGlow)
        {
            m_rightRippleGlow.Offset({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
        }
    }
    else
    {
        // always / spotlight idle
        if (m_spotlightMode)
        {
            if (m_spotlightPressed)
            {
                // Only update position while pressed — radius is being animated
                if (m_spotlightMaskGradient)
                {
                    m_spotlightMaskGradient.EllipseCenter({ static_cast<float>(pt.x), static_cast<float>(pt.y) });
                }
            }
            else
            {
                UpdateSpotlightMask(static_cast<float>(pt.x), static_cast<float>(pt.y), m_radius, true);
            }
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
    winrt::Windows::UI::Composition::CompositionEllipseGeometry geom{ nullptr };
    if (button == MouseButton::Left)
    {
        circleShape = m_leftPointer;
        geom = m_leftGeometry;
    }
    else
    {
        // right
        circleShape = m_rightPointer;
        geom = m_rightGeometry;
    }

    // Stop any in-flight press-down shrink so the geometry doesn't keep
    // animating while the fill is being faded out.
    if (geom && m_compositor)
    {
        geom.StopAnimation(L"Radius");
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
            if (instance->m_spotlightMode)
            {
                instance->SpotlightAnimatePress();
                break;
            }
            if (instance->m_rippleMode)
            {
                if (instance->m_leftPointerEnabled)
                {
                    // Defer the held indicator: only spawn it if the button is
                    // still down after the hold threshold. A quick click handled
                    // on button-up emits a single ripple instead.
                    instance->m_leftButtonPressed = true;
                    if (instance->m_leftHoldTimer == 0)
                    {
                        instance->m_leftHoldTimer = SetTimer(instance->m_hwnd, HOLD_RIPPLE_TIMER_LEFT, HOLD_RIPPLE_THRESHOLD_MS, nullptr);
                    }
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
            if (instance->m_spotlightMode)
            {
                instance->SpotlightAnimatePress();
                break;
            }
            if (instance->m_rippleMode)
            {
                if (instance->m_rightPointerEnabled)
                {
                    // Defer the held indicator (see WM_LBUTTONDOWN).
                    instance->m_rightButtonPressed = true;
                    if (instance->m_rightHoldTimer == 0)
                    {
                        instance->m_rightHoldTimer = SetTimer(instance->m_hwnd, HOLD_RIPPLE_TIMER_RIGHT, HOLD_RIPPLE_THRESHOLD_MS, nullptr);
                    }
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
                // Held ripple ring follows the cursor while a button is down,
                // gated by the "follow cursor while held" setting. When the
                // setting is off, the ring stays anchored at the click point.
                if (instance->m_rippleShowDragTrail)
                {
                    if (instance->m_leftButtonPressed && instance->m_leftPointer)
                    {
                        instance->UpdateDrawingPointPosition(MouseButton::Left);
                    }
                    if (instance->m_rightButtonPressed && instance->m_rightPointer)
                    {
                        instance->UpdateDrawingPointPosition(MouseButton::Right);
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
            if (instance->m_spotlightPressed)
            {
                instance->SpotlightAnimateRelease();
            }
            if (instance->m_leftButtonPressed)
            {
                if (instance->m_rippleMode)
                {
                    if (instance->m_leftHoldTimer != 0)
                    {
                        // Released before the hold threshold => quick click.
                        KillTimer(instance->m_hwnd, instance->m_leftHoldTimer);
                        instance->m_leftHoldTimer = 0;
                        instance->EmitSingleRipple(MouseButton::Left);
                    }
                    else
                    {
                        // Held indicator was already shown; expand + fade it.
                        instance->FadeRippleHoldDot(MouseButton::Left);
                    }
                }
                else
                {
                    instance->StartDrawingPointFading(MouseButton::Left);
                }
                instance->m_leftButtonPressed = false;
                if (!instance->m_rippleMode && instance->m_alwaysPointerEnabled && !instance->m_rightButtonPressed)
                {
                    // Add AlwaysPointer only when it's enabled and RightPointer is not active
                    instance->AddDrawingPoint(MouseButton::None);
                }
            }
            break;
        case WM_RBUTTONUP:
            if (instance->m_spotlightPressed)
            {
                instance->SpotlightAnimateRelease();
            }
            if (instance->m_rightButtonPressed)
            {
                if (instance->m_rippleMode)
                {
                    if (instance->m_rightHoldTimer != 0)
                    {
                        // Released before the hold threshold => quick click.
                        KillTimer(instance->m_hwnd, instance->m_rightHoldTimer);
                        instance->m_rightHoldTimer = 0;
                        instance->EmitSingleRipple(MouseButton::Right);
                    }
                    else
                    {
                        instance->FadeRippleHoldDot(MouseButton::Right);
                    }
                }
                else
                {
                    instance->StartDrawingPointFading(MouseButton::Right);
                }
                instance->m_rightButtonPressed = false;
                if (!instance->m_rippleMode && instance->m_alwaysPointerEnabled && !instance->m_leftButtonPressed)
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
    m_spotlightPressed = false;
    m_leftPointer = nullptr;
    m_rightPointer = nullptr;
    m_alwaysPointer = nullptr;
    m_leftGeometry = nullptr;
    m_rightGeometry = nullptr;
    m_leftRippleGlow = nullptr;
    m_rightRippleGlow = nullptr;
    m_leftGlowGeometry = nullptr;
    m_rightGlowGeometry = nullptr;
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

    // Reset transient pressed-state flag so a settings change while a button
    // happens to be down doesn't leave the spotlight stuck at a shrunken size.
    m_spotlightPressed = false;

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
        {
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
        case HOLD_RIPPLE_TIMER_LEFT:
            // Button held past the threshold: show the persistent held indicator.
            KillTimer(instance->m_hwnd, instance->m_leftHoldTimer);
            instance->m_leftHoldTimer = 0;
            if (instance->m_leftButtonPressed)
            {
                instance->SpawnRippleHoldDot(MouseButton::Left);
            }
            break;
        case HOLD_RIPPLE_TIMER_RIGHT:
            KillTimer(instance->m_hwnd, instance->m_rightHoldTimer);
            instance->m_rightHoldTimer = 0;
            if (instance->m_rightButtonPressed)
            {
                instance->SpawnRippleHoldDot(MouseButton::Right);
            }
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

// Spotlight press-down: shrink the mask radius briefly while a button is held.
void Highlighter::SpotlightAnimatePress()
{
    if (!m_spotlightMode || !m_spotlightMaskGradient)
    {
        return;
    }

    m_spotlightPressed = true;
    const float pressedRadius = m_radius * 0.85f;

    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.2f, 0.0f }, { 0.4f, 1.0f });
    auto anim = m_compositor.CreateVector2KeyFrameAnimation();
    anim.InsertKeyFrame(0.0f, { m_radius, m_radius });
    anim.InsertKeyFrame(1.0f, { pressedRadius, pressedRadius }, ease);
    anim.Duration(std::chrono::milliseconds(120));
    m_spotlightMaskGradient.StartAnimation(L"EllipseRadius", anim);
}

// Spotlight release: animate the mask back to the configured radius.
void Highlighter::SpotlightAnimateRelease()
{
    m_spotlightPressed = false;

    if (!m_spotlightMode || !m_spotlightMaskGradient)
    {
        return;
    }

    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.215f, 0.61f }, { 0.355f, 1.0f });
    auto current = m_spotlightMaskGradient.EllipseRadius();
    auto anim = m_compositor.CreateVector2KeyFrameAnimation();
    anim.InsertKeyFrame(0.0f, current);
    anim.InsertKeyFrame(1.0f, { m_radius, m_radius }, ease);
    anim.Duration(std::chrono::milliseconds(200));
    m_spotlightMaskGradient.StartAnimation(L"EllipseRadius", anim);
}

// Spawn the press/hold ring + glow at the click point. The shapes persist
// until FadeRippleHoldDot is called (button-up). While held they can be
// re-positioned to follow the cursor (UpdateDrawingPointPosition).
void Highlighter::SpawnRippleHoldDot(MouseButton button)
{
    if (!m_compositor || !m_shape)
    {
        return;
    }

    winrt::Windows::UI::Color color = (button == MouseButton::Left) ? m_leftClickColor : m_rightClickColor;
    if (color.A == 0)
    {
        return;
    }

    POINT pt{};
    if (!GetCursorPos(&pt))
    {
        return;
    }
    ScreenToClient(m_hwnd, &pt);
    const float fx = static_cast<float>(pt.x);
    const float fy = static_cast<float>(pt.y);

    // Resolve sizing/intensity from the ripple-specific settings so they're
    // independent of the legacy "always-on dot" controls.
    const float baseSize = (m_rippleSize > 1.0f) ? m_rippleSize : 1.0f;
    float intensity = m_rippleIntensity;
    if (intensity < 0.15f) intensity = 0.15f;
    if (intensity > 1.35f) intensity = 1.35f;

    const float ringHeld = baseSize * 0.55f;
    const float glowHeld = baseSize * 0.65f;
    const float lineWidth = (std::max)(2.25f, baseSize * (0.035f + intensity * 0.045f));

    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.215f, 0.61f }, { 0.355f, 1.0f });
    // Held indicator: appears once the button has been held past the hold
    // threshold and sits at the held radius until release. It must NOT expand
    // outward on appearance — it only FADES IN at the held size. The single
    // outward "ripple" expansion happens exclusively on release
    // (FadeRippleHoldDot). If this grew outward, a slow single click (release
    // shortly after the threshold) would show grow-to-held + release as two
    // expansions — the double-ripple bug.
    auto dur = std::chrono::milliseconds(120);

    auto clampByte = [](float v) -> uint8_t {
        if (v < 0.0f) v = 0.0f;
        if (v > 255.0f) v = 255.0f;
        return static_cast<uint8_t>(v);
    };

    // Glow color is the click color, lower alpha (×0.30), scaled by intensity.
    const float glowAlpha = static_cast<float>(color.A) * 0.30f * intensity;
    auto glowColor = winrt::Windows::UI::ColorHelper::FromArgb(clampByte(glowAlpha), color.R, color.G, color.B);
    auto glowTransparent = winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B);

    // Ring color uses full base alpha (alphaMul like the press recipe).
    const float alphaMul = 0.18f + intensity * 0.78f;
    auto ringColor = winrt::Windows::UI::ColorHelper::FromArgb(clampByte(static_cast<float>(color.A) * alphaMul), color.R, color.G, color.B);
    auto ringTransparent = winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B);

    // Clean up any stray "still held" shapes for this button — guards against
    // stray button-down without matching button-up (e.g. focus loss).
    winrt::CompositionSpriteShape& heldRing = (button == MouseButton::Left) ? m_leftPointer : m_rightPointer;
    winrt::CompositionSpriteShape& heldGlow = (button == MouseButton::Left) ? m_leftRippleGlow : m_rightRippleGlow;
    winrt::CompositionEllipseGeometry& heldGeom = (button == MouseButton::Left) ? m_leftGeometry : m_rightGeometry;
    winrt::CompositionEllipseGeometry& heldGlowGeom = (button == MouseButton::Left) ? m_leftGlowGeometry : m_rightGlowGeometry;

    if (m_shape && m_shape.Shapes())
    {
        auto shapes = m_shape.Shapes();
        uint32_t idx = 0;
        if (heldRing && shapes.IndexOf(heldRing, idx))
        {
            shapes.RemoveAt(idx);
        }
        if (heldGlow && shapes.IndexOf(heldGlow, idx))
        {
            shapes.RemoveAt(idx);
        }
    }

    // Glow (filled) — added first so the ring renders on top. Sits at the held
    // radius and fades its alpha in (no outward size growth).
    auto glowGeom = m_compositor.CreateEllipseGeometry();
    glowGeom.Radius({ glowHeld, glowHeld });
    auto glowBrush = m_compositor.CreateColorBrush(glowTransparent);
    auto glowShape = m_compositor.CreateSpriteShape(glowGeom);
    glowShape.Offset({ fx, fy });
    glowShape.FillBrush(glowBrush);
    m_shape.Shapes().Append(glowShape);

    auto glowFadeIn = m_compositor.CreateColorKeyFrameAnimation();
    glowFadeIn.InsertKeyFrame(0.0f, glowTransparent);
    glowFadeIn.InsertKeyFrame(1.0f, glowColor, ease);
    glowFadeIn.Duration(dur);
    glowBrush.StartAnimation(L"Color", glowFadeIn);

    // Ring (stroked) — same: fixed at held radius, alpha fade-in only.
    auto ringGeom = m_compositor.CreateEllipseGeometry();
    ringGeom.Radius({ ringHeld, ringHeld });
    auto ringBrush = m_compositor.CreateColorBrush(ringTransparent);
    auto ringShape = m_compositor.CreateSpriteShape(ringGeom);
    ringShape.Offset({ fx, fy });
    ringShape.StrokeBrush(ringBrush);
    ringShape.StrokeThickness(lineWidth);
    ringShape.IsStrokeNonScaling(true);
    m_shape.Shapes().Append(ringShape);

    auto ringFadeIn = m_compositor.CreateColorKeyFrameAnimation();
    ringFadeIn.InsertKeyFrame(0.0f, ringTransparent);
    ringFadeIn.InsertKeyFrame(1.0f, ringColor, ease);
    ringFadeIn.Duration(dur);
    ringBrush.StartAnimation(L"Color", ringFadeIn);

    heldRing = ringShape;
    heldGlow = glowShape;
    heldGeom = ringGeom;
    heldGlowGeom = glowGeom;
}

// Continue the held-ring/glow animation outward and fade both to transparent.
// For right-click, optionally spawn the expanding crosshair lines.
void Highlighter::FadeRippleHoldDot(MouseButton button)
{
    if (!m_compositor || !m_shape)
    {
        return;
    }

    winrt::CompositionSpriteShape& heldRing = (button == MouseButton::Left) ? m_leftPointer : m_rightPointer;
    winrt::CompositionSpriteShape& heldGlow = (button == MouseButton::Left) ? m_leftRippleGlow : m_rightRippleGlow;
    winrt::CompositionEllipseGeometry& heldGeom = (button == MouseButton::Left) ? m_leftGeometry : m_rightGeometry;
    winrt::CompositionEllipseGeometry& heldGlowGeom = (button == MouseButton::Left) ? m_leftGlowGeometry : m_rightGlowGeometry;

    if (!heldRing && !heldGlow)
    {
        return;
    }

    winrt::Windows::UI::Color color = (button == MouseButton::Left) ? m_leftClickColor : m_rightClickColor;

    const float baseSize = (m_rippleSize > 1.0f) ? m_rippleSize : 1.0f;
    float intensity = m_rippleIntensity;
    if (intensity < 0.15f) intensity = 0.15f;
    if (intensity > 1.35f) intensity = 1.35f;

    int durationMs = m_rippleDurationMs;
    if (durationMs < 60) durationMs = 60;
    if (durationMs > 2000) durationMs = 2000;
    auto dur = std::chrono::milliseconds(durationMs);

    const float ringHeld = baseSize * 0.55f;
    const float ringEnd = baseSize * 1.05f;
    const float glowHeld = baseSize * 0.65f;
    const float glowEnd = baseSize * 1.40f;

    auto clampByte = [](float v) -> uint8_t {
        if (v < 0.0f) v = 0.0f;
        if (v > 255.0f) v = 255.0f;
        return static_cast<uint8_t>(v);
    };

    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.215f, 0.61f }, { 0.355f, 1.0f });
    auto transparent = winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B);

    // Track everything spawned by this fade (and the held shapes themselves)
    // so the completion callback can remove them in one pass.
    auto spawned = std::make_shared<std::vector<winrt::CompositionSpriteShape>>();

    auto batch = m_compositor.CreateScopedBatch(winrt::CompositionBatchTypes::Animation);

    if (heldGlow && heldGlowGeom)
    {
        // The held indicator has settled at the held radius; expand it outward
        // from there and fade it to transparent.
        heldGlowGeom.StopAnimation(L"Radius");
        auto glowAnim = m_compositor.CreateVector2KeyFrameAnimation();
        glowAnim.InsertKeyFrame(0.0f, { glowHeld, glowHeld });
        glowAnim.InsertKeyFrame(1.0f, { glowEnd, glowEnd }, ease);
        glowAnim.Duration(dur);
        heldGlowGeom.StartAnimation(L"Radius", glowAnim);

        auto brush = heldGlow.FillBrush().as<winrt::Windows::UI::Composition::CompositionColorBrush>();
        auto startColor = brush.Color();
        auto colorAnim = m_compositor.CreateColorKeyFrameAnimation();
        colorAnim.InsertKeyFrame(0.0f, startColor);
        colorAnim.InsertKeyFrame(1.0f, transparent, ease);
        colorAnim.Duration(dur);
        brush.StartAnimation(L"Color", colorAnim);

        spawned->push_back(heldGlow);
    }

    if (heldRing && heldGeom)
    {
        heldGeom.StopAnimation(L"Radius");
        auto ringAnim = m_compositor.CreateVector2KeyFrameAnimation();
        ringAnim.InsertKeyFrame(0.0f, { ringHeld, ringHeld });
        ringAnim.InsertKeyFrame(1.0f, { ringEnd, ringEnd }, ease);
        ringAnim.Duration(dur);
        heldGeom.StartAnimation(L"Radius", ringAnim);

        auto brush = heldRing.StrokeBrush().as<winrt::Windows::UI::Composition::CompositionColorBrush>();
        auto startColor = brush.Color();
        auto colorAnim = m_compositor.CreateColorKeyFrameAnimation();
        colorAnim.InsertKeyFrame(0.0f, startColor);
        colorAnim.InsertKeyFrame(1.0f, transparent, ease);
        colorAnim.Duration(dur);
        brush.StartAnimation(L"Color", colorAnim);

        spawned->push_back(heldRing);
    }

    // Right-click only: spawn expanding crosshair lines centered on the ring.
    // Gated by the "show crosshairs on right-click release" toggle.
    if (button == MouseButton::Right && m_rippleShowReleasePulse && heldRing)
    {
        const float xhairAlphaMul = 0.18f + intensity * 0.78f;
        auto xhairColor = winrt::Windows::UI::ColorHelper::FromArgb(clampByte(static_cast<float>(color.A) * xhairAlphaMul), color.R, color.G, color.B);
        const float xhairThickness = (std::max)(1.25f, baseSize * (0.025f + intensity * 0.03f));

        auto center = heldRing.Offset();
        const float startSpan = ringHeld * 0.85f;
        const float endSpan = ringEnd * 0.85f;

        auto makeLine = [&](float ax1, float ay1, float ax2, float ay2,
                            float bx1, float by1, float bx2, float by2) {
            auto lineGeom = m_compositor.CreateLineGeometry();
            lineGeom.Start({ ax1, ay1 });
            lineGeom.End({ ax2, ay2 });

            auto lineBrush = m_compositor.CreateColorBrush(xhairColor);
            auto lineShape = m_compositor.CreateSpriteShape(lineGeom);
            lineShape.StrokeBrush(lineBrush);
            lineShape.StrokeThickness(xhairThickness);
            lineShape.IsStrokeNonScaling(true);
            m_shape.Shapes().Append(lineShape);
            spawned->push_back(lineShape);

            auto startAnim = m_compositor.CreateVector2KeyFrameAnimation();
            startAnim.InsertKeyFrame(0.0f, { ax1, ay1 });
            startAnim.InsertKeyFrame(1.0f, { bx1, by1 }, ease);
            startAnim.Duration(dur);
            lineGeom.StartAnimation(L"Start", startAnim);

            auto endAnim = m_compositor.CreateVector2KeyFrameAnimation();
            endAnim.InsertKeyFrame(0.0f, { ax2, ay2 });
            endAnim.InsertKeyFrame(1.0f, { bx2, by2 }, ease);
            endAnim.Duration(dur);
            lineGeom.StartAnimation(L"End", endAnim);

            auto colorAnim = m_compositor.CreateColorKeyFrameAnimation();
            colorAnim.InsertKeyFrame(0.0f, xhairColor);
            colorAnim.InsertKeyFrame(1.0f, transparent, ease);
            colorAnim.Duration(dur);
            lineBrush.StartAnimation(L"Color", colorAnim);
        };

        // Horizontal line (left half, right half).
        makeLine(center.x - startSpan, center.y, center.x - startSpan * 0.30f, center.y,
                 center.x - endSpan,   center.y, center.x - endSpan   * 0.30f, center.y);
        makeLine(center.x + startSpan * 0.30f, center.y, center.x + startSpan, center.y,
                 center.x + endSpan   * 0.30f, center.y, center.x + endSpan,   center.y);
        // Vertical line (top half, bottom half).
        makeLine(center.x, center.y - startSpan, center.x, center.y - startSpan * 0.30f,
                 center.x, center.y - endSpan,   center.x, center.y - endSpan   * 0.30f);
        makeLine(center.x, center.y + startSpan * 0.30f, center.x, center.y + startSpan,
                 center.x, center.y + endSpan   * 0.30f, center.x, center.y + endSpan);
    }

    // Detach our member handles BEFORE the batch completes so subsequent
    // press events on this button create fresh shapes rather than racing.
    heldRing = nullptr;
    heldGlow = nullptr;
    heldGeom = nullptr;
    heldGlowGeom = nullptr;

    batch.End();

    if (spawned->empty())
    {
        return;
    }

    auto dispatcher = m_dispatcherQueueController.DispatcherQueue();
    batch.Completed([dispatcher, spawned](auto&&, auto&&) {
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

// Self-contained single ripple for a quick click (press + release before the
// hold threshold). Spawns a fresh ring + glow that grow from the click point
// outward and fade to transparent in one continuous animation — no held
// indicator, so a single click produces exactly one ripple. For right-click,
// optionally spawns the expanding crosshair lines too.
void Highlighter::EmitSingleRipple(MouseButton button)
{
    if (!m_compositor || !m_shape)
    {
        return;
    }

    winrt::Windows::UI::Color color = (button == MouseButton::Left) ? m_leftClickColor : m_rightClickColor;
    if (color.A == 0)
    {
        return;
    }

    POINT pt{};
    if (!GetCursorPos(&pt))
    {
        return;
    }
    ScreenToClient(m_hwnd, &pt);
    const float fx = static_cast<float>(pt.x);
    const float fy = static_cast<float>(pt.y);

    const float baseSize = (m_rippleSize > 1.0f) ? m_rippleSize : 1.0f;
    float intensity = m_rippleIntensity;
    if (intensity < 0.15f) intensity = 0.15f;
    if (intensity > 1.35f) intensity = 1.35f;

    int durationMs = m_rippleDurationMs;
    if (durationMs < 60) durationMs = 60;
    if (durationMs > 2000) durationMs = 2000;
    auto dur = std::chrono::milliseconds(durationMs);

    const float ringStart = baseSize * 0.20f;
    const float ringEnd = baseSize * 1.05f;
    const float glowStart = baseSize * 0.30f;
    const float glowEnd = baseSize * 1.40f;
    const float lineWidth = (std::max)(2.25f, baseSize * (0.035f + intensity * 0.045f));

    auto clampByte = [](float v) -> uint8_t {
        if (v < 0.0f) v = 0.0f;
        if (v > 255.0f) v = 255.0f;
        return static_cast<uint8_t>(v);
    };

    const float glowAlpha = static_cast<float>(color.A) * 0.30f * intensity;
    auto glowColor = winrt::Windows::UI::ColorHelper::FromArgb(clampByte(glowAlpha), color.R, color.G, color.B);
    const float alphaMul = 0.18f + intensity * 0.78f;
    auto ringColor = winrt::Windows::UI::ColorHelper::FromArgb(clampByte(static_cast<float>(color.A) * alphaMul), color.R, color.G, color.B);

    auto ease = m_compositor.CreateCubicBezierEasingFunction({ 0.215f, 0.61f }, { 0.355f, 1.0f });
    auto transparent = winrt::Windows::UI::ColorHelper::FromArgb(0, color.R, color.G, color.B);

    auto spawned = std::make_shared<std::vector<winrt::CompositionSpriteShape>>();

    auto batch = m_compositor.CreateScopedBatch(winrt::CompositionBatchTypes::Animation);

    // Glow (filled) — added first so the ring renders on top.
    auto glowGeom = m_compositor.CreateEllipseGeometry();
    glowGeom.Radius({ glowStart, glowStart });
    auto glowBrush = m_compositor.CreateColorBrush(glowColor);
    auto glowShape = m_compositor.CreateSpriteShape(glowGeom);
    glowShape.Offset({ fx, fy });
    glowShape.FillBrush(glowBrush);
    m_shape.Shapes().Append(glowShape);
    spawned->push_back(glowShape);

    auto glowAnim = m_compositor.CreateVector2KeyFrameAnimation();
    glowAnim.InsertKeyFrame(0.0f, { glowStart, glowStart });
    glowAnim.InsertKeyFrame(1.0f, { glowEnd, glowEnd }, ease);
    glowAnim.Duration(dur);
    glowGeom.StartAnimation(L"Radius", glowAnim);

    auto glowColorAnim = m_compositor.CreateColorKeyFrameAnimation();
    glowColorAnim.InsertKeyFrame(0.0f, glowColor);
    glowColorAnim.InsertKeyFrame(1.0f, transparent, ease);
    glowColorAnim.Duration(dur);
    glowBrush.StartAnimation(L"Color", glowColorAnim);

    // Ring (stroked).
    auto ringGeom = m_compositor.CreateEllipseGeometry();
    ringGeom.Radius({ ringStart, ringStart });
    auto ringBrush = m_compositor.CreateColorBrush(ringColor);
    auto ringShape = m_compositor.CreateSpriteShape(ringGeom);
    ringShape.Offset({ fx, fy });
    ringShape.StrokeBrush(ringBrush);
    ringShape.StrokeThickness(lineWidth);
    ringShape.IsStrokeNonScaling(true);
    m_shape.Shapes().Append(ringShape);
    spawned->push_back(ringShape);

    auto ringAnim = m_compositor.CreateVector2KeyFrameAnimation();
    ringAnim.InsertKeyFrame(0.0f, { ringStart, ringStart });
    ringAnim.InsertKeyFrame(1.0f, { ringEnd, ringEnd }, ease);
    ringAnim.Duration(dur);
    ringGeom.StartAnimation(L"Radius", ringAnim);

    auto ringColorAnim = m_compositor.CreateColorKeyFrameAnimation();
    ringColorAnim.InsertKeyFrame(0.0f, ringColor);
    ringColorAnim.InsertKeyFrame(1.0f, transparent, ease);
    ringColorAnim.Duration(dur);
    ringBrush.StartAnimation(L"Color", ringColorAnim);

    // Right-click only: spawn expanding crosshair lines centered on the click
    // point. Gated by the "show crosshairs on right-click release" toggle.
    if (button == MouseButton::Right && m_rippleShowReleasePulse)
    {
        auto xhairColor = ringColor;
        const float xhairThickness = (std::max)(1.25f, baseSize * (0.025f + intensity * 0.03f));

        const float startSpan = (baseSize * 0.55f) * 0.85f;
        const float endSpan = ringEnd * 0.85f;

        auto makeLine = [&](float ax1, float ay1, float ax2, float ay2,
                            float bx1, float by1, float bx2, float by2) {
            auto lineGeom = m_compositor.CreateLineGeometry();
            lineGeom.Start({ ax1, ay1 });
            lineGeom.End({ ax2, ay2 });

            auto lineBrush = m_compositor.CreateColorBrush(xhairColor);
            auto lineShape = m_compositor.CreateSpriteShape(lineGeom);
            lineShape.StrokeBrush(lineBrush);
            lineShape.StrokeThickness(xhairThickness);
            lineShape.IsStrokeNonScaling(true);
            m_shape.Shapes().Append(lineShape);
            spawned->push_back(lineShape);

            auto startAnim = m_compositor.CreateVector2KeyFrameAnimation();
            startAnim.InsertKeyFrame(0.0f, { ax1, ay1 });
            startAnim.InsertKeyFrame(1.0f, { bx1, by1 }, ease);
            startAnim.Duration(dur);
            lineGeom.StartAnimation(L"Start", startAnim);

            auto endAnim = m_compositor.CreateVector2KeyFrameAnimation();
            endAnim.InsertKeyFrame(0.0f, { ax2, ay2 });
            endAnim.InsertKeyFrame(1.0f, { bx2, by2 }, ease);
            endAnim.Duration(dur);
            lineGeom.StartAnimation(L"End", endAnim);

            auto colorAnim = m_compositor.CreateColorKeyFrameAnimation();
            colorAnim.InsertKeyFrame(0.0f, xhairColor);
            colorAnim.InsertKeyFrame(1.0f, transparent, ease);
            colorAnim.Duration(dur);
            lineBrush.StartAnimation(L"Color", colorAnim);
        };

        // Horizontal line (left half, right half).
        makeLine(fx - startSpan, fy, fx - startSpan * 0.30f, fy,
                 fx - endSpan,   fy, fx - endSpan   * 0.30f, fy);
        makeLine(fx + startSpan * 0.30f, fy, fx + startSpan, fy,
                 fx + endSpan   * 0.30f, fy, fx + endSpan,   fy);
        // Vertical line (top half, bottom half).
        makeLine(fx, fy - startSpan, fx, fy - startSpan * 0.30f,
                 fx, fy - endSpan,   fx, fy - endSpan   * 0.30f);
        makeLine(fx, fy + startSpan * 0.30f, fx, fy + startSpan,
                 fx, fy + endSpan   * 0.30f, fx, fy + endSpan);
    }

    batch.End();

    auto dispatcher = m_dispatcherQueueController.DispatcherQueue();
    batch.Completed([dispatcher, spawned](auto&&, auto&&) {
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
