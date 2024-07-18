#pragma once

#include <modules/interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    class Projects
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void SettingsTelemetry(const PowertoyModuleIface::HotkeyEx& hotkey) noexcept;
    };
};
