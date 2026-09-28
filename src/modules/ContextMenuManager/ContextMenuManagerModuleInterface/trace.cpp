#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {b6f6a3a1-6e3c-4f7a-9b1a-1b7a6a8f9c21}
    (0xb6f6a3a1, 0x6e3c, 0x4f7a, 0x9b, 0x1a, 0x1b, 0x7a, 0x6a, 0x8f, 0x9c, 0x21),
    TraceLoggingOptionProjectTelemetry());

// Log if the user has Context Menu Manager enabled or disabled
void Trace::EnableContextMenuManager(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ContextMenuManager_EnableContextMenuManager",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}
