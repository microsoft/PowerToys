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

// Log if the user has AdvancedPaste enabled or disabled
void Trace::AdvancedPaste_Enable(const bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "AdvancedPaste_EnableAdvancedPaste",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log if the user has invoked AdvancedPaste
void Trace::AdvancedPaste_Invoked(std::wstring mode) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "AdvancedPaste_InvokeAdvancedPaste",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(mode.c_str(), "Mode"));
}

// Log if an error occurs in AdvancedPaste
void Trace::AdvancedPaste_Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "AdvancedPaste_Error",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(methodName.c_str(), "MethodName"),
        TraceLoggingValue(errorCode, "ErrorCode"),
        TraceLoggingValue(errorMessage.c_str(), "ErrorMessage"));
}

// Event to send settings telemetry.
void Trace::AdvancedPaste_SettingsTelemetry(const PowertoyModuleIface::Hotkey& pastePlainHotkey,
                              const PowertoyModuleIface::Hotkey& advancedPasteUIHotkey,
                              const PowertoyModuleIface::Hotkey& pasteMarkdownHotkey,
                              const PowertoyModuleIface::Hotkey& pasteJsonHotkey,
                              const bool preview_custom_format_output) noexcept
{
    std::wstring pastePlainHotkeyStr =
        std::wstring(pastePlainHotkey.win ? L"Win + " : L"") +
        std::wstring(pastePlainHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(pastePlainHotkey.shift ? L"Shift + " : L"") +
        std::wstring(pastePlainHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(pastePlainHotkey.key);

    std::wstring advancedPasteUIHotkeyStr =
        std::wstring(advancedPasteUIHotkey.win ? L"Win + " : L"") +
        std::wstring(advancedPasteUIHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(advancedPasteUIHotkey.shift ? L"Shift + " : L"") +
        std::wstring(advancedPasteUIHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(advancedPasteUIHotkey.key);

    std::wstring pasteMarkdownHotkeyStr =
        std::wstring(pasteMarkdownHotkey.win ? L"Win + " : L"") +
        std::wstring(pasteMarkdownHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(pasteMarkdownHotkey.shift ? L"Shift + " : L"") +
        std::wstring(pasteMarkdownHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(pasteMarkdownHotkey.key);

    std::wstring pasteJsonHotkeyStr =
        std::wstring(pasteJsonHotkey.win ? L"Win + " : L"") +
        std::wstring(pasteJsonHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(pasteJsonHotkey.shift ? L"Shift + " : L"") +
        std::wstring(pasteJsonHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(pasteJsonHotkey.key);

    TraceLoggingWrite(
        g_hProvider,
        "AdvancedPaste_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(pastePlainHotkeyStr.c_str(), "PastePlainHotkey"),
        TraceLoggingWideString(advancedPasteUIHotkeyStr.c_str(), "AdvancedPasteUIHotkey"),
        TraceLoggingWideString(pasteMarkdownHotkeyStr.c_str(), "PasteMarkdownHotkey"),
        TraceLoggingWideString(pasteJsonHotkeyStr.c_str(), "PasteJsonHotkey"),
        TraceLoggingBoolean(preview_custom_format_output, "ShowCustomPreview")
    );
}
