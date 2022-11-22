#include "pch.h"
#include "HotkeyManager.h"

using namespace interop;

HotkeyManager::HotkeyManager()
{
    keyboardEventCallback = gcnew KeyboardEventCallback(this, &HotkeyManager::KeyboardEventProc);
    isActiveCallback = gcnew IsActiveCallback(this, &HotkeyManager::IsActiveProc);
    filterKeyboardCallback = gcnew FilterKeyboardEvent(this, &HotkeyManager::FilterKeyboardProc);

    keyboardHook = gcnew KeyboardHook(
        keyboardEventCallback,
        isActiveCallback,
        filterKeyboardCallback);
    hotkeys = gcnew Dictionary<HOTKEY_HANDLE, HotkeyCallback ^>();
    pressedKeys = gcnew Hotkey();
    keyboardHook->Start();
}

HotkeyManager::~HotkeyManager()
{
    delete keyboardHook;
}

// When all Shortcut keys are pressed, fire the HotkeyCallback event.
void HotkeyManager::KeyboardEventProc(KeyboardEvent ^ /*ev*/)
{
    // pressedKeys always stores the latest keyboard state
    auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);
    if (hotkeys->ContainsKey(pressedKeysHandle))
    {
        hotkeys[pressedKeysHandle]->Invoke();

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

// KeyboardEvent callback is only fired for relevant key events.
bool HotkeyManager::FilterKeyboardProc(KeyboardEvent ^ ev)
{
    // Updating the pressed keys here so we know if the keypress event should be propagated or not.
    pressedKeys->Win = (GetAsyncKeyState(VK_LWIN) & 0x8000) || (GetAsyncKeyState(VK_RWIN) & 0x8000);
    pressedKeys->Ctrl = GetAsyncKeyState(VK_CONTROL) & 0x8000;
    pressedKeys->Alt = GetAsyncKeyState(VK_MENU) & 0x8000;
    pressedKeys->Shift = GetAsyncKeyState(VK_SHIFT) & 0x8000;
    pressedKeys->Key = static_cast<unsigned char>(ev->key);

    // Convert to hotkey handle
    auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);

    // Check if any hotkey matches the pressed keys if the current key event is a key down event
    if ((ev->message == WM_KEYDOWN || ev->message == WM_SYSKEYDOWN) && hotkeys->ContainsKey(pressedKeysHandle))
    {
        return true;
    }

    return false;
}

// NOTE: Replaces old hotkey if one already present.
HOTKEY_HANDLE HotkeyManager::RegisterHotkey(Hotkey ^ hotkey, HotkeyCallback ^ callback)
{
    auto handle = GetHotkeyHandle(hotkey);
    hotkeys[handle] = callback;
    return handle;
}

void HotkeyManager::UnregisterHotkey(HOTKEY_HANDLE handle)
{
    hotkeys->Remove(handle);
}

HOTKEY_HANDLE HotkeyManager::GetHotkeyHandle(Hotkey ^ hotkey)
{
    HOTKEY_HANDLE handle = hotkey->Key;
    handle |= hotkey->Win << 8;
    handle |= hotkey->Ctrl << 9;
    handle |= hotkey->Shift << 10;
    handle |= hotkey->Alt << 11;
    return handle;
}
