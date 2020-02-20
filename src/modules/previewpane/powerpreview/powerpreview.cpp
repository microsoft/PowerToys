#include "pch.h"
#include <interface/lowlevel_keyboard_event_data.h>
#include <interface/win_hook_event_data.h>
#include <settings_objects.h>
#include <common.h>
#include "powerpreview.h"
#include "trace.h"
#include "settings.h"
#include "resource.h"

// Destroy the powertoy and free memory.
void PowerPreviewModule::destroy()
{
    Trace::Destroyed();
    delete this;
}

// Return the display name of the powertoy, this will be cached.
const wchar_t* PowerPreviewModule::get_name()
{
    return m_moduleName.c_str();
}

const wchar_t** PowerPreviewModule::get_events()
{
    return nullptr;
}

// Return JSON with the configuration options.
bool PowerPreviewModule::get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size)
{
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    // Create a Settings object.
    PowerToysSettings::Settings settings(hinstance, get_name());

    // General Settings.
    settings.set_description(GET_RESOURCE_STRING(IDS_GENERAL_DESCRIPTION));
    settings.set_icon_key(GET_RESOURCE_STRING(IDS_ICON_KEY_NAME));

    // Preview Pane: Settings Group Header.
    settings.add_header_szLarge(
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_HEADER_ID),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_DESC),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_TEXT));

    for (FileExplorerPreviewSettings& previewHandler : this->m_previewHandlers)
    {
        settings.add_bool_toogle(
            previewHandler.GetName(),
            previewHandler.GetDescription(),
            previewHandler.GetState());
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerPreviewModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues values = PowerToysSettings::PowerToyValues::from_json_string(config);

        for (FileExplorerPreviewSettings& previewHandler : this->m_previewHandlers)
        {
            previewHandler.UpdateState(values);
        }

        values.save_to_settings_file();
    }
    catch (std::exception const& e)
    {
        Trace::SetConfigInvalidJSON(e.what());
    }
}

// Enable preview handlers.
void PowerPreviewModule::enable()
{
    init_settings();
    this->m_enabled = true;
}

// Disable all preview handlers.
void PowerPreviewModule::disable()
{
    this->m_enabled;
}

// Returns if the powertoys is enabled
bool PowerPreviewModule::is_enabled()
{
    for (FileExplorerPreviewSettings& previewHandler : this->m_previewHandlers)
    {
        // if : at least one preview handler is enabled.
        //      => set the General settings state for the preview handlers to true.
        // if : No preview handler is enabled.
        //      => set the General settings state for preview hanlders to false.
        if (previewHandler.GetState())
        {
            this->m_enabled = true;
            return true;
        }
    }
    this->m_enabled = false;
    return false;
}

// Handle incoming event, data is event-specific
intptr_t PowerPreviewModule::signal_event(const wchar_t* name, intptr_t data)
{
    return 0;
}

// Load the settings file.
void PowerPreviewModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_name());

        // Load settings states.
        for (FileExplorerPreviewSettings& previewHandler : this->m_previewHandlers)
        {
            previewHandler.LoadState(settings);
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}