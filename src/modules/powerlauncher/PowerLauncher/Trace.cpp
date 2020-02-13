#include "pch.h"

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

void Trace::PowerToyIsEnabled()
{
	TraceLoggingWrite(
		g_hProvider,
		"PowerLauncher_IsEnabled",
		ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
		TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
		TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::PowerToyIsDisabled()
{
	TraceLoggingWrite(
		g_hProvider,
		"PowerLauncher_IsDisabled",
		ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
		TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
		TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::SetConfigInvalidJSON(const char* exceptionMessage)
{
	TraceLoggingWrite(
		g_hProvider,
		"PowerLauncher_SetConfig__InvalidJSONGiven",
		TraceLoggingString(exceptionMessage, "ExceptionMessage"),
		ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
		TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
		TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::InitSetErrorLoadingFile(const char* exceptionMessage)
{
	TraceLoggingWrite(
		g_hProvider,
		"PowerLauncher_InitSet__ErrorLoadingFile",
		TraceLoggingString(exceptionMessage, "ExceptionMessage"),
		ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
		TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
		TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::Destroy()
{
	TraceLoggingWrite(
		g_hProvider,
		"PowerLauncher_Destroy",
		ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
		TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
		TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}