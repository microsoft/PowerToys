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

// Log if a key remap has been invoked (not being used currently, due to being garrulous)
void Trace::KeyRemapInvoked(bool isKeyToKey) noexcept
{
    if (isKeyToKey)
    {
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_KeyToKeyRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
    }
    else
    {
        TraceLoggingWrite(
            g_hProvider,
            "KeyboardManager_KeyToShortcutRemapInvoked",
            ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
            TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
    }
}

// Log if a shortcut remap has been invoked (not being used currently, due to being garrulous)
void Trace::ShortcutRemapInvoked(bool isShortcutToShortcut, bool isAppSpecific) noexcept
{
    if (isAppSpecific)
    {
        if (isShortcutToShortcut)
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_AppSpecificShortcutToShortcutRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
        else
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_AppSpecificShortcutToKeyRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
    }
    else
    {
        if (isShortcutToShortcut)
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_OSLevelShortcutToShortcutRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
        else
        {
            TraceLoggingWrite(
                g_hProvider,
                "KeyboardManager_OSLevelShortcutToKeyRemapInvoked",
                ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
                TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
        }
    }
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
