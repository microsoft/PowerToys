#include "pch.h"
#include "general_settings.h"
#include "auto_start_helper.h"
#include "tray_icon.h"
#include "quick_access_host.h"
#include "Generated files/resource.h"
#include "hotkey_conflict_detector.h"

#include <common/SettingsAPI/settings_helpers.h>
#include "powertoy_module.h"
#include <common/themes/windows_colors.h>

#include "trace.h"
#include "ai_detection.h"
#include <common/utils/elevation.h>
#include <common/version/version.h>
#include <common/utils/resources.h>

namespace
{
    json::JsonValue create_empty_shortcut_array_value()
    {
        return json::JsonValue::Parse(L"[]");
    }

    void ensure_ignored_conflict_properties_shape(json::JsonObject& obj)
    {
        if (!json::has(obj, L"ignored_shortcuts", json::JsonValueType::Array))
        {
            obj.SetNamedValue(L"ignored_shortcuts", create_empty_shortcut_array_value());
        }
    }

    json::JsonObject create_default_ignored_conflict_properties()
    {
        json::JsonObject obj;
        ensure_ignored_conflict_properties_shape(obj);
        return obj;
    }

    DashboardSortOrder parse_dashboard_sort_order(const json::JsonObject& obj, DashboardSortOrder fallback)
    {
        if (json::has(obj, L"dashboard_sort_order", json::JsonValueType::Number))
        {
            const auto raw_value = static_cast<int>(obj.GetNamedNumber(L"dashboard_sort_order", static_cast<double>(static_cast<int>(fallback))));
            return raw_value == static_cast<int>(DashboardSortOrder::ByStatus) ? DashboardSortOrder::ByStatus : DashboardSortOrder::Alphabetical;
        }

        if (json::has(obj, L"dashboard_sort_order", json::JsonValueType::String))
        {
            const auto raw = obj.GetNamedString(L"dashboard_sort_order");
            if (raw == L"ByStatus")
            {
                return DashboardSortOrder::ByStatus;
            }

            if (raw == L"Alphabetical")
            {
                return DashboardSortOrder::Alphabetical;
            }
        }

        return fallback;
    }
}

// TODO: would be nice to get rid of these globals, since they're basically cached json settings
static std::wstring settings_theme = L"system";
static bool show_tray_icon = true;
static bool show_theme_adaptive_tray_icon = false;
static bool run_as_elevated = false;
static bool show_new_updates_toast_notification = true;
static bool download_updates_automatically = true;
static bool show_whats_new_after_updates = true;
static bool enable_experimentation = true;
static bool enable_warnings_elevated_apps = true;
static bool enable_quick_access = true;
static PowerToysSettings::HotkeyObject quick_access_shortcut;
static DashboardSortOrder dashboard_sort_order = DashboardSortOrder::Alphabetical;
static json::JsonObject ignored_conflict_properties = create_default_ignored_conflict_properties();

json::JsonObject GeneralSettings::to_json()
{
    json::JsonObject result;

    auto ignoredProps = ignoredConflictProperties;
    ensure_ignored_conflict_properties_shape(ignoredProps);

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

    result.SetNamedValue(L"show_tray_icon", json::value(showSystemTrayIcon));
    result.SetNamedValue(L"show_theme_adaptive_tray_icon", json::value(showThemeAdaptiveTrayIcon));
    result.SetNamedValue(L"is_elevated", json::value(isElevated));
    result.SetNamedValue(L"run_elevated", json::value(isRunElevated));
    result.SetNamedValue(L"show_new_updates_toast_notification", json::value(showNewUpdatesToastNotification));
    result.SetNamedValue(L"download_updates_automatically", json::value(downloadUpdatesAutomatically));
    result.SetNamedValue(L"show_whats_new_after_updates", json::value(showWhatsNewAfterUpdates));
    result.SetNamedValue(L"enable_experimentation", json::value(enableExperimentation));
    result.SetNamedValue(L"dashboard_sort_order", json::value(static_cast<int>(dashboardSortOrder)));
    result.SetNamedValue(L"is_admin", json::value(isAdmin));
    result.SetNamedValue(L"enable_warnings_elevated_apps", json::value(enableWarningsElevatedApps));
    result.SetNamedValue(L"enable_quick_access", json::value(enableQuickAccess));
    result.SetNamedValue(L"quick_access_shortcut", quickAccessShortcut.get_json());
    result.SetNamedValue(L"theme", json::value(theme));
    result.SetNamedValue(L"system_theme", json::value(systemTheme));
    result.SetNamedValue(L"powertoys_version", json::value(powerToysVersion));
    result.SetNamedValue(L"ignored_conflict_properties", json::value(ignoredProps));

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
    show_tray_icon = loaded.GetNamedBoolean(L"show_tray_icon", true);
    show_theme_adaptive_tray_icon = loaded.GetNamedBoolean(L"show_theme_adaptive_tray_icon", false);
    run_as_elevated = loaded.GetNamedBoolean(L"run_elevated", false);
    show_new_updates_toast_notification = loaded.GetNamedBoolean(L"show_new_updates_toast_notification", true);
    download_updates_automatically = loaded.GetNamedBoolean(L"download_updates_automatically", true) && check_user_is_admin();
    show_whats_new_after_updates = loaded.GetNamedBoolean(L"show_whats_new_after_updates", true);
    enable_experimentation = loaded.GetNamedBoolean(L"enable_experimentation", true);
    enable_warnings_elevated_apps = loaded.GetNamedBoolean(L"enable_warnings_elevated_apps", true);
    enable_quick_access = loaded.GetNamedBoolean(L"enable_quick_access", true);
    if (json::has(loaded, L"quick_access_shortcut", json::JsonValueType::Object))
    {
        quick_access_shortcut = PowerToysSettings::HotkeyObject::from_json(loaded.GetNamedObject(L"quick_access_shortcut"));
    }
    dashboard_sort_order = parse_dashboard_sort_order(loaded, dashboard_sort_order);

    if (json::has(loaded, L"ignored_conflict_properties", json::JsonValueType::Object))
    {
        ignored_conflict_properties = loaded.GetNamedObject(L"ignored_conflict_properties");
    }
    else
    {
        ignored_conflict_properties = create_default_ignored_conflict_properties();
    }

    ensure_ignored_conflict_properties_shape(ignored_conflict_properties);

    return loaded;
}

GeneralSettings get_general_settings()
{
    const bool is_user_admin = check_user_is_admin();
    GeneralSettings settings
    {
        .showSystemTrayIcon = show_tray_icon,
        .showThemeAdaptiveTrayIcon = show_theme_adaptive_tray_icon,
        .isElevated = is_process_elevated(),
        .isRunElevated = run_as_elevated,
        .isAdmin = is_user_admin,
        .enableWarningsElevatedApps = enable_warnings_elevated_apps,
        .enableQuickAccess = enable_quick_access,
        .quickAccessShortcut = quick_access_shortcut,
        .showNewUpdatesToastNotification = show_new_updates_toast_notification,
        .downloadUpdatesAutomatically = download_updates_automatically && is_user_admin,
        .showWhatsNewAfterUpdates = show_whats_new_after_updates,
        .enableExperimentation = enable_experimentation,
    .dashboardSortOrder = dashboard_sort_order,
        .theme = settings_theme,
        .systemTheme = WindowsColors::is_dark_mode() ? L"dark" : L"light",
        .powerToysVersion = get_product_version(),
        .ignoredConflictProperties = ignored_conflict_properties
    };

    ensure_ignored_conflict_properties_shape(settings.ignoredConflictProperties);

    settings.isStartupEnabled = is_auto_start_task_active_for_this_user();

    for (auto& [name, powertoy] : modules())
    {
        settings.isModulesEnabledMap[name] = powertoy->is_enabled();
    }

    return settings;
}

void apply_general_settings(const json::JsonObject& general_configs, bool save)
{
    std::wstring old_settings_json_string;
    if (save)
    {
        old_settings_json_string = get_general_settings().to_json().Stringify().c_str();
    }

    Logger::info(L"apply_general_settings: {}", std::wstring{ general_configs.ToString() });
    run_as_elevated = general_configs.GetNamedBoolean(L"run_elevated", false);

    enable_warnings_elevated_apps = general_configs.GetNamedBoolean(L"enable_warnings_elevated_apps", true);

    bool new_enable_quick_access = general_configs.GetNamedBoolean(L"enable_quick_access", true);
    Logger::info(L"apply_general_settings: enable_quick_access={}, new_enable_quick_access={}", enable_quick_access, new_enable_quick_access);

    PowerToysSettings::HotkeyObject new_quick_access_shortcut;
    if (json::has(general_configs, L"quick_access_shortcut", json::JsonValueType::Object))
    {
        new_quick_access_shortcut = PowerToysSettings::HotkeyObject::from_json(general_configs.GetNamedObject(L"quick_access_shortcut"));
    }

    auto hotkey_equals = [](const PowerToysSettings::HotkeyObject& a, const PowerToysSettings::HotkeyObject& b) {
        return a.get_code() == b.get_code() &&
               a.get_modifiers() == b.get_modifiers();
    };

    if (enable_quick_access != new_enable_quick_access || !hotkey_equals(quick_access_shortcut, new_quick_access_shortcut))
    {
        enable_quick_access = new_enable_quick_access;
        quick_access_shortcut = new_quick_access_shortcut;

        if (enable_quick_access)
        {
            QuickAccessHost::start();
        }
        else
        {
            QuickAccessHost::stop();
        }
        update_quick_access_hotkey(enable_quick_access, quick_access_shortcut);
    }

    show_new_updates_toast_notification = general_configs.GetNamedBoolean(L"show_new_updates_toast_notification", true);

    download_updates_automatically = general_configs.GetNamedBoolean(L"download_updates_automatically", true);
    show_whats_new_after_updates = general_configs.GetNamedBoolean(L"show_whats_new_after_updates", true);

    enable_experimentation = general_configs.GetNamedBoolean(L"enable_experimentation", true);
    dashboard_sort_order = parse_dashboard_sort_order(general_configs, dashboard_sort_order);

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
        if (gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_enabled || gpo_run_as_startup == powertoys_gpo::gpo_rule_configured_not_configured)
        {
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
                auto& hkmng = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
                hkmng.EnableHotkeyByModule(name);

                // Trigger AI capability detection when ImageResizer is enabled
                if (name == L"Image Resizer")
                {
                    Logger::info(L"ImageResizer enabled, triggering AI capability detection");
                    DetectAiCapabilitiesAsync(true);  // Skip settings check since we know it's being enabled
                }
            }
            else
            {
                Logger::info(L"apply_general_settings: Disabling powertoy {}", name);
                powertoy->disable();
                auto& hkmng = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
                hkmng.DisableHotkeyByModule(name);
            }
            // Sync the hotkey state with the module state, so it can be removed for disabled modules.
            powertoy.UpdateHotkeyEx();
        }
    }

    if (json::has(general_configs, L"theme", json::JsonValueType::String))
    {
        settings_theme = general_configs.GetNamedString(L"theme");
    }

    if (json::has(general_configs, L"show_tray_icon", json::JsonValueType::Boolean))
    {
        show_tray_icon = general_configs.GetNamedBoolean(L"show_tray_icon");
        set_tray_icon_visible(show_tray_icon);
    }

    if (json::has(general_configs, L"show_theme_adaptive_tray_icon", json::JsonValueType::Boolean))
    {
        bool new_theme_adaptive = general_configs.GetNamedBoolean(L"show_theme_adaptive_tray_icon");
        if (show_theme_adaptive_tray_icon != new_theme_adaptive)
        {
            show_theme_adaptive_tray_icon = new_theme_adaptive;
            set_tray_icon_theme_adaptive(show_theme_adaptive_tray_icon);
        }
    }

    if (json::has(general_configs, L"ignored_conflict_properties", json::JsonValueType::Object))
    {
        ignored_conflict_properties = general_configs.GetNamedObject(L"ignored_conflict_properties");
        ensure_ignored_conflict_properties_shape(ignored_conflict_properties);
    }

    if (save)
    {
        GeneralSettings save_settings = get_general_settings();
        std::wstring new_settings_json_string = save_settings.to_json().Stringify().c_str();
        if (old_settings_json_string != new_settings_json_string)
        {
            PTSettingsHelper::save_general_settings(save_settings.to_json());
            Trace::SettingsChanged(save_settings);
        }
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
            auto& hkmng = HotkeyConflictDetector::HotkeyConflictManager::GetInstance();
            hkmng.EnableHotkeyByModule(name);
            powertoy.UpdateHotkeyEx();
        }
    }
}


