#include "pch.h"
#include "trace.h"
#include <string>

/*
*
* This file captures the telemetry for the File Explorer Custom Renders project.
* The following telemetry is to be captured for this library:
* (1.) Is the previewer enabled.  
* (2.) File rendered per user in 24 hrs per file time (one for MD, one for SVG)
* (3.) Crashes.
*
*/

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider()
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider()
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::FilePreviewerIsEnabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_ExplorerFilePreview_PreviewIsEnabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::FilePreviewerIsDisabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_ExplorerFilePreview_PreviewIsDisabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ExplorerSVGRenderEnabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_ExplorerFilePreview_SVGRenderEnabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ExplorerSVGRenderDisabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_ExplorerFilePreview_SVGRenderDisabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::PowerPreviewSettingsUpDateFailed(LPCWSTR SettingsName)
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_FilePreview_FailedUpdatingSettings",
        TraceLoggingWideString(SettingsName, "ExceptionMessage"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::PreviewPaneSVGRenderEnabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_PreviewPane_SVGRenderEnabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::PreviewPaneSVGRenderDisabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_PreviewPane__SVGRenderDisabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}


void Trace::PreviewPaneMarkDownRenderDisabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_PreviewPane_MDRenderEnabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::PreviewPaneMarkDownRenderEnabled()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_PreviewPane__MDRenderDisabled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::SetConfigInvalidJSON(const char* exceptionMessage)
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_SetConfig__InvalidJSONGiven",
        TraceLoggingString(exceptionMessage, "ExceptionMessage"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::Destroyed()
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview__Destroyed",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::InitSetErrorLoadingFile(const char* exceptionMessage) 
{
    TraceLoggingWrite(
        g_hProvider,
        "PowerPreview_InitSet__ErrorLoadingFile",
        TraceLoggingString(exceptionMessage, "ExceptionMessage"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

