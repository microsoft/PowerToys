#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void EnableAutoHideCursor(bool enabled) noexcept;
};
