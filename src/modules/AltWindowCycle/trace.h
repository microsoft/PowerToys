#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has AltWindowCycle enabled or disabled
    static void EnableAltWindowCycle(const bool enabled) noexcept;

    // Log that the user invoked the module to cycle a window
    static void CycleWindow(const bool forward) noexcept;
};
