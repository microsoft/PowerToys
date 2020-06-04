#pragma once

#define MULTI_ZONE_STAMP L"FancyZones_zones"
#include <common/settings_objects.h>

struct Settings
{
    // The values specified here are the defaults.
    bool shiftDrag = true;
    bool mouseSwitch = false;
    bool displayChange_moveWindows = false;
    bool zoneSetChange_flashZones = false;
    bool zoneSetChange_moveWindows = false;
    bool overrideSnapHotkeys = false;
    bool moveWindowAcrossMonitors = false;
    bool appLastZone_moveWindows = false;
    bool use_cursorpos_editor_startupscreen = true;
    bool showZonesOnAllMonitors = false;
    bool makeDraggedWindowTransparent = true;
    std::wstring zoneColor = L"#F5FCFF";
    std::wstring zoneBorderColor = L"#FFFFFF";
    std::wstring zoneHighlightColor = L"#008CFF";
    int zoneHighlightOpacity = 50;
    PowerToysSettings::HotkeyObject editorHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_3);
    std::wstring excludedApps = L"";
    std::vector<std::wstring> excludedAppsArray;
};

interface __declspec(uuid("{BA4E77C4-6F44-4C5D-93D3-CBDE880495C2}")) IFancyZonesSettings : public IUnknown
{
    IFACEMETHOD_(void, SetCallback)(interface IFancyZonesCallback* callback) = 0;
    IFACEMETHOD_(void, ResetCallback)() = 0;
    IFACEMETHOD_(bool, GetConfig)(_Out_ PWSTR buffer, _Out_ int *buffer_size) = 0;
    IFACEMETHOD_(void, SetConfig)(PCWSTR serializedPowerToysSettingsJson) = 0;
    IFACEMETHOD_(void, CallCustomAction)(PCWSTR action) = 0;
    IFACEMETHOD_(const Settings*, GetSettings)() const = 0;
};

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR config) noexcept;