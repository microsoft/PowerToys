// FindMyMouse.cpp : Based on Raymond Chen's SuperSonar.cpp
//
#include "pch.h"
#include "FindMyMouse.h"
#include "trace.h"
#include "common/utils/game_mode.h"

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

bool m_doNotActivateOnGameMode = true;

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

protected:
    // Base class members you can access.
    D* Shim() { return static_cast<D*>(this); }
    LRESULT BaseWndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept;

    HWND m_hwnd;
    POINT m_sonarPos = ptNowhere;

    static constexpr int SonarRadius = 100;
    static constexpr int SonarZoomFactor = 9;
    static constexpr DWORD FadeDuration = 500;
    static constexpr int FinalAlphaNumerator = 1;
    static constexpr int FinalAlphaDenominator = 2;
    winrt::DispatcherQueueController m_dispatcherQueueController{ nullptr };

private:
    static bool IsEqual(POINT const& p1, POINT const& p2)
    {
        return p1.x == p2.x && p1.y == p2.y;
    }

    static constexpr POINT ptNowhere = { -1, -1 };

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

    HWND m_hwndOwner;
    SonarState m_sonarState = SonarState::Idle;
    POINT m_lastKeyPos{};
    DWORD m_lastKeyTime{};

    static constexpr DWORD NoSonar = 0;
    static constexpr DWORD SonarWaitingForMouseMove = 1;
    DWORD m_sonarStart = NoSonar;
    bool m_isSnoopingMouse = false;

private:
    static constexpr auto className = L"FindMyMouse";

    // Use the runner name for the Window title. Otherwise, since Find My Mouse has an actual visual, its Window name will be the one shown in Task Manager after being shown.
    static constexpr auto windowTitle = L"PowerToys Runner";

    static LRESULT CALLBACK s_WndProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

    BOOL OnSonarCreate();
    void OnSonarDestroy();
    void OnSonarInput(WPARAM flags, HRAWINPUT hInput);
    void OnSonarKeyboardInput(RAWINPUT const& input);
    void OnSonarMouseInput(RAWINPUT const& input);
    void OnMouseTimer();

    void StartSonar();
    void StopSonar();

    void UpdateMouseSnooping();
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
        wc.hbrBackground = (HBRUSH)GetStockObject(NULL_BRUSH);
        wc.lpszClassName = className;

        if (!RegisterClassW(&wc))
        {
            return false;
        }
    }

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | Shim()->GetExtendedStyle();
    return CreateWindowExW(exStyle, className, windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hinst, this) != nullptr;
}

template<typename D>
void SuperSonar<D>::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
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
        auto info = (LPCREATESTRUCT)lParam;
        SetWindowLongPtr(hwnd, GWLP_USERDATA, (LONG_PTR)info->lpCreateParams);
        self = (SuperSonar*)info->lpCreateParams;
        self->m_hwnd = hwnd;
    }
    else
    {
        self = (SuperSonar*)GetWindowLongPtr(hwnd, GWLP_USERDATA);
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
        return OnSonarCreate() ? 0 : -1;

    case WM_DESTROY:
        OnSonarDestroy();
        break;

    case WM_INPUT:
        OnSonarInput(wParam, (HRAWINPUT)lParam);
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
    if ((int)result < sizeof(RAWINPUTHEADER))
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
    // Don't activate if game mode is on.
    if (m_doNotActivateOnGameMode && detect_game_mode())
    {
        return;
    }

    if (input.data.keyboard.VKey != VK_CONTROL)
    {
        StopSonar();
        return;
    }

    bool pressed = (input.data.keyboard.Flags & RI_KEY_BREAK) == 0;
    bool rightCtrl = (input.data.keyboard.Flags & RI_KEY_E0) != 0;

    // Deal with rightCtrl first.
    if (rightCtrl)
    {
        /*
        * SuperSonar originally exited when pressing right control after pressing left control twice.
        * We take care of exiting FindMyMouse through module disabling in PowerToys settings instead.
        if (m_sonarState == SonarState::ControlUp2)
        {
            Terminate();
        }
        */
        StopSonar();
        return;
    }

    switch (m_sonarState)
    {
    case SonarState::Idle:
        if (pressed)
        {
            m_sonarState = SonarState::ControlDown1;
            m_lastKeyTime = GetTickCount();
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
    case SonarState::ControlUp2:
        if (pressed)
        {
            m_sonarState = SonarState::ControlDown2;
            auto now = GetTickCount();
            POINT ptCursor{};
            if (GetCursorPos(&ptCursor) &&
                now - m_lastKeyTime <= GetDoubleClickTime() &&
                IsEqual(m_lastKeyPos, ptCursor))
            {
                StartSonar();
            }
            m_lastKeyTime = now;
            m_lastKeyPos = ptCursor;
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

template<typename D>
void SuperSonar<D>::OnSonarMouseInput(RAWINPUT const& input)
{
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
    Logger::info("Focusing the sonar on the mouse cursor.");
    Trace::MousePointerFocused();
    // Cover the entire virtual screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN), GetSystemMetrics(SM_YVIRTUALSCREEN), GetSystemMetrics(SM_CXVIRTUALSCREEN), GetSystemMetrics(SM_CYVIRTUALSCREEN), 0);
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
    auto now = GetTickCount();

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
    bool wantSnoopingMouse = m_sonarStart != NoSonar || m_sonarState != SonarState::Idle;
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

struct CompositionSpotlight : SuperSonar<CompositionSpotlight>
{
    static constexpr UINT WM_OPACITY_ANIMATION_COMPLETED = WM_APP;
    static constexpr float SonarRadiusFloat = static_cast<float>(SonarRadius);

    DWORD GetExtendedStyle()
    {
        return WS_EX_NOREDIRECTIONBITMAP;
    }

    void AfterMoveSonar()
    {
        m_spotlight.Offset({ (float)m_sonarPos.x, (float)m_sonarPos.y, 0.0f });
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
        m_batch.Completed([hwnd = m_hwnd](auto&&, auto&&) {
            PostMessage(hwnd, WM_OPACITY_ANIMATION_COMPLETED, 0, 0);
        });
        m_root.Opacity(visible ? static_cast<float>(FinalAlphaNumerator) / FinalAlphaDenominator : 0.0f);
        if (visible)
        {
            ShowWindow(m_hwnd, SW_SHOWNOACTIVATE);
        }
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

        auto backdrop = m_compositor.CreateSpriteVisual();
        backdrop.RelativeSizeAdjustment({ 1.0f, 1.0f }); // fill the parent
        backdrop.Brush(m_compositor.CreateColorBrush({ 255, 0, 0, 0 }));
        layer.Children().InsertAtTop(backdrop);

        m_circleGeometry = m_compositor.CreateEllipseGeometry(); // radius set via expression animation
        auto circleShape = m_compositor.CreateSpriteShape(m_circleGeometry);
        circleShape.FillBrush(m_compositor.CreateColorBrush({ 255, 255, 255, 255 }));
        circleShape.Offset({ SonarRadiusFloat * SonarZoomFactor, SonarRadiusFloat * SonarZoomFactor });
        m_spotlight = m_compositor.CreateShapeVisual();
        m_spotlight.Size({ SonarRadiusFloat * 2 * SonarZoomFactor, SonarRadiusFloat * 2 * SonarZoomFactor });
        m_spotlight.AnchorPoint({ 0.5f, 0.5f });
        m_spotlight.Shapes().Append(circleShape);

        layer.Children().InsertAtTop(m_spotlight);

        // Implicitly animate the alpha.
        auto animation = m_compositor.CreateScalarKeyFrameAnimation();
        animation.Target(L"Opacity");
        animation.InsertExpressionKeyFrame(1.0f, L"this.FinalValue");
        animation.Duration(std::chrono::milliseconds{ FadeDuration });
        auto collection = m_compositor.CreateImplicitAnimationCollection();
        collection.Insert(L"Opacity", animation);
        m_root.ImplicitAnimations(collection);

        // Radius of spotlight shrinks as opacity increases.
        // At opacity zero, it is SonarRadius * SonarZoomFactor.
        // At maximum opacity, it is SonarRadius.
        auto radiusExpression = m_compositor.CreateExpressionAnimation();
        radiusExpression.SetReferenceParameter(L"Root", m_root);
        wchar_t expressionText[256];
        winrt::check_hresult(StringCchPrintfW(expressionText, ARRAYSIZE(expressionText), L"Lerp(Vector2(%d, %d), Vector2(%d, %d), Root.Opacity * %d / %d)", SonarRadius * SonarZoomFactor, SonarRadius * SonarZoomFactor, SonarRadius, SonarRadius, FinalAlphaDenominator, FinalAlphaNumerator));
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

private:
    winrt::Compositor m_compositor{ nullptr };
    winrt::Desktop::DesktopWindowTarget m_target{ nullptr };
    winrt::ContainerVisual m_root{ nullptr };
    winrt::CompositionEllipseGeometry m_circleGeometry{ nullptr };
    winrt::ShapeVisual m_spotlight{ nullptr };
    winrt::CompositionCommitBatch m_batch{ nullptr };
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
        auto step = (int)((now - m_fadeStart) * MaxAlpha / this->FadeDuration);

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
        int radius = this->SonarRadius + this->SonarRadius * range * (this->SonarZoomFactor - 1) / MaxAlpha;
        return radius;
    }

private:
    static constexpr DWORD FadeFramePeriod = 10;
    static constexpr int MaxAlpha = SuperSonar<D>::FinalAlphaNumerator * 255 / SuperSonar<D>::FinalAlphaDenominator;
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

        FillRgn(ps.hdc, spotlight, (HBRUSH)GetStockObject(WHITE_BRUSH));
        Sleep(1000 / 60);
        ExtSelectClipRgn(ps.hdc, spotlight, RGN_DIFF);
        FillRect(ps.hdc, &ps.rcPaint, (HBRUSH)GetStockObject(BLACK_BRUSH));
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

        HBRUSH white = (HBRUSH)GetStockObject(WHITE_BRUSH);

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

        HBRUSH black = (HBRUSH)GetStockObject(BLACK_BRUSH);

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

void FindMyMouseSetDoNotActivateOnGameMode(bool doNotActivate)
{
    m_doNotActivateOnGameMode = doNotActivate;
}

// Based on SuperSonar's original wWinMain.
int FindMyMouseMain(HINSTANCE hinst)
{
    Logger::info("Starting a sonar instance.");
    if (m_sonar != nullptr)
    {
        Logger::error("A sonar instance was still working when trying to start a new one.");
        return 0;
    }

    CompositionSpotlight sonar;
    if (!sonar.Initialize(hinst))
    {
        Logger::error("Couldn't initialize a sonar instance.");
        return 0;
    }
    m_sonar = &sonar;
    Logger::info("Initialized the sonar instance.");

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

#pragma endregion Super_Sonar_API
