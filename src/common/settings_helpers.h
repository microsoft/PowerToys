#pragma once
#include <string>
#include <Shlobj.h>

#include "json.h"

namespace PTSettingsHelper {

  void save_module_settings(std::wstring_view powertoy_name, json::JsonObject& settings);
  json::JsonObject load_module_settings(std::wstring_view powertoy_name);
  void save_general_settings(const json::JsonObject& settings);
  json::JsonObject load_general_settings();

}
