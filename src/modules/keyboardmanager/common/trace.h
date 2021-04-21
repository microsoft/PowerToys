#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has KBM enabled or disabled - Can also be used to see how often users have to restart the keyboard hook
    static void EnableKeyboardManager(const bool enabled) noexcept;

    // Log if a key remap has been invoked
    static void KeyRemapInvoked(bool isKeyToKey) noexcept;

    // Log if a shortcut remap has been invoked
    static void ShortcutRemapInvoked(bool isShortcutToShortcut, bool isAppSpecific) noexcept;
    
    // Log if an error occurs in KBM
    static void Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;
};
