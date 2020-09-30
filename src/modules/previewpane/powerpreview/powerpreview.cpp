#include "pch.h"
#include <settings_objects.h>
#include <common.h>
#include "powerpreview.h"
#include "trace.h"
#include "settings.h"
#include "Generated Files/resource.h"

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

// Return JSON with the configuration options.
bool PowerPreviewModule::get_config(_Out_ wchar_t* buffer, _Out_ int* buffer_size)
{
    HINSTANCE hinstance = reinterpret_cast<HINSTANCE>(&__ImageBase);

    // Create a Settings object.
    PowerToysSettings::Settings settings(hinstance, get_name());

    // General Settings.
    settings.set_description(GET_RESOURCE_STRING(IDS_GENERAL_DESCRIPTION));
    settings.set_icon_key(GET_RESOURCE_STRING(IDS_ICON_KEY_NAME));
    settings.set_overview_link(L"https://aka.ms/PowerToysOverview_FileExplorerAddOns");

    // Preview Pane: Settings Group Header.
    settings.add_header_szLarge(
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_HEADER_ID),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_DESC),
        GET_RESOURCE_STRING(IDS_PRVPANE_FILE_PREV_STTNGS_GROUP_TEXT));

    for (auto fileExplorerModule : this->m_fileExplorerModules)
    {
        settings.add_bool_toggle(
            fileExplorerModule->GetToggleSettingName(),
            fileExplorerModule->GetToggleSettingDescription(),
            fileExplorerModule->GetToggleSettingState());
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerPreviewModule::set_config(const wchar_t* config)
{
    try
    {
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::from_json_string(config);

        bool updateSuccess = true;
        for (auto fileExplorerModule : this->m_fileExplorerModules)
        {
            updateSuccess = updateSuccess && fileExplorerModule->UpdateState(settings, this->m_enabled);
        }

        if (!updateSuccess)
        {
            show_update_warning_message();
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
    elevation_check_wrapper([this]() {
        for (auto fileExplorerModule : this->m_fileExplorerModules)
        {
            if (fileExplorerModule->GetToggleSettingState())
            {
                // Enable all the modules with initial state set as true.
                fileExplorerModule->Enable();
            }
            else
            {
                fileExplorerModule->Disable();
            }
        }
    });

    if (!this->m_enabled)
    {
        Trace::EnabledPowerPreview(true);
    }

    this->m_enabled = true;
}

// Disable active preview handlers.
void PowerPreviewModule::disable()
{
    elevation_check_wrapper([this]() {
        for (auto fileExplorerModule : this->m_fileExplorerModules)
        {
            fileExplorerModule->Disable();
        }
    });

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

// Load the settings file.
void PowerPreviewModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_name());

        // Load settings states.
        for (auto fileExplorerModule : this->m_fileExplorerModules)
        {
            fileExplorerModule->LoadState(settings);
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}

// Function to check if the registry states need to be updated
bool PowerPreviewModule::is_registry_update_required()
{
    for (auto fileExplorerModule : this->m_fileExplorerModules)
    {
        if (fileExplorerModule->GetToggleSettingState() != fileExplorerModule->CheckRegistryState())
        {
            return true;
        }
    }

    return false;
}

// Function to warn the user that PowerToys needs to run as administrator for changes to take effect
void PowerPreviewModule::show_update_warning_message()
{
    // Show warning message if update is required
    MessageBoxW(NULL,
                GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_DESCRIPTION).c_str(),
                GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_TITLE).c_str(),
                MB_OK | MB_ICONWARNING);
}

// Function that checks if a registry method is required and if so checks if the process is elevated and accordingly executes the method or shows a warning
void PowerPreviewModule::elevation_check_wrapper(std::function<void()> method)
{
    // Check if a registry update is required
    if (is_registry_update_required())
    {
        // Check if the process is elevated in order to have permissions to modify HKLM registry
        if (is_process_elevated(false))
        {
            method();
        }
        // Show a warning if it doesn't have permissions
        else
        {
            show_update_warning_message();
        }
    }
}
