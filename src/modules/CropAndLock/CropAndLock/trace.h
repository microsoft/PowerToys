#pragma once

#include <common/Telemetry/TraceBase.h>
#include <modules/interface/powertoy_module_interface.h>

class Trace
{
public:
    class CropAndLock : public telemetry::TraceBase
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
