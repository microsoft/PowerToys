#pragma once
#include <string>
#include <Shlobj.h>

#include "../utils/json.h"

namespace PTSettingsHelper
{
    constexpr inline const wchar_t* log_settings_filename = L"log_settings.json";

    std::wstring get_powertoys_general_save_file_location();
    std::wstring get_module_save_file_location(std::wstring_view powertoy_key);
    std::wstring get_module_save_folder_location(std::wstring_view powertoy_name);
    std::wstring get_root_save_folder_location();
    std::wstring get_local_low_folder_location();

    void save_module_settings(std::wstring_view powertoy_name, json::JsonObject& settings);
    json::JsonObject load_module_settings(std::wstring_view powertoy_name);
    void save_general_settings(const json::JsonObject& settings);
    json::JsonObject load_general_settings();
    std::wstring get_log_settings_file_location();

    bool get_oobe_opened_state();
    void save_oobe_opened_state();
    std::wstring get_last_version_run();
    void save_last_version_run(const std::wstring& version);
}
