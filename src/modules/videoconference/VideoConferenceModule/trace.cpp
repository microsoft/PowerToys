#include "pch.h"

#include "trace.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// Log if the user has VCM enabled or disabled
void Trace::EnableVideoConference(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "VideoConference_EnableVideoConference",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::SettingsChanged(const struct VideoConferenceSettings& settings) noexcept
{
    bool CustomOverlayImage = (settings.imageOverlayPath.length() > 0);

    TraceLoggingWriteWrapper(
        g_hProvider,
        "VideoConference_SettingsChanged",
        TraceLoggingWideString(settings.toolbarPositionString.c_str(), "ToolbarPosition"),
        TraceLoggingWideString(settings.toolbarMonitorString.c_str(), "ToolbarMonitorSelection"),
        TraceLoggingBool(CustomOverlayImage, "CustomImageOverlayUsed"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::MicrophoneMuted() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "VideoConference_MicrophoneMuted",
        TraceLoggingBoolean(true, "MicrophoneMuted"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CameraMuted() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "VideoConference_CameraMuted",
        TraceLoggingBoolean(true, "CameraMuted"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
