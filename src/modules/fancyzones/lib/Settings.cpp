#include "pch.h"
#include <common/settings_objects.h>
#include "lib/Settings.h"
#include "lib/FancyZones.h"
#include "trace.h"

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
    IFACEMETHODIMP_(Settings) GetSettings() noexcept { return m_settings; }

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
    } m_configBools[8] = {
        { L"fancyzones_shiftDrag", &m_settings.shiftDrag, IDS_SETTING_DESCRIPTION_SHIFTDRAG },
        { L"fancyzones_overrideSnapHotkeys", &m_settings.overrideSnapHotkeys, IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS },
        { L"fancyzones_zoneSetChange_flashZones", &m_settings.zoneSetChange_flashZones, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES },
        { L"fancyzones_displayChange_moveWindows", &m_settings.displayChange_moveWindows, IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS },
        { L"fancyzones_zoneSetChange_moveWindows", &m_settings.zoneSetChange_moveWindows, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS },
        { L"fancyzones_virtualDesktopChange_moveWindows", &m_settings.virtualDesktopChange_moveWindows, IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS },
        { L"fancyzones_appLastZone_moveWindows", &m_settings.appLastZone_moveWindows, IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS },
        { L"use_cursorpos_editor_startupscreen", &m_settings.use_cursorpos_editor_startupscreen, IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN },
    };

    const std::wstring m_zoneHiglightName = L"fancyzones_zoneHighlightColor";
    const std::wstring m_editorHotkeyName = L"fancyzones_editor_hotkey";
    const std::wstring m_excludedAppsName = L"fancyzones_excluded_apps";
    const std::wstring m_zoneHighlightOpacity = L"fancyzones_highlight_opacity";
};

IFACEMETHODIMP_(bool) FancyZonesSettings::GetConfig(_Out_ PWSTR buffer, _Out_ int *buffer_size) noexcept
{
    PowerToysSettings::Settings settings(m_hinstance, m_moduleName);

    // Pass a string literal or a resource id to Settings::set_description().
    settings.set_description(IDS_SETTING_DESCRIPTION);
    settings.set_icon_key(L"pt-fancy-zones");
    settings.set_overview_link(L"https://github.com/microsoft/PowerToys/blob/master/src/modules/fancyzones/README.md");
    settings.set_video_link(L"https://youtu.be/rTtGzZYAXgY");

    // Add a custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
    // method should be overriden as well.
    settings.add_custom_action(
        L"ToggledFZEditor", // action name.
        IDS_SETTING_LAUNCH_EDITOR_LABEL,
        IDS_SETTING_LAUNCH_EDITOR_BUTTON,
        IDS_SETTING_LAUNCH_EDITOR_DESCRIPTION
    );
    settings.add_hotkey(m_editorHotkeyName, IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, m_settings.editorHotkey);

    for (auto const& setting : m_configBools)
    {
        settings.add_bool_toogle(setting.name, setting.resourceId, *setting.value);
    }

    settings.add_int_spinner(m_zoneHighlightOpacity, IDS_SETTINGS_HIGHLIGHT_OPACITY, m_settings.zoneHighlightOpacity, 0, 100, 1);
    settings.add_color_picker(m_zoneHiglightName, IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR, m_settings.zoneHightlightColor);
    settings.add_multiline_string(m_excludedAppsName, IDS_SETTING_EXCLCUDED_APPS_DESCRIPTION, m_settings.excludedApps);

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

    if (m_callback && action_object.get_name() == L"ToggledFZEditor")
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

    if (auto val = values.get_string_value(m_zoneHiglightName))
    {
        m_settings.zoneHightlightColor = std::move(*val);
    }

    if (const auto val = values.get_json(m_editorHotkeyName))
    {
        m_settings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
    }

    if (auto val = values.get_string_value(m_excludedAppsName))
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

    if (auto val = values.get_int_value(m_zoneHighlightOpacity))
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

    values.add_property(m_zoneHiglightName, m_settings.zoneHightlightColor);
    values.add_property(m_zoneHighlightOpacity, m_settings.zoneHighlightOpacity);
    values.add_property(m_editorHotkeyName, m_settings.editorHotkey.get_json());
    values.add_property(m_excludedAppsName, m_settings.excludedApps);

    values.save_to_settings_file();
}
CATCH_LOG();

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR name) noexcept
{
    return winrt::make_self<FancyZonesSettings>(hinstance, name);
}