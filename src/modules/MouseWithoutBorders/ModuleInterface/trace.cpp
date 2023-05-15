#include "pch.h"
#include "trace.h"

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

#define EventEnableMouseWithoutBordersKey "MouseWithoutBorders_EnableMouseWithoutBorders"
#define EventEnabledKey "Enabled"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::MouseWithoutBorders::Enable(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventEnableMouseWithoutBordersKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, EventEnabledKey));
}

void Trace::MouseWithoutBorders::ToggleServiceRegistration(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "MouseWithoutBorders_ToggleServiceRegistration",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, EventEnabledKey));
}

void Trace::MouseWithoutBorders::Activate() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "MouseWithoutBorders_Activate",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log that the user tried to activate the editor
void Trace::MouseWithoutBorders::AddFirewallRule() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "MouseWithoutBorders_AddFirewallRule",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
