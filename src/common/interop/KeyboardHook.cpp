#include "pch.h"
#include "KeyboardHook.h"
#include <exception>
#include <msclr\marshal.h>
#include <msclr\marshal_cppstd.h>

using namespace interop;
using namespace System::Runtime::InteropServices;
using namespace System;
using namespace System::Diagnostics;

KeyboardHook::KeyboardHook(
    KeyboardEventCallback ^ keyboardEventCallback,
    IsActiveCallback ^ isActiveCallback,
    FilterKeyboardEvent ^ filterKeyboardEvent)
{
    kbEventDispatch = gcnew Thread(gcnew ThreadStart(this, &KeyboardHook::DispatchProc));
    queue = gcnew Queue<KeyboardEvent ^>();
    this->keyboardEventCallback = keyboardEventCallback;
    this->isActiveCallback = isActiveCallback;
    this->filterKeyboardEvent = filterKeyboardEvent;
}

KeyboardHook::~KeyboardHook()
{
    quit = true;
    kbEventDispatch->Join();

    // Unregister low level hook procedure
    UnhookWindowsHookEx(hookHandle);
}

void KeyboardHook::DispatchProc()
{
    Monitor::Enter(queue);
    quit = false;
    while (!quit)
    {
        if (queue->Count == 0)
        {
            Monitor::Wait(queue);
            continue;
        }
        auto nextEv = queue->Dequeue();

        // Release lock while callback is being invoked
        Monitor::Exit(queue);
        
        keyboardEventCallback->Invoke(nextEv);
        
        // Re-aquire lock
        Monitor::Enter(queue);
    }

    Monitor::Exit(queue);
}

void KeyboardHook::Start()
{
    hookProc = gcnew HookProcDelegate(this, &KeyboardHook::HookProc);
    Process ^ curProcess = Process::GetCurrentProcess();
    ProcessModule ^ curModule = curProcess->MainModule;
    // register low level hook procedure
    hookHandle = SetWindowsHookEx(
        WH_KEYBOARD_LL, 
        (HOOKPROC)(void*)Marshal::GetFunctionPointerForDelegate(hookProc), 
        GetModuleHandle(msclr::interop::marshal_as<std::wstring>(curModule->ModuleName).c_str()),
        0);
    if (hookHandle == nullptr)
    {
        throw std::exception("SetWindowsHookEx failed.");
    }

    kbEventDispatch->Start();
}

LRESULT CALLBACK KeyboardHook::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && isActiveCallback->Invoke())
    {
        KeyboardEvent ^ ev = gcnew KeyboardEvent();
        ev->message = wParam;
        ev->key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->vkCode;
        if (filterKeyboardEvent != nullptr && !filterKeyboardEvent->Invoke(ev))
        {
            return CallNextHookEx(hookHandle, nCode, wParam, lParam);
        }

        Monitor::Enter(queue);
        queue->Enqueue(ev);
        Monitor::Pulse(queue);
        Monitor::Exit(queue);
        return 1;
    }
    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
}