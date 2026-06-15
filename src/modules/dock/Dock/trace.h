#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace
{
public:
    class Dock : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
    };
};
