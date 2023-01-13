#pragma once

#include <FancyZonesLib/FancyZonesData/LayoutData.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace LayoutTemplatesIds
    {
        const static wchar_t* LayoutTemplatesArrayID = L"layout-templates";
        const static wchar_t* TypeID = L"type";
        const static wchar_t* ShowSpacingID = L"show-spacing";
        const static wchar_t* SpacingID = L"spacing";
        const static wchar_t* ZoneCountID = L"zone-count";
        const static wchar_t* SensitivityRadiusID = L"sensitivity-radius";
    }
}

class LayoutTemplates
{
public:
    static LayoutTemplates& instance();

    inline static std::wstring LayoutTemplatesFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-layout-templates.json";
#else
        return saveFolderPath + L"\\layout-templates.json";
#endif
    }

    void LoadData();

    std::optional<LayoutData> GetLayout(FancyZonesDataTypes::ZoneSetLayoutType type) const noexcept;

private:
    LayoutTemplates();
    ~LayoutTemplates() = default;

    std::unique_ptr<FileWatcher> m_fileWatcher;
    std::vector<LayoutData> m_layouts;
};