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

#include <dwmapi.h>
#include <ScalingUtils.h>
#include <WindowCornersUtil.h>

#include <cmath>

namespace NonLocalizable
{
    const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";
    const static wchar_t* WINDOW_IS_PINNED_PROP = L"AlwaysOnTop_Pinned";
}

bool isExcluded(HWND window)
{
    auto processPath = get_process_path(window);
    CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));

    return check_excluded_app(window, processPath, AlwaysOnTopSettings::settings().excludedApps);
}

namespace
{
    bool TryGetExtendedFrameBounds(HWND window, RECT& rect) noexcept
    {
        return DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof(rect)) == S_OK;
    }
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

        m_dimOverlay = std::make_unique<DimOverlay>();
        if (!m_dimOverlay->Initialize(m_hinstance))
        {
            Logger::warn("Failed to initialize AlwaysOnTop dim overlay.");
            m_dimOverlay.reset();
        }

        RegisterHotkey();
        RegisterLLKH();
        
        SubscribeToEvents();
        StartTrackingTopmostWindows();
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
        UpdateDimOverlay();
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
        UpdateDimOverlay();
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
        }
    }
    else
    {
        if (PinTopmostWindow(window))
        {
            soundType = Sound::Type::On;
            AssignBorder(window);
            
            Trace::AlwaysOnTop::PinWindow();
        }
    }

    UpdateDimOverlay();

    if (AlwaysOnTopSettings::settings().enableSound)
    {
        m_sound.Play(soundType);    
    }
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

    UpdateDimOverlay();
}

void AlwaysOnTop::CleanUp()
{
    UnpinAll();
    if (m_dimOverlay)
    {
        m_dimOverlay->Terminate();
        m_dimOverlay.reset();
    }
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
    if (!data->hwnd)
    {
        return;
    }

    const bool frameEnabled = AlwaysOnTopSettings::settings().enableFrame;

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
        if (frameEnabled)
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
    }
    break;
    case EVENT_SYSTEM_MINIMIZESTART:
    {
        if (frameEnabled)
        {
            auto iter = m_topmostWindows.find(data->hwnd);
            if (iter != m_topmostWindows.end())
            {
                m_topmostWindows[data->hwnd] = nullptr;
            }
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
            if (frameEnabled)
            {
                AssignBorder(data->hwnd);
            }
        }
    }
    break;
    case EVENT_SYSTEM_MOVESIZEEND:
    {
        if (frameEnabled)
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
        if (frameEnabled)
        {
            RefreshBorders();
        }
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

    UpdateDimOverlay();
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

    UpdateDimOverlay();
}

void AlwaysOnTop::UpdateDimOverlay()
{
    if (!m_dimOverlay)
    {
        return;
    }

    struct PinnedEntry
    {
        HWND window = nullptr;
        HWND border = nullptr;
    };

    std::vector<DimOverlayHole> holes;
    std::vector<PinnedEntry> pinnedEntries;
    holes.reserve(m_topmostWindows.size());
    pinnedEntries.reserve(m_topmostWindows.size());

    for (const auto& [window, border] : m_topmostWindows)
    {
        if (!IsWindow(window) || !IsPinned(window))
        {
            continue;
        }

        if (!IsWindowVisible(window) || IsIconic(window))
        {
            continue;
        }

        if (!m_virtualDesktopUtils.IsWindowOnCurrentDesktop(window))
        {
            continue;
        }

        RECT rect{};
        if (!TryGetExtendedFrameBounds(window, rect))
        {
            if (!GetWindowRect(window, &rect))
            {
                continue;
            }
        }

        int radius = WindowCornerUtils::CornersRadius(window);
        if (radius > 0)
        {
            const float scale = ScalingUtils::ScalingFactor(window);
            radius = static_cast<int>(std::lround(radius * scale));
        }
        else
        {
            radius = 0;
        }

        holes.push_back({ rect, radius });
        pinnedEntries.push_back({ window, border ? border->Hwnd() : nullptr });
    }

    const bool visible = !holes.empty();
    m_dimOverlay->Update(std::move(holes), visible);

    if (!visible)
    {
        return;
    }

    const HWND overlayHwnd = m_dimOverlay->Hwnd();
    if (!overlayHwnd)
    {
        return;
    }

    SetWindowPos(overlayHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);

    for (const auto& entry : pinnedEntries)
    {
        if (entry.border)
        {
            SetWindowPos(entry.border, overlayHwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
            SetWindowPos(entry.window, entry.border, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        else
        {
            SetWindowPos(entry.window, overlayHwnd, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
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
