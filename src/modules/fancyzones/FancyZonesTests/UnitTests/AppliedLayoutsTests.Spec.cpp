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
        TEST_METHOD_INITIALIZE(Init)
        {
            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());   
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

        TEST_METHOD (AppliedLayoutsParseDataWithResolution)
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

        TEST_METHOD (Save)
        {
            FancyZonesDataTypes::WorkAreaId workAreaId1{ 
                .monitorId = {
                    .deviceId = { .id = L"id-1", .instanceId = L"id-1", .number = 1 },
                    .serialNumber = L"serial-number-1"
                }, 
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{30387C86-BB15-476D-8683-AF93F6D73E99}").value() 
            };
            FancyZonesDataTypes::WorkAreaId workAreaId2{ 
                .monitorId = {
                    .deviceId = { .id = L"id-2", .instanceId = L"id-2", .number = 2 },
                    .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{30387C86-BB15-476D-8683-AF93F6D73E99}").value() 
            };
            FancyZonesDataTypes::WorkAreaId workAreaId3{
                .monitorId = {
                    .deviceId = { .id = L"id-1", .instanceId = L"id-1", .number = 1 },
                    .serialNumber = L"serial-number-1" },
                .virtualDesktopId = GUID_NULL
            };
            FancyZonesDataTypes::WorkAreaId workAreaId4{
                .monitorId = {
                    .deviceId = { .id = L"id-2", .instanceId = L"id-2", .number = 2 },
                    .serialNumber = L"serial-number-2" },
                .virtualDesktopId = GUID_NULL
            };

            LayoutData layout1{ .uuid = FancyZonesUtils::GuidFromString(L"{D7DBECFA-23FC-4F45-9B56-51CFA9F6ABA2}").value() };
            LayoutData layout2{ .uuid = FancyZonesUtils::GuidFromString(L"{B9EDB48C-EC48-4E82-993F-A15DC1FF09D3}").value() };
            LayoutData layout3{ .uuid = FancyZonesUtils::GuidFromString(L"{94CF0000-7814-4D72-9624-794060FA269C}").value() };
            LayoutData layout4{ .uuid = FancyZonesUtils::GuidFromString(L"{13FA7ADF-1B6C-4FB6-8142-254B77C128E2}").value() };

            AppliedLayouts::TAppliedLayoutsMap expected{};
            expected.insert({ workAreaId1, layout1 });
            expected.insert({ workAreaId2, layout2 });
            expected.insert({ workAreaId3, layout3 });
            expected.insert({ workAreaId4, layout4 });

            AppliedLayouts::instance().SetAppliedLayouts(expected);
            AppliedLayouts::instance().SaveData();

            AppliedLayouts::instance().LoadData();
            auto actual = AppliedLayouts::instance().GetAppliedLayoutMap();
            Assert::AreEqual(expected.size(), actual.size());
            Assert::IsTrue(expected.at(workAreaId1) == actual.at(workAreaId1));
            Assert::IsTrue(expected.at(workAreaId2) == actual.at(workAreaId2));
            Assert::IsTrue(expected.at(workAreaId3) == actual.at(workAreaId3));
            Assert::IsTrue(expected.at(workAreaId4) == actual.at(workAreaId4));
        }

        TEST_METHOD (CloneDeviceInfo)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{EA6B6934-D55F-49F5-A9A5-CFADE21FFFB8}").value()
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{EF1A8099-7D1E-4738-805A-571B31B02674}").value()
            };

            LayoutData layout { .uuid = FancyZonesUtils::GuidFromString(L"{361F96DD-FD10-4D01-ABAC-CC1C857294DD}").value() };
            Assert::IsTrue(AppliedLayouts::instance().ApplyLayout(deviceSrc, layout));
            
            AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst);

            Assert::IsTrue(layout == AppliedLayouts::instance().GetDeviceLayout(deviceSrc));
            Assert::IsTrue(layout == AppliedLayouts::instance().GetDeviceLayout(deviceDst));
        }

        TEST_METHOD (CloneDeviceInfoFromUnknownDevice)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{EA6B6934-D55F-49F5-A9A5-CFADE21FFFB8}").value()
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{EF1A8099-7D1E-4738-805A-571B31B02674}").value()
            };

            AppliedLayouts::instance().LoadData();

            Assert::IsFalse(AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst));

            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(deviceSrc).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(deviceDst).has_value());
        }

        TEST_METHOD (CloneDeviceInfoNullVirtualDesktopId)
        {
            FancyZonesDataTypes::WorkAreaId deviceSrc{
                .monitorId = { .deviceId = { .id = L"Device1", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = GUID_NULL
            };
            FancyZonesDataTypes::WorkAreaId deviceDst{
                .monitorId = { .deviceId = { .id = L"Device2", .instanceId = L"" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{EF1A8099-7D1E-4738-805A-571B31B02674}").value()
            };

            LayoutData layout{ .uuid = FancyZonesUtils::GuidFromString(L"{361F96DD-FD10-4D01-ABAC-CC1C857294DD}").value() };
            Assert::IsTrue(AppliedLayouts::instance().ApplyLayout(deviceSrc, layout));
            
            AppliedLayouts::instance().CloneLayout(deviceSrc, deviceDst);

            Assert::IsTrue(layout == AppliedLayouts::instance().GetDeviceLayout(deviceSrc));
            Assert::IsTrue(layout == AppliedLayouts::instance().GetDeviceLayout(deviceDst));
        }

        TEST_METHOD (ApplyLayout)
        {
            FancyZonesDataTypes::WorkAreaId workAreaId {
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };

            LayoutData expectedLayout {
                .uuid = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 15,
                .sensitivityRadius = 30
            };

            AppliedLayouts::instance().ApplyLayout(workAreaId, expectedLayout);

            Assert::IsTrue(AppliedLayouts::instance().GetDeviceLayout(workAreaId).has_value());
            Assert::IsTrue(expectedLayout == AppliedLayouts::instance().GetAppliedLayoutMap().find(workAreaId)->second);
        }

        TEST_METHOD (ApplyLayoutReplace)
        {
            // prepare
            FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value()
            };
            
            LayoutData layout{
                .uuid = FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Rows,
                .showSpacing = true,
                .spacing = 3,
                .zoneCount = 4,
                .sensitivityRadius = 22
            };

            AppliedLayouts::instance().SetAppliedLayouts({ {workAreaId, layout} });

            // test
            LayoutData expectedLayout{
                .uuid = FancyZonesUtils::GuidFromString(L"{33A2B101-06E0-437B-A61E-CDBECF502906}").value(),
                .type = FancyZonesDataTypes::ZoneSetLayoutType::Focus,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 15,
                .sensitivityRadius = 30
            };

            AppliedLayouts::instance().ApplyLayout(workAreaId, expectedLayout);
            Assert::IsTrue(expectedLayout == AppliedLayouts::instance().GetDeviceLayout(workAreaId));
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

    TEST_CLASS (AppliedLayoutsSyncVirtualDesktops)
    {
        const GUID virtualDesktop1 = FancyZonesUtils::GuidFromString(L"{30387C86-BB15-476D-8683-AF93F6D73E99}").value();
        const GUID virtualDesktop2 = FancyZonesUtils::GuidFromString(L"{65F6343A-868F-47EE-838E-55A178A7FB7A}").value();
        const GUID deletedVirtualDesktop = FancyZonesUtils::GuidFromString(L"{2D9F3E2D-F61D-4618-B35D-85C9B8DFDFD8}").value();
        
        LayoutData layout1{ .uuid = FancyZonesUtils::GuidFromString(L"{D7DBECFA-23FC-4F45-9B56-51CFA9F6ABA2}").value() };
        LayoutData layout2{ .uuid = FancyZonesUtils::GuidFromString(L"{B9EDB48C-EC48-4E82-993F-A15DC1FF09D3}").value() };
        LayoutData layout3{ .uuid = FancyZonesUtils::GuidFromString(L"{94CF0000-7814-4D72-9624-794060FA269C}").value() };
        LayoutData layout4{ .uuid = FancyZonesUtils::GuidFromString(L"{13FA7ADF-1B6C-4FB6-8142-254B77C128E2}").value() };

        FancyZonesDataTypes::WorkAreaId GetWorkAreaID(int number, GUID virtualDesktop)
        {
            return FancyZonesDataTypes::WorkAreaId{
                .monitorId = {
                    .deviceId = { 
                        .id = std::wstring(L"id-") + std::to_wstring(number), 
                        .instanceId = std::wstring(L"id-") + std::to_wstring(number), 
                        .number = number 
                    },
                    .serialNumber = std::wstring(L"serial-number-") + std::to_wstring(number) 
                },
                .virtualDesktopId = virtualDesktop
            };
        }
        
        TEST_METHOD_INITIALIZE(Init)
        {
            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
        }

        TEST_METHOD(SyncVirtualDesktops_SwitchVirtualDesktop)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, virtualDesktop1), layout1 });
            layouts.insert({ GetWorkAreaID(2, virtualDesktop1), layout2 });
            layouts.insert({ GetWorkAreaID(1, virtualDesktop2), layout3 });
            layouts.insert({ GetWorkAreaID(2, virtualDesktop2), layout4 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = virtualDesktop1;
            GUID lastUsedVirtualDesktop = virtualDesktop2;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = { { virtualDesktop1, virtualDesktop2 } };
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop1)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop1)));
            Assert::IsTrue(layout3 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop2)));
            Assert::IsTrue(layout4 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop2)));
        }

        TEST_METHOD (SyncVirtualDesktops_CurrentVirtualDesktopDeleted)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, virtualDesktop1), layout1 });
            layouts.insert({ GetWorkAreaID(2, virtualDesktop1), layout2 });
            layouts.insert({ GetWorkAreaID(1, deletedVirtualDesktop), layout3 });
            layouts.insert({ GetWorkAreaID(2, deletedVirtualDesktop), layout4 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = virtualDesktop1;
            GUID lastUsedVirtualDesktop = deletedVirtualDesktop;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = { { virtualDesktop1 } };
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop1)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop1)));
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, deletedVirtualDesktop)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, deletedVirtualDesktop)).has_value());
        }

        TEST_METHOD (SyncVirtualDesktops_NotCurrentVirtualDesktopDeleted)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, virtualDesktop1), layout1 });
            layouts.insert({ GetWorkAreaID(2, virtualDesktop1), layout2 });
            layouts.insert({ GetWorkAreaID(1, deletedVirtualDesktop), layout3 });
            layouts.insert({ GetWorkAreaID(2, deletedVirtualDesktop), layout4 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = virtualDesktop1;
            GUID lastUsedVirtualDesktop = virtualDesktop1;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = { { virtualDesktop1 } };
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop1)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop1)));
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, deletedVirtualDesktop)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, deletedVirtualDesktop)).has_value());
        }

        TEST_METHOD (SyncVirtualDesktops_AllIdsFromRegistryAreNew)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, deletedVirtualDesktop), layout1 });
            layouts.insert({ GetWorkAreaID(2, deletedVirtualDesktop), layout2 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = virtualDesktop1;
            GUID lastUsedVirtualDesktop = deletedVirtualDesktop;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = { { virtualDesktop1, virtualDesktop2 } };
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop1)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop1)));
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop2)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop2)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, deletedVirtualDesktop)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, deletedVirtualDesktop)).has_value());
        }

        TEST_METHOD (SyncVirtualDesktop_NoDesktopsInRegistry)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, deletedVirtualDesktop), layout1 });
            layouts.insert({ GetWorkAreaID(2, deletedVirtualDesktop), layout2 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = GUID_NULL;
            GUID lastUsedVirtualDesktop = deletedVirtualDesktop;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = std::nullopt;
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, GUID_NULL)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, GUID_NULL)));
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, deletedVirtualDesktop)).has_value());
            Assert::IsFalse(AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, deletedVirtualDesktop)).has_value());
        }

        TEST_METHOD(SyncVirtualDesktops_SwithVirtualDesktopFirstTime)
        {
            AppliedLayouts::TAppliedLayoutsMap layouts{};
            layouts.insert({ GetWorkAreaID(1, GUID_NULL), layout1 });
            layouts.insert({ GetWorkAreaID(2, GUID_NULL), layout2 });
            AppliedLayouts::instance().SetAppliedLayouts(layouts);

            GUID currentVirtualDesktop = virtualDesktop2;
            GUID lastUsedVirtualDesktop = GUID_NULL;
            std::optional<std::vector<GUID>> virtualDesktopsInRegistry = { { virtualDesktop1, virtualDesktop2 } };
            AppliedLayouts::instance().SyncVirtualDesktops(currentVirtualDesktop, lastUsedVirtualDesktop, virtualDesktopsInRegistry);

            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop1)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop1)));
            Assert::IsTrue(layout1 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(1, virtualDesktop2)));
            Assert::IsTrue(layout2 == AppliedLayouts::instance().GetDeviceLayout(GetWorkAreaID(2, virtualDesktop2)));
        }
    };

    TEST_CLASS (AppliedLayoutsFromOutdatedFileMappingUnitTests)
    {
        FancyZonesData& m_fzData = FancyZonesDataInstance();
        std::wstring m_testFolder = L"FancyZonesUnitTests";
        std::wstring m_testFolderPath = PTSettingsHelper::get_module_save_folder_location(m_testFolder);

        TEST_METHOD_INITIALIZE(Init)
        {
            m_fzData.SetSettingsModulePath(m_testFolder);
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            // MoveAppliedLayoutsFromZonesSettings creates all of these files, clean up
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(CustomLayouts::CustomLayoutsFileName());
            std::filesystem::remove(LayoutHotkeys::LayoutHotkeysFileName());
            std::filesystem::remove(LayoutTemplates::LayoutTemplatesFileName());
            std::filesystem::remove_all(m_testFolderPath);
            AppliedLayouts::instance().LoadData(); // clean data
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
                obj.SetNamedValue(L"active-zoneset", activeZoneset);

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
    };
}