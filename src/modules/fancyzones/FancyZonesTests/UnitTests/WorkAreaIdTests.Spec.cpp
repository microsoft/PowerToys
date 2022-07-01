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

            Assert::IsTrue(id1 == id2);
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

        TEST_METHOD (NoSerialNumber)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"serial-number" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsFalse(id1 == id2);
        }

        TEST_METHOD (NoSerialNumber2)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
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

        TEST_METHOD (DefaultMonitorIdDifferentInstanceId)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"instance-id" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"Default_Monitor", .instanceId = L"another-instance-id" }, .serialNumber = L"" },
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

        TEST_METHOD (SameIdDifferentInstance)
        {
            FancyZonesDataTypes::WorkAreaId id1{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id-1" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            FancyZonesDataTypes::WorkAreaId id2{
                .monitorId = { .deviceId = { .id = L"device", .instanceId = L"instance-id-2" }, .serialNumber = L"" },
                .virtualDesktopId = FancyZonesUtils::GuidFromString(L"{E21F6F29-76FD-4FC1-8970-17AB8AD64847}").value()
            };

            Assert::IsTrue(id1 == id2);
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
    };

}
