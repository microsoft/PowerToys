#include "pch.h"
#include "AlwaysOnTop.h"

#include <common/display/dpi_aware.h>
#include <common/utils/game_mode.h>
#include <common/utils/excluded_apps.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/utils/process_path.h>

#include <common/utils/elevation.h>
#include <Generated Files/resource.h>

#include <interop/shared_constants.h>

#include <trace.h>
#include <WinHookEventIDs.h>

#include <algorithm>
#include <cmath>
#include <optional>

namespace NonLocalizable
{
    const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";
    const static wchar_t* WINDOW_IS_PINNED_PROP = L"AlwaysOnTop_Pinned";
    constexpr UINT_PTR CURSOR_DODGE_TIMER_ID = 1;
    constexpr UINT CURSOR_DODGE_DEFAULT_TIMER_INTERVAL_MS = 16;
    constexpr ULONGLONG CURSOR_DODGE_ANIMATION_DURATION_MS = 180;
    constexpr int CURSOR_DODGE_TRIGGER_DISTANCE = 48;
    constexpr ULONGLONG CURSOR_DODGE_COOLDOWN_MS = 600;
    constexpr UINT SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND = 0xEFE0;
    constexpr ULONG_PTR SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND_OWNER_TAG = 0x414F5450;
    constexpr DWORD SYSTEM_EVENT_MENU_POPUP_START = 0x0006;
    constexpr DWORD SYSTEM_EVENT_MENU_POPUP_END = 0x0007;
}

namespace
{
    enum class WindowCorner : int
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
    };

    struct CandidateCorner
    {
        POINT origin{};
        WindowCorner corner{};
        long long distanceSquared{};
    };

    UINT GetCursorDodgeTimerIntervalMs() noexcept
    {
        const auto settings = AlwaysOnTopSettings::settings();
        if (!settings)
        {
            return NonLocalizable::CURSOR_DODGE_DEFAULT_TIMER_INTERVAL_MS;
        }

        return static_cast<UINT>(std::clamp(settings->cursorDodgeAnimationIntervalMs, 8, 100));
    }

    void UnsubscribeEvents(std::vector<HWINEVENTHOOK>& hooks) noexcept
    {
        for (const auto hook : hooks)
        {
            if (hook)
            {
                UnhookWinEvent(hook);
            }
        }

        hooks.clear();
    }

    bool HasMenuCommand(HMENU menu, UINT commandId) noexcept
    {
        return menu && GetMenuState(menu, commandId, MF_BYCOMMAND) != static_cast<UINT>(-1);
    }

    bool IsAlwaysOnTopMenuCommand(HMENU menu) noexcept
    {
        if (!HasMenuCommand(menu, NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND))
        {
            return false;
        }

        MENUITEMINFOW menuItemInfo{};
        menuItemInfo.cbSize = sizeof(menuItemInfo);
        menuItemInfo.fMask = MIIM_DATA;

        return GetMenuItemInfoW(menu,
                                NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND,
                                FALSE,
                                &menuItemInfo) &&
               menuItemInfo.dwItemData == NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND_OWNER_TAG;
    }

    bool GetWindowWorkAreaCandidates(HWND window, std::array<CandidateCorner, 4>& candidates, WindowCorner& currentCorner) noexcept
    {
        RECT windowRect{};
        if (!GetWindowRect(window, &windowRect))
        {
            return false;
        }

        MONITORINFO monitorInfo{};
        monitorInfo.cbSize = sizeof(monitorInfo);

        const auto monitor = MonitorFromRect(&windowRect, MONITOR_DEFAULTTONEAREST);
        if (!monitor || !GetMonitorInfoW(monitor, &monitorInfo))
        {
            return false;
        }

        const int windowWidth = windowRect.right - windowRect.left;
        const int windowHeight = windowRect.bottom - windowRect.top;
        const auto settings = AlwaysOnTopSettings::settings();
        const int paddingHorizontal = settings ? std::clamp(settings->cursorDodgeCornerPaddingHorizontal, 0, 200) : 10;
        const int paddingVertical = settings ? std::clamp(settings->cursorDodgeCornerPaddingVertical, 0, 200) : 10;
        const int minLeft = monitorInfo.rcWork.left + paddingHorizontal;
        const int minTop = monitorInfo.rcWork.top + paddingVertical;
        const int maxLeft = (std::max)(minLeft, static_cast<int>(monitorInfo.rcWork.right) - windowWidth - paddingHorizontal);
        const int maxTop = (std::max)(minTop, static_cast<int>(monitorInfo.rcWork.bottom) - windowHeight - paddingVertical);

        candidates = {
            CandidateCorner{ POINT{ minLeft, minTop }, WindowCorner::TopLeft, 0 },
            CandidateCorner{ POINT{ maxLeft, minTop }, WindowCorner::TopRight, 0 },
            CandidateCorner{ POINT{ minLeft, maxTop }, WindowCorner::BottomLeft, 0 },
            CandidateCorner{ POINT{ maxLeft, maxTop }, WindowCorner::BottomRight, 0 },
        };

        const auto getDistanceToCorner = [&](const CandidateCorner& candidate) -> long long {
            const long long dx = static_cast<long long>(windowRect.left) - candidate.origin.x;
            const long long dy = static_cast<long long>(windowRect.top) - candidate.origin.y;
            return (dx * dx) + (dy * dy);
        };

        currentCorner = candidates.front().corner;
        long long bestDistance = getDistanceToCorner(candidates.front());
        for (const auto& candidate : candidates)
        {
            const auto distance = getDistanceToCorner(candidate);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                currentCorner = candidate.corner;
            }
        }

        return true;
    }

    long long GetDistanceSquared(const POINT& a, const POINT& b) noexcept
    {
        const long long dx = static_cast<long long>(a.x) - b.x;
        const long long dy = static_cast<long long>(a.y) - b.y;
        return (dx * dx) + (dy * dy);
    }

    double GetDistance(const POINT& a, const POINT& b) noexcept
    {
        return std::sqrt(static_cast<double>(GetDistanceSquared(a, b)));
    }

    bool IsCursorNearRect(const RECT& rect, const POINT& cursorPos) noexcept
    {
        RECT triggerRect = rect;
        InflateRect(&triggerRect, NonLocalizable::CURSOR_DODGE_TRIGGER_DISTANCE, NonLocalizable::CURSOR_DODGE_TRIGGER_DISTANCE);
        return PtInRect(&triggerRect, cursorPos);
    }

    constexpr double EaseOutCubic(double progress) noexcept
    {
        const double inverted = 1.0 - progress;
        return 1.0 - (inverted * inverted * inverted);
    }

    constexpr int Interpolate(int start, int end, double progress) noexcept
    {
        return start + static_cast<int>((end - start) * progress);
    }

    double GetDodgeScore(HWND window,
                         const CandidateCorner& candidate,
                         const POINT& candidateCenter,
                         const POINT& cursorPos,
                         const POINT& currentCenter,
                         const std::optional<WindowCorner>& previousCorner,
                         ULONGLONG tick) noexcept
    {
        const double cursorDistance = GetDistance(candidateCenter, cursorPos);
        const double travelDistance = GetDistance(candidateCenter, currentCenter);
        const double previousCornerPenalty = previousCorner && candidate.corner == *previousCorner ? 500.0 : 0.0;
        const auto jitterSeed = (reinterpret_cast<UINT_PTR>(window) >> 4) ^ tick ^ (static_cast<UINT_PTR>(static_cast<int>(candidate.corner)) * 1103515245u);
        const double jitter = static_cast<double>(static_cast<int>(jitterSeed % 31) - 15);

        return cursorDistance - (travelDistance * 0.25) - previousCornerPenalty + jitter;
    }
}

bool isExcluded(HWND window)
{
    auto processPath = get_process_path(window);
    CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

    const auto settings = AlwaysOnTopSettings::settings();
    return check_excluded_app(window, processPath, settings->excludedApps);
}

AlwaysOnTop::AlwaysOnTop(bool useLLKH, DWORD mainThreadId) :
    SettingsObserver({ SettingId::FrameEnabled, SettingId::Hotkey, SettingId::IncreaseOpacityHotkey, SettingId::DecreaseOpacityHotkey, SettingId::ExcludeApps, SettingId::ShowInSystemMenu, SettingId::CursorDodgeEnabled, SettingId::CursorDodgeAnimationInterval }),
    m_hinstance(reinterpret_cast<HINSTANCE>(&__ImageBase)),
    m_useCentralizedLLKH(useLLKH),
    m_mainThreadId(mainThreadId),
    m_notificationUtil(std::make_unique<notifications::NotificationUtil>())
{
    s_instance = this;
    DPIAware::EnableDPIAwarenessForThisProcess();

    if (InitMainWindow())
    {
        InitializeWinhookEventIds();

        AlwaysOnTopSettings::instance().InitFileWatcher();
        AlwaysOnTopSettings::instance().LoadSettings();

        RegisterHotkey();
        RegisterLLKH();
        
        SubscribeToEvents();
        StartTrackingTopmostWindows();

        if (HWND foregroundWindow = GetForegroundWindow())
        {
            UpdateSystemMenuItem(foregroundWindow);
        }
    }
    else
    {
        Logger::error("Failed to init AlwaysOnTop module");
        // TODO: show localized message
    }
}

AlwaysOnTop::~AlwaysOnTop()
{
    m_running = false;
    m_notificationUtil.reset();

    if (m_hPinEvent)
    {
        // Needed to unblock MsgWaitForMultipleObjects one last time
        SetEvent(m_hPinEvent);
        CloseHandle(m_hPinEvent);
    }
    m_thread.join();

    CleanUp();
}

bool AlwaysOnTop::InitMainWindow()
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = WndProc_Helper;
    wcex.hInstance = m_hinstance;
    wcex.lpszClassName = NonLocalizable::TOOL_WINDOW_CLASS_NAME;
    RegisterClassExW(&wcex);

    m_window = CreateWindowExW(WS_EX_TOOLWINDOW, NonLocalizable::TOOL_WINDOW_CLASS_NAME, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, m_hinstance, this);
    if (!m_window)
    {
        Logger::error(L"Failed to create AlwaysOnTop window: {}", get_last_error_or_default(GetLastError()));
        return false;
    }

    return true;
}

void AlwaysOnTop::SettingsUpdate(SettingId id)
{
    switch (id)
    {
    case SettingId::Hotkey:
    case SettingId::IncreaseOpacityHotkey:
    case SettingId::DecreaseOpacityHotkey:
    {
        RegisterHotkey();
    }
    break;
    case SettingId::CursorDodgeEnabled:
    case SettingId::CursorDodgeAnimationInterval:
    {
        UpdateCursorDodgeTimerInterval();
    }
    break;
    case SettingId::FrameEnabled:
    {
        const auto settings = AlwaysOnTopSettings::settings();
        if (settings->enableFrame)
        {
            for (auto& iter : m_topmostWindows)
            {
                if (!iter.second)
                {
                    AssignBorder(iter.first);
                }
            }
        }
        else
        {
            for (auto& iter : m_topmostWindows)
            {
                iter.second = nullptr;
            }
        }    
    }
    break;
    case SettingId::ExcludeApps:
    {
        std::vector<HWND> toErase{};
        for (const auto& [window, border] : m_topmostWindows)
        {
            if (isExcluded(window))
            {
                UnpinTopmostWindow(window);
                toErase.push_back(window);
            }
        }

        for (const auto window: toErase)
        {
            m_topmostWindows.erase(window);
            m_dodgeAnimations.erase(window);
            m_lastDodgeTicks.erase(window);
            m_previousDodgeCorners.erase(window);
            m_windowOriginalLayeredState.erase(window);
        }
    }
    break;
    case SettingId::ShowInSystemMenu:
    {
        const auto settings = AlwaysOnTopSettings::settings();
        UpdateSystemMenuEventHooks(settings->showInSystemMenu);
        m_lastSystemMenuWindow = nullptr;
        UpdateSystemMenuItem(GetForegroundWindow());
    }
    break;
    default:
        break;
    }
}

LRESULT AlwaysOnTop::WndProc(HWND window, UINT message, WPARAM wparam, LPARAM lparam) noexcept
{
    if (message == WM_HOTKEY)
    {
        int hotkeyId = static_cast<int>(wparam);
        if (HWND fw{ GetForegroundWindow() })
        {
            if (hotkeyId == static_cast<int>(HotkeyId::Pin))
            {
                ProcessCommand(fw);
            }
            else if (hotkeyId == static_cast<int>(HotkeyId::IncreaseOpacity))
            {
                StepWindowTransparency(fw, Settings::transparencyStep);
            }
            else if (hotkeyId == static_cast<int>(HotkeyId::DecreaseOpacity))
            {
                StepWindowTransparency(fw, -Settings::transparencyStep);
            }
        }
    }
    else if (message == WM_PRIV_SETTINGS_CHANGED)
    {
        AlwaysOnTopSettings::instance().LoadSettings();
    }
    else if (message == WM_TIMER && wparam == NonLocalizable::CURSOR_DODGE_TIMER_ID)
    {
        PollCursorDodge();
    }
    
    return 0;
}

void AlwaysOnTop::ProcessCommand(HWND window)
{
    bool gameMode = detect_game_mode();
    if (AlwaysOnTopSettings::settings()->blockInGameMode && gameMode)
    {
        return;
    }

    if (isExcluded(window))
    {
        return;
    }

    Sound::Type soundType = Sound::Type::Off;
    bool stateChanged = false;
    bool topmost = IsTopmost(window);
    if (topmost)
    {
        if (UnpinTopmostWindow(window))
        {
            stateChanged = true;
            auto iter = m_topmostWindows.find(window);
            if (iter != m_topmostWindows.end())
            {
                m_topmostWindows.erase(iter);
            }

            // Restore transparency when unpinning
            RestoreWindowAlpha(window);
            m_windowOriginalLayeredState.erase(window);
            m_lastDodgeTicks.erase(window);
            m_dodgeAnimations.erase(window);
            m_previousDodgeCorners.erase(window);

            Trace::AlwaysOnTop::UnpinWindow();
        }
    }
    else
    {
        if (PinTopmostWindow(window))
        {
            stateChanged = true;
            soundType = Sound::Type::On;
            AssignBorder(window);
            
            Trace::AlwaysOnTop::PinWindow();
        }
    }

    if (stateChanged && AlwaysOnTopSettings::settings()->enableSound)
    {
        m_sound.Play(soundType);    
    }

    UpdateSystemMenuItem(window);
}

void AlwaysOnTop::StartTrackingTopmostWindows()
{
    using result_t = std::vector<HWND>;
    result_t result;

    auto enumWindows = [](HWND hwnd, LPARAM param) -> BOOL {
        if (!IsWindowVisible(hwnd))
        {
            return TRUE;
        }

        if (isExcluded(hwnd))
        {
            return TRUE;
        }

        auto windowName = GetWindowTextLength(hwnd);
        if (windowName > 0)
        {
            result_t& result = *reinterpret_cast<result_t*>(param);
            result.push_back(hwnd);
        }

        return TRUE;
    };

    EnumWindows(enumWindows, reinterpret_cast<LPARAM>(&result));

    for (HWND window : result)
    {
        if (IsPinned(window))
        {
            AssignBorder(window);
        }
    }
}

bool AlwaysOnTop::AssignBorder(HWND window)
{
    if (m_virtualDesktopUtils.IsWindowOnCurrentDesktop(window) && AlwaysOnTopSettings::settings()->enableFrame)
    {
        auto border = WindowBorder::Create(window, m_hinstance);
        if (border)
        {
            m_topmostWindows[window] = std::move(border);
        }
    }
    else
    {
        m_topmostWindows[window] = nullptr;
    }
    
    return true;
}

void AlwaysOnTop::RegisterHotkey() const
{
    if (m_useCentralizedLLKH)
    {
        // All hotkeys are handled by centralized LLKH
        return;
    }

    // Register hotkeys only when not using centralized LLKH
    UnregisterHotKey(m_window, static_cast<int>(HotkeyId::Pin));
    UnregisterHotKey(m_window, static_cast<int>(HotkeyId::IncreaseOpacity));
    UnregisterHotKey(m_window, static_cast<int>(HotkeyId::DecreaseOpacity));

    const auto settings = AlwaysOnTopSettings::settings();

    // Register pin hotkey
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::Pin), settings->hotkey.get_modifiers(), settings->hotkey.get_code());

    RegisterHotKey(m_window, static_cast<int>(HotkeyId::IncreaseOpacity), settings->increaseOpacityHotkey.get_modifiers(), settings->increaseOpacityHotkey.get_code());
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::DecreaseOpacity), settings->decreaseOpacityHotkey.get_modifiers(), settings->decreaseOpacityHotkey.get_code());
}

void AlwaysOnTop::RegisterLLKH()
{
    if (!m_useCentralizedLLKH)
    {
        return;
    }
	
    m_hPinEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::ALWAYS_ON_TOP_PIN_EVENT);
    m_hTerminateEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::ALWAYS_ON_TOP_TERMINATE_EVENT);
    m_hIncreaseOpacityEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::ALWAYS_ON_TOP_INCREASE_OPACITY_EVENT);
    m_hDecreaseOpacityEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::ALWAYS_ON_TOP_DECREASE_OPACITY_EVENT);

    if (!m_hPinEvent)
    {
        Logger::warn(L"Failed to create pinEvent. {}", get_last_error_or_default(GetLastError()));
        return;
    }

    if (!m_hTerminateEvent)
    {
        Logger::warn(L"Failed to create terminateEvent. {}", get_last_error_or_default(GetLastError()));
        return;
    }

    if (!m_hIncreaseOpacityEvent)
    {
        Logger::warn(L"Failed to create increaseOpacityEvent. {}", get_last_error_or_default(GetLastError()));
    }

    if (!m_hDecreaseOpacityEvent)
    {
        Logger::warn(L"Failed to create decreaseOpacityEvent. {}", get_last_error_or_default(GetLastError()));
    }

    HANDLE handles[4] = { m_hPinEvent,
                          m_hTerminateEvent,
                          m_hIncreaseOpacityEvent,
                          m_hDecreaseOpacityEvent };

    m_thread = std::thread([this, handles]() {
        MSG msg;
        while (m_running)
        {
            DWORD dwEvt = MsgWaitForMultipleObjects(4, handles, false, INFINITE, QS_ALLINPUT);
            if (!m_running)
            {
                break;
            }
            switch (dwEvt)
            {
            case WAIT_OBJECT_0: // Pin event
                if (HWND fw{ GetForegroundWindow() })
                {
                    ProcessCommand(fw);
                }
                break;
            case WAIT_OBJECT_0 + 1: // Terminate event
                PostThreadMessage(m_mainThreadId, WM_QUIT, 0, 0);
                break;
            case WAIT_OBJECT_0 + 2: // Increase opacity event
                if (HWND fw{ GetForegroundWindow() })
                {
                    StepWindowTransparency(fw, Settings::transparencyStep);
                }
                break;
            case WAIT_OBJECT_0 + 3: // Decrease opacity event
                if (HWND fw{ GetForegroundWindow() })
                {
                    StepWindowTransparency(fw, -Settings::transparencyStep);
                }
                break;
            case WAIT_OBJECT_0 + 4: // Message queue
                if (PeekMessageW(&msg, nullptr, 0, 0, PM_REMOVE))
                {
                    TranslateMessage(&msg);
                    DispatchMessageW(&msg);
                }
                break;
            default:
                break;
            }
        }
    });
}

void AlwaysOnTop::SubscribeToEvents()
{
    // subscribe to windows events
    std::array<DWORD, 7> events_to_subscribe = {
        EVENT_OBJECT_LOCATIONCHANGE,
        EVENT_SYSTEM_MINIMIZESTART,
        EVENT_SYSTEM_MINIMIZEEND,
        EVENT_SYSTEM_MOVESIZEEND,
        EVENT_SYSTEM_FOREGROUND,
        EVENT_OBJECT_DESTROY,
        EVENT_OBJECT_FOCUS,
    };

    for (const auto event : events_to_subscribe)
    {
        auto hook = SetWinEventHook(event, event, nullptr, WinHookProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
        if (hook)
        {
            m_staticWinEventHooks.emplace_back(hook);
        }
        else
        {
            Logger::error(L"Failed to set win event hook");
        }
    }

    UpdateSystemMenuEventHooks(AlwaysOnTopSettings::settings()->showInSystemMenu);
}

void AlwaysOnTop::UpdateSystemMenuEventHooks(bool enable)
{
    constexpr std::array<DWORD, 3> menu_events_to_subscribe = {
        NonLocalizable::SYSTEM_EVENT_MENU_POPUP_START,
        NonLocalizable::SYSTEM_EVENT_MENU_POPUP_END,
        EVENT_OBJECT_INVOKED,
    };

    if (enable)
    {
        if (m_systemMenuWinEventHooks.size() == menu_events_to_subscribe.size())
        {
            return;
        }

        // Recover from any partial hook registration before re-registering.
        UnsubscribeEvents(m_systemMenuWinEventHooks);

        for (const auto event : menu_events_to_subscribe)
        {
            auto hook = SetWinEventHook(event, event, nullptr, WinHookProc, 0, 0, WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS);
            if (hook)
            {
                m_systemMenuWinEventHooks.emplace_back(hook);
            }
            else
            {
                Logger::error(L"Failed to set system menu win event hook");
            }
        }
    }
    else
    {
        UnsubscribeEvents(m_systemMenuWinEventHooks);
    }
}

void AlwaysOnTop::UpdateSystemMenuItem(HWND window) const noexcept
{
    if (!window || !IsWindow(window))
    {
        return;
    }

    const auto systemMenu = GetSystemMenu(window, false);
    if (!systemMenu)
    {
        return;
    }

    const auto settings = AlwaysOnTopSettings::settings();
    if (!settings->showInSystemMenu)
    {
        if (IsAlwaysOnTopMenuCommand(systemMenu))
        {
            RemoveMenu(systemMenu, NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND, MF_BYCOMMAND);
        }
        return;
    }

    auto text = GET_RESOURCE_STRING(IDS_SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP);
    MENUITEMINFOW menuItemInfo{};
    menuItemInfo.cbSize = sizeof(menuItemInfo);
    menuItemInfo.fMask = MIIM_ID | MIIM_STATE | MIIM_STRING | MIIM_DATA;
    menuItemInfo.wID = NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND;
    menuItemInfo.fState = IsPinned(window) ? MFS_CHECKED : MFS_UNCHECKED;
    menuItemInfo.dwTypeData = text.data();
    menuItemInfo.dwItemData = NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND_OWNER_TAG;

    if (!HasMenuCommand(systemMenu, NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND))
    {
        InsertMenuItemW(systemMenu, SC_CLOSE, FALSE, &menuItemInfo);
    }
    else if (IsAlwaysOnTopMenuCommand(systemMenu))
    {
        menuItemInfo.fMask = MIIM_STATE | MIIM_STRING;
        SetMenuItemInfoW(systemMenu, NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND, FALSE, &menuItemInfo);
    }
    else
    {
        Logger::warn(L"Skipping Always On Top system menu command registration because ID 0x{:X} is already in use by another item.",
                     NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND);
    }
}

void AlwaysOnTop::UnpinAll()
{
    for (const auto& [topWindow, border] : m_topmostWindows)
    {
        if (!UnpinTopmostWindow(topWindow))
        {
            Logger::error(L"Unpinning topmost window failed");
        }
        // Restore transparency when unpinning all
        RestoreWindowAlpha(topWindow);
    }

    m_topmostWindows.clear();
    m_lastDodgeTicks.clear();
    m_dodgeAnimations.clear();
    m_previousDodgeCorners.clear();
    m_windowOriginalLayeredState.clear();
}

void AlwaysOnTop::CleanUp()
{
    UnsubscribeEvents(m_systemMenuWinEventHooks);
    UnsubscribeEvents(m_staticWinEventHooks);

    UnpinAll();
    if (m_window)
    {
        KillTimer(m_window, NonLocalizable::CURSOR_DODGE_TIMER_ID);
        DestroyWindow(m_window);
        m_window = nullptr;
    }

    UnregisterClass(NonLocalizable::TOOL_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
}

bool AlwaysOnTop::IsTopmost(HWND window) const noexcept
{
    int exStyle = GetWindowLong(window, GWL_EXSTYLE);
    return (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;
}

bool AlwaysOnTop::IsPinned(HWND window) const noexcept
{
    auto handle = GetProp(window, NonLocalizable::WINDOW_IS_PINNED_PROP);
    return (handle != NULL);
}

bool AlwaysOnTop::PinTopmostWindow(HWND window) const noexcept
{
    if (!SetProp(window, NonLocalizable::WINDOW_IS_PINNED_PROP, reinterpret_cast<HANDLE>(1)))
    {
        Logger::error(L"SetProp failed, {}", get_last_error_or_default(GetLastError()));
    }

    auto res = SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    if (!res)
    {
        Logger::error(L"Failed to pin window, {}", get_last_error_or_default(GetLastError()));
    }

    return res;
}

bool AlwaysOnTop::UnpinTopmostWindow(HWND window) const noexcept
{
    RemoveProp(window, NonLocalizable::WINDOW_IS_PINNED_PROP);
    auto res = SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
    if (!res)
    {
        Logger::error(L"Failed to unpin window, {}", get_last_error_or_default(GetLastError()));
    }

    return res;
}

bool AlwaysOnTop::IsTracked(HWND window) const noexcept
{
    auto iter = m_topmostWindows.find(window);
    return (iter != m_topmostWindows.end());
}

void AlwaysOnTop::UpdateCursorDodgeTimerInterval()
{
    if (!m_window)
    {
        return;
    }

    const auto settings = AlwaysOnTopSettings::settings();
    if (!settings || !settings->enableCursorDodge)
    {
        // Ensure windows do not get stuck at an in-between position if dodge is disabled mid-animation.
        for (const auto& [window, animation] : m_dodgeAnimations)
        {
            if (!window || !IsWindow(window) || !IsWindowVisible(window) || !IsPinned(window) || IsIconic(window) || IsZoomed(window))
            {
                continue;
            }

            if (!SetWindowPos(window, HWND_TOPMOST, animation.target.x, animation.target.y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE))
            {
                Logger::warn(L"Failed to finalize pinned window dodge, {}", get_last_error_or_default(GetLastError()));
                continue;
            }

            auto topmostIter = m_topmostWindows.find(window);
            if (topmostIter != m_topmostWindows.end() && topmostIter->second)
            {
                topmostIter->second->UpdateBorderPosition();
            }
        }

        m_dodgeAnimations.clear();
        KillTimer(m_window, NonLocalizable::CURSOR_DODGE_TIMER_ID);
        return;
    }

    SetTimer(m_window, NonLocalizable::CURSOR_DODGE_TIMER_ID, GetCursorDodgeTimerIntervalMs(), nullptr);
}

void AlwaysOnTop::PollCursorDodge()
{
    const auto settings = AlwaysOnTopSettings::settings();
    if (!settings->enableCursorDodge)
    {
        return;
    }

    UpdateDodgeAnimations();

    if (m_topmostWindows.empty())
    {
        return;
    }

    POINT cursorPos{};
    if (!GetCursorPos(&cursorPos))
    {
        return;
    }

    for (const auto& [window, border] : m_topmostWindows)
    {
        if (TryDodgeWindow(window, cursorPos))
        {
            break;
        }
    }
}

void AlwaysOnTop::UpdateDodgeAnimations()
{
    const auto now = GetTickCount64();
    for (auto iter = m_dodgeAnimations.begin(); iter != m_dodgeAnimations.end();)
    {
        const auto window = iter->first;
        const auto& animation = iter->second;

        if (!window || !IsWindow(window) || !IsWindowVisible(window) || !IsPinned(window) || IsIconic(window) || IsZoomed(window))
        {
            iter = m_dodgeAnimations.erase(iter);
            continue;
        }

        const auto elapsed = now - animation.startTick;
        const double progress = elapsed >= NonLocalizable::CURSOR_DODGE_ANIMATION_DURATION_MS ?
                                    1.0 :
                                    static_cast<double>(elapsed) / NonLocalizable::CURSOR_DODGE_ANIMATION_DURATION_MS;
        const double easedProgress = EaseOutCubic(progress);
        const int x = Interpolate(animation.start.x, animation.target.x, easedProgress);
        const int y = Interpolate(animation.start.y, animation.target.y, easedProgress);

        if (!SetWindowPos(window, HWND_TOPMOST, x, y, 0, 0, SWP_NOSIZE | SWP_NOACTIVATE))
        {
            Logger::warn(L"Failed to animate pinned window dodge, {}", get_last_error_or_default(GetLastError()));
            iter = m_dodgeAnimations.erase(iter);
            continue;
        }

        auto topmostIter = m_topmostWindows.find(window);
        if (topmostIter != m_topmostWindows.end() && topmostIter->second)
        {
            topmostIter->second->UpdateBorderPosition();
        }

        if (progress >= 1.0)
        {
            iter = m_dodgeAnimations.erase(iter);
        }
        else
        {
            ++iter;
        }
    }
}

void AlwaysOnTop::StartDodgeAnimation(HWND window, const RECT& windowRect, const POINT& target)
{
    m_dodgeAnimations[window] = WindowDodgeAnimation{
        POINT{ windowRect.left, windowRect.top },
        target,
        GetTickCount64(),
    };
}

bool AlwaysOnTop::TryDodgeWindow(HWND window, const POINT& cursorPos)
{
    if (!window || !IsWindow(window) || !IsWindowVisible(window) || !IsPinned(window) || IsIconic(window) || IsZoomed(window))
    {
        return false;
    }

    if (m_dodgeAnimations.find(window) != m_dodgeAnimations.end())
    {
        return false;
    }

    RECT windowRect{};
    if (!GetWindowRect(window, &windowRect) || !IsCursorNearRect(windowRect, cursorPos))
    {
        return false;
    }

    const auto now = GetTickCount64();
    const auto lastMove = m_lastDodgeTicks[window];
    if ((now - lastMove) < NonLocalizable::CURSOR_DODGE_COOLDOWN_MS)
    {
        return false;
    }

    std::array<CandidateCorner, 4> candidates{};
    WindowCorner currentCorner{};
    if (!GetWindowWorkAreaCandidates(window, candidates, currentCorner))
    {
        return false;
    }

    const int windowWidth = windowRect.right - windowRect.left;
    const int windowHeight = windowRect.bottom - windowRect.top;
    const POINT currentCenter{
        windowRect.left + (windowWidth / 2),
        windowRect.top + (windowHeight / 2),
    };
    std::optional<WindowCorner> previousCorner;
    if (const auto previousCornerIter = m_previousDodgeCorners.find(window); previousCornerIter != m_previousDodgeCorners.end())
    {
        previousCorner = static_cast<WindowCorner>(previousCornerIter->second);
    }

    CandidateCorner* bestCandidate = nullptr;
    double bestScore = 0.0;
    for (auto& candidate : candidates)
    {
        const POINT candidateCenter{
            candidate.origin.x + (windowWidth / 2),
            candidate.origin.y + (windowHeight / 2),
        };

        if (candidate.corner == currentCorner)
        {
            continue;
        }

        const double score = GetDodgeScore(window, candidate, candidateCenter, cursorPos, currentCenter, previousCorner, now);
        if (!bestCandidate || score > bestScore)
        {
            bestCandidate = &candidate;
            bestScore = score;
        }
    }

    if (!bestCandidate)
    {
        return false;
    }

    m_lastDodgeTicks[window] = now;
    m_previousDodgeCorners[window] = static_cast<int>(currentCorner);
    StartDodgeAnimation(window, windowRect, bestCandidate->origin);

    return true;
}

void AlwaysOnTop::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    switch (data->event)
    {
    case NonLocalizable::SYSTEM_EVENT_MENU_POPUP_START:
    {
        if (data->idObject == OBJID_SYSMENU && data->hwnd)
        {
            m_lastSystemMenuWindow = AlwaysOnTopSettings::settings()->showInSystemMenu ? data->hwnd : nullptr;
            UpdateSystemMenuItem(data->hwnd);
        }
    }
    return;
    case NonLocalizable::SYSTEM_EVENT_MENU_POPUP_END:
    {
        if (data->idObject == OBJID_SYSMENU && data->hwnd == m_lastSystemMenuWindow)
        {
            m_lastSystemMenuWindow = nullptr;
        }
    }
    return;
    case EVENT_OBJECT_INVOKED:
    {
        if (!AlwaysOnTopSettings::settings()->showInSystemMenu)
        {
            return;
        }

        if (data->idChild != static_cast<LONG>(NonLocalizable::SYSTEM_MENU_TOGGLE_ALWAYS_ON_TOP_COMMAND))
        {
            return;
        }

        const bool isMenuRelatedObject = (data->idObject == OBJID_SYSMENU || data->idObject == OBJID_MENU || data->idObject == OBJID_CLIENT);
        if (!isMenuRelatedObject && (!m_lastSystemMenuWindow || !IsWindow(m_lastSystemMenuWindow)))
        {
            return;
        }

        const auto hasToggleMenuItem = [](HWND window) -> bool {
            if (!window || !IsWindow(window))
            {
                return false;
            }

            const auto systemMenu = GetSystemMenu(window, false);
            return systemMenu && IsAlwaysOnTopMenuCommand(systemMenu);
        };

        HWND commandWindow = nullptr;
        const auto trySetCommandWindow = [&](HWND candidate) noexcept {
            if (!commandWindow && hasToggleMenuItem(candidate))
            {
                commandWindow = candidate;
            }
        };

        if (m_lastSystemMenuWindow && IsWindow(m_lastSystemMenuWindow))
        {
            trySetCommandWindow(m_lastSystemMenuWindow);
        }
        trySetCommandWindow(data->hwnd);
        trySetCommandWindow(GetForegroundWindow());

        if (commandWindow)
        {
            ProcessCommand(commandWindow);
        }
    }
    return;
    default:
        break;
    }

    if (!AlwaysOnTopSettings::settings()->enableFrame || !data->hwnd)
    {
        return;
    }

    std::vector<HWND> toErase{};
    for (const auto& [window, border] : m_topmostWindows)
    {
        // check if the window was closed, since for some EVENT_OBJECT_DESTROY doesn't work
        // fixes https://github.com/microsoft/PowerToys/issues/15300
        bool visible = IsWindowVisible(window);
        if (!visible)
        {
            UnpinTopmostWindow(window);
            toErase.push_back(window);
        }
    }

    for (const auto window : toErase)
    {
        m_topmostWindows.erase(window);
        m_lastDodgeTicks.erase(window);
        m_dodgeAnimations.erase(window);
        m_previousDodgeCorners.erase(window);
        m_windowOriginalLayeredState.erase(window);
    }

    switch (data->event)
    {
    case EVENT_OBJECT_LOCATIONCHANGE:
    {
        auto iter = m_topmostWindows.find(data->hwnd);
        if (iter != m_topmostWindows.end())
        {
            const auto& border = iter->second;
            if (border)
            {
                border->UpdateBorderPosition();
            }
        }
    }
    break;
    case EVENT_SYSTEM_MINIMIZESTART:
    {
        auto iter = m_topmostWindows.find(data->hwnd);
        if (iter != m_topmostWindows.end())
        {
            m_topmostWindows[data->hwnd] = nullptr;
        }
    }
    break;
    case EVENT_SYSTEM_MINIMIZEEND:
    {
        auto iter = m_topmostWindows.find(data->hwnd);
        if (iter != m_topmostWindows.end())
        {
            // pin border again, in some cases topmost flag stops working: https://github.com/microsoft/PowerToys/issues/17332
            PinTopmostWindow(data->hwnd); 
            AssignBorder(data->hwnd);
        }
    }
    break;
    case EVENT_SYSTEM_MOVESIZEEND:
    {
        auto iter = m_topmostWindows.find(data->hwnd);
        if (iter != m_topmostWindows.end())
        {
            const auto& border = iter->second;
            if (border)
            {
                border->UpdateBorderPosition();
            }
        }
    }
    break;
    case EVENT_SYSTEM_FOREGROUND:
    {
        UpdateSystemMenuItem(data->hwnd);

        if (!is_process_elevated() && IsProcessOfWindowElevated(data->hwnd))
        {
            m_notificationUtil->WarnIfElevationIsRequired(GET_RESOURCE_STRING(IDS_ALWAYSONTOP),
                                                          GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED),
                                                          GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_LEARN_MORE),
                                                          GET_RESOURCE_STRING(IDS_SYSTEM_FOREGROUND_ELEVATED_DIALOG_DONT_SHOW_AGAIN));
        }
        RefreshBorders();
    }
    break;
    case EVENT_OBJECT_FOCUS:
    {
        for (const auto& [window, border] : m_topmostWindows)
        {
            // check if topmost was reset
            // fixes https://github.com/microsoft/PowerToys/issues/19168
            if (!IsTopmost(window))
            {
                Logger::trace(L"A window no longer has Topmost set and it should. Setting topmost again.");
                PinTopmostWindow(window);
            }
        }
    }
    break;
    default:
        break;
    }
}

void AlwaysOnTop::RefreshBorders()
{
    for (const auto& [window, border] : m_topmostWindows)
    {
        if (m_virtualDesktopUtils.IsWindowOnCurrentDesktop(window))
        {
            if (!border)
            {
                AssignBorder(window);
            }
        }
        else
        {
            if (border)
            {
                m_topmostWindows[window] = nullptr;
            }
        }
    }
}

HWND AlwaysOnTop::ResolveTransparencyTargetWindow(HWND window)
{
    if (!window || !IsWindow(window))
    {
        return nullptr;
    }

    // Only allow transparency changes on pinned windows
    if (!IsPinned(window))
    {
        return nullptr;
    }

    return window;
}


void AlwaysOnTop::StepWindowTransparency(HWND window, int delta)
{
    HWND targetWindow = ResolveTransparencyTargetWindow(window);
    if (!targetWindow)
    {
        return;
    }

    int currentTransparency = Settings::maxTransparencyPercentage;
    LONG exStyle = GetWindowLong(targetWindow, GWL_EXSTYLE);
    if (exStyle & WS_EX_LAYERED)
    {
        BYTE alpha = 255;
        if (GetLayeredWindowAttributes(targetWindow, nullptr, &alpha, nullptr))
        {
            currentTransparency = (alpha * 100) / 255;
        }
    }

    int newTransparency = (std::max)(Settings::minTransparencyPercentage, 
                                     (std::min)(Settings::maxTransparencyPercentage, currentTransparency + delta));

    if (newTransparency != currentTransparency)
    {
        ApplyWindowAlpha(targetWindow, newTransparency);

        if (AlwaysOnTopSettings::settings()->enableSound)
        {
            m_sound.Play(delta > 0 ? Sound::Type::IncreaseOpacity : Sound::Type::DecreaseOpacity);
        }

        Logger::debug(L"Transparency adjusted to {}%", newTransparency);
    }
}

void AlwaysOnTop::ApplyWindowAlpha(HWND window, int percentage)
{
    if (!window || !IsWindow(window))
    {
        return;
    }
    
    percentage = (std::max)(Settings::minTransparencyPercentage, 
                            (std::min)(Settings::maxTransparencyPercentage, percentage));

    LONG exStyle = GetWindowLong(window, GWL_EXSTYLE);
    bool isCurrentlyLayered = (exStyle & WS_EX_LAYERED) != 0;

    // Cache original state on first transparency application
    if (m_windowOriginalLayeredState.find(window) == m_windowOriginalLayeredState.end())
    {
        WindowLayeredState state;
        state.hadLayeredStyle = isCurrentlyLayered;
        
        if (isCurrentlyLayered)
        {
            BYTE alpha = 255;
            COLORREF colorKey = 0;
            DWORD flags = 0;
            if (GetLayeredWindowAttributes(window, &colorKey, &alpha, &flags))
            {
                state.originalAlpha = alpha;
                state.usedColorKey = (flags & LWA_COLORKEY) != 0;
                state.colorKey = colorKey;
            }
            else
            {
                Logger::warn(L"GetLayeredWindowAttributes failed for layered window, skipping");
                return;
            }
        }
        m_windowOriginalLayeredState[window] = state;
    }

    // Clear WS_EX_LAYERED first to ensure SetLayeredWindowAttributes works
    if (isCurrentlyLayered)
    {
        SetWindowLong(window, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
        SetWindowPos(window, nullptr, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        exStyle = GetWindowLong(window, GWL_EXSTYLE);
    }

    BYTE alphaValue = static_cast<BYTE>((255 * percentage) / 100);
    SetWindowLong(window, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
    SetLayeredWindowAttributes(window, 0, alphaValue, LWA_ALPHA);
}

void AlwaysOnTop::RestoreWindowAlpha(HWND window)
{
    if (!window || !IsWindow(window))
    {
        return;
    }

    LONG exStyle = GetWindowLong(window, GWL_EXSTYLE);
    auto it = m_windowOriginalLayeredState.find(window);
    
    if (it != m_windowOriginalLayeredState.end())
    {
        const auto& originalState = it->second;
        
        if (originalState.hadLayeredStyle)
        {
            // Window originally had WS_EX_LAYERED - restore original attributes
            // Clear and re-add to ensure clean state
            if (exStyle & WS_EX_LAYERED)
            {
                SetWindowLong(window, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
                exStyle = GetWindowLong(window, GWL_EXSTYLE);
            }
            SetWindowLong(window, GWL_EXSTYLE, exStyle | WS_EX_LAYERED);
            
            // Restore original alpha and/or color key
            DWORD flags = LWA_ALPHA;
            if (originalState.usedColorKey)
            {
                flags |= LWA_COLORKEY;
            }
            SetLayeredWindowAttributes(window, originalState.colorKey, originalState.originalAlpha, flags);
            SetWindowPos(window, nullptr, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
        }
        else
        {
            // Window originally didn't have WS_EX_LAYERED - remove it completely
            if (exStyle & WS_EX_LAYERED)
            {
                SetLayeredWindowAttributes(window, 0, 255, LWA_ALPHA);
                SetWindowLong(window, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
                SetWindowPos(window, nullptr, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);
            }
        }
        
        m_windowOriginalLayeredState.erase(it);
    }
    else
    {
        // Fallback: no cached state, just remove layered style
        if (exStyle & WS_EX_LAYERED)
        {
            SetLayeredWindowAttributes(window, 0, 255, LWA_ALPHA);
            SetWindowLong(window, GWL_EXSTYLE, exStyle & ~WS_EX_LAYERED);
        }
    }
}
