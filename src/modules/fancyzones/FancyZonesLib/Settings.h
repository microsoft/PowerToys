#pragma once

#include <common/SettingsAPI/settings_objects.h>

enum struct OverlappingZonesAlgorithm : int
{
    Smallest = 0,
    Largest = 1,
    Positional = 2,
    ClosestCenter = 3,
    EnumElements = 4, // number of elements in the enum, not counting this
};

// in reality, this file needs to be kept in sync currently with src/settings-ui/Settings.UI.Library/FZConfigProperties.cs
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
    bool moveWindowsBasedOnPosition = false;
    bool appLastZone_moveWindows = false;
    bool openWindowOnActiveMonitor = false;
    bool restoreSize = false;
    bool quickLayoutSwitch = true;
    bool flashZonesOnQuickSwitch = true;
    bool use_cursorpos_editor_startupscreen = true;
    bool showZonesOnAllMonitors = false;
    bool spanZonesAcrossMonitors = false;
    bool makeDraggedWindowTransparent = true;
    bool systemTheme = true;
    bool showZoneNumber = true;
    std::wstring zoneColor = L"#AACDFF";
    std::wstring zoneBorderColor = L"#FFFFFF";
    std::wstring zoneHighlightColor = L"#008CFF";
    std::wstring zoneNumberColor = L"#000000";
    int zoneHighlightOpacity = 50;
    OverlappingZonesAlgorithm overlappingZonesAlgorithm = OverlappingZonesAlgorithm::Smallest;
    PowerToysSettings::HotkeyObject editorHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, true, VK_OEM_3);
    bool windowSwitching = true;
    PowerToysSettings::HotkeyObject nextTabHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_NEXT);
    PowerToysSettings::HotkeyObject prevTabHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_PRIOR);
    std::wstring excludedApps = L"";
    std::vector<std::wstring> excludedAppsArray;
};

interface __declspec(uuid("{BA4E77C4-6F44-4C5D-93D3-CBDE880495C2}")) IFancyZonesSettings : public IUnknown
{
    IFACEMETHOD_(bool, GetConfig)(_Out_ PWSTR buffer, _Out_ int *buffer_size) = 0;
    IFACEMETHOD_(void, SetConfig)(PCWSTR serializedPowerToysSettings) = 0;
    IFACEMETHOD_(void, ReloadSettings)() = 0;
    IFACEMETHOD_(const Settings*, GetSettings)() const = 0;
};

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR name, PCWSTR key) noexcept;
