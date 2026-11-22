#include "pch.h"
#include "trace.h"
#include <TraceLoggingProvider.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {6c1f9e2a-4b7d-5d3e-a8c4-1f0b9d2e7a35}
    (0x6c1f9e2a, 0x4b7d, 0x5d3e, 0xa8, 0xc4, 0x1f, 0x0b, 0x9d, 0x2e, 0x7a, 0x35),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider()
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider()
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::Enable(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "ScrollAcceleration_EnableScrollAcceleration",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}
