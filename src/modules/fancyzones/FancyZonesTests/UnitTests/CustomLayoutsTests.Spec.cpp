#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/CustomLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutHotkeys.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>
#include <FancyZonesLib/util.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (CustomLayoutsUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";
        std::wstring m_testFolderPath = PTSettingsHelper::get_module_save_folder_location(m_testFolder);

        json::JsonObject CanvasLayoutJson()
        {
            json::JsonObject canvasLayoutJson{};
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::NameID, json::value(L"Custom canvas layout"));
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::TypeID, json::value(NonLocalizable::CustomLayoutsIds::CanvasID));

            json::JsonObject info{};
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RefWidthID, json::value(1920));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RefHeightID, json::value(1080));

            json::JsonArray zonesArray{};
            {
                json::JsonObject zone{};
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::XAxisID, json::value(0));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::YAxisID, json::value(0));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::WidthID, json::value(1140));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::HeightID, json::value(1040));
                zonesArray.Append(zone);
            }
            {
                json::JsonObject zone{};
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::XAxisID, json::value(1140));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::YAxisID, json::value(649));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::WidthID, json::value(780));
                zone.SetNamedValue(NonLocalizable::CustomLayoutsIds::HeightID, json::value(391));
                zonesArray.Append(zone);
            }

            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::ZonesID, zonesArray);
            canvasLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::InfoID, info);
            return canvasLayoutJson;
        }

        json::JsonObject GridLayoutJson()
        {
            json::JsonObject gridLayoutJson{};
            gridLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17D}"));
            gridLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::NameID, json::value(L"Custom grid layout"));
            gridLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::TypeID, json::value(NonLocalizable::CustomLayoutsIds::GridID));

            json::JsonArray rowsPercentage{};
            rowsPercentage.Append(json::value(5000));
            rowsPercentage.Append(json::value(5000));

            json::JsonArray columnsPercentage{};
            columnsPercentage.Append(json::value(3333));
            columnsPercentage.Append(json::value(5000));
            columnsPercentage.Append(json::value(1667));

            json::JsonArray cells{};
            {
                json::JsonArray cellsRow{};
                cellsRow.Append(json::value(0));
                cellsRow.Append(json::value(1));
                cellsRow.Append(json::value(2));
                cells.Append(cellsRow);
            }
            {
                json::JsonArray cellsRow{};
                cellsRow.Append(json::value(3));
                cellsRow.Append(json::value(1));
                cellsRow.Append(json::value(4));
                cells.Append(cellsRow);
            }

            json::JsonObject info{};
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RowsID, json::value(2));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::ColumnsID, json::value(3));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::RowsPercentageID, rowsPercentage);
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::ColumnsPercentageID, columnsPercentage);
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::CellChildMapID, cells);
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::SensitivityRadiusID, json::value(20));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::ShowSpacingID, json::value(false));
            info.SetNamedValue(NonLocalizable::CustomLayoutsIds::SpacingID, json::value(16));

            gridLayoutJson.SetNamedValue(NonLocalizable::CustomLayoutsIds::InfoID, info);
            return gridLayoutJson;
        }
        
        TEST_METHOD_INITIALIZE(Init)
        {
            m_fzData.SetSettingsModulePath(L"FancyZonesUnitTests");
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            // Move...FromZonesSettings creates all of these files, clean up
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(CustomLayouts::CustomLayoutsFileName());
            std::filesystem::remove(LayoutHotkeys::LayoutHotkeysFileName());
            std::filesystem::remove(LayoutTemplates::LayoutTemplatesFileName());
            std::filesystem::remove_all(m_testFolderPath);
        }

        TEST_METHOD (CustomLayoutsParse)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            layoutsArray.Append(CanvasLayoutJson());
            layoutsArray.Append(GridLayoutJson());

            root.SetNamedValue(NonLocalizable::CustomLayoutsIds::CustomLayoutsArrayID, layoutsArray);
            json::to_file(CustomLayouts::CustomLayoutsFileName(), root);

            // test
            CustomLayouts::instance().LoadData();
            Assert::AreEqual((size_t)2, CustomLayouts::instance().GetAllLayouts().size());
            Assert::IsTrue(CustomLayouts::instance().GetLayout(FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}").value()).has_value());
            Assert::IsTrue(CustomLayouts::instance().GetLayout(FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17D}").value()).has_value());
        }

        TEST_METHOD (CustomLayoutsParseEmpty)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};
            root.SetNamedValue(NonLocalizable::CustomLayoutsIds::CustomLayoutsArrayID, layoutsArray);
            json::to_file(CustomLayouts::CustomLayoutsFileName(), root);

            // test
            CustomLayouts::instance().LoadData();
            Assert::IsTrue(CustomLayouts::instance().GetAllLayouts().empty());
        }

        TEST_METHOD (CustomsLayoutsNoFile)
        {
            // test
            CustomLayouts::instance().LoadData();
            Assert::IsTrue(CustomLayouts::instance().GetAllLayouts().empty());
        }

        TEST_METHOD (MoveCustomLayoutsFromZonesSettings)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            customLayoutsArray.Append(GridLayoutJson());
            customLayoutsArray.Append(CanvasLayoutJson());

            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            CustomLayouts::instance().LoadData();
            Assert::AreEqual((size_t)2, CustomLayouts::instance().GetAllLayouts().size());
            Assert::IsTrue(CustomLayouts::instance().GetLayout(FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}").value()).has_value());
            Assert::IsTrue(CustomLayouts::instance().GetLayout(FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17D}").value()).has_value());
        }

        TEST_METHOD (MoveCustomLayoutsFromZonesSettingsNoCustomLayoutsData)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            CustomLayouts::instance().LoadData();
            Assert::IsTrue(CustomLayouts::instance().GetAllLayouts().empty());
        }

        TEST_METHOD (MoveCustomLayoutsFromZonesSettingsNoFile)
        {
            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            CustomLayouts::instance().LoadData();
            Assert::IsTrue(CustomLayouts::instance().GetAllLayouts().empty());
        }
    };
}