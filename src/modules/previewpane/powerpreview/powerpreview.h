#include "pch.h"
#include <interface/powertoy_module_interface.h>
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <settings_objects.h>
#include <common.h>
#include "trace.h"
#include "settings.h"
#include "resource.h"

using namespace PowerPreviewSettings;

extern "C" IMAGE_DOS_HEADER __ImageBase;

// Implement the PowerToy Module Interface and all the required methods.
class PowerPreviewModule : public PowertoyModuleIface
{
private:
    // The PowerToy state.
    bool m_enabled = false;
    ExplrSVGSttngs explrSVGSettings = ExplrSVGSttngs();
    PrevPaneSVGRendrSettings prevPaneSVGSettings = PrevPaneSVGRendrSettings();
    PrevPaneMDRendrSettings prevPaneMDSettings = PrevPaneMDRendrSettings();
    std::wstring moduleName;

    // Load and save Settings.
    void init_settings();

public:
    PowerPreviewModule()
    {
        moduleName = GET_RESOURCE_STRING(IDS_MODULE_NAME);
        init_settings();
    };

    // Destroy the powertoy and free memory
    virtual void destroy() override
    {
        delete this;
        Trace::Destroyed();
    }

    // Return the display name of the powertoy, this will be cached
    virtual const wchar_t* get_name() override
    {
        return moduleName.c_str();
    }

    virtual const wchar_t** get_events() override
    {
        return nullptr;
    }

    // Return JSON with the configuration options.
    virtual bool get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size) override
    {
        HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

        // Create a Settings object.
        PowerToysSettings::Settings settings(hinstance, get_name());

        // General Settings.
        settings.set_description(GET_RESOURCE_STRING(IDS_GENERAL_DESCRIPTION));
        settings.set_icon_key(GET_RESOURCE_STRING(IDS_ICON_KEY_NAME));

        // Explorer: Settings Group Header.
        settings.add_header_szLarge(
            GET_RESOURCE_STRING(IDS_EXPLR_ICONS_PREV_STTNGS_GROUP_HEADER_ID),
            GET_RESOURCE_STRING(IDS_EXPLR_ICONS_PREV_STTNGS_GROUP_DESC),
            GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_TEXT));

        // Explorer: SVG Icon Settings.
        settings.add_bool_toogle(
            explrSVGSettings.GetName(),
            explrSVGSettings.GetDescription(),
            explrSVGSettings.GetState());

        // Preview Pane: Settings Group Header.
        settings.add_header_szLarge(
            GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_HEADER_ID),
            GET_RESOURCE_STRING(IDS_EXPLR_ICONS_PREV_STTNGS_GROUP_DESC),
            GET_RESOURCE_STRING(IDS_EXPLR_ICONS_PREV_STTNGS_GROUP_TEXT));

        // Preview Pane: SVG Settings.
        settings.add_bool_toogle(
            prevPaneSVGSettings.GetName(),
            prevPaneSVGSettings.GetDescription(),
            prevPaneSVGSettings.GetState());

        // Preview Pane: Mark Down Settings.
        settings.add_bool_toogle(
            prevPaneMDSettings.GetName(),
            prevPaneMDSettings.GetDescription(),
            prevPaneMDSettings.GetState());

        return settings.serialize_to_buffer(buffer, buffer_size);
    }

    // Called by the runner to pass the updated settings values as a serialized JSON.
    virtual void set_config(const wchar_t* config) override
    {
        try
        {
            PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config);
            explrSVGSettings.UpdateState(values);
            prevPaneSVGSettings.UpdateState(values);
            prevPaneMDSettings.UpdateState(values);
            values.save_to_settings_file();
        }
        catch (std::exception const& e)
        {
            Trace::SetConfigInvalidJSON(e.what());
        }
    }

    // Enable the powertoy
    virtual void enable()
    {
        m_enabled = true;
        Trace::FilePreviewerIsEnabled();
    }

    // Disable the powertoy
    virtual void disable()
    {
        m_enabled = false;
        Trace::FilePreviewerIsDisabled();
    }

    // Returns if the powertoys is enabled
    virtual bool is_enabled() override
    {
        return m_enabled;
    }

    // Handle incoming event, data is event-specific
    virtual intptr_t signal_event(const wchar_t* name, intptr_t data) override
    {
        return 0;
    }

    virtual void register_system_menu_helper(PowertoySystemMenuIface* helper) override {}
    virtual void signal_system_menu_action(const wchar_t* name) override {}
};