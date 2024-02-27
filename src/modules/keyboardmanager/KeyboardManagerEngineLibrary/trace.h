#pragma once

#include "State.h"

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if a key to key remap has been invoked today.
    static void DailyKeyToKeyRemapInvoked() noexcept;

    // Log if a key to shortcut remap has been invoked today.
    static void DailyKeyToShortcutRemapInvoked() noexcept;

    // Log if a shortcut to key remap has been invoked today.
    static void DailyShortcutToKeyRemapInvoked() noexcept;

    // Log if a shortcut to shortcut remap has been invoked today.
    static void DailyShortcutToShortcutRemapInvoked() noexcept;

    // Log if an app specific shortcut to key remap has been invoked today.
    static void DailyAppSpecificShortcutToKeyRemapInvoked() noexcept;

    // Log if an app specific shortcut to shortcut remap has been invoked today.
    static void DailyAppSpecificShortcutToShortcutRemapInvoked() noexcept;

    // Log if a key remap has been invoked (not being used currently, due to being garrulous)
    static void KeyRemapInvoked(bool isKeyToKey) noexcept;

    // Log if a shortcut remap has been invoked (not being used currently, due to being garrulous)
    static void ShortcutRemapInvoked(bool isShortcutToShortcut, bool isAppSpecific) noexcept;

    // Log the current remappings of key and shortcuts when keyboard manager engine loads the settings.
    static void SendKeyAndShortcutRemapLoadedConfiguration(State& remappings) noexcept;

    // Log an error while trying to send remappings telemetry.
    static void ErrorSendingKeyAndShortcutRemapLoadedConfiguration() noexcept;

    // Log if an error occurs in KBM
    static void Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept;
};
