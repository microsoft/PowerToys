#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void EnableSeamlessCursor(const bool enabled) noexcept;
    static void CalibrationSaved(const int borders, const int enabled, const int invalid) noexcept;
};
