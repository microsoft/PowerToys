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
    TEST_CLASS (AppliedLayoutsUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";
        std::wstring m_testFolderPath = PTSettingsHelper::get_module_save_folder_location(m_testFolder);

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
            AppliedLayouts::instance().LoadData(); // clean data 
        }

        TEST_METHOD (AppliedLayoutsParse)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject device{};
                device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(L"DELA026#5&10a58c63&0&UID16777488"));
                device.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                
                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, device);
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            // test
            AppliedLayouts::instance().LoadData();
            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());

            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
            Assert::IsTrue(AppliedLayouts::instance().IsLayoutApplied(id));
        }

        TEST_METHOD(AppliedLayoutsParseDataWithResolution)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            // test
            AppliedLayouts::instance().LoadData();
            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());

            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
            Assert::IsTrue(AppliedLayouts::instance().IsLayoutApplied(id));
        }

        TEST_METHOD (AppliedLayoutsParseDataWithResolution2)
        {
            // same monitor names and virtual desktop ids, but different resolution
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(16));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_1920_1080_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            // test
            AppliedLayouts::instance().LoadData();
            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());

            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
            Assert::IsTrue(AppliedLayouts::instance().IsLayoutApplied(id));
        }

        TEST_METHOD (AppliedLayoutsParseDataWithResolution3)
        {
            // same monitor names and virtual desktop ids, but different resolution
            // non-default layouts applied

            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD178}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Columns)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            // test
            AppliedLayouts::instance().LoadData();
            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());

            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
            Assert::IsTrue(AppliedLayouts::instance().IsLayoutApplied(id));
        }

        TEST_METHOD (AppliedLayoutsParseEmpty)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};
            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            // test
            AppliedLayouts::instance().LoadData();
            Assert::IsTrue(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
        }

        TEST_METHOD (AppliedLayoutsNoFile)
        {
            // test
            AppliedLayouts::instance().LoadData();
            Assert::IsTrue(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
        }

        TEST_METHOD (MoveAppliedLayoutsFromZonesSettings)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray devicesArray{}, customLayoutsArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            
            {
                json::JsonObject activeZoneset{};
                activeZoneset.SetNamedValue(L"uuid", json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                activeZoneset.SetNamedValue(L"type", json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));

                json::JsonObject obj{};
                obj.SetNamedValue(L"device-id", json::value(L"VSC9636#5&37ac4db&0&UID160005_3840_2160_{00000000-0000-0000-0000-000000000000}"));
                obj.SetNamedValue(L"active-zoneset", activeZoneset);;
                obj.SetNamedValue(L"editor-show-spacing", json::value(true));
                obj.SetNamedValue(L"editor-spacing", json::value(3));
                obj.SetNamedValue(L"editor-zone-count", json::value(4));
                obj.SetNamedValue(L"editor-sensitivity-radius", json::value(22));

                devicesArray.Append(obj);
            }

            root.SetNamedValue(L"devices", devicesArray);
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            AppliedLayouts::instance().LoadData();
            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());

            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"VSC9636", .instanceId = L"5&37ac4db&0&UID160005" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
        }

        TEST_METHOD (MoveAppliedLayoutsFromZonesSettingsNoAppliedLayoutsData)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray customLayoutsArray{}, templateLayoutsArray{}, quickLayoutKeysArray{};
            root.SetNamedValue(L"custom-zone-sets", customLayoutsArray);
            root.SetNamedValue(L"templates", templateLayoutsArray);
            root.SetNamedValue(L"quick-layout-keys", quickLayoutKeysArray);
            json::to_file(m_fzData.GetZoneSettingsPath(m_testFolder), root);

            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            AppliedLayouts::instance().LoadData();
            Assert::IsTrue(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
        }

        TEST_METHOD (MoveAppliedLayoutsFromZonesSettingsNoFile)
        {
            // test
            m_fzData.ReplaceZoneSettingsFileFromOlderVersions();
            AppliedLayouts::instance().LoadData();
            Assert::IsTrue(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
        }

        TEST_METHOD (CloneDeviceInfo)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceSrc));
            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceDst));

            AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();
            Assert::IsFalse(actualMap.find(deviceSrc) == actualMap.end());
            Assert::IsFalse(actualMap.find(deviceDst) == actualMap.end());

            auto expected = AppliedLayouts::instance().GetDeviceLayout(deviceSrc);
            auto actual = AppliedLayouts::instance().GetDeviceLayout(deviceDst);

            Assert::IsTrue(expected.has_value());
            Assert::IsTrue(actual.has_value());
            Assert::IsTrue(expected.value().uuid == actual.value().uuid);
        }

        TEST_METHOD (CloneDeviceInfoIntoUnknownDevice)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceSrc));

            AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();
            Assert::IsFalse(actualMap.find(deviceSrc) == actualMap.end());
            Assert::IsFalse(actualMap.find(deviceDst) == actualMap.end());

            auto expected = AppliedLayouts::instance().GetDeviceLayout(deviceSrc);
            auto actual = AppliedLayouts::instance().GetDeviceLayout(deviceDst);

            Assert::IsTrue(expected.has_value());
            Assert::IsTrue(actual.has_value());
            Assert::IsTrue(expected.value().uuid == actual.value().uuid);
        }

        TEST_METHOD (CloneDeviceInfoFromUnknownDevice)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            AppliedLayouts::instance().LoadData();
            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceDst));

            Assert::IsFalse(AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst));

            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(deviceSrc).has_value());
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(deviceDst).has_value());
        }

        TEST_METHOD (CloneDeviceInfoNullVirtualDesktopId)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = GUID_NULL
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceSrc));
            Assert::IsTrue(AppliedLayouts::instance().ApplyDefaultLayout(deviceDst));

            AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();
            Assert::IsFalse(actualMap.find(deviceSrc) == actualMap.end());
            Assert::IsFalse(actualMap.find(deviceDst) == actualMap.end());

            auto expected = AppliedLayouts::instance().GetDeviceLayout(deviceSrc);
            auto actual = AppliedLayouts::instance().GetDeviceLayout(deviceDst);

            Assert::IsTrue(expected.has_value());
            Assert::IsTrue(actual.has_value());
            Assert::IsTrue(expected.value().uuid == actual.value().uuid);
        }

        TEST_METHOD (ApplyLayout)
        {
            // prepare
            FancyZonesDataTypes::WorkAreaId deviceId {
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };

            // test
            LayoutData expectedLayout {
                .uuid = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 15,
                .sensitivityRadius = 30
            };

            AppliedLayouts::instance().ApplyLayout(deviceId, expectedLayout);

            Assert::IsFalse(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(deviceId).has_value());

            auto actual = AppliedLayouts::instance().GetAppliedLayoutMap().find(deviceId)->second;
            Assert::IsTrue(expectedLayout.type == actual.type);
            Assert::AreEqual(expectedLayout.showSpacing, actual.showSpacing);
            Assert::AreEqual(expectedLayout.spacing, actual.spacing);
            Assert::AreEqual(expectedLayout.zoneCount, actual.zoneCount);
            Assert::AreEqual(expectedLayout.sensitivityRadius, actual.sensitivityRadius);
        }

        TEST_METHOD (ApplyLayoutReplace)
        {
            // prepare
            FancyZonesDataTypes::WorkAreaId deviceId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };

            json::JsonObject root{};
            json::JsonArray layoutsArray{};
            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Rows)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(true));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(3));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(22));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceIdID, json::value(L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }
            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);
            AppliedLayouts::instance().LoadData();

            // test
            LayoutData expectedLayout{
                .uuid = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 15,
                .sensitivityRadius = 30
            };

            AppliedLayouts::instance().ApplyLayout(deviceId, expectedLayout);

            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(deviceId).has_value());

            auto actual = AppliedLayouts::instance().GetAppliedLayoutMap().find(deviceId)->second;
            Assert::AreEqual(FancyZonesUtils::GuidToString(expectedLayout.uuid).value().c_str(), FancyZonesUtils::GuidToString(actual.uuid).value().c_str());
            Assert::IsTrue(expectedLayout.type == actual.type);
            Assert::AreEqual(expectedLayout.showSpacing, actual.showSpacing);
            Assert::AreEqual(expectedLayout.spacing, actual.spacing);
            Assert::AreEqual(expectedLayout.zoneCount, actual.zoneCount);
            Assert::AreEqual(expectedLayout.sensitivityRadius, actual.sensitivityRadius);
        }

        TEST_METHOD (ApplyDefaultLayout)
        {
            FancyZonesDataTypes::WorkAreaId expected{
                .monitorId = { .deviceId = { .id = L"Device", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            auto result = AppliedLayouts::instance().ApplyDefaultLayout(expected);
            Assert::IsTrue(result);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();

            Assert::IsFalse(actualMap.find(expected) == actualMap.end());
        }

        TEST_METHOD (ApplyDefaultLayoutWithNullVirtualDesktopId)
        {
            FancyZonesDataTypes::WorkAreaId expected{
                .monitorId = { .deviceId = { .id = L"Device", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = GUID_NULL
            };

            auto result = AppliedLayouts::instance().ApplyDefaultLayout(expected);
            Assert::IsTrue(result);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();

            Assert::IsFalse(actualMap.find(expected) == actualMap.end());
        }

        TEST_METHOD (IsLayoutApplied)
        {
            // prepare
            FancyZonesDataTypes::WorkAreaId id{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            AppliedLayouts::instance().ApplyLayout(id, LayoutData{});

            // test
            Assert::IsTrue(AppliedLayouts::instance().IsLayoutApplied(id));
        }

        TEST_METHOD (IsLayoutApplied2)
        {
            // prepare
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            AppliedLayouts::instance().ApplyLayout(id1, LayoutData{});

            // test
            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-2", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{F21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };
            Assert::IsFalse(AppliedLayouts::instance().IsLayoutApplied(id2));
        }
    };
}