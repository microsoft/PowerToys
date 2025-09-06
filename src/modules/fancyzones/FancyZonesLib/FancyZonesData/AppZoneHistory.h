#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace AppZoneHistoryIds
    {
        const static wchar_t* AppZoneHistoryID = L"app-zone-history";
        const static wchar_t* AppPathID = L"app-path";
        const static wchar_t* HistoryID = L"history";
        const static wchar_t* LayoutIndexesID = L"zone-index-set";
        const static wchar_t* LayoutIdID = L"zoneset-uuid";
        const static wchar_t* DeviceIdID = L"device-id";
        const static wchar_t* DeviceID = L"device";
        const static wchar_t* MonitorID = L"monitor";
        const static wchar_t* MonitorInstanceID = L"monitor-instance";
        const static wchar_t* MonitorSerialNumberID = L"serial-number";
        const static wchar_t* MonitorNumberID = L"monitor-number";
        const static wchar_t* VirtualDesktopID = L"virtual-desktop";
    }
}

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
#else
        return saveFolderPath + L"\\app-zone-history.json";
#endif
    }

#if defined(UNIT_TESTS)
    inline void SetAppZoneHistory(const TAppZoneHistoryMap& history)
    {
        m_history = history;
    }
#endif

    void LoadData();
    void SaveData();
    void AdjustWorkAreaIds(const std::vector<FancyZonesDataTypes::MonitorId>& ids);

    static std::wstring GetProcessPathWithAUMID(HWND window) noexcept;
    bool SetAppLastZones(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId, const ZoneIndexSet& zoneIndexSet);
    bool RemoveAppLastZone(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId);

    void RemoveApp(const std::wstring& appPath);

    const TAppZoneHistoryMap& GetFullAppZoneHistory() const noexcept;
    std::optional<FancyZonesDataTypes::AppZoneHistoryData> GetZoneHistory(const std::wstring& appPath, const FancyZonesDataTypes::WorkAreaId& workAreaId) const noexcept;

    bool IsAnotherWindowOfApplicationInstanceZoned(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId) const noexcept;
    ZoneIndexSet GetAppLastZoneIndexSet(HWND window, const FancyZonesDataTypes::WorkAreaId& workAreaId, const GUID& layoutId) const;

    void SyncVirtualDesktops(const GUID& currentVirtualDesktop, const GUID& lastUsedVirtualDesktop, std::optional<std::vector<GUID>> desktops);
    
private:
    AppZoneHistory();
    ~AppZoneHistory() = default;

    TAppZoneHistoryMap m_history;
};
