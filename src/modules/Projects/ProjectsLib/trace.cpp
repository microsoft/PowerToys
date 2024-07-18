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

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::Projects::Enable(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "Projects_EnableProjects",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::Projects::SettingsTelemetry(const PowertoyModuleIface::HotkeyEx& hotkey) noexcept
{
    std::wstring hotKeyStr =
        std::wstring((hotkey.modifiersMask & MOD_WIN) == MOD_WIN ? L"Win + " : L"") +
        std::wstring((hotkey.modifiersMask & MOD_CONTROL) == MOD_CONTROL ? L"Ctrl + " : L"") +
        std::wstring((hotkey.modifiersMask & MOD_SHIFT) == MOD_SHIFT ? L"Shift + " : L"") +
        std::wstring((hotkey.modifiersMask & MOD_ALT) == MOD_ALT ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(hotkey.vkCode);

    TraceLoggingWrite(
        g_hProvider,
        "Projects_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(hotKeyStr.c_str(), "HotKey"));
}
