#include "pch.h"
#include "trace.h"

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

void Trace::CropAndLock::Enable(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_EnableCropAndLock",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::CropAndLock::ActivateReparent() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_ActivateReparent",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::ActivateThumbnail() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_ActivateThumbnail",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::CreateReparentWindow() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_CreateReparentWindow",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::CropAndLock::CreateThumbnailWindow() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_CreateThumbnailWindow",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

// Event to send settings telemetry.
void Trace::CropAndLock::SettingsTelemetry(PowertoyModuleIface::Hotkey& reparentHotkey, PowertoyModuleIface::Hotkey& thumbnailHotkey) noexcept
{
    std::wstring hotKeyStrReparent =
        std::wstring(reparentHotkey.win ? L"Win + " : L"") +
        std::wstring(reparentHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(reparentHotkey.shift ? L"Shift + " : L"") +
        std::wstring(reparentHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(reparentHotkey.key);

    std::wstring hotKeyStrThumbnail =
        std::wstring(thumbnailHotkey.win ? L"Win + " : L"") +
        std::wstring(thumbnailHotkey.ctrl ? L"Ctrl + " : L"") +
        std::wstring(thumbnailHotkey.shift ? L"Shift + " : L"") +
        std::wstring(thumbnailHotkey.alt ? L"Alt + " : L"") +
        std::wstring(L"VK ") + std::to_wstring(thumbnailHotkey.key);

    TraceLoggingWrite(
        g_hProvider,
        "CropAndLock_Settings",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingWideString(hotKeyStrReparent.c_str(), "ReparentHotKey"),
        TraceLoggingWideString(hotKeyStrThumbnail.c_str(), "ThumbnailHotkey")
    );
}
