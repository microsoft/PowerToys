#include "pch.h"

#include <filesystem>

#include <FancyZonesLib/util.h>
#include <FancyZonesLib/ZoneSet.h>
#include <FancyZonesLib/WorkArea.h>
#include <FancyZonesLib/FancyZones.h>
#include <FancyZonesLib/FancyZonesData.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/JsonHelpers.h>
#include <FancyZonesLib/ZoneColors.h>
#include "Util.h"

#include <common/utils/process_path.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    const std::wstring m_deviceId = L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
    const std::wstring m_virtualDesktopId = L"MyVirtualDesktopId";

    TEST_CLASS (WorkAreaCreationUnitTests)
    {
        std::wstringstream m_parentUniqueId;
        std::wstringstream m_uniqueId;

        HINSTANCE m_hInst{};
        HMONITOR m_monitor{};
        MONITORINFOEX m_monitorInfo{};
        GUID m_virtualDesktopGuid{};
        ZoneColors m_zoneColors{};
        OverlappingZonesAlgorithm m_overlappingAlgorithm = OverlappingZonesAlgorithm::Positional;

        FancyZonesData& m_fancyZonesData = FancyZonesDataInstance();

        void testWorkArea(winrt::com_ptr<IWorkArea> workArea)
        {
            const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);

            Assert::IsNotNull(workArea.get());
            Assert::AreEqual(m_uniqueId.str().c_str(), workArea->UniqueId().c_str());
        }

        TEST_METHOD_INITIALIZE(Init)
            {
                m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);

                m_monitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
                m_monitorInfo.cbSize = sizeof(m_monitorInfo);
                Assert::AreNotEqual(0, GetMonitorInfoW(m_monitor, &m_monitorInfo));

                m_parentUniqueId << L"DELA026#5&10a58c63&0&UID16777488_" << m_monitorInfo.rcMonitor.right << "_" << m_monitorInfo.rcMonitor.bottom << "_{61FA9FC0-26A6-4B37-A834-491C148DFC57}";
                m_uniqueId << L"DELA026#5&10a58c63&0&UID16777488_" << m_monitorInfo.rcMonitor.right << "_" << m_monitorInfo.rcMonitor.bottom << "_{39B25DD2-130D-4B5D-8851-4791D66B1539}";

                m_fancyZonesData.SetSettingsModulePath(L"FancyZonesUnitTests");
                m_fancyZonesData.clear_data();

                auto guid = Helpers::StringToGuid(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}");
                Assert::IsTrue(guid.has_value());
                m_virtualDesktopGuid = *guid;

                m_zoneColors = ZoneColors{
                    .primaryColor = FancyZonesUtils::HexToRGB(L"#4287f5"),
                    .borderColor = FancyZonesUtils::HexToRGB(L"#FFFFFF"),
                    .highlightColor = FancyZonesUtils::HexToRGB(L"#42eff5"),
                    .highlightOpacity = 50,
                };
            }

            TEST_METHOD (CreateWorkArea)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                testWorkArea(workArea);

                auto* activeZoneSet{ workArea->ActiveZoneSet() };
                Assert::IsNotNull(activeZoneSet);
                Assert::AreEqual(static_cast<int>(activeZoneSet->LayoutType()), static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid));
                Assert::AreEqual(activeZoneSet->GetZones().size(), static_cast<size_t>(3));
            }

            TEST_METHOD (CreateWorkAreaNoHinst)
            {
                auto workArea = MakeWorkArea({}, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                testWorkArea(workArea);

                auto* activeZoneSet{ workArea->ActiveZoneSet() };
                Assert::IsNotNull(activeZoneSet);
                Assert::AreEqual(static_cast<int>(activeZoneSet->LayoutType()), static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid));
                Assert::AreEqual(activeZoneSet->GetZones().size(), static_cast<size_t>(3));
            }

            TEST_METHOD (CreateWorkAreaNoHinstFlashZones)
            {
                auto workArea = MakeWorkArea({}, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                testWorkArea(workArea);

                auto* activeZoneSet{ workArea->ActiveZoneSet() };
                Assert::IsNotNull(activeZoneSet);
                Assert::AreEqual(static_cast<int>(activeZoneSet->LayoutType()), static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid));
                Assert::AreEqual(activeZoneSet->GetZones().size(), static_cast<size_t>(3));
            }

            TEST_METHOD (CreateWorkAreaNoMonitor)
            {
                auto workArea = MakeWorkArea(m_hInst, {}, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                testWorkArea(workArea);
            }

            TEST_METHOD (CreateWorkAreaNoDeviceId)
            {
                // Generate unique id without device id
                std::wstring uniqueId = FancyZonesUtils::GenerateUniqueId(m_monitor, {}, m_virtualDesktopId);
                auto workArea = MakeWorkArea(m_hInst, m_monitor, uniqueId, {}, m_zoneColors, m_overlappingAlgorithm);

                const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);
                const std::wstring expectedUniqueId = L"FallbackDevice_" + std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom) + L"_" + m_virtualDesktopId;

                Assert::IsNotNull(workArea.get());
                Assert::AreEqual(expectedUniqueId.c_str(), workArea->UniqueId().c_str());

                auto* activeZoneSet{ workArea->ActiveZoneSet() };
                Assert::IsNotNull(activeZoneSet);
                Assert::AreEqual(static_cast<int>(activeZoneSet->LayoutType()), static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid));
                Assert::AreEqual(activeZoneSet->GetZones().size(), static_cast<size_t>(3));
            }

            TEST_METHOD (CreateWorkAreaNoDesktopId)
            {
                // Generate unique id without virtual desktop id
                std::wstring uniqueId = FancyZonesUtils::GenerateUniqueId(m_monitor, m_deviceId, {});
                auto workArea = MakeWorkArea(m_hInst, m_monitor, uniqueId, {}, m_zoneColors, m_overlappingAlgorithm);

                const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);
                Assert::IsNotNull(workArea.get());
                Assert::IsTrue(workArea->UniqueId().empty());

                auto* activeZoneSet{ workArea->ActiveZoneSet() };
                Assert::IsNotNull(activeZoneSet);
                Assert::AreEqual(static_cast<int>(activeZoneSet->LayoutType()), static_cast<int>(FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid));
                Assert::AreEqual(activeZoneSet->GetZones().size(), static_cast<size_t>(3));
            }

        TEST_METHOD (CreateWorkAreaClonedFromParent)
        {
            using namespace FancyZonesDataTypes;

                const ZoneSetLayoutType type = ZoneSetLayoutType::PriorityGrid;
                const int spacing = 10;
                const int zoneCount = 5;
                const auto customSetGuid = Helpers::CreateGuidString();
                const auto parentZoneSet = ZoneSetData{ customSetGuid, type };
                const auto parentDeviceInfo = DeviceInfoData{ parentZoneSet, true, spacing, zoneCount };
                m_fancyZonesData.SetDeviceInfo(m_parentUniqueId.str(), parentDeviceInfo);

                auto parentWorkArea = MakeWorkArea(m_hInst, m_monitor, m_parentUniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                
                // newWorkArea = false - workArea won't be cloned from parent
                auto actualWorkArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                Assert::IsNotNull(actualWorkArea->ActiveZoneSet());

                Assert::IsTrue(m_fancyZonesData.GetDeviceInfoMap().contains(m_uniqueId.str()));
                auto currentDeviceInfo = m_fancyZonesData.GetDeviceInfoMap().at(m_uniqueId.str());
                // default values
                Assert::AreEqual(true, currentDeviceInfo.showSpacing);
                Assert::AreEqual(3, currentDeviceInfo.zoneCount);
                Assert::AreEqual(16, currentDeviceInfo.spacing);
                Assert::AreEqual(static_cast<int>(ZoneSetLayoutType::PriorityGrid), static_cast<int>(currentDeviceInfo.activeZoneSet.type));
            }
    };

    TEST_CLASS (WorkAreaUnitTests)
    {
        std::wstringstream m_uniqueId;

        HINSTANCE m_hInst{};
        HMONITOR m_monitor{};
        MONITORINFO m_monitorInfo{};
        ZoneColors m_zoneColors{};
        OverlappingZonesAlgorithm m_overlappingAlgorithm = OverlappingZonesAlgorithm::Positional;

        FancyZonesData& m_fancyZonesData = FancyZonesDataInstance();

        TEST_METHOD_INITIALIZE(Init)
            {
                m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);

                m_monitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
                m_monitorInfo.cbSize = sizeof(m_monitorInfo);
                Assert::AreNotEqual(0, GetMonitorInfoW(m_monitor, &m_monitorInfo));

                m_uniqueId << L"DELA026#5&10a58c63&0&UID16777488_" << m_monitorInfo.rcMonitor.right << "_" << m_monitorInfo.rcMonitor.bottom << "_{39B25DD2-130D-4B5D-8851-4791D66B1539}";

                m_fancyZonesData.SetSettingsModulePath(L"FancyZonesUnitTests");
                m_fancyZonesData.clear_data();

                m_zoneColors = ZoneColors{
                    .primaryColor = FancyZonesUtils::HexToRGB(L"#4287f5"),
                    .borderColor = FancyZonesUtils::HexToRGB(L"#FFFFFF"),
                    .highlightColor = FancyZonesUtils::HexToRGB(L"#42eff5"),
                    .highlightOpacity = 50,
                };
            }

        public:
            TEST_METHOD (MoveSizeEnter)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeEnter(Mocks::Window());

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeEnterTwice)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = S_OK;

                workArea->MoveSizeEnter(Mocks::Window());
                const auto actual = workArea->MoveSizeEnter(Mocks::Window());

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeUpdate)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeUpdate(POINT{ 0, 0 }, true, false);

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeUpdatePointNegativeCoordinates)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeUpdate(POINT{ -10, -10 }, true, false);

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeUpdatePointBigCoordinates)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeUpdate(POINT{ m_monitorInfo.rcMonitor.right + 1, m_monitorInfo.rcMonitor.bottom + 1 }, true, false);

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeEnd)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto window = Mocks::Window();
                workArea->MoveSizeEnter(window);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeEnd(window, POINT{ 0, 0 });
                Assert::AreEqual(expected, actual);

                const auto zoneSet = workArea->ActiveZoneSet();
                zoneSet->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                const auto actualZoneIndexSet = zoneSet->GetZoneIndexSetFromWindow(window);
                Assert::IsFalse(std::vector<ZoneIndex>{} == actualZoneIndexSet);
            }

            TEST_METHOD (MoveSizeEndWindowNotAdded)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto window = Mocks::Window();
                workArea->MoveSizeEnter(window);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeEnd(window, POINT{ -100, -100 });
                Assert::AreEqual(expected, actual);

                const auto zoneSet = workArea->ActiveZoneSet();
                const auto actualZoneIndexSet = zoneSet->GetZoneIndexSetFromWindow(window);
                Assert::IsTrue(std::vector<ZoneIndex>{} == actualZoneIndexSet);
            }

            TEST_METHOD (MoveSizeEndDifferentWindows)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto window = Mocks::Window();
                workArea->MoveSizeEnter(window);

                const auto expected = E_INVALIDARG;
                const auto actual = workArea->MoveSizeEnd(Mocks::Window(), POINT{ 0, 0 });

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeEndWindowNotSet)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto expected = E_INVALIDARG;
                const auto actual = workArea->MoveSizeEnd(Mocks::Window(), POINT{ 0, 0 });

                Assert::AreEqual(expected, actual);
            }

            TEST_METHOD (MoveSizeEndInvalidPoint)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);

                const auto window = Mocks::Window();
                workArea->MoveSizeEnter(window);

                const auto expected = S_OK;
                const auto actual = workArea->MoveSizeEnd(window, POINT{ -1, -1 });
                Assert::AreEqual(expected, actual);

                const auto zoneSet = workArea->ActiveZoneSet();
                zoneSet->MoveWindowIntoZoneByIndex(window, Mocks::Window(), 0);
                const auto actualZoneIndex = zoneSet->GetZoneIndexSetFromWindow(window);
                Assert::IsFalse(std::vector<ZoneIndex>{} == actualZoneIndex); // with invalid point zone remains the same
            }

            TEST_METHOD (MoveWindowIntoZoneByIndex)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                workArea->MoveWindowIntoZoneByIndex(Mocks::Window(), 0);

                const auto actual = workArea->ActiveZoneSet();
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionAndIndex)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                const auto window = Mocks::WindowCreate(m_hInst);
                workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);

                const auto& actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
                Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
                const auto& appHistoryArray = actualAppZoneHistory.begin()->second;
                Assert::AreEqual((size_t)1, appHistoryArray.size());
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == appHistoryArray[0].zoneIndexSet);
            }

            TEST_METHOD (MoveWindowIntoZoneByDirectionManyTimes)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                const auto window = Mocks::WindowCreate(m_hInst);
                workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);
                workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);
                workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_RIGHT, true);

                const auto& actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
                Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
                const auto& appHistoryArray = actualAppZoneHistory.begin()->second;
                Assert::AreEqual((size_t)1, appHistoryArray.size());
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == appHistoryArray[0].zoneIndexSet);
            }

            TEST_METHOD (SaveWindowProcessToZoneIndexNullptrWindow)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                workArea->SaveWindowProcessToZoneIndex(nullptr);

                const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
                Assert::IsTrue(actualAppZoneHistory.empty());
            }

            TEST_METHOD (SaveWindowProcessToZoneIndexNoWindowAdded)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                auto window = Mocks::WindowCreate(m_hInst);
                auto zone = MakeZone(RECT{ 0, 0, 100, 100 }, 1);
                workArea->ActiveZoneSet()->AddZone(zone);

                workArea->SaveWindowProcessToZoneIndex(window);

                const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
                Assert::IsTrue(actualAppZoneHistory.empty());
            }

            TEST_METHOD (SaveWindowProcessToZoneIndexNoWindowAddedWithFilledAppZoneHistory)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                const auto window = Mocks::WindowCreate(m_hInst);
                const auto processPath = get_process_path(window);
                const auto deviceId = workArea->UniqueId();
                const auto zoneSetId = workArea->ActiveZoneSet()->Id();

                // fill app zone history map
                Assert::IsTrue(m_fancyZonesData.SetAppLastZones(window, deviceId, Helpers::GuidToString(zoneSetId), { 0 }));
                Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
                const auto& appHistoryArray1 = m_fancyZonesData.GetAppZoneHistoryMap().at(processPath);
                Assert::AreEqual((size_t)1, appHistoryArray1.size());
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == appHistoryArray1[0].zoneIndexSet);

                // add zone without window
                const auto zone = MakeZone(RECT{ 0, 0, 100, 100 }, 1);
                workArea->ActiveZoneSet()->AddZone(zone);

                workArea->SaveWindowProcessToZoneIndex(window);
                Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
                const auto& appHistoryArray2 = m_fancyZonesData.GetAppZoneHistoryMap().at(processPath);
                Assert::AreEqual((size_t)1, appHistoryArray2.size());
                Assert::IsTrue(std::vector<ZoneIndex>{ 0 } == appHistoryArray2[0].zoneIndexSet);
            }

            TEST_METHOD (SaveWindowProcessToZoneIndexWindowAdded)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                auto window = Mocks::WindowCreate(m_hInst);
                const auto processPath = get_process_path(window);
                const auto deviceId = workArea->UniqueId();
                const auto zoneSetId = workArea->ActiveZoneSet()->Id();

                auto zone = MakeZone(RECT{ 0, 0, 100, 100 }, 1);
                workArea->ActiveZoneSet()->AddZone(zone);
                workArea->MoveWindowIntoZoneByIndex(window, 0);

                //fill app zone history map
                Assert::IsTrue(m_fancyZonesData.SetAppLastZones(window, deviceId, Helpers::GuidToString(zoneSetId), { 2 }));
                Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
                const auto& appHistoryArray = m_fancyZonesData.GetAppZoneHistoryMap().at(processPath);
                Assert::AreEqual((size_t)1, appHistoryArray.size());
                Assert::IsTrue(std::vector<ZoneIndex>{ 2 } == appHistoryArray[0].zoneIndexSet);

                workArea->SaveWindowProcessToZoneIndex(window);

                const auto& actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
                Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
                const auto& expected = workArea->ActiveZoneSet()->GetZoneIndexSetFromWindow(window);
                const auto& actual = appHistoryArray[0].zoneIndexSet;
                Assert::IsTrue(expected == actual);
            }

            TEST_METHOD (WhenWindowIsNotResizablePlacingItIntoTheZoneShouldNotResizeIt)
            {
                auto workArea = MakeWorkArea(m_hInst, m_monitor, m_uniqueId.str(), {}, m_zoneColors, m_overlappingAlgorithm);
                Assert::IsNotNull(workArea->ActiveZoneSet());

                auto window = Mocks::WindowCreate(m_hInst);

                int originalWidth = 450;
                int originalHeight = 550;

                SetWindowPos(window, nullptr, 150, 150, originalWidth, originalHeight, SWP_SHOWWINDOW);
                SetWindowLong(window, GWL_STYLE, GetWindowLong(window, GWL_STYLE) & ~WS_SIZEBOX);

                auto zone = MakeZone(RECT{ 50, 50, 300, 300 }, 1);
                workArea->ActiveZoneSet()->AddZone(zone);

                workArea->MoveWindowIntoZoneByDirectionAndIndex(window, VK_LEFT, true);

                RECT inZoneRect;
                GetWindowRect(window, &inZoneRect);
                Assert::AreEqual(originalWidth, (int)inZoneRect.right - (int)inZoneRect.left);
                Assert::AreEqual(originalHeight, (int)inZoneRect.bottom - (int)inZoneRect.top);
            }
    };
}
