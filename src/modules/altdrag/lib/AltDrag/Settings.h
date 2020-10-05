#pragma once

#include <common/settings_objects.h>

#include "pch.h"

// Zoned window properties are not localized.
//namespace ZonedWindowProperties
//{
//    const wchar_t PropertyMultipleZoneID[] = L"FancyZones_zones";
//    const wchar_t PropertyRestoreSizeID[] = L"FancyZones_RestoreSize";
//    const wchar_t PropertyRestoreOriginID[] = L"FancyZones_RestoreOrigin";
//
//    const wchar_t MultiMonitorDeviceID[] = L"FancyZones#MultiMonitorDevice";
//}

struct Settings
{
    // The values specified here are the defaults
    bool spanZonesAcrossMonitors = false;
    bool makeDraggedWindowTransparent = true;
    std::wstring zoneColor = L"#F5FCFF";
    std::wstring zoneBorderColor = L"#FFFFFF";
    std::wstring zoneHighlightColor = L"#008CFF";
    int zoneHighlightOpacity = 50;
    PowerToysSettings::HotkeyObject editorHotkey = PowerToysSettings::HotkeyObject::from_settings(true, false, false, false, VK_OEM_3);
    std::wstring excludedApps = L"";
    std::vector<std::wstring> excludedAppsArray;
};

interface IAltDragSettings : public IUnknown
{
    IFACEMETHOD_(void, SetCallback)
    (interface IAltDragCallback * callback) = 0;
    IFACEMETHOD_(void, ResetCallback)
    () = 0;
    IFACEMETHOD_(bool, GetConfig)
    (_Out_ PWSTR buffer, _Out_ int* buffer_size) = 0;
    IFACEMETHOD_(void, SetConfig)
    (PCWSTR serializedPowerToysSettingsJson) = 0;
    IFACEMETHOD_(void, CallCustomAction)
    (PCWSTR action) = 0;
    IFACEMETHOD_(const Settings*, GetSettings)
    () const = 0;
};

winrt::com_ptr<IAltDragSettings> MakeAltDragSettings(HINSTANCE hinstance, PCWSTR config) noexcept;