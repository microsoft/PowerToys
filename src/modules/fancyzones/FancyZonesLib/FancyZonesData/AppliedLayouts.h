#pragma once

#include <map>
#include <memory>
#include <optional>

#include <FancyZonesLib/FancyZonesData/Layout.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace AppliedLayoutsIds
    {
        const static wchar_t* AppliedLayoutsArrayID = L"applied-layouts";
        const static wchar_t* DeviceIdID = L"device-id";
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
    using TAppliedLayoutsMap = std::unordered_map<FancyZonesDataTypes::DeviceIdData, Layout>;
    
    static AppliedLayouts& instance();

    inline static std::wstring AppliedLayoutsFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-applied-layouts.json";
#endif
        return saveFolderPath + L"\\applied-layouts.json";
    }

    void LoadData();
    void SaveData();

    void SetVirtualDesktopCheckCallback(std::function<bool(GUID)> callback);
    void SyncVirtualDesktops(GUID currentVirtualDesktopId);
    void RemoveDeletedVirtualDesktops(const std::vector<GUID>& activeDesktops);

    std::optional<Layout> GetDeviceLayout(const FancyZonesDataTypes::DeviceIdData& id) const noexcept;
    const TAppliedLayoutsMap& GetAppliedLayoutMap() const noexcept;

    bool IsLayoutApplied(const FancyZonesDataTypes::DeviceIdData& id) const noexcept;

    bool ApplyLayout(const FancyZonesDataTypes::DeviceIdData& deviceId, const FancyZonesDataTypes::ZoneSetData& layout);
    bool ApplyDefaultLayout(const FancyZonesDataTypes::DeviceIdData& deviceId);
    bool CloneLayout(const FancyZonesDataTypes::DeviceIdData& srcId, const FancyZonesDataTypes::DeviceIdData& dstId);

private:
    AppliedLayouts();
    ~AppliedLayouts() = default;

    std::unique_ptr<FileWatcher> m_fileWatcher;
    TAppliedLayoutsMap m_layouts;
    std::function<bool(GUID)> m_virtualDesktopCheckCallback;
};
