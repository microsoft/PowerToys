#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>

#include "util.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (AppZoneHistoryUnitTests)
    {
        HINSTANCE m_hInst{};

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = (HINSTANCE)GetModuleHandleW(nullptr);
            AppZoneHistory::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppZoneHistory::instance().AppZoneHistoryFileName());
        }

        TEST_METHOD (AppLastZoneInvalidWindow)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::Window();

            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetId));

            const int expectedZoneIndex = 1;
            Assert::IsFalse(AppZoneHistory::instance().SetAppLastZones(window, deviceId, zoneSetId, { expectedZoneIndex }));
        }

        TEST_METHOD (AppLastZoneNullWindow)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = nullptr;

            const int expectedZoneIndex = 1;
            Assert::IsFalse(AppZoneHistory::instance().SetAppLastZones(window, deviceId, zoneSetId, { expectedZoneIndex }));
        }

        TEST_METHOD (AppLastdeviceIdTest)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceId1{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const FancyZonesDataTypes::DeviceIdData deviceId2{ L"DELA026#5&10a58c63&0&UID16777489_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            const int expectedZoneIndex = 10;
            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, deviceId1, zoneSetId, { expectedZoneIndex }));
            Assert::IsTrue(std::vector<ZoneIndex>{ expectedZoneIndex } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId1, zoneSetId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId2, zoneSetId));
        }

        TEST_METHOD (AppLastZoneSetIdTest)
        {
            const std::wstring zoneSetId1 = L"{B7A1F5A9-9DC2-4505-84AB-993253839093}";
            const std::wstring zoneSetId2 = L"{B7A1F5A9-9DC2-4505-84AB-993253839094}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            const int expectedZoneIndex = 10;
            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, deviceId, zoneSetId1, { expectedZoneIndex }));
            Assert::IsTrue(std::vector<ZoneIndex>{ expectedZoneIndex } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetId1));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetId2));
        }

        TEST_METHOD (AppLastZoneRemoveWindow)
        {
            const std::wstring zoneSetId = L"{B7A1F5A9-9DC2-4505-84AB-993253839093}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, deviceId, zoneSetId, { 1 }));
            Assert::IsTrue(AppZoneHistory::instance().RemoveAppLastZone(window, deviceId, zoneSetId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetId));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownWindow)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, deviceId, zoneSetId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetId));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownZoneSetId)
        {
            const std::wstring zoneSetIdToInsert = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const std::wstring zoneSetIdToRemove = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F1}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, deviceId, zoneSetIdToInsert, { 1 }));
            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, deviceId, zoneSetIdToRemove));
            Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceId, zoneSetIdToInsert));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownWindowId)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceIdToInsert{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const FancyZonesDataTypes::DeviceIdData deviceIdToRemove{ L"DELA026#5&10a58c63&0&UID16777489_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, deviceIdToInsert, zoneSetId, { 1 }));
            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, deviceIdToRemove, zoneSetId));
            Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, deviceIdToInsert, zoneSetId));
        }

        TEST_METHOD (AppLastZoneRemoveNullWindow)
        {
            const std::wstring zoneSetId = L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}";
            const FancyZonesDataTypes::DeviceIdData deviceId{ L"DELA026#5&10a58c63&0&UID16777488_2194_1234_{39B25DD2-130D-4B5D-8851-4791D66B1539}" };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(nullptr, deviceId, zoneSetId));
        }
    };
}