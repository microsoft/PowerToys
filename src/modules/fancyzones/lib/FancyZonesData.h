#pragma once

#include "JsonHelpers.h"

#include <common/settings_helpers.h>
#include <common/json.h>
#include <mutex>

#include <string>
#include <unordered_map>
#include <optional>
#include <vector>
#include <winnt.h>

namespace FancyZonesDataTypes
{
    struct ZoneSetData;
    struct DeviceInfoData;
    struct CustomZoneSetData;
    struct AppZoneHistoryData;
}

#if defined(UNIT_TESTS)
namespace FancyZonesUnitTests
{
    class FancyZonesDataUnitTests;
    class FancyZonesIFancyZonesCallbackUnitTests;
    class ZoneSetCalculateZonesUnitTests;
    class ZoneWindowUnitTests;
}
#endif
namespace FancyZonesDataNS
{
    const std::wstring& GetActiveZoneSetTmpPath();
    const std::wstring& GetAppliedZoneSetTmpPath();
    const std::wstring& GetDeletedCustomZoneSetsTmpPath();

    class FancyZonesData
    {
    public:
        FancyZonesData();

        inline const std::wstring& GetPersistFancyZonesJSONPath() const
        {
            return zonesSettingsFilePath;
        }

        inline const std::wstring& GetPersistAppZoneHistoryFilePath() const
        {
            return appZoneHistoryFilePath;
        }

        std::optional<FancyZonesDataTypes::DeviceInfoData> FindDeviceInfo(const std::wstring& zoneWindowId) const;

        std::optional<FancyZonesDataTypes::CustomZoneSetData> FindCustomZoneSet(const std::wstring& guid) const;

        inline const std::unordered_map<std::wstring, FancyZonesDataTypes::DeviceInfoData>& GetDeviceInfoMap() const
        {
            std::scoped_lock lock{ dataLock };
            return deviceInfoMap;
        }

        inline const std::unordered_map<std::wstring, FancyZonesDataTypes::CustomZoneSetData>& GetCustomZoneSetsMap() const
        {
            std::scoped_lock lock{ dataLock };
            return customZoneSetsMap;
        }

        inline const std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>>& GetAppZoneHistoryMap() const
        {
            std::scoped_lock lock{ dataLock };
            return appZoneHistoryMap;
        }

        void AddDevice(const std::wstring& deviceId);
        void CloneDeviceInfo(const std::wstring& source, const std::wstring& destination);
        void UpdatePrimaryDesktopData(const std::wstring& desktopId);
        void RemoveDeletedDesktops(const std::vector<std::wstring>& activeDesktops);

        bool IsAnotherWindowOfApplicationInstanceZoned(HWND window, const std::wstring_view& deviceId) const;
        void UpdateProcessIdToHandleMap(HWND window, const std::wstring_view& deviceId);
        std::vector<int> GetAppLastZoneIndexSet(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId) const;
        bool RemoveAppLastZone(HWND window, const std::wstring_view& deviceId, const std::wstring_view& zoneSetId);
        bool SetAppLastZones(HWND window, const std::wstring& deviceId, const std::wstring& zoneSetId, const std::vector<int>& zoneIndexSet);

        void SetActiveZoneSet(const std::wstring& deviceId, const FancyZonesDataTypes::ZoneSetData& zoneSet);

        void ParseDataFromTmpFiles();

        json::JsonObject GetPersistFancyZonesJSON();

        void LoadFancyZonesData();
        void SaveFancyZonesData() const;

    private:
#if defined(UNIT_TESTS)
        friend class FancyZonesUnitTests::FancyZonesDataUnitTests;
        friend class FancyZonesUnitTests::FancyZonesIFancyZonesCallbackUnitTests;
        friend class FancyZonesUnitTests::ZoneWindowUnitTests;
        friend class FancyZonesUnitTests::ZoneSetCalculateZonesUnitTests;

        inline void SetDeviceInfo(const std::wstring& deviceId, FancyZonesDataTypes::DeviceInfoData data)
        {
            deviceInfoMap[deviceId] = data;
        }

        inline bool ParseDeviceInfos(const json::JsonObject& fancyZonesDataJSON)
        {
            deviceInfoMap = JSONHelpers::ParseDeviceInfos(fancyZonesDataJSON);
            return !deviceInfoMap.empty();
        }

        inline void clear_data()
        {
            appZoneHistoryMap.clear();
            deviceInfoMap.clear();
            customZoneSetsMap.clear();
        }

        inline void SetSettingsModulePath(std::wstring_view moduleName)
        {
            std::wstring result = PTSettingsHelper::get_module_save_folder_location(moduleName);
            zonesSettingsFilePath = result + L"\\" + std::wstring(L"zones-settings.json");
            appZoneHistoryFilePath = result + L"\\" + std::wstring(L"app-zone-history.json");
        }
#endif
        void ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath);
        void ParseCustomZoneSetFromTmpFile(std::wstring_view tmpFilePath);
        void ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath);

        void MigrateCustomZoneSetsFromRegistry();
        void RemoveDesktopAppZoneHistory(const std::wstring& desktopId);

        std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>> appZoneHistoryMap{};
        std::unordered_map<std::wstring, FancyZonesDataTypes::DeviceInfoData> deviceInfoMap{};
        std::unordered_map<std::wstring, FancyZonesDataTypes::CustomZoneSetData> customZoneSetsMap{};

        std::wstring zonesSettingsFilePath;
        std::wstring appZoneHistoryFilePath;

        mutable std::recursive_mutex dataLock;
    };

    FancyZonesData& FancyZonesDataInstance();
}
