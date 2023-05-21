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

// Log if the user has Peek enabled or disabled
void Trace::EnablePeek(const bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "Peek_EnablePeek",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log if the user has invoked Peek
void Trace::PeekInvoked() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "Peek_InvokePeek",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
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
        "Peek_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(hotKeyStr.c_str(), "HotKey"));
}
