#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::EnableImageResizer(_In_ bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ImageResizer_EnableImageResizer",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::Invoked() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ImageResizer_Invoked",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::InvokedRet(_In_ HRESULT hr) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ImageResizer_InvokedRet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::QueryContextMenuError(_In_ HRESULT hr) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ImageResizer_QueryContextMenuError",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingHResult(hr),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
