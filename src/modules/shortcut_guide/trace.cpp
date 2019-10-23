#include "pch.h"
#include "trace.h"

TRACELOGGING_DEFINE_PROVIDER(
  g_hProvider,
  "Microsoft.PowerToys",
  // {38e8889b-9731-53f5-e901-e8a7c1753074}
  (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
  TraceLoggingOptionProjectTelemetry());

void Trace::RegisterProvider() noexcept {
  TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept {
  TraceLoggingUnregister(g_hProvider);
}

void Trace::HideGuide(const __int64 duration_ms, std::vector<int> &key_pressed) noexcept {
  std::string vk_codes;
  std::vector<int>::iterator it;
  for (it = key_pressed.begin(); it != key_pressed.end(); ) {
    vk_codes += std::to_string(*it);
    if (++it != key_pressed.end()) {
      vk_codes += " ";
    }
  }

  TraceLoggingWrite(
    g_hProvider,
    "ShortcutGuide_HideGuide",
    TraceLoggingInt64(duration_ms, "DurationInMs"),
    TraceLoggingInt64(key_pressed.size(), "NumberOfKeysPressed"),
    TraceLoggingString(vk_codes.c_str(), "ListOfKeysPressed"),
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EnableShortcutGuide(const bool enabled) noexcept {
  TraceLoggingWrite(
    g_hProvider,
    "ShortcutGuide_EnableGuide",
    TraceLoggingBoolean(enabled, "Enabled"),
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::SettingsChanged(const int press_delay_time, const int overlay_opacity, const std::wstring& theme) noexcept {
  TraceLoggingWrite(
    g_hProvider,
    "ShortcutGuide_SettingsChanged",
    TraceLoggingInt32(press_delay_time, "PressDelayTime"),
    TraceLoggingInt32(overlay_opacity, "OverlayOpacity"),
    TraceLoggingWideString(theme.c_str(), "Theme"),
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
