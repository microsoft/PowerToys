#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log whether the user has Text Expander enabled or disabled.
    static void EnableTextExpander(const bool enabled) noexcept;
};
