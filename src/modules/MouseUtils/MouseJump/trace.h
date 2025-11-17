#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void EnableJumpTool(const bool enabled) noexcept;

    static void InvokeJumpTool() noexcept;
};
