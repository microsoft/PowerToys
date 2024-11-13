#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if ColorPicker is enabled or disabled
    static void EnableColorPicker(const bool enabled) noexcept;

};
