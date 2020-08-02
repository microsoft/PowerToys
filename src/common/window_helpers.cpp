#include "window_helpers.h"
#include "pch.h"
#include <wil/Resource.h>


HWND CreateMsgWindow(_In_ HINSTANCE hInst, _In_ WNDPROC pfnWndProc, _In_ void* p)
{
    WNDCLASS wc = { 0 };

    PCWSTR wndClassName = L"MsgWindow";

    wc.lpfnWndProc = DefWindowProc;
    wc.cbWndExtra = sizeof(void*);
    wc.hInstance = hInst;
    wc.hbrBackground = (HBRUSH)(COLOR_BTNFACE + 1);
    wc.lpszClassName = wndClassName;

    RegisterClass(&wc);

    HWND hwnd = CreateWindowEx(
        0, wndClassName, nullptr, 0, 0, 0, 0, 0, HWND_MESSAGE, 0, hInst, nullptr);
    if (hwnd)
    {
        SetWindowLongPtr(hwnd, 0, (LONG_PTR)p);
        if (pfnWndProc)
        {
            SetWindowLongPtr(hwnd, GWLP_WNDPROC, (LONG_PTR)pfnWndProc);
        }
    }

    return hwnd;
}

bool IsProcessOfWindowElevated(HWND window)
{
    DWORD pid = 0;
    GetWindowThreadProcessId(window, &pid);
    if (!pid)
    {
        return false;
    }

    wil::unique_handle hProcess{ OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION,
                                             FALSE,
                                             pid) };

    wil::unique_handle token;
    bool elevated = false;

    if (OpenProcessToken(hProcess.get(), TOKEN_QUERY, &token))
    {
        TOKEN_ELEVATION elevation;
        DWORD size;
        if (GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
        {
            return elevation.TokenIsElevated != 0;
        }
    }
    return false;
}
