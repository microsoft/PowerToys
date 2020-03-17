#include "pch.h"
#include "general_settings.h"
#include "auto_start_helper.h"

#include <common/common.h>
#include <common/settings_helpers.h>
#include "powertoy_module.h"
#include <common/windows_colors.h>
#include <common/winstore.h>

#include "trace.h"

static std::wstring settings_theme = L"system";
static bool run_as_elevated = false;

// TODO: add resource.rc for settings project and localize
namespace localized_strings
{
    const std::wstring_view STARTUP_DISABLED_BY_POLICY = L"This setting has been disabled by your administrator.";
    const std::wstring_view STARTUP_DISABLED_BY_USER = LR"(This setting has been disabled manually via <a href="https://ms_settings_startupapps" target="_blank">Startup Settings</a>.)";
}

json::JsonObject GeneralSettings::to_json()
{
    json::JsonObject result;

    result.SetNamedValue(L"packaged", json::value(isPackaged));
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
    return loaded;
}

GeneralSettings get_settings()
{
    GeneralSettings settings{
        .isPackaged = winstore::running_as_packaged(),
        .isElevated = is_process_elevated(),
        .isRunElevated = run_as_elevated,
        .isAdmin = check_user_is_admin(),
        .theme = settings_theme,
        .systemTheme = WindowsColors::is_dark_mode() ? L"dark" : L"light",
        .powerToysVersion = get_product_version(),
    };

    if (winstore::running_as_packaged())
    {
        using namespace localized_strings;
        const auto task_state = winstore::get_startup_task_status_async().get();
        switch (task_state)
        {
        case winstore::StartupTaskState::Disabled:
            settings.isStartupEnabled = false;
            break;
        case winstore::StartupTaskState::Enabled:
            settings.isStartupEnabled = true;
            break;
        case winstore::StartupTaskState::DisabledByPolicy:
            settings.startupDisabledReason = STARTUP_DISABLED_BY_POLICY;
            settings.isStartupEnabled = false;
            break;
        case winstore::StartupTaskState::DisabledByUser:
            settings.startupDisabledReason = STARTUP_DISABLED_BY_USER;
            settings.isStartupEnabled = false;
            break;
        }
    }
    else
    {
        settings.isStartupEnabled = is_auto_start_task_active_for_this_user();
    }

    for (auto& [name, powertoy] : modules())
    {
        settings.isModulesEnabledMap[name] = powertoy->is_enabled();
    }

    return settings;
}

json::JsonObject get_general_settings()
{
    auto settings = get_settings();
    return settings.to_json();
}

void apply_general_settings(const json::JsonObject& general_configs)
{
    run_as_elevated = general_configs.GetNamedBoolean(L"run_elevated", false);

    if (json::has(general_configs, L"startup", json::JsonValueType::Boolean))
    {
        const bool startup = general_configs.GetNamedBoolean(L"startup");
        if (winstore::running_as_packaged())
        {
            winstore::switch_startup_task_state_async(startup).wait();
        }
        else
        {
            if (startup)
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
            }
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
            const bool module_inst_enabled = modules().at(name)->is_enabled();
            const bool target_enabled = value.GetBoolean();
            if (module_inst_enabled == target_enabled)
            {
                continue;
            }
            if (target_enabled)
            {
                modules().at(name)->enable();
            }
            else
            {
                modules().at(name)->disable();
            }
        }
    }

    if (json::has(general_configs, L"theme", json::JsonValueType::String))
    {
        settings_theme = general_configs.GetNamedString(L"theme");
    }

    GeneralSettings save_settings = get_settings();
    PTSettingsHelper::save_general_settings(save_settings.to_json());
    Trace::SettingsChanged(save_settings);
}

void start_initial_powertoys()
{
    bool only_enable_some_powertoys = false;

    std::unordered_set<std::wstring> powertoys_to_enable;

    json::JsonObject general_settings;
    try
    {
        general_settings = load_general_settings();
        json::JsonObject enabled = general_settings.GetNamedObject(L"enabled");
        for (const auto& enabled_element : enabled)
        {
            if (enabled_element.Value().GetBoolean())
            {
                // Enable this powertoy.
                powertoys_to_enable.emplace(enabled_element.Key());
            }
        }
        only_enable_some_powertoys = true;
    }
    catch (...)
    {
        // Couldn't read the general settings correctly.
        // Load all powertoys.
        // TODO: notify user about invalid json config
        only_enable_some_powertoys = false;
    }

    for (auto& [name, powertoy] : modules())
    {
        if (only_enable_some_powertoys)
        {
            if (powertoys_to_enable.find(name) != powertoys_to_enable.end())
            {
                powertoy->enable();
            }
        }
        else
        {
            powertoy->enable();
        }
    }
}
