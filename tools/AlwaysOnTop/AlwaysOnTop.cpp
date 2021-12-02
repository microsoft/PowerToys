#include "pch.h"
#include "AlwaysOnTop.h"

#include <mmsystem.h> // sound
#include <shellapi.h> // game mode

const static wchar_t* TOOL_WINDOW_CLASS_NAME = L"AlwaysOnTopWindow";

// common
extern "C" IMAGE_DOS_HEADER __ImageBase;

// common
inline bool detect_game_mode()
{
    QUERY_USER_NOTIFICATION_STATE notification_state;
    if (SHQueryUserNotificationState(&notification_state) != S_OK)
    {
        return false;
    }
    return (notification_state == QUNS_RUNNING_D3D_FULL_SCREEN);
}

AlwaysOnTop::AlwaysOnTop()
    : m_hinstance(reinterpret_cast<HINSTANCE>(&__ImageBase))
{
    s_instance = this;

    Init();
    StartTrackingTopmostWindows();
}

AlwaysOnTop::~AlwaysOnTop()
{
    CleanUp();
}

void AlwaysOnTop::Init()
{
    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = WndProc_Helper;
    wcex.hInstance = m_hinstance;
    wcex.lpszClassName = TOOL_WINDOW_CLASS_NAME;
    RegisterClassExW(&wcex);

    m_window = CreateWindowExW(WS_EX_TOOLWINDOW, TOOL_WINDOW_CLASS_NAME, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, m_hinstance, this);
    if (!m_window) 
    {
        return;
    }

    RegisterHotKey(m_window, 1, MOD_CONTROL | MOD_NOREPEAT, 0x54 /* T */);

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
            MessageBoxW(NULL, L"Failed to set win event hook", L"AlwaysOnTop error", MB_OK | MB_ICONERROR);
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
    return 0;
}

void AlwaysOnTop::ProcessCommand(HWND window)
{
    bool gameMode = detect_game_mode();
    if (!m_activateInGameMode && gameMode)
    {
        return;
    }

    bool topmost = IsTopmost(window);
    if (topmost) 
    {
        if (ResetTopmostWindow(window))
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
        if (SetTopmostWindow(window))
        {
            auto tracker = std::make_unique<WindowTracker>(window);
            if (!tracker->Init(m_hinstance))
            {
                // Failed to init tracker, reset topmost
                ResetTopmostWindow(window);
                return;
            }

            m_topmostWindows[window] = std::move(tracker);
        }
    }

    auto soundPlayed = PlaySound((LPCTSTR)SND_ALIAS_SYSTEMASTERISK, NULL, SND_ALIAS_ID);
    if (!soundPlayed)
    {
        MessageBoxW(NULL, L"Sound playing error", L"AlwaysOnTop error", MB_OK | MB_ICONERROR);
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
            auto tracker = std::make_unique<WindowTracker>(window);
            if (!tracker->Init(m_hinstance))
            {
                // Failed to init tracker, reset topmost
                ResetTopmostWindow(window);
                return;
            }

            m_topmostWindows[window] = std::move(tracker);
        }
    }
}

void AlwaysOnTop::ResetAll()
{
    for (const auto& [topWindow, tracker]: m_topmostWindows)
    {
        if (!ResetTopmostWindow(topWindow))
        {
            //TODO: log error
        }
    }

    m_topmostWindows.clear();
}

void AlwaysOnTop::CleanUp()
{
    ResetAll();
    if (m_window)
    {
        DestroyWindow(m_window);
        m_window = nullptr;
    }

    UnregisterClass(TOOL_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
}

bool AlwaysOnTop::IsTopmost(HWND window) const noexcept
{
    int exStyle = GetWindowLong(window, GWL_EXSTYLE);
    return (exStyle & WS_EX_TOPMOST) == WS_EX_TOPMOST;
}

bool AlwaysOnTop::SetTopmostWindow(HWND window) const noexcept
{
    return SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE);
}

bool AlwaysOnTop::ResetTopmostWindow(HWND window) const noexcept
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
        tracker->DrawFrame();
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