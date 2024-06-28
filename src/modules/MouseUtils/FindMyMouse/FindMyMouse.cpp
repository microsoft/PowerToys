// FindMyMouse.cpp : Based on Raymond Chen's SuperSonar.cpp
//
#include "pch.h"
#include "FindMyMouse.h"
#include "WinHookEventIDs.h"
#include "trace.h"
#include "common/utils/game_mode.h"
#include "common/utils/process_path.h"
#include "common/utils/excluded_apps.h"
#include "common/utils/MsWindowsSettings.h"
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

#pragma region Super_Sonar_Base_Code

template<typename D>
struct SuperSonar
{
    bool Initialize(HINSTANCE hinst);
    void Terminate();

protected:
    // You are expected to override these, as appropriate.

    DWORD GetExtendedStyle()
    {
        return 0;
    }

    LRESULT WndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        return BaseWndProc(message, wParam, lParam);
    }

    void BeforeMoveSonar() {}
    void AfterMoveSonar() {}
    void SetSonarVisibility(bool visible) = delete;
    void UpdateMouseSnooping();
    bool IsForegroundAppExcluded();

protected:
    // Base class members you can access.
    D* Shim() { return static_cast<D*>(this); }
    LRESULT BaseWndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    HWND m_hwnd{};
    POINT m_sonarPos = ptNowhere;

    // Only consider double left control click if at least 100ms passed between the clicks, to avoid keyboards that might be sending rapid clicks.
    // At actual check, time a fifth of the current double click setting might be used instead to take into account users who might have low values.
    static const int MIN_DOUBLE_CLICK_TIME = 100;

    bool m_destroyed = false;
    FindMyMouseActivationMethod m_activationMethod = FIND_MY_MOUSE_DEFAULT_ACTIVATION_METHOD;
    bool m_includeWinKey = FIND_MY_MOUSE_DEFAULT_INCLUDE_WIN_KEY;
    bool m_doNotActivateOnGameMode = FIND_MY_MOUSE_DEFAULT_DO_NOT_ACTIVATE_ON_GAME_MODE;
    int m_sonarRadius = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_RADIUS;
    int m_sonarZoomFactor = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_INITIAL_ZOOM;
    DWORD m_fadeDuration = FIND_MY_MOUSE_DEFAULT_ANIMATION_DURATION_MS;
    int m_finalAlphaNumerator = FIND_MY_MOUSE_DEFAULT_OVERLAY_OPACITY;
    std::vector<std::wstring> m_excludedApps;
    int m_shakeMinimumDistance = FIND_MY_MOUSE_DEFAULT_SHAKE_MINIMUM_DISTANCE;
    static constexpr int FinalAlphaDenominator = 100;
    winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };

    // Don't consider movements started past these milliseconds to detect shaking.
    int m_shakeIntervalMs = FIND_MY_MOUSE_DEFAULT_SHAKE_INTERVAL_MS;
    // By which factor must travelled distance be than the diagonal of the rectangle containing the movements. (value in percent)
    int m_shakeFactor = FIND_MY_MOUSE_DEFAULT_SHAKE_FACTOR;

private:

    // Save the mouse movement that occurred in any direction.
    struct PointerRecentMovement
    {
        POINT diff;
        ULONGLONG tick;
    };
    std::vector<PointerRecentMovement> m_movementHistory;
    // Raw Input may give relative or absolute values. Need to take each case into account.
    bool m_seenAnAbsoluteMousePosition = false;
    POINT m_lastAbsolutePosition = { 0, 0 };

    static inline byte GetSign(LONG const& num)
    {
        if (num > 0)
            return 1;
        if (num < 0)
            return -1;
        return 0;
    }

    static bool IsEqual(POINT const& p1, POINT const& p2)
    {
        return p1.x == p2.x && p1.y == p2.y;
    }

    static constexpr POINT ptNowhere = { LONG_MIN, LONG_MIN };
    static constexpr DWORD TIMER_ID_TRACK = 100;
    static constexpr DWORD IdlePeriod = 1000;

    // Activate sonar: Hit LeftControl twice.
    enum class SonarState
    {
        Idle,
        ControlDown1,
        ControlUp1,
        ControlDown2,
        ControlUp2,
    };

    HWND m_hwndOwner{};
    SonarState m_sonarState = SonarState::Idle;
    POINT m_lastKeyPos{};
    ULONGLONG m_lastKeyTime{};

    static constexpr DWORD NoSonar = 0;
    static constexpr DWORD SonarWaitingForMouseMove = 1;
    ULONGLONG m_sonarStart = NoSonar;
    bool m_isSnoopingMouse = false;

private:
    static constexpr auto className = L"FindMyMouse";

    static constexpr auto windowTitle = L"PowerToys Find My Mouse";

    static LRESULT CALLBACK s_WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

    BOOL OnSonarCreate();
    void OnSonarDestroy();
    void OnSonarInput(WPARAM flags, HRAWINPUT hInput);
    void OnSonarKeyboardInput(RAWINPUT const& input);
    void OnSonarMouseInput(RAWINPUT const& input);
    void OnMouseTimer();

    void DetectShake();
    bool KeyboardInputCanActivate();

    void StartSonar();
    void StopSonar();
};

template<typename D>
bool SuperSonar<D>::Initialize(HINSTANCE hinst)
{
    SetThreadDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    WNDCLASS wc{};

    if (!GetClassInfoW(hinst, className, &wc))
    {
        wc.lpfnWndProc = s_WndProc;
        wc.hInstance = hinst;
        wc.hIcon = LoadIcon(hinst, IDI_APPLICATION);
        wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
        wc.hbrBackground = static_cast<HBRUSH>(GetStockObject(NULL_BRUSH));
        wc.lpszClassName = className;

        if (!RegisterClassW(&wc))
        {
            return false;
        }
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | Shim()->GetExtendedStyle();
    return CreateWindowExW(exStyle, className, windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hinst, this) != nullptr;
}

template<typename D>
void SuperSonar<D>::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
        m_destroyed = true;
        DestroyWindow(m_hwndOwner);
    });
    if (!enqueueSucceeded)
    {
        Logger::error("Couldn't enqueue message to destroy the sonar Window.");
    }
}

template<typename D>
LRESULT SuperSonar<D>::s_WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    SuperSonar* self;
    if (message == WM_NCCREATE)
    {
        auto info = reinterpret_cast<LPCREATESTRUCT>(lParam);
        SetWindowLongPtr(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(info->lpCreateParams));
        self = static_cast<SuperSonar*>(info->lpCreateParams);
        self->m_hwnd = hwnd;
    }
    else
    {
        self = reinterpret_cast<SuperSonar*>(GetWindowLongPtr(hwnd, GWLP_USERDATA));
    }
    if (self)
    {
        return self->Shim()->WndProc(message, wParam, lParam);
    }
    else
    {
        return DefWindowProc(hwnd, message, wParam, lParam);
    }
}

template<typename D>
LRESULT SuperSonar<D>::BaseWndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept
{
    switch (message)
    {
    case WM_CREATE:
        if(!OnSonarCreate()) return -1;
        UpdateMouseSnooping();
        return 0;

    case WM_DESTROY:
        OnSonarDestroy();
        break;

    case WM_INPUT:
        OnSonarInput(wParam, reinterpret_cast<HRAWINPUT>(lParam));
        break;

    case WM_TIMER:
        switch (wParam)
        {
        case TIMER_ID_TRACK:
            OnMouseTimer();
            break;
        }
        break;

    case WM_NCHITTEST:
        return HTTRANSPARENT;
    }

    if (message == WM_PRIV_SHORTCUT)
    {
        if (m_sonarStart == NoSonar)
        {
            StartSonar();
        }
        else
        {
            StopSonar();
        }
    }

    return DefWindowProc(m_hwnd, message, wParam, lParam);
}

template<typename D>
BOOL SuperSonar<D>::OnSonarCreate()
{
    RAWINPUTDEVICE keyboard{};
    keyboard.usUsagePage = HID_USAGE_PAGE_GENERIC;
    keyboard.usUsage = HID_USAGE_GENERIC_KEYBOARD;
    keyboard.dwFlags = RIDEV_INPUTSINK;
    keyboard.hwndTarget = m_hwnd;
    return RegisterRawInputDevices(&keyboard, 1, sizeof(keyboard));
}

template<typename D>
void SuperSonar<D>::OnSonarDestroy()
{
    PostQuitMessage(0);
}

template<typename D>
void SuperSonar<D>::OnSonarInput(WPARAM flags, HRAWINPUT hInput)
{
    RAWINPUT input;
    UINT size = sizeof(input);
    auto result = GetRawInputData(hInput, RID_INPUT, &input, &size, sizeof(RAWINPUTHEADER));
    if (result < sizeof(RAWINPUTHEADER))
    {
        return;
    }

    switch (input.header.dwType)
    {
    case RIM_TYPEKEYBOARD:
        OnSonarKeyboardInput(input);
        break;
    case RIM_TYPEMOUSE:
        OnSonarMouseInput(input);
        break;
    }
}

template<typename D>
void SuperSonar<D>::OnSonarKeyboardInput(RAWINPUT const& input)
{
    // Don't stop the sonar when the shortcut is released
    if (m_activationMethod == FindMyMouseActivationMethod::Shortcut && (input.data.keyboard.Flags & RI_KEY_BREAK) != 0)
    {
        return;
    }

    if ((m_activationMethod != FindMyMouseActivationMethod::DoubleRightControlKey && m_activationMethod != FindMyMouseActivationMethod::DoubleLeftControlKey)
        || input.data.keyboard.VKey != VK_CONTROL)
    {
        StopSonar();
        return;
    }

    bool pressed = (input.data.keyboard.Flags & RI_KEY_BREAK) == 0;

    bool leftCtrlPressed = (input.data.keyboard.Flags & RI_KEY_E0) == 0;
    bool rightCtrlPressed = (input.data.keyboard.Flags & RI_KEY_E0) != 0;

    if ((m_activationMethod == FindMyMouseActivationMethod::DoubleRightControlKey && !rightCtrlPressed)
        || (m_activationMethod == FindMyMouseActivationMethod::DoubleLeftControlKey && !leftCtrlPressed))
    {
        StopSonar();
        return;
    }

    switch (m_sonarState)
    {
    case SonarState::Idle:
        if (pressed)
        {
            m_sonarState = SonarState::ControlDown1;
            m_lastKeyTime = GetTickCount64();
            m_lastKeyPos = {};
            GetCursorPos(&m_lastKeyPos);
            UpdateMouseSnooping();
        }
        break;

    case SonarState::ControlDown1:
        if (!pressed)
        {
            m_sonarState = SonarState::ControlUp1;
        }
        break;

    case SonarState::ControlUp1:
        if (pressed && KeyboardInputCanActivate())
        {
            auto now = GetTickCount64();
            auto doubleClickInterval = now - m_lastKeyTime;
            POINT ptCursor{};
            auto doubleClickTimeSetting = GetDoubleClickTime();
            if (GetCursorPos(&ptCursor) &&
                doubleClickInterval >= min(MIN_DOUBLE_CLICK_TIME, doubleClickTimeSetting / 5) &&
                doubleClickInterval <= doubleClickTimeSetting &&
                IsEqual(m_lastKeyPos, ptCursor))
            {
                m_sonarState = SonarState::ControlDown2;
                StartSonar();
            }
            else
            {
                m_sonarState = SonarState::ControlDown1;
                m_lastKeyTime = GetTickCount64();
                m_lastKeyPos = {};
                GetCursorPos(&m_lastKeyPos);
                UpdateMouseSnooping();
            }
            Logger::info("Detecting double left control click with {} ms interval.", doubleClickInterval);
            m_lastKeyTime = now;
            m_lastKeyPos = ptCursor;
        }
        break;
    case SonarState::ControlUp2:
        // Also deactivate sonar with left control.
        if (pressed)
        {
            StopSonar();
        }
        break;
    case SonarState::ControlDown2:
        if (!pressed)
        {
            m_sonarState = SonarState::ControlUp2;
        }
        break;
    }
}

// Shaking detection algorithm is: Has distance travelled been much greater than the diagonal of the rectangle containing the movement?
template<typename D>
void SuperSonar<D>::DetectShake()
{
    ULONGLONG shakeStartTick = GetTickCount64() - m_shakeIntervalMs;
    
    // Prune the story of movements for those movements that started too long ago.
    std::erase_if(m_movementHistory, [shakeStartTick](const PointerRecentMovement& movement) { return movement.tick < shakeStartTick; });
    
    
    double distanceTravelled = 0;
    LONGLONG currentX=0, minX=0, maxX=0;
    LONGLONG currentY=0, minY=0, maxY=0;

    for (const PointerRecentMovement& movement : m_movementHistory)
    {
        currentX += movement.diff.x;
        currentY += movement.diff.y;
        distanceTravelled += sqrt(static_cast<double>(movement.diff.x) * movement.diff.x + static_cast<double>(movement.diff.y) * movement.diff.y); // Pythagorean theorem
        minX = min(currentX, minX);
        maxX = max(currentX, maxX);
        minY = min(currentY, minY);
        maxY = max(currentY, maxY);
    }
    
    if (distanceTravelled < m_shakeMinimumDistance)
    {
        return;
    }

    // Size of the rectangle the pointer moved in.
    double rectangleWidth =  static_cast<double>(maxX) - minX;
    double rectangleHeight =  static_cast<double>(maxY) - minY;

    double diagonal = sqrt(rectangleWidth * rectangleWidth + rectangleHeight * rectangleHeight);
    if (diagonal > 0 && distanceTravelled / diagonal > (m_shakeFactor/100.f))
    {
        m_movementHistory.clear();
        StartSonar();
    }

}

template<typename D>
bool SuperSonar<D>::KeyboardInputCanActivate()
{
    return !m_includeWinKey || (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000);
}

template<typename D>
void SuperSonar<D>::OnSonarMouseInput(RAWINPUT const& input)
{
    if (m_activationMethod == FindMyMouseActivationMethod::ShakeMouse)
    {
        LONG relativeX = 0;
        LONG relativeY = 0;
        if ((input.data.mouse.usFlags & MOUSE_MOVE_ABSOLUTE) == MOUSE_MOVE_ABSOLUTE && (input.data.mouse.lLastX!=0 || input.data.mouse.lLastY!=0))
        {
            // Getting absolute mouse coordinates. Likely inside a VM / RDP session.
            if (m_seenAnAbsoluteMousePosition)
            {
                relativeX = input.data.mouse.lLastX - m_lastAbsolutePosition.x;
                relativeY = input.data.mouse.lLastY - m_lastAbsolutePosition.y;
                m_lastAbsolutePosition.x = input.data.mouse.lLastX;
                m_lastAbsolutePosition.y = input.data.mouse.lLastY;
            }
            m_seenAnAbsoluteMousePosition = true;
        }
        else
        {
            relativeX = input.data.mouse.lLastX;
            relativeY = input.data.mouse.lLastY;
        }
        if (m_movementHistory.size() > 0)
        {
            PointerRecentMovement& lastMovement = m_movementHistory.back();
            // If the pointer is still moving in the same direction, just add to that movement instead of adding a new movement.
            // This helps in keeping the list of movements smaller even in cases where a high number of messages is sent.
            if (GetSign(lastMovement.diff.x) == GetSign(relativeX) && GetSign(lastMovement.diff.y) == GetSign(relativeY))
            {
                lastMovement.diff.x += relativeX;
                lastMovement.diff.y += relativeY;
            }
            else
            {
                m_movementHistory.push_back({ .diff = { .x=relativeX, .y=relativeY }, .tick = GetTickCount64() });
                // Mouse movement changed directions. Take the opportunity do detect shake.
                DetectShake();
            }
        }
        else
        {
            m_movementHistory.push_back({ .diff = { .x = relativeX, .y = relativeY }, .tick = GetTickCount64() });
        }
    
    }

    if (input.data.mouse.usButtonFlags)
    {
        StopSonar();
    }
    else if (m_sonarStart != NoSonar)
    {
        OnMouseTimer();
    }
}

template<typename D>
void SuperSonar<D>::StartSonar()
{
    // Don't activate if game mode is on.
    if (m_doNotActivateOnGameMode && detect_game_mode())
    {
        return;
    }

    if (IsForegroundAppExcluded())
    {
        return;
    }

    Logger::info("Focusing the sonar on the mouse cursor.");
    Trace::MousePointerFocused();
    // Cover the entire virtual screen.
    // HACK: Draw with 1 pixel off. Otherwise Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);
    m_sonarPos = ptNowhere;
    OnMouseTimer();
    UpdateMouseSnooping();
    Shim()->SetSonarVisibility(true);
}

template<typename D>
void SuperSonar<D>::StopSonar()
{
    if (m_sonarStart != NoSonar)
    {
        m_sonarStart = NoSonar;
        Shim()->SetSonarVisibility(false);
        KillTimer(m_hwnd, TIMER_ID_TRACK);
    }
    m_sonarState = SonarState::Idle;
    UpdateMouseSnooping();
}

template<typename D>
void SuperSonar<D>::OnMouseTimer()
{
    auto now = GetTickCount64();

    // If mouse has moved, then reset the sonar timer.
    POINT ptCursor{};
    if (!GetCursorPos(&ptCursor))
    {
        // We are no longer the active desktop - done.
        StopSonar();
        return;
    }
    ScreenToClient(m_hwnd, &ptCursor);

    if (IsEqual(m_sonarPos, ptCursor))
    {
        // Mouse is stationary.
        if (m_sonarStart != SonarWaitingForMouseMove && now - m_sonarStart >= IdlePeriod)
        {
            StopSonar();
            return;
        }
    }
    else
    {
        // Mouse has moved.
        if (IsEqual(m_sonarPos, ptNowhere))
        {
            // Initial call, mark sonar as active but waiting for first mouse-move.
            now = SonarWaitingForMouseMove;
        }
        SetTimer(m_hwnd, TIMER_ID_TRACK, IdlePeriod, nullptr);
        Shim()->BeforeMoveSonar();
        m_sonarPos = ptCursor;
        m_sonarStart = now;
        Shim()->AfterMoveSonar();
    }
}

template<typename D>
void SuperSonar<D>::UpdateMouseSnooping()
{
    bool wantSnoopingMouse = m_sonarStart != NoSonar || m_sonarState != SonarState::Idle || m_activationMethod == FindMyMouseActivationMethod::ShakeMouse;
    if (m_isSnoopingMouse != wantSnoopingMouse)
    {
        m_isSnoopingMouse = wantSnoopingMouse;
        RAWINPUTDEVICE mouse{};
        mouse.usUsagePage = HID_USAGE_PAGE_GENERIC;
        mouse.usUsage = HID_USAGE_GENERIC_MOUSE;
        if (wantSnoopingMouse)
        {
            mouse.dwFlags = RIDEV_INPUTSINK;
            mouse.hwndTarget = m_hwnd;
        }
        else
        {
            mouse.dwFlags = RIDEV_REMOVE;
            mouse.hwndTarget = nullptr;
        }
        RegisterRawInputDevices(&mouse, 1, sizeof(mouse));
    }
}

template<typename D>
bool SuperSonar<D>::IsForegroundAppExcluded()
{
    if (m_excludedApps.size() < 1)
    {
        return false;
    }
    if (HWND foregroundApp{ GetForegroundWindow() })
    {
        auto processPath = get_process_path(foregroundApp);
        CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

        return check_excluded_app(foregroundApp, processPath, m_excludedApps);
    }
    else
    {
        return false;
    }
}

struct CompositionSpotlight : SuperSonar<CompositionSpotlight>
{
    static constexpr UINT WM_OPACITY_ANIMATION_COMPLETED = WM_APP;
    float m_sonarRadiusFloat = static_cast<float>(m_sonarRadius);

    DWORD GetExtendedStyle()
    {
        return WS_EX_NOREDIRECTIONBITMAP;
    }

    void AfterMoveSonar()
    {
        m_spotlight.Offset({ static_cast<float>(m_sonarPos.x), static_cast<float>(m_sonarPos.y), 0.0f });
    }

    LRESULT WndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        switch (message)
        {
        case WM_CREATE:
            return OnCompositionCreate() && BaseWndProc(message, wParam, lParam);

        case WM_OPACITY_ANIMATION_COMPLETED:
            OnOpacityAnimationCompleted();
            break;
        }
        return BaseWndProc(message, wParam, lParam);
    }

    void SetSonarVisibility(bool visible)
    {
        m_batch = m_compositor.GetCommitBatch(winrt::CompositionBatchTypes::Animation);
        BOOL isEnabledAnimations = GetAnimationsEnabled();
        m_animation.Duration(std::chrono::milliseconds{ isEnabledAnimations ? m_fadeDuration : 1 });
        m_batch.Completed([hwnd = m_hwnd](auto&&, auto&&) {
            PostMessage(hwnd, WM_OPACITY_ANIMATION_COMPLETED, 0, 0);
        });
        m_root.Opacity(visible ? static_cast<float>(m_finalAlphaNumerator) / FinalAlphaDenominator : 0.0f);
        if (visible)
        {
            ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
        }
    }

    HWND GetHwnd() noexcept
    {
        return m_hwnd;
    }

private:
    bool OnCompositionCreate()
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

        // Our composition tree:
        //
        // [root] ContainerVisual
        // \ LayerVisual
        //   \[gray backdrop]
        //    [spotlight]
        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f }); // fill the parent
        m_root.Opacity(0.0f);
        m_target.Root(m_root);

        auto layer = m_compositor.CreateLayerVisual();
        layer.RelativeSizeAdjustment({ 1.0f, 1.0f }); // fill the parent
        m_root.Children().InsertAtTop(layer);

        m_backdrop = m_compositor.CreateSpriteVisual();
        m_backdrop.RelativeSizeAdjustment({ 1.0f, 1.0f }); // fill the parent
        m_backdrop.Brush(m_compositor.CreateColorBrush(m_backgroundColor));
        layer.Children().InsertAtTop(m_backdrop);

        m_circleGeometry = m_compositor.CreateEllipseGeometry(); // radius set via expression animation
        m_circleShape = m_compositor.CreateSpriteShape(m_circleGeometry);
        m_circleShape.FillBrush(m_compositor.CreateColorBrush(m_spotlightColor));
        m_circleShape.Offset({ m_sonarRadiusFloat * m_sonarZoomFactor, m_sonarRadiusFloat * m_sonarZoomFactor });
        m_spotlight = m_compositor.CreateShapeVisual();
        m_spotlight.Size({ m_sonarRadiusFloat * 2 * m_sonarZoomFactor, m_sonarRadiusFloat * 2 * m_sonarZoomFactor });
        m_spotlight.AnchorPoint({ 0.5f, 0.5f });
        m_spotlight.Shapes().Append(m_circleShape);

        layer.Children().InsertAtTop(m_spotlight);

        // Implicitly animate the alpha.
        m_animation = m_compositor.CreateScalarKeyFrameAnimation();
        m_animation.Target(L"Opacity");
        m_animation.InsertExpressionKeyFrame(1.0f, L"this.FinalValue");
        m_animation.Duration(std::chrono::milliseconds{ m_fadeDuration });
        auto collection = m_compositor.CreateImplicitAnimationCollection();
        collection.Insert(L"Opacity", m_animation);
        m_root.ImplicitAnimations(collection);

        // Radius of spotlight shrinks as opacity increases.
        // At opacity zero, it is m_sonarRadius * SonarZoomFactor.
        // At maximum opacity, it is m_sonarRadius.
        auto radiusExpression = m_compositor.CreateExpressionAnimation();
        radiusExpression.SetReferenceParameter(L"Root", m_root);
        wchar_t expressionText[256];
        winrt::check_hresult(StringCchPrintfW(expressionText, ARRAYSIZE(expressionText), L"Lerp(Vector2(%d, %d), Vector2(%d, %d), Root.Opacity * %d / %d)", m_sonarRadius * m_sonarZoomFactor, m_sonarRadius * m_sonarZoomFactor, m_sonarRadius, m_sonarRadius, FinalAlphaDenominator, m_finalAlphaNumerator));
        radiusExpression.Expression(expressionText);
        m_circleGeometry.StartAnimation(L"Radius", radiusExpression);

        return true;
    }
    catch (...)
    {
        return false;
    }

    void OnOpacityAnimationCompleted()
    {
        if (m_root.Opacity() < 0.01f)
        {
            ShowWindow(m_hwnd, SW_HIDE);
        }
    }

public:
    void ApplySettings(const FindMyMouseSettings& settings, bool applyToRuntimeObjects) {
        if (!applyToRuntimeObjects)
        {
            // Runtime objects not created yet. Just update fields.
            m_sonarRadius = settings.spotlightRadius;
            m_sonarRadiusFloat = static_cast<float>(m_sonarRadius);
            m_backgroundColor = settings.backgroundColor;
            m_spotlightColor = settings.spotlightColor;
            m_activationMethod = settings.activationMethod;
            m_includeWinKey = settings.includeWinKey;
            m_doNotActivateOnGameMode = settings.doNotActivateOnGameMode;
            m_fadeDuration = settings.animationDurationMs > 0 ? settings.animationDurationMs : 1;
            m_finalAlphaNumerator = settings.overlayOpacity;
            m_sonarZoomFactor = settings.spotlightInitialZoom;
            m_excludedApps = settings.excludedApps;
            m_shakeMinimumDistance = settings.shakeMinimumDistance;
            m_shakeIntervalMs = settings.shakeIntervalMs;
            m_shakeFactor = settings.shakeFactor;
        }
        else
        {
            // Runtime objects already created. Should update in the owner thread.
            if (m_dispatcherQueueController == nullptr)
            {
                Logger::warn("Tried accessing the dispatch queue controller before it was initialized.");
                // No dispatcher Queue Controller? Means initialization still hasn't run, so settings will be applied then.
                return;
            }
            auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
            FindMyMouseSettings localSettings = settings;
            bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
                if (!m_destroyed)
                {
                    // Runtime objects not created yet. Just update fields.
                    m_sonarRadius = localSettings.spotlightRadius;
                    m_sonarRadiusFloat = static_cast<float>(m_sonarRadius);
                    m_backgroundColor = localSettings.backgroundColor;
                    m_spotlightColor = localSettings.spotlightColor;
                    m_activationMethod = localSettings.activationMethod;
                    m_includeWinKey = localSettings.includeWinKey;
                    m_doNotActivateOnGameMode = localSettings.doNotActivateOnGameMode;
                    m_fadeDuration = localSettings.animationDurationMs > 0 ? localSettings.animationDurationMs : 1;
                    m_finalAlphaNumerator = localSettings.overlayOpacity;
                    m_sonarZoomFactor = localSettings.spotlightInitialZoom;
                    m_excludedApps = localSettings.excludedApps;
                    m_shakeMinimumDistance = localSettings.shakeMinimumDistance;
                    m_shakeIntervalMs = localSettings.shakeIntervalMs;
                    m_shakeFactor = localSettings.shakeFactor;
                    UpdateMouseSnooping(); // For the shake mouse activation method

                    // Apply new settings to runtime composition objects.
                    m_backdrop.Brush().as<winrt::CompositionColorBrush>().Color(m_backgroundColor);
                    m_circleShape.FillBrush().as<winrt::CompositionColorBrush>().Color(m_spotlightColor);
                    m_circleShape.Offset({ m_sonarRadiusFloat * m_sonarZoomFactor, m_sonarRadiusFloat * m_sonarZoomFactor });
                    m_spotlight.Size({ m_sonarRadiusFloat * 2 * m_sonarZoomFactor, m_sonarRadiusFloat * 2 * m_sonarZoomFactor });
                    m_animation.Duration(std::chrono::milliseconds{ m_fadeDuration });
                    m_circleGeometry.StopAnimation(L"Radius");

                    // Update animation
                    auto radiusExpression = m_compositor.CreateExpressionAnimation();
                    radiusExpression.SetReferenceParameter(L"Root", m_root);
                    wchar_t expressionText[256];
                    winrt::check_hresult(StringCchPrintfW(expressionText, ARRAYSIZE(expressionText), L"Lerp(Vector2(%d, %d), Vector2(%d, %d), Root.Opacity * %d / %d)", m_sonarRadius * m_sonarZoomFactor, m_sonarRadius * m_sonarZoomFactor, m_sonarRadius, m_sonarRadius, FinalAlphaDenominator, m_finalAlphaNumerator));
                    radiusExpression.Expression(expressionText);
                    m_circleGeometry.StartAnimation(L"Radius", radiusExpression);
                }
            });
            if (!enqueueSucceeded)
            {
                Logger::error("Couldn't enqueue message to update the sonar settings.");
            }
        }
    }

private:
    winrt::Compositor m_compositor{ nullptr };
    winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::ContainerVisual m_root{ nullptr };
    winrt::CompositionEllipseGeometry m_circleGeometry{ nullptr };
    winrt::ShapeVisual m_spotlight{ nullptr };
    winrt::CompositionCommitBatch m_batch{ nullptr };
    winrt::SpriteVisual m_backdrop{ nullptr };
    winrt::CompositionSpriteShape m_circleShape{ nullptr };
    winrt::Windows::UI::Color m_backgroundColor = FIND_MY_MOUSE_DEFAULT_BACKGROUND_COLOR;
    winrt::Windows::UI::Color m_spotlightColor = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_COLOR;
    winrt::ScalarKeyFrameAnimation m_animation{ nullptr };
};

template<typename D>
struct GdiSonar : SuperSonar<D>
{
    LRESULT WndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        switch (message)
        {
        case WM_CREATE:
            SetLayeredWindowAttributes(this->m_hwnd, 0, 0, LWA_ALPHA);
            break;

        case WM_TIMER:
            switch (wParam)
            {
            case TIMER_ID_FADE:
                OnFadeTimer();
                break;
            }
            break;

        case WM_PAINT:
            this->Shim()->OnPaint();
            break;
        }
        return this->BaseWndProc(message, wParam, lParam);
    }

    void BeforeMoveSonar() { this->Shim()->InvalidateSonar(); }
    void AfterMoveSonar() { this->Shim()->InvalidateSonar(); }

    void SetSonarVisibility(bool visible)
    {
        m_alphaTarget = visible ? MaxAlpha : 0;
        m_fadeStart = GetTickCount() - FadeFramePeriod;
        SetTimer(this->m_hwnd, TIMER_ID_FADE, FadeFramePeriod, nullptr);
        OnFadeTimer();
    }

    void OnFadeTimer()
    {
        auto now = GetTickCount();
        auto step = (int)((now - m_fadeStart) * MaxAlpha / this->m_fadeDuration);

        this->Shim()->InvalidateSonar();
        if (m_alpha < m_alphaTarget)
        {
            m_alpha += step;
            if (m_alpha > m_alphaTarget)
                m_alpha = m_alphaTarget;
        }
        else if (m_alpha > m_alphaTarget)
        {
            m_alpha -= step;
            if (m_alpha < m_alphaTarget)
                m_alpha = m_alphaTarget;
        }
        SetLayeredWindowAttributes(this->m_hwnd, 0, (BYTE)m_alpha, LWA_ALPHA);
        this->Shim()->InvalidateSonar();
        if (m_alpha == m_alphaTarget)
        {
            KillTimer(this->m_hwnd, TIMER_ID_FADE);
            if (m_alpha == 0)
            {
                ShowWindow(this->m_hwnd, SW_HIDE);
            }
        }
        else
        {
            ShowWindow(this->m_hwnd, SW_SHOWNOACTIVATE);
        }
    }

protected:
    int CurrentSonarRadius()
    {
        int range = MaxAlpha - m_alpha;
        int radius = this->m_sonarRadius + this->m_sonarRadius * range * (this->m_sonarZoomFactor - 1) / MaxAlpha;
        return radius;
    }

private:
    static constexpr DWORD FadeFramePeriod = 10;
    int MaxAlpha = SuperSonar<D>::m_finalAlphaNumerator * 255 / SuperSonar<D>::FinalAlphaDenominator;
    static constexpr DWORD TIMER_ID_FADE = 101;

private:
    int m_alpha = 0;
    int m_alphaTarget = 0;
    DWORD m_fadeStart = 0;
};

struct GdiSpotlight : GdiSonar<GdiSpotlight>
{
    void InvalidateSonar()
    {
        RECT rc;
        auto radius = CurrentSonarRadius();
        rc.left = this->m_sonarPos.x - radius;
        rc.top = this->m_sonarPos.y - radius;
        rc.right = this->m_sonarPos.x + radius;
        rc.bottom = this->m_sonarPos.y + radius;
        InvalidateRect(this->m_hwnd, &rc, FALSE);
    }

    void OnPaint()
    {
        PAINTSTRUCT ps;
        BeginPaint(this->m_hwnd, &ps);

        auto radius = CurrentSonarRadius();
        auto spotlight = CreateRoundRectRgn(
            this->m_sonarPos.x - radius, this->m_sonarPos.y - radius, this->m_sonarPos.x + radius, this->m_sonarPos.y + radius, radius * 2, radius * 2);

        FillRgn(ps.hdc, spotlight, static_cast<HBRUSH>(GetStockObject(WHITE_BRUSH)));
        Sleep(1000 / 60);
        ExtSelectClipRgn(ps.hdc, spotlight, RGN_DIFF);
        FillRect(ps.hdc, &ps.rcPaint, static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH)));
        DeleteObject(spotlight);

        EndPaint(this->m_hwnd, &ps);
    }
};

struct GdiCrosshairs : GdiSonar<GdiCrosshairs>
{
    void InvalidateSonar()
    {
        RECT rc;
        auto radius = CurrentSonarRadius();
        GetClientRect(m_hwnd, &rc);
        rc.left = m_sonarPos.x - radius;
        rc.right = m_sonarPos.x + radius;
        InvalidateRect(m_hwnd, &rc, FALSE);

        GetClientRect(m_hwnd, &rc);
        rc.top = m_sonarPos.y - radius;
        rc.bottom = m_sonarPos.y + radius;
        InvalidateRect(m_hwnd, &rc, FALSE);
    }

    void OnPaint()
    {
        PAINTSTRUCT ps;
        BeginPaint(this->m_hwnd, &ps);

        auto radius = CurrentSonarRadius();
        RECT rc;

        HBRUSH white = static_cast<HBRUSH>(GetStockObject(WHITE_BRUSH));

        rc.left = m_sonarPos.x - radius;
        rc.top = ps.rcPaint.top;
        rc.right = m_sonarPos.x + radius;
        rc.bottom = ps.rcPaint.bottom;
        FillRect(ps.hdc, &rc, white);

        rc.left = ps.rcPaint.left;
        rc.top = m_sonarPos.y - radius;
        rc.right = ps.rcPaint.right;
        rc.bottom = m_sonarPos.y + radius;
        FillRect(ps.hdc, &rc, white);

        HBRUSH black = static_cast<HBRUSH>(GetStockObject(BLACK_BRUSH));

        // Top left
        rc.left = ps.rcPaint.left;
        rc.top = ps.rcPaint.top;
        rc.right = m_sonarPos.x - radius;
        rc.bottom = m_sonarPos.y - radius;
        FillRect(ps.hdc, &rc, black);

        // Top right
        rc.left = m_sonarPos.x + radius;
        rc.top = ps.rcPaint.top;
        rc.right = ps.rcPaint.right;
        rc.bottom = m_sonarPos.y - radius;
        FillRect(ps.hdc, &rc, black);

        // Bottom left
        rc.left = ps.rcPaint.left;
        rc.top = m_sonarPos.y + radius;
        rc.right = m_sonarPos.x - radius;
        rc.bottom = ps.rcPaint.bottom;
        FillRect(ps.hdc, &rc, black);

        // Bottom right
        rc.left = m_sonarPos.x + radius;
        rc.top = m_sonarPos.y + radius;
        rc.right = ps.rcPaint.right;
        rc.bottom = ps.rcPaint.bottom;
        FillRect(ps.hdc, &rc, black);

        EndPaint(this->m_hwnd, &ps);
    }
};

#pragma endregion Super_Sonar_Base_Code


#pragma region Super_Sonar_API

CompositionSpotlight* m_sonar = nullptr;
void FindMyMouseApplySettings(const FindMyMouseSettings& settings)
{
    if (m_sonar != nullptr)
    {
        Logger::info("Applying settings.");
        m_sonar->ApplySettings(settings, true);
    }
}

void FindMyMouseDisable()
{
    if (m_sonar != nullptr)
    {
        Logger::info("Terminating a sonar instance.");
        m_sonar->Terminate();
    }
}

bool FindMyMouseIsEnabled()
{
    return (m_sonar != nullptr);
}

// Based on SuperSonar's original wWinMain.
int FindMyMouseMain(HINSTANCE hinst, const FindMyMouseSettings& settings)
{
    Logger::info("Starting a sonar instance.");
    if (m_sonar != nullptr)
    {
        Logger::error("A sonar instance was still working when trying to start a new one.");
        return 0;
    }

    CompositionSpotlight sonar;
    sonar.ApplySettings(settings, false);
    if (!sonar.Initialize(hinst))
    {
        Logger::error("Couldn't initialize a sonar instance.");
        return 0;
    }
    m_sonar = &sonar;
    Logger::info("Initialized the sonar instance.");

    InitializeWinhookEventIds();

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

    Logger::info("Sonar message loop ended.");
    m_sonar = nullptr;

    return (int)msg.wParam;
}

HWND GetSonarHwnd() noexcept
{
    if (m_sonar != nullptr)
    {
        return m_sonar->GetHwnd();
    }

    return nullptr;
}

#pragma endregion Super_Sonar_API
