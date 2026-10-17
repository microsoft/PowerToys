#include "pch.h"

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/MonitorUtils.h>
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

    TEST_CLASS (MonitorSerialNumberAssignment)
    {
        FancyZonesDataTypes::MonitorId Display(const std::wstring& id, const std::wstring& instanceId)
        {
            return FancyZonesDataTypes::MonitorId{ .deviceId = { .id = id, .instanceId = instanceId } };
        }

        FancyZonesDataTypes::MonitorId HardwareMonitor(const std::wstring& id, const std::wstring& instanceId, const std::wstring& serialNumber)
        {
            return FancyZonesDataTypes::MonitorId{ .deviceId = { .id = id, .instanceId = instanceId }, .serialNumber = serialNumber };
        }

        TEST_METHOD (SameModelMonitors)
        {
            std::vector<FancyZonesDataTypes::MonitorId> displays = {
                Display(L"DELF13B", L"5&2d51e510&0&UID4352"),
                Display(L"DELF13B", L"5&2d51e510&0&UID4354"),
            };

            std::vector<FancyZonesDataTypes::MonitorId> hardwareMonitors = {
                HardwareMonitor(L"DELF13B", L"5&2d51e510&0&UID4354", L"HK19904"),
                HardwareMonitor(L"DELF13B", L"5&2d51e510&0&UID4352", L"4S4L914"),
            };

            MonitorUtils::AssignSerialNumbers(displays, hardwareMonitors);

            Assert::AreEqual(std::wstring(L"4S4L914"), displays[0].serialNumber);
            Assert::AreEqual(std::wstring(L"HK19904"), displays[1].serialNumber);
        }

        TEST_METHOD (UniqueModelWithoutInstanceIdMatch)
        {
            std::vector<FancyZonesDataTypes::MonitorId> displays = {
                Display(L"DELF13B", L"5&2d51e510&0&UID4352"),
                Display(L"GSM5B7F", L"5&2d51e510&0&UID4354"),
            };

            std::vector<FancyZonesDataTypes::MonitorId> hardwareMonitors = {
                HardwareMonitor(L"DELF13B", L"4&125707d6&0&UID28741", L"4S4L914"),
                HardwareMonitor(L"GSM5B7F", L"4&125707d6&0&UID28742", L"123456"),
            };

            MonitorUtils::AssignSerialNumbers(displays, hardwareMonitors);

            Assert::AreEqual(std::wstring(L"4S4L914"), displays[0].serialNumber);
            Assert::AreEqual(std::wstring(L"123456"), displays[1].serialNumber);
        }

        TEST_METHOD (SameModelWithoutInstanceIdMatch)
        {
            std::vector<FancyZonesDataTypes::MonitorId> displays = {
                Display(L"DELF13B", L"5&2d51e510&0&UID4352"),
            };

            std::vector<FancyZonesDataTypes::MonitorId> hardwareMonitors = {
                HardwareMonitor(L"DELF13B", L"4&125707d6&0&UID28741", L"HK19904"),
                HardwareMonitor(L"DELF13B", L"4&125707d6&0&UID28742", L"4S4L914"),
            };

            MonitorUtils::AssignSerialNumbers(displays, hardwareMonitors);

            // Can't tell which one it is, so don't guess.
            Assert::IsTrue(displays[0].serialNumber.empty());
        }
    };
}
