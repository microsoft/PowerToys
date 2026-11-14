// FindMyMouse.cpp : Based on Raymond Chen's SuperSonar.cpp
//
#include "pch.h"
#include "FindMyMouse.h"
#include "WinHookEventIDs.h"
#include "trace.h"
#include "common/utils/game_mode.h"
#include "common/utils/process_path.h"
#include "common/utils/excluded_apps.h"
#include "common/utils/haptic_feedback.h"
#include "common/utils/MsWindowsSettings.h"
#include <winrt/Windows.Graphics.h>
#include <winrt/Windows.Devices.Haptics.h>

#include <winrt/Microsoft.UI.Composition.Interop.h>
#include <winrt/Microsoft.UI.Dispatching.h>
#include <winrt/Microsoft.UI.Xaml.h>
#include <winrt/Microsoft.UI.Xaml.Controls.h>
#include <winrt/Microsoft.UI.Xaml.Media.h>
#include <winrt/Microsoft.UI.Xaml.Hosting.h>
#include <winrt/Microsoft.UI.Interop.h>
#include <winrt/Microsoft.UI.Content.h>

#include <vector>

namespace winrt
{
    using namespace winrt::Windows::System;
}

namespace muxc = winrt::Microsoft::UI::Composition;
namespace muxx = winrt::Microsoft::UI::Xaml;
namespace muxxc = winrt::Microsoft::UI::Xaml::Controls;
namespace muxxh = winrt::Microsoft::UI::Xaml::Hosting;

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
    void UpdateHapticInputWindows();
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
    std::vector<std::wstring> m_excludedApps;
    int m_shakeMinimumDistance = FIND_MY_MOUSE_DEFAULT_SHAKE_MINIMUM_DISTANCE;
    winrt::Microsoft::UI::Dispatching::DispatcherQueueController m_dispatcherQueueController{ nullptr };

    // Don't consider movements started past these milliseconds to detect shaking.
    int m_shakeIntervalMs = FIND_MY_MOUSE_DEFAULT_SHAKE_INTERVAL_MS;
    // By which factor must travelled distance be than the diagonal of the rectangle containing the movements. (value in percent)
    int m_shakeFactor = FIND_MY_MOUSE_DEFAULT_SHAKE_FACTOR;
    HapticFeedback::Player m_hapticFeedback;
    HMONITOR m_activeMonitor = nullptr;
    HMONITOR m_candidateMonitor = nullptr;
    int m_candidateMonitorInputCount = 0;
    bool m_isAtNonTraversableEdge = false;

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
    static constexpr UINT_PTR TIMER_ID_HAPTIC_INPUT = 101;
    static constexpr DWORD IdlePeriod = 1000;
    static constexpr int HapticInputWindowSize = 96;
    static constexpr auto hapticInputWindowClassName = L"FindMyMouseHapticInputWindow";
    static constexpr auto hapticEdgeWindowClassName = L"FindMyMouseHapticEdgeWindow";

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
    HINSTANCE m_hinstance{};
    SonarState m_sonarState = SonarState::Idle;
    POINT m_lastKeyPos{};
    ULONGLONG m_lastKeyTime{};

    static constexpr DWORD NoSonar = 0;
    static constexpr DWORD SonarWaitingForMouseMove = 1;
    ULONGLONG m_sonarStart = NoSonar;
    bool m_isSnoopingMouse = false;
    HWND m_hapticInputWindow = nullptr;
    std::vector<HWND> m_hapticEdgeWindows;
    uint16_t m_pendingHapticWaveform = 0;
    bool m_isAcquiringHapticController = false;

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
    void InitializeHaptics();
    bool TryPlayHaptic(uint16_t waveform);
    void RequestHaptic(uint16_t waveform);
    void PrepareHapticControllerAcquisition();
    void CompletePendingHaptic();
    void CancelPendingHaptic();
    bool EnsureHapticInputWindow();
    void PositionHapticInputWindow(const POINT& cursorPos);
    void RegisterHapticEdgeWindows();
    void UnregisterHapticEdgeWindows();
    void UpdatePointerHapticsState();
    static bool IsAtNonTraversableEdge(const POINT& cursorPos, HMONITOR monitor);
    static LRESULT CALLBACK HapticInputWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK HapticEdgeWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam);

    void StartSonar(FindMyMouseActivationMethod activationMethod);
    void StopSonar();
};

template<typename D>
bool SuperSonar<D>::Initialize(HINSTANCE hinst)
{
    m_hinstance = hinst;
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
            Logger::error("RegisterClassW failed. GetLastError={}", GetLastError());
            return false;
        }
    }
    // else: class already registered

    m_hwndOwner = CreateWindow(L"static", nullptr, WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinst, nullptr);
    if (!m_hwndOwner)
    {
        Logger::error("Failed to create owner window. GetLastError={}", GetLastError());
        return false;
    }

    DWORD exStyle = WS_EX_TRANSPARENT | WS_EX_LAYERED | WS_EX_TOOLWINDOW | Shim()->GetExtendedStyle();
    HWND created = CreateWindowExW(exStyle, className, windowTitle, WS_POPUP, CW_USEDEFAULT, 0, CW_USEDEFAULT, 0, m_hwndOwner, nullptr, hinst, this);
    if (!created)
    {
        Logger::error("CreateWindowExW failed. GetLastError={}", GetLastError());
        return false;
    }

    return true;
}

template<typename D>
void SuperSonar<D>::Terminate()
{
    auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
    bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
        m_destroyed = true;
        CancelPendingHaptic();
        if (m_hapticInputWindow)
        {
            DestroyWindow(m_hapticInputWindow);
            m_hapticInputWindow = nullptr;
        }
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
        if (!OnSonarCreate())
            return -1;
        UpdateMouseSnooping();
        return 0;

    case WM_DESTROY:
        OnSonarDestroy();
        break;

    case WM_DISPLAYCHANGE:
        UnregisterHapticEdgeWindows();
        UpdateHapticInputWindows();
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
            StartSonar(FindMyMouseActivationMethod::Shortcut);
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
    const BOOL registered = RegisterRawInputDevices(&keyboard, 1, sizeof(keyboard));
    if (registered)
    {
        InitializeHaptics();
        RegisterHapticEdgeWindows();
    }
    return registered;
}

template<typename D>
void SuperSonar<D>::OnSonarDestroy()
{
    CancelPendingHaptic();
    UnregisterHapticEdgeWindows();
    if (m_hapticInputWindow)
    {
        DestroyWindow(m_hapticInputWindow);
        m_hapticInputWindow = nullptr;
    }
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

    if ((m_activationMethod != FindMyMouseActivationMethod::DoubleRightControlKey && m_activationMethod != FindMyMouseActivationMethod::DoubleLeftControlKey) || input.data.keyboard.VKey != VK_CONTROL)
    {
        StopSonar();
        return;
    }

    bool pressed = (input.data.keyboard.Flags & RI_KEY_BREAK) == 0;

    bool leftCtrlPressed = (input.data.keyboard.Flags & RI_KEY_E0) == 0;
    bool rightCtrlPressed = (input.data.keyboard.Flags & RI_KEY_E0) != 0;

    if ((m_activationMethod == FindMyMouseActivationMethod::DoubleRightControlKey && !rightCtrlPressed) || (m_activationMethod == FindMyMouseActivationMethod::DoubleLeftControlKey && !leftCtrlPressed))
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
                StartSonar(m_activationMethod);
            }
            else
            {
                m_sonarState = SonarState::ControlDown1;
                m_lastKeyTime = GetTickCount64();
                m_lastKeyPos = {};
                GetCursorPos(&m_lastKeyPos);
                UpdateMouseSnooping();
            }
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
    LONGLONG currentX = 0, minX = 0, maxX = 0;
    LONGLONG currentY = 0, minY = 0, maxY = 0;

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
        if (distanceTravelled >= m_shakeMinimumDistance / 2.0)
        {
            PrepareHapticControllerAcquisition();
        }
        return;
    }

    // Size of the rectangle that the pointer moved in.
    double rectangleWidth = static_cast<double>(maxX) - minX;
    double rectangleHeight = static_cast<double>(maxY) - minY;

    double diagonal = sqrt(rectangleWidth * rectangleWidth + rectangleHeight * rectangleHeight);
    if (diagonal > 0 && distanceTravelled / diagonal > (m_shakeFactor / 100.f))
    {
        m_movementHistory.clear();
        StartSonar(m_activationMethod);
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
    UpdatePointerHapticsState();

    if (m_activationMethod == FindMyMouseActivationMethod::ShakeMouse)
    {
        LONG relativeX = 0;
        LONG relativeY = 0;
        if ((input.data.mouse.usFlags & MOUSE_MOVE_ABSOLUTE) == MOUSE_MOVE_ABSOLUTE && (input.data.mouse.lLastX != 0 || input.data.mouse.lLastY != 0))
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
                m_movementHistory.push_back({ .diff = { .x = relativeX, .y = relativeY }, .tick = GetTickCount64() });
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
void SuperSonar<D>::StartSonar(FindMyMouseActivationMethod activationMethod)
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

    Trace::MousePointerFocused(static_cast<int>(activationMethod));
    // Cover the entire virtual screen.
    // HACK: Draw with 1 pixel off. Otherwise, Windows glitches the task bar transparency when a transparent window fill the whole screen.
    SetWindowPos(m_hwnd, HWND_TOPMOST, GetSystemMetrics(SM_XVIRTUALSCREEN) + 1, GetSystemMetrics(SM_YVIRTUALSCREEN) + 1, GetSystemMetrics(SM_CXVIRTUALSCREEN) - 2, GetSystemMetrics(SM_CYVIRTUALSCREEN) - 2, 0);
    m_sonarPos = ptNowhere;
    OnMouseTimer();
    UpdateMouseSnooping();
    Shim()->SetSonarVisibility(true);
    if (activationMethod == FindMyMouseActivationMethod::ShakeMouse)
    {
        const auto waveform = winrt::Windows::Devices::Haptics::KnownSimpleHapticsControllerWaveforms::Success();
        if (!TryPlayHaptic(waveform))
        {
            RequestHaptic(waveform);
        }
    }
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
    bool wantSnoopingMouse = m_sonarStart != NoSonar ||
                             m_sonarState != SonarState::Idle ||
                             m_activationMethod == FindMyMouseActivationMethod::ShakeMouse ||
                             m_hapticFeedback.IsPlaybackEnabled();
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
void SuperSonar<D>::InitializeHaptics()
{
    try
    {
        m_hapticFeedback.InitializeForCurrentThread();
        Logger::info("Find My Mouse haptic feedback supported={}, device present={}", m_hapticFeedback.IsSupported(), m_hapticFeedback.IsDevicePresent());
    }
    catch (const winrt::hresult_error& error)
    {
        Logger::warn(L"Find My Mouse haptics initialization failed: {}", error.message());
    }
}

template<typename D>
bool SuperSonar<D>::TryPlayHaptic(uint16_t waveform)
{
    try
    {
        const bool sent = m_hapticFeedback.TryPlay(waveform);
#ifdef _DEBUG
        Logger::info(
            "Find My Mouse haptic waveform={} controller={} device_type={} sent={}",
            waveform,
            m_hapticFeedback.HasCurrentController(),
            static_cast<int>(m_hapticFeedback.CurrentControllerDeviceType()),
            sent);
#endif
        return sent;
    }
    catch (const winrt::hresult_error& error)
    {
        Logger::warn(L"Find My Mouse failed to play haptic feedback: {}", error.message());
        return false;
    }
}

template<typename D>
void SuperSonar<D>::RequestHaptic(uint16_t waveform)
{
    if (!m_hapticFeedback.IsPlaybackEnabled())
    {
        return;
    }

    if (m_hapticFeedback.HasCurrentController() && TryPlayHaptic(waveform))
    {
        return;
    }

    if (m_pendingHapticWaveform != 0 || !EnsureHapticInputWindow())
    {
        return;
    }

    POINT cursorPos{};
    if (!GetCursorPos(&cursorPos))
    {
        return;
    }

    m_pendingHapticWaveform = waveform;
    PositionHapticInputWindow(cursorPos);
    SetTimer(m_hapticInputWindow, TIMER_ID_HAPTIC_INPUT, 250, nullptr);
}

template<typename D>
void SuperSonar<D>::PrepareHapticControllerAcquisition()
{
    if (!m_hapticFeedback.IsPlaybackEnabled() || m_hapticFeedback.HasCurrentController() || m_pendingHapticWaveform != 0 || m_isAcquiringHapticController || !EnsureHapticInputWindow())
    {
        return;
    }

    POINT cursorPos{};
    if (!GetCursorPos(&cursorPos))
    {
        return;
    }

    m_isAcquiringHapticController = true;
    PositionHapticInputWindow(cursorPos);
    SetTimer(m_hapticInputWindow, TIMER_ID_HAPTIC_INPUT, 250, nullptr);
}

template<typename D>
void SuperSonar<D>::CompletePendingHaptic()
{
    if (m_pendingHapticWaveform == 0)
    {
        return;
    }

    if (TryPlayHaptic(m_pendingHapticWaveform))
    {
        CancelPendingHaptic();
    }
}

template<typename D>
void SuperSonar<D>::CancelPendingHaptic()
{
    m_pendingHapticWaveform = 0;
    m_isAcquiringHapticController = false;
    if (m_hapticInputWindow)
    {
        KillTimer(m_hapticInputWindow, TIMER_ID_HAPTIC_INPUT);
        ShowWindow(m_hapticInputWindow, SW_HIDE);
    }
}

template<typename D>
bool SuperSonar<D>::EnsureHapticInputWindow()
{
    if (m_hapticInputWindow)
    {
        return true;
    }

    WNDCLASSEXW windowClass{ sizeof(windowClass) };
    windowClass.lpfnWndProc = HapticInputWindowProc;
    windowClass.hInstance = GetModuleHandleW(nullptr);
    windowClass.lpszClassName = hapticInputWindowClassName;
    RegisterClassExW(&windowClass);

    m_hapticInputWindow = CreateWindowExW(
        WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_LAYERED,
        hapticInputWindowClassName,
        nullptr,
        WS_POPUP,
        0,
        0,
        0,
        0,
        nullptr,
        nullptr,
        GetModuleHandleW(nullptr),
        this);
    if (!m_hapticInputWindow)
    {
        Logger::warn("Find My Mouse failed to create haptic input window. GetLastError={}", GetLastError());
        return false;
    }

    SetLayeredWindowAttributes(m_hapticInputWindow, 0, 1, LWA_ALPHA);
    return true;
}

template<typename D>
void SuperSonar<D>::PositionHapticInputWindow(const POINT& cursorPos)
{
    const int virtualLeft = GetSystemMetrics(SM_XVIRTUALSCREEN);
    const int virtualTop = GetSystemMetrics(SM_YVIRTUALSCREEN);
    const int virtualRight = virtualLeft + GetSystemMetrics(SM_CXVIRTUALSCREEN);
    const int virtualBottom = virtualTop + GetSystemMetrics(SM_CYVIRTUALSCREEN);
    const int left = std::clamp(static_cast<int>(cursorPos.x) - HapticInputWindowSize / 2, virtualLeft, virtualRight - HapticInputWindowSize);
    const int top = std::clamp(static_cast<int>(cursorPos.y) - HapticInputWindowSize / 2, virtualTop, virtualBottom - HapticInputWindowSize);

    SetWindowPos(
        m_hapticInputWindow,
        HWND_TOPMOST,
        left,
        top,
        HapticInputWindowSize,
        HapticInputWindowSize,
        SWP_NOACTIVATE | SWP_SHOWWINDOW);
}

template<typename D>
LRESULT CALLBACK SuperSonar<D>::HapticInputWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    SuperSonar* self = nullptr;
    if (message == WM_NCCREATE)
    {
        const auto createInfo = reinterpret_cast<LPCREATESTRUCTW>(lParam);
        self = static_cast<SuperSonar*>(createInfo->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    else
    {
        self = reinterpret_cast<SuperSonar*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }

    if (self)
    {
        if (message == WM_MOUSEMOVE)
        {
            if (self->m_pendingHapticWaveform != 0)
            {
                self->CompletePendingHaptic();
            }
            else if (self->m_isAcquiringHapticController && self->m_hapticFeedback.HasCurrentController())
            {
                self->CancelPendingHaptic();
            }
            return 0;
        }

        if (message == WM_TIMER && wParam == TIMER_ID_HAPTIC_INPUT)
        {
            self->CancelPendingHaptic();
            return 0;
        }

        if (message >= WM_LBUTTONDOWN && message <= WM_XBUTTONDBLCLK)
        {
            self->CancelPendingHaptic();
        }
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}

template<typename D>
void SuperSonar<D>::UpdateHapticInputWindows()
{
    if (m_hapticFeedback.IsPlaybackEnabled())
    {
        RegisterHapticEdgeWindows();
    }
    else
    {
        CancelPendingHaptic();
        UnregisterHapticEdgeWindows();
    }
}

template<typename D>
void SuperSonar<D>::RegisterHapticEdgeWindows()
{
    if (!m_hapticFeedback.IsPlaybackEnabled() || !m_hapticEdgeWindows.empty())
    {
        return;
    }

    WNDCLASSEXW windowClass{ sizeof(windowClass) };
    windowClass.lpfnWndProc = HapticEdgeWindowProc;
    windowClass.hInstance = m_hinstance;
    windowClass.lpszClassName = hapticEdgeWindowClassName;
    RegisterClassExW(&windowClass);

    std::vector<RECT> monitorRects;
    EnumDisplayMonitors(
        nullptr,
        nullptr,
        [](HMONITOR monitor, HDC, LPRECT, LPARAM context) -> BOOL {
            MONITORINFO monitorInfo{ sizeof(monitorInfo) };
            if (GetMonitorInfoW(monitor, &monitorInfo))
            {
                reinterpret_cast<std::vector<RECT>*>(context)->push_back(monitorInfo.rcMonitor);
            }
            return TRUE;
        },
        reinterpret_cast<LPARAM>(&monitorRects));

    for (const auto& rect : monitorRects)
    {
        const RECT edgeRects[]{
            { rect.left, rect.top, rect.left + 1, rect.bottom },
            { rect.right - 1, rect.top, rect.right, rect.bottom },
            { rect.left, rect.top, rect.right, rect.top + 1 },
            { rect.left, rect.bottom - 1, rect.right, rect.bottom }
        };

        for (const auto& edgeRect : edgeRects)
        {
            const HWND window = CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST | WS_EX_LAYERED,
                hapticEdgeWindowClassName,
                nullptr,
                WS_POPUP,
                edgeRect.left,
                edgeRect.top,
                edgeRect.right - edgeRect.left,
                edgeRect.bottom - edgeRect.top,
                nullptr,
                nullptr,
                m_hinstance,
                this);
            if (window)
            {
                SetLayeredWindowAttributes(window, 0, 1, LWA_ALPHA);
                ShowWindow(window, SW_SHOWNOACTIVATE);
                m_hapticEdgeWindows.push_back(window);
            }
        }
    }
}

template<typename D>
void SuperSonar<D>::UnregisterHapticEdgeWindows()
{
    for (const HWND window : m_hapticEdgeWindows)
    {
        DestroyWindow(window);
    }
    m_hapticEdgeWindows.clear();
}

template<typename D>
LRESULT CALLBACK SuperSonar<D>::HapticEdgeWindowProc(HWND hwnd, UINT message, WPARAM wParam, LPARAM lParam)
{
    SuperSonar* self = nullptr;
    if (message == WM_NCCREATE)
    {
        const auto createInfo = reinterpret_cast<LPCREATESTRUCTW>(lParam);
        self = static_cast<SuperSonar*>(createInfo->lpCreateParams);
        SetWindowLongPtrW(hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(self));
    }
    else
    {
        self = reinterpret_cast<SuperSonar*>(GetWindowLongPtrW(hwnd, GWLP_USERDATA));
    }

    if (self && message == WM_MOUSEMOVE)
    {
        self->UpdatePointerHapticsState();
        self->CompletePendingHaptic();
        return 0;
    }

    return DefWindowProcW(hwnd, message, wParam, lParam);
}

template<typename D>
void SuperSonar<D>::UpdatePointerHapticsState()
{
    POINT cursorPos{};
    if (!GetCursorPos(&cursorPos))
    {
        return;
    }

    const HMONITOR currentMonitor = MonitorFromPoint(cursorPos, MONITOR_DEFAULTTONEAREST);
    if (!m_activeMonitor)
    {
        m_activeMonitor = currentMonitor;
    }
    else if (currentMonitor == m_activeMonitor)
    {
        m_candidateMonitor = nullptr;
        m_candidateMonitorInputCount = 0;
    }
    else
    {
        if (currentMonitor != m_candidateMonitor)
        {
            m_candidateMonitor = currentMonitor;
            m_candidateMonitorInputCount = 1;
        }
        else if (++m_candidateMonitorInputCount >= 2)
        {
            m_activeMonitor = currentMonitor;
            m_candidateMonitor = nullptr;
            m_candidateMonitorInputCount = 0;
            RequestHaptic(winrt::Windows::Devices::Haptics::KnownSimpleHapticsControllerWaveforms::Hover());
        }
    }

    const bool isAtNonTraversableEdge = IsAtNonTraversableEdge(cursorPos, currentMonitor);
    if (isAtNonTraversableEdge && !m_isAtNonTraversableEdge)
    {
        RequestHaptic(winrt::Windows::Devices::Haptics::KnownSimpleHapticsControllerWaveforms::Collide());
    }
    m_isAtNonTraversableEdge = isAtNonTraversableEdge;
}

template<typename D>
bool SuperSonar<D>::IsAtNonTraversableEdge(const POINT& cursorPos, HMONITOR monitor)
{
    MONITORINFO monitorInfo{ sizeof(monitorInfo) };
    if (!GetMonitorInfoW(monitor, &monitorInfo))
    {
        return false;
    }

    const RECT& rect = monitorInfo.rcMonitor;
    const POINT probes[]{
        { rect.left - 1, cursorPos.y },
        { rect.right, cursorPos.y },
        { cursorPos.x, rect.top - 1 },
        { cursorPos.x, rect.bottom }
    };
    const bool atEdges[]{
        cursorPos.x <= rect.left,
        cursorPos.x >= rect.right - 1,
        cursorPos.y <= rect.top,
        cursorPos.y >= rect.bottom - 1
    };

    for (size_t index = 0; index < std::size(probes); ++index)
    {
        if (atEdges[index] && MonitorFromPoint(probes[index], MONITOR_DEFAULTTONULL) == nullptr)
        {
            return true;
        }
    }

    return false;
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
        // Remove WS_EX_NOREDIRECTIONBITMAP for Composition/XAML to allow DWM redirection.
        return 0;
    }

    void AfterMoveSonar()
    {
        const float scale = static_cast<float>(m_surface.XamlRoot().RasterizationScale());
        // Move gradient center
        if (m_spotlightMaskGradient)
        {
            m_spotlightMaskGradient.EllipseCenter({ static_cast<float>(m_sonarPos.x) / scale,
                                                    static_cast<float>(m_sonarPos.y) / scale });
        }
        // Move spotlight visual (color fill) below masked backdrop
        if (m_spotlight)
        {
            m_spotlight.Offset({ static_cast<float>(m_sonarPos.x) / scale,
                                 static_cast<float>(m_sonarPos.y) / scale,
                                 0.0f });
        }
    }

    LRESULT WndProc(UINT message, WPARAM wParam, LPARAM lParam) noexcept
    {
        switch (message)
        {
        case WM_CREATE:
            if (!OnCompositionCreate())
                return -1;
            return BaseWndProc(message, wParam, lParam);

        case WM_OPACITY_ANIMATION_COMPLETED:
            OnOpacityAnimationCompleted();
            break;
        case WM_SIZE:
            UpdateIslandSize();
            break;
        }
        return BaseWndProc(message, wParam, lParam);
    }

    void SetSonarVisibility(bool visible)
    {
        m_batch = m_compositor.GetCommitBatch(muxc::CompositionBatchTypes::Animation);
        BOOL isEnabledAnimations = GetAnimationsEnabled();
        m_animation.Duration(std::chrono::milliseconds{ isEnabledAnimations ? m_fadeDuration : 1 });
        m_batch.Completed([hwnd = m_hwnd](auto&&, auto&&) {
            PostMessage(hwnd, WM_OPACITY_ANIMATION_COMPLETED, 0, 0);
        });
        m_root.Opacity(visible ? 1.0f : 0.0f);
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
        // Creating composition resources
        // Ensure a DispatcherQueue bound to this thread (required by WinAppSDK composition/XAML)
        if (!m_dispatcherQueueController)
        {
            // Ensure COM is initialized
            try
            {
                winrt::init_apartment(winrt::apartment_type::single_threaded);
                // COM STA initialized
            }
            catch (const winrt::hresult_error& e)
            {
                Logger::error("Failed to initialize COM apartment: {}", winrt::to_string(e.message()));
                return false;
            }

            try
            {
                m_dispatcherQueueController =
                    winrt::Microsoft::UI::Dispatching::DispatcherQueueController::CreateOnCurrentThread();
                // DispatcherQueueController created
            }
            catch (const winrt::hresult_error& e)
            {
                Logger::error("Failed to create DispatcherQueueController: {}", winrt::to_string(e.message()));
                return false;
            }
        }

        // 1) Create a XAML island and attach it to this HWND
        try
        {
            m_island = winrt::Microsoft::UI::Xaml::Hosting::DesktopWindowXamlSource{};
            auto windowId = winrt::Microsoft::UI::GetWindowIdFromWindow(m_hwnd);
            m_island.Initialize(windowId);
            // Xaml source initialized
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error("Failed to create XAML island: {}", winrt::to_string(e.message()));
            return false;
        }

        UpdateIslandSize();
        // Island size set

        // 2) Create a XAML container to host the Composition child visual
        m_surface = winrt::Microsoft::UI::Xaml::Controls::Grid{};

        // A transparent background keeps hit-testing consistent vs. null brush
        m_surface.Background(winrt::Microsoft::UI::Xaml::Media::SolidColorBrush{
            winrt::Microsoft::UI::Colors::Transparent() });
        m_surface.HorizontalAlignment(muxx::HorizontalAlignment::Stretch);
        m_surface.VerticalAlignment(muxx::VerticalAlignment::Stretch);

        m_island.Content(m_surface);

        // 3) Get the compositor from the XAML visual tree (pure MUXC path)
        try
        {
            auto elementVisual =
                winrt::Microsoft::UI::Xaml::Hosting::ElementCompositionPreview::GetElementVisual(m_surface);
            m_compositor = elementVisual.Compositor();
            // Compositor acquired
        }
        catch (const winrt::hresult_error& e)
        {
            Logger::error("Failed to get compositor: {}", winrt::to_string(e.message()));
            return false;
        }

        // 4) Build the composition tree
        //
        // [root] ContainerVisual (fills host)
        //  \ LayerVisual
        //     \ [backdrop dim * radial gradient mask (hole)]
        m_root = m_compositor.CreateContainerVisual();
        m_root.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Opacity(0.0f);

        // Insert our root as a hand-in Visual under the XAML element
        winrt::Microsoft::UI::Xaml::Hosting::ElementCompositionPreview::SetElementChildVisual(m_surface, m_root);

        auto layer = m_compositor.CreateLayerVisual();
        layer.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_root.Children().InsertAtTop(layer);

        const float scale = static_cast<float>(m_surface.XamlRoot().RasterizationScale());
        const float rDip = m_sonarRadiusFloat / scale;
        const float zoom = static_cast<float>(m_sonarZoomFactor);

        // Spotlight shape (below backdrop, visible through hole)
        m_circleGeometry = m_compositor.CreateEllipseGeometry();
        m_circleShape = m_compositor.CreateSpriteShape(m_circleGeometry);
        m_circleShape.FillBrush(m_compositor.CreateColorBrush(m_spotlightColor));
        m_circleShape.Offset({ rDip * zoom, rDip * zoom });
        m_spotlight = m_compositor.CreateShapeVisual();
        m_spotlight.Size({ rDip * 2 * zoom, rDip * 2 * zoom });
        m_spotlight.AnchorPoint({ 0.5f, 0.5f });
        m_spotlight.Shapes().Append(m_circleShape);
        layer.Children().InsertAtTop(m_spotlight);

        // Dim color (source)
        m_dimColorBrush = m_compositor.CreateColorBrush(m_backgroundColor);
        // Radial gradient mask (center transparent, outer opaque)
        // Fixed feather width: 1px for radius < 300, 2px for radius >= 300
        const float featherPixels = (m_sonarRadius >= 300) ? 2.0f : 1.0f;
        const float featherOffset = 1.0f - featherPixels / (rDip * zoom);
        m_spotlightMaskGradient = m_compositor.CreateRadialGradientBrush();
        m_spotlightMaskGradient.MappingMode(muxc::CompositionMappingMode::Absolute);
        m_maskStopCenter = m_compositor.CreateColorGradientStop();
        m_maskStopCenter.Offset(0.0f);
        m_maskStopCenter.Color(winrt::Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0));
        m_maskStopInner = m_compositor.CreateColorGradientStop();
        m_maskStopInner.Offset(featherOffset);
        m_maskStopInner.Color(winrt::Windows::UI::ColorHelper::FromArgb(0, 0, 0, 0));
        m_maskStopOuter = m_compositor.CreateColorGradientStop();
        m_maskStopOuter.Offset(1.0f);
        m_maskStopOuter.Color(winrt::Windows::UI::ColorHelper::FromArgb(255, 255, 255, 255));
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopCenter);
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopInner);
        m_spotlightMaskGradient.ColorStops().Append(m_maskStopOuter);
        m_spotlightMaskGradient.EllipseCenter({ rDip * zoom, rDip * zoom });
        m_spotlightMaskGradient.EllipseRadius({ rDip * zoom, rDip * zoom });

        m_maskBrush = m_compositor.CreateMaskBrush();
        m_maskBrush.Source(m_dimColorBrush);
        m_maskBrush.Mask(m_spotlightMaskGradient);

        m_backdrop = m_compositor.CreateSpriteVisual();
        m_backdrop.RelativeSizeAdjustment({ 1.0f, 1.0f });
        m_backdrop.Brush(m_maskBrush);
        layer.Children().InsertAtTop(m_backdrop);

        // 5) Implicit opacity animation on the root
        m_animation = m_compositor.CreateScalarKeyFrameAnimation();
        m_animation.Target(L"Opacity");
        m_animation.InsertExpressionKeyFrame(1.0f, L"this.FinalValue");
        m_animation.Duration(std::chrono::milliseconds{ m_fadeDuration });
        auto collection = m_compositor.CreateImplicitAnimationCollection();
        collection.Insert(L"Opacity", m_animation);
        m_root.ImplicitAnimations(collection);

        // 6) Spotlight radius shrinks as opacity increases (expression animation)
        SetupRadiusAnimations(rDip * zoom, rDip, featherPixels);

        // Composition created successfully
        return true;
    }
    catch (const winrt::hresult_error& e)
    {
        Logger::error("Failed to create FindMyMouse visual: {}", winrt::to_string(e.message()));
        return false;
    }

    void OnOpacityAnimationCompleted()
    {
        if (m_root.Opacity() < 0.01f)
        {
            ShowWindow(m_hwnd, SW_HIDE);
        }
    }

    // Helper to setup radius and feather expression animations
    void SetupRadiusAnimations(float startRadiusDip, float endRadiusDip, float featherPixels)
    {
        // Radius expression: shrinks from startRadiusDip to endRadiusDip as opacity goes 0->1
        auto radiusExpression = m_compositor.CreateExpressionAnimation();
        radiusExpression.SetReferenceParameter(L"Root", m_root);
        wchar_t expressionText[256];
        winrt::check_hresult(StringCchPrintfW(
            expressionText, ARRAYSIZE(expressionText), L"Lerp(Vector2(%.1f, %.1f), Vector2(%.1f, %.1f), Root.Opacity)", startRadiusDip, startRadiusDip, endRadiusDip, endRadiusDip));
        radiusExpression.Expression(expressionText);
        m_spotlightMaskGradient.StartAnimation(L"EllipseRadius", radiusExpression);

        // Feather expression: maintains fixed pixel width as radius changes
        auto featherExpression = m_compositor.CreateExpressionAnimation();
        featherExpression.SetReferenceParameter(L"Root", m_root);
        wchar_t featherExpressionText[256];
        winrt::check_hresult(StringCchPrintfW(
            featherExpressionText, ARRAYSIZE(featherExpressionText), L"1.0f - %.1ff / Lerp(%.1ff, %.1ff, Root.Opacity)", featherPixels, startRadiusDip, endRadiusDip));
        featherExpression.Expression(featherExpressionText);
        m_maskStopInner.StartAnimation(L"Offset", featherExpression);

        // Circle geometry radius for visual consistency
        if (m_circleGeometry)
        {
            auto radiusExpression2 = m_compositor.CreateExpressionAnimation();
            radiusExpression2.SetReferenceParameter(L"Root", m_root);
            radiusExpression2.Expression(expressionText);
            m_circleGeometry.StartAnimation(L"Radius", radiusExpression2);
        }
    }

    void UpdateIslandSize()
    {
        if (!m_island)
            return;

        RECT rc{};
        if (!GetClientRect(m_hwnd, &rc))
            return;

        const int width = rc.right - rc.left;
        const int height = rc.bottom - rc.top;

        auto bridge = m_island.SiteBridge();
        bridge.MoveAndResize(winrt::Windows::Graphics::RectInt32{ 0, 0, width, height });
    }

public:
    void ApplySettings(const FindMyMouseSettings& settings, bool applyToRuntimeObjects)
    {
        if (!applyToRuntimeObjects)
        {
            m_sonarRadius = settings.spotlightRadius;
            m_sonarRadiusFloat = static_cast<float>(m_sonarRadius);
            m_backgroundColor = settings.backgroundColor;
            m_spotlightColor = settings.spotlightColor;
            m_activationMethod = settings.activationMethod;
            m_includeWinKey = settings.includeWinKey;
            m_doNotActivateOnGameMode = settings.doNotActivateOnGameMode;
            m_fadeDuration = settings.animationDurationMs > 0 ? settings.animationDurationMs : 1;
            m_sonarZoomFactor = settings.spotlightInitialZoom;
            m_excludedApps = settings.excludedApps;
            m_shakeMinimumDistance = settings.shakeMinimumDistance;
            m_shakeIntervalMs = settings.shakeIntervalMs;
            m_shakeFactor = settings.shakeFactor;
            m_hapticFeedback.Configure(settings.hapticsEnabled);
            UpdateHapticInputWindows();
        }
        else
        {
            if (m_dispatcherQueueController == nullptr)
            {
                Logger::warn("Tried accessing the dispatch queue controller before it was initialized.");
                return;
            }
            auto dispatcherQueue = m_dispatcherQueueController.DispatcherQueue();
            FindMyMouseSettings localSettings = settings;
            bool enqueueSucceeded = dispatcherQueue.TryEnqueue([=]() {
                if (!m_destroyed)
                {
                    m_sonarRadius = localSettings.spotlightRadius;
                    m_sonarRadiusFloat = static_cast<float>(m_sonarRadius);
                    m_backgroundColor = localSettings.backgroundColor;
                    m_spotlightColor = localSettings.spotlightColor;
                    m_activationMethod = localSettings.activationMethod;
                    m_includeWinKey = localSettings.includeWinKey;
                    m_doNotActivateOnGameMode = localSettings.doNotActivateOnGameMode;
                    m_fadeDuration = localSettings.animationDurationMs > 0 ? localSettings.animationDurationMs : 1;
                    m_sonarZoomFactor = localSettings.spotlightInitialZoom;
                    m_excludedApps = localSettings.excludedApps;
                    m_shakeMinimumDistance = localSettings.shakeMinimumDistance;
                    m_shakeIntervalMs = localSettings.shakeIntervalMs;
                    m_shakeFactor = localSettings.shakeFactor;
                    m_hapticFeedback.Configure(localSettings.hapticsEnabled);
                    UpdateHapticInputWindows();
                    UpdateMouseSnooping(); // For the shake mouse activation method

                    // Apply new settings to runtime composition objects.
                    if (m_dimColorBrush)
                    {
                        m_dimColorBrush.Color(m_backgroundColor);
                    }
                    if (m_circleShape)
                    {
                        if (auto brush = m_circleShape.FillBrush().try_as<muxc::CompositionColorBrush>())
                        {
                            brush.Color(m_spotlightColor);
                        }
                    }
                    const float scale = static_cast<float>(m_surface.XamlRoot().RasterizationScale());
                    const float rDip = m_sonarRadiusFloat / scale;
                    const float zoom = static_cast<float>(m_sonarZoomFactor);
                    const float featherPixels = (m_sonarRadius >= 300) ? 2.0f : 1.0f;
                    const float startRadiusDip = rDip * zoom;
                    m_spotlightMaskGradient.StopAnimation(L"EllipseRadius");
                    m_maskStopInner.StopAnimation(L"Offset");
                    if (m_circleGeometry)
                    {
                        m_circleGeometry.StopAnimation(L"Radius");
                    }
                    m_spotlightMaskGradient.EllipseCenter({ startRadiusDip, startRadiusDip });
                    if (m_spotlight)
                    {
                        m_spotlight.Size({ rDip * 2 * zoom, rDip * 2 * zoom });
                        m_circleShape.Offset({ startRadiusDip, startRadiusDip });
                    }
                    SetupRadiusAnimations(startRadiusDip, rDip, featherPixels);
                }
            });
            if (!enqueueSucceeded)
            {
                Logger::error("Couldn't enqueue message to update the sonar settings.");
            }
        }
    }

private:
    muxc::Compositor m_compositor{ nullptr };
    muxxh::DesktopWindowXamlSource m_island{ nullptr };
    muxxc::Grid m_surface{ nullptr };

    muxc::ContainerVisual m_root{ nullptr };
    muxc::CompositionCommitBatch m_batch{ nullptr };
    muxc::SpriteVisual m_backdrop{ nullptr };
    // Spotlight shape visuals
    muxc::CompositionEllipseGeometry m_circleGeometry{ nullptr };
    muxc::ShapeVisual m_spotlight{ nullptr };
    muxc::CompositionSpriteShape m_circleShape{ nullptr };
    // Radial gradient mask components
    muxc::CompositionMaskBrush m_maskBrush{ nullptr };
    muxc::CompositionColorBrush m_dimColorBrush{ nullptr };
    muxc::CompositionRadialGradientBrush m_spotlightMaskGradient{ nullptr };
    muxc::CompositionColorGradientStop m_maskStopCenter{ nullptr };
    muxc::CompositionColorGradientStop m_maskStopInner{ nullptr };
    muxc::CompositionColorGradientStop m_maskStopOuter{ nullptr };
    winrt::Windows::UI::Color m_backgroundColor = FIND_MY_MOUSE_DEFAULT_BACKGROUND_COLOR;
    winrt::Windows::UI::Color m_spotlightColor = FIND_MY_MOUSE_DEFAULT_SPOTLIGHT_COLOR;
    muxc::ScalarKeyFrameAnimation m_animation{ nullptr };
};

#pragma endregion Super_Sonar_Base_Code

#pragma region Super_Sonar_API

CompositionSpotlight* m_sonar = nullptr;
void FindMyMouseApplySettings(const FindMyMouseSettings& settings)
{
    if (m_sonar != nullptr)
    {
        m_sonar->ApplySettings(settings, true);
    }
}

void FindMyMouseDisable()
{
    if (m_sonar != nullptr)
    {
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

    InitializeWinhookEventIds();

    MSG msg;

    // Main message loop:
    while (GetMessage(&msg, nullptr, 0, 0))
    {
        TranslateMessage(&msg);
        DispatchMessage(&msg);
    }

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