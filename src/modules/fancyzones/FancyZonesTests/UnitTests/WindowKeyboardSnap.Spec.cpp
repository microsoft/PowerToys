#include "pch.h"

#include <FancyZonesLib/WindowKeyboardSnap.h>

#include <FancyZonesLib/FancyZonesData/AppliedLayouts.h>
#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>
#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/util.h>

#include <common/utils/json.h>

#include <filesystem>

#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (WindowKeyboardSnap_ByIndex_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::wstring m_layoutIdStr = L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}";
        constexpr GUID layoutId()
        {
            return FancyZonesUtils::GuidFromString(m_layoutIdStr).value();
        }

        const HMONITOR m_monitor = Mocks::Monitor();

        const FancyZonesDataTypes::WorkAreaId m_workAreaId = {
                .monitorId = {
                    .monitor = m_monitor,
                    .deviceId = {
                        .id = L"device-id-1",
                        .instanceId = L"5&10a58c63&0&UID16777488",
                        .number = 1,
                    },
                    .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() };

        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap;
        HINSTANCE m_hInst{};
         
        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(m_layoutIdStr));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            layoutsArray.Append(WorkAreaLayoutObject(m_workAreaId));

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_INITIALIZE (Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();
            FancyZonesSettings::instance().SetSettings({ .overrideSnapHotkeys = true, .moveWindowsBasedOnPosition = false });

            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, {}, FancyZonesUtils::Rect{ RECT(0, 0, 1920, 1080) });
            m_workAreaMap.insert({ m_monitor, std::move(workArea) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }
        
        TEST_METHOD (Snap_Left)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_LEFT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 3 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Snap_Right)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveToNextZone)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 1 }, true);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveToPrevZone)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 1 }, true);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_LEFT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveNext_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 3 }, true);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MovePrev_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_LEFT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 3 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveNext_NoCycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 3 }, true);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = false;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MovePrev_NoCycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = false;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_LEFT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 3 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }
    };

    TEST_CLASS (WindowKeyboardSnap_MoveAcrossMonitors_ByIndex_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::vector<HMONITOR> m_monitors = { Mocks::Monitor(), Mocks::Monitor() };

        const std::vector<FancyZonesDataTypes::WorkAreaId> m_workAreaIds = {
            FancyZonesDataTypes::WorkAreaId{
                .monitorId = {
                    .monitor = m_monitors[0],
                    .deviceId = {
                        .id = L"device-id-1",
                        .instanceId = L"5&10a58c63&0&UID16777488",
                        .number = 1,
                    },
                    .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() },
            FancyZonesDataTypes::WorkAreaId{ 
                .monitorId = { 
                    .monitor = m_monitors[1], 
                    .deviceId = {
                        .id = L"device-id-2",
                        .instanceId = L"5&10a58c63&0&UID16777489",
                        .number = 2,
                    },
                    .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() }
        };

        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap;
        HINSTANCE m_hInst{};

        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            for (const auto& workAreaId : m_workAreaIds)
            {
                layoutsArray.Append(WorkAreaLayoutObject(workAreaId));
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            auto workArea1 = WorkArea::Create(m_hInst, m_workAreaIds[0], {}, FancyZonesUtils::Rect{ RECT(0, 0, 1920, 1080) });
            m_workAreaMap.insert({ m_monitors[0], std::move(workArea1) });

            auto workArea2 = WorkArea::Create(m_hInst, m_workAreaIds[1], {}, FancyZonesUtils::Rect{ RECT(1920, 0, 3840, 1080) });
            m_workAreaMap.insert({ m_monitors[1], std::move(workArea2) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }

        TEST_METHOD (Snap_Left)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[0], VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[0], VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Left_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap[m_monitors[1]]->Snap(window, { 0 });

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[1], VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitors[0])->Snap(window, { 3 });

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[0], VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Left_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap[m_monitors[0]]->Snap(window, { 0 });

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[0], VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitors[1])->Snap(window, { 3 });

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[1], VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Left_NoCycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap[m_monitors[0]]->Snap(window, { 0 });

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = false;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[0], VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right_NoCycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitors[1])->Snap(window, { 3 });

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = false;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitors[1], VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[1])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0])->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }
    };

    TEST_CLASS (WindowKeyboardSnap_ByPosition_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::wstring m_layoutIdStr = L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}";
        constexpr GUID layoutId()
        {
            return FancyZonesUtils::GuidFromString(m_layoutIdStr).value();
        }

        const HMONITOR m_monitor = Mocks::Monitor();
        const RECT m_rect = RECT{ 0, 0, 1920, 1080 };

        const FancyZonesDataTypes::WorkAreaId m_workAreaId = {
            .monitorId = {
                .monitor = m_monitor,
                .deviceId = {
                    .id = L"device-id-1",
                    .instanceId = L"5&10a58c63&0&UID16777488",
                    .number = 1,
                },
                .serialNumber = L"serial-number-1" },
            .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value()
        };

        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap;
        HINSTANCE m_hInst{};

        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(m_layoutIdStr));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            layoutsArray.Append(WorkAreaLayoutObject(m_workAreaId));

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        // using zone rects instead of the actual window rect 
        // otherwise we'll need to wait after snapping, for the window to be resized
        RECT GetZoneRect(const WorkArea* workArea, ZoneIndex index)
        {
            auto rect = workArea->GetLayout()->Zones().at(index).GetZoneRect();
            auto workAreaRect = workArea->GetWorkAreaRect();
            return rect;
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();

            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, {}, FancyZonesUtils::Rect{ m_rect });
            m_workAreaMap.insert({ m_monitor, std::move(workArea) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }

        TEST_METHOD (Snap_Left)
        {   
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()).size());
        }

        TEST_METHOD (Snap_Right)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap, { { m_monitor, m_rect } }));

            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()).size());

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
        }

        TEST_METHOD (Snap_Up)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_UP, m_workAreaMap, { { m_monitor, m_rect } }));

            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()).size());
            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
        }

        TEST_METHOD (Snap_Down)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap, { { m_monitor, m_rect } }));

            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()).size());
            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
        }

        TEST_METHOD (Move_Left)
        {   
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 1 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 1);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Move_Right)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Move_Up)
        {   
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 2 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 2);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_UP, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Move_Down)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveLeft_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveRight_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 1 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 1);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveUp_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_UP, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (MoveDown_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 2 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 2);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }
    };

    TEST_CLASS (WindowKeyboardSnap_MoveAcrossMonitors_ByPosition_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::vector<std::pair<HMONITOR, RECT>> m_monitors = {
            { Mocks::Monitor(), RECT{ 0, 0, 1920, 1080 } }, // left
            { Mocks::Monitor(), RECT{ 1920, 0, 3840, 1080 } }, // right
            { Mocks::Monitor(), RECT{ 0, -1080, 1920, 0 } } // top
        };

        const std::vector<FancyZonesDataTypes::WorkAreaId> m_workAreaIds = {
            FancyZonesDataTypes::WorkAreaId{
                .monitorId = {
                    .monitor = m_monitors[0].first,
                    .deviceId = {
                        .id = L"device-id-left",
                        .instanceId = L"5&10a58c63&0&UID16777488",
                        .number = 1,
                    },
                    .serialNumber = L"serial-number-left" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() },
            FancyZonesDataTypes::WorkAreaId{ 
                .monitorId = { 
                    .monitor = m_monitors[1].first, 
                    .deviceId = {
                        .id = L"device-id-right",
                        .instanceId = L"5&10a58c63&0&UID16777489",
                        .number = 2,
                    },
                    .serialNumber = L"serial-number-right" },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() },
            FancyZonesDataTypes::WorkAreaId{ 
                .monitorId = { 
                    .monitor = m_monitors[2].first, 
                    .deviceId = {
                        .id = L"device-id-top",
                        .instanceId = L"5&10a58c63&0&UID16777487",
                        .number = 3,
                    },
                    .serialNumber = L"serial-number-top" },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value() }
        };

        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap;
        HINSTANCE m_hInst{};

        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}"));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            for (const auto& workAreaId : m_workAreaIds)
            {
                layoutsArray.Append(WorkAreaLayoutObject(workAreaId));
            }

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        RECT GetAdjustedZoneRect(const WorkArea* workArea, ZoneIndex index)
        {
            auto rect = workArea->GetLayout()->Zones().at(index).GetZoneRect();
            auto workAreaRect = workArea->GetWorkAreaRect();
            rect.left += workAreaRect.left();
            rect.right += workAreaRect.left();
            rect.top += workAreaRect.top();
            rect.bottom += workAreaRect.top();
            return rect;
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            auto workArea1 = WorkArea::Create(m_hInst, m_workAreaIds[0], {}, FancyZonesUtils::Rect{ m_monitors[0].second });
            m_workAreaMap.insert({ m_monitors[0].first, std::move(workArea1) });

            auto workArea2 = WorkArea::Create(m_hInst, m_workAreaIds[1], {}, FancyZonesUtils::Rect{ m_monitors[1].second });
            m_workAreaMap.insert({ m_monitors[1].first, std::move(workArea2) });

            auto workArea3 = WorkArea::Create(m_hInst, m_workAreaIds[2], {}, FancyZonesUtils::Rect{ m_monitors[2].second });
            m_workAreaMap.insert({ m_monitors[2].first, std::move(workArea3) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }

        TEST_METHOD (Snap_Left)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect; 
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
        }

        TEST_METHOD (Snap_Right)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
        }

        TEST_METHOD (Snap_Up)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_UP, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
        }

        TEST_METHOD (Snap_Down)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_DOWN, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindows = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
        }

        TEST_METHOD (Snap_Left_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 0;
            m_workAreaMap[m_monitors[1].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[1].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[1].first, VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[1].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 3;
            m_workAreaMap[m_monitors[0].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[0].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[1].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Up_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 0;
            m_workAreaMap[m_monitors[0].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[0].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_UP, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
             
            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[2].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Down_NextWorkArea)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 2;
            m_workAreaMap[m_monitors[2].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[2].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[2].first, VK_DOWN, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[2].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Left_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 0;
            m_workAreaMap[m_monitors[0].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[0].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_LEFT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[1].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Right_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 3;
            m_workAreaMap[m_monitors[1].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[1].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[1].first, VK_RIGHT, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[1].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Up_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 0;
            m_workAreaMap[m_monitors[2].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[2].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[2].first, VK_UP, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[2].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }

        TEST_METHOD (Snap_Down_NextWorkArea_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const ZoneIndex initialZoneIndex = 2;
            m_workAreaMap[m_monitors[0].first]->Snap(window, { initialZoneIndex });
            RECT windowRect = GetAdjustedZoneRect(m_workAreaMap[m_monitors[0].first].get(), initialZoneIndex); // use zone rect instead of the actual position
            
            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitors[0].first, VK_DOWN, m_workAreaMap, m_monitors));

            const auto& actualAppZoneHistory = AppZoneHistory::instance().GetFullAppZoneHistory();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());

            const auto& layoutWindowsPrevWorkArea = m_workAreaMap.at(m_monitors[0].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{} == layoutWindowsPrevWorkArea.GetZoneIndexSetFromWindow(window));

            const auto& layoutWindowsActualWorkArea = m_workAreaMap.at(m_monitors[2].first)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindowsActualWorkArea.GetZoneIndexSetFromWindow(window));
        }
    };

    TEST_CLASS (WindowKeyboardSnap_Extend_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::wstring m_layoutIdStr = L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}";
        constexpr GUID layoutId()
        {
            return FancyZonesUtils::GuidFromString(m_layoutIdStr).value();
        }

        const HMONITOR m_monitor = Mocks::Monitor();
        const FancyZonesDataTypes::WorkAreaId m_workAreaId = {
            .monitorId = {
                .monitor = m_monitor,
                .deviceId = {
                    .id = L"device-id-1",
                    .instanceId = L"5&10a58c63&0&UID16777488",
                    .number = 1,
                },
                .serialNumber = L"serial-number-1" },
            .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value()
        };
        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap = {};
        HINSTANCE m_hInst{};
         
        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(m_layoutIdStr));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            layoutsArray.Append(WorkAreaLayoutObject(m_workAreaId));

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        RECT GetAdjustedZoneRect(const WorkArea* workArea, ZoneIndex index)
        {
            auto rect = workArea->GetLayout()->Zones().at(index).GetZoneRect();
            auto workAreaRect = workArea->GetWorkAreaRect();
            rect.left += workAreaRect.left();
            rect.right += workAreaRect.left();
            rect.top += workAreaRect.top();
            rect.bottom += workAreaRect.top();
            return rect;
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();

            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, {}, FancyZonesUtils::Rect{ RECT(0, 0, 1920, 1080) });
            m_workAreaMap.insert({ m_monitor,  std::move(workArea) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }

        TEST_METHOD(ExtendNonSnappedWindow)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect = {10,10,150,150};
            Assert::IsTrue(SetWindowPos(window, nullptr, windowRect.left, windowRect.top, windowRect.right - windowRect.left, windowRect.bottom - windowRect.top, 0));

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap));

            const auto& layoutWindows = m_workAreaMap[m_monitor]->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaMap[m_monitor]->UniqueId(), m_workAreaMap[m_monitor]->GetLayoutId()).size());
        }

        TEST_METHOD (ExtendSnappedWindow)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 0 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }

        TEST_METHOD (ExtendLeft)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 1 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 1);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }

        TEST_METHOD (ExtendRight)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 0 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }

        TEST_METHOD (ExtendUp)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 2 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 2);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_UP, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }

        TEST_METHOD (ExtendDown)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 0 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }

        TEST_METHOD (ExtendAndRevert)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 0 });
            RECT windowRect = GetAdjustedZoneRect(workArea.get(), 0);
        
            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap));
            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap));
            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap));
            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_UP, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }
    };

    TEST_CLASS(WindowKeyboardSnap_MultiMonitorMode_UnitTests)
    {
        const std::wstring m_virtualDesktopIdStr = L"{A998CA86-F08D-4BCA-AED8-77F5C8FC9925}";
        const std::wstring m_layoutIdStr = L"{ACE817FD-2C51-4E13-903A-84CAB86FD17C}";
        constexpr GUID layoutId()
        {
            return FancyZonesUtils::GuidFromString(m_layoutIdStr).value();
        }

        HINSTANCE m_hInst{};
        const RECT m_rect = RECT{ 0, 0, 1920, 1080 };
        const HMONITOR m_monitor = nullptr;
        const FancyZonesDataTypes::WorkAreaId m_workAreaId = {
            .monitorId = {
                .monitor = m_monitor,
                .deviceId = {
                    .id = L"FancyZones",
                    .instanceId = L"MultiMonitorDevice",
                    .number = 0,
                },
                .serialNumber = L"" },
            .virtualDesktopId = FancyZonesUtils::GuidFromString(m_virtualDesktopIdStr).value()
        };
        std::unordered_map<HMONITOR, std::unique_ptr<WorkArea>> m_workAreaMap = {};
        
        json::JsonObject WorkAreaLayoutObject(const FancyZonesDataTypes::WorkAreaId& workAreaId)
        {
            json::JsonObject layout{};
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::UuidID, json::value(m_layoutIdStr));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::TypeID, json::value(FancyZonesDataTypes::TypeToString(FancyZonesDataTypes::ZoneSetLayoutType::Grid)));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ShowSpacingID, json::value(false));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SpacingID, json::value(0));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::ZoneCountID, json::value(4));
            layout.SetNamedValue(NonLocalizable::AppliedLayoutsIds::SensitivityRadiusID, json::value(20));

            json::JsonObject workAreaIdObj{};
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorID, json::value(workAreaId.monitorId.deviceId.id));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorInstanceID, json::value(workAreaId.monitorId.deviceId.instanceId));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorSerialNumberID, json::value(workAreaId.monitorId.serialNumber));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::MonitorNumberID, json::value(workAreaId.monitorId.deviceId.number));
            workAreaIdObj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::VirtualDesktopID, json::value(m_virtualDesktopIdStr));

            json::JsonObject obj{};
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::DeviceID, workAreaIdObj);
            obj.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutID, layout);

            return obj;
        }

        void PrepareGridLayout()
        {
            json::JsonObject root{};
            json::JsonArray layoutsArray{};

            layoutsArray.Append(WorkAreaLayoutObject(m_workAreaId));

            root.SetNamedValue(NonLocalizable::AppliedLayoutsIds::AppliedLayoutsArrayID, layoutsArray);
            json::to_file(AppliedLayouts::AppliedLayoutsFileName(), root);

            AppliedLayouts::instance().LoadData();
        }

        // use zone rects instead of the actual window rect
        // otherwise we'll need to wait after snapping, for the window to be resized
        // important when snapping/extending by position
        RECT GetZoneRect(const WorkArea* workArea, ZoneIndex index)
        {
            auto rect = workArea->GetLayout()->Zones().at(index).GetZoneRect();
            auto workAreaRect = workArea->GetWorkAreaRect();
            return rect;
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            AppZoneHistory::instance().LoadData();
            PrepareGridLayout();

            auto workArea = WorkArea::Create(m_hInst, m_workAreaId, {}, FancyZonesUtils::Rect{ RECT(0, 0, 1920, 1080) });
            m_workAreaMap.insert({ m_monitor, std::move(workArea) });
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppliedLayouts::AppliedLayoutsFileName());
            std::filesystem::remove(AppZoneHistory::AppZoneHistoryFileName());
        }

        TEST_METHOD(Snap_ByIndex)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_LEFT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 3 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 3 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD(Move_ByIndex)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 1 }, true);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Move_ByIndex_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 3 }, true);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, m_monitor, VK_RIGHT, m_workAreaMap, { m_monitor }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Snap_ByPosition)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            RECT windowRect;
            Assert::IsTrue(GetWindowRect(window, &windowRect));

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_LEFT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::AreEqual((size_t)1, layoutWindows.GetZoneIndexSetFromWindow(window).size());
            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()).size());
        }

        TEST_METHOD (Move_ByPosition)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_RIGHT, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 1 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Move_ByPosition_Cycle)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            m_workAreaMap.at(m_monitor)->Snap(window, { 0 }, true);
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            auto settings = FancyZonesSettings::settings();
            settings.moveWindowAcrossMonitors = true;
            FancyZonesSettings::instance().SetSettings(settings);

            Assert::IsTrue(windowKeyboardSnap.Snap(window, windowRect, m_monitor, VK_UP, m_workAreaMap, { { m_monitor, m_rect } }));

            const auto& layoutWindows = m_workAreaMap.at(m_monitor)->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, m_workAreaId, layoutId()));
        }

        TEST_METHOD (Extend)
        {
            WindowKeyboardSnap windowKeyboardSnap;
            const auto window = Mocks::WindowCreate(m_hInst);
            const auto& workArea = m_workAreaMap[m_monitor];
            workArea->Snap(window, { 0 });
            RECT windowRect = GetZoneRect(m_workAreaMap[m_monitor].get(), 0);

            Assert::IsTrue(windowKeyboardSnap.Extend(window, windowRect, m_monitor, VK_DOWN, m_workAreaMap));

            const auto& layoutWindows = workArea->GetLayoutWindows();
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == layoutWindows.GetZoneIndexSetFromWindow(window));
            Assert::IsTrue(ZoneIndexSet{ 0, 2 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workArea->UniqueId(), workArea->GetLayoutId()));
        }
    };
}
