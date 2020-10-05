#include "pch.h"
#include "KeyboardHook.h"
#include <exception>
#include <msclr\marshal.h>
#include <msclr\marshal_cppstd.h>
#include <common/debug_control.h>
#include <common/common.h>

using namespace interop;
using namespace System::Runtime::InteropServices;
using namespace System;
using namespace System::Diagnostics;

// A keyboard event sent with this value in the extra Information field should be ignored by the hook so that it can be captured by the system instead.
const int IGNORE_FLAG = 0x5555;

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
    Process ^ curProcess = Process::GetCurrentProcess();
    ProcessModule ^ curModule = curProcess->MainModule;
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

        if ((filterKeyboardEvent != nullptr && !filterKeyboardEvent->Invoke(ev)) || (ev->dwExtraInfo == IGNORE_FLAG))
        {
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        keyboardEventCallback->Invoke(ev);
        return 1;
    }
    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
}