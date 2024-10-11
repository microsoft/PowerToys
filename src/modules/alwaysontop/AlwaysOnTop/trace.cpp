#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

#define EventEnableAlwaysOnTopKey "AlwaysOnTop_EnableAlwaysOnTop"
#define EventPinWindowKey "AlwaysOnTop_PinWindow"
#define EventUnpinWindowKey "AlwaysOnTop_UnpinWindow"
#define EventEnabledKey "Enabled"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::AlwaysOnTop::Enable(bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        EventEnableAlwaysOnTopKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, EventEnabledKey));
}

void Trace::AlwaysOnTop::PinWindow() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        EventPinWindowKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::AlwaysOnTop::UnpinWindow() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        EventUnpinWindowKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
