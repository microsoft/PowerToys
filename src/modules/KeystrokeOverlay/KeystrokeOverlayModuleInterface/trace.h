#pragma once

#include <common/Telemetry/TraceBase.h>

class Trace : public telemetry::TraceBase
{
public:
    static void RegisterProvider();
    static void UnregisterProvider();
    
    // Log if the user has Keystroke Overlay enabled or disabled
    static void EnableKeystrokeOverlay(const bool enabled) noexcept;

};
