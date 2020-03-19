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
    for (auto previewHandler : this->m_previewHandlers)
    {
        if (previewHandler != NULL)
        {
            // Stop all the active previews handlers.
            if (this->m_enabled && previewHandler->GetState())
            {
                previewHandler->DisablePreview();
            }

            delete previewHandler;
        }
    }

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

    for (auto previewHandler : this->m_previewHandlers)
    {
        settings.add_bool_toogle(
            previewHandler->GetName(),
            previewHandler->GetDescription(),
            previewHandler->GetState());
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerPreviewModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues setttings = PowerToysSettings::PowerToyValues::from_json_string(config);

        for (auto previewHandler : this->m_previewHandlers)
        {
            auto toggle = setttings.get_bool_value(previewHandler->GetName());
            if (toggle)
            {
                auto lastState = previewHandler->GetState();
                if (lastState != *toggle)
                {
                    previewHandler->SetState(*toggle);

                    // If global setting is enable. Add or remove the preview handler otherwise just change the UI and save the updated config.
                    if (this->m_enabled)
                    {
                        if (lastState)
                        {
                            previewHandler->DisablePreview();
                        }
                        else
                        {
                            previewHandler->EnablePreview();
                        }
                    }

                }                
            }
        }

        setttings.save_to_settings_file();
    }
    catch (std::exception const& e)
    {
        Trace::SetConfigInvalidJSON(e.what());
    }
}

// Enable preview handlers.
void PowerPreviewModule::enable()
{
    for (auto previewHandler : this->m_previewHandlers)
    {
        if (previewHandler->GetState())
        {
            // Enable all the previews with intial state set as true.
            previewHandler->EnablePreview();
        }
    }

    this->m_enabled = true;
}

// Disable active preview handlers.
void PowerPreviewModule::disable()
{
    for (auto previewHandler : this->m_previewHandlers)
    {
        if (previewHandler->GetState())
        {
            previewHandler->DisablePreview();
        }
    }

    this->m_enabled = false;
}

// Returns if the powertoys is enabled
bool PowerPreviewModule::is_enabled()
{
    return this->m_enabled;
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
        for (auto previewHandler : this->m_previewHandlers)
        {
            auto toggle = settings.get_bool_value(previewHandler->GetName());
            if (toggle)
            {
                // If no exisiting setting found leave the default intitialization value i.e: true
                previewHandler->SetState(*toggle);
            }
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}