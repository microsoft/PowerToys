#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log number of key remaps when the user uses Edit Keyboard and saves settings
    static void KeyRemapCount(const DWORD keyToKeyCount, const DWORD keyToShortcutCount, const DWORD keyToTextCount) noexcept;

    // Log number of os level shortcut remaps when the user uses Edit Shortcuts and saves settings
    static void OSLevelShortcutRemapCount(const DWORD shortcutToShortcutCount, const DWORD shortcutToKeyCount) noexcept;

    // Log number of app specific shortcut remaps when the user uses Edit Shortcuts and saves settings
    static void AppSpecificShortcutRemapCount(const DWORD shortcutToShortcutCount, const DWORD shortcutToKeyCount) noexcept;

    // Log if an error occurs in KBM
    static void Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;
};
