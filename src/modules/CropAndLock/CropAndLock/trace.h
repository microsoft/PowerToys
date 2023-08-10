#pragma once
#include <modules/interface/powertoy_module_interface.h>

class Trace
{
public:
    static void RegisterProvider() noexcept;
    static void UnregisterProvider() noexcept;

    class CropAndLock
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void ActivateReparent() noexcept;
        static void ActivateThumbnail() noexcept;
        static void CreateReparentWindow() noexcept;
        static void CreateThumbnailWindow() noexcept;
        static void SettingsTelemetry(PowertoyModuleIface::Hotkey&, PowertoyModuleIface::Hotkey&) noexcept;
    };
};
