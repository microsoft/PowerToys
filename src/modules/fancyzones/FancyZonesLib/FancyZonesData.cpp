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
}

void FancyZonesData::ReplaceZoneSettingsFileFromOlderVersions()
{
    if (std::filesystem::exists(zonesSettingsFileName))
    {
        Logger::info("Replace zones-settings file");

        json::JsonObject fancyZonesDataJSON = JSONHelpers::GetPersistFancyZonesJSON(zonesSettingsFileName, appZoneHistoryFileName);
        
        auto deviceInfoMap = JSONHelpers::ParseDeviceInfos(fancyZonesDataJSON);
        if (deviceInfoMap)
        {
            JSONHelpers::SaveAppliedLayouts(deviceInfoMap.value());
        }

        auto customLayouts = JSONHelpers::ParseCustomZoneSets(fancyZonesDataJSON);
        if (customLayouts)
        {
            JSONHelpers::SaveCustomLayouts(customLayouts.value());
        }

        auto templates = JSONHelpers::ParseLayoutTemplates(fancyZonesDataJSON);
        if (templates)
        {
            JSONHelpers::SaveLayoutTemplates(templates.value());
        }
        
        auto quickKeysMap = JSONHelpers::ParseQuickKeys(fancyZonesDataJSON);
        if (quickKeysMap)
        {
            JSONHelpers::SaveLayoutHotkeys(quickKeysMap.value());
        }

        std::filesystem::remove(zonesSettingsFileName);
    }
}
