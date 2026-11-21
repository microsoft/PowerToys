#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has WindowTabs enabled or disabled
    static void EnableWindowTabs(const bool enabled) noexcept;

    // Log that the user invoked the module to cycle a window
    static void CycleWindow(const bool forward) noexcept;
};
