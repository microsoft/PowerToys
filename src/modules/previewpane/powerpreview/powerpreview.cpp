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
            // Disable all the active preview handlers.
            if (this->m_enabled && previewHandler->GetToggleSettingState())
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
    settings.set_overview_link(L"https://github.com/microsoft/PowerToys/blob/master/src/modules/previewpane/README.md");

    // Preview Pane: Settings Group Header.
    settings.add_header_szLarge(
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_HEADER_ID),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_DESC),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_TEXT));

    for (auto previewHandler : this->m_previewHandlers)
    {
        settings.add_bool_toogle(
            previewHandler->GetToggleSettingName(),
            previewHandler->GetToggleSettingDescription(),
            previewHandler->GetToggleSettingState());
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerPreviewModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::from_json_string(config);

        for (auto previewHandler : this->m_previewHandlers)
        {
            previewHandler->UpdateState(settings, this->m_enabled);
        }

        settings.save_to_settings_file();
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
        if (previewHandler->GetToggleSettingState())
        {
            // Enable all the previews with intial state set as true.
            previewHandler->EnablePreview();
        }
        else
        {
            previewHandler->DisablePreview();
        }
    }

    if (!this->m_enabled)
    {
        Trace::EnabledPowerPreview(true);
    }

    this->m_enabled = true;
}

// Disable active preview handlers.
void PowerPreviewModule::disable()
{
    for (auto previewHandler : this->m_previewHandlers)
    {
        previewHandler->DisablePreview();
    }

    if (this->m_enabled)
    {
        Trace::EnabledPowerPreview(false);
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
            previewHandler->LoadState(settings);
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}