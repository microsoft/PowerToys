#include "pch.h"
#include "KeyboardHook.h"
#include <exception>
#include <msclr/marshal.h>
#include <msclr/marshal_cppstd.h>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>

using namespace interop;
using namespace System::Runtime::InteropServices;
using namespace System;
using namespace System::Diagnostics;

KeyboardHook::KeyboardHook(
    KeyboardEventCallback ^ keyboardEventCallback,
    IsActiveCallback ^ isActiveCallback,
    FilterKeyboardEvent ^ filterKeyboardEvent)
{
    this->keyboardEventCallback = keyboardEventCallback;
    this->isActiveCallback = isActiveCallback;
    this->filterKeyboardEvent = filterKeyboardEvent;
}

KeyboardHook::~KeyboardHook()
{
    // Unregister low level hook procedure
    UnhookWindowsHookEx(hookHandle);
}

void KeyboardHook::Start()
{
    hookProc = gcnew HookProcDelegate(this, &KeyboardHook::HookProc);
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
            (HOOKPROC)(void*)Marshal::GetFunctionPointerForDelegate(hookProc),
            0,
            0);
        if (hookHandle == nullptr)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Interop");
        }
    }
}

LRESULT __clrcall KeyboardHook::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && isActiveCallback->Invoke())
    {
        KeyboardEvent ^ ev = gcnew KeyboardEvent();
        ev->message = wParam;
        ev->key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->vkCode;
        ev->dwExtraInfo = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->dwExtraInfo;

        // Ignore the keyboard hook if the FilterkeyboardEvent returns false.
        if ((filterKeyboardEvent != nullptr && !filterKeyboardEvent->Invoke(ev)))
        {
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        keyboardEventCallback->Invoke(ev);
        return 1;
    }
    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
}