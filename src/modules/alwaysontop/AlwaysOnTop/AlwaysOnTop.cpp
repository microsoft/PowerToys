#include "pch.h"
#include "AlwaysOnTop.h"

#include <mmsystem.h> // sound

#include <common/utils/game_mode.h>
#include <common/utils/resources.h>
#include <common/utils/winapi_error.h>

#include <WinHookEventIDs.h>

namespace NonLocalizable
{
    const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";
}

AlwaysOnTop::AlwaysOnTop() :
    m_hinstance(reinterpret_cast<HINSTANCE>(&__ImageBase))
{
    s_instance = this;

    if (InitMainWindow())
    {
        InitializeWinhookEventIds();

        m_settings.InitFileWatcher();
        m_settings.AddObserver(std::bind(&AlwaysOnTop::UpdateSettings, this));
        m_settings.LoadSettings();

        RegisterHotkey();
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

void AlwaysOnTop::UpdateSettings()
{
    if (m_settings.GetSettings().enableFrame)
    {
        for (auto& iter : m_topmostWindows)
        {
            AssignTracker(iter.first);
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
        m_settings.LoadSettings();
    }
    
    return 0;
}

void AlwaysOnTop::ProcessCommand(HWND window)
{
    bool gameMode = detect_game_mode();
    if (!m_settings.GetSettings().blockInGameMode && gameMode)
    {
        return;
    }

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
        }
    }
    else
    {
        if (PinTopmostWindow(window))
        {
            if (m_settings.GetSettings().enableFrame)
            {
                AssignTracker(window);
            }
            else
            {
                m_topmostWindows[window] = nullptr;
            }
        }
    }

    if (m_settings.GetSettings().enableSound)
    {
        // TODO: don't block main thread
        auto soundPlayed = PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID);
        if (!soundPlayed)
        {
            Logger::error(L"Sound playing error");
        }
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
        if (IsTopmost(window))
        {
            AssignTracker(window);
        }
    }
}

bool AlwaysOnTop::AssignTracker(HWND window)
{
    auto tracker = std::make_unique<WindowTracker>(window);
    if (!tracker->Init(m_hinstance))
    {
        // Failed to init tracker, reset topmost
        UnpinTopmostWindow(window);
        return false;
    }

    m_topmostWindows[window] = std::move(tracker);
    return true;
}

void AlwaysOnTop::RegisterHotkey() const
{
    UnregisterHotKey(m_window, static_cast<int>(HotkeyId::Pin));
    RegisterHotKey(m_window, static_cast<int>(HotkeyId::Pin), m_settings.GetSettings().hotkey.get_modifiers(), m_settings.GetSettings().hotkey.get_code());
}

void AlwaysOnTop::SubscribeToEvents()
{
    // subscribe to windows events
    std::array<DWORD, 4> events_to_subscribe = {
        EVENT_OBJECT_LOCATIONCHANGE,
        EVENT_SYSTEM_MOVESIZEEND,
        EVENT_SYSTEM_SWITCHEND,
        EVENT_OBJECT_DESTROY
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
    for (const auto& [topWindow, tracker] : m_topmostWindows)
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

bool AlwaysOnTop::PinTopmostWindow(HWND window) const noexcept
{
    return SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
}

bool AlwaysOnTop::UnpinTopmostWindow(HWND window) const noexcept
{
    return SetWindowPos(window, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
}

bool AlwaysOnTop::IsTracked(HWND window) const noexcept
{
    auto iter = m_topmostWindows.find(window);
    return (iter != m_topmostWindows.end());
}

void AlwaysOnTop::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    if (!m_settings.GetSettings().enableFrame)
    {
        return;
    }

    auto iter = m_topmostWindows.find(data->hwnd);
    if (iter == m_topmostWindows.end())
    {
        return;
    }

    switch (data->event)
    {
    case EVENT_OBJECT_LOCATIONCHANGE:
    case EVENT_SYSTEM_MOVESIZEEND:
    {
        const auto& tracker = iter->second;
        tracker->RedrawFrame();
    }
    break;
    case EVENT_OBJECT_DESTROY:
    case EVENT_SYSTEM_SWITCHEND:
    {
        const auto& tracker = iter->second;
        tracker->Hide();
    }
    break;
    default:
        break;
    }
}