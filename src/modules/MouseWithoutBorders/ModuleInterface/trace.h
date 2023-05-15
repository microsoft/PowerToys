#pragma once

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    class MouseWithoutBorders
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void ToggleServiceRegistration(bool enabled) noexcept;
        static void Activate() noexcept;
        static void AddFirewallRule() noexcept;
    };
};
