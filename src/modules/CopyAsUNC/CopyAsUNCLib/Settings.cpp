#include "pch.h"
#include "Settings.h"
#include "Constants.h"

#include <filesystem>
#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>

static bool LastModifiedTime(const std::wstring& filePath, FILETIME* lpFileTime)
{
    WIN32_FILE_ATTRIBUTE_DATA attr{};
    if (GetFileAttributesExW(filePath.c_str(), GetFileExInfoStandard, &attr))
    {
        *lpFileTime = attr.ftLastWriteTime;
        return true;
    }
    return false;
}

CopyAsUNCSettings::CopyAsUNCSettings()
{
    generalJsonFilePath = PTSettingsHelper::get_powertoys_general_save_file_location();
    std::wstring savePath = PTSettingsHelper::get_module_save_folder_location(constants::nonlocalizable::PowerToyKey);
    std::error_code ec;

    jsonFilePath = savePath + constants::nonlocalizable::DataFilePath;
    RefreshEnabledState();
    Load();
}

void CopyAsUNCSettings::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(constants::nonlocalizable::JsonKeyShowInExtendedContextMenu, json::value(settings.showInExtendedContextMenu));

    json::to_file(jsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CopyAsUNCSettings::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        Save();
    }
    else
    {
        ParseJson();
    }
}

void CopyAsUNCSettings::RefreshEnabledState()
{
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
        json::get(modulesEnabledState, L"Copy as UNC", settings.enabled, true);
    }
    catch (const winrt::hresult_error&)
    {
    }
}

void CopyAsUNCSettings::Reload()
{
    FILETIME lastModifiedTime{};
    if (LastModifiedTime(jsonFilePath, &lastModifiedTime) &&
        CompareFileTime(&lastModifiedTime, &lastLoadedTime) == 1)
    {
        Load();
    }
}

void CopyAsUNCSettings::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
            if (json::has(jsonSettings, constants::nonlocalizable::JsonKeyShowInExtendedContextMenu, json::JsonValueType::Boolean))
            {
                settings.showInExtendedContextMenu = jsonSettings.GetNamedBoolean(constants::nonlocalizable::JsonKeyShowInExtendedContextMenu);
            }
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

CopyAsUNCSettings& CopyAsUNCSettingsInstance()
{
    static CopyAsUNCSettings instance;
    return instance;
}
