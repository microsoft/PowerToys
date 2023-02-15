#include "pch.h"

#include <filesystem>

#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/FancyZonesData/DefaultLayouts.h>
#include <FancyZonesLib/FancyZonesWindowProperties.h>
#include <FancyZonesLib/LayoutAssignedWindows.h>
#include "Util.h"

#include <common/utils/process_path.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    const std::wstring m_deviceId = L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    const std::wstring m_virtualDesktopId = L"MyVirtualDesktopId";

    TEST_CLASS (WorkAreaCreationUnitTests)
    {
        FancyZonesDataTypes::WorkAreaId m_workAreaId;
        FancyZonesDataTypes::WorkAreaId m_emptyUniqueId;
        FancyZonesUtils::Rect m_workAreaRect{ RECT(0,0,1920,1080) };

        HINSTANCE m_hInst{};
        HMONITOR m_monitor{};

        TEST_METHOD_INITIALIZE(Init) noexcept
        {
            m_workAreaId.monitorId.deviceId.id = L"DELA026";
            m_workAreaId.monitorId.deviceId.instanceId = L"5&10a58c63&0&UID16777488";
            m_workAreaId.monitorId.serialNumber = L"serial-number";
            m_workAreaId.virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value();

            AppZoneHistory::instance().LoadData();
            AppliedLayouts::instance().LoadData();
            DefaultLayouts::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp) noexcept
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());

            std::filesystem::remove(DefaultLayouts::DefaultLayoutsFileName());
        }

        TEST_METHOD (CreateWorkArea)
        {
            const auto defaultLayout = DefaultLayouts::instance().GetDefaultLayout();

            auto workArea = WorkArea::Create({}, m_workAreaId, m_emptyUniqueId, m_workAreaRect);
            Assert::IsFalse(workArea == nullptr);
            Assert::IsTrue(m_workAreaId == workArea->UniqueId());

            const auto& layout = workArea->GetLayout();
            Assert::IsNotNull(layout.get());
            Assert::IsNotNull(workArea->GetLayoutWindows().get());
            Assert::AreEqual(static_cast<int>(defaultLayout.type), static_cast<int>(layout->Type()));
            Assert::AreEqual(defaultLayout.zoneCount, static_cast<int>(layout->Zones().size()));
        }

        TEST_METHOD (CreateCombinedWorkArea)
        {
            const auto defaultLayout = DefaultLayouts::instance().GetDefaultLayout();

            auto workArea = WorkArea::Create({}, m_workAreaId, m_emptyUniqueId, m_workAreaRect);
            Assert::IsFalse(workArea == nullptr);
            Assert::IsTrue(m_workAreaId == workArea->UniqueId());

            const auto& layout = workArea->GetLayout();
            Assert::IsNotNull(layout.get());
            Assert::IsNotNull(workArea->GetLayoutWindows().get());
            Assert::AreEqual(static_cast<int>(defaultLayout.type), static_cast<int>(layout->Type()));
            Assert::AreEqual(defaultLayout.zoneCount, static_cast<int>(layout->Zones().size()));
        }

        TEST_METHOD (CreateWorkAreaClonedFromParent)
        {
            using namespace FancyZonesDataTypes;
            
            FancyZonesDataTypes::WorkAreaId parentUniqueId;
            parentUniqueId.monitorId.deviceId.id = L"DELA026";
            parentUniqueId.monitorId.deviceId.instanceId = L"5&10a58c63&0&UID16777488";
            parentUniqueId.monitorId.serialNumber = L"serial-number";
            parentUniqueId.virtualDesktopId = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}").value();

            LayoutData layout{
                .uuid = FancyZonesUtils::GuidFromString(L"{61FA9FC0-26A6-4B37-A834-491C148DFC58}").value(),
                .type = ZoneSetLayoutType::Rows,
                .showSpacing = true,
                .spacing = 10,
                .zoneCount = 10,
                .sensitivityRadius = 20,
            };

            auto parentWorkArea = WorkArea::Create(m_hInst, parentUniqueId, m_emptyUniqueId, m_workAreaRect);
            AppliedLayouts::instance().ApplyLayout(parentUniqueId, layout);

            auto actualWorkArea = WorkArea::Create(m_hInst, m_workAreaId, parentUniqueId, m_workAreaRect);
            Assert::IsNotNull(actualWorkArea->GetLayout().get());
            Assert::IsNotNull(actualWorkArea->GetLayoutWindows().get());

            Assert::IsTrue(AppliedLayouts::instance().GetAppliedLayoutMap().contains(m_workAreaId));
            const auto& actualLayout = AppliedLayouts::instance().GetAppliedLayoutMap().at(m_workAreaId);

            Assert::AreEqual(static_cast<int>(layout.type), static_cast<int>(actualLayout.type));
            Assert::AreEqual(FancyZonesUtils::GuidToString(layout.uuid).value(), FancyZonesUtils::GuidToString(actualLayout.uuid).value());
            Assert::AreEqual(layout.sensitivityRadius, actualLayout.sensitivityRadius);
            Assert::AreEqual(layout.showSpacing, actualLayout.showSpacing);
            Assert::AreEqual(layout.spacing, actualLayout.spacing);
            Assert::AreEqual(layout.zoneCount, actualLayout.zoneCount);
        }

        TEST_METHOD (CreateWorkAreaWithCustomDefault)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::TypeID, json::value(L"custom"));
            json::JsonObject item{};
            item.SetNamedValue(NonLocalizable::DefaultLayoutsIds::MonitorConfigurationTypeID, json::value(L"horizontal"));
            item.SetNamedValue(NonLocalizable::DefaultLayoutsIds::LayoutID, layout);
            layoutsArray.Append(item);
            root.SetNamedValue(NonLocalizable::DefaultLayoutsIds::DefaultLayoutsArrayID, layoutsArray);
            
            json::to_file(DefaultLayouts::DefaultLayoutsFileName(), root);
            DefaultLayouts::instance().LoadData();

            // test
            auto workArea = WorkArea::Create({}, m_workAreaId, m_emptyUniqueId, m_workAreaRect);
            Assert::IsFalse(workArea == nullptr);
            Assert::IsTrue(m_workAreaId == workArea->UniqueId());

            Assert::IsNotNull(workArea->GetLayout().get());

            const auto& actualLayout = workArea->GetLayout();
            Assert::AreEqual(static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::Custom), static_cast<int>(actualLayout->Type()));
            Assert::IsTrue(FancyZonesUtils::GuidFromString(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}").value() == actualLayout->Id());
        }

        TEST_METHOD (CreateWorkAreaWithTemplateDefault)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray layoutsArray{};
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::TypeID, json::value(L"grid"));
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ShowSpacingID, json::value(true));
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SpacingID, json::value(1));
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::DefaultLayoutsIds::SensitivityRadiusID, json::value(30));

            json::JsonObject item{};
            item.SetNamedValue(NonLocalizable::DefaultLayoutsIds::MonitorConfigurationTypeID, json::value(L"horizontal"));
            item.SetNamedValue(NonLocalizable::DefaultLayoutsIds::LayoutID, layout);
            layoutsArray.Append(item);
            root.SetNamedValue(NonLocalizable::DefaultLayoutsIds::DefaultLayoutsArrayID, layoutsArray);

            json::to_file(DefaultLayouts::DefaultLayoutsFileName(), root);
            DefaultLayouts::instance().LoadData();

            // test
            auto workArea = WorkArea::Create({}, m_workAreaId, m_emptyUniqueId, m_workAreaRect);
            Assert::IsFalse(workArea == nullptr);
            Assert::IsTrue(m_workAreaId == workArea->UniqueId());

            Assert::IsNotNull(workArea->GetLayout().get());
            
            const auto& actualLayout = workArea->GetLayout();
            Assert::AreEqual(static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::Grid), static_cast<int>(actualLayout->Type()));
            Assert::AreEqual(static_cast<size_t>(4), actualLayout->Zones().size());
            Assert::IsTrue(GUID_NULL == actualLayout->Id());
        }
    };

    TEST_CLASS (WorkAreaMoveWindowUnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const FancyZonesDataTypes::WorkAreaId m_workAreaId{
            .monitorId = {
                .monitor = Mocks::Monitor(),
                .deviceId = { 
                    .id = L"DELA026",
                    .instanceId = L"5&10a58c63&0&UID16777488",
                    .number = 1,
                },
                .serialNumber = L"serial-number"
            },
            .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value()
        };

        FancyZonesDataTypes::WorkAreaId m_parentUniqueId; // default empty

        HINSTANCE m_hInst{};
        FancyZonesUtils::Rect m_workAreaRect{ RECT(0, 0, 1920, 1080) };

        void PrepareEmptyLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Blank)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(0));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(0));

                json::JsonObject workAreaId{};
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(m_workAreaId.monitorId.deviceId.id));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(m_workAreaId.monitorId.deviceId.instanceId));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(m_workAreaId.monitorId.serialNumber));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(m_workAreaId.monitorId.deviceId.number));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));
                
                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaId);
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);
            
            AppliedLayouts::instance().LoadData();
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            {
                json::JsonObject layout{};
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
                layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

                json::JsonObject workAreaId{};
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(m_workAreaId.monitorId.deviceId.id));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(m_workAreaId.monitorId.deviceId.instanceId));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(m_workAreaId.monitorId.serialNumber));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(m_workAreaId.monitorId.deviceId.number));
                workAreaId.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaId);
                obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

                layoutsArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_INITIALIZE(Init) noexcept
        {
            AppZoneHistory::instance().LoadData();
            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp) noexcept
        {
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
        }

        TEST_METHOD (EmptyZonesMoveLeftByIndex)
        {
            // prepare
            PrepareEmptyLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)0, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (EmptyZonesRightByIndex)
        {
            // prepare
            PrepareEmptyLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)0, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveLeftNonAppliedWindowByIndex)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveRightNonAppliedWindowByIndex)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByIndex)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& layoutWindows = workArea->GetLayoutWindows();
            
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows->GetZoneIndexSetFromWindow(window));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
        }

        TEST_METHOD (MoveAppliedWindowByIndexCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ static_cast<ZoneIndex>(workArea->GetLayout()->Zones().size() - 1) } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByIndexNoCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, false);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (EmptyZonesMoveByPosition)
        {
            // prepare
            PrepareEmptyLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)0, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveLeftNonAppliedWindowByPosition)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveRightNonAppliedWindowByPosition)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_RIGHT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowHorizontallyByPosition)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_RIGHT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowVerticallyByPosition)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_DOWN, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByPositionHorizontallyCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_LEFT, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByPositionHorizontallyNoCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_LEFT, false);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByPositionVerticallyCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& layoutWindows = workArea->GetLayoutWindows();
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_UP, true);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (MoveAppliedWindowByPositionVerticallyNoCycle)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->MoveWindowIntoZoneByDirectionAndPosition(window, VK_UP, false);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }
    
        TEST_METHOD (ExtendZoneHorizontally)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->ExtendWindowByDirectionAndPosition(window, VK_RIGHT);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (ExtendZoneVertically)
        {
            // prepare
            PrepareGridLayout();
            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);
            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true); // apply to 1st zone

            // test
            workArea->ExtendWindowByDirectionAndPosition(window, VK_DOWN);

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == layoutWindows->GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (WhenWindowIsNotResizablePlacingItIntoTheZoneShouldNotResizeIt)
        {
            const auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            constexpr int originalWidth = 450;
            constexpr int originalHeight = 550;

            SetWindowPos(window, nullptr, 150, 150, originalWidth, originalHeight, SWP_SHOWWINDOW);
            SetWindowLong(window, GWL_STYLE, GetWindowLong(window, GWL_STYLE) & ~WS_SIZEBOX);

            workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, true);

            RECT inZoneRect;
            GetWindowRect(window, &inZoneRect);

            Assert::AreEqual(originalWidth, (int)inZoneRect.right - (int)inZoneRect.left);
            Assert::AreEqual(originalHeight, (int)inZoneRect.bottom - (int)inZoneRect.top);
        }

        TEST_METHOD (SnapWindowPropertyTest)
        {
            const auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            const ZoneIndexSet expected = { 1, 2 };
            workArea->SnapWindow(window, expected);

            const auto actual = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
            Assert::AreEqual(expected.size(), actual.size());
            for (int i = 0; i < expected.size(); i++)
            {
                Assert::AreEqual(expected.at(i), actual.at(i));
            }
        }

        TEST_METHOD (SnapAppZoneHistoryTest)
        {
            const auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            const ZoneIndexSet expected = { 1, 2 };
            workArea->SnapWindow(window, expected);

            const auto processPath = get_process_path(window);
            const auto history = AppZoneHistory::instance().GetZoneHistory(processPath, m_workAreaId);

            Assert::IsTrue(history.has_value());
            Assert::AreEqual(expected.size(), history->zoneIndexSet.size());
            for (int i = 0; i < expected.size(); i++)
            {
                Assert::AreEqual(expected.at(i), history->zoneIndexSet.at(i));
            }
        }

        TEST_METHOD (UnsnapPropertyTest)
        {
            const auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            workArea->SnapWindow(window, { 1, 2 });
            workArea->UnsnapWindow(window);

            const auto actual = FancyZonesWindowProperties::RetrieveZoneIndexProperty(window);
            Assert::IsTrue(actual.empty());
        }

        TEST_METHOD (UnsnapAppZoneHistoryTest)
        {
            const auto workArea = WorkArea::Create(m_hInst, m_workAreaId, m_parentUniqueId, m_workAreaRect);
            const auto window = Mocks::WindowCreate(m_hInst);

            workArea->SnapWindow(window, { 1, 2 });
            workArea->UnsnapWindow(window);

            const auto processPath = get_process_path(window);
            const auto history = AppZoneHistory::instance().GetZoneHistory(processPath, m_workAreaId);

            Assert::IsFalse(history.has_value());
        }
    };
}
