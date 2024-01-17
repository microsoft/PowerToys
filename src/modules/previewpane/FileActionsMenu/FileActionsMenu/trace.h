#pragma once
#include <interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();

    // Log if the user has FileActionsMenu enabled or disabled
    static void EnableFileActionsMenu(const bool enabled) noexcept;

    // Log if the user has invoked FileActionsMenu
    static void FileActionsMenuInvoked() noexcept;

    // Event to send settings telemetry.
    static void Trace::SettingsTelemetry(PowertoyModuleIface::Hotkey& hotkey) noexcept;

};
