#include "pch.h"
#include <common/settings_objects.h>

struct FancyZonesSettings : winrt::implements<FancyZonesSettings, IFancyZonesSettings>
{
public:
    FancyZonesSettings(HINSTANCE hinstance, PCWSTR name)
        : m_hinstance(hinstance)
        , m_name(name)
    {
        LoadSettings(name, true /*fromFile*/);
    }

    IFACEMETHODIMP_(void) SetCallback(IFancyZonesCallback* callback) { m_callback = callback; }
    IFACEMETHODIMP_(bool) GetConfig(_Out_ PWSTR buffer, _Out_ int *buffer_sizeg) noexcept;
    IFACEMETHODIMP_(void) SetConfig(PCWSTR config) noexcept;
    IFACEMETHODIMP_(void) CallCustomAction(PCWSTR action) noexcept;
    IFACEMETHODIMP_(Settings) GetSettings() noexcept { return m_settings; }

private:
    void LoadSettings(PCWSTR config, bool fromFile) noexcept;
    void SaveSettings() noexcept;

    IFancyZonesCallback* m_callback{};
    const HINSTANCE m_hinstance;
    PCWSTR m_name{};

    Settings m_settings;

    struct
    {
        PCWSTR name;
        bool* value;
        int resourceId;
    } m_configBools[11] = {
		{ L"fancyzones_enableOnInteract", &m_settings.enableOnInteract, IDS_SETTING_DESCRIPTION_ENABLEONINTERACT},
        { L"fancyzones_shiftDrag", &m_settings.shiftDrag, IDS_SETTING_DESCRIPTION_SHIFTDRAG },
		{ L"fancyzones_mouseDrag", &m_settings.mouseDrag, IDS_SETTING_DESCRIPTION_MOUSEDRAG },
        { L"fancyzones_overrideSnapHotkeys", &m_settings.overrideSnapHotkeys, IDS_SETTING_DESCRIPTION_OVERRIDE_SNAP_HOTKEYS },
        { L"fancyzones_zoneSetChange_flashZones", &m_settings.zoneSetChange_flashZones, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_FLASHZONES },
        { L"fancyzones_displayChange_moveWindows", &m_settings.displayChange_moveWindows, IDS_SETTING_DESCRIPTION_DISPLAYCHANGE_MOVEWINDOWS },
        { L"fancyzones_zoneSetChange_moveWindows", &m_settings.zoneSetChange_moveWindows, IDS_SETTING_DESCRIPTION_ZONESETCHANGE_MOVEWINDOWS },
        { L"fancyzones_virtualDesktopChange_moveWindows", &m_settings.virtualDesktopChange_moveWindows, IDS_SETTING_DESCRIPTION_VIRTUALDESKTOPCHANGE_MOVEWINDOWS },
        { L"fancyzones_appLastZone_moveWindows", &m_settings.appLastZone_moveWindows, IDS_SETTING_DESCRIPTION_APPLASTZONE_MOVEWINDOWS },
        { L"fancyzones_use_standalone_editor", &m_settings.use_standalone_editor, IDS_SETTING_DESCRIPTION_USE_STANDALONE_EDITOR },
        { L"use_cursorpos_editor_startupscreen", &m_settings.use_cursorpos_editor_startupscreen, IDS_SETTING_DESCRIPTION_USE_CURSORPOS_EDITOR_STARTUPSCREEN },
    };

    struct
    {
        PCWSTR name;
        std::wstring* value;
        int resourceId;
    } m_configStrings[1] = {
        { L"fancyzones_zoneHighlightColor", &m_settings.zoneHightlightColor, IDS_SETTING_DESCRIPTION_ZONEHIGHLIGHTCOLOR },
    };
    const std::wstring m_editor_hotkey_name = L"fancyzones_editor_hotkey";
};

IFACEMETHODIMP_(bool) FancyZonesSettings::GetConfig(_Out_ PWSTR buffer, _Out_ int *buffer_size) noexcept
{
    PowerToysSettings::Settings settings(m_hinstance, m_name);

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
    settings.add_hotkey(m_editor_hotkey_name, IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, m_settings.editorHotkey);

    for (auto const& setting : m_configBools)
    {
        settings.add_bool_toogle(setting.name, setting.resourceId, *setting.value);
    }

    for (auto const& setting : m_configStrings)
    {
        settings.add_color_picker(setting.name, setting.resourceId, *setting.value);
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

IFACEMETHODIMP_(void) FancyZonesSettings::SetConfig(PCWSTR config) noexcept try
{
    LoadSettings(config, false /*fromFile*/);
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
        PowerToysSettings::PowerToyValues::load_from_settings_file(m_name) :
        PowerToysSettings::PowerToyValues::from_json_string(config);

    for (auto const& setting : m_configBools)
    {
        if (values.is_bool_value(setting.name))
        {
            *setting.value = values.get_bool_value(setting.name);
        }
    }

    for (auto const& setting : m_configStrings)
    {
        if (values.is_string_value(setting.name))
        {
            *setting.value = values.get_string_value(setting.name);
        }
    }

    if (values.is_object_value(m_editor_hotkey_name))
    {
        m_settings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(values.get_json(m_editor_hotkey_name));
    }
}
CATCH_LOG();

void FancyZonesSettings::SaveSettings() noexcept try
{
    PowerToysSettings::PowerToyValues values(m_name);

    for (auto const& setting : m_configBools)
    {
        values.add_property(setting.name, *setting.value);
    }

    for (auto const& setting : m_configStrings)
    {
        values.add_property(setting.name, *setting.value);
    }

    values.add_property(m_editor_hotkey_name, m_settings.editorHotkey);

    values.save_to_settings_file();
}
CATCH_LOG();

winrt::com_ptr<IFancyZonesSettings> MakeFancyZonesSettings(HINSTANCE hinstance, PCWSTR name) noexcept
{
    return winrt::make_self<FancyZonesSettings>(hinstance, name);
}