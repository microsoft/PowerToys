#include "pch.h"
#include "trace.h"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
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

// Log if the user has KBM enabled or disabled - Can also be used to see how often users have to restart the keyboard hook
void Trace::EnableKeyboardManager(const bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_EnableKeyboardManager",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log number of key remaps when the user uses Edit Keyboard and saves settings
void Trace::KeyRemapCount(const DWORD count) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_KeyRemapCount",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(count, "KeyRemapCount"));
}

// Log number of os level shortcut remaps when the user uses Edit Shortcuts and saves settings
void Trace::OSLevelShortcutRemapCount(const DWORD count) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_OSLevelShortcutRemapCount",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(count, "OSLevelShortcutRemapCount"));
}
