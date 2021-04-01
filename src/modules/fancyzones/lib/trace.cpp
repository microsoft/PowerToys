#include "pch.h"
#include "trace.h"
#include "lib/ZoneSet.h"
#include "lib/Settings.h"
#include "lib/FancyZonesData.h"
#include "lib/FancyZonesDataTypes.h"

// Telemetry strings should not be localized.
#define LoggingProviderKey "Microsoft.PowerToys"

#define EventEnableFancyZonesKey "FancyZones_EnableFancyZones"
#define EventKeyDownKey "FancyZones_OnKeyDown"
#define EventZoneSettingsChangedKey "FancyZones_ZoneSettingsChanged"
#define EventEditorLaunchKey "FancyZones_EditorLaunch"
#define EventSettingsChangedKey "FancyZones_SettingsChanged"
#define EventDesktopChangedKey "FancyZones_VirtualDesktopChanged"
#define EventZoneWindowKeyUpKey "FancyZones_ZoneWindowKeyUp"
#define EventMoveSizeEndKey "FancyZones_MoveSizeEnd"
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
#define ZoneColorKey "ZoneColor"
#define ZoneBorderColorKey "ZoneBorderColor"
#define ZoneHighlightColorKey "ZoneHighlightColor"
#define ZoneHighlightOpacityKey "ZoneHighlightOpacity"
#define HotkeyKey "Hotkey"
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

ZoneSetInfo GetZoneSetInfo(_In_opt_ winrt::com_ptr<IZoneSet> set) noexcept
{
    ZoneSetInfo info;
    if (set)
    {
        auto zones = set->GetZones();
        info.NumberOfZones = zones.size();
        info.NumberOfWindows = 0;
        for (int i = 0; i < static_cast<int>(zones.size()); i++)
        {
            if (!set->IsZoneEmpty(i))
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
    const FancyZonesData& data = FancyZonesDataInstance();
    int appsHistorySize = static_cast<int>(data.GetAppZoneHistoryMap().size());
    const auto& customZones = data.GetCustomZoneSetsMap();
    const auto& devices = data.GetDeviceInfoMap();
    const auto& quickKeys = data.GetLayoutQuickKeys();

    std::unique_ptr<INT32[]> customZonesArray(new (std::nothrow) INT32[customZones.size()]);
    if (!customZonesArray)
    {
        return;
    }

    auto getCustomZoneCount = [&data](const std::variant<FancyZonesDataTypes::CanvasLayoutInfo, FancyZonesDataTypes::GridLayoutInfo>& layoutInfo) -> int {
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
    for (const auto& [id, device] : devices)
    {
        const FancyZonesDataTypes::ZoneSetLayoutType type = device.activeZoneSet.type;
        if (!activeZoneSetInfo.empty())
        {
            activeZoneSetInfo += L"; ";
        }
        activeZoneSetInfo += L"type: " + FancyZonesDataTypes::TypeToString(type);

        int zoneCount = -1;
        if (type == FancyZonesDataTypes::ZoneSetLayoutType::Custom)
        {
            const auto& activeCustomZone = customZones.find(device.activeZoneSet.uuid);
            if (activeCustomZone != customZones.end())
            {
                zoneCount = getCustomZoneCount(activeCustomZone->second.info);
            }
        }
        else
        {
            zoneCount = device.zoneCount;
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
        TraceLoggingInt32Array(customZonesArray.get(), static_cast<int>(customZones.size()), NumberOfZonesForEachCustomZoneSetKey),
        TraceLoggingInt32(static_cast<int>(devices.size()), ActiveZoneSetsCountKey),
        TraceLoggingWideString(activeZoneSetInfo.c_str(), ActiveZoneSetsListKey),
        TraceLoggingInt32(static_cast<int>(quickKeys.size()), LayoutUsingQuickKeyCountKey));
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

void Trace::SettingsChanged(const Settings& settings) noexcept
{
    const auto& editorHotkey = settings.editorHotkey;
    std::wstring hotkeyStr = L"alt:" + std::to_wstring(editorHotkey.alt_pressed())
        + L", ctrl:" + std::to_wstring(editorHotkey.ctrl_pressed())
        + L", shift:" + std::to_wstring(editorHotkey.shift_pressed())
        + L", win:" + std::to_wstring(editorHotkey.win_pressed())
        + L", code:" + std::to_wstring(editorHotkey.get_code())
        + L", keyFromCode:" + editorHotkey.get_key();

    TraceLoggingWrite(
        g_hProvider,
        EventSettingsChangedKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(settings.shiftDrag, ShiftDragKey),
        TraceLoggingBoolean(settings.mouseSwitch, MouseSwitchKey),
        TraceLoggingBoolean(settings.displayChange_moveWindows, MoveWindowsOnDisplayChangeKey),
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
        TraceLoggingWideString(settings.zoneColor.c_str(), ZoneColorKey),
        TraceLoggingWideString(settings.zoneBorderColor.c_str(), ZoneBorderColorKey),
        TraceLoggingWideString(settings.zoneHighlightColor.c_str(), ZoneHighlightColorKey),
        TraceLoggingInt32(settings.zoneHighlightOpacity, ZoneHighlightOpacityKey),
        TraceLoggingInt32((int)settings.overlappingZonesAlgorithm, OverlappingZonesAlgorithmKey),
        TraceLoggingWideString(hotkeyStr.c_str(), HotkeyKey),
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

void Trace::ZoneWindow::KeyUp(WPARAM wParam) noexcept
{
    TraceLoggingWrite(
        g_hProvider,
        EventZoneWindowKeyUpKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(wParam, KeyboardValueKey));
}

void Trace::ZoneWindow::MoveSizeEnd(_In_opt_ winrt::com_ptr<IZoneSet> activeSet) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeSet);
    TraceLoggingWrite(
        g_hProvider,
        EventMoveSizeEndKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeSet.get()), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey));
}

void Trace::ZoneWindow::CycleActiveZoneSet(_In_opt_ winrt::com_ptr<IZoneSet> activeSet, InputMode mode) noexcept
{
    auto const zoneInfo = GetZoneSetInfo(activeSet);
    TraceLoggingWrite(
        g_hProvider,
        EventCycleActiveZoneSetKey,
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingValue(reinterpret_cast<void*>(activeSet.get()), ActiveSetKey),
        TraceLoggingValue(zoneInfo.NumberOfZones, NumberOfZonesKey),
        TraceLoggingValue(zoneInfo.NumberOfWindows, NumberOfWindowsKey),
        TraceLoggingValue(static_cast<int>(mode), InputModeKey));
}
