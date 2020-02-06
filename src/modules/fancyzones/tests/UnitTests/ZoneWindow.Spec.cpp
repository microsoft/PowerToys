#include "pch.h"

#include <filesystem>

#include <common/common.h>
#include <lib/util.h>
#include <lib/ZoneSet.h>
#include <lib/ZoneWindow.h>
#include <lib/FancyZones.h>
#include "Util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    struct MockZoneWindowHost : public winrt::implements<MockZoneWindowHost, IZoneWindowHost>
    {
        IFACEMETHODIMP_(void)
        MoveWindowsOnActiveZoneSetChange() noexcept {};
        IFACEMETHODIMP_(COLORREF)
        GetZoneHighlightColor() noexcept
        {
            return RGB(0xFF, 0xFF, 0xFF);
        }
        IFACEMETHODIMP_(IZoneSet*)
        GetCurrentMonitorZoneSet(HMONITOR monitor) noexcept
        {
            return m_zoneSet;
        }
        IFACEMETHODIMP_(int)
        GetZoneHighlightOpacity() noexcept
        {
            return 100;
        }

        IZoneSet* m_zoneSet;
    };

    TEST_CLASS(ZoneWindowUnitTests)
    {
        const std::wstring m_deviceId = L"\\\\?\\DISPLAY#DELA026#5&10a58c63&0&UID16777488#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}";
        const std::wstring m_virtualDesktopId = L"MyVirtualDesktopId";
        std::wstringstream m_uniqueId;

        HINSTANCE m_hInst{};
        HMONITOR m_monitor{};
        MONITORINFO m_monitorInfo{};
        MockZoneWindowHost m_zoneWindowHost{};
        IZoneWindowHost* m_hostPtr = m_zoneWindowHost.get_strong().get();

        winrt::com_ptr<IZoneWindow> m_zoneWindow;

        JSONHelpers::FancyZonesData& m_fancyZonesData = JSONHelpers::FancyZonesDataInstance();

        std::wstring GuidString(const GUID& guid)
        {
            OLECHAR* guidString;
            Assert::AreEqual(S_OK, StringFromCLSID(guid, &guidString));

            std::wstring guidStr{ guidString };
            CoTaskMemFree(guidString);

            return guidStr;
        }

        std::wstring CreateGuidString()
        {
            GUID guid;
            Assert::AreEqual(S_OK, CoCreateGuid(&guid));

            return GuidString(guid);           
        }

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);

            m_monitor = MonitorFromPoint(POINT{ 0, 0 }, MONITOR_DEFAULTTOPRIMARY);
            m_monitorInfo.cbSize = sizeof(m_monitorInfo);
            Assert::AreNotEqual(0, GetMonitorInfoW(m_monitor, &m_monitorInfo));

            m_uniqueId << L"DELA026#5&10a58c63&0&UID16777488_" << m_monitorInfo.rcMonitor.right << "_" << m_monitorInfo.rcMonitor.bottom << "_MyVirtualDesktopId";

            Assert::IsFalse(ZoneWindowUtils::GetActiveZoneSetTmpPath().empty());
            Assert::IsFalse(ZoneWindowUtils::GetAppliedZoneSetTmpPath().empty());
            Assert::IsFalse(ZoneWindowUtils::GetCustomZoneSetsTmpPath().empty());

            Assert::IsFalse(std::filesystem::exists(ZoneWindowUtils::GetActiveZoneSetTmpPath()));
            Assert::IsFalse(std::filesystem::exists(ZoneWindowUtils::GetAppliedZoneSetTmpPath()));
            Assert::IsFalse(std::filesystem::exists(ZoneWindowUtils::GetCustomZoneSetsTmpPath()));

            m_fancyZonesData = JSONHelpers::FancyZonesData();
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            //cleanup temp files if were created
            std::filesystem::remove(ZoneWindowUtils::GetActiveZoneSetTmpPath());
            std::filesystem::remove(ZoneWindowUtils::GetAppliedZoneSetTmpPath());
            std::filesystem::remove(ZoneWindowUtils::GetCustomZoneSetsTmpPath());

            m_zoneWindow = nullptr;
        }

        winrt::com_ptr<IZoneWindow> InitZoneWindowWithActiveZoneSet()
        {
            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();
            Assert::IsFalse(std::filesystem::exists(activeZoneSetTempPath));

            const auto type = JSONHelpers::ZoneSetLayoutType::Columns;
            const auto expectedZoneSet = JSONHelpers::ZoneSetData{ CreateGuidString(), type};
            const auto data = JSONHelpers::DeviceInfoData{ expectedZoneSet, true, 16, 3 };
            const auto deviceInfo = JSONHelpers::DeviceInfoJSON{ m_uniqueId.str(), data };
            const auto json = JSONHelpers::DeviceInfoJSON::ToJson(deviceInfo);
            json::to_file(activeZoneSetTempPath, json);
            Assert::IsTrue(std::filesystem::exists(activeZoneSetTempPath));

            m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);

            return MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);
        }

        void testZoneWindow(winrt::com_ptr<IZoneWindow> zoneWindow)
        {
            const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);

            Assert::IsNotNull(zoneWindow.get());
            Assert::IsFalse(zoneWindow->IsDragEnabled());
            Assert::AreEqual(m_uniqueId.str().c_str(), zoneWindow->UniqueId().c_str());
            Assert::AreEqual(expectedWorkArea, zoneWindow->WorkAreaKey());
        }

    public:
        TEST_METHOD(CreateZoneWindow)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);
            testZoneWindow(m_zoneWindow);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
        }

        TEST_METHOD(CreateZoneWindowNoHinst)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, {}, m_monitor, m_uniqueId.str(), false);

            testZoneWindow(m_zoneWindow);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
        }

        TEST_METHOD(CreateZoneWindowNoHinstFlashZones)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, {}, m_monitor, m_uniqueId.str(), true);

            testZoneWindow(m_zoneWindow);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
        }

        TEST_METHOD(CreateZoneWindowNoMonitor)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, {}, m_uniqueId.str(), false);

            Assert::IsNull(m_zoneWindow.get());
            Assert::IsNotNull(m_hostPtr);
        }

        TEST_METHOD(CreateZoneWindowNoMonitorFlashZones)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, {}, m_uniqueId.str(), true);

            Assert::IsNull(m_zoneWindow.get());
            Assert::IsNotNull(m_hostPtr);
        }

        TEST_METHOD(CreateZoneWindowNoDeviceId)
        {
            // Generate unique id without device id
            std::wstring uniqueId = ZoneWindowUtils::GenerateUniqueId(m_monitor, nullptr, m_virtualDesktopId.c_str());
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, uniqueId, false);

            const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);
            const std::wstring expectedUniqueId = L"FallbackDevice_" + std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom) + L"_" + m_virtualDesktopId;

            Assert::IsNotNull(m_zoneWindow.get());
            Assert::IsFalse(m_zoneWindow->IsDragEnabled());
            Assert::AreEqual(expectedUniqueId.c_str(), m_zoneWindow->UniqueId().c_str());
            Assert::AreEqual(expectedWorkArea, m_zoneWindow->WorkAreaKey());
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
        }

        TEST_METHOD(CreateZoneWindowNoDesktopId)
        {
            // Generate unique id without virtual desktop id
            std::wstring uniqueId = ZoneWindowUtils::GenerateUniqueId(m_monitor, m_deviceId.c_str(), nullptr);
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, uniqueId, false);

            const std::wstring expectedWorkArea = std::to_wstring(m_monitorInfo.rcMonitor.right) + L"_" + std::to_wstring(m_monitorInfo.rcMonitor.bottom);
            Assert::IsNotNull(m_zoneWindow.get());
            Assert::IsFalse(m_zoneWindow->IsDragEnabled());
            Assert::IsTrue(m_zoneWindow->UniqueId().empty());
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());
        }

        TEST_METHOD(CreateZoneWindowWithActiveZoneTmpFile)
        {
            using namespace JSONHelpers;

            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();

            for (int type = static_cast<int>(ZoneSetLayoutType::Focus); type < static_cast<int>(ZoneSetLayoutType::Custom); type++)
            {
                const auto expectedZoneSet = ZoneSetData{ CreateGuidString(), static_cast<ZoneSetLayoutType>(type) };
                const auto data = DeviceInfoData{ expectedZoneSet, true, 16, 3 };
                const auto deviceInfo = DeviceInfoJSON{ m_uniqueId.str(), data };
                const auto json = DeviceInfoJSON::ToJson(deviceInfo);
                json::to_file(activeZoneSetTempPath, json);

                m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);

                //temp file read on initialization
                auto actual = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

                testZoneWindow(actual);

                Assert::IsNotNull(actual->ActiveZoneSet());
            }
        }

        TEST_METHOD(CreateZoneWindowWithActiveCustomZoneTmpFile)
        {
            using namespace JSONHelpers;

            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();

            const ZoneSetLayoutType type = ZoneSetLayoutType::Custom;
            const auto expectedZoneSet = ZoneSetData{ CreateGuidString(), type };
            const auto data = DeviceInfoData{ expectedZoneSet, true, 16, 3 };
            const auto deviceInfo = DeviceInfoJSON{ m_uniqueId.str(), data };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);
            json::to_file(activeZoneSetTempPath, json);

            m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);

            //temp file read on initialization
            auto actual = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            testZoneWindow(actual);

            //custom zone needs temp file for applied zone
            Assert::IsNotNull(actual->ActiveZoneSet());
            const auto actualZoneSet = actual->ActiveZoneSet()->GetZones();
            Assert::AreEqual((size_t)0, actualZoneSet.size());
        }

        TEST_METHOD(CreateZoneWindowWithActiveCustomZoneAppliedTmpFile)
        {
            using namespace JSONHelpers;

            //save required data
            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();
            const auto appliedZoneSetTempPath = ZoneWindowUtils::GetAppliedZoneSetTmpPath();

            const ZoneSetLayoutType type = ZoneSetLayoutType::Custom;
            const auto customSetGuid = CreateGuidString();
            const auto expectedZoneSet = ZoneSetData{ customSetGuid, type};
            const auto data = DeviceInfoData{ expectedZoneSet, true, 16, 3 };
            const auto deviceInfo = DeviceInfoJSON{ m_uniqueId.str(), data };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);
            json::to_file(activeZoneSetTempPath, json);

            const auto info = CanvasLayoutInfo{
                100, 100, std::vector{ CanvasLayoutInfo::Rect{ 0, 0, 100, 100 } }
            };
            const auto customZoneData = CustomZoneSetData{ L"name", CustomLayoutType::Canvas, info };
            auto customZoneJson = CustomZoneSetJSON::ToJson(CustomZoneSetJSON{ customSetGuid, customZoneData });
            json::to_file(appliedZoneSetTempPath, customZoneJson);
            m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);

            //temp file read on initialization
            auto actual = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            testZoneWindow(actual);

            //custom zone needs temp file for applied zone
            Assert::IsNotNull(actual->ActiveZoneSet());
            const auto actualZoneSet = actual->ActiveZoneSet()->GetZones();
            Assert::AreEqual((size_t)1, actualZoneSet.size());
        }

        TEST_METHOD(CreateZoneWindowWithActiveCustomZoneAppliedTmpFileWithDeletedCustomZones)
        {
            using namespace JSONHelpers;

            //save required data
            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();
            const auto appliedZoneSetTempPath = ZoneWindowUtils::GetAppliedZoneSetTmpPath();
            const auto deletedZonesTempPath = ZoneWindowUtils::GetCustomZoneSetsTmpPath();

            const ZoneSetLayoutType type = ZoneSetLayoutType::Custom;
            const auto customSetGuid = CreateGuidString();
            const auto expectedZoneSet = ZoneSetData{ customSetGuid, type };
            const auto data = DeviceInfoData{ expectedZoneSet, true, 16, 3 };
            const auto deviceInfo = DeviceInfoJSON{ m_uniqueId.str(), data };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);
            json::to_file(activeZoneSetTempPath, json);

            const auto info = CanvasLayoutInfo{
                100, 100, std::vector{ CanvasLayoutInfo::Rect{ 0, 0, 100, 100 } }
            };
            const auto customZoneData = CustomZoneSetData{ L"name", CustomLayoutType::Canvas, info };
            const auto customZoneSet = CustomZoneSetJSON{ customSetGuid, customZoneData };
            auto customZoneJson = CustomZoneSetJSON::ToJson(customZoneSet);
            json::to_file(appliedZoneSetTempPath, customZoneJson);

            //save same zone as deleted
            json::JsonObject deletedCustomZoneSets = {};
            json::JsonArray zonesArray{};
            zonesArray.Append(json::JsonValue::CreateStringValue(customZoneSet.uuid.substr(1, customZoneSet.uuid.size() - 2).c_str()));
            deletedCustomZoneSets.SetNamedValue(L"deleted-custom-zone-sets", zonesArray);
            json::to_file(deletedZonesTempPath, deletedCustomZoneSets);

            m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);
            m_fancyZonesData.ParseDeletedCustomZoneSetsFromTmpFile(deletedZonesTempPath);

            //temp file read on initialization
            auto actual = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            testZoneWindow(actual);

            Assert::IsNotNull(actual->ActiveZoneSet());
            const auto actualZoneSet = actual->ActiveZoneSet()->GetZones();
            Assert::AreEqual((size_t)1, actualZoneSet.size());
        }

        TEST_METHOD(CreateZoneWindowWithActiveCustomZoneAppliedTmpFileWithUnusedDeletedCustomZones)
        {
            using namespace JSONHelpers;

            //save required data
            const auto activeZoneSetTempPath = ZoneWindowUtils::GetActiveZoneSetTmpPath();
            const auto appliedZoneSetTempPath = ZoneWindowUtils::GetAppliedZoneSetTmpPath();
            const auto deletedZonesTempPath = ZoneWindowUtils::GetCustomZoneSetsTmpPath();

            const ZoneSetLayoutType type = ZoneSetLayoutType::Custom;
            const auto customSetGuid = CreateGuidString();
            const auto expectedZoneSet = ZoneSetData{ customSetGuid, type };
            const auto data = DeviceInfoData{ expectedZoneSet, true, 16, 3 };
            const auto deviceInfo = DeviceInfoJSON{ m_uniqueId.str(), data };
            const auto json = DeviceInfoJSON::ToJson(deviceInfo);
            json::to_file(activeZoneSetTempPath, json);

            const auto info = CanvasLayoutInfo{
                100, 100, std::vector{ CanvasLayoutInfo::Rect{ 0, 0, 100, 100 } }
            };
            const auto customZoneData = CustomZoneSetData{ L"name", CustomLayoutType::Canvas, info };
            const auto customZoneSet = CustomZoneSetJSON{ customSetGuid, customZoneData };
            auto customZoneJson = CustomZoneSetJSON::ToJson(customZoneSet);
            json::to_file(appliedZoneSetTempPath, customZoneJson);

            //save different zone as deleted
            json::JsonObject deletedCustomZoneSets = {};
            json::JsonArray zonesArray{};
            const auto uuid = CreateGuidString();
            zonesArray.Append(json::JsonValue::CreateStringValue(uuid.substr(1, uuid.size() - 2).c_str()));
            deletedCustomZoneSets.SetNamedValue(L"deleted-custom-zone-sets", zonesArray);
            json::to_file(deletedZonesTempPath, deletedCustomZoneSets);

            m_fancyZonesData.ParseDeviceInfoFromTmpFile(activeZoneSetTempPath);
            m_fancyZonesData.ParseDeletedCustomZoneSetsFromTmpFile(deletedZonesTempPath);

            //temp file read on initialization
            auto actual = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            testZoneWindow(actual);

            Assert::IsNotNull(actual->ActiveZoneSet());
            const auto actualZoneSet = actual->ActiveZoneSet()->GetZones();
            Assert::AreEqual((size_t)1, actualZoneSet.size());
        }

        TEST_METHOD(MoveSizeEnter)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = S_OK;
            const auto actual = m_zoneWindow->MoveSizeEnter(Mocks::Window(), true);

            Assert::AreEqual(expected, actual);
            Assert::IsTrue(m_zoneWindow->IsDragEnabled());
        }

        TEST_METHOD(MoveSizeEnterTwice)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = E_INVALIDARG;

            m_zoneWindow->MoveSizeEnter(Mocks::Window(), true);
            const auto actual = m_zoneWindow->MoveSizeEnter(Mocks::Window(), false);

            Assert::AreEqual(expected, actual);
            Assert::IsTrue(m_zoneWindow->IsDragEnabled());
        }

        TEST_METHOD(MoveSizeUpdate)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = S_OK;
            const auto actual = m_zoneWindow->MoveSizeUpdate(POINT{ 0, 0 }, true);

            Assert::AreEqual(expected, actual);
            Assert::IsTrue(m_zoneWindow->IsDragEnabled());
        }

        TEST_METHOD(MoveSizeUpdatePointNegativeCoordinates)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = S_OK;
            const auto actual = m_zoneWindow->MoveSizeUpdate(POINT{ -10, -10 }, true);

            Assert::AreEqual(expected, actual);
            Assert::IsTrue(m_zoneWindow->IsDragEnabled());
        }

        TEST_METHOD(MoveSizeUpdatePointBigCoordinates)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = S_OK;
            const auto actual = m_zoneWindow->MoveSizeUpdate(POINT{ m_monitorInfo.rcMonitor.right + 1, m_monitorInfo.rcMonitor.bottom + 1 }, true);

            Assert::AreEqual(expected, actual);
            Assert::IsTrue(m_zoneWindow->IsDragEnabled());
        }

        TEST_METHOD(MoveSizeEnd)
        {
            auto zoneWindow = InitZoneWindowWithActiveZoneSet();

            const auto window = Mocks::Window();
            zoneWindow->MoveSizeEnter(window, true);

            const auto expected = S_OK;
            const auto actual = zoneWindow->MoveSizeEnd(window, POINT{ 0, 0 });
            Assert::AreEqual(expected, actual);

            const auto zoneSet = zoneWindow->ActiveZoneSet();
            zoneSet->MoveWindowIntoZoneByIndex(window, Mocks::Window(), false);
            const auto actualZoneIndex = zoneSet->GetZoneIndexFromWindow(window);
            Assert::AreNotEqual(-1, actualZoneIndex);
        }

        TEST_METHOD(MoveSizeEndWindowNotAdded)
        {
            auto zoneWindow = InitZoneWindowWithActiveZoneSet();

            const auto window = Mocks::Window();
            zoneWindow->MoveSizeEnter(window, true);

            const auto expected = S_OK;
            const auto actual = zoneWindow->MoveSizeEnd(window, POINT{ 0, 0 });
            Assert::AreEqual(expected, actual);

            const auto zoneSet = zoneWindow->ActiveZoneSet();
            const auto actualZoneIndex = zoneSet->GetZoneIndexFromWindow(window);
            Assert::AreEqual(-1, actualZoneIndex);
        }

        TEST_METHOD(MoveSizeEndDifferentWindows)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto window = Mocks::Window();
            m_zoneWindow->MoveSizeEnter(window, true);

            const auto expected = E_INVALIDARG;
            const auto actual = m_zoneWindow->MoveSizeEnd(Mocks::Window(), POINT{ 0, 0 });

            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(MoveSizeEndWindowNotSet)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = E_INVALIDARG;
            const auto actual = m_zoneWindow->MoveSizeEnd(Mocks::Window(), POINT{ 0, 0 });

            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(MoveSizeEndInvalidPoint)
        {
            auto zoneWindow = InitZoneWindowWithActiveZoneSet();

            const auto window = Mocks::Window();
            zoneWindow->MoveSizeEnter(window, true);

            const auto expected = S_OK;
            const auto actual = zoneWindow->MoveSizeEnd(window, POINT{ -1, -1 });
            Assert::AreEqual(expected, actual);

            const auto zoneSet = zoneWindow->ActiveZoneSet();
            zoneSet->MoveWindowIntoZoneByIndex(window, Mocks::Window(), false);
            const auto actualZoneIndex = zoneSet->GetZoneIndexFromWindow(window);
            Assert::AreNotEqual(-1, actualZoneIndex); //with invalid point zone remains the same
        }

        TEST_METHOD(MoveSizeCancel)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);

            const auto expected = S_OK;
            const auto actual = m_zoneWindow->MoveSizeCancel();

            Assert::AreEqual(expected, actual);
        }

        TEST_METHOD(MoveWindowIntoZoneByIndexNoActiveZoneSet)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());

            m_zoneWindow->MoveWindowIntoZoneByIndex(Mocks::Window(), 0);
        }

        TEST_METHOD(MoveWindowIntoZoneByIndex)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            m_zoneWindow->MoveWindowIntoZoneByIndex(Mocks::Window(), 0);

            const auto actual = m_zoneWindow->ActiveZoneSet();
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionNoActiveZoneSet)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());

            m_zoneWindow->MoveWindowIntoZoneByIndex(Mocks::Window(), 0);
        }

        TEST_METHOD(MoveWindowIntoZoneByDirection)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            const auto window = Mocks::WindowCreate(m_hInst);
            m_zoneWindow->MoveWindowIntoZoneByDirection(window, VK_RIGHT);

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
            const auto actual = actualAppZoneHistory.begin()->second;
            Assert::AreEqual(0, actual.zoneIndex);
        }

        TEST_METHOD(MoveWindowIntoZoneByDirectionManyTimes)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            const auto window = Mocks::WindowCreate(m_hInst);
            m_zoneWindow->MoveWindowIntoZoneByDirection(window, VK_RIGHT);
            m_zoneWindow->MoveWindowIntoZoneByDirection(window, VK_RIGHT);
            m_zoneWindow->MoveWindowIntoZoneByDirection(window, VK_RIGHT);

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
            const auto actual = actualAppZoneHistory.begin()->second;
            Assert::AreEqual(2, actual.zoneIndex);
        }

        TEST_METHOD(SaveWindowProcessToZoneIndexNoActiveZoneSet)
        {
            m_zoneWindow = MakeZoneWindow(m_hostPtr, m_hInst, m_monitor, m_uniqueId.str(), false);
            Assert::IsNull(m_zoneWindow->ActiveZoneSet());

            m_zoneWindow->SaveWindowProcessToZoneIndex(Mocks::Window());

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::IsTrue(actualAppZoneHistory.empty());
        }

        TEST_METHOD(SaveWindowProcessToZoneIndexNullptrWindow)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            m_zoneWindow->SaveWindowProcessToZoneIndex(nullptr);

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::IsTrue(actualAppZoneHistory.empty());
        }

        TEST_METHOD(SaveWindowProcessToZoneIndexNoWindowAdded)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            auto window = Mocks::WindowCreate(m_hInst);
            auto zone = MakeZone(RECT{ 0, 0, 100, 100 });
            m_zoneWindow->ActiveZoneSet()->AddZone(zone);

            m_zoneWindow->SaveWindowProcessToZoneIndex(window);

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::IsTrue(actualAppZoneHistory.empty());
        }

        TEST_METHOD(SaveWindowProcessToZoneIndexNoWindowAddedWithFilledAppZoneHistory)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            const auto window = Mocks::WindowCreate(m_hInst);
            const auto processPath = get_process_path(window);
            const auto deviceId = m_zoneWindow->UniqueId();
            const auto zoneSetId = m_zoneWindow->ActiveZoneSet()->Id();
            
            //fill app zone history map
            Assert::IsTrue(m_fancyZonesData.SetAppLastZone(window, deviceId, GuidString(zoneSetId), 0));
            Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
            Assert::AreEqual(0, m_fancyZonesData.GetAppZoneHistoryMap().at(processPath).zoneIndex);

            //add zone without window
            const auto zone = MakeZone(RECT{ 0, 0, 100, 100 });
            m_zoneWindow->ActiveZoneSet()->AddZone(zone);

            m_zoneWindow->SaveWindowProcessToZoneIndex(window);
            Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
            Assert::AreEqual(0, m_fancyZonesData.GetAppZoneHistoryMap().at(processPath).zoneIndex);
        }

        TEST_METHOD(SaveWindowProcessToZoneIndexWindowAdded)
        {
            m_zoneWindow = InitZoneWindowWithActiveZoneSet();
            Assert::IsNotNull(m_zoneWindow->ActiveZoneSet());

            auto window = Mocks::WindowCreate(m_hInst);
            const auto processPath = get_process_path(window);
            const auto deviceId = m_zoneWindow->UniqueId();
            const auto zoneSetId = m_zoneWindow->ActiveZoneSet()->Id();

            auto zone = MakeZone(RECT{ 0, 0, 100, 100 });
            zone->AddWindowToZone(window, Mocks::Window(), false);
            m_zoneWindow->ActiveZoneSet()->AddZone(zone);

            //fill app zone history map
            Assert::IsTrue(m_fancyZonesData.SetAppLastZone(window, deviceId, GuidString(zoneSetId), 2));
            Assert::AreEqual((size_t)1, m_fancyZonesData.GetAppZoneHistoryMap().size());
            Assert::AreEqual(2, m_fancyZonesData.GetAppZoneHistoryMap().at(processPath).zoneIndex);

            m_zoneWindow->SaveWindowProcessToZoneIndex(window);

            const auto actualAppZoneHistory = m_fancyZonesData.GetAppZoneHistoryMap();
            Assert::AreEqual((size_t)1, actualAppZoneHistory.size());
            const auto expected = m_zoneWindow->ActiveZoneSet()->GetZoneIndexFromWindow(window);
            const auto actual = m_fancyZonesData.GetAppZoneHistoryMap().at(processPath).zoneIndex;
            Assert::AreEqual(expected, actual);
        }
    };
}
