#include "pch.h"
#include "Settings.h"
#include "ImageResizerConstants.h"

#include <common/utils/json.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <filesystem>
#include <commctrl.h>

namespace
{
    const wchar_t c_imageResizerDataFilePath[] = L"\\image-resizer-settings.json";
    const wchar_t c_rootRegPath[] = L"Software\\Microsoft\\ImageResizer";
    const wchar_t c_enabled[] = L"enabled";
    const wchar_t c_ImageResizer[] = L"Image Resizer";

    unsigned int RegReadInteger(const std::wstring& valueName, unsigned int defaultValue)
    {
        DWORD type = REG_DWORD;
        DWORD data = 0;
        DWORD size = sizeof(DWORD);
        if (SHGetValue(HKEY_CURRENT_USER, c_rootRegPath, valueName.c_str(), &type, &data, &size) == ERROR_SUCCESS)
        {
            return data;
        }
        return defaultValue;
    }

    bool RegReadBoolean(const std::wstring& valueName, bool defaultValue)
    {
        DWORD value = RegReadInteger(valueName.c_str(), defaultValue ? 1 : 0);
        return (value == 0) ? false : true;
    }

    bool LastModifiedTime(const std::wstring& filePath, FILETIME* lpFileTime)
    {
        WIN32_FILE_ATTRIBUTE_DATA attr{};
        if (GetFileAttributesExW(filePath.c_str(), GetFileExInfoStandard, &attr))
        {
            *lpFileTime = attr.ftLastWriteTime;
            return true;
        }
        return false;
    }
}

CSettings::CSettings()
{
    generalJsonFilePath = PTSettingsHelper::get_powertoys_general_save_file_location();
    std::wstring oldSavePath = PTSettingsHelper::get_module_save_folder_location(ImageResizerConstants::ModuleOldSaveFolderKey);
    std::wstring savePath = PTSettingsHelper::get_module_save_folder_location(ImageResizerConstants::ModuleSaveFolderKey);
    std::error_code ec;
    if (std::filesystem::exists(oldSavePath, ec))
    {
        std::filesystem::copy(oldSavePath, savePath, std::filesystem::copy_options::recursive, ec);
        std::filesystem::remove_all(oldSavePath, ec);
    }

    jsonFilePath = savePath + std::wstring(c_imageResizerDataFilePath);
    Load();
}

void CSettings::Save()
{
    json::JsonObject jsonData;

    json::to_file(jsonFilePath, jsonData);
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

void CSettings::Load()
{
    if (!std::filesystem::exists(jsonFilePath))
    {
        MigrateFromRegistry();

        Save();
    }
    else
    {
        ParseJson();
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
        json::get(jsonSettings, c_enabled, modulesEnabledState, json::JsonObject{});
        json::get(modulesEnabledState, c_ImageResizer, settings.enabled, true);
    }
    catch (const winrt::hresult_error&)
    {
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
    settings.enabled = RegReadBoolean(c_enabled, true);
}

void CSettings::ParseJson()
{
    auto json = json::from_file(jsonFilePath);
    if (json)
    {
        const json::JsonObject& jsonSettings = json.value();
        try
        {
            // NB: add any new settings here
        }
        catch (const winrt::hresult_error&)
        {
        }
    }
    GetSystemTimeAsFileTime(&lastLoadedTime);
}

CSettings& CSettingsInstance()
{
    static CSettings instance;
    return instance;
}