#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    class AlwaysOnTop
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void PinWindow() noexcept;
        static void UnpinWindow() noexcept;
    };
};
