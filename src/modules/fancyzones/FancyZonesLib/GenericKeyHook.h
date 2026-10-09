#pragma once

#include "pch.h"
#include <functional>
#include <common/debug_control.h>

template<int... keys>
class GenericKeyHook
{
public:

    GenericKeyHook(std::function<void(bool)> extCallback)
    {
        callback = std::move(extCallback);
    }

    void enable()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        if (IsDebuggerPresent())
        {
            return;
        }
#endif
        if (!hHook)
        {
            hHook = SetWindowsHookEx(WH_KEYBOARD_LL, GenericKeyHookProc, GetModuleHandle(NULL), 0);
        }
    }

    void disable()
    {
        if (hHook)
        {
            UnhookWindowsHookEx(hHook);
            hHook = NULL;
            callback(false);
        }
    }

private:
    inline static HHOOK hHook = nullptr;
    inline static std::function<void(bool)> callback;

    static LRESULT CALLBACK GenericKeyHookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (nCode == HC_ACTION)
        {
            if (wParam == WM_KEYDOWN || wParam == WM_KEYUP)
            {
                PKBDLLHOOKSTRUCT kbdHookStruct = reinterpret_cast<PKBDLLHOOKSTRUCT>(lParam);
                if (((kbdHookStruct->vkCode == keys) || ...))
                {
                    callback(wParam == WM_KEYDOWN);
                }
            }
        }
        return CallNextHookEx(hHook, nCode, wParam, lParam);
    }
};
