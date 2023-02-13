#include "pch.h"
#include "AlwaysOnTop.h"

#include <common/display/dpi_aware.h>
#include <common/utils/game_mode.h>
#include <common/utils/excluded_apps.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>
#include <common/utils/process_path.h>

#include <interop/shared_constants.h>

#include <trace.h>
#include <WinHookEventIDs.h>


namespace NonLocalizable
{
    const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";
    const static wchar_t* WINDOW_IS_PINNED_PROP = L"AlwaysOnTop_Pinned";
}

bool isExcluded(HWND window)
{
    auto processPath = get_process_path(window);
    CharUpperBuffW(processPath.data(), static_cast<DWORD>(processPath.length()));
    return find_app_name_in_path(processPath, AlwaysOnTopSettings::settings().excludedApps);
}

AlwaysOnTop::AlwaysOnTop(bool useLLKH) :
    SettingsObserver({SettingId::FrameEnabled, SettingId::Hotkey, SettingId::ExcludeApps}),
    m_hinstance(reinterpret_cast<HINSTANCE>(&__ImageBase)),
    m_useCentralizedLLKH(useLLKH)
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
        if (HWND fw{ GetForegroundWindow() })
        {
            ProcessCommand(fw);
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
        return;
    }

    UnregisterHotKey(m_window, static_cast<int>(HotkeyId::Pin));
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::Pin), AlwaysOnTopSettings::settings().hotkey.get_modifiers(), AlwaysOnTopSettings::settings().hotkey.get_code());
}

void AlwaysOnTop::RegisterLLKH()
{
    if (!m_useCentralizedLLKH)
    {
        return;
    }
	
    m_hPinEvent = CreateEventW(nullptr, false, false, CommonSharedConstants::ALWAYS_ON_TOP_PIN_EVENT);

    if (!m_hPinEvent)
    {
        Logger::warn(L"Failed to create pinEvent. {}", get_last_error_or_default(GetLastError()));
        return;
    }

    m_thread = std::thread([this]() {
        MSG msg;
        while (m_running)
        {
            DWORD dwEvt = MsgWaitForMultipleObjects(1, &m_hPinEvent, false, INFINITE, QS_ALLINPUT);
            if (!m_running)
            {
                break;
            }
            switch (dwEvt)
            {
            case WAIT_OBJECT_0:
                if (HWND fw{ GetForegroundWindow() })
                {
                    ProcessCommand(fw);
                }
                break;
            case WAIT_OBJECT_0 + 1:
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
    }

    m_topmostWindows.clear();
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
    if (!AlwaysOnTopSettings::settings().enableFrame || !data->hwnd)
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