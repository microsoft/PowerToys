#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace
{
public:
    class AlwaysOnTop : public telemetry::TraceBase
    {
    public:
        static void Enable(bool enabled) noexcept;
        static void PinWindow() noexcept;
        static void UnpinWindow() noexcept;
    };
};
