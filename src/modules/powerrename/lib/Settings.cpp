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
    const wchar_t c_powerRenameLastRunFilePath[] = L"\\power-rename-last-run-data.json";
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
    const wchar_t c_lastWindowWidth[] = L"LastWindowWidth";
    const wchar_t c_lastWindowHeight[] = L"LastWindowHeight";

}

CSettings::CSettings()
{
    generalJsonFilePath = PTSettingsHelper::get_powertoys_general_save_file_location();
    std::wstring result = PTSettingsHelper::get_module_save_folder_location(PowerRenameConstants::ModuleKey);
    moduleJsonFilePath = result + std::wstring(c_powerRenameDataFilePath);
    UIFlagsFilePath = result + std::wstring(c_powerRenameUIFlagsFilePath);
    RefreshEnabledState();
    Load();
}

void CSettings::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(c_showIconOnMenu, json::value(settings.showIconOnMenu));
    jsonData.SetNamedValue(c_extendedContextMenuOnly, json::value(settings.extendedContextMenuOnly));
    jsonData.SetNamedValue(c_persistState, json::value(settings.persistState));
    jsonData.SetNamedValue(c_mruEnabled, json::value(settings.MRUEnabled));
    jsonData.SetNamedValue(c_maxMRUSize, json::value(settings.maxMRUSize));
    jsonData.SetNamedValue(c_useBoostLib, json::value(settings.useBoostLib));

    json::to_file(moduleJsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::Load()
{
    if (!std::filesystem::exists(moduleJsonFilePath))
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
    if (LastModifiedTime(moduleJsonFilePath, &lastModifiedTime) &&
            CompareFileTime(&lastModifiedTime, &lastLoadedTime) == 1)
    {
        Load();
    }
}

void CSettings::RefreshEnabledState()
{
    // Load json settings from data file if it is modified in the meantime.
    FILETIME lastModifiedTime{};
    if (!(LastModifiedTime(generalJsonFilePath, &lastModifiedTime) &&
          CompareFileTime(&lastModifiedTime, &lastLoadedGeneralSettingsTime) == 1))
        return;

    lastLoadedGeneralSettingsTime = lastModifiedTime;

    auto json = json::from_file(generalJsonFilePath);
    if (!json)
        return;

    const json::JsonObject& jsonSettings = json.value();
    try
    {
        json::JsonObject modulesEnabledState;
        json::get(jsonSettings, L"enabled", modulesEnabledState, json::JsonObject{});
        json::get(modulesEnabledState, L"PowerRename", settings.enabled, true);
    }
    catch (const winrt::hresult_error&)
    {
    }
}

void CSettings::MigrateFromRegistry()
{
    //settings.enabled = GetRegBoolean(c_enabled, true);
    settings.showIconOnMenu = GetRegBoolean(c_showIconOnMenu, true);
    settings.extendedContextMenuOnly = GetRegBoolean(c_extendedContextMenuOnly, false); // Disabled by default.
    settings.persistState = GetRegBoolean(c_persistState, true);
    settings.MRUEnabled = GetRegBoolean(c_mruEnabled, true);
    settings.maxMRUSize = GetRegNumber(c_maxMRUSize, 10);
    settings.flags = GetRegNumber(c_flags, 0);

    LastRunSettingsInstance().SetSearchText(GetRegString(c_searchText, L""));
    LastRunSettingsInstance().SetReplaceText(GetRegString(c_replaceText, L""));

    settings.useBoostLib = false; // Never existed in registry, disabled by default.
}

void CSettings::ParseJson()
{
    auto json = json::from_file(moduleJsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
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

LastRunSettings& LastRunSettingsInstance()
{
    static LastRunSettings instance;
    return instance;
}

void LastRunSettings::Load()
{
    const auto lastRunSettingsFilePath = PTSettingsHelper::get_module_save_folder_location(PowerRenameConstants::ModuleKey) + c_powerRenameLastRunFilePath;
    auto json = json::from_file(lastRunSettingsFilePath);
    if (!json)
        return;

    json::get(*json, c_searchText, searchText, L"");
    json::get(*json, c_replaceText, replaceText, L"");
    json::get(*json, c_lastWindowWidth, lastWindowWidth, DEFAULT_WINDOW_WIDTH);
    json::get(*json, c_lastWindowHeight, lastWindowHeight, DEFAULT_WINDOW_HEIGHT);
}

void LastRunSettings::Save()
{
    json::JsonObject json;

    json.SetNamedValue(c_searchText, json::value(searchText));
    json.SetNamedValue(c_replaceText, json::value(replaceText));
    json.SetNamedValue(c_lastWindowWidth, json::value(lastWindowWidth));
    json.SetNamedValue(c_lastWindowHeight, json::value(lastWindowHeight));

    const auto lastRunSettingsFilePath = PTSettingsHelper::get_module_save_folder_location(PowerRenameConstants::ModuleKey) + c_powerRenameLastRunFilePath;
    json::to_file(lastRunSettingsFilePath, json);
}
