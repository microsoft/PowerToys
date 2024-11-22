#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableMeasureTool(const bool enabled) noexcept;

    static void BoundsToolActivated() noexcept;
    static void MeasureToolActivated() noexcept;
};
