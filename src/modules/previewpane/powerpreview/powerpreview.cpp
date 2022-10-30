#include "pch.h"
#include <common/SettingsAPI/settings_objects.h>
#include "powerpreview.h"
#include "trace.h"
#include "Generated Files/resource.h"
#include <common/notifications/dont_show_again.h>
#include <common/notifications/notifications.h>

#include <common/utils/elevation.h>
#include <common/utils/resources.h>
#include <common/utils/os-detect.h>
#include <common/utils/process_path.h>

#include <SettingsAPI/settings_helpers.h>

// Constructor
PowerPreviewModule::PowerPreviewModule() :
    m_moduleName(GET_RESOURCE_STRING(IDS_MODULE_NAME)),
    app_key(powerpreviewConstants::ModuleKey)
{
    const std::wstring installationDir = get_module_folderpath();

    std::filesystem::path logFilePath(PTSettingsHelper::get_module_save_folder_location(this->app_key));
    logFilePath.append(LogSettings::fileExplorerLogPath);
    Logger::init(LogSettings::fileExplorerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    Logger::info("Initializing PowerPreviewModule");
    const bool installPerUser = true;
    m_fileExplorerModules.push_back({ .settingName = L"svg-previewer-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PREVPANE_SVG_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredSvgPreviewEnabledValue,
                                      .registryChanges = getSvgPreviewHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"md-previewer-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PREVPANE_MD_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredMarkdownPreviewEnabledValue,
                                      .registryChanges = getMdPreviewHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"monaco-previewer-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PREVPANE_MONACO_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredMonacoPreviewEnabledValue,
                                      .registryChanges = getMonacoPreviewHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"pdf-previewer-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PREVPANE_PDF_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredPdfPreviewEnabledValue,
                                      .registryChanges = getPdfPreviewHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"gcode-previewer-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PREVPANE_GCODE_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredGcodePreviewEnabledValue,
                                      .registryChanges = getGcodePreviewHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"svg-thumbnail-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_SVG_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredSvgThumbnailsEnabledValue,
                                      .registryChanges = getSvgThumbnailHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"pdf-thumbnail-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_PDF_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredPdfThumbnailsEnabledValue,
                                      .registryChanges = getPdfThumbnailHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"gcode-thumbnail-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_GCODE_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredGcodeThumbnailsEnabledValue,
                                      .registryChanges = getGcodeThumbnailHandlerChangeSet(installationDir, installPerUser) });

    m_fileExplorerModules.push_back({ .settingName = L"stl-thumbnail-toggle-setting",
                                      .settingDescription = GET_RESOURCE_STRING(IDS_STL_THUMBNAIL_PROVIDER_SETTINGS_DESCRIPTION),
                                      .checkModuleGPOEnabledRuleFunction = powertoys_gpo::getConfiguredStlThumbnailsEnabledValue,
                                      .registryChanges = getStlThumbnailHandlerChangeSet(installationDir, installPerUser) });

    try
    {
        PowerToysSettings::PowerToyValues settings =
            PowerToysSettings::PowerToyValues::load_from_settings_file(PowerPreviewModule::get_key());

        apply_settings(settings);
    }
    catch (std::exception const& e)
    {
        Trace::InitSetErrorLoadingFile(e.what());
    }
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
            fileExplorerModule.settingName,
            fileExplorerModule.settingDescription,
            fileExplorerModule.registryChanges.isApplied());
    }

    return settings.serialize_to_buffer(buffer, buffer_size);
}

// Called by the runner to pass the updated settings values as a serialized JSON.
void PowerPreviewModule::set_config(const wchar_t* config)
{
    try
    {
        auto settings = PowerToysSettings::PowerToyValues::from_json_string(config, get_key());
        apply_settings(settings);
        settings.save_to_settings_file();
    }
    catch (std::exception const& e)
    {
        Trace::SetConfigInvalidJSON(e.what());
    }
}

void PowerPreviewModule::enable()
{
    if (!m_enabled)
    {
        Trace::EnabledPowerPreview(true);
    }

    m_enabled = true;
}

// Disable active preview handlers.
void PowerPreviewModule::disable()
{
    for (auto& fileExplorerModule : m_fileExplorerModules)
    {
        if (!fileExplorerModule.registryChanges.unApply())
        {
            Logger::error(L"Couldn't disable file explorer module {} during module disable() call", fileExplorerModule.settingName);
        }
    }

    if (m_enabled)
    {
        Trace::EnabledPowerPreview(false);
    }

    m_enabled = false;
}

// Returns if the powertoys is enabled
bool PowerPreviewModule::is_enabled()
{
    return m_enabled;
}

// Function to warn the user that PowerToys needs to run as administrator for changes to take effect
void PowerPreviewModule::show_update_warning_message()
{
    using namespace notifications;
    if (is_toast_disabled(PreviewModulesDontShowAgainRegistryPath, PreviewModulesDisableIntervalInDays))
    {
        return;
    }

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

void PowerPreviewModule::apply_settings(const PowerToysSettings::PowerToyValues& settings)
{
    bool notifyShell = false;

    for (auto& fileExplorerModule : m_fileExplorerModules)
    {
        const auto toggle = settings.get_bool_value(fileExplorerModule.settingName);
        const auto gpo_rule = fileExplorerModule.checkModuleGPOEnabledRuleFunction();
        const auto gpo_is_configured = gpo_rule == powertoys_gpo::gpo_rule_configured_enabled || gpo_rule == powertoys_gpo::gpo_rule_configured_disabled;

        if (gpo_rule == powertoys_gpo::gpo_rule_configured_unavailable)
        {
            Logger::warn(L"Couldn't read the gpo rule for Power Preview module {}", fileExplorerModule.settingName);
        }
        if (gpo_rule == powertoys_gpo::gpo_rule_configured_wrong_value)
        {
            Logger::warn(L"gpo rule for Power Preview module {} is set to an unknown value", fileExplorerModule.settingName);
        }

        // Skip if no need to update
        if (!toggle.has_value() && !gpo_is_configured)
        {
            continue;
        }

        bool module_new_state = false;
        if (toggle.has_value())
        {
            module_new_state = *toggle;
        }
        if (gpo_is_configured)
        {
            // gpo rule overrides settings state
            module_new_state = gpo_rule == powertoys_gpo::gpo_rule_configured_enabled;
        }

        // Skip if no need to update
        if (module_new_state == fileExplorerModule.registryChanges.isApplied())
        {
            continue;
        }

        // (Un)Apply registry changes depending on the new setting value
        const bool updated = module_new_state ? fileExplorerModule.registryChanges.apply() : fileExplorerModule.registryChanges.unApply();

        if (updated)
        {
            notifyShell = true;
            Trace::PowerPreviewSettingsUpdated(fileExplorerModule.settingName.c_str(), !*toggle, *toggle, true);
        }
        else
        {
            Logger::error(L"Couldn't {} file explorer module {} during apply_settings", *toggle ? L"enable " : L"disable", fileExplorerModule.settingName);
            Trace::PowerPreviewSettingsUpdateFailed(fileExplorerModule.settingName.c_str(), !*toggle, *toggle, true);
        }
    }
    if (notifyShell)
    {
        SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, NULL, NULL);
    }
}
