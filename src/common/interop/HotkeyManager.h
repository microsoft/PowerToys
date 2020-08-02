#pragma once
#include <Windows.h>
#include "KeyboardHook.h"

namespace interop
{
public
    ref struct Hotkey
    {
        bool Win;
        bool Ctrl;
        bool Shift;
        bool Alt;
        unsigned char Key;

        Hotkey()
        {
            Win = false;
            Ctrl = false;
            Shift = false;
            Alt = false;
            Key = 0;
        }
    };

public
    delegate void HotkeyCallback();

    typedef unsigned short HOTKEY_HANDLE;

public
    ref class HotkeyManager
    {
    public:
        HotkeyManager();
        ~HotkeyManager();

        HOTKEY_HANDLE RegisterHotkey(Hotkey ^ hotkey, HotkeyCallback ^ callback);
        void UnregisterHotkey(HOTKEY_HANDLE handle);

    private:
        KeyboardHook ^ keyboardHook;
        Dictionary<HOTKEY_HANDLE, HotkeyCallback ^> ^ hotkeys;
        Hotkey ^ pressedKeys;
        KeyboardEventCallback ^ keyboardEventCallback;
        IsActiveCallback ^ isActiveCallback;
        FilterKeyboardEvent ^ filterKeyboardCallback;

        void KeyboardEventProc(KeyboardEvent ^ ev);
        bool IsActiveProc();
        bool FilterKeyboardProc(KeyboardEvent ^ ev);
        HOTKEY_HANDLE GetHotkeyHandle(Hotkey ^ hotkey);
    };
}
