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

// Not in the telemetry provider group, so diagnostic data collection doesn't enable it.
TRACELOGGING_DEFINE_PROVIDER(
    g_hPerformanceProvider,
    "Microsoft.PowerToys.Performance",
    // {9d83a68b-e53f-5d64-0e80-e3a9faf69485}
    (0x9d83a68b, 0xe53f, 0x5d64, 0x0e, 0x80, 0xe3, 0xa9, 0xfa, 0xf6, 0x94, 0x85));

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
        TraceLoggingBoolean(settings.includePrereleaseUpdates, "IncludePrereleaseUpdates"),
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

void Trace::TrayIconLeftClick(bool quickAccessEnabled)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "TrayIcon_LeftClick",
        TraceLoggingBoolean(quickAccessEnabled, "QuickAccessEnabled"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::TrayIconDoubleClick(bool quickAccessEnabled)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "TrayIcon_DoubleClick",
        TraceLoggingBoolean(quickAccessEnabled, "QuickAccessEnabled"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::TrayIconRightClick(bool quickAccessEnabled)
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "TrayIcon_RightClick",
        TraceLoggingBoolean(quickAccessEnabled, "QuickAccessEnabled"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::RegisterPerformanceProvider()
{
    TraceLoggingRegister(g_hPerformanceProvider);
}

void Trace::UnregisterPerformanceProvider()
{
    TraceLoggingUnregister(g_hPerformanceProvider);
}

void Trace::StartupStage(const char* stage, uint64_t msSinceProcessStart)
{
    TraceLoggingWrite(
        g_hPerformanceProvider,
        "Runner_StartupStage",
        TraceLoggingString(stage, "Stage"),
        TraceLoggingUInt64(msSinceProcessStart, "MsSinceProcessStart"));
}
