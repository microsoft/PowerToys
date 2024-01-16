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

// Log number of key remaps when the user uses Edit Keyboard and saves settings
void Trace::KeyRemapCount(const DWORD keyToKeyCount, const DWORD keyToShortcutCount, const DWORD keyToTextCount) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_KeyRemapCount",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(keyToKeyCount + keyToShortcutCount, "KeyRemapCount"),
        TraceLoggingValue(keyToKeyCount, "KeyToKeyRemapCount"),
        TraceLoggingValue(keyToShortcutCount, "KeyToShortcutRemapCount"),
        TraceLoggingValue(keyToTextCount, "KeyToTextRemapCount"));
}

// Log number of os level shortcut remaps when the user uses Edit Shortcuts and saves settings
void Trace::OSLevelShortcutRemapCount(const DWORD shortcutToShortcutCount, const DWORD shortcutToKeyCount) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_OSLevelShortcutRemapCount",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(shortcutToShortcutCount + shortcutToKeyCount, "OSLevelShortcutRemapCount"),
        TraceLoggingValue(shortcutToShortcutCount, "OSLevelShortcutToShortcutRemapCount"),
        TraceLoggingValue(shortcutToKeyCount, "OSLevelShortcutToKeyRemapCount"));
}

// Log number of app specific shortcut remaps when the user uses Edit Shortcuts and saves settings
void Trace::AppSpecificShortcutRemapCount(const DWORD shortcutToShortcutCount, const DWORD shortcutToKeyCount) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_AppSpecificShortcutRemapCount",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(shortcutToShortcutCount + shortcutToKeyCount, "AppSpecificShortcutRemapCount"),
        TraceLoggingValue(shortcutToShortcutCount, "AppSpecificShortcutToShortcutRemapCount"),
        TraceLoggingValue(shortcutToKeyCount, "AppSpecificShortcutToKeyRemapCount"));
}

// Log if an error occurs in KBM
void Trace::Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "KeyboardManager_Error",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(methodName.c_str(), "MethodName"),
        TraceLoggingValue(errorCode, "ErrorCode"),
        TraceLoggingValue(errorMessage.c_str(), "ErrorMessage"));
}
