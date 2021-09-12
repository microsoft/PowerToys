#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has KBM enabled or disabled - Can also be used to see how often users have to restart the keyboard hook
    static void EnableKeyboardManager(const bool enabled) noexcept;
};
