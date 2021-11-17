#include "pch.h"
#include "AlwaysOnTop.h"
#include <mmsystem.h> // sound
#include <shellapi.h> // game mode

const static wchar_t* HOTKEY_WINDOW_CLASS_NAME = L"HotkeyHandleWindowClass";

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
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    WNDCLASSEXW wcex{};
    wcex.cbSize = sizeof(WNDCLASSEX);
    wcex.lpfnWndProc = WndProc_Helper;
    wcex.hInstance = hinstance;
    wcex.lpszClassName = HOTKEY_WINDOW_CLASS_NAME;
    RegisterClassExW(&wcex);

    m_hotKeyHandleWindow = CreateWindowExW(WS_EX_TOOLWINDOW, HOTKEY_WINDOW_CLASS_NAME, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinstance, this);
    if (!m_hotKeyHandleWindow) {
        return;
    }

    RegisterHotKey(m_hotKeyHandleWindow, 1, MOD_CONTROL | MOD_NOREPEAT, 0x54 /* T */);

    // subscribe to windows events
    std::array<DWORD, 3> events_to_subscribe = {
        EVENT_SYSTEM_MOVESIZEEND,
        EVENT_SYSTEM_SWITCHEND,
        EVENT_OBJECT_FOCUS
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
            auto iter = std::find(m_topmostWindows.begin(), m_topmostWindows.end(), window);
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
            m_topmostWindows.insert(m_topmostWindows.begin(), window);
            OrderWindows();
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
            m_topmostWindows.push_back(window);
        }
    }

    OrderWindows();
}

void AlwaysOnTop::ResetAll()
{
    for (HWND topWindow : m_topmostWindows)
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
    if (m_hotKeyHandleWindow)
    {
        DestroyWindow(m_hotKeyHandleWindow);
        m_hotKeyHandleWindow = nullptr;
    }
    UnregisterClass(HOTKEY_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
}

bool AlwaysOnTop::OrderWindows() const noexcept
{
    if (m_topmostWindows.empty())
    {
        return true;
    }

    BOOL res = true;
    for (int i = static_cast<int>(m_topmostWindows.size()) - 1; i >= 0; i--)
    {
        res &= SetWindowPos(m_topmostWindows[i], nullptr, 0, 0, 0, 0, SWP_NOSIZE | SWP_NOMOVE | SWP_NOREDRAW);

        if (!res)
        {
            MessageBoxW(NULL, L"Failed to order windows", L"AlwaysOnTop error", MB_OK | MB_ICONERROR);
        }
    }

    return res;
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
    for (HWND topmostWindow : m_topmostWindows)
    {
        if (window == topmostWindow)
        {
            return true;
        }
    }

    return false;
}

void AlwaysOnTop::HandleWinHookEvent(WinHookEvent* data) noexcept
{
    switch (data->event)
    {
    case EVENT_SYSTEM_MOVESIZEEND: // moved or resized
    case EVENT_SYSTEM_SWITCHEND: // alt-tab
    case EVENT_OBJECT_FOCUS: // focused
    {
        if (IsTracked(data->hwnd))
        {
            OrderWindows();
        }
    }
    break;
    default:
        break;
    }
}