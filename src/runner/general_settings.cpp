#include "pch.h"
#include "general_settings.h"
#include "auto_start_helper.h"
#include <common/settings_helpers.h>
#include "powertoy_module.h"

using namespace web;

web::json::value load_general_settings() {
  return PTSettingsHelper::load_general_settings();
}

web::json::value get_general_settings() {
  json::value result = json::value::object();
  bool startup = is_auto_start_task_active_for_this_user();
  result.as_object()[L"startup"] = json::value::boolean(startup);

  json::value enabled = json::value::object();
  for (auto&[name, powertoy] : modules()) {
    enabled.as_object()[name] = json::value::boolean(powertoy.is_enabled());
  }
  result.as_object()[L"enabled"] = enabled;
  return result;
}

void apply_general_settings(const json::value& general_configs) {
  bool contains_startup = general_configs.has_boolean_field(L"startup");
  if (contains_startup) {
    bool startup = general_configs.at(L"startup").as_bool();
    bool current_startup = is_auto_start_task_active_for_this_user();
    if (current_startup != startup) {
      if (startup) {
        enable_auto_start_task_for_this_user();
      } else {
        disable_auto_start_task_for_this_user();
      }
    }
  }
  bool contains_enabled = general_configs.has_object_field(L"enabled");
  if (contains_enabled) {
    for (auto enabled_element : general_configs.at(L"enabled").as_object()) {
      if (enabled_element.second.is_boolean() && modules().find(enabled_element.first) != modules().end()) {
        bool module_inst_enabled = modules().at(enabled_element.first).is_enabled();
        bool target_enabled = enabled_element.second.as_bool();
        if (module_inst_enabled != target_enabled) {
          if (target_enabled) {
            modules().at(enabled_element.first).enable();
          } else {
            modules().at(enabled_element.first).disable();
          }
        }
      }
    }
  }
  json::value save_settings = get_general_settings();
  PTSettingsHelper::save_general_settings(save_settings);
}

void start_initial_powertoys() {
  bool only_enable_some_powertoys = false;

  std::unordered_set<std::wstring> powertoys_to_enable;

  json::value general_settings;
  try {
    general_settings = load_general_settings();
    json::value enabled = general_settings[L"enabled"];
    for (auto enabled_element : enabled.as_object()) {
      if (enabled_element.second.as_bool()) {
        // Enable this powertoy.
        powertoys_to_enable.emplace(enabled_element.first);
      }
    }
    only_enable_some_powertoys = true;
  }
  catch (std::exception ex) {
    // Couldn't read the general settings correctly.
    // Load all powertoys.
    only_enable_some_powertoys = false;
  }

  for (auto&[name, powertoy] : modules()) {
    if (only_enable_some_powertoys) {
      if (powertoys_to_enable.find(name)!=powertoys_to_enable.end()) {
        powertoy.enable();
      }
    } else {
      powertoy.enable();
    }
  }
}
