#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has KBM enabled or disabled - Can also be used to see how often users have to restart the keyboard hook
    static void EnableKeyboardManager(const bool enabled) noexcept;

    // Log number of key remaps when the user uses Edit Keyboard and saves settings
    static void KeyRemapCount(const DWORD count) noexcept;

    // Log number of os level shortcut remaps when the user uses Edit Shortcuts and saves settings
    static void OSLevelShortcutRemapCount(const DWORD count) noexcept;
};
