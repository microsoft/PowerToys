#include "pch.h"
#include "trace.h"
#include "lib/ZoneSet.h"
#include "lib/Settings.h"
#include "lib/JsonHelpers.h"

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

void Trace::FancyZones::DataChanged() noexcept
{
    const JSONHelpers::FancyZonesData& data = JSONHelpers::FancyZonesDataInstance();
    int appsHistorySize = static_cast<int>(data.GetAppZoneHistoryMap().size());
    const auto& customZones = data.GetCustomZoneSetsMap();
    const auto& devices = data.GetDeviceInfoMap();

    std::unique_ptr<INT32[]> customZonesArray(new (std::nothrow) INT32[customZones.size()]);
    if (!customZonesArray)
    {
        return;
    }

    auto getCustomZoneCount = [&data](const std::variant<JSONHelpers::CanvasLayoutInfo, JSONHelpers::GridLayoutInfo>& layoutInfo) -> int {
        if (std::holds_alternative<JSONHelpers::GridLayoutInfo>(layoutInfo))
        {
            const auto& info = std::get<JSONHelpers::GridLayoutInfo>(layoutInfo);
            return (info.rows() * info.columns());
        }
        else if (std::holds_alternative<JSONHelpers::CanvasLayoutInfo>(layoutInfo))
        {
            const auto& info = std::get<JSONHelpers::CanvasLayoutInfo>(layoutInfo);
            return static_cast<int>(info.zones.size());
        }
        return 0;
    };

    //NumberOfZonesForEachCustomZoneSet
    int i = 0;
    for (const auto& [id, customZoneSetData] : customZones)
    {
        customZonesArray.get()[i] = getCustomZoneCount(customZoneSetData.info);
        i++;
    }

    //ActiveZoneSetsList
    std::wstring activeZoneSetInfo;
    for (const auto& [id, device] : devices)
    {
        const JSONHelpers::ZoneSetLayoutType type = device.activeZoneSet.type;
        if (!activeZoneSetInfo.empty())
        {
            activeZoneSetInfo += L"; ";
        }
        activeZoneSetInfo += L"type: " + JSONHelpers::TypeToString(type);

        int zoneCount = -1;
        if (type == JSONHelpers::ZoneSetLayoutType::Custom)
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
        "FancyZones_ZoneSettingsChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingInt32(appsHistorySize, "AppsInHistoryCount"),
        TraceLoggingInt32(static_cast<int>(customZones.size()), "CustomZoneSetCount"),
        TraceLoggingInt32Array(customZonesArray.get(), static_cast<int>(customZones.size()), "NumberOfZonesForEachCustomZoneSet"),
        TraceLoggingInt32(static_cast<int>(devices.size()), "ActiveZoneSetsCount"),
        TraceLoggingWideString(activeZoneSetInfo.c_str(), "ActiveZoneSetsList"));
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
        "FancyZones_SettingsChanged",
        ProjectTelemetryPrivacyDataTag(ProjectTelemetryTag_ProductAndServicePerformance),
        TraceLoggingKeyword(PROJECT_KEYWORD_MEASURE),
        TraceLoggingBoolean(settings.shiftDrag, "ShiftDrag"),
        TraceLoggingBoolean(settings.displayChange_moveWindows, "MoveWindowsOnDisplayChange"),
        TraceLoggingBoolean(settings.virtualDesktopChange_moveWindows, "MoveWindowsOnVirtualDesktopChange"),
        TraceLoggingBoolean(settings.zoneSetChange_flashZones, "FlashZonesOnZoneSetChange"),
        TraceLoggingBoolean(settings.zoneSetChange_moveWindows, "MoveWindowsOnZoneSetChange"),
        TraceLoggingBoolean(settings.overrideSnapHotkeys, "OverrideSnapHotKeys"),
        TraceLoggingBoolean(settings.appLastZone_moveWindows, "MoveWindowsToLastZoneOnAppOpening"),
        TraceLoggingBoolean(settings.use_cursorpos_editor_startupscreen, "UseCursorPosOnEditorStartup"),
        TraceLoggingWideString(settings.zoneHightlightColor.c_str(), "ZoneHighlightColor"),
        TraceLoggingInt32(settings.zoneHighlightOpacity, "ZoneHighlightOpacity"),
        TraceLoggingWideString(hotkeyStr.c_str(), "Hotkey"),
        TraceLoggingInt32(static_cast<int>(settings.excludedAppsArray.size()), "ExcludedAppsCount"));
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
