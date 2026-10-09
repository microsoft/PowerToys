#pragma once

#include <common/SettingsAPI/settings_helpers.h>

#include <FancyZonesLib/ModuleConstants.h>

namespace NonLocalizable
{
    namespace LastUsedVirtualDesktop
    {
        const static wchar_t* LastUsedVirtualDesktopID = L"last-used-virtual-desktop";
    }
}

class LastUsedVirtualDesktop
{
public:
    static LastUsedVirtualDesktop& instance();

    inline static std::wstring LastUsedVirtualDesktopFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-last-used-virtual-desktop.json";
#else
        return saveFolderPath + L"\\last-used-virtual-desktop.json";
#endif
    }

    void LoadData();
    void SaveData() const;

    GUID GetId() const;
    void SetId(GUID id);

private:
    LastUsedVirtualDesktop() = default;
    ~LastUsedVirtualDesktop() = default;

    GUID m_id{};
};
