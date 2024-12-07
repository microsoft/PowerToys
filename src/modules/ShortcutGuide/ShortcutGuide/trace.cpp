#include "pch.h"
#include "trace.h"

#include <common/Telemetry/TraceBase.h>

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

void Trace::SendGuideSession(const __int64 duration_ms, const wchar_t* close_type) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ShortcutGuide_GuideSession",
        TraceLoggingInt64(duration_ms, "DurationInMs"),
        TraceLoggingWideString(close_type, "CloseType"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::SendSettings(ShortcutGuideSettings settings) noexcept
{
    TraceLoggingWriteWrapper(
        g_hProvider,
        "ShortcutGuide_Settings",
        TraceLoggingWideString(settings.hotkey.c_str(), "Hotkey"),
        TraceLoggingInt32(settings.overlayOpacity, "OverlayOpacity"),
        TraceLoggingWideString(settings.theme.c_str(), "Theme"),
        TraceLoggingWideString(settings.disabledApps.c_str(), "DisabledApps"),
        TraceLoggingBoolean(settings.shouldReactToPressedWinKey, "ShouldReactToPressedWinKey"),
        TraceLoggingInt32(settings.windowsKeyPressTimeForGlobalWindowsShortcuts, "WindowsKeyPressTimeForGlobalWindowsShortcuts"),
        TraceLoggingInt32(settings.windowsKeyPressTimeForTaskbarIconShortcuts, "WindowsKeyPressTimeForTaskbarIconShortcuts"),
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
