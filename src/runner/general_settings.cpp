#include "pch.h"
#include "general_settings.h"
#include "auto_start_helper.h"
#include "Generated files/resource.h"

#include <common/SettingsAPI/settings_helpers.h>
#include "powertoy_module.h"
#include <common/themes/windows_colors.h>

#include "trace.h"
#include <common/utils/elevation.h>
#include <common/version/version.h>
#include <common/utils/resources.h>

// TODO: would be nice to get rid of these globals, since they're basically cached json settings
static std::wstring settings_theme = L"system";
static bool run_as_elevated = false;
static bool show_new_updates_toast_notification = true;
static bool download_updates_automatically = true;
static bool show_whats_new_after_updates = true;
static bool enable_experimentation = true;
static bool enable_warnings_elevated_apps = true;

json::JsonObject GeneralSettings::to_json()
{
    json::JsonObject result;

    result.SetNamedValue(L"startup", json::value(isStartupEnabled));
    if (!startupDisabledReason.empty())
    {
        result.SetNamedValue(L"startup_disabled_reason", json::value(startupDisabledReason));
    }

    json::JsonObject enabled;
    for (const auto& [name, isEnabled] : isModulesEnabledMap)
    {
        enabled.SetNamedValue(name, json::value(isEnabled));
    }
    result.SetNamedValue(L"enabled", std::move(enabled));

    result.SetNamedValue(L"is_elevated", json::value(isElevated));
    result.SetNamedValue(L"run_elevated", json::value(isRunElevated));
    result.SetNamedValue(L"show_new_updates_toast_notification", json::value(showNewUpdatesToastNotification));
    result.SetNamedValue(L"download_updates_automatically", json::value(downloadUpdatesAutomatically));
    result.SetNamedValue(L"show_whats_new_after_updates", json::value(showWhatsNewAfterUpdates));
    result.SetNamedValue(L"enable_experimentation", json::value(enableExperimentation));
    result.SetNamedValue(L"is_admin", json::value(isAdmin));
    result.SetNamedValue(L"enable_warnings_elevated_apps", json::value(enableWarningsElevatedApps));
    result.SetNamedValue(L"theme", json::value(theme));
    result.SetNamedValue(L"system_theme", json::value(systemTheme));
    result.SetNamedValue(L"powertoys_version", json::value(powerToysVersion));

    return result;
}

json::JsonObject load_general_settings()
{
    auto loaded = PTSettingsHelper::load_general_settings();
    settings_theme = loaded.GetNamedString(L"theme", L"system");
    if (settings_theme != L"dark" && settings_theme != L"light")
    {
        settings_theme = L"system";
    }
    run_as_elevated = loaded.GetNamedBoolean(L"run_elevated", false);
    show_new_updates_toast_notification = loaded.GetNamedBoolean(L"show_new_updates_toast_notification", true);
    download_updates_automatically = loaded.GetNamedBoolean(L"download_updates_automatically", true) && check_user_is_admin();
    show_whats_new_after_updates = loaded.GetNamedBoolean(L"show_whats_new_after_updates", true);
    enable_experimentation = loaded.GetNamedBoolean(L"enable_experimentation", true);
    enable_warnings_elevated_apps = loaded.GetNamedBoolean(L"enable_warnings_elevated_apps", true);

    return loaded;
}

GeneralSettings get_general_settings()
{
    const bool is_user_admin = check_user_is_admin();
    GeneralSettings settings{
        .isElevated = is_process_elevated(),
        .isRunElevated = run_as_elevated,
        .isAdmin = is_user_admin,
        .enableWarningsElevatedApps = enable_warnings_elevated_apps,
        .showNewUpdatesToastNotification = show_new_updates_toast_notification,
        .downloadUpdatesAutomatically = download_updates_automatically && is_user_admin,
        .showWhatsNewAfterUpdates = show_whats_new_after_updates,
        .enableExperimentation = enable_experimentation,
        .theme = settings_theme,
        .systemTheme = WindowsColors::is_dark_mode() ? L"dark" : L"light",
        .powerToysVersion = get_product_version()
    };

    settings.isStartupEnabled = is_auto_start_task_active_for_this_user();

    for (auto& [name, powertoy] : modules())
    {
        settings.isModulesEnabledMap[name] = powertoy->is_enabled();
    }

    return settings;
}

void apply_general_settings(const json::JsonObject& general_configs, bool save)
{
    Logger::info(L"apply_general_settings: {}", std::wstring{ general_configs.ToString() });
    run_as_elevated = general_configs.GetNamedBoolean(L"run_elevated", false);

    enable_warnings_elevated_apps = general_configs.GetNamedBoolean(L"enable_warnings_elevated_apps", true);

    show_new_updates_toast_notification = general_configs.GetNamedBoolean(L"show_new_updates_toast_notification", true);

    download_updates_automatically = general_configs.GetNamedBoolean(L"download_updates_automatically", true);
    show_whats_new_after_updates = general_configs.GetNamedBoolean(L"show_whats_new_after_updates", true);

    enable_experimentation = general_configs.GetNamedBoolean(L"enable_experimentation", true);

    // apply_general_settings is called by the runner's WinMain, so we can just force the run at startup gpo rule here.
    auto gpo_run_as_startup = powertoys_gpo::getConfiguredRunAtStartupValue();

    if (json::has(general_configs, L"startup", json::JsonValueType::Boolean))
    {
        bool startup = general_configs.GetNamedBoolean(L"startup");

        if (gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_enabled)
        {
            startup = true;
        }
        else if (gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_disabled)
        {
            startup = false;
        }

        if (startup)
        {
            if (is_process_elevated())
            {
                delete_auto_start_task_for_this_user();
                create_auto_start_task_for_this_user(run_as_elevated);
            }
            else
            {
                if (!is_auto_start_task_active_for_this_user())
                {
                    delete_auto_start_task_for_this_user();
                    create_auto_start_task_for_this_user(false);

                    run_as_elevated = false;
                }
                else if (!general_configs.GetNamedBoolean(L"run_elevated", false))
                {
                    delete_auto_start_task_for_this_user();
                    create_auto_start_task_for_this_user(false);
                }
            }
        }
        else
        {
            delete_auto_start_task_for_this_user();
        }
    }
    else
    {
        delete_auto_start_task_for_this_user();
        if (gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_enabled || gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_not_configured) {
            create_auto_start_task_for_this_user(run_as_elevated);
        }
    }

    if (json::has(general_configs, L"enabled"))
    {
        for (const auto& enabled_element : general_configs.GetNamedObject(L"enabled"))
        {
            const auto value = enabled_element.Value();
            if (value.ValueType() != json::JsonValueType::Boolean)
            {
                continue;
            }
            const std::wstring name{ enabled_element.Key().c_str() };
            const bool found = modules().find(name) != modules().end();
            if (!found)
            {
                continue;
            }
            PowertoyModule& powertoy = modules().at(name);
            const bool module_inst_enabled = powertoy->is_enabled();
            bool target_enabled = value.GetBoolean();

            auto gpo_rule = powertoy->gpo_policy_enabled_configuration();
            if (gpo_rule == powertoys_gpo::gpo_rule_configured_enabled || gpo_rule == powertoys_gpo::gpo_rule_configured_disabled)
            {
                // Apply the GPO Rule.
                target_enabled = gpo_rule == powertoys_gpo::gpo_rule_configured_enabled;
            }

            if (module_inst_enabled == target_enabled)
            {
                continue;
            }
            if (target_enabled)
            {
                Logger::info(L"apply_general_settings: Enabling powertoy {}", name);
                powertoy->enable();
            }
            else
            {
                Logger::info(L"apply_general_settings: Disabling powertoy {}", name);
                powertoy->disable();
            }
            // Sync the hotkey state with the module state, so it can be removed for disabled modules.
            powertoy.UpdateHotkeyEx();
        }
    }

    if (json::has(general_configs, L"theme", json::JsonValueType::String))
    {
        settings_theme = general_configs.GetNamedString(L"theme");
    }

    if (save)
    {
        GeneralSettings save_settings = get_general_settings();
        PTSettingsHelper::save_general_settings(save_settings.to_json());
        Trace::SettingsChanged(save_settings);
    }
}

void start_enabled_powertoys()
{
    std::unordered_set<std::wstring> powertoys_to_disable;
    std::unordered_map<std::wstring, powertoys_gpo::gpo_rule_configured_t> powertoys_gpo_configuration;
    // Take into account default values supplied by modules themselves and gpo configurations
    for (auto& [name, powertoy] : modules())
    {
        auto gpo_rule = powertoy->gpo_policy_enabled_configuration();
        powertoys_gpo_configuration[name] = gpo_rule;
        if (gpo_rule == powertoys_gpo::gpo_rule_configured_unavailable)
        {
            Logger::warn(L"start_enabled_powertoys: couldn't read the gpo rule for Powertoy {}", name);
        }
        if (gpo_rule == powertoys_gpo::gpo_rule_configured_wrong_value)
        {
            Logger::warn(L"start_enabled_powertoys: gpo rule for Powertoy {} is set to an unknown value", name);
        }

        if (!powertoy->is_enabled_by_default())
            powertoys_to_disable.emplace(name);
    }

    json::JsonObject general_settings;
    try
    {
        general_settings = load_general_settings();
        if (general_settings.HasKey(L"enabled"))
        {
            json::JsonObject enabled = general_settings.GetNamedObject(L"enabled");
            for (const auto& disabled_element : enabled)
            {
                std::wstring disable_module_name{ static_cast<std::wstring_view>(disabled_element.Key()) };

                if (powertoys_gpo_configuration.find(disable_module_name) != powertoys_gpo_configuration.end() && (powertoys_gpo_configuration[disable_module_name] == powertoys_gpo::gpo_rule_configured_enabled || powertoys_gpo_configuration[disable_module_name] == powertoys_gpo::gpo_rule_configured_disabled))
                {
                    // If gpo forces the enabled setting, no need to check the setting for this PowerToy. It will be applied later on this function.
                    continue;
                }

                // Disable explicitly disabled modules
                if (!disabled_element.Value().GetBoolean())
                {
                    Logger::info(L"start_enabled_powertoys: Powertoy {} explicitly disabled", disable_module_name);
                    powertoys_to_disable.emplace(std::move(disable_module_name));
                }
                // If module was scheduled for disable, but it's enabled in the settings - override default value
                else if (auto it = powertoys_to_disable.find(disable_module_name); it != end(powertoys_to_disable))
                {
                    Logger::info(L"start_enabled_powertoys: Overriding default enabled value for {} powertoy", disable_module_name);
                    powertoys_to_disable.erase(it);
                }
            }
        }
    }
    catch (...)
    {
    }

    for (auto& [name, powertoy] : modules())
    {
        bool should_powertoy_be_enabled = true;

        auto gpo_rule = powertoys_gpo_configuration.find(name) != powertoys_gpo_configuration.end() ? powertoys_gpo_configuration[name] : powertoys_gpo::gpo_rule_configured_not_configured;

        if (gpo_rule == powertoys_gpo::gpo_rule_configured_enabled || gpo_rule == powertoys_gpo::gpo_rule_configured_disabled)
        {
            // Apply the GPO Rule.
            should_powertoy_be_enabled = gpo_rule == powertoys_gpo::gpo_rule_configured_enabled;
            Logger::info(L"start_enabled_powertoys: GPO sets the enabled state for {} powertoy as {}", name, should_powertoy_be_enabled);
        }
        else if (powertoys_to_disable.contains(name))
        {
            // Apply the settings or default information provided by the PowerToy on first run.
            should_powertoy_be_enabled = false;
        }

        if (should_powertoy_be_enabled)
        {
            Logger::info(L"start_enabled_powertoys: Enabling powertoy {}", name);
            powertoy->enable();
            powertoy.UpdateHotkeyEx();
        }
    }
}
