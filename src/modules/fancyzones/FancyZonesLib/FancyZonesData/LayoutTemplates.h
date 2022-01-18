#pragma once

#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace LayoutTemplatesIds
    {
        const static wchar_t* LayoutTemplatesArrayID = L"layout-templates";
    }
}

class LayoutTemplates
{
public:
    inline static std::wstring LayoutTemplatesFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-layout-templates.json";
#endif
        return saveFolderPath + L"\\layout-templates.json";
    }
};