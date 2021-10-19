#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    // Log if the user has FindMyMouse enabled or disabled
    static void EnableFindMyMouse(const bool enabled) noexcept;

    // Log that the user activated the module by focusing the mouse pointer
    static void MousePointerFocused() noexcept;
};
