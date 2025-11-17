#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has MousePointerCrosshairs enabled or disabled
    static void EnableMousePointerCrosshairs(const bool enabled) noexcept;

    // Log that the user activated the module by having the crosshairs be drawn
    static void StartDrawingCrosshairs() noexcept;
};
