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

void Trace::LightSwitch::Enable(bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LightSwitch_EnableLightSwitch",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::LightSwitch::ShortcutInvoked() noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LightSwitch_ShortcutInvoked",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::LightSwitch::ScheduleModeToggled(const std::wstring& newMode) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LightSwitch_ScheduleModeToggled",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(newMode.c_str(), "NewMode"));
}

void Trace::LightSwitch::ThemeTargetChanged(bool changeApps, bool changeSystem) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "LightSwitch_ThemeTargetChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(changeApps, "ChangeApps"),
        TraceLoggingBoolean(changeSystem, "ChangeSystem"));
}