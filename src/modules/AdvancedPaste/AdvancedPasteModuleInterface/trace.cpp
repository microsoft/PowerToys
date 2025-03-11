#include "pch.h"
#include "trace.h"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

// Log if the user has AdvancedPaste enabled or disabled
void Trace::AdvancedPaste_Enable(const bool enabled) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "AdvancedPaste_EnableAdvancedPaste",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log if the user has invoked AdvancedPaste
void Trace::AdvancedPaste_Invoked(std::wstring mode) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "AdvancedPaste_InvokeAdvancedPaste",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(mode.c_str(), "Mode"));
}

// Log if an error occurs in AdvancedPaste
void Trace::AdvancedPaste_Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWriteWrapper(
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
                              const bool is_advanced_ai_enabled,
                              const bool preview_custom_format_output,
                              const std::unordered_map<std::wstring, PowertoyModuleIface::Hotkey>& additionalActionsHotkeys) noexcept
{
    const auto getHotKeyStr = [](const PowertoyModuleIface::Hotkey& hotKey)
    {
        return std::wstring(hotKey.win ? L"Win + " : L"") +
               std::wstring(hotKey.ctrl ? L"Ctrl + " : L"") +
               std::wstring(hotKey.shift ? L"Shift + " : L"") +
               std::wstring(hotKey.alt ? L"Alt + " : L"") +
               std::wstring(L"VK ") + std::to_wstring(hotKey.key);
    };

    std::vector<std::wstring> hotkeyStrs;
    const auto getHotkeyCStr = [&](const PowertoyModuleIface::Hotkey& hotkey)
    {
        hotkeyStrs.push_back(getHotKeyStr(hotkey)); // Probably unnecessary, but offers protection against the macro TraceLoggingWideString expanding to something that would invalidate the pointer
        return hotkeyStrs.back().c_str();
    };

    const auto getAdditionalActionHotkeyCStr = [&](const std::wstring& name)
    {
        const auto it = additionalActionsHotkeys.find(name);
        return it != additionalActionsHotkeys.end() ? getHotkeyCStr(it->second) : L"";
    };

    TraceLoggingWriteWrapper(
        g_hProvider,
        "AdvancedPaste_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(getHotkeyCStr(pastePlainHotkey), "PastePlainHotkey"),
        TraceLoggingWideString(getHotkeyCStr(advancedPasteUIHotkey), "AdvancedPasteUIHotkey"),
        TraceLoggingWideString(getHotkeyCStr(pasteMarkdownHotkey), "PasteMarkdownHotkey"),
        TraceLoggingWideString(getHotkeyCStr(pasteJsonHotkey), "PasteJsonHotkey"),
        TraceLoggingBoolean(is_advanced_ai_enabled, "IsAdvancedAIEnabled"),
        TraceLoggingBoolean(preview_custom_format_output, "ShowCustomPreview"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"ImageToText"), "ImageToTextHotkey"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"PasteAsTxtFile"), "PasteAsTxtFileHotkey"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"PasteAsPngFile"), "PasteAsPngFileHotkey"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"PasteAsHtmlFile"), "PasteAsHtmlFileHotkey"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"TranscodeToMp3"), "TranscodeToMp3Hotkey"),
        TraceLoggingWideString(getAdditionalActionHotkeyCStr(L"TranscodeToMp4"), "TranscodeToMp4Hotkey")
    );
}
