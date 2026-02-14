#include "pch.h"
#include "HotkeyManager.h"
#include "HotkeyManager.g.cpp"

namespace winrt::PowerToys::Interop::implementation
{
    HotkeyManager::HotkeyManager()
    {
        keyboardEventCallback = KeyboardEventCallback{ this, &HotkeyManager::KeyboardEventProc };
        isActiveCallback = IsActiveCallback{ this, &HotkeyManager::IsActiveProc };
        filterKeyboardCallback = FilterKeyboardEvent{ this, &HotkeyManager::FilterKeyboardProc };
        keyboardHook = KeyboardHook{ keyboardEventCallback, isActiveCallback, filterKeyboardCallback };
        keyboardHook.Start();
    }

    // When all Shortcut keys are pressed, fire the HotkeyCallback event.
    void HotkeyManager::KeyboardEventProc(KeyboardEvent /*ev*/)
    {
        // pressedKeys always stores the latest keyboard state
        auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);
        if (hotkeys.find(pressedKeysHandle) != hotkeys.end())
        {
            hotkeys[pressedKeysHandle]();

            // After invoking the hotkey send a dummy key to prevent Start Menu from activating
            INPUT dummyEvent[1] = {};
            dummyEvent[0].type = INPUT_KEYBOARD;
            dummyEvent[0].ki.wVk = 0xFF;
            dummyEvent[0].ki.dwFlags = KEYEVENTF_KEYUP;
            SendInput(1, dummyEvent, sizeof(INPUT));
        }
    }

    // Hotkeys are intended to be global, therefore they are always active no matter the
    // context in which the keypress occurs.
    bool HotkeyManager::IsActiveProc()
    {
        return true;
    }
    bool HotkeyManager::FilterKeyboardProc(KeyboardEvent ev)
    {
        // Updating the pressed keys here so we know if the keypress event should be propagated or not.
        pressedKeys.Win = (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000);
        pressedKeys.Ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
        pressedKeys.Alt = GetAsyncKeyState(VK_MENU) & 0x8000;
        pressedKeys.Shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
        pressedKeys.Key = static_cast<unsigned char>(ev.key);

        // Convert to hotkey handle
        auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);

        // Check if any hotkey matches the pressed keys if the current key event is a key down event
        if ((ev.message == WM_KEYDOWN || ev.message == WM_SYSKEYDOWN) && hotkeys.find(pressedKeysHandle)!=hotkeys.end())
        {
            return true;
        }

        return false;
    }

    uint16_t HotkeyManager::RegisterHotkey(winrt::PowerToys::Interop::Hotkey const& _hotkey, winrt::PowerToys::Interop::HotkeyCallback const& _callback)
    {
        auto handle = GetHotkeyHandle(_hotkey);
        hotkeys[handle] = _callback;
        return handle;
    }

    void HotkeyManager::UnregisterHotkey(uint16_t _handle)
    {
        auto iter = hotkeys.find(_handle);
        if (iter != hotkeys.end()) {
            hotkeys.erase(iter);
        }
    }

    void HotkeyManager::Close()
    {
    }

    uint16_t HotkeyManager::GetHotkeyHandle(Hotkey hotkey)
    {
        uint16_t handle = hotkey.Key;
        handle |= hotkey.Win << 8;
        handle |= hotkey.Ctrl << 9;
        handle |= hotkey.Shift << 10;
        handle |= hotkey.Alt << 11;
        return handle;
    }
}
