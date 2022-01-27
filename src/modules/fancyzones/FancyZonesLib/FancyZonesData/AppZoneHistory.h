#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/settings_helpers.h>

class AppZoneHistory
{
public:
    using TAppZoneHistoryMap = std::unordered_map<std::wstring, std::vector<FancyZonesDataTypes::AppZoneHistoryData>>;

    static AppZoneHistory& instance();

    inline static std::wstring AppZoneHistoryFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-app-zone-history.json";
#endif
        return saveFolderPath + L"\\app-zone-history.json";
    }

    void LoadData();
    void SaveData();

    bool SetAppLastZones(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring& zoneSetId, const ZoneIndexSet& zoneIndexSet);
    bool RemoveAppLastZone(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId);

    void RemoveApp(const std::wstring& appPath);

    const TAppZoneHistoryMap& GetFullAppZoneHistory() const noexcept;
    std::optional<FancyZonesDataTypes::AppZoneHistoryData> GetZoneHistory(const std::wstring& appPath, const FancyZonesDataTypes::DeviceIdData& deviceId) const noexcept;

    bool IsAnotherWindowOfApplicationInstanceZoned(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId) const noexcept;
    void UpdateProcessIdToHandleMap(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId);
    ZoneIndexSet GetAppLastZoneIndexSet(HWND window, const FancyZonesDataTypes::DeviceIdData& deviceId, const std::wstring_view& zoneSetId) const;

    void SetVirtualDesktopCheckCallback(std::function<bool(GUID)> callback);
    void SyncVirtualDesktops(GUID currentVirtualDesktopId);
    void RemoveDeletedVirtualDesktops(const std::vector<GUID>& activeDesktops);

private:
    AppZoneHistory();
    ~AppZoneHistory() = default;

    TAppZoneHistoryMap m_history;
    std::function<bool(GUID)> m_virtualDesktopCheckCallback;
};
