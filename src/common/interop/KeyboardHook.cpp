#include "pch.h"
#include "KeyboardHook.h"
#include <exception>

using namespace interop;
using namespace System::Runtime::InteropServices;

KeyboardHook::KeyboardHook(
    KeyboardEventCallback ^ cb,
    IsActiveCallback ^ activeCb)
{
    kbEventDispatch = gcnew Thread(gcnew ThreadStart(this, &KeyboardHook::DispatchProc));
    queue = gcnew Queue<KeyboardEvent ^>();
    callback = cb;
    isActive = activeCb;
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
        
        callback->Invoke(nextEv);
        
        // Re-aquire lock
        Monitor::Enter(queue);
    }

    Monitor::Exit(queue);
}

void KeyboardHook::Start()
{
    auto del = gcnew HookProcDelegate(this, &KeyboardHook::HookProc);
    // register low level hook procedure
    hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, (HOOKPROC) (void*) Marshal::GetFunctionPointerForDelegate(del), 0, 0);
    if (hookHandle == nullptr)
    {
        throw std::exception("SetWindowsHookEx failed.");
    }

    kbEventDispatch->Start();
}

LRESULT CALLBACK KeyboardHook::HookProc(int nCode, WPARAM wParam, LPARAM lParam)
{
    if (nCode == HC_ACTION && isActive->Invoke())
    {
        KeyboardEvent ^ ev = gcnew KeyboardEvent();
        ev->message = wParam;
        ev->key = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam)->vkCode;

        Monitor::Enter(queue);
        queue->Enqueue(ev);
        Monitor::Pulse(queue);
        Monitor::Exit(queue);
        return 1;
    }
    return CallNextHookEx(hookHandle, nCode, wParam, lParam);
}