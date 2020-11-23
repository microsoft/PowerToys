#pragma once

#include "JsonHelpers.h"
#include "HashHelpers.h"

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
    struct DeviceIdData;
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
    class ZoneWindowCreationUnitTests;
}
#endif

class FancyZonesData
{
public:
    FancyZonesData();

    std::optional<FancyZonesDataTypes::DeviceInfoData> FindDeviceInfo(const FancyZonesDataTypes::DeviceIdData& zoneWindowId) const;

    std::optional<FancyZonesDataTypes::CustomZoneSetData> FindCustomZoneSet(const std::wstring& guid) const;

    inline const std::unordered_map<FancyZonesDataTypes::DeviceIdData, FancyZonesDataTypes::DeviceInfoData>& GetDeviceInfoMap() const
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

    bool AddDevice(const FancyZonesDataTypes::DeviceIdData& deviceId);
    void CloneDeviceInfo(const FancyZonesDataTypes::DeviceIdData& source, const FancyZonesDataTypes::DeviceIdData& destination);
    void UpdatePrimaryDesktopData(const GUID& desktopId);
    void RemoveDeletedDesktops(const std::vector<GUID>& activeDesktops);

    bool IsAnotherWindowOfApplicationInstanceZoned(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId) const;
    void UpdateProcessIdToHandleMap(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId);
    std::vector<size_t> GetAppLastZoneIndexSet(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId) const;
    bool RemoveAppLastZone(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId);
    bool SetAppLastZones(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring& zoneSetId, const std::vector<size_t>& zoneIndexSet);

    void SetActiveZoneSet(const FancyZonesDataTypes::DeviceIdData& deviceId, const FancyZonesDataTypes::ZoneSetData& zoneSet);

    void SerializeDeviceInfoToTmpFile(const GUID& currentVirtualDesktop) const;
    void ParseDataFromTmpFiles();

    json::JsonObject GetPersistFancyZonesJSON();

    void LoadFancyZonesData();
    void SaveFancyZonesData() const;

private:
#if defined(UNIT_TESTS)
    friend class FancyZonesUnitTests::FancyZonesDataUnitTests;
    friend class FancyZonesUnitTests::FancyZonesIFancyZonesCallbackUnitTests;
    friend class FancyZonesUnitTests::ZoneWindowUnitTests;
    friend class FancyZonesUnitTests::ZoneWindowCreationUnitTests;
    friend class FancyZonesUnitTests::ZoneSetCalculateZonesUnitTests;

    inline void SetDeviceInfo(const FancyZonesDataTypes::DeviceIdData& deviceId, FancyZonesDataTypes::DeviceInfoData data)
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
        zonesSettingsFileName = result + L"\\" + std::wstring(L"zones-settings.json");
        appZoneHistoryFileName = result + L"\\" + std::wstring(L"app-zone-history.json");
    }
#endif
    void ParseDeviceInfoFromTmpFile(std::wstring_view tmpFilePath);
    void ParseCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath);
    void ParseDeletedCustomZoneSetsFromTmpFile(std::wstring_view tmpFilePath);

    void RemoveDesktopAppZoneHistory(const GUID& desktopId);

    // Maps app path to app's zone history data
    std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>> appZoneHistoryMap{};
    // Maps device unique ID to device data
    std::unordered_map<FancyZonesDataTypes::DeviceIdData, FancyZonesDataTypes::DeviceInfoData> deviceInfoMap{};
    // Maps custom zoneset UUID to it's data
    std::unordered_map<std::wstring, FancyZonesDataTypes::CustomZoneSetData> customZoneSetsMap{};

    std::wstring zonesSettingsFileName;
    std::wstring appZoneHistoryFileName;

    std::wstring activeZoneSetTmpFileName;
    std::wstring appliedZoneSetTmpFileName;
    std::wstring deletedCustomZoneSetsTmpFileName;

    mutable std::recursive_mutex dataLock;
};

FancyZonesData& FancyZonesDataInstance();

namespace DefaultValues
{
    const int ZoneCount = 3;
    const bool ShowSpacing = true;
    const int Spacing = 16;
    const int SensitivityRadius = 20;
}
