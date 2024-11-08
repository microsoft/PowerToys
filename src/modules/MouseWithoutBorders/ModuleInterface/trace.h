#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace
{
public:
    class MouseWithoutBorders : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void ToggleServiceRegistration(bool enabled) noexcept;
        static void Activate() noexcept;
        static void AddFirewallRule() noexcept;
    };
};
