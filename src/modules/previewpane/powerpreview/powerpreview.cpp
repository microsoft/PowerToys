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

    for (auto thumbnailProvider : this->m_thumbnailProviders)
    {
        if (thumbnailProvider != NULL)
        {
            // Disable all the active thumbnail providers.
            if (this->m_enabled && thumbnailProvider->GetToggleSettingState())
            {
                thumbnailProvider->DisablePreview();
            }

            delete thumbnailProvider;
        }
    }

    delete this;
}

// Return the localized display name of the powertoy
const wchar_t* PowerPreviewModule::get_name()
{
    return m_moduleName.c_str();
}

// Return the non localized key of the powertoy, this will be cached by the runner
const wchar_t* PowerPreviewModule::get_key()
{
    return app_key.c_str();
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

    for (auto previewHandler : this->m_previewHandlers)
    {
        settings.add_bool_toggle(
            previewHandler->GetToggleSettingName(),
            previewHandler->GetToggleSettingDescription(),
            previewHandler->GetToggleSettingState());
    }

    for (auto thumbnailProvider : this->m_thumbnailProviders)
    {
        settings.add_bool_toggle(
            thumbnailProvider->GetToggleSettingName(),
            thumbnailProvider->GetToggleSettingDescription(),
            thumbnailProvider->GetToggleSettingState());
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

        for (auto thumbnailProvider : this->m_thumbnailProviders)
        {
            thumbnailProvider->UpdateState(settings, this->m_enabled);
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
            // Enable all the previews with initial state set as true.
            previewHandler->EnablePreview();
        }
        else
        {
            previewHandler->DisablePreview();
        }
    }

    for (auto thumbnailProvider : this->m_thumbnailProviders)
    {
        if (thumbnailProvider->GetToggleSettingState())
        {
            // Enable all the thumbnail providers with initial state set as true.
            thumbnailProvider->EnableThumbnailProvider();
        }
        else
        {
            thumbnailProvider->DisableThumbnailProvider();
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

    for (auto thumbnailProvider : this->m_thumbnailProviders)
    {
        thumbnailProvider->DisableThumbnailProvider();
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
        for (auto previewHandler : this->m_previewHandlers)
        {
            previewHandler->LoadState(settings);
        }

        for (auto thumbnailProvider : this->m_thumbnailProviders)
        {
            thumbnailProvider->LoadState(settings);
        }
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
}