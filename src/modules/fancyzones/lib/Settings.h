#pragma once

#define ZONE_STAMP L"FancyZones_zone"
#include <common/settings_objects.h>

struct Settings
{
    // The values specified here are the defaults.
    bool shiftDrag = true;
    bool displayChange_moveWindows = false;
    bool virtualDesktopChange_moveWindows = false;
    bool zoneSetChange_flashZones = false;
    bool zoneSetChange_moveWindows = false;
    bool overrideSnapHotkeys = false;
    bool appLastZone_moveWindows = false;
    bool use_cursorpos_editor_startupscreen = true;
    std::wstring zoneHightlightColor = L"#0078D7";
    int zoneHighlightOpacity = 225;
    PowerToysSettings::HotkeyObject editorHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_3);
    std::wstring excludedApps = L"";
    std::vector<std::wstring> excludedAppsArray;
};

interface __declspec(uuid("{BA4E77C4-6F44-4C5D-93D3-CBDE880495C2}")) IFancyZonesSettings : public IUnknown
{
    IFACEMETHOD_(void, SetCallback)(interface IFancyZonesCallback* callback) = 0;
    IFACEMETHOD_(bool, GetConfig)(_Out_ PWSTR buffer, _Out_ int *buffer_size) = 0;
    IFACEMETHOD_(void, SetConfig)(PCWSTR config) = 0;
    IFACEMETHOD_(void, CallCustomAction)(PCWSTR action) = 0;
    IFACEMETHOD_(Settings, GetSettings)() = 0;
};

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR config) noexcept;