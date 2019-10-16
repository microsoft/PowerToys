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

void Trace::EventShow() noexcept {
  TraceLoggingWrite(
    g_hProvider,
    "ShortcutGuide::Event::ShowGuide",
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EventHide(const __int64 duration_ms, std::vector<int> &key_pressed) noexcept {
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
    "ShortcutGuide::Event::HideGuide",
    TraceLoggingInt64(duration_ms, "Duration in ms"),
    TraceLoggingInt64(key_pressed.size(), "# of key pressed"),
    TraceLoggingString(vk_codes.c_str(), "list of key pressed"),
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::EnableShortcutGuide(bool enabled) noexcept {
  TraceLoggingWrite(
    g_hProvider,
    "ShortcutGuide::Event::EnableGuide",
    TraceLoggingBoolean(enabled, "Enabled"),
    ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
    TraceLoggingBoolean(TRUE, "UTCReplace_AppSessionGuid"),
    TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}
