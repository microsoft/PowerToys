#pragma once

#include <map>
#include <memory>
#include <optional>

#include <FancyZonesLib/FancyZonesData/LayoutData.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace AppliedLayoutsIds
    {
        const static wchar_t* AppliedLayoutsArrayID = L"applied-layouts";
        const static wchar_t* DeviceIdID = L"device-id";
        const static wchar_t* DeviceID = L"device";
        const static wchar_t* MonitorID = L"monitor";
        const static wchar_t* MonitorInstanceID = L"monitor-instance";
        const static wchar_t* MonitorSerialNumberID = L"serial-number";
        const static wchar_t* MonitorNumberID = L"monitor-number";
        const static wchar_t* VirtualDesktopID = L"virtual-desktop";
        const static wchar_t* AppliedLayoutID = L"applied-layout";
        const static wchar_t* UuidID = L"uuid";
        const static wchar_t* TypeID = L"type";
        const static wchar_t* ShowSpacingID = L"show-spacing";
        const static wchar_t* SpacingID = L"spacing";
        const static wchar_t* ZoneCountID = L"zone-count";
        const static wchar_t* SensitivityRadiusID = L"sensitivity-radius";
    }
}

class AppliedLayouts
{
public:
    using TAppliedLayoutsMap = std::unordered_map<FancyZonesDataTypes::WorkAreaId, LayoutData>;

    static AppliedLayouts& instance();

    inline static std::wstring AppliedLayoutsFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-applied-layouts.json";
#else
        return saveFolderPath + L"\\applied-layouts.json";
#endif
    }

    void LoadData();
    void SaveData();
    void AdjustWorkAreaIds(const std::vector<FancyZonesDataTypes::MonitorId>& ids);

    void SyncVirtualDesktops();
    void RemoveDeletedVirtualDesktops(const std::vector<GUID>& activeDesktops);

    std::optional<LayoutData> GetDeviceLayout(const FancyZonesDataTypes::WorkAreaId& id) const noexcept;
    const TAppliedLayoutsMap& GetAppliedLayoutMap() const noexcept;

    bool IsLayoutApplied(const FancyZonesDataTypes::WorkAreaId& id) const noexcept;

    bool ApplyLayout(const FancyZonesDataTypes::WorkAreaId& deviceId, LayoutData layout);
    bool ApplyDefaultLayout(const FancyZonesDataTypes::WorkAreaId& deviceId);
    bool CloneLayout(const FancyZonesDataTypes::WorkAreaId& srcId, const FancyZonesDataTypes::WorkAreaId& dstId);

private:
    AppliedLayouts();
    ~AppliedLayouts() = default;

    std::unique_ptr<FileWatcher> m_fileWatcher;
    TAppliedLayoutsMap m_layouts;
};
