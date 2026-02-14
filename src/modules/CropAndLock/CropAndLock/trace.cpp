#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::CropAndLock::Enable(bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_EnableCropAndLock",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::CropAndLock::ActivateReparent() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_ActivateReparent",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::ActivateThumbnail() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_ActivateThumbnail",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::ActivateScreenshot() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_ActivateScreenshot",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::CreateReparentWindow() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_CreateReparentWindow",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::CreateThumbnailWindow() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_CreateThumbnailWindow",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::CreateScreenshotWindow() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "CropAndLock_CreateScreenshotWindow",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

