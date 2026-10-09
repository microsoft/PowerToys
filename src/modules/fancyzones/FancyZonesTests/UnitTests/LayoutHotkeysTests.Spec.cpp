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
    TEST_CLASS (LayoutHotkeysUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";
        std::wstring m_testFolderPath = PTSettingsHelper::get_module_save_folder_location(m_testFolder);

        TEST_METHOD_INITIALIZE(Init)
        {
            m_fzData.SetSettingsModulePath(m_testFolder);

            std::filesystem::remove(LayoutHotkeys::LayoutHotkeysFileName());
            LayoutHotkeys::instance().LoadData(); // reset
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

        TEST_METHOD (LayoutHotkeysParse)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray keysArray{};

            {
                json::JsonObject keyJson{};
                keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutUuidID, json::value(L"{33A2B101-06E0-437B-A61E-CDBECF502906}"));
                keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::KeyID, json::value(1));

                keysArray.Append(keyJson);
            }
            {
                json::JsonObject keyJson{};
                keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutUuidID, json::value(L"{33A2B101-06E0-437B-A61E-CDBECF502907}"));
                keyJson.SetNamedValue(NonLocalizable::LayoutHotkeysIds::KeyID, json::value(2));

                keysArray.Append(keyJson);
            }

            root.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutHotkeysArrayID, keysArray);
            json::to_file(LayoutHotkeys::LayoutHotkeysFileName(), root);

            // test
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)2, LayoutHotkeys::instance().GetHotkeysCount());
            Assert::AreEqual(L"{33A2B101-06E0-437B-A61E-CDBECF502906}", FancyZonesUtils::GuidToString(LayoutHotkeys::instance().GetLayoutId(1).value()).value().c_str());
            Assert::AreEqual(L"{33A2B101-06E0-437B-A61E-CDBECF502907}", FancyZonesUtils::GuidToString(LayoutHotkeys::instance().GetLayoutId(2).value()).value().c_str());
        }

        TEST_METHOD (LayoutHotkeysParseEmpty)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray keysArray{};
            root.SetNamedValue(NonLocalizable::LayoutHotkeysIds::LayoutHotkeysArrayID, keysArray);
            json::to_file(LayoutHotkeys::LayoutHotkeysFileName(), root);

            // test
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)0, LayoutHotkeys::instance().GetHotkeysCount());
        }

        TEST_METHOD (LayoutHotkeysNoFile)
        {
            // test
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)0, LayoutHotkeys::instance().GetHotkeysCount());
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettings)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            json::JsonObject layoutKeyObj{};
            layoutKeyObj.SetNamedValue(L"uuid", json::value(L"{BF7DD882-AB90-4AB8-88A0-96CCFCEC538C}"));
            layoutKeyObj.SetNamedValue(L"key", json::value(1));
            quickLayoutKeysArray.Append(layoutKeyObj);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)1, LayoutHotkeys::instance().GetHotkeysCount());
            Assert::AreEqual(L"{BF7DD882-AB90-4AB8-88A0-96CCFCEC538C}", FancyZonesUtils::GuidToString(LayoutHotkeys::instance().GetLayoutId(1).value()).value().c_str());
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettingsNoQuickLayoutKeys)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, templateLayoutsArray{};
            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)0, LayoutHotkeys::instance().GetHotkeysCount());
        }

        TEST_METHOD (MoveLayoutHotkeysFromZonesSettingsNoFile)
        {
            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            LayoutHotkeys::instance().LoadData();
            Assert::AreEqual((size_t)0, LayoutHotkeys::instance().GetHotkeysCount());
        }
    };
}