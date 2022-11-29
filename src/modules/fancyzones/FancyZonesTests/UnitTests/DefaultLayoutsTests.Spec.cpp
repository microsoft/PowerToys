#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData/DefaultLayouts.h>
#include <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/util.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (DefaultLayoutsUnitTests)
    {
        std::wstring m_testFolder = L"FancyZonesUnitTests";
        std::wstring m_testFolderPath = PTSettingsHelper::get_module_save_folder_location(m_testFolder);

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(DefaultLayouts::DefaultLayoutsFileName());
            std::filesystem::remove_all(m_testFolderPath);
        }

        TEST_METHOD (DefaultLayoutsParse)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            // custom, horizontal
            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::TypeID, json::value(L"custom"));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::DefaultLayoutsIds::MonitorConfigurationTypeID, json::value(L"horizontal"));
                obj.SetNamedValue(NonLocalizable::DefaultLayoutsIds::LayoutID, layout);
                layoutsArray.Append(obj);
            }

            // template, vertical
            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::TypeID, json::value(L"grid"));
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SpacingID, json::value(1));
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SensitivityRadiusID, json::value(30));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::DefaultLayoutsIds::MonitorConfigurationTypeID, json::value(L"vertical"));
                obj.SetNamedValue(NonLocalizable::DefaultLayoutsIds::LayoutID, layout);
                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::DefaultLayoutsIds::DefaultLayoutsArrayID, layoutsArray);
            json::to_file(DefaultLayouts::DefaultLayoutsFileName(), root);

            // test
            DefaultLayouts::instance().LoadData();
            
            LayoutData horizontal{
                .uuid = FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Custom
            };
            Assert::IsTrue(horizontal == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Horizontal));

            LayoutData vertical{
                .uuid = GUID_NULL,
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Grid,
                .showSpacing = true,
                .spacing = 1,
                .zoneCount = 4,
                .sensitivityRadius = 30
            };
            Assert::IsTrue(vertical == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Vertical));
        }

            TEST_METHOD (DefaultLayoutsParseEmpty)
            {
                // prepare
                json::JsonObject root{};
                json::JsonArray layoutsArray{};
                root.SetNamedValue(NonLocalizable::DefaultLayoutsIds::DefaultLayoutsArrayID, layoutsArray);
                json::to_file(DefaultLayouts::DefaultLayoutsFileName(), root);

                // test
                DefaultLayouts::instance().LoadData();

                LayoutData priorityGrid{
                    .uuid = GUID_NULL,
                    .type = FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid,
                    .showSpacing = DefaultValues::ShowSpacing,
                    .spacing = DefaultValues::Spacing,
                    .zoneCount = DefaultValues::ZoneCount,
                    .sensitivityRadius = DefaultValues::SensitivityRadius
                };

                Assert::IsTrue(priorityGrid == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Horizontal));
                Assert::IsTrue(priorityGrid == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Vertical));
            }

            TEST_METHOD (DefaultLayoutsNoFile)
            {
                // test
                DefaultLayouts::instance().LoadData();

                LayoutData priorityGrid{
                    .uuid = GUID_NULL,
                    .type = FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid,
                    .showSpacing = DefaultValues::ShowSpacing,
                    .spacing = DefaultValues::Spacing,
                    .zoneCount = DefaultValues::ZoneCount,
                    .sensitivityRadius = DefaultValues::SensitivityRadius
                };

                Assert::IsTrue(priorityGrid == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Horizontal));
                Assert::IsTrue(priorityGrid == DefaultLayouts::instance().GetDefaultLayout(MonitorConfigurationType::Vertical));
            }
    };
}