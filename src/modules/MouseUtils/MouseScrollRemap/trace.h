#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;
    static void EnableMouseScrollRemap(bool enabled) noexcept;
};
