#pragma once
#include <array>
#include <string>
#include <string_view>
#include <Shlobj.h>

#include "../utils/json.h"

namespace PTSettingsHelper
{
    constexpr inline const wchar_t* log_settings_filename = L"log_settings.json";
    constexpr inline const wchar_t* low_memory_modules_json_field_name = L"low_memory_modules";
    constexpr inline std::array supported_low_memory_modules{
        std::wstring_view{ L"TextExtractor" },
        std::wstring_view{ L"ColorPicker" },
        std::wstring_view{ L"AdvancedPaste" },
        std::wstring_view{ L"Peek" },
    };

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

    json::JsonObject create_default_low_memory_module_settings();
    void refresh_low_memory_settings_cache();
    void refresh_low_memory_settings_cache(const json::JsonObject& general_settings);
    void ensure_low_memory_settings_shape(json::JsonObject& obj);
    bool is_any_low_memory_module_enabled(const json::JsonObject& low_memory_settings);
    bool is_low_memory_module(std::wstring_view module_key);
    bool is_low_memory_mode_enabled(std::wstring_view powertoy_key, bool default_value = false);

    bool get_oobe_opened_state();
    void save_oobe_opened_state();
    std::wstring get_last_version_run();
    void save_last_version_run(const std::wstring& version);

    void save_data_diagnostics(bool enabled);
}
