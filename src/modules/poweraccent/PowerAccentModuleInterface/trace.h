#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has PowerAccent enabled or disabled
    static void EnablePowerAccent(const bool enabled) noexcept;
};
