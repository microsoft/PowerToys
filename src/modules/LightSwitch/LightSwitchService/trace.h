#pragma once

#include <common/Telemetry/TraceBase.h>
#include <modules/interface/powertoy_module_interface.h>

class Trace
{
public:
    class LightSwitch : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void ShortcutInvoked() noexcept;
        static void ScheduleModeToggled(const std::wstring& newMode) noexcept;
        static void ThemeTargetChanged(bool changeApps, bool changeSystem) noexcept;
    };
};
