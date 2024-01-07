#include "pch.h"
#include "trace.h"

#include "general_settings.h"

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

void Trace::EventLaunch(const std::wstring& versionNumber, bool isProcessElevated)
{
    TraceLoggingWrite(
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

    TraceLoggingWrite(
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
