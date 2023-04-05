#include "pch.h"
#include "FancyZonesData.h"

#include <filesystem>

#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/ModuleConstants.h>

// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t FancyZonesSettingsFile[] = L"settings.json";
    const wchar_t FancyZonesDataFile[] = L"zones-settings.json";
    const wchar_t FancyZonesAppZoneHistoryFile[] = L"app-zone-history.json";
}

FancyZonesData& FancyZonesDataInstance()
{
    static FancyZonesData instance;
    return instance;
}

FancyZonesData::FancyZonesData()
{
    std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);

    settingsFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesSettingsFile);
    appZoneHistoryFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesAppZoneHistoryFile);
    zonesSettingsFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesDataFile);

    // Custom code to avoid overlapping windows in zones
    // Load previously saved zones settings
    if (std::filesystem::exists(zonesSettingsFileName))
    {
        Logger::info("Loading existing zones settings file {}", zonesSettingsFileName.c_str());
        json::JsonObject fancyZonesDataJSON = JSONHelpers::ParseJsonFile(zonesSettingsFileName);
        if (fancyZonesDataJSON.HasNamedField(NonLocalizable::CustomZoneSets))
        {
            customZoneSets = JSONHelpers::DeserializeCustomZoneSets(fancyZonesDataJSON.GetNamedArray(NonLocalizable::CustomZoneSets));
        }
        if (fancyZonesDataJSON.HasNamedField(NonLocalizable::Devices))
        {
            zoneSettingsMap = JSONHelpers::DeserializeDeviceZoneSettingsMap(fancyZonesDataJSON.GetNamedArray(NonLocalizable::Devices));
        }
    }
}
