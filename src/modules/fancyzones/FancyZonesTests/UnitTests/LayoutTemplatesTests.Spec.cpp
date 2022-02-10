#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesData/LayoutTemplates.h>

#include "util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (LayoutTemplatesUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";

        TEST_METHOD_INITIALIZE(Init)
        {
            m_fzData.SetSettingsModulePath(m_testFolder);
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove_all(LayoutTemplates::LayoutTemplatesFileName());
            std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_testFolder));
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettings)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            
            {
                json::JsonObject layout{};
                layout.SetNamedValue(L"type", json::value(L"blank"));
                layout.SetNamedValue(L"show-spacing", json::value(false));
                layout.SetNamedValue(L"spacing", json::value(0));
                layout.SetNamedValue(L"zone-count", json::value(0));
                layout.SetNamedValue(L"sensitivity-radius", json::value(0));
                templateLayoutsArray.Append(layout);
            }
            {
                json::JsonObject layout{};
                layout.SetNamedValue(L"type", json::value(L"grid"));
                layout.SetNamedValue(L"show-spacing", json::value(true));
                layout.SetNamedValue(L"spacing", json::value(-10));
                layout.SetNamedValue(L"zone-count", json::value(4));
                layout.SetNamedValue(L"sensitivity-radius", json::value(30));
                templateLayoutsArray.Append(layout);
            }

            root.SetNamedValue(L"templates", templateLayoutsArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            
            auto result = json::from_file(LayoutTemplates::LayoutTemplatesFileName());
            Assert::IsTrue(result.has_value());
            Assert::IsTrue(result.value().HasKey(NonLocalizable::LayoutTemplatesIds::LayoutTemplatesArrayID));
            auto res = CustomAssert::CompareJsonArrays(templateLayoutsArray, result.value().GetNamedArray(NonLocalizable::LayoutTemplatesIds::LayoutTemplatesArrayID));
            Assert::IsTrue(res.first, res.second.c_str());
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettingsNoTemplates)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, quickLayoutKeysArray{};
            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            auto result = json::from_file(LayoutTemplates::LayoutTemplatesFileName());
            Assert::IsFalse(result.has_value());
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettingsNoFile)
        {
            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            auto result = json::from_file(LayoutTemplates::LayoutTemplatesFileName());
            Assert::IsFalse(result.has_value());
        }
    };
}