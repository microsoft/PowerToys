#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has CmdNotFound enabled or disabled
    static void EnableCmdNotFoundGpo(const bool enabled) noexcept;
};
