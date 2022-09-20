#include "pch.h"
#include "Settings.h"
#include "Constants.h"

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

FileLocksmithSettings::FileLocksmithSettings()
{
    std::wstring savePath = PTSettingsHelper::get_module_save_folder_location(constants::nonlocalizable::PowerToyKey);
    std::error_code ec;

    jsonFilePath = savePath + constants::nonlocalizable::DataFilePath;
    Load();
}

void FileLocksmithSettings::Save()
{
    json::JsonObject jsonData;

    jsonData.SetNamedValue(constants::nonlocalizable::JsonKeyEnabled, json::value(settings.enabled));

    json::to_file(jsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void FileLocksmithSettings::Load()
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

void FileLocksmithSettings::Reload()
{
    // Load json settings from data file if it is modified in the meantime.
    FILETIME lastModifiedTime{};
    if (LastModifiedTime(jsonFilePath, &lastModifiedTime) &&
        CompareFileTime(&lastModifiedTime, &lastLoadedTime) == 1)
    {
        Load();
    }
}

void FileLocksmithSettings::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
            if (json::has(jsonSettings, constants::nonlocalizable::JsonKeyEnabled, json::JsonValueType::Boolean))
            {
                settings.enabled = jsonSettings.GetNamedBoolean(constants::nonlocalizable::JsonKeyEnabled);
            }
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

FileLocksmithSettings& FileLocksmithSettingsInstance()
{
    static FileLocksmithSettings instance;
    return instance;
}
