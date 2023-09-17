#include "pch.h"
#include <filesystem>

#include <FancyZonesLib/FancyZonesData/AppZoneHistory.h>

#include "util.h"
#include <modules/fancyzones/FancyZonesLib/util.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (AppZoneHistoryUnitTests)
    {
        HINSTANCE m_hInst{};

        TEST_METHOD_INITIALIZE(Init)
        {
            m_hInst = static_cast<HINSTANCE>(GetModuleHandleW(nullptr));
            AppZoneHistory::instance().LoadData();
        }

        TEST_METHOD_CLEANUP(CleanUp)
        {
            std::filesystem::remove(AppZoneHistory::instance().AppZoneHistoryFileName());
        }

        TEST_METHOD (AppZoneHistoryParse)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray appZoneHistoryArray{};

            {
                json::JsonArray history{};
                {
                    json::JsonObject device{};
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(L"monitor-1"));
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}"));

                    json::JsonArray zones{};
                    zones.Append(json::value(0));
                    zones.Append(json::value(1));

                    json::JsonObject historyObj{};
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, zones);

                    history.Append(historyObj);
                }
                {
                    json::JsonObject device{};
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(L"monitor-2"));
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}"));

                    json::JsonArray zones{};
                    zones.Append(json::value(2));

                    json::JsonObject historyObj{};
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, zones);

                    history.Append(historyObj);
                }

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppPathID, json::value(L"app-1"));
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::HistoryID, history);
                appZoneHistoryArray.Append(obj);
            }
            {
                json::JsonArray history{};
                {
                    json::JsonObject device{};
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(L"monitor-1"));
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}"));

                    json::JsonArray zones{};
                    zones.Append(json::value(0));

                    json::JsonObject historyObj{};
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, zones);

                    history.Append(historyObj);
                }

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppPathID, json::value(L"app-2"));
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::HistoryID, history);
                appZoneHistoryArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppZoneHistoryID, appZoneHistoryArray);
            json::to_file(AppZoneHistory::AppZoneHistoryFileName(), root);

            // test
            AppZoneHistory::instance().LoadData();
            Assert::AreEqual((size_t)2, AppZoneHistory::instance().GetFullAppZoneHistory().size());

            {
                FancyZonesDataTypes::WorkAreaId id{
                    .monitorId = { .deviceId = { .id = L"monitor-1" } },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}").value()
                };
                Assert::IsTrue(AppZoneHistory::instance().GetZoneHistory(L"app-1", id).has_value());
            }
            {
                FancyZonesDataTypes::WorkAreaId id{
                    .monitorId = { .deviceId = { .id = L"monitor-2" } },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}").value()
                };
                Assert::IsTrue(AppZoneHistory::instance().GetZoneHistory(L"app-1", id).has_value());
            }
            {
                FancyZonesDataTypes::WorkAreaId id{
                    .monitorId = { .deviceId = { .id = L"monitor-1" } },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}").value()
                };
                Assert::IsTrue(AppZoneHistory::instance().GetZoneHistory(L"app-2", id).has_value());
            }
        }

        TEST_METHOD (AppZoneHistoryParseEmpty)
        {
            // prepare
            json::JsonObject root{};
            json::to_file(AppZoneHistory::AppZoneHistoryFileName(), root);

            // test
            AppZoneHistory::instance().LoadData();
            Assert::IsTrue(AppZoneHistory::instance().GetFullAppZoneHistory().empty());
        }

        TEST_METHOD (AppZoneHistoryParseInvalid)
        {
            // prepare
            json::JsonObject root{};
            json::JsonArray appZoneHistoryArray{};

            {
                json::JsonArray history{};
                {
                    json::JsonObject device{};
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(L"monitor-1"));
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}"));

                    json::JsonArray zones{};
                    zones.Append(json::value(0));
                    zones.Append(json::value(1));

                    json::JsonObject historyObj{};
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, zones);

                    history.Append(historyObj);
                }

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppPathID, json::value(L"app-1"));
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::HistoryID, history);
                appZoneHistoryArray.Append(obj);
            }
            {
                json::JsonArray history{};
                {
                    json::JsonObject device{};
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::MonitorID, json::value(L"monitor-1"));
                    device.SetNamedValue(NonLocalizable::AppZoneHistoryIds::VirtualDesktopID, json::value(L"{72FA9FC0-26A6-4B37-A834-}"));

                    json::JsonArray zones{};
                    zones.Append(json::value(0));

                    json::JsonObject historyObj{};
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIdID, json::value(L"{61FA9FC0-26A6-4B37-A834-491C148DFC57}"));
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::DeviceID, device);
                    historyObj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::LayoutIndexesID, zones);

                    history.Append(historyObj);
                }

                json::JsonObject obj{};
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppPathID, json::value(L"app-2"));
                obj.SetNamedValue(NonLocalizable::AppZoneHistoryIds::HistoryID, history);
                appZoneHistoryArray.Append(obj);
            }

            root.SetNamedValue(NonLocalizable::AppZoneHistoryIds::AppZoneHistoryID, appZoneHistoryArray);
            json::to_file(AppZoneHistory::AppZoneHistoryFileName(), root);

            // test
            AppZoneHistory::instance().LoadData();
            Assert::AreEqual((size_t)1, AppZoneHistory::instance().GetFullAppZoneHistory().size());

            {
                FancyZonesDataTypes::WorkAreaId id{
                    .monitorId = { .deviceId = { .id = L"monitor-1" } },
                    .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{72FA9FC0-26A6-4B37-A834-491C148DFC58}").value()
                };
                Assert::IsTrue(AppZoneHistory::instance().GetZoneHistory(L"app-1", id).has_value());
            }
        }

        TEST_METHOD (AppLastZoneInvalidWindow)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{ 
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::Window();

            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutId));

            const int expectedZoneIndex = 1;
            Assert::IsFalse(AppZoneHistory::instance().SetAppLastZones(window, workAreaId, layoutId, { expectedZoneIndex }));
        }

        TEST_METHOD (AppLastZoneNullWindow)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = nullptr;

            const int expectedZoneIndex = 1;
            Assert::IsFalse(AppZoneHistory::instance().SetAppLastZones(window, workAreaId, layoutId, { expectedZoneIndex }));
        }

        TEST_METHOD (AppLastdeviceIdTest)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId1{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const FancyZonesDataTypes::WorkAreaId workAreaId2{
                .monitorId = { .deviceId = { .id = L"DELA027", .instanceId = L"5&10a58c63&0&UID16777489" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            const int expectedZoneIndex = 10;
            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, workAreaId1, layoutId, { expectedZoneIndex }));
            Assert::IsTrue(std::vector<ZoneIndex>{ expectedZoneIndex } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId1, layoutId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId2, layoutId));
        }

        TEST_METHOD (AppLastZoneSetIdTest)
        {
            const auto layoutId1 = FancyZonesUtils::GuidFromString(L"{B7A1F5A9-9DC2-4505-84AB-993253839093}").value();
            const auto layoutId2 = FancyZonesUtils::GuidFromString(L"{B7A1F5A9-9DC2-4505-84AB-993253839094}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            const int expectedZoneIndex = 10;
            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, workAreaId, layoutId1, { expectedZoneIndex }));
            Assert::IsTrue(std::vector<ZoneIndex>{ expectedZoneIndex } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutId1));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutId2));
        }

        TEST_METHOD (AppLastZoneRemoveWindow)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{B7A1F5A9-9DC2-4505-84AB-993253839093}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, workAreaId, layoutId, { 1 }));
            Assert::IsTrue(AppZoneHistory::instance().RemoveAppLastZone(window, workAreaId, layoutId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutId));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownWindow)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, workAreaId, layoutId));
            Assert::IsTrue(std::vector<ZoneIndex>{} == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutId));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownZoneSetId)
        {
            const auto layoutIdToInsert = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const auto layoutIdToRemove = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F1}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, workAreaId, layoutIdToInsert, { 1 }));
            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, workAreaId, layoutIdToRemove));
            Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaId, layoutIdToInsert));
        }

        TEST_METHOD (AppLastZoneRemoveUnknownWindowId)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaIdToInsert{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const FancyZonesDataTypes::WorkAreaId workAreaIdToRemove{
                .monitorId = { .deviceId = { .id = L"DELA027", .instanceId = L"5&10a58c63&0&UID16777489" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };
            const auto window = Mocks::WindowCreate(m_hInst);

            Assert::IsTrue(AppZoneHistory::instance().SetAppLastZones(window, workAreaIdToInsert, layoutId, { 1 }));
            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(window, workAreaIdToRemove, layoutId));
            Assert::IsTrue(std::vector<ZoneIndex>{ 1 } == AppZoneHistory::instance().GetAppLastZoneIndexSet(window, workAreaIdToInsert, layoutId));
        }

        TEST_METHOD (AppLastZoneRemoveNullWindow)
        {
            const auto layoutId = FancyZonesUtils::GuidFromString(L"{2FEC41DA-3A0B-4E31-9CE1-9473C65D99F2}").value();
            const FancyZonesDataTypes::WorkAreaId workAreaId{
                .monitorId = { .deviceId = { .id = L"DELA026", .instanceId = L"5&10a58c63&0&UID16777488" } },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{39B25DD2-130D-4B5D-8851-4791D66B1539}").value()
            };

            Assert::IsFalse(AppZoneHistory::instance().RemoveAppLastZone(nullptr, workAreaId, layoutId));
        }
    };
}
