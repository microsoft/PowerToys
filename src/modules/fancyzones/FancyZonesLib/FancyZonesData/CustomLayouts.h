#pragma once

#include <guiddef.h>
#include <map>
#include <memory>
#include <optional>

#include <FancyZonesLib/FancyZonesData/LayoutData.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/GuidUtils.h>
#include <FancyZonesLib/ModuleConstants.h>

#include <common/SettingsAPI/FileWatcher.h>
#include <common/SettingsAPI/settings_helpers.h>

namespace NonLocalizable
{
    namespace CustomLayoutsIds
    {
        const static wchar_t* CustomLayoutsArrayID = L"custom-layouts";
        const static wchar_t* UuidID = L"uuid";
        const static wchar_t* NameID = L"name";
        const static wchar_t* InfoID = L"info";
        const static wchar_t* TypeID = L"type";
        const static wchar_t* CanvasID = L"canvas";
        const static wchar_t* GridID = L"grid";
        const static wchar_t* SensitivityRadiusID = L"sensitivity-radius";

        // canvas
        const static wchar_t* RefHeightID = L"ref-height";
        const static wchar_t* RefWidthID = L"ref-width";
        const static wchar_t* ZonesID = L"zones";
        const static wchar_t* XID = L"X";
        const static wchar_t* YID = L"Y";
        const static wchar_t* WidthID = L"width";
        const static wchar_t* HeightID = L"height";

        // grid
        const static wchar_t* RowsID = L"rows";
        const static wchar_t* ColumnsID = L"columns";
        const static wchar_t* RowsPercentageID = L"rows-percentage";
        const static wchar_t* ColumnsPercentageID = L"columns-percentage";
        const static wchar_t* CellChildMapID = L"cell-child-map";
        const static wchar_t* ShowSpacingID = L"show-spacing";
        const static wchar_t* SpacingID = L"spacing";
    }
}

class CustomLayouts
{
public:
    using TCustomLayoutMap = std::unordered_map<GUID, FancyZonesDataTypes::CustomLayoutData>;

    static CustomLayouts& instance();

    inline static std::wstring CustomLayoutsFileName()
    {
        std::wstring saveFolderPath = PTSettingsHelper::get_module_save_folder_location(NonLocalizable::ModuleKey);
#if defined(UNIT_TESTS)
        return saveFolderPath + L"\\test-custom-layouts.json";
#else
        return saveFolderPath + L"\\custom-layouts.json";
#endif
    }

    void LoadData();

    std::optional<LayoutData> GetLayout(const GUID& id) const noexcept;
    std::optional<FancyZonesDataTypes::CustomLayoutData> GetCustomLayoutData(const GUID& id) const noexcept;
    const TCustomLayoutMap& GetAllLayouts() const noexcept;

private:
    CustomLayouts();
    ~CustomLayouts() = default;

    TCustomLayoutMap m_layouts;
    std::unique_ptr<FileWatcher> m_fileWatcher;
};
