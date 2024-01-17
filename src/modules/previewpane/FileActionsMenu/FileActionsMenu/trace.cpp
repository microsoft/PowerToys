#include "pch.h"
#include "trace.h"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // 0c3ce679-94d2-4a06-972d-3192f7f8eaea
    (0x3c3ce679, 0x94d2, 0x4a06, 0x97, 0x2d, 0x31, 0x92, 0xf7, 0xf8, 0xea, 0xea),
    TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider()
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider()
{
    TraceLoggingUnregister(g_hProvider);
}

// Log if the user has FileActionsMenu enabled or disabled
void Trace::EnableFileActionsMenu(const bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FileActionsMenu_EnableFileActionsMenu",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

// Log if the user has invoked FileActionsMenu
void Trace::FileActionsMenuInvoked() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FileActionsMenu_InvokeFileActionsMenu",
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
        "FileActionsMenu_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(hotKeyStr.c_str(), "HotKey"));
}
