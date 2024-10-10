#include "pch.h"
#include "trace.h"

#include <ProjectTelemetry.h>

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::Workspaces::Enable(bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "Workspaces_Enable",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::Workspaces::Launch(bool success, 
    const WorkspacesData::WorkspacesProject& project, 
    InvokePoint invokePoint, 
    double launchTimeSeconds, 
    bool setupIsDifferent, 
    const std::vector<std::pair<std::wstring, std::wstring>> errors) noexcept
{
    int cliCount = 0;
    int adminCount = 0;
    for (const auto& app : project.apps)
    {
        if (!app.commandLineArgs.empty())
        {
            cliCount++;
        }

        if (app.isElevated)
        {
            adminCount++;
        }
    }

    std::string invokePointStr;
    switch (invokePoint)
    {
    case EditorButton:
        invokePointStr = "launchButton";
        break;
    case Shortcut:
        invokePointStr = "shortcut";
        break;
    case LaunchAndEdit:
        invokePointStr = "launchAndEdit";
        break;
    default:
        break;
    }

    std::wstring errorStr{};
    for (const auto& [exeName, errorMessage] : errors)
    {
        errorStr += exeName + L":" + errorMessage + L"; ";
    }
    
    TraceLoggingWriteWrapper(
        g_hProvider,
        "Workspaces_LaunchEvent",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(success, "successful"), // True if launch successfully completely. False if ANY app failed.
        TraceLoggingInt64(project.monitors.size(), "numScreens"), // Number of screens present in the project
        TraceLoggingInt64(project.apps.size(), "appCount"), // Total number of apps in the project
        TraceLoggingInt32(cliCount, "cliCount"), // Number of apps with CLI args
        TraceLoggingInt32(adminCount, "adminCount"), // Number of apps with "Launch as admin" set
        TraceLoggingString(invokePointStr.c_str(), "invokePoint"), // The method by which the user launched the project.
        TraceLoggingFloat64(launchTimeSeconds, "launchTime"), // The time, in seconds, it took for the project to completely launch (from when user invoked launch to when last window successfully moved)
        TraceLoggingBool(setupIsDifferent, "setupDiff"), // True if users monitor setup (in terms of # monitors and monitor resolution & aspect ratio) is different from the setup defined at project creation. False if setup is the same.
        TraceLoggingWideString(errorStr.c_str(), "failures") // List of errors encountered when applicable. Collects .exe name and error message in String fields
        );
}
