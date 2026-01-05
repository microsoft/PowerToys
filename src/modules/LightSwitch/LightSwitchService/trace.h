#pragma once

#include <common/Telemetry/TraceBase.h>
#include <string>

class Trace
{
public:
    class LightSwitch : public telemetry::TraceBase
    {
    public:
        static void RegisterProvider();
        static void UnregisterProvider();
        static void ScheduleModeToggled(const std::wstring& newMode) noexcept;
        static void ThemeTargetChanged(bool changeApps, bool changeSystem) noexcept;
    };
};
