#pragma once
#include <interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has Peek enabled or disabled
    static void EnablePeek(const bool enabled) noexcept;

    // Log if the user has invoked Peek
    static void PeekInvoked() noexcept;

    // Event to send settings telemetry.
    static void Trace::SettingsTelemetry(PowertoyModuleIface::Hotkey& hotkey) noexcept;

};
