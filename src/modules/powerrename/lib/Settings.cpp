#include "pch.h"
#include "Settings.h"
#include "PowerRenameInterfaces.h"
#include "Helpers.h"
#include <common/SettingsAPI/settings_helpers.h>

#include <filesystem>
#include <commctrl.h>
#include <algorithm>
#include <fstream>
#include <dll/PowerRenameConstants.h>

namespace
{
    const wchar_t c_powerRenameDataFilePath[] = L"\\power-rename-settings.json";
    const wchar_t c_powerRenameUIFlagsFilePath[] = L"\\power-rename-ui-flags";

    const wchar_t c_enabled[] = L"Enabled";
    const wchar_t c_showIconOnMenu[] = L"ShowIcon";
    const wchar_t c_extendedContextMenuOnly[] = L"ExtendedContextMenuOnly";
    const wchar_t c_persistState[] = L"PersistState";
    const wchar_t c_maxMRUSize[] = L"MaxMRUSize";
    const wchar_t c_flags[] = L"Flags";
    const wchar_t c_searchText[] = L"SearchText";
    const wchar_t c_replaceText[] = L"ReplaceText";
    const wchar_t c_mruEnabled[] = L"MRUEnabled";
    const wchar_t c_useBoostLib[] = L"UseBoostLib";

}

CSettings::CSettings()
{
    std::wstring result = PTSettingsHelper::get_module_save_folder_location(PowerRenameConstants::ModuleKey);
    jsonFilePath = result + std::wstring(c_powerRenameDataFilePath);
    UIFlagsFilePath = result + std::wstring(c_powerRenameUIFlagsFilePath);
    Load();
}

void CSettings::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(c_enabled, json::value(settings.enabled));
    jsonData.SetNamedValue(c_showIconOnMenu, json::value(settings.showIconOnMenu));
    jsonData.SetNamedValue(c_extendedContextMenuOnly, json::value(settings.extendedContextMenuOnly));
    jsonData.SetNamedValue(c_persistState, json::value(settings.persistState));
    jsonData.SetNamedValue(c_mruEnabled, json::value(settings.MRUEnabled));
    jsonData.SetNamedValue(c_maxMRUSize, json::value(settings.maxMRUSize));
    jsonData.SetNamedValue(c_searchText, json::value(settings.searchText));
    jsonData.SetNamedValue(c_replaceText, json::value(settings.replaceText));
    jsonData.SetNamedValue(c_useBoostLib, json::value(settings.useBoostLib));

    json::to_file(jsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        MigrateFromRegistry();

        Save();
        WriteFlags();
    }
    else
    {
        ParseJson();
        ReadFlags();
    }
}

void CSettings::Reload()
{
    // Load json settings from data file if it is modified in the meantime.
    FILETIME lastModifiedTime{};
    if (LastModifiedTime(jsonFilePath, &lastModifiedTime) &&
        CompareFileTime(&lastModifiedTime, &lastLoadedTime) == 1)
    {
        Load();
    }
}

void CSettings::MigrateFromRegistry()
{
    settings.enabled = GetRegBoolean(c_enabled, true);
    settings.showIconOnMenu = GetRegBoolean(c_showIconOnMenu, true);
    settings.extendedContextMenuOnly = GetRegBoolean(c_extendedContextMenuOnly, false); // Disabled by default.
    settings.persistState = GetRegBoolean(c_persistState, true);
    settings.MRUEnabled = GetRegBoolean(c_mruEnabled, true);
    settings.maxMRUSize = GetRegNumber(c_maxMRUSize, 10);
    settings.flags = GetRegNumber(c_flags, 0);
    settings.searchText = GetRegString(c_searchText, L"");
    settings.replaceText = GetRegString(c_replaceText, L"");
    settings.useBoostLib = false; // Never existed in registry, disabled by default.
}

void CSettings::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
            if (json::has(jsonSettings, c_enabled, json::JsonValueType::Boolean))
            {
                settings.enabled = jsonSettings.GetNamedBoolean(c_enabled);
            }
            if (json::has(jsonSettings, c_showIconOnMenu, json::JsonValueType::Boolean))
            {
                settings.showIconOnMenu = jsonSettings.GetNamedBoolean(c_showIconOnMenu);
            }
            if (json::has(jsonSettings, c_extendedContextMenuOnly, json::JsonValueType::Boolean))
            {
                settings.extendedContextMenuOnly = jsonSettings.GetNamedBoolean(c_extendedContextMenuOnly);
            }
            if (json::has(jsonSettings, c_persistState, json::JsonValueType::Boolean))
            {
                settings.persistState = jsonSettings.GetNamedBoolean(c_persistState);
            }
            if (json::has(jsonSettings, c_mruEnabled, json::JsonValueType::Boolean))
            {
                settings.MRUEnabled = jsonSettings.GetNamedBoolean(c_mruEnabled);
            }
            if (json::has(jsonSettings, c_maxMRUSize, json::JsonValueType::Number))
            {
                settings.maxMRUSize = static_cast<unsigned int>(jsonSettings.GetNamedNumber(c_maxMRUSize));
            }
            if (json::has(jsonSettings, c_searchText, json::JsonValueType::String))
            {
                settings.searchText = jsonSettings.GetNamedString(c_searchText);
            }
            if (json::has(jsonSettings, c_replaceText, json::JsonValueType::String))
            {
                settings.replaceText = jsonSettings.GetNamedString(c_replaceText);
            }
            if (json::has(jsonSettings, c_useBoostLib, json::JsonValueType::Boolean))
            {
                settings.useBoostLib = jsonSettings.GetNamedBoolean(c_useBoostLib);
            }
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::ReadFlags()
{
    std::ifstream file(UIFlagsFilePath, std::ios::binary);
    if (file.is_open())
    {
        file >> settings.flags;
    }
}

void CSettings::WriteFlags()
{
    std::ofstream file(UIFlagsFilePath, std::ios::binary);
    if (file.is_open())
    {
        file << settings.flags;
    }
}

CSettings& CSettingsInstance()
{
    static CSettings instance;
    return instance;
}
