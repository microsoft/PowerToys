#include "pch.h"
#include "settings_helpers.h"

namespace PTSettingsHelper
{
    constexpr inline const wchar_t* settings_filename = L"\\settings.json";
    constexpr inline const wchar_t* oobe_filename = L"oobe_settings.json";
    constexpr inline const wchar_t* last_version_run_filename = L"last_version_run.json";
    constexpr inline const wchar_t* opened_at_first_launch_json_field_name = L"openedAtFirstLaunch";
    constexpr inline const wchar_t* last_version_json_field_name = L"last_version";

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

    bool get_oobe_opened_state()
    {
        std::filesystem::path oobePath(PTSettingsHelper::get_root_save_folder_location());
        oobePath = oobePath.append(oobe_filename);
        if (std::filesystem::exists(oobePath))
        {
            auto saved_settings = json::from_file(oobePath.c_str());
            if (!saved_settings.has_value())
            {
                return false;
            }

            bool opened = saved_settings->GetNamedBoolean(opened_at_first_launch_json_field_name, false);
            return opened;
        }
        
        return false;
    }

    void save_oobe_opened_state()
    {
        std::filesystem::path oobePath(PTSettingsHelper::get_root_save_folder_location());
        oobePath = oobePath.append(oobe_filename);

        json::JsonObject obj;
        obj.SetNamedValue(opened_at_first_launch_json_field_name, json::value(true));

        json::to_file(oobePath.c_str(), obj);      
    }

    std::wstring get_last_version_run()
    {

        std::filesystem::path lastVersionRunPath(PTSettingsHelper::get_root_save_folder_location());
        lastVersionRunPath = lastVersionRunPath.append(last_version_run_filename);
        if (std::filesystem::exists(lastVersionRunPath))
        {
            auto saved_settings = json::from_file(lastVersionRunPath.c_str());
            if (!saved_settings.has_value())
            {
                return L"";
            }

            std::wstring last_version = saved_settings->GetNamedString(last_version_json_field_name, L"").c_str();
            return last_version;
        }
        return L"";
    }

    void save_last_version_run(const std::wstring& version)
    {
        std::filesystem::path lastVersionRunPath(PTSettingsHelper::get_root_save_folder_location());
        lastVersionRunPath = lastVersionRunPath.append(last_version_run_filename);

        json::JsonObject obj;
        obj.SetNamedValue(last_version_json_field_name, json::value(version));

        json::to_file(lastVersionRunPath.c_str(), obj);
    }

}
