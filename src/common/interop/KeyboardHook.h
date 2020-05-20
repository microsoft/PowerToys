#pragma once

using namespace System::Threading;
using namespace System::Collections::Generic;

namespace interop
{
public
    ref struct KeyboardEvent
    {
        WPARAM message;
        int key;
    };

public
    delegate void KeyboardEventCallback(KeyboardEvent ^ ev);
public
    delegate bool IsActiveCallback();

public
    ref class KeyboardHook
    {
    public:
        KeyboardHook(
            KeyboardEventCallback ^ callback, 
            IsActiveCallback ^ isActiveCallback);
        ~KeyboardHook();

        void Start();

    private:
        delegate LRESULT HookProcDelegate(int nCode, WPARAM wParam, LPARAM lParam);
        Thread ^ kbEventDispatch;
        Queue<KeyboardEvent ^> ^ queue;
        KeyboardEventCallback ^ callback;
        IsActiveCallback ^ isActive;
        bool quit;
        HHOOK hookHandle;

        void DispatchProc();
        LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    };

}
