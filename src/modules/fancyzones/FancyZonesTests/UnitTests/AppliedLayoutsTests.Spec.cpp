#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/util.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (AppliedLayoutsUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";

        TEST_METHOD_INITIALIZE(Init)
        {
            m_fzData.SetSettingsModulePath(L"FancyZonesUnitTests");
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove_all(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove_all(PTSettingsHelper::get_module_save_folder_location(m_testFolder));
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

            FancyZonesDataTypes::DeviceIdData id{
                .deviceName = L"DELA026#5&10a58c63&0&UID16777488",
                .width = 2194,
                .height = 1234,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(id).has_value());
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

            FancyZonesDataTypes::DeviceIdData id{
                .deviceName = L"VSC9636#5&37ac4db&0&UID160005",
                .width = 3840,
                .height = 2160,
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
            FancyZonesDataTypes::DeviceIdData deviceSrc{
                .deviceName = L"Device1",
                .width = 200,
                .height = 100,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::DeviceIdData deviceDst{
                .deviceName = L"Device2",
                .width = 300,
                .height = 400,
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
            FancyZonesDataTypes::DeviceIdData deviceSrc{
                .deviceName = L"Device1",
                .width = 200,
                .height = 100,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::DeviceIdData deviceDst{
                .deviceName = L"Device2",
                .width = 300,
                .height = 400,
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
            FancyZonesDataTypes::DeviceIdData deviceSrc{
                .deviceName = L"Device1",
                .width = 200,
                .height = 100,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };
            FancyZonesDataTypes::DeviceIdData deviceDst{
                .deviceName = L"Device2",
                .width = 300,
                .height = 400,
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
            FancyZonesDataTypes::DeviceIdData deviceSrc{
                .deviceName = L"Device1",
                .width = 200,
                .height = 100,
                .virtualDesktopId = GUID_NULL
            };
            FancyZonesDataTypes::DeviceIdData deviceDst{
                .deviceName = L"Device2",
                .width = 300,
                .height = 400,
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
            FancyZonesDataTypes::DeviceIdData deviceId {
                .deviceName = L"DELA026#5&10a58c63&0&UID16777488",
                .width = 2194,
                .height = 1234,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };

            // test
            FancyZonesDataTypes::ZoneSetData expectedZoneSetData {
                .uuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}",
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus
            };

            AppliedLayouts::instance().ApplyLayout(deviceId, expectedZoneSetData);

            Assert::IsFalse(AppliedLayouts::instance().GetAppliedLayoutMap().empty());
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(deviceId).has_value());
        }

        TEST_METHOD (ApplyLayoutReplace)
        {
            // prepare
            FancyZonesDataTypes::DeviceIdData deviceId{
                .deviceName = L"DELA026#5&10a58c63&0&UID16777488",
                .width = 2194,
                .height = 1234,
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
            FancyZonesDataTypes::ZoneSetData expectedZoneSetData {
                .uuid = L"{33A2B101-06E0-437B-A61E-CDBECF502906}",
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus
            };

            AppliedLayouts::instance().ApplyLayout(deviceId, expectedZoneSetData);

            Assert::AreEqual((size_t)1, AppliedLayouts::instance().GetAppliedLayoutMap().size());
            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(deviceId).has_value());

            auto actual = AppliedLayouts::instance().GetAppliedLayoutMap().find(deviceId)->second;
            Assert::AreEqual(expectedZoneSetData.uuid.c_str(), FancyZonesUtils::GuidToString(actual.uuid).value().c_str());
            Assert::IsTrue(expectedZoneSetData.type == actual.type);
        }

        TEST_METHOD (ApplyDefaultLayout)
        {
            FancyZonesDataTypes::DeviceIdData expected{
                .deviceName = L"Device",
                .width = 200,
                .height = 100,
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{00000000-0000-0000-0000-000000000000}").value()
            };

            auto result = AppliedLayouts::instance().ApplyDefaultLayout(expected);
            Assert::IsTrue(result);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();

            Assert::IsFalse(actualMap.find(expected) == actualMap.end());
        }

        TEST_METHOD (ApplyDefaultLayoutWithNullVirtualDesktopId)
        {
            FancyZonesDataTypes::DeviceIdData expected{
                .deviceName = L"Device",
                .width = 200,
                .height = 100,
                .virtualDesktopId = GUID_NULL
            };

            auto result = AppliedLayouts::instance().ApplyDefaultLayout(expected);
            Assert::IsTrue(result);

            auto actualMap = AppliedLayouts::instance().GetAppliedLayoutMap();

            Assert::IsFalse(actualMap.find(expected) == actualMap.end());
        }
    };
}