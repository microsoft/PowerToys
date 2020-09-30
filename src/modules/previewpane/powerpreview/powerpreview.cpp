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

    for (auto fileExplorerAddon : this->m_fileExplorerAddons)
    {
        settings.add_bool_toggle(
            fileExplorerAddon->GetToggleSettingName(),
            fileExplorerAddon->GetToggleSettingDescription(),
            fileExplorerAddon->GetToggleSettingState());
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
        for (auto fileExplorerAddon : this->m_fileExplorerAddons)
        {
            updateSuccess = updateSuccess && fileExplorerAddon->UpdateState(settings, this->m_enabled);
        }

        if (!updateSuccess)
        {
            // Show warning message if update is required
            MessageBoxW(NULL,
                        L"Restart as admin",
                        L"Failed",
                        MB_OK | MB_ICONERROR);
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
    if (is_registry_update_required())
    {
        if (is_process_elevated(false))
        {
            for (auto fileExplorerAddon : this->m_fileExplorerAddons)
            {
                if (fileExplorerAddon->GetToggleSettingState())
                {
                    // Enable all the addons with initial state set as true.
                    fileExplorerAddon->Enable();
                }
                else
                {
                    fileExplorerAddon->Disable();
                }
            }
        }
        else
        {
            // Show warning message if update is required
            MessageBoxW(NULL,
                        L"Restart as admin",
                        L"Failed",
                        MB_OK | MB_ICONERROR);
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
    if (is_registry_update_required())
    {
        if (is_process_elevated(false))
        {
            for (auto fileExplorerAddon : this->m_fileExplorerAddons)
            {
                fileExplorerAddon->Disable();
            }
        }
        else
        {
            // Show warning message if update is required
            MessageBoxW(NULL,
                        L"Restart as admin",
                        L"Failed",
                        MB_OK | MB_ICONERROR);
        }
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

// Load the settings file.
void PowerPreviewModule::init_settings()
{
    try
    {
        // Load and parse the settings file for this PowerToy.
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_name());

        // Load settings states.
        for (auto fileExplorerAddon : this->m_fileExplorerAddons)
        {
            fileExplorerAddon->LoadState(settings);
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}

bool PowerPreviewModule::is_registry_update_required()
{
    for (auto fileExplorerAddon : this->m_fileExplorerAddons)
    {
        if (fileExplorerAddon->GetToggleSettingState() != fileExplorerAddon->CheckRegistryState())
        {
            return true;
        }
    }

    return false;
}
