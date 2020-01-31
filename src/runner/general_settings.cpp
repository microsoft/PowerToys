#include "pch.h"
#include "general_settings.h"
#include "auto_start_helper.h"

#include <common/common.h>
#include <common/settings_helpers.h>
#include "powertoy_module.h"
#include <common/windows_colors.h>
#include <common/winstore.h>

static std::wstring settings_theme = L"system";
static bool run_as_elevated = false;

// TODO: add resource.rc for settings project and localize
namespace localized_strings
{
    const std::wstring_view STARTUP_DISABLED_BY_POLICY = L"This setting has been disabled by your administrator.";
    const std::wstring_view STARTUP_DISABLED_BY_USER = LR"(This setting has been disabled manually via <a href="https://ms_settings_startupapps" target="_blank">Startup Settings</a>.)";
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

json::JsonObject get_general_settings()
{
    json::JsonObject result;

    const bool packaged = winstore::running_as_packaged();
    result.SetNamedValue(L"packaged", json::value(packaged));

    bool startup{};
    if (winstore::running_as_packaged())
    {
        using namespace localized_strings;
        const auto task_state = winstore::get_startup_task_status_async().get();
        switch (task_state)
        {
        case winstore::StartupTaskState::Disabled:
            startup = false;
            break;
        case winstore::StartupTaskState::Enabled:
            startup = true;
            break;
        case winstore::StartupTaskState::DisabledByPolicy:
            result.SetNamedValue(L"startup_disabled_reason", json::value(STARTUP_DISABLED_BY_POLICY));
            startup = false;
            break;
        case winstore::StartupTaskState::DisabledByUser:
            result.SetNamedValue(L"startup_disabled_reason", json::value(STARTUP_DISABLED_BY_USER));
            startup = false;
            break;
        }
    }
    else
    {
        startup = is_auto_start_task_active_for_this_user();
    }
    result.SetNamedValue(L"startup", json::value(startup));

    json::JsonObject enabled;
    for (auto& [name, powertoy] : modules())
    {
        enabled.SetNamedValue(name, json::value(powertoy.is_enabled()));
    }
    result.SetNamedValue(L"enabled", std::move(enabled));

    bool is_elevated = is_process_elevated();
    result.SetNamedValue(L"is_elevated", json::value(is_elevated));
    result.SetNamedValue(L"run_elevated", json::value(run_as_elevated));
    result.SetNamedValue(L"theme", json::value(settings_theme));
    result.SetNamedValue(L"system_theme", json::value(WindowsColors::is_dark_mode() ? L"dark" : L"light"));
    result.SetNamedValue(L"powertoys_version", json::value(get_product_version()));
    return result;
}

void apply_general_settings(const json::JsonObject& general_configs)
{
    if (json::has(general_configs, L"startup", json::JsonValueType::Boolean))
    {
        const bool startup = general_configs.GetNamedBoolean(L"startup");
        if (winstore::running_as_packaged())
        {
            winstore::switch_startup_task_state_async(startup).wait();
        }
        else
        {
            const bool current_startup = is_auto_start_task_active_for_this_user();
            if (current_startup != startup)
            {
                if (startup)
                {
                    enable_auto_start_task_for_this_user();
                }
                else
                {
                    disable_auto_start_task_for_this_user();
                }
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
            const bool module_inst_enabled = modules().at(name).is_enabled();
            const bool target_enabled = value.GetBoolean();
            if (module_inst_enabled == target_enabled)
            {
                continue;
            }
            if (target_enabled)
            {
                modules().at(name).enable();
            }
            else
            {
                modules().at(name).disable();
            }
        }
    }
    run_as_elevated = general_configs.GetNamedBoolean(L"run_elevated", false);
    if (json::has(general_configs, L"theme", json::JsonValueType::String))
    {
        settings_theme = general_configs.GetNamedString(L"theme");
    }
    json::JsonObject save_settings = get_general_settings();
    PTSettingsHelper::save_general_settings(save_settings);
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
                powertoy.enable();
            }
        }
        else
        {
            powertoy.enable();
        }
    }
}
