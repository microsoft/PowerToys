#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has Laser Pointer enabled or disabled
    static void EnableLaserPointer(const bool enabled) noexcept;

    // Log that the user started drawing a laser trail
    static void StartLaserPointerSession() noexcept;
};
