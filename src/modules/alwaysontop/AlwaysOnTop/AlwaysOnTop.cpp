#include "pch.h"
#include "AlwaysOnTop.h"

#include <filesystem>
#include <string>
#include <cstring>

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

#ifndef EVENT_OBJECT_COMMAND
#define EVENT_OBJECT_COMMAND 0x8010
#endif

// Raised when a window's system menu is about to be displayed.
#ifndef EVENT_SYSTEM_MENUPOPUPSTART
#define EVENT_SYSTEM_MENUPOPUPSTART 0x0006
#endif


namespace NonLocalizable
{
    const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";
    const static wchar_t* WINDOW_IS_PINNED_PROP = L"AlwaysOnTop_Pinned";
    const static UINT ALWAYS_ON_TOP_MENU_ITEM_ID = 0x1000;
}

bool isExcluded(HWND window)
{
    auto processPath = get_process_path(window);
    CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

    return check_excluded_app(window, processPath, AlwaysOnTopSettings::settings().excludedApps);
}

AlwaysOnTop::AlwaysOnTop(bool useLLKH, DWORD mainThreadId) :
    SettingsObserver({SettingId::FrameEnabled, SettingId::Hotkey, SettingId::ExcludeApps}),
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

        if (HWND foreground = GetForegroundWindow())
        {
            EnsureSystemMenuForWindow(foreground);
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
    {
        RegisterHotkey();
    }
    break;
    case SettingId::FrameEnabled:
    {
        if (AlwaysOnTopSettings::settings().enableFrame)
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
        }
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
                ProcessCommandWithSource(fw, L"hotkey");
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
    
    return 0;
}

void AlwaysOnTop::ProcessCommand(HWND window)
{
    bool gameMode = detect_game_mode();
    if (AlwaysOnTopSettings::settings().blockInGameMode && gameMode)
    {
        return;
    }

    if (isExcluded(window))
    {
        return;
    }

    Sound::Type soundType = Sound::Type::Off;
    bool topmost = IsTopmost(window);
    Logger::trace(L"[AOT] ProcessCommand toggle start hwnd={:#x} topmost={}", reinterpret_cast<uintptr_t>(window), topmost);
    if (topmost)
    {
        if (UnpinTopmostWindow(window))
        {
            auto iter = m_topmostWindows.find(window);
            if (iter != m_topmostWindows.end())
            {
                m_topmostWindows.erase(iter);
            }

            // Restore transparency when unpinning
            RestoreWindowAlpha(window);
            m_windowOriginalLayeredState.erase(window);

            Trace::AlwaysOnTop::UnpinWindow();
            Logger::trace(L"[AOT] Unpinned hwnd={:#x}", reinterpret_cast<uintptr_t>(window));
        }
    }
    else
    {
        if (PinTopmostWindow(window))
        {
            soundType = Sound::Type::On;
            AssignBorder(window);
            
            Trace::AlwaysOnTop::PinWindow();
            Logger::trace(L"[AOT] Pinned hwnd={:#x}", reinterpret_cast<uintptr_t>(window));
        }
    }

    if (AlwaysOnTopSettings::settings().enableSound)
    {
        m_sound.Play(soundType);    
    }

    EnsureSystemMenuForWindow(window);
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
    if (m_virtualDesktopUtils.IsWindowOnCurrentDesktop(window) && AlwaysOnTopSettings::settings().enableFrame)
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

    // Register pin hotkey
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::Pin), AlwaysOnTopSettings::settings().hotkey.get_modifiers(), AlwaysOnTopSettings::settings().hotkey.get_code());

    // Register transparency hotkeys using the same modifiers as the pin hotkey
    UINT modifiers = AlwaysOnTopSettings::settings().hotkey.get_modifiers();
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::IncreaseOpacity), modifiers, VK_OEM_PLUS);
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::DecreaseOpacity), modifiers, VK_OEM_MINUS);
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
                    ProcessCommandWithSource(fw, L"llkh");
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
    std::array<DWORD, 10> events_to_subscribe = {
        EVENT_OBJECT_LOCATIONCHANGE,
        EVENT_SYSTEM_MINIMIZESTART,
        EVENT_SYSTEM_MINIMIZEEND,
        EVENT_SYSTEM_MOVESIZEEND,
        EVENT_SYSTEM_FOREGROUND,
        EVENT_OBJECT_DESTROY,
        EVENT_OBJECT_FOCUS,
        EVENT_OBJECT_INVOKED,
        EVENT_OBJECT_COMMAND,
        EVENT_SYSTEM_MENUPOPUPSTART,
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
    m_windowOriginalLayeredState.clear();
}

void AlwaysOnTop::CleanUp()
{
    UnpinAll();
    if (m_window)
    {
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

void AlwaysOnTop::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    if (!data || !data->hwnd)
    {
        return;
    }

    if (data->event == EVENT_SYSTEM_FOREGROUND || data->event == EVENT_SYSTEM_MENUPOPUPSTART)
    {
        HWND target = ResolveMenuTargetWindow(data->hwnd);
        Logger::trace(L"[AOT:SystemMenu] Ensure on event {} (src={:#x}, target={:#x})", data->event, reinterpret_cast<uintptr_t>(data->hwnd), reinterpret_cast<uintptr_t>(target));
        EnsureSystemMenuForWindow(target);
    }

    if ((data->event == EVENT_OBJECT_INVOKED || data->event == EVENT_OBJECT_COMMAND) &&
        data->idChild == static_cast<LONG>(NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID))
    {
        HWND target = ResolveMenuTargetWindow(data->hwnd);
        Logger::trace(L"System menu click captured (event={}, src={:#x}, target={:#x})", data->event, reinterpret_cast<uintptr_t>(data->hwnd), reinterpret_cast<uintptr_t>(target));
        auto hasItem = [](HWND w) {
            if (!w)
            {
                return false;
            }
            HMENU m = GetSystemMenu(w, FALSE);
            return m && GetMenuState(m, NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID, MF_BYCOMMAND) != static_cast<UINT>(-1);
        };

        if (!hasItem(target))
        {
            HWND fg = GetForegroundWindow();
            HWND fgRoot = fg ? GetAncestor(fg, GA_ROOT) : nullptr;
            Logger::trace(L"[AOT:SystemMenu] Fallback to foreground (src={:#x}, fg={:#x}, fgRoot={:#x})",
                          reinterpret_cast<uintptr_t>(data->hwnd),
                          reinterpret_cast<uintptr_t>(fg),
                          reinterpret_cast<uintptr_t>(fgRoot));
            if (hasItem(fgRoot))
            {
                target = fgRoot;
            }
        }

        HMENU systemMenu = GetSystemMenu(target, FALSE);
        if (systemMenu && GetMenuState(systemMenu, NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID, MF_BYCOMMAND) != static_cast<UINT>(-1))
        {
            ProcessCommandWithSource(target, L"systemmenu");
            EnsureSystemMenuForWindow(target);
        }
        else
        {
            Logger::trace(L"Menu click ignored; menu item not present (target={:#x})", reinterpret_cast<uintptr_t>(target));
        }
        return;
    }

    if (!AlwaysOnTopSettings::settings().enableFrame)
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

void AlwaysOnTop::ProcessCommandWithSource(HWND window, const wchar_t* sourceTag)
{
    Logger::trace(L"[AOT] ProcessCommand source={} hwnd={:#x}", sourceTag ? sourceTag : L"unknown", reinterpret_cast<uintptr_t>(window));
    ProcessCommand(window);
}

bool AlwaysOnTop::ShouldInjectSystemMenu(HWND window) const noexcept
{
    if (!window || !IsWindow(window))
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: invalid window handle");
        return false;
    }

    // Only consider top-level, visible windows that expose a system menu.
    LONG style = GetWindowLong(window, GWL_STYLE);
    if ((style & WS_SYSMENU) == 0 || (style & WS_CHILD) == WS_CHILD)
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: missing WS_SYSMENU or is child (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return false;
    }

    if (!IsWindowVisible(window))
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: not visible (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return false;
    }

    if (GetAncestor(window, GA_ROOT) != window)
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: not root window (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return false;
    }

    char className[256]{};
    if (GetClassNameA(window, className, ARRAYSIZE(className)) && is_system_window(window, className))
    {
        const std::wstring classNameW{ std::wstring(className, className + std::strlen(className)) };
        Logger::trace(L"[AOT:SystemMenu] Skip: system window class {}", classNameW);
        return false;
    }

    if (isExcluded(window))
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: user excluded (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return false;
    }

    DWORD processId = 0;
    GetWindowThreadProcessId(window, &processId);
    if (processId == GetCurrentProcessId())
    {
        Logger::trace(L"[AOT:SystemMenu] Skip: PowerToys process (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return false;
    }

    auto processPath = get_process_path(window);
    if (!processPath.empty())
    {
        const std::filesystem::path path{ processPath };
        const auto fileName = path.filename().wstring();

        if (_wcsnicmp(fileName.c_str(), L"PowerToys", 9) == 0 ||
            _wcsicmp(fileName.c_str(), L"PowerLauncher.exe") == 0)
        {
            Logger::trace(L"[AOT:SystemMenu] Skip: PowerToys executable {}", fileName.c_str());
            return false;
        }
    }

    return true;
}

void AlwaysOnTop::UpdateSystemMenuItemState(HWND window, HMENU systemMenu) const noexcept
{
    if (!systemMenu)
    {
        Logger::trace(L"[AOT:SystemMenu] Update state skipped: null menu");
        return;
    }

    const UINT state = IsTopmost(window) ? MF_CHECKED : MF_UNCHECKED;
    CheckMenuItem(systemMenu, NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID, MF_BYCOMMAND | state);
}

HWND AlwaysOnTop::ResolveMenuTargetWindow(HWND window) const noexcept
{
    if (!window)
    {
        return nullptr;
    }

    HWND candidate = window;
    auto log_choice = [&](const wchar_t* stage, HWND hwnd) {
        Logger::trace(L"[AOT:SystemMenu] Resolve target: {} -> {:#x}", stage, reinterpret_cast<uintptr_t>(hwnd));
    };

    LONG style = GetWindowLong(candidate, GWL_STYLE);
    if ((style & WS_SYSMENU) == 0 || (style & WS_CHILD) == WS_CHILD)
    {
        HWND owner = GetWindow(candidate, GW_OWNER);
        if (owner)
        {
            candidate = owner;
            log_choice(L"GW_OWNER", candidate);
        }
        else
        {
            candidate = GetAncestor(window, GA_ROOTOWNER);
            log_choice(L"GA_ROOTOWNER", candidate);
        }

        if (!candidate)
        {
            candidate = GetAncestor(window, GA_ROOT);
            log_choice(L"GA_ROOT", candidate);
        }
        if (!candidate)
        {
            candidate = GetForegroundWindow();
            log_choice(L"Foreground fallback", candidate);
        }
    }

    return candidate;
}

void AlwaysOnTop::EnsureSystemMenuForWindow(HWND window)
{
    Logger::trace(L"[AOT:SystemMenu] Ensure request (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));

    if (!ShouldInjectSystemMenu(window))
    {
        return;
    }

    HMENU systemMenu = GetSystemMenu(window, FALSE);
    if (!systemMenu)
    {
        Logger::trace(L"[AOT:SystemMenu] GetSystemMenu failed (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        return;
    }

    // Insert menu item once per window.
    if (GetMenuState(systemMenu, NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID, MF_BYCOMMAND) == static_cast<UINT>(-1))
    {
        Logger::trace(L"[AOT:SystemMenu] Inserting menu item (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
        int itemCount = GetMenuItemCount(systemMenu);
        if (itemCount == -1)
        {
            Logger::trace(L"[AOT:SystemMenu] GetMenuItemCount failed (hwnd={:#x}, lastError={})", reinterpret_cast<uintptr_t>(window), GetLastError());
            return;
        }

        int insertPos = itemCount;
        for (int i = 0; i < itemCount; ++i)
        {
            if (GetMenuItemID(systemMenu, i) == SC_CLOSE)
            {
                insertPos = i;
                break;
            }
        }

        // Add a separator only if the previous item is not already a separator
        if (insertPos > 0)
        {
            MENUITEMINFOW prevInfo{};
            prevInfo.cbSize = sizeof(MENUITEMINFOW);
            prevInfo.fMask = MIIM_FTYPE;
            if (GetMenuItemInfoW(systemMenu, insertPos - 1, TRUE, &prevInfo) && !(prevInfo.fType & MFT_SEPARATOR))
            {
                InsertMenuW(systemMenu, insertPos, MF_BYPOSITION | MF_SEPARATOR, 0, nullptr);
                ++insertPos;
            }
        }

        const std::wstring menuLabel = GET_RESOURCE_STRING_FALLBACK(IDS_SYSTEM_MENU_ALWAYS_ON_TOP, L"Always on top");
        InsertMenuW(systemMenu,
                    insertPos + 1,
                    MF_BYPOSITION | MF_STRING,
                    NonLocalizable::ALWAYS_ON_TOP_MENU_ITEM_ID,
                    menuLabel.c_str());

        DrawMenuBar(window);
    }
    else
    {
        Logger::trace(L"[AOT:SystemMenu] Already present, updating state only (hwnd={:#x})", reinterpret_cast<uintptr_t>(window));
    }

    UpdateSystemMenuItemState(window, systemMenu);
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

        if (AlwaysOnTopSettings::settings().enableSound)
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
