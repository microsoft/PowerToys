#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has Awake enabled or disabled
    static void EnableAwake(const bool enabled) noexcept;
};
