#include "pch.h"

#include <common/utils/gpo.h>
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>

#include "settings.h"
#include "constants.h"
#include "Generated Files/resource.h"

// NewSettings are stored in PowerToys/New/settings.json
// The New PowerToy enabled state is stored in the general PowerToys/settings.json

static bool LastModifiedTime(const std::wstring& file_Path, FILETIME* returned_file_timestamp)
{
    WIN32_FILE_ATTRIBUTE_DATA attr{};
    if (GetFileAttributesExW(file_Path.c_str(), GetFileExInfoStandard, &attr))
    {
        *returned_file_timestamp = attr.ftLastWriteTime;
        return true;
    }
    return false;
}

NewSettings::NewSettings()
{
    // New+ overall enable state is stored in the general settings json file
    general_settings_json_file_path = PTSettingsHelper::get_powertoys_general_save_file_location();

    // New+' actual settings are stored in new_settings_json_file_path
    std::wstring settings_save_path = PTSettingsHelper::get_module_save_folder_location(newplus::constants::non_localizable::powertoy_key);
    new_settings_json_file_path = settings_save_path + newplus::constants::non_localizable::settings_json_data_file_path;

    RefreshEnabledState();

    Load();
}

void NewSettings::Save()
{
    json::JsonObject new_settings_json_data;

    new_settings_json_data.SetNamedValue(newplus::constants::non_localizable::settings_json_key_hide_file_extension,
                                         json::value(new_settings.hide_file_extension));

    new_settings_json_data.SetNamedValue(newplus::constants::non_localizable::settings_json_key_hide_starting_digits,
                                         json::value(new_settings.hide_starting_digits));

    new_settings_json_data.SetNamedValue(newplus::constants::non_localizable::settings_json_key_template_location,
                                         json::value(new_settings.template_location));

    json::to_file(new_settings_json_file_path, new_settings_json_data);

    GetSystemTimeAsFileTime(&new_settings_last_loaded_timestamp);
}

void NewSettings::Load()
{
    if (!std::filesystem::exists(new_settings_json_file_path))
    {
        InitializeWithDefaultSettings();

        Save();
    }
    else
    {
        ParseJson();
    }
}

void NewSettings::InitializeWithDefaultSettings()
{
    // Init the default New settings - in case the New/settings.json doesn't exist
    // Currently a similar defaulting logic is also in InitializeWithDefaultSettings in NewViewModel.cs
    SetHideFileExtension(true);

    SetTemplateLocation(GetTemplateLocationDefaultPath());
}

void NewSettings::RefreshEnabledState()
{
    // Load json general settings from data file, if it was modified since we last checked
    FILETIME last_modified_timestamp{};
    if (!(LastModifiedTime(general_settings_json_file_path, &last_modified_timestamp) &&
          CompareFileTime(&last_modified_timestamp, &general_settings_last_loaded_timestamp) == 1))
    {
        return;
    }

    general_settings_last_loaded_timestamp = last_modified_timestamp;

    auto json = json::from_file(general_settings_json_file_path);
    if (!json)
    {
        return;
    }

    // Load the enabled settings for the New PowerToy via the general settings
    const json::JsonObject& json_general_settings = json.value();
    try
    {
        json::JsonObject powertoy_new_enabled_state;
        json::get(json_general_settings, L"enabled", powertoy_new_enabled_state, json::JsonObject{});
        json::get(powertoy_new_enabled_state, newplus::constants::non_localizable::powertoy_key, new_settings.enabled, false);
    }
    catch (const winrt::hresult_error&)
    {
        Logger::error(L"New+ unable to load enabled state from json");
    }
}

void NewSettings::Reload()
{
    // Load json New settings from data file, if it was modified since we last checked.
    FILETIME very_latest_modified_timestamp{};
    if (LastModifiedTime(new_settings_json_file_path, &very_latest_modified_timestamp) &&
        CompareFileTime(&very_latest_modified_timestamp, &new_settings_last_loaded_timestamp) == 1)
    {
        Load();
    }
}

void NewSettings::ParseJson()
{
    auto json = json::from_file(new_settings_json_file_path);
    if (json)
    {
        try
        {
            const json::JsonObject& new_settings_json = json.value();

            if (json::has(new_settings_json, newplus::constants::non_localizable::settings_json_key_hide_file_extension, json::JsonValueType::Boolean))
            {
                new_settings.hide_file_extension = new_settings_json.GetNamedBoolean(
                    newplus::constants::non_localizable::settings_json_key_hide_file_extension);
            }

            if (json::has(new_settings_json, newplus::constants::non_localizable::settings_json_key_hide_starting_digits, json::JsonValueType::Boolean))
            {
                new_settings.hide_starting_digits = new_settings_json.GetNamedBoolean(
                    newplus::constants::non_localizable::settings_json_key_hide_starting_digits);
            }

            if (json::has(new_settings_json, newplus::constants::non_localizable::settings_json_key_template_location, json::JsonValueType::String))
            {
                new_settings.template_location = new_settings_json.GetNamedString(
                    newplus::constants::non_localizable::settings_json_key_template_location);
            }
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
    GetSystemTimeAsFileTime(&new_settings_last_loaded_timestamp);
}

bool NewSettings::GetEnabled()
{
    auto gpoSetting = powertoys_gpo::getConfiguredNewPlusEnabledValue();
    if (gpoSetting == powertoys_gpo::gpo_rule_configured_enabled)
    {
        return true;
    }
    if (gpoSetting == powertoys_gpo::gpo_rule_configured_disabled)
    {
        return false;
    }

    Reload();

    RefreshEnabledState();

    return new_settings.enabled;
}

bool NewSettings::GetHideFileExtension() const
{
    return new_settings.hide_file_extension;
}

void NewSettings::SetHideFileExtension(const bool hide_file_extension)
{
    new_settings.hide_file_extension = hide_file_extension;
}

bool NewSettings::GetHideStartingDigits() const
{
    return new_settings.hide_starting_digits;
}

void NewSettings::SetHideStartingDigits(const bool hide_starting_digits)
{
    new_settings.hide_starting_digits = hide_starting_digits;
}

std::wstring NewSettings::GetTemplateLocation() const
{
    return new_settings.template_location;
}

void NewSettings::SetTemplateLocation(const std::wstring template_location)
{
    new_settings.template_location = template_location;
}

std::wstring NewSettings::GetTemplateLocationDefaultPath()
{
    static const std::wstring default_template_sub_folder_name =
        GET_RESOURCE_STRING_FALLBACK(
            IDS_DEFAULT_TEMPLATE_SUB_FOLDER_NAME_WHERE_TEMPLATES_ARE_STORED,
            L"Templates");

    static const std::wstring full_path = PTSettingsHelper::get_module_save_folder_location(
                                              newplus::constants::non_localizable::powertoy_key) +
                                          L"\\" + default_template_sub_folder_name;

    return full_path;
}

NewSettings& NewSettingsInstance()
{
    static NewSettings instance;

    return instance;
}
