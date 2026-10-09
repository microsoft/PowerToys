#include "pch.h"

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/util.h>

#include <FancyZonesTests/UnitTests/Util.h>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace FancyZonesUnitTests
{
    TEST_CLASS (WorkAreaIdComparison)
    {
        TEST_METHOD (MonitorHandleSame)
        {
            auto monitor = Mocks::Monitor();
            
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .monitor = monitor, .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .monitor = monitor, .deviceId = { .id = L"device-2", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
        }

        TEST_METHOD (MonitorHandleDifferent)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .monitor = Mocks::Monitor(), .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .monitor = Mocks::Monitor(), .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (VirtualDesktopDifferent)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{F21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (VirtualDesktopNull)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = GUID_NULL
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (DifferentSerialNumber)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"another-serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (DefaultMonitorIdDifferentInstanceIdSameNumber)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"another-instance-id", .number = 1 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
        }

        TEST_METHOD (DefaultMonitorIdDifferentInstanceIdDifferentNumber)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"another-instance-id", .number = 2 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (DefaultMonitorIdSameInstanceId)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
        }

        TEST_METHOD (DifferentId)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-2", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (SameIdDifferentSerialNumbers)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-2" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (DifferentIdSameSerialNumbers)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device-1", .instanceId = L"instance-id-1" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device-2", .instanceId = L"instance-id-2" }, .serialNumber = L"serial-number-1" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (MonitorReconnect)
        {
            // same: id, serial number and monitor number
            // different: instance id

            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID1", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID2", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
        }

        TEST_METHOD (SameMonitorModels)
        {
            // same: id, serial number
            // different: monitor number, instance id

            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID1", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"4&125707d6&0&UID2", .number = 2 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD(SerialNumberNotFoundError)
        {
            // serial number is empty, other values are the same

            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id", .number = 1 }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
        }
    };

}
