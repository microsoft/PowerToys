#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

#define LoggingProviderKey "Microsoft.PowerToys"

#define EventEnableDockKey "Dock_EnableDock"
#define EventEnabledKey "Enabled"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::Dock::Enable(bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        EventEnableDockKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, EventEnabledKey));
}
