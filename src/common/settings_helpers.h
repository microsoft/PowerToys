#pragma once
#include <string>
#include <Shlobj.h>

#include "json.h"

namespace PTSettingsHelper
{
    std::wstring get_module_save_folder_location(std::wstring_view powertoy_name);
    std::wstring get_module_save_file_location(std::wstring_view powertoy_name);
    std::wstring get_root_save_folder_location();

    void save_module_settings(std::wstring_view powertoy_name, json::JsonObject& settings);
    json::JsonObject load_module_settings(std::wstring_view powertoy_name);
    void save_general_settings(const json::JsonObject& settings);
    json::JsonObject load_general_settings();

}
