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
        filterKeyboardCallback
    );
    hotkeys = gcnew Dictionary<HOTKEY_HANDLE, HotkeyCallback ^>();
    pressedKeys = gcnew Hotkey();
    keyboardHook->Start();
}

HotkeyManager::~HotkeyManager()
{
    delete keyboardHook;
}

// When all Shortcut keys are pressed, fire the HotkeyCallback event.
void HotkeyManager::KeyboardEventProc(KeyboardEvent^ ev)
{
    auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);
    if (hotkeys->ContainsKey(pressedKeysHandle))
    {
        hotkeys[pressedKeysHandle]->Invoke();
    }
}

// Hotkeys are intended to be global, therefore they are always active no matter the
// context in which the keypress occurs.
bool HotkeyManager::IsActiveProc()
{
    return true;
}

// KeyboardEvent callback is only fired for relevant key events.
bool HotkeyManager::FilterKeyboardProc(KeyboardEvent^ ev)
{
    auto oldHandle = GetHotkeyHandle(pressedKeys);

    // Updating the pressed keys here so we know if the keypress event 
    // should be propagated or not.
    UpdatePressedKeys(ev);

    auto pressedKeysHandle = GetHotkeyHandle(pressedKeys);

    // Check if the hotkey matches the pressed keys, and check if the pressed keys aren't duplicate
    // (there shouldn't be auto repeating hotkeys)
    if (hotkeys->ContainsKey(pressedKeysHandle) && oldHandle != pressedKeysHandle)
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

void HotkeyManager::UpdatePressedKey(DWORD code, bool replaceWith, unsigned char replaceWithKey)
{
    switch (code)
    {
    case VK_LWIN:
    case VK_RWIN:
        pressedKeys->Win = replaceWith;
        break;
    case VK_CONTROL:
    case VK_LCONTROL:
    case VK_RCONTROL:
        pressedKeys->Ctrl = replaceWith;
        break;
    case VK_SHIFT:
    case VK_LSHIFT:
    case VK_RSHIFT:
        pressedKeys->Shift = replaceWith;
        break;
    case VK_MENU:
    case VK_LMENU:
    case VK_RMENU:
        pressedKeys->Alt = replaceWith;
        break;
    default:
        pressedKeys->Key = replaceWithKey;
        break;
    }
}

void HotkeyManager::UpdatePressedKeys(KeyboardEvent ^ ev)
{
    switch (ev->message)
    {
    case WM_KEYDOWN:
    case WM_SYSKEYDOWN:
    {
        UpdatePressedKey(ev->key, true, ev->key);
    }
    break;
    case WM_KEYUP:
    case WM_SYSKEYUP:
    {
        UpdatePressedKey(ev->key, false, 0);
    }
    break;
    }
}