#include "pch.h"
#include "settings_helpers.h"

namespace PTSettingsHelper
{
    constexpr inline const wchar_t* settings_filename = L"\\settings.json";
    constexpr inline const wchar_t* oobe_filename = L"oobe_settings.json";
    constexpr inline const wchar_t* last_version_run_filename = L"last_version_run.json";
    constexpr inline const wchar_t* opened_at_first_launch_json_field_name = L"openedAtFirstLaunch";
    constexpr inline const wchar_t* last_version_json_field_name = L"last_version";
    constexpr inline const wchar_t* DataDiagnosticsRegKey = L"Software\\Classes\\PowerToys";
    constexpr inline const wchar_t* DataDiagnosticsRegValueName = L"AllowDataDiagnostics";

    std::wstring get_root_save_folder_location()
    {
        PWSTR local_app_path;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppData, 0, NULL, &local_app_path));
        std::wstring result{ local_app_path };
        CoTaskMemFree(local_app_path);

        result += L"\\Microsoft\\PowerToys";
        std::filesystem::path save_path(result);
        if (!std::filesystem::exists(save_path))
        {
            std::filesystem::create_directories(save_path);
        }
        return result;
    }

    std::wstring get_local_low_folder_location()
    {
        PWSTR local_app_path;
        winrt::check_hresult(SHGetKnownFolderPath(FOLDERID_LocalAppDataLow, 0, NULL, &local_app_path));
        std::wstring result{ local_app_path };
        CoTaskMemFree(local_app_path);

        result += L"\\Microsoft\\PowerToys";
        std::filesystem::path save_path(result);
        if (!std::filesystem::exists(save_path))
        {
            std::filesystem::create_directories(save_path);
        }
        return result;
    }

    std::wstring get_module_save_folder_location(std::wstring_view powertoy_key)
    {
        std::wstring result = get_root_save_folder_location();
        result += L"\\";
        result += powertoy_key;
        std::filesystem::path save_path(result);
        if (!std::filesystem::exists(save_path))
        {
            std::filesystem::create_directories(save_path);
        }
        return result;
    }

    std::wstring get_module_save_file_location(std::wstring_view powertoy_key)
    {
        return get_module_save_folder_location(powertoy_key) + settings_filename;
    }

    std::wstring get_powertoys_general_save_file_location()
    {
        return get_root_save_folder_location() + settings_filename;
    }

    void save_module_settings(std::wstring_view powertoy_key, json::JsonObject& settings)
    {
        const std::wstring save_file_location = get_module_save_file_location(powertoy_key);
        json::to_file(save_file_location, settings);
    }

    json::JsonObject load_module_settings(std::wstring_view powertoy_key)
    {
        const std::wstring save_file_location = get_module_save_file_location(powertoy_key);
        auto saved_settings = json::from_file(save_file_location);
        return saved_settings.has_value() ? std::move(*saved_settings) : json::JsonObject{};
    }

    void save_general_settings(const json::JsonObject& settings)
    {
        const std::wstring save_file_location = get_powertoys_general_save_file_location();
        json::to_file(save_file_location, settings);
    }

    json::JsonObject load_general_settings()
    {
        const std::wstring save_file_location = get_powertoys_general_save_file_location();
        auto saved_settings = json::from_file(save_file_location);
        return saved_settings.has_value() ? std::move(*saved_settings) : json::JsonObject{};
    }

    std::wstring get_log_settings_file_location()
    {
        std::filesystem::path result(PTSettingsHelper::get_root_save_folder_location());
        result = result.append(log_settings_filename);
        return result.wstring();
    }

    void save_data_diagnostics(bool enabled)
    {
        HKEY key{};
        if (RegCreateKeyExW(HKEY_CURRENT_USER,
                            DataDiagnosticsRegKey,
                            0,
                            nullptr,
                            REG_OPTION_NON_VOLATILE,
                            KEY_ALL_ACCESS,
                            nullptr,
                            &key,
                            nullptr) != ERROR_SUCCESS)
        {
            return;
        }

        const DWORD value = enabled ? 1 : 0;
        if (RegSetValueExW(key, DataDiagnosticsRegValueName, 0, REG_DWORD, reinterpret_cast<const BYTE*>(&value), sizeof(value)) != ERROR_SUCCESS)
        {
            RegCloseKey(key);
            return;
        }
        RegCloseKey(key);
    }
}
