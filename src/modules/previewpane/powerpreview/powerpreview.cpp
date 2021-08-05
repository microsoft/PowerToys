#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include "powerpreview.h"
#include "trace.h"
#include "settings.h"
#include "Generated Files/resource.h"
#include <common/notifications/dont_show_again.h>
#include <common/notifications/notifications.h>

#include <common/utils/elevation.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>

// Constructor
PowerPreviewModule::PowerPreviewModule() :
    m_moduleName(GET_RESOURCE_STRING(IDS_MODULE_NAME)),
    app_key(powerpreviewConstants::ModuleKey)
{
    // Initialize the preview modules.
    m_fileExplorerModules.emplace_back(std::make_unique<PreviewHandlerSettings>(
        true,
        L"svg-previewer-toggle-setting",
        GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
        L"{ddee2b8a-6807-48a6-bb20-2338174ff779}",
        L"Svg Preview Handler",
        std::make_unique<RegistryWrapper>()));

    m_fileExplorerModules.emplace_back(std::make_unique<PreviewHandlerSettings>(
        true,
        L"md-previewer-toggle-setting",
        GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
        L"{45769bcc-e8fd-42d0-947e-02beef77a1f5}",
        L"Markdown Preview Handler",
        std::make_unique<RegistryWrapper>()));

    m_fileExplorerModules.emplace_back(std::make_unique<PreviewHandlerSettings>(
        true,
        L"pdf-previewer-toggle-setting",
        GET_RESOURCE_STRING(IDS_PREVPANE_PDF_SETTINGS_DESCRIPTION),
        L"{07665729-6243-4746-95b7-79579308d1b2}",
        L"PDF Preview Handler",
        std::make_unique<RegistryWrapper>()));

    m_fileExplorerModules.emplace_back(std::make_unique<ThumbnailProviderSettings>(
        true,
        L"svg-thumbnail-toggle-setting",
        GET_RESOURCE_STRING(IDS_SVG_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
        L"{36B27788-A8BB-4698-A756-DF9F11F64F84}",
        L"Svg Thumbnail Provider",
        std::make_unique<RegistryWrapper>(),
        L".svg\\shellex\\{E357FCCD-A995-4576-B01F-234630154E96}"));

    // Initialize the toggle states for each module.
    init_settings();

    // File Explorer might be disabled if user updated from old to new settings.
    // Initialize the registry state in the constructor as PowerPreviewModule::enable/disable will not be called on startup
    update_registry_to_match_toggles();
}

// Destroy the powertoy and free memory.
void PowerPreviewModule::destroy()
{
    Trace::Destroyed();
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

    for (auto& fileExplorerModule : m_fileExplorerModules)
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
        PowerToysSettings::PowerToyValues settings = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());

        bool updateSuccess = true;
        bool isElevated = is_process_elevated(false);
        for (auto& fileExplorerModule : m_fileExplorerModules)
        {
            // The new settings interface does not have a toggle to modify enabled, consider File Explorer to always be enabled
            updateSuccess = updateSuccess && fileExplorerModule->UpdateState(settings, true, isElevated);
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
        for (auto& fileExplorerModule : m_fileExplorerModules)
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
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_key());

        // Load settings states.
        for (auto& fileExplorerModule : m_fileExplorerModules)
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
    for (auto& fileExplorerModule : m_fileExplorerModules)
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
    using namespace notifications;
    if (!is_toast_disabled(PreviewModulesDontShowAgainRegistryPath, PreviewModulesDisableIntervalInDays))
    {
        std::vector<action_t> actions = {
            link_button{ GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_OPEN_SETTINGS),
                         L"powertoys://open_settings/" },
            link_button{ GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_DONT_SHOW_AGAIN),
                         L"powertoys://couldnt_toggle_powerpreview_modules_disable/" }
        };
        show_toast_with_activations(GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_DESCRIPTION),
                                    GET_RESOURCE_STRING(IDS_FILEEXPLORER_ADMIN_RESTART_WARNING_TITLE),
                                    {},
                                    std::move(actions));
    }
}

// Function that checks if a registry method is required and if so checks if the process is elevated and accordingly executes the method or shows a warning
void PowerPreviewModule::registry_and_elevation_check_wrapper(std::function<void()> method)
{
    // Check if a registry update is required
    if (is_registry_update_required())
    {
        elevation_check_wrapper(method);
    }
}

// Function that checks if the process is elevated and accordingly executes the method or shows a warning
void PowerPreviewModule::elevation_check_wrapper(std::function<void()> method)
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

// Function that updates the registry state to match the toggle states
void PowerPreviewModule::update_registry_to_match_toggles()
{
    registry_and_elevation_check_wrapper([this]() {
        for (auto& fileExplorerModule : m_fileExplorerModules)
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
}
