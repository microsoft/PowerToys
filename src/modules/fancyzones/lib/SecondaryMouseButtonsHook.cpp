#include "pch.h"
#include "SecondaryMouseButtonsHook.h"
#include <common/debug_control.h>

#pragma region public

HHOOK SecondaryMouseButtonsHook::hHook = {};
std::function<void()> SecondaryMouseButtonsHook::callback = {};

SecondaryMouseButtonsHook::SecondaryMouseButtonsHook(std::function<void()> extCallback)
{
    callback = std::move(extCallback);
}

void SecondaryMouseButtonsHook::enable()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif
    if (!hHook)
    {
        hHook = SetWindowsHookEx(WH_MOUSE_LL, SecondaryMouseButtonsProc, GetModuleHandle(NULL), 0);
    }
}

void SecondaryMouseButtonsHook::disable()
{
    if (hHook)
    {
        UnhookWindowsHookEx(hHook);
        hHook = NULL;
    }
}

#pragma endregion

#pragma region private

LRESULT CALLBACK SecondaryMouseButtonsHook::SecondaryMouseButtonsProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        if (wParam == WM_RBUTTONDOWN || wParam == WM_MBUTTONDOWN || wParam == WM_XBUTTONDOWN)
        {
            callback();
        }
    }
    return CallNextHookEx(hHook, nCode, wParam, lParam);
}

#pragma endregion
