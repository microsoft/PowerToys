#include "pch.h"
#include "FancyZonesData.h"

#include <filesystem>

#include <common/Display/dpi_aware.h>
#include <common/logger/logger.h>
#include <common/SettingsAPI/settings_helpers.h>

#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/ModuleConstants.h>
#include <FancyZonesLib/util.h>

// Non-localizable strings
namespace NonLocalizable
{
    const wchar_t FancyZonesSettingsFile[] = L"settings.json";
    const wchar_t FancyZonesDataFile[] = L"zones-settings.json";
    const wchar_t FancyZonesAppZoneHistoryFile[] = L"app-zone-history.json";
    const wchar_t FancyZonesEditorParametersFile[] = L"editor-parameters.json";
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
    editorParametersFileName = saveFolderPath + L"\\" + std::wstring(NonLocalizable::FancyZonesEditorParametersFile);
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

void FancyZonesData::SaveFancyZonesEditorParameters(bool spanZonesAcrossMonitors, const std::wstring& virtualDesktopId, const HMONITOR& targetMonitor, const std::vector<std::pair<HMONITOR, MONITORINFOEX>>& allMonitors) const
{
    JSONHelpers::EditorArgs argsJson; /* json arguments */
    argsJson.processId = GetCurrentProcessId(); /* Process id */
    argsJson.spanZonesAcrossMonitors = spanZonesAcrossMonitors; /* Span zones */

    if (spanZonesAcrossMonitors)
    {
        auto monitorRect = FancyZonesUtils::GetAllMonitorsCombinedRect<&MONITORINFOEX::rcWork>();
        std::wstring monitorId = FancyZonesUtils::GenerateUniqueIdAllMonitorsArea(virtualDesktopId);

        JSONHelpers::MonitorInfo monitorJson;
        monitorJson.id = monitorId;
        monitorJson.top = monitorRect.top;
        monitorJson.left = monitorRect.left;
        monitorJson.width = monitorRect.right - monitorRect.left;
        monitorJson.height = monitorRect.bottom - monitorRect.top;
        monitorJson.isSelected = true;
        monitorJson.dpi = 0; // unused

        argsJson.monitors.emplace_back(std::move(monitorJson)); /* add monitor data */
    }
    else
    {
        // device id map for correct device ids
        std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;

        for (auto& monitorData : allMonitors)
        {
            HMONITOR monitor = monitorData.first;
            auto monitorInfo = monitorData.second;

            JSONHelpers::MonitorInfo monitorJson;

            std::wstring deviceId = FancyZonesUtils::GetDisplayDeviceId(monitorInfo.szDevice, displayDeviceIdxMap);
            std::wstring monitorId = FancyZonesUtils::GenerateUniqueId(monitor, deviceId, virtualDesktopId);

            if (monitor == targetMonitor)
            {
                monitorJson.isSelected = true; /* Is monitor selected for the main editor window opening */
            }

            monitorJson.id = monitorId; /* Monitor id */

            UINT dpi = 0;
            if (DPIAware::GetScreenDPIForMonitor(monitor, dpi) != S_OK)
            {
                continue;
            }

            monitorJson.dpi = dpi; /* DPI */
            monitorJson.top = monitorInfo.rcWork.top; /* Top coordinate */
            monitorJson.left = monitorInfo.rcWork.left; /* Left coordinate */
            monitorJson.width = monitorInfo.rcWork.right - monitorInfo.rcWork.left; /* Width */
            monitorJson.height = monitorInfo.rcWork.bottom - monitorInfo.rcWork.top; /* Height */
            
            argsJson.monitors.emplace_back(std::move(monitorJson)); /* add monitor data */
        }
    }
    

    json::to_file(editorParametersFileName, JSONHelpers::EditorArgs::ToJson(argsJson));
}
