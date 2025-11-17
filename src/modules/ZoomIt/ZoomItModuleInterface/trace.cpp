#include "pch.h"
#include "trace.h"
#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// Log if the user has ZoomIt enabled or disabled
void Trace::EnableZoomIt(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_EnableZoomIt",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::ZoomItStarted() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_Started",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateBreak() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateBreak",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateDraw() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateDraw",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateZoom() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateZoom",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateLiveZoom() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateLiveZoom",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateDemoType() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateDemoType",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateRecord() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateRecord",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoomItActivateSnip() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ZoomIt_ActivateSnip",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
