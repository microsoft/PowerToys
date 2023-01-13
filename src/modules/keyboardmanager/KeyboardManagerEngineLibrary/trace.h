#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if a key remap has been invoked (not being used currently, due to being garrulous)
    static void KeyRemapInvoked(bool isKeyToKey) noexcept;

    // Log if a shortcut remap has been invoked (not being used currently, due to being garrulous)
    static void ShortcutRemapInvoked(bool isShortcutToShortcut, bool isAppSpecific) noexcept;
    
    // Log if an error occurs in KBM
    static void Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;
};
