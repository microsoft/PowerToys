#include "pch.h"
#include "KeyboardHook.h"
#include "KeyboardHook.g.cpp"
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>

namespace winrt::PowerToys::Interop::implementation
{
    KeyboardHook::KeyboardHook(winrt::PowerToys::Interop::KeyboardEventCallback const& keyboardEventCallback, winrt::PowerToys::Interop::IsActiveCallback const& isActiveCallback, winrt::PowerToys::Interop::FilterKeyboardEvent const& filterKeyboardEvent)
    {
        assert(s_instance == nullptr);
        s_instance = this;
        this->keyboardEventCallback = keyboardEventCallback;
        this->isActiveCallback = isActiveCallback;
        this->filterKeyboardEvent = filterKeyboardEvent;
    }

    void KeyboardHook::Close()
    {
        if (hookHandle)
        {
            if (UnhookWindowsHookEx(hookHandle))
            {
                hookHandle = nullptr;
            }
        }
        s_instance = nullptr;
    }

    void KeyboardHook::Start()
    {
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
        const bool hookDisabled = IsDebuggerPresent();
#else
        const bool hookDisabled = false;
#endif
        if (!hookDisabled)
        {
            // register low level hook procedure
            hookHandle = SetWindowsHookEx(
                WH_KEYBOARD_LL,
                HookProc,
                0,
                0);
            if (hookHandle == nullptr)
            {
                DWORD errorCode = GetLastError();
                show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Interop");
            }
        }
    }
    LRESULT KeyboardHook::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        if (s_instance != nullptr && nCode == HC_ACTION && s_instance->isActiveCallback())
        {
            KeyboardEvent ev;
            ev.message = wParam;
            ev.key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->vkCode;
            ev.dwExtraInfo = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->dwExtraInfo;

            // Ignore the keyboard hook if the FilterkeyboardEvent returns false.
            if ((s_instance->filterKeyboardEvent != nullptr && !s_instance->filterKeyboardEvent(ev)))
            {
                return CallNextHookEx(NULL, nCode, wParam, lParam);
            }

            s_instance->keyboardEventCallback(ev);
            return 1;
        }
        return CallNextHookEx(NULL, nCode, wParam, lParam);
    }
}
