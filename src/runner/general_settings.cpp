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
static bool startup_disabled_manually = false;
static bool run_as_elevated = false;
static bool download_updates_automatically = true;

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
    result.SetNamedValue(L"download_updates_automatically", json::value(downloadUpdatesAutomatically));
    result.SetNamedValue(L"is_admin", json::value(isAdmin));
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
    download_updates_automatically = loaded.GetNamedBoolean(L"download_updates_automatically", true) && check_user_is_admin();

    return loaded;
}

GeneralSettings get_general_settings()
{
    const bool is_user_admin = check_user_is_admin();
    GeneralSettings settings{
        .isElevated = is_process_elevated(),
        .isRunElevated = run_as_elevated,
        .isAdmin = is_user_admin,
        .downloadUpdatesAutomatically = download_updates_automatically && is_user_admin,
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
    run_as_elevated = general_configs.GetNamedBoolean(L"run_elevated", false);

    download_updates_automatically = general_configs.GetNamedBoolean(L"download_updates_automatically", true);

    if (json::has(general_configs, L"startup", json::JsonValueType::Boolean))
    {
        const bool startup = general_configs.GetNamedBoolean(L"startup");

        auto settings = get_general_settings();
        static std::once_flag once_flag;
        std::call_once(once_flag, [settings, startup, general_configs] {
            if (json::has(general_configs, L"startup", json::JsonValueType::Boolean))
            {
                if (startup == true && settings.isStartupEnabled == false)
                {
                    startup_disabled_manually = true;
                }
            }
        });

        if (startup && !startup_disabled_manually)
        {
            if (is_process_elevated())
            {
                delete_auto_start_task_for_this_user();
                create_auto_start_task_for_this_user(general_configs.GetNamedBoolean(L"run_elevated", false));
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
            startup_disabled_manually = false;
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
            const bool target_enabled = value.GetBoolean();
            if (module_inst_enabled == target_enabled)
            {
                continue;
            }
            if (target_enabled)
            {
                powertoy->enable();
            }
            else
            {
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
    // Take into account default values supplied by modules themselves
    for (auto& [name, powertoy] : modules())
    {
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
                // Disable explicitly disabled modules
                if (!disabled_element.Value().GetBoolean())
                {
                    powertoys_to_disable.emplace(std::move(disable_module_name));
                }
                // If module was scheduled for disable, but it's enabled in the settings - override default value
                else if (auto it = powertoys_to_disable.find(disable_module_name); it != end(powertoys_to_disable))
                {
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
        if (!powertoys_to_disable.contains(name))
        {
            powertoy->enable();
            powertoy.UpdateHotkeyEx();
        }
    }
}
