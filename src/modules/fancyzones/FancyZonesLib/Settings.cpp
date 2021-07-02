#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include <common/utils/resources.h>

#include "FancyZonesLib/Settings.h"
#include "FancyZonesLib/FancyZones.h"
#include "trace.h"

// Non-Localizable strings
namespace NonLocalizable
{
    // FancyZones settings descriptions are localized, but underlying toggle (spinner, color picker) names are not.
    const wchar_t ShiftDragID[] = L"fancyzones_shiftDrag";
    const wchar_t MouseSwitchID[] = L"fancyzones_mouseSwitch";
    const wchar_t OverrideSnapHotKeysID[] = L"fancyzones_overrideSnapHotkeys";
    const wchar_t MoveWindowAcrossMonitorsID[] = L"fancyzones_moveWindowAcrossMonitors";
    const wchar_t MoveWindowsBasedOnPositionID[] = L"fancyzones_moveWindowsBasedOnPosition";
    const wchar_t OverlappingZonesAlgorithmID[] = L"fancyzones_overlappingZonesAlgorithm";
    const wchar_t DisplayChangeMoveWindowsID[] = L"fancyzones_displayChange_moveWindows";
    const wchar_t ZoneSetChangeMoveWindowsID[] = L"fancyzones_zoneSetChange_moveWindows";
    const wchar_t AppLastZoneMoveWindowsID[] = L"fancyzones_appLastZone_moveWindows";
    const wchar_t OpenWindowOnActiveMonitorID[] = L"fancyzones_openWindowOnActiveMonitor";
    const wchar_t RestoreSizeID[] = L"fancyzones_restoreSize";
    const wchar_t QuickLayoutSwitch[] = L"fancyzones_quickLayoutSwitch";
    const wchar_t FlashZonesOnQuickSwitch[] = L"fancyzones_flashZonesOnQuickSwitch";
    const wchar_t UseCursorPosEditorStartupScreenID[] = L"use_cursorpos_editor_startupscreen";
    const wchar_t ShowOnAllMonitorsID[] = L"fancyzones_show_on_all_monitors";
    const wchar_t SpanZonesAcrossMonitorsID[] = L"fancyzones_span_zones_across_monitors";
    const wchar_t MakeDraggedWindowTransparentID[] = L"fancyzones_makeDraggedWindowTransparent";

    const wchar_t ZoneColorID[] = L"fancyzones_zoneColor";
    const wchar_t ZoneBorderColorID[] = L"fancyzones_zoneBorderColor";
    const wchar_t ZoneHighlightColorID[] = L"fancyzones_zoneHighlightColor";
    const wchar_t EditorHotkeyID[] = L"fancyzones_editor_hotkey";
    const wchar_t ExcludedAppsID[] = L"fancyzones_excluded_apps";
    const wchar_t ZoneHighlightOpacityID[] = L"fancyzones_highlight_opacity";

    const wchar_t ToggleEditorActionID[] = L"ToggledFZEditor";
    const wchar_t IconKeyID[] = L"pt-fancy-zones";
    const wchar_t OverviewURL[] = L"https://aka.ms/PowerToysOverview_FancyZones";
    const wchar_t VideoURL[] = L"https://youtu.be/rTtGzZYAXgY";
    const wchar_t PowerToysIssuesURL[] = L"https://aka.ms/powerToysReportBug";
}

struct FancyZonesSettings : winrt::implements<FancyZonesSettings, IFancyZonesSettings>
{
public:
    FancyZonesSettings(HINSTANCE hinstance, PCWSTR name, PCWSTR key) :
        m_hinstance(hinstance),
        m_moduleName(name),
        m_moduleKey(key)
    {
        LoadSettings(name, true);
    }

    IFACEMETHODIMP_(bool)
    GetConfig(_Out_ PWSTR buffer, _Out_ int* buffer_sizeg) noexcept;
    IFACEMETHODIMP_(void)
    SetConfig(PCWSTR config) noexcept;
    IFACEMETHODIMP_(void)
    ReloadSettings() noexcept;
    IFACEMETHODIMP_(const Settings*)
    GetSettings() const noexcept { return &m_settings; }

private:
    void LoadSettings(PCWSTR config, bool fromFile) noexcept;
    void SaveSettings() noexcept;

    const HINSTANCE m_hinstance;
    std::wstring m_moduleName{};
    std::wstring m_moduleKey{};

    Settings m_settings;

    struct
    {
        PCWSTR name;
        bool* value;
        int resourceId;
    } m_configBools[16] = {
        { NonLocalizable::ShiftDragID, &m_settings.shiftDrag, IDS_SETTING_DESCRIPTION_SHIFTDRAG },
        { NonLocalizable::MouseSwitchID, &m_settings.mouseSwitch, IDS_SETTING_DESCRIPTION_MOUSESWITCH },
        { NonLocalizable::OverrideSnapHotKeysID, &m_settings.overrideSnapHotkeys, IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS },
        { NonLocalizable::MoveWindowAcrossMonitorsID, &m_settings.moveWindowAcrossMonitors, IDS_SETTING_DESCRIPTION_MOVE_WINDOW_ACROSS_MONITORS },
        { NonLocalizable::MoveWindowsBasedOnPositionID, &m_settings.moveWindowsBasedOnPosition, IDS_SETTING_DESCRIPTION_MOVE_WINDOWS_BASED_ON_POSITION },
        { NonLocalizable::DisplayChangeMoveWindowsID, &m_settings.displayChange_moveWindows, IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS },
        { NonLocalizable::ZoneSetChangeMoveWindowsID, &m_settings.zoneSetChange_moveWindows, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS },
        { NonLocalizable::AppLastZoneMoveWindowsID, &m_settings.appLastZone_moveWindows, IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS },
        { NonLocalizable::OpenWindowOnActiveMonitorID, &m_settings.openWindowOnActiveMonitor, IDS_SETTING_DESCRIPTION_OPEN_WINDOW_ON_ACTIVE_MONITOR },
        { NonLocalizable::RestoreSizeID, &m_settings.restoreSize, IDS_SETTING_DESCRIPTION_RESTORESIZE },
        { NonLocalizable::QuickLayoutSwitch, &m_settings.quickLayoutSwitch, IDS_SETTING_DESCRIPTION_QUICKLAYOUTSWITCH },
        { NonLocalizable::FlashZonesOnQuickSwitch, &m_settings.flashZonesOnQuickSwitch, IDS_SETTING_DESCRIPTION_FLASHZONESONQUICKSWITCH },
        { NonLocalizable::UseCursorPosEditorStartupScreenID, &m_settings.use_cursorpos_editor_startupscreen, IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN },
        { NonLocalizable::ShowOnAllMonitorsID, &m_settings.showZonesOnAllMonitors, IDS_SETTING_DESCRIPTION_SHOW_FANCY_ZONES_ON_ALL_MONITORS },
        { NonLocalizable::SpanZonesAcrossMonitorsID, &m_settings.spanZonesAcrossMonitors, IDS_SETTING_DESCRIPTION_SPAN_ZONES_ACROSS_MONITORS },
        { NonLocalizable::MakeDraggedWindowTransparentID, &m_settings.makeDraggedWindowTransparent, IDS_SETTING_DESCRIPTION_MAKE_DRAGGED_WINDOW_TRANSPARENT },
    };
};

IFACEMETHODIMP_(bool)
FancyZonesSettings::GetConfig(_Out_ PWSTR buffer, _Out_ int* buffer_size) noexcept
{
    PowerToysSettings::Settings settings(m_hinstance, m_moduleName);

    // Pass a string literal or a resource id to Settings::set_description().
    settings.set_description(IDS_SETTING_DESCRIPTION);
    settings.set_icon_key(NonLocalizable::IconKeyID);
    settings.set_overview_link(NonLocalizable::OverviewURL);
    settings.set_video_link(NonLocalizable::VideoURL);

    // Add a custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
    // method should be overridden as well.
    settings.add_custom_action(
        NonLocalizable::ToggleEditorActionID, // action name.
        IDS_SETTING_LAUNCH_EDITOR_LABEL,
        IDS_SETTING_LAUNCH_EDITOR_BUTTON,
        IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION);
    settings.add_hotkey(NonLocalizable::EditorHotkeyID, IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, m_settings.editorHotkey);

    for (auto const& setting : m_configBools)
    {
        settings.add_bool_toggle(setting.name, setting.resourceId, *setting.value);
    }

    settings.add_color_picker(NonLocalizable::ZoneHighlightColorID, IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, m_settings.zoneHighlightColor);
    settings.add_color_picker(NonLocalizable::ZoneColorID, IDS_SETTING_DESCRIPTION_ZONECOLOR, m_settings.zoneColor);
    settings.add_color_picker(NonLocalizable::ZoneBorderColorID, IDS_SETTING_DESCRIPTION_ZONE_BORDER_COLOR, m_settings.zoneBorderColor);

    settings.add_int_spinner(NonLocalizable::ZoneHighlightOpacityID, IDS_SETTINGS_HIGHLIGHT_OPACITY, m_settings.zoneHighlightOpacity, 0, 100, 1);

    settings.add_multiline_string(NonLocalizable::ExcludedAppsID, IDS_SETTING_EXCLUDED_APPS_DESCRIPTION, m_settings.excludedApps);

    return settings.serialize_to_buffer(buffer, buffer_size);
}

IFACEMETHODIMP_(void)
FancyZonesSettings::SetConfig(PCWSTR serializedPowerToysSettingsJson) noexcept
{
    LoadSettings(serializedPowerToysSettingsJson, false /*fromFile*/);
    SaveSettings();
}

IFACEMETHODIMP_(void)
FancyZonesSettings::ReloadSettings() noexcept
{
    LoadSettings(m_moduleKey.c_str(), true /*fromFile*/);
}

void FancyZonesSettings::LoadSettings(PCWSTR config, bool fromFile) noexcept
{
    try
    {
        PowerToysSettings::PowerToyValues values = fromFile ?
                                                       PowerToysSettings::PowerToyValues::load_from_settings_file(m_moduleKey) :
                                                       PowerToysSettings::PowerToyValues::from_json_string(config, m_moduleKey);

        for (auto const& setting : m_configBools)
        {
            if (const auto val = values.get_bool_value(setting.name))
            {
                *setting.value = *val;
            }
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneColorID))
        {
            m_settings.zoneColor = std::move(*val);
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneBorderColorID))
        {
            m_settings.zoneBorderColor = std::move(*val);
        }

        if (auto val = values.get_string_value(NonLocalizable::ZoneHighlightColorID))
        {
            m_settings.zoneHighlightColor = std::move(*val);
        }

        if (const auto val = values.get_json(NonLocalizable::EditorHotkeyID))
        {
            m_settings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
        }

        if (auto val = values.get_string_value(NonLocalizable::ExcludedAppsID))
        {
            m_settings.excludedApps = std::move(*val);
            m_settings.excludedAppsArray.clear();
            auto excludedUppercase = m_settings.excludedApps;
            CharUpperBuffW(excludedUppercase.data(), (DWORD)excludedUppercase.length());
            std::wstring_view view(excludedUppercase);
            while (view.starts_with('\n') || view.starts_with('\r'))
            {
                view.remove_prefix(1);
            }
            while (!view.empty())
            {
                auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
                m_settings.excludedAppsArray.emplace_back(view.substr(0, pos));
                view.remove_prefix(pos);
                while (view.starts_with('\n') || view.starts_with('\r'))
                {
                    view.remove_prefix(1);
                }
            }
        }

        if (auto val = values.get_int_value(NonLocalizable::ZoneHighlightOpacityID))
        {
            m_settings.zoneHighlightOpacity = *val;
        }

        if (auto val = values.get_int_value(NonLocalizable::OverlappingZonesAlgorithmID))
        {
            // Avoid undefined behavior
            if (*val >= 0 || *val < (int)OverlappingZonesAlgorithm::EnumElements)
            {
                m_settings.overlappingZonesAlgorithm = (OverlappingZonesAlgorithm)*val;
            }
        }
    }
    catch (...)
    {
        // Failure to load settings does not break FancyZones functionality. Display error message and continue with default settings.
        MessageBox(NULL,
                   GET_RESOURCE_STRING(IDS_FANCYZONES_SETTINGS_LOAD_ERROR).c_str(),
                   GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
                   MB_OK);
    }
}

void FancyZonesSettings::SaveSettings() noexcept
{
    try
    {
        PowerToysSettings::PowerToyValues values(m_moduleName, m_moduleKey);

        for (auto const& setting : m_configBools)
        {
            values.add_property(setting.name, *setting.value);
        }

        values.add_property(NonLocalizable::ZoneColorID, m_settings.zoneColor);
        values.add_property(NonLocalizable::ZoneBorderColorID, m_settings.zoneBorderColor);
        values.add_property(NonLocalizable::ZoneHighlightColorID, m_settings.zoneHighlightColor);
        values.add_property(NonLocalizable::ZoneHighlightOpacityID, m_settings.zoneHighlightOpacity);
        values.add_property(NonLocalizable::OverlappingZonesAlgorithmID, (int)m_settings.overlappingZonesAlgorithm);
        values.add_property(NonLocalizable::EditorHotkeyID, m_settings.editorHotkey.get_json());
        values.add_property(NonLocalizable::ExcludedAppsID, m_settings.excludedApps);

        values.save_to_settings_file();
    }
    catch (...)
    {
        // Failure to save settings does not break FancyZones functionality. Display error message and continue with currently cached settings.
        std::wstring errorMessage = GET_RESOURCE_STRING(IDS_FANCYZONES_SETTINGS_LOAD_ERROR) + L" " + NonLocalizable::PowerToysIssuesURL;
        MessageBox(NULL,
                   errorMessage.c_str(),
                   GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
                   MB_OK);
    }
}

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR name, PCWSTR key) noexcept
{
    return winrt::make_self<FancyZonesSettings>(hinstance, name, key);
}
