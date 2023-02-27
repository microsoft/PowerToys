#include "pch.h"
#include "trace.h"

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

// Log if the user has PastePlain enabled or disabled
void Trace::EnablePastePlain(const bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PastePlain_EnablePastePlain",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log if the user has invoked PastePlain
void Trace::PastePlainInvoked() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PastePlain_InvokePastePlain",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if a PastePlain invocation has succeeded
void Trace::PastePlainSuccess() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PastePlain_Success",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Log if an error occurs in PastePlain
void Trace::PastePlainError(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "PastePlain_Error",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(methodName.c_str(), "MethodName"),
        TraceLoggingValue(errorCode, "ErrorCode"),
        TraceLoggingValue(errorMessage.c_str(), "ErrorMessage"));
}

// Event to send settings telemetry.
void Trace::SettingsTelemetry(PowertoyModuleIface::Hotkey& hotkey) noexcept
{
    std::wstring hotKeyStr =
        std::wstring(hotkey.win ? L"Win + " : L"") +
        std::wstring(hotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(hotkey.shift ? L"Shift + " : L"") +
        std::wstring(hotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(hotkey.key);

    TraceLoggingWrite(
        g_hProvider,
        "PastePlain_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(hotKeyStr.c_str(), "HotKey")
    );
}
