#include "pch.h"
#include "trace.h"
#include "FancyZonesLib/Layout.h"
#include "FancyZonesLib/LayoutAssignedWindows.h"
#include "FancyZonesLib/Settings.h"
#include "FancyZonesData/AppZoneHistory.h"
#include "FancyZonesLib/FancyZonesData/AppliedLayouts.h"
#include "FancyZonesLib/FancyZonesData/CustomLayouts.h"
#include "FancyZonesLib/FancyZonesData/LayoutHotkeys.h"
#include "FancyZonesLib/FancyZonesDataTypes.h"
#include "FancyZonesLib/util.h"

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

#define EventEnableFancyZonesKey "FancyZones_EnableFancyZones"
#define EventKeyDownKey "FancyZones_OnKeyDown"
#define EventZoneSettingsChangedKey "FancyZones_ZoneSettingsChanged"
#define EventEditorLaunchKey "FancyZones_EditorLaunch"
#define EventSettingsKey "FancyZones_Settings"
#define EventDesktopChangedKey "FancyZones_VirtualDesktopChanged"
#define EventWorkAreaKeyUpKey "FancyZones_ZoneWindowKeyUp"
#define EventSnapNewWindowIntoZone "FancyZones_SnapNewWindowIntoZone"
#define EventKeyboardSnapWindowToZone "FancyZones_KeyboardSnapWindowToZone"
#define EventMoveOrResizeStartedKey "FancyZones_MoveOrResizeStarted"
#define EventMoveOrResizeEndedKey "FancyZones_MoveOrResizeEnded"
#define EventCycleActiveZoneSetKey "FancyZones_CycleActiveZoneSet"
#define EventQuickLayoutSwitchKey "FancyZones_QuickLayoutSwitch"

#define EventEnabledKey "Enabled"
#define PressedKeyCodeKey "Hotkey"
#define PressedWindowKey "WindowsKey"
#define PressedControlKey "ControlKey"
#define MoveSizeActionKey "InMoveSize"
#define AppsInHistoryCountKey "AppsInHistoryCount"
#define CustomZoneSetCountKey "CustomZoneSetCount"
#define LayoutUsingQuickKeyCountKey "LayoutUsingQuickKeyCount"
#define NumberOfZonesForEachCustomZoneSetKey "NumberOfZonesForEachCustomZoneSet"
#define ActiveZoneSetsCountKey "ActiveZoneSetsCount"
#define ActiveZoneSetsListKey "ActiveZoneSetsList"
#define EditorLaunchValueKey "Value"
#define ShiftDragKey "ShiftDrag"
#define MouseSwitchKey "MouseSwitch"
#define MoveWindowsOnDisplayChangeKey "MoveWindowsOnDisplayChange"
#define FlashZonesOnZoneSetChangeKey "FlashZonesOnZoneSetChange"
#define MoveWindowsOnZoneSetChangeKey "MoveWindowsOnZoneSetChange"
#define OverrideSnapHotKeysKey "OverrideSnapHotKeys"
#define MoveWindowAcrossMonitorsKey "MoveWindowAcrossMonitors"
#define MoveWindowsBasedOnPositionKey "MoveWindowsBasedOnPosition"
#define MoveWindowsToLastZoneOnAppOpeningKey "MoveWindowsToLastZoneOnAppOpening"
#define OpenWindowOnActiveMonitorKey "OpenWindowOnActiveMonitor"
#define RestoreSizeKey "RestoreSize"
#define QuickLayoutSwitchKey "QuickLayoutSwitch"
#define FlashZonesOnQuickSwitchKey "FlashZonesOnQuickSwitch"
#define UseCursorPosOnEditorStartupKey "UseCursorPosOnEditorStartup"
#define ShowZonesOnAllMonitorsKey "ShowZonesOnAllMonitors"
#define SpanZonesAcrossMonitorsKey "SpanZonesAcrossMonitors"
#define MakeDraggedWindowTransparentKey "MakeDraggedWindowTransparent"
#define AllowSnapChildWindows "AllowSnapChildWindows"
#define DisableRoundCornersOnSnapping "DisableRoundCornersOnSnapping"
#define ZoneColorKey "ZoneColor"
#define ZoneBorderColorKey "ZoneBorderColor"
#define ZoneHighlightColorKey "ZoneHighlightColor"
#define ZoneHighlightOpacityKey "ZoneHighlightOpacity"
#define EditorHotkeyKey "EditorHotkey"
#define WindowSwitchingToggleKey "WindowSwitchingToggle"
#define NextTabHotkey "NextTabHotkey"
#define PrevTabHotkey "PrevTabHotkey"
#define ExcludedAppsCountKey "ExcludedAppsCount"
#define KeyboardValueKey "KeyboardValue"
#define ActiveSetKey "ActiveSet"
#define NumberOfZonesKey "NumberOfZones"
#define NumberOfWindowsKey "NumberOfWindows"
#define InputModeKey "InputMode"
#define OverlappingZonesAlgorithmKey "OverlappingZonesAlgorithm"
#define QuickLayoutSwitchedWithShortcutUsed "ShortcutUsed"

TRACELOGGING_DEFINE_PROVIDER(
    g_hProvider,
    LoggingProviderKey,
    // {38e8889b-9731-53f5-e901-e8a7c1753074}
    (0x38e8889b, 0x9731, 0x53f5, 0xe9, 0x01, 0xe8, 0xa7, 0xc1, 0x75, 0x30, 0x74),
    TraceLoggingOptionProjectTelemetry());

struct ZoneSetInfo
{
    size_t NumberOfZones = 0;
    size_t NumberOfWindows = 0;
};


ZoneSetInfo GetZoneSetInfo(_In_opt_ Layout* layout, const LayoutAssignedWindows& layoutWindows) noexcept
{
    ZoneSetInfo info;
    if (layout)
    {
        auto zones = layout->Zones();
        info.NumberOfZones = zones.size();
        info.NumberOfWindows = 0;
        for (int i = 0; i < static_cast<int>(zones.size()); i++)
        {
            if (!layoutWindows.IsZoneEmpty(i))
            {
                info.NumberOfWindows++;
            }
        }
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
        EventEnableFancyZonesKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(enabled, EventEnabledKey));
}

void Trace::FancyZones::OnKeyDown(DWORD vkCode, bool win, bool control, bool inMoveSize) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventKeyDownKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(vkCode, PressedKeyCodeKey),
        TraceLoggingBoolean(win, PressedWindowKey),
        TraceLoggingBoolean(control, PressedControlKey),
        TraceLoggingBoolean(inMoveSize, MoveSizeActionKey));
}

void Trace::FancyZones::DataChanged() noexcept
{
    int appsHistorySize = static_cast<int>(AppZoneHistory::instance().GetFullAppZoneHistory().size());
    const auto& customZones = CustomLayouts::instance().GetAllLayouts();
    const auto& layouts = AppliedLayouts::instance().GetAppliedLayoutMap();
    auto quickKeysCount = LayoutHotkeys::instance().GetHotkeysCount();

    std::unique_ptr<INT32[]> customZonesArray(new (std::nothrow) INT32[customZones.size()]);
    if (!customZonesArray)
    {
        return;
    }

    auto getCustomZoneCount = [](const std::variant<FancyZonesDataTypes::CanvasLayoutInfo, FancyZonesDataTypes::GridLayoutInfo>& layoutInfo) -> int {
        if (std::holds_alternative<FancyZonesDataTypes::GridLayoutInfo>(layoutInfo))
        {
            const auto& info = std::get<FancyZonesDataTypes::GridLayoutInfo>(layoutInfo);
            return (info.rows() * info.columns());
        }
        else if (std::holds_alternative<FancyZonesDataTypes::CanvasLayoutInfo>(layoutInfo))
        {
            const auto& info = std::get<FancyZonesDataTypes::CanvasLayoutInfo>(layoutInfo);
            return static_cast<int>(info.zones.size());
        }
        return 0;
    };

    // NumberOfZonesForEachCustomZoneSet
    int i = 0;
    for (const auto& [id, customZoneSetData] : customZones)
    {
        customZonesArray.get()[i] = getCustomZoneCount(customZoneSetData.info);
        i++;
    }

    // ActiveZoneSetsList
    std::wstring activeZoneSetInfo;
    for (const auto& [id, layout] : layouts)
    {
        const FancyZonesDataTypes::ZoneSetLayoutType type = layout.type;
        if (!activeZoneSetInfo.empty())
        {
            activeZoneSetInfo += L"; ";
        }
        activeZoneSetInfo += L"type: " + FancyZonesDataTypes::TypeToString(type);

        int zoneCount = -1;
        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Custom)
        {
            auto guid = layout.uuid;
            const auto& activeCustomZone = customZones.find(guid);
            if (activeCustomZone != customZones.end())
            {
                zoneCount = getCustomZoneCount(activeCustomZone->second.info);
            }  
        }
        else
        {
            zoneCount = layout.zoneCount;
        }

        if (zoneCount != -1)
        {
            activeZoneSetInfo += L", zone count: " + std::to_wstring(zoneCount);
        }
        else
        {
            activeZoneSetInfo += L", custom zone data was deleted";
        }
    }
    TraceLoggingWrite(
        g_hProvider,
        EventZoneSettingsChangedKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingInt32(appsHistorySize, AppsInHistoryCountKey),
        TraceLoggingInt32(static_cast<int>(customZones.size()), CustomZoneSetCountKey),
        TraceLoggingInt32Array(customZonesArray.get(), static_cast<uint16_t>(customZones.size()), NumberOfZonesForEachCustomZoneSetKey),
        TraceLoggingInt32(static_cast<int>(layouts.size()), ActiveZoneSetsCountKey),
        TraceLoggingWideString(activeZoneSetInfo.c_str(), ActiveZoneSetsListKey),
        TraceLoggingInt32(static_cast<int>(quickKeysCount), LayoutUsingQuickKeyCountKey));
}

void Trace::FancyZones::EditorLaunched(int value) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventEditorLaunchKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingInt32(value, EditorLaunchValueKey));
}

// Log if an error occurs in FZ
void Trace::FancyZones::Error(const DWORD errorCode, std::wstring errorMessage, std::wstring methodName) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        "FancyZones_Error",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(methodName.c_str(), "MethodName"),
        TraceLoggingValue(errorCode, "ErrorCode"),
        TraceLoggingValue(errorMessage.c_str(), "ErrorMessage"));
}

void Trace::FancyZones::QuickLayoutSwitched(bool shortcutUsed) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventQuickLayoutSwitchKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(shortcutUsed, QuickLayoutSwitchedWithShortcutUsed));
}

void Trace::FancyZones::SnapNewWindowIntoZone(Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeLayout, layoutWindows);
    TraceLoggingWrite(
        g_hProvider,
        EventSnapNewWindowIntoZone,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeLayout), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey));
}

void Trace::FancyZones::KeyboardSnapWindowToZone(Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeLayout, layoutWindows);
    TraceLoggingWrite(
        g_hProvider,
        EventKeyboardSnapWindowToZone,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeLayout), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey));
}

static std::wstring HotKeyToString(const PowerToysSettings::HotkeyObject& hotkey)
{
    return L"alt:" + std::to_wstring(hotkey.alt_pressed())
        + L", ctrl:" + std::to_wstring(hotkey.ctrl_pressed())
        + L", shift:" + std::to_wstring(hotkey.shift_pressed())
        + L", win:" + std::to_wstring(hotkey.win_pressed())
        + L", code:" + std::to_wstring(hotkey.get_code())
        + L", keyFromCode:" + hotkey.get_key();
}

void Trace::SettingsTelemetry(const Settings& settings) noexcept
{
    auto editorHotkeyStr = HotKeyToString(settings.editorHotkey);
    auto nextTabHotkeyStr = HotKeyToString(settings.nextTabHotkey);
    auto prevTabHotkeyStr = HotKeyToString(settings.prevTabHotkey);

    TraceLoggingWrite(
        g_hProvider,
        EventSettingsKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(settings.shiftDrag, ShiftDragKey),
        TraceLoggingBoolean(settings.mouseSwitch, MouseSwitchKey),
        TraceLoggingBoolean(settings.displayOrWorkAreaChange_moveWindows, MoveWindowsOnDisplayChangeKey),
        TraceLoggingBoolean(settings.zoneSetChange_flashZones, FlashZonesOnZoneSetChangeKey),
        TraceLoggingBoolean(settings.zoneSetChange_moveWindows, MoveWindowsOnZoneSetChangeKey),
        TraceLoggingBoolean(settings.overrideSnapHotkeys, OverrideSnapHotKeysKey),
        TraceLoggingBoolean(settings.moveWindowAcrossMonitors, MoveWindowAcrossMonitorsKey),
        TraceLoggingBoolean(settings.moveWindowsBasedOnPosition, MoveWindowsBasedOnPositionKey),
        TraceLoggingBoolean(settings.appLastZone_moveWindows, MoveWindowsToLastZoneOnAppOpeningKey),
        TraceLoggingBoolean(settings.openWindowOnActiveMonitor, OpenWindowOnActiveMonitorKey),
        TraceLoggingBoolean(settings.restoreSize, RestoreSizeKey),
        TraceLoggingBoolean(settings.quickLayoutSwitch, QuickLayoutSwitchKey),
        TraceLoggingBoolean(settings.flashZonesOnQuickSwitch, FlashZonesOnQuickSwitchKey),
        TraceLoggingBoolean(settings.use_cursorpos_editor_startupscreen, UseCursorPosOnEditorStartupKey),
        TraceLoggingBoolean(settings.showZonesOnAllMonitors, ShowZonesOnAllMonitorsKey),
        TraceLoggingBoolean(settings.spanZonesAcrossMonitors, SpanZonesAcrossMonitorsKey),
        TraceLoggingBoolean(settings.makeDraggedWindowTransparent, MakeDraggedWindowTransparentKey),
        TraceLoggingBoolean(settings.allowSnapChildWindows, AllowSnapChildWindows),
        TraceLoggingBoolean(settings.disableRoundCorners, DisableRoundCornersOnSnapping),
        TraceLoggingWideString(settings.zoneColor.c_str(), ZoneColorKey),
        TraceLoggingWideString(settings.zoneBorderColor.c_str(), ZoneBorderColorKey),
        TraceLoggingWideString(settings.zoneHighlightColor.c_str(), ZoneHighlightColorKey),
        TraceLoggingInt32(settings.zoneHighlightOpacity, ZoneHighlightOpacityKey),
        TraceLoggingInt32((int)settings.overlappingZonesAlgorithm, OverlappingZonesAlgorithmKey),
        TraceLoggingWideString(editorHotkeyStr.c_str(), EditorHotkeyKey),
        TraceLoggingBoolean(settings.windowSwitching, WindowSwitchingToggleKey),
        TraceLoggingWideString(nextTabHotkeyStr.c_str(), NextTabHotkey),
        TraceLoggingWideString(prevTabHotkeyStr.c_str(), PrevTabHotkey),
        TraceLoggingInt32(static_cast<int>(settings.excludedAppsArray.size()), ExcludedAppsCountKey));
}

void Trace::VirtualDesktopChanged() noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventDesktopChangedKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE));
}

void Trace::WorkArea::KeyUp(WPARAM wParam) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventWorkAreaKeyUpKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(wParam, KeyboardValueKey));
}

void Trace::WorkArea::MoveOrResizeStarted(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeLayout, layoutWindows);
    TraceLoggingWrite(
        g_hProvider,
        EventMoveOrResizeStartedKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeLayout), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey));
}

void Trace::WorkArea::MoveOrResizeEnd(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeLayout, layoutWindows);
    TraceLoggingWrite(
        g_hProvider,
        EventMoveOrResizeEndedKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeLayout), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey));
}

void Trace::WorkArea::CycleActiveZoneSet(_In_opt_ Layout* activeLayout, const LayoutAssignedWindows& layoutWindows, InputMode mode) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeLayout, layoutWindows);
    TraceLoggingWrite(
        g_hProvider,
        EventCycleActiveZoneSetKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeLayout), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey),
        TraceLoggingValue(static_cast<int>(mode), InputModeKey));
}
