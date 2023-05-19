#include "pch.h"
#include "MiddleMouseButtonsHook.h"
#include <common/debug_control.h>

#pragma region public

HHOOK MiddleMouseButtonsHook::hHook = {};
std::function<void()> MiddleMouseButtonsHook::callback = {};

MiddleMouseButtonsHook::MiddleMouseButtonsHook(std::function<void()> extCallback)
{
    callback = std::move(extCallback);
}

void MiddleMouseButtonsHook::enable()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif
    if (!hHook)
    {
        hHook = SetWindowsHookEx(WH_MOUSE_LL, MiddleMouseButtonsProc, GetModuleHandle(NULL), 0);
    }
}

void MiddleMouseButtonsHook::disable()
{
    if (hHook)
    {
        UnhookWindowsHookEx(hHook);
        hHook = NULL;
    }
}

#pragma endregion

#pragma region private

LRESULT CALLBACK MiddleMouseButtonsHook::MiddleMouseButtonsProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        if (wParam == WM_MBUTTONDOWN)
        {
            callback();
        }
    }
    return CallNextHookEx(hHook, nCode, wParam, lParam);
}

#pragma endregion
