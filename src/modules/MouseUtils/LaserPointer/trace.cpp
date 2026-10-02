#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// Log if the user has Laser Pointer enabled or disabled
void Trace::EnableLaserPointer(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LaserPointer_EnableLaserPointer",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log that the user armed the laser pointer with its shortcut, starting a session
void Trace::StartLaserPointerSession() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LaserPointer_StartLaserPointerSession",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
