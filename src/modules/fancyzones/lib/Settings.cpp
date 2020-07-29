#include "pch.h"
#include <common/settings_objects.h>
#include "lib/Settings.h"
#include "lib/FancyZones.h"
#include "trace.h"

// Non-Localizable strings
namespace NonLocalizable
{
    // FancyZones settings descriptions are localized, but underlying toggle (spinner, color picker) names are not.
    const wchar_t ShiftDrag[] = L"fancyzones_shiftDrag";
    const wchar_t MouseSwitch[] = L"fancyzones_mouseSwitch";
    const wchar_t OverrideSnapHotKeys[] = L"fancyzones_overrideSnapHotkeys";
    const wchar_t MoveWindowAcrossMonitors[] = L"fancyzones_moveWindowAcrossMonitors";
    const wchar_t DisplayChangeMoveWindows[] = L"fancyzones_displayChange_moveWindows";
    const wchar_t ZoneSetChangeMoveWindows[] = L"fancyzones_zoneSetChange_moveWindows";
    const wchar_t AppLastZoneMoveWindows[] = L"fancyzones_appLastZone_moveWindows";
    const wchar_t OpenWindowOnActiveMonitor[] = L"fancyzones_openWindowOnActiveMonitor";
    const wchar_t RestoreSize[] = L"fancyzones_restoreSize";
    const wchar_t UserCursorPosEditorStartupScreen[] = L"use_cursorpos_editor_startupscreen";
    const wchar_t ShowOnAllMonitors[] = L"fancyzones_show_on_all_monitors";
    const wchar_t SpanZonesAcrossMonitors[] = L"fancyzones_span_zones_across_monitors";
    const wchar_t MakeDraggedWindowTransparent[] = L"fancyzones_makeDraggedWindowTransparent";

    const wchar_t ZoneColor[] = L"fancyzones_zoneColor";
    const wchar_t ZoneBorderColor[] = L"fancyzones_zoneBorderColor";
    const wchar_t ZoneHighlightColor[] = L"fancyzones_zoneHighlightColor";
    const wchar_t EditorHotkey[] = L"fancyzones_editor_hotkey";
    const wchar_t ExcludedApps[] = L"fancyzones_excluded_apps";
    const wchar_t ZoneHighlightOpacity[] = L"fancyzones_highlight_opacity";

    const wchar_t ToggleEditorAction[] = L"ToggledFZEditor";
    const wchar_t IconKey[] = L"pt-fancy-zones";
    const wchar_t OverviewLink[] = L"https://aka.ms/PowerToysOverview_FancyZones";
    const wchar_t VideoLink[] = L"https://youtu.be/rTtGzZYAXgY";
}

struct FancyZonesSettings : winrt::implements<FancyZonesSettings, IFancyZonesSettings>
{
public:
    FancyZonesSettings(HINSTANCE hinstance, PCWSTR name)
        : m_hinstance(hinstance)
        , m_moduleName(name)
    {
        LoadSettings(name, true);
    }
    
    IFACEMETHODIMP_(void) SetCallback(IFancyZonesCallback* callback) { m_callback = callback; }
    IFACEMETHODIMP_(void) ResetCallback() { m_callback = nullptr; }
    IFACEMETHODIMP_(bool) GetConfig(_Out_ PWSTR buffer, _Out_ int *buffer_sizeg) noexcept;
    IFACEMETHODIMP_(void) SetConfig(PCWSTR config) noexcept;
    IFACEMETHODIMP_(void) CallCustomAction(PCWSTR action) noexcept;
    IFACEMETHODIMP_(const Settings*) GetSettings() const noexcept { return &m_settings; }

private:
    void LoadSettings(PCWSTR config, bool fromFile) noexcept;
    void SaveSettings() noexcept;

    IFancyZonesCallback* m_callback{};
    const HINSTANCE m_hinstance;
    PCWSTR m_moduleName{};

    Settings m_settings;

    struct
    {
        PCWSTR name;
        bool* value;
        int resourceId;
    } m_configBools[13 /* 14 */] = { // "Turning FLASHING_ZONE option off"
        { NonLocalizable::ShiftDrag, &m_settings.shiftDrag, IDS_SETTING_DESCRIPTION_SHIFTDRAG },
        { NonLocalizable::MouseSwitch, &m_settings.mouseSwitch, IDS_SETTING_DESCRIPTION_MOUSESWITCH },
        { NonLocalizable::OverrideSnapHotKeys, &m_settings.overrideSnapHotkeys, IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS },
        { NonLocalizable::MoveWindowAcrossMonitors, &m_settings.moveWindowAcrossMonitors, IDS_SETTING_DESCRIPTION_MOVE_WINDOW_ACROSS_MONITORS },
        // "Turning FLASHING_ZONE option off"
        //{ L"fancyzones_zoneSetChange_flashZones", &m_settings.zoneSetChange_flashZones, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES },
        { NonLocalizable::DisplayChangeMoveWindows, &m_settings.displayChange_moveWindows, IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS },
        { NonLocalizable::ZoneSetChangeMoveWindows, &m_settings.zoneSetChange_moveWindows, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS },
        { NonLocalizable::AppLastZoneMoveWindows, &m_settings.appLastZone_moveWindows, IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS },
        { NonLocalizable::OpenWindowOnActiveMonitor, &m_settings.openWindowOnActiveMonitor, IDS_SETTING_DESCRIPTION_OPEN_WINDOW_ON_ACTIVE_MONITOR },
        { NonLocalizable::RestoreSize, &m_settings.restoreSize, IDS_SETTING_DESCRIPTION_RESTORESIZE },
        { NonLocalizable::UserCursorPosEditorStartupScreen, &m_settings.use_cursorpos_editor_startupscreen, IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN },
        { NonLocalizable::ShowOnAllMonitors, &m_settings.showZonesOnAllMonitors, IDS_SETTING_DESCRIPTION_SHOW_FANCY_ZONES_ON_ALL_MONITORS},
        { NonLocalizable::SpanZonesAcrossMonitors, &m_settings.spanZonesAcrossMonitors, IDS_SETTING_DESCRIPTION_SPAN_ZONES_ACROSS_MONITORS },        
        { NonLocalizable::MakeDraggedWindowTransparent, &m_settings.makeDraggedWindowTransparent, IDS_SETTING_DESCRIPTION_MAKE_DRAGGED_WINDOW_TRANSPARENT},
    };

};

IFACEMETHODIMP_(bool) FancyZonesSettings::GetConfig(_Out_ PWSTR buffer, _Out_ int *buffer_size) noexcept
{
    PowerToysSettings::Settings settings(m_hinstance, m_moduleName);

    // Pass a string literal or a resource id to Settings::set_description().
    settings.set_description(IDS_SETTING_DESCRIPTION);
    settings.set_icon_key(NonLocalizable::IconKey);
    settings.set_overview_link(NonLocalizable::OverviewLink);
    settings.set_video_link(NonLocalizable::VideoLink);

    // Add a custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
    // method should be overridden as well.
    settings.add_custom_action(
        NonLocalizable::ToggleEditorAction, // action name.
        IDS_SETTING_LAUNCH_EDITOR_LABEL,
        IDS_SETTING_LAUNCH_EDITOR_BUTTON,
        IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION
    );
    settings.add_hotkey(NonLocalizable::EditorHotkey, IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, m_settings.editorHotkey);

    for (auto const& setting : m_configBools)
    {
        settings.add_bool_toggle(setting.name, setting.resourceId, *setting.value);
    }

    settings.add_color_picker(NonLocalizable::ZoneHighlightColor, IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, m_settings.zoneHighlightColor);
    settings.add_color_picker(NonLocalizable::ZoneColor, IDS_SETTING_DESCRIPTION_ZONECOLOR, m_settings.zoneColor);
    settings.add_color_picker(NonLocalizable::ZoneBorderColor, IDS_SETTING_DESCRIPTION_ZONE_BORDER_COLOR, m_settings.zoneBorderColor);
    
    settings.add_int_spinner(NonLocalizable::ZoneHighlightOpacity, IDS_SETTINGS_HIGHLIGHT_OPACITY, m_settings.zoneHighlightOpacity, 0, 100, 1);
    
    settings.add_multiline_string(NonLocalizable::ExcludedApps, IDS_SETTING_EXCLUDED_APPS_DESCRIPTION, m_settings.excludedApps);

    return settings.serialize_to_buffer(buffer, buffer_size);
}

IFACEMETHODIMP_(void) FancyZonesSettings::SetConfig(PCWSTR serializedPowerToysSettingsJson) noexcept try
{
    LoadSettings(serializedPowerToysSettingsJson, false /*fromFile*/);
    SaveSettings();
    if (m_callback)
    {
        m_callback->SettingsChanged();
    }
    Trace::SettingsChanged(m_settings);
}
CATCH_LOG();

IFACEMETHODIMP_(void) FancyZonesSettings::CallCustomAction(PCWSTR action) noexcept try
{
    // Parse the action values, including name.
    PowerToysSettings::CustomActionObject action_object =
        PowerToysSettings::CustomActionObject::from_json_string(action);

    if (m_callback && action_object.get_name() == NonLocalizable::ToggleEditorAction)
    {
        m_callback->ToggleEditor();
    }
}
CATCH_LOG();

void FancyZonesSettings::LoadSettings(PCWSTR config, bool fromFile) noexcept try
{
    PowerToysSettings::PowerToyValues values = fromFile ?
        PowerToysSettings::PowerToyValues::load_from_settings_file(m_moduleName) :
        PowerToysSettings::PowerToyValues::from_json_string(config);

    for (auto const& setting : m_configBools)
    {
        if (const auto val = values.get_bool_value(setting.name))
        {
            *setting.value = *val;
        }
    }

    if (auto val = values.get_string_value(NonLocalizable::ZoneColor))
    {
        m_settings.zoneColor = std::move(*val);
    }

    if (auto val = values.get_string_value(NonLocalizable::ZoneBorderColor))
    {
        m_settings.zoneBorderColor = std::move(*val);
    }

    if (auto val = values.get_string_value(NonLocalizable::ZoneHighlightColor))
    {
        m_settings.zoneHighlightColor = std::move(*val);
    }

    if (const auto val = values.get_json(NonLocalizable::EditorHotkey))
    {
        m_settings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
    }

    if (auto val = values.get_string_value(NonLocalizable::ExcludedApps))
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

    if (auto val = values.get_int_value(NonLocalizable::ZoneHighlightOpacity))
    {
        m_settings.zoneHighlightOpacity = *val;
    }
}
CATCH_LOG();

void FancyZonesSettings::SaveSettings() noexcept try
{
    PowerToysSettings::PowerToyValues values(m_moduleName);

    for (auto const& setting : m_configBools)
    {
        values.add_property(setting.name, *setting.value);
    }

    values.add_property(NonLocalizable::ZoneColor, m_settings.zoneColor);
    values.add_property(NonLocalizable::ZoneBorderColor, m_settings.zoneBorderColor);
    values.add_property(NonLocalizable::ZoneHighlightColor, m_settings.zoneHighlightColor);
    values.add_property(NonLocalizable::ZoneHighlightOpacity, m_settings.zoneHighlightOpacity);
    values.add_property(NonLocalizable::EditorHotkey, m_settings.editorHotkey.get_json());
    values.add_property(NonLocalizable::ExcludedApps, m_settings.excludedApps);

    values.save_to_settings_file();
}
CATCH_LOG();

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR name) noexcept
{
    return winrt::make_self<FancyZonesSettings>(hinstance, name);
}