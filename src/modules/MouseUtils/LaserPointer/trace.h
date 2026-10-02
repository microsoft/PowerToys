#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has Laser Pointer enabled or disabled
    static void EnableLaserPointer(const bool enabled) noexcept;

    // Log that the user armed the laser pointer with its shortcut, starting a session
    static void StartLaserPointerSession() noexcept;
};
