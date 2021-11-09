#include "pch.h"
#include "AlwaysOnTop.h"

const static wchar_t* HOTKEY_WINDOW_CLASS_NAME = L"HotkeyHandleWindowClass";

// common
extern "C" IMAGE_DOS_HEADER __ImageBase;

AlwaysOnTop::AlwaysOnTop()
{
    s_instance = this;
    Init();
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

    RegisterHotKey(hotKeyHandleWindow, 1, MOD_CONTROL | MOD_NOREPEAT, 32 /* space */);
}

void AlwaysOnTop::CleanUp()
{
    ResetCurrentOnTop();
    if (hotKeyHandleWindow)
    {
        DestroyWindow(hotKeyHandleWindow);
        hotKeyHandleWindow = nullptr;
    }
    UnregisterClass(HOTKEY_WINDOW_CLASS_NAME, reinterpret_cast<HINSTANCE>(&__ImageBase));
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
    bool alreadyOnTop = (currentlyOnTop == window);
    ResetCurrentOnTop();
    if (!alreadyOnTop) 
    {
        (void)SetWindowOnTop(window);
    }
}

bool AlwaysOnTop::SetWindowOnTop(HWND window)
{
    if (SetWindowPos(window, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)) 
    {
        currentlyOnTop = window;
        return true;
    }
    return false;
}

void AlwaysOnTop::ResetCurrentOnTop()
{
    if (currentlyOnTop && SetWindowPos(currentlyOnTop, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE)) 
    {
    }
    currentlyOnTop = nullptr;
}
