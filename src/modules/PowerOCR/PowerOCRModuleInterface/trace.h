#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has PowerOCR enabled or disabled
    static void EnablePowerOCR(const bool enabled) noexcept;
};
