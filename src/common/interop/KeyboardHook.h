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
    delegate bool FilterKeyboardEvent(KeyboardEvent ^ ev);

public
    ref class KeyboardHook
    {
    public:
        KeyboardHook(
            KeyboardEventCallback ^ keyboardEventCallback,
            IsActiveCallback ^ isActiveCallback,
            FilterKeyboardEvent ^ filterKeyboardEvent);
        ~KeyboardHook();

        void Start();

    private:
        delegate LRESULT HookProcDelegate(int nCode, WPARAM wParam, LPARAM lParam);
        Thread ^ kbEventDispatch;
        Queue<KeyboardEvent ^> ^ queue;
        KeyboardEventCallback ^ keyboardEventCallback;
        IsActiveCallback ^ isActiveCallback;
        FilterKeyboardEvent ^ filterKeyboardEvent;
        bool quit;
        HHOOK hookHandle;
        HookProcDelegate ^ hookProc;

        void DispatchProc();
        LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    };

}
