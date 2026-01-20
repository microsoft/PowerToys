#include "pch.h"
#include "trace.h"

#include "general_settings.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::EventLaunch(const std::wstring& versionNumber, bool isProcessElevated)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "Runner_Launch",
        TraceLoggingWideString(versionNumber.c_str(), "Version"),
        TraceLoggingBoolean(isProcessElevated, "Elevated"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::SettingsChanged(const GeneralSettings& settings)
{
    std::wstring enabledModules;
    for (const auto& [name, isEnabled] : settings.isModulesEnabledMap)
    {
        if (isEnabled)
        {
            if (!enabledModules.empty())
            {
                enabledModules += L", ";
            }

            enabledModules += name;
        }
    }

    TraceLoggingWriteWrapper(
        g_hProvider,
        "GeneralSettingsChanged",
        TraceLoggingBoolean(settings.isStartupEnabled, "RunAtStartup"),
        TraceLoggingBoolean(settings.enableWarningsElevatedApps, "EnableWarningsElevatedApps"),
        TraceLoggingWideString(settings.startupDisabledReason.c_str(), "StartupDisabledReason"),
        TraceLoggingWideString(enabledModules.c_str(), "ModulesEnabled"),
        TraceLoggingBoolean(settings.isRunElevated, "AlwaysRunElevated"),
        TraceLoggingBoolean(settings.downloadUpdatesAutomatically, "DownloadUpdatesAutomatically"),
        TraceLoggingBoolean(settings.enableExperimentation, "EnableExperimentation"),
        TraceLoggingWideString(settings.theme.c_str(), "Theme"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::UpdateCheckCompleted(bool success, bool updateAvailable, const std::wstring& fromVersion, const std::wstring& toVersion)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "UpdateCheck_Completed",
        TraceLoggingBoolean(success, "Success"),
        TraceLoggingBoolean(updateAvailable, "UpdateAvailable"),
        TraceLoggingWideString(fromVersion.c_str(), "FromVersion"),
        TraceLoggingWideString(toVersion.c_str(), "ToVersion"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::UpdateDownloadCompleted(bool success, const std::wstring& version)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "UpdateDownload_Completed",
        TraceLoggingBoolean(success, "Success"),
        TraceLoggingWideString(version.c_str(), "Version"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
