#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    static void EnableMouseScrollRemap(const bool enabled) noexcept;
};
