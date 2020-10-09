#include "pch.h"
#include <common/settings_objects.h>
#include <common/common.h>
#include "Settings.h"
#include "AltDrag.h"
#include "trace.h"

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Non-Localizable strings
namespace NonLocalizable
{
    // FancyZones settings descriptions are localized, but underlying toggle (spinner, color picker) names are not.

    const wchar_t hotkeyID[] = L"altdrag_hotkeyColor";
}

struct AltDragSettings : winrt::implements<AltDragSettings, IAltDragSettings>
{
public:
    AltDragSettings(HINSTANCE hinstance, PCWSTR name) :
        m_hinstance(hinstance), m_moduleName(name)
    {
        LoadSettings(name, true);
    }

    IFACEMETHODIMP_(void)
    SetCallback(IAltDragCallback* callback) { m_callback = callback; }
    IFACEMETHODIMP_(void)
    ResetCallback() { m_callback = nullptr; }
    IFACEMETHODIMP_(bool)
    GetConfig(_Out_ PWSTR buffer, _Out_ int* buffer_sizeg) noexcept;
    IFACEMETHODIMP_(void)
    SetConfig(PCWSTR config) noexcept;
    IFACEMETHODIMP_(void)
    CallCustomAction(PCWSTR action) noexcept;
    IFACEMETHODIMP_(const Settings*)
    GetSettings() const noexcept { return &m_settings; }

private:
    void LoadSettings(PCWSTR config, bool fromFile) noexcept;
    void SaveSettings() noexcept;

    IAltDragCallback* m_callback{};
    const HINSTANCE m_hinstance;
    PCWSTR m_moduleName{};

    Settings m_settings;

   // struct
    //{
      //  PCWSTR name;
        //bool* value;
        //int resourceId;
    //} m_configBools[14 /* 15 */] = {
        // "Turning FLASHING_ZONE option off
      //  { NonLocalizable::SpanZonesAcrossMonitorsID, &m_settings.spanZonesAcrossMonitors, true },
       // { NonLocalizable::MakeDraggedWindowTransparentID, &m_settings.makeDraggedWindowTransparent, true },
    //};
};

IFACEMETHODIMP_(bool)
AltDragSettings::GetConfig(_Out_ PWSTR buffer, _Out_ int* buffer_size) noexcept
{
    PowerToysSettings::Settings settings(m_hinstance, m_moduleName);

    // Pass a string literal or a resource id to Settings::set_description().
   // settings.set_description(IDS_SETTING_DESCRIPTION);
    //settings.set_icon_key(NonLocalizable::IconKeyID);
    //settings.set_overview_link(NonLocalizable::OverviewURL);
    //settings.set_video_link(NonLocalizable::VideoURL);

    // Add a custom action property. When using this settings type, the "PowertoyModuleIface::call_custom_action()"
    // method should be overridden as well.
    //settings.add_hotkey(NonLocalizable::EditorHotkeyID, IDS_SETTING_LAUNCH_EDITOR_HOTKEY_LABEL, m_settings.editorHotkey);

   // for (auto const& setting : m_configBools)
    //{
     //   settings.add_bool_toggle(setting.name, setting.resourceId, *setting.value);
    //}

    settings.add_color_picker(NonLocalizable::hotkeyID, L"Hotkey color", m_settings.hotkey);
    //settings.add_color_picker(NonLocalizable::ZoneColorID, IDS_SETTING_DESCRIPTION_ZONECOLOR, m_settings.zoneColor);
    //settings.add_color_picker(NonLocalizable::ZoneBorderColorID, IDS_SETTING_DESCRIPTION_ZONE_BORDER_COLOR, m_settings.zoneBorderColor);

    //settings.add_int_spinner(NonLocalizable::ZoneHighlightOpacityID, IDS_SETTINGS_HIGHLIGHT_OPACITY, m_settings.zoneHighlightOpacity, 0, 100, 1);

    //settings.add_multiline_string(NonLocalizable::ExcludedAppsID, IDS_SETTING_EXCLUDED_APPS_DESCRIPTION, m_settings.excludedApps);

    return settings.serialize_to_buffer(buffer, buffer_size);
}

IFACEMETHODIMP_(void)
AltDragSettings::SetConfig(PCWSTR serializedPowerToysSettingsJson) noexcept
{
    LoadSettings(serializedPowerToysSettingsJson, false /*fromFile*/);
    SaveSettings();
    if (m_callback)
    {
        m_callback->SettingsChanged();
    }
    //Trace::SettingsChanged(m_settings);
}

IFACEMETHODIMP_(void)
AltDragSettings::CallCustomAction(PCWSTR action) noexcept
{
    try
    {
        // Parse the action values, including name.
        PowerToysSettings::CustomActionObject action_object =
            PowerToysSettings::CustomActionObject::from_json_string(action);

       // if (m_callback && action_object.get_name() == NonLocalizable::ToggleEditorActionID)
        //{
            //m_callback->ToggleEditor();
        //}
    }
    catch (...)
    {
        OutputDebugString(L"LOL!");
        // Currently only custom action comming from main PowerToys window is request to launch editor.
        // If new custom action is added, error message need to be modified.
        //std::wstring errorMessage = GET_RESOURCE_STRING(IDS_FANCYZONES_EDITOR_LAUNCH_ERROR) + L" " + NonLocalizable::PowerToysIssuesURL;
       // MessageBox(NULL,
         //          errorMessage.c_str(),
           //        GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
             //      MB_OK);
    }
}

void AltDragSettings::LoadSettings(PCWSTR config, bool fromFile) noexcept
{
    try
    {
        PowerToysSettings::PowerToyValues values = fromFile ?
                                                       PowerToysSettings::PowerToyValues::load_from_settings_file(m_moduleName) :
                                                       PowerToysSettings::PowerToyValues::from_json_string(config);

        //for (auto const& setting : m_configBools)
        //{
          //  if (const auto val = values.get_bool_value(setting.name))
           // {
             //   *setting.value = *val;
            //}
        //}

        if (auto val = values.get_string_value(NonLocalizable::hotkeyID))
        {
            m_settings.hotkey = std::move(*val);
        }


        //if (const auto val = values.get_json(NonLocalizable::EditorHotkeyID))
        //{
          //  m_settings.editorHotkey = PowerToysSettings::HotkeyObject::from_json(*val);
       // }

        //if (auto val = values.get_string_value(NonLocalizable::ExcludedAppsID))
       // {
            //m_settings.excludedApps = std::move(*val);
            //m_settings.excludedAppsArray.clear();
            //auto excludedUppercase = m_settings.excludedApps;
            //CharUpperBuffW(excludedUppercase.data(), (DWORD)excludedUppercase.length());
            //std::wstring_view view(excludedUppercase);
            //while (view.starts_with('\n') || view.starts_with('\r'))
            //{
              //  view.remove_prefix(1);
            //}
            //while (!view.empty())
           // {
                //auto pos = (std::min)(view.find_first_of(L"\r\n"), view.length());
                //m_settings.excludedAppsArray.emplace_back(view.substr(0, pos));
                //view.remove_prefix(pos);
                //while (view.starts_with('\n') || view.starts_with('\r'))
                //{
              //      view.remove_prefix(1);
            //    }
          //  }
        //}

       // if (auto val = values.get_int_value(NonLocalizable::ZoneHighlightOpacityID))
        //{
          //  m_settings.zoneHighlightOpacity = *val;
        //}
    }
    catch (...)
    {
        // Failure to load settings does not break FancyZones functionality. Display error message and continue with default settings.
        //MessageBox(NULL,
          //         GET_RESOURCE_STRING(IDS_FANCYZONES_SETTINGS_LOAD_ERROR).c_str(),
            //       GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
              //     MB_OK);
    }
}

void AltDragSettings::SaveSettings() noexcept
{
    try
    {
        PowerToysSettings::PowerToyValues values(m_moduleName);

       // for (auto const& setting : m_configBools)
        //{
         //   values.add_property(setting.name, *setting.value);
        //}

        values.add_property(NonLocalizable::hotkeyID, m_settings.hotkey);

        values.save_to_settings_file();
    }
    catch (...)
    {
        // Failure to save settings does not break FancyZones functionality. Display error message and continue with currently cached settings.
       // std::wstring errorMessage = GET_RESOURCE_STRING(IDS_FANCYZONES_SETTINGS_LOAD_ERROR) + L" " + NonLocalizable::PowerToysIssuesURL;
        //MessageBox(NULL,
          //         errorMessage.c_str(),
           //        GET_RESOURCE_STRING(IDS_POWERTOYS_FANCYZONES).c_str(),
             //      MB_OK);
    }
}

winrt::com_ptr<IAltDragSettings> MakeAltDragSettings(HINSTANCE hinstance, PCWSTR name) noexcept
{
    return winrt::make_self<AltDragSettings>(hinstance, name);
}
