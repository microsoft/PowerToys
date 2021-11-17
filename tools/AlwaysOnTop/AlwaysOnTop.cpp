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

    hotKeyHandleWindow = CreateWindowExW(WS_EX_TOOLWINDOW, HOTKEY_WINDOW_CLASS_NAME, L"", WS_POPUP, 0, 0, 0, 0, nullptr, nullptr, hinstance, this);
    if (!hotKeyHandleWindow) {
        return;
    }

    RegisterHotKey(hotKeyHandleWindow, 1, MOD_CONTROL | MOD_NOREPEAT, 0x54 /* T */);
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
            auto iter = std::find(topmostWindows.begin(), topmostWindows.end(), window);
            if (iter != topmostWindows.end())
            {
                topmostWindows.erase(iter);
            }
        }
    }
    else
    {
        if (SetTopmostWindow(window))
        {
            topmostWindows.push_back(window);
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
            topmostWindows.push_back(window);
        }
    }
}

void AlwaysOnTop::ResetAll()
{
    for (HWND topWindow : topmostWindows)
    {
        if (!ResetTopmostWindow(topWindow))
        {
            //TODO: log error
        }
    }

    topmostWindows.clear();
}

void AlwaysOnTop::CleanUp()
{
    ResetAll();
    if (hotKeyHandleWindow)
    {
        DestroyWindow(hotKeyHandleWindow);
        hotKeyHandleWindow = nullptr;
    }
    UnregisterClass(HOTKEY_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
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
