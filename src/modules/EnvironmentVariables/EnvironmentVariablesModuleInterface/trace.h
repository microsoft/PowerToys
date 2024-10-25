#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has EnvironmentVariables enabled or disabled
    static void EnableEnvironmentVariables(const bool enabled) noexcept;

    // Log that the user tried to activate the editor
    static void ActivateEnvironmentVariables() noexcept;
};
