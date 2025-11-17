#pragma once

#include <common/Telemetry/TraceBase.h>
class Trace : public telemetry::TraceBase
{
public:
    // Log if the user has ZoomIt enabled or disabled
    static void EnableZoomIt(const bool enabled) noexcept;
    static void ZoomItStarted() noexcept;
    static void ZoomItActivateBreak() noexcept;
    static void ZoomItActivateDraw() noexcept;
    static void ZoomItActivateZoom() noexcept;
    static void ZoomItActivateLiveZoom() noexcept;
    static void ZoomItActivateDemoType() noexcept;
    static void ZoomItActivateRecord() noexcept;
    static void ZoomItActivateSnip() noexcept;
};
