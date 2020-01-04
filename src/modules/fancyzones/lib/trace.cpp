#include "pch.h"
#include "trace.h"
#include "lib/ZoneSet.h"
#include "lib/Settings.h"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    "Microsoft.PowerToys",
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

struct ZoneSetInfo
{
    size_t NumberOfZones = 0;
    size_t NumberOfWindows = 0;
};

ZoneSetInfo GetZoneSetInfo(_In_opt_ winrt::com_ptr<IZoneSet> set) noexcept
{
    ZoneSetInfo info;
    if (set)
    {
        auto zones = set->GetZones();
        info.NumberOfZones = zones.size();
        info.NumberOfWindows = std::count_if(zones.cbegin(), zones.cend(), [&](winrt::com_ptr<IZone> zone)
        {
            return !zone->IsEmpty();
        });
    }
    return info;
}

void Trace::RegisterProvider() noexcept
{
    TraceLoggingRegister(g_hProvider);
}

void Trace::UnregisterProvider() noexcept
{
    TraceLoggingUnregister(g_hProvider);
}

void Trace::FancyZones::EnableFancyZones(bool enabled) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_EnableFancyZones",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, "Enabled"));
}

void Trace::FancyZones::OnKeyDown(DWORD vkCode, bool win, bool control, bool inMoveSize) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_OnKeyDown",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(vkCode, "Hotkey"),
        TraceLoggingBoolean(win, "WindowsKey"),
        TraceLoggingBoolean(control, "ControlKey"),
        TraceLoggingBoolean(inMoveSize, "InMoveSize"));
}

void Trace::SettingsChanged(const Settings& settings) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_SettingsChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(settings.shiftDrag, "ShiftDrag"),
        TraceLoggingBoolean(settings.displayChange_moveWindows, "MoveWindowsOnDisplayChange"),
        TraceLoggingBoolean(settings.virtualDesktopChange_moveWindows, "MoveWindowsOnVirtualDesktopChange"),
        TraceLoggingBoolean(settings.zoneSetChange_flashZones, "FlashZonesOnZoneSetChange"),
        TraceLoggingBoolean(settings.zoneSetChange_moveWindows, "MoveWindowsOnZoneSetChange"),
        TraceLoggingBoolean(settings.overrideSnapHotkeys, "OverrideSnapHotKeys"),
        TraceLoggingWideString(settings.zoneHightlightColor.c_str(), "ZoneHighlightColor"));
}

void Trace::VirtualDesktopChanged() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_VirtualDesktopChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::ZoneWindow::KeyUp(WPARAM wParam) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_ZoneWindowKeyUp",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(wParam, "KeyboardValue"));
}

void Trace::ZoneWindow::MoveSizeEnd(_In_opt_ winrt::com_ptr<IZoneSet> activeSet) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeSet);
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_MoveSizeEnd",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeSet.get()), "ActiveSet"),
        TraceLoggingValue(zoneInfo.NumberOfZones, "NumberOfZones"),
        TraceLoggingValue(zoneInfo.NumberOfWindows, "NumberOfWindows"));
}

void Trace::ZoneWindow::CycleActiveZoneSet(_In_opt_ winrt::com_ptr<IZoneSet> activeSet, InputMode mode) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeSet);
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_CycleActiveZoneSet",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeSet.get()), "ActiveSet"),
        TraceLoggingValue(zoneInfo.NumberOfZones, "NumberOfZones"),
        TraceLoggingValue(zoneInfo.NumberOfWindows, "NumberOfWindows"),
        TraceLoggingValue(static_cast<int>(mode), "InputMode"));
}
