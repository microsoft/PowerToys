// Copyright (c) Microsoft Corporation
// Licensed under the MIT license. See LICENSE in the project root.

#include <windows.h>
#include <setupapi.h>
#include <physicalmonitorenumerationapi.h>
#include <lowlevelmonitorconfigurationapi.h>
#include <iostream>
#include <stdexcept>
#include <string>
#include <vector>
#include <sstream>
#include <iomanip>
#include "Protocol.h"
#include "ProtocolTests.h"

namespace
{
    constexpr GUID DisplayClass = { 0x4d36e968, 0xe325, 0x11ce, { 0xbf, 0xc1, 0x08, 0x00, 0x2b, 0xe1, 0x03, 0x18 } };
    constexpr wchar_t HardwareIds[] = L"ROOT\\PtDisplayIddPoc\0";

    void Require(bool condition, const char* operation)
    {
        if (!condition)
        {
            throw std::runtime_error(std::string(operation) + " (Win32=" + std::to_string(GetLastError()) + ")");
        }
    }

    struct DeviceSet
    {
        HDEVINFO Value;
        ~DeviceSet()
        {
            if (Value != INVALID_HANDLE_VALUE)
            {
                SetupDiDestroyDeviceInfoList(Value);
            }
        }
    };

    struct Handle
    {
        HANDLE Value = INVALID_HANDLE_VALUE;
        ~Handle()
        {
            if (Value != INVALID_HANDLE_VALUE)
            {
                CloseHandle(Value);
            }
        }
    };

    void CreateRoot()
    {
        DeviceSet devices{ SetupDiCreateDeviceInfoList(&DisplayClass, nullptr) };
        Require(devices.Value != INVALID_HANDLE_VALUE, "SetupDiCreateDeviceInfoList");
        SP_DEVINFO_DATA info{ sizeof(info) };
        Require(SetupDiCreateDeviceInfoW(devices.Value, L"Display", &DisplayClass, L"PowerToys IDD POC", nullptr, DICD_GENERATE_ID, &info), "SetupDiCreateDeviceInfo");
        Require(SetupDiSetDeviceRegistryPropertyW(devices.Value, &info, SPDRP_HARDWAREID, reinterpret_cast<const BYTE*>(HardwareIds), sizeof(HardwareIds)), "Set hardware ID");
        Require(SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devices.Value, &info), "Register device");
        std::cout << "{\"Created\":true}\n";
    }

    void OpenFixture(Handle& handle, bool control = false)
    {
        DeviceSet devices{ SetupDiGetClassDevsW(&FixtureInterface, nullptr, nullptr, DIGCF_PRESENT | DIGCF_DEVICEINTERFACE) };
        Require(devices.Value != INVALID_HANDLE_VALUE, "Find fixture interface");
        SP_DEVICE_INTERFACE_DATA iface{ sizeof(iface) };
        Require(SetupDiEnumDeviceInterfaces(devices.Value, nullptr, &FixtureInterface, 0, &iface), "Enumerate fixture interface");
        DWORD length = 0;
        SetupDiGetDeviceInterfaceDetailW(devices.Value, &iface, nullptr, 0, &length, nullptr);
        Require(GetLastError() == ERROR_INSUFFICIENT_BUFFER && length > 0, "Size fixture interface");
        std::vector<BYTE> buffer(length);
        auto* detail = reinterpret_cast<SP_DEVICE_INTERFACE_DETAIL_DATA_W*>(buffer.data());
        detail->cbSize = sizeof(*detail);
        Require(SetupDiGetDeviceInterfaceDetailW(devices.Value, &iface, detail, length, nullptr, nullptr), "Get fixture interface");
        handle.Value = CreateFileW(detail->DevicePath, control ? GENERIC_READ | GENERIC_WRITE : GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, nullptr, OPEN_EXISTING, 0, nullptr);
        Require(handle.Value != INVALID_HANDLE_VALUE, "Open fixture");
    }

    FixtureSnapshot Snapshot(HANDLE device)
    {
        FixtureSnapshot value{};
        DWORD bytes = 0;
        Require(DeviceIoControl(device, SnapshotIoctl, nullptr, 0, &value, sizeof(value), &bytes, nullptr), "Read callback evidence");
        Require(bytes == sizeof(value) && value.Version == ProtocolVersion, "Validate snapshot version");
        return value;
    }

    void Control(HANDLE device, ULONG connector, FixtureOperation operation, ULONG value)
    {
        FixtureCommand command{ ProtocolVersion, connector, operation, value };
        DWORD bytes = 0;
        Require(DeviceIoControl(device, ControlIoctl, &command, sizeof(command), nullptr, 0, &bytes, nullptr), "Apply fixture command");
    }

    void WriteSnapshot(const FixtureSnapshot& value)
    {
        std::cout << "{\"Version\":" << value.Version << ",\"Monitors\":[";
        for (size_t i = 0; i < 2; ++i)
        {
            const auto& m = value.Monitors[i];
            if (i)
            {
                std::cout << ',';
            }
            std::cout << "{\"Connector\":" << m.Connector << ",\"Brightness\":" << m.Brightness
                      << ",\"Transmits\":" << m.Transmits << ",\"Receives\":" << m.Receives
                      << ",\"Gets\":" << m.Gets << ",\"Sets\":" << m.Sets
                      << ",\"Capabilities\":" << m.Capabilities
                      << ",\"LastTransmitAddress\":" << m.LastTransmitAddress
                      << ",\"LastTransmitLength\":" << m.LastTransmitLength << ",\"LastTransmit\":[";
            for (size_t j = 0; j < sizeof(m.LastTransmit); ++j)
            {
                if (j)
                {
                    std::cout << ',';
                }
                std::cout << static_cast<UINT>(m.LastTransmit[j]);
            }
            std::cout << "],\"LastReceiveAddress\":" << m.LastReceiveAddress
                      << ",\"LastReceiveLength\":" << m.LastReceiveLength
                      << ",\"Connected\":" << (m.Connected ? "true" : "false")
                      << ",\"Mode\":" << static_cast<ULONG>(m.Mode)
                      << ",\"Failures\":" << m.Failures << ",\"Arrivals\":" << m.Arrivals
                      << ",\"Departures\":" << m.Departures << '}';
        }
        std::cout << "]}";
    }

    struct Physical
    {
        std::vector<PHYSICAL_MONITOR> Monitors;
        ~Physical()
        {
            for (const auto& monitor : Monitors)
            {
                if (!DestroyPhysicalMonitor(monitor.hPhysicalMonitor))
                {
                    std::cerr << "DestroyPhysicalMonitor failed: " << GetLastError() << '\n';
                }
            }
        }
    };

    struct Inventory
    {
        std::vector<HMONITOR> Monitors;
        DWORD Error = ERROR_SUCCESS;
    };

    BOOL CALLBACK Collect(HMONITOR monitor, HDC, LPRECT, LPARAM data)
    {
        auto& inventory = *reinterpret_cast<Inventory*>(data);
        MONITORINFOEXW info{};
        info.cbSize = sizeof(info);
        if (!GetMonitorInfoW(monitor, &info))
        {
            inventory.Error = GetLastError();
            return FALSE;
        }
        DISPLAY_DEVICEW adapter{ sizeof(adapter) };
        for (DWORD i = 0; EnumDisplayDevicesW(nullptr, i, &adapter, 0); ++i)
        {
            if (wcscmp(adapter.DeviceName, info.szDevice) == 0 &&
                wcscmp(adapter.DeviceString, L"PowerToys IDD POC") == 0)
            {
                inventory.Monitors.push_back(monitor);
            }
            adapter = { sizeof(adapter) };
        }
        return TRUE;
    }

    void GetPhysicalHandles(Physical& physical)
    {
        Inventory inventory;
        Require(EnumDisplayMonitors(nullptr, nullptr, Collect, reinterpret_cast<LPARAM>(&inventory)), "EnumDisplayMonitors");
        SetLastError(inventory.Error);
        Require(inventory.Error == ERROR_SUCCESS && inventory.Monitors.size() == 2, "Require two active fixture monitors");
        for (const auto monitor : inventory.Monitors)
        {
            DWORD count = 0;
            Require(GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, &count), "GetNumberOfPhysicalMonitorsFromHMONITOR");
            Require(count == 1, "Require one physical handle per fixture monitor");
            PHYSICAL_MONITOR value{};
            Require(GetPhysicalMonitorsFromHMONITOR(monitor, 1, &value), "GetPhysicalMonitorsFromHMONITOR");
            std::cerr << "PhysicalMonitor[" << physical.Monitors.size() << "]: count=" << count
                      << " null=" << (value.hPhysicalMonitor == nullptr ? "true" : "false") << '\n';
            physical.Monitors.push_back(value);
        }
    }

    int Diagnose(HANDLE fixture)
    {
        Physical physical;
        GetPhysicalHandles(physical);
        bool usable = true;
        bool productionHandlesUsable = true;
        std::cout << "{\"Monitors\":[";
        for (size_t i = 0; i < physical.Monitors.size(); ++i)
        {
            const auto handle = physical.Monitors[i].hPhysicalMonitor;
            DWORD length = 0, brightness = 0, maximum = 0;
            MC_VCP_CODE_TYPE type{};
            SetLastError(ERROR_SUCCESS);
            const BOOL caps = GetCapabilitiesStringLength(handle, &length);
            const DWORD capsError = caps ? ERROR_SUCCESS : GetLastError();
            SetLastError(ERROR_SUCCESS);
            const BOOL get = GetVCPFeatureAndVCPFeatureReply(handle, 0x10, &type, &brightness, &maximum);
            const DWORD getError = get ? ERROR_SUCCESS : GetLastError();
            if (i)
            {
                std::cout << ',';
            }
            std::cout << "{\"Index\":" << i << ",\"PhysicalHandle\":" << reinterpret_cast<UINT_PTR>(handle)
                      << ",\"CapabilitiesLengthSuccess\":" << (caps ? "true" : "false")
                      << ",\"CapabilitiesLengthError\":" << capsError << ",\"CapabilitiesLength\":" << length
                      << ",\"GetVcpSuccess\":" << (get ? "true" : "false") << ",\"GetVcpError\":" << getError
                      << ",\"Brightness\":" << brightness << ",\"Maximum\":" << maximum << '}';
            usable = usable && caps && get;
            productionHandlesUsable = productionHandlesUsable && handle != nullptr;
        }
        std::cout << "],\"Callbacks\":";
        WriteSnapshot(Snapshot(fixture));
        std::cout << ",\"NativeHandlesNonzero\":" << (productionHandlesUsable ? "true" : "false")
                  << ",\"Passed\":" << (usable ? "true" : "false") << "}\n";
        return usable ? 0 : 1;
    }

    void Probe(HANDLE fixture)
    {
        Physical physical;
        GetPhysicalHandles(physical);
        const auto before = Snapshot(fixture);
        std::ostringstream reads;
        DWORD originals[2]{};
        for (size_t i = 0; i < 2; ++i)
        {
            const auto monitor = physical.Monitors[i].hPhysicalMonitor;
            DWORD length = 0;
            Require(GetCapabilitiesStringLength(monitor, &length), "GetCapabilitiesStringLength");
            Require(length > 0 && length < 4096, "Bound capabilities length");
            std::vector<char> capabilities(length);
            Require(CapabilitiesRequestAndCapabilitiesReply(monitor, capabilities.data(), length), "CapabilitiesRequestAndCapabilitiesReply");
            Require(std::string(capabilities.data()).find("vcp(10)") != std::string::npos, "Require brightness capability");
            MC_VCP_CODE_TYPE type{};
            DWORD maximum = 0;
            Require(GetVCPFeatureAndVCPFeatureReply(monitor, 0x10, &type, &originals[i], &maximum), "Get brightness");
            Require(maximum == 100, "Require VCP maximum 100");
            if (i)
            {
                reads << ',';
            }
            reads << originals[i];
        }
        bool modified[2]{};
        FixtureSnapshot after{};
        try
        {
            for (size_t i = 0; i < 2; ++i)
            {
                const DWORD expected = i ? 73 : 31;
                const auto monitor = physical.Monitors[i].hPhysicalMonitor;
                modified[i] = true;
                Require(SetVCPFeature(monitor, 0x10, expected), "Set brightness");
                DWORD actual = 0, maximum = 0;
                MC_VCP_CODE_TYPE type{};
                Require(GetVCPFeatureAndVCPFeatureReply(monitor, 0x10, &type, &actual, &maximum), "Read brightness back");
                Require(actual == expected && maximum == 100, "Verify native brightness readback");
            }
            after = Snapshot(fixture);
            Require(after.Monitors[0].Brightness != after.Monitors[1].Brightness, "Independent brightness");
            for (size_t i = 0; i < 2; ++i)
            {
                Require(after.Monitors[i].Gets > before.Monitors[i].Gets &&
                            after.Monitors[i].Sets > before.Monitors[i].Sets &&
                            after.Monitors[i].Capabilities > before.Monitors[i].Capabilities &&
                            after.Monitors[i].Receives > before.Monitors[i].Receives,
                        "Require driver-observed DDC/CI callbacks");
            }
        }
        catch (...)
        {
            for (size_t i = 0; i < 2; ++i)
            {
                if (modified[i] && !SetVCPFeature(physical.Monitors[i].hPhysicalMonitor, 0x10, originals[i]))
                {
                    std::cerr << "Brightness restoration failed: " << GetLastError() << '\n';
                }
            }
            throw;
        }
        for (size_t i = 0; i < 2; ++i)
        {
            Require(SetVCPFeature(physical.Monitors[i].hPhysicalMonitor, 0x10, originals[i]), "Restore brightness");
        }
        std::cout << "{\"Before\":";
        WriteSnapshot(before);
        std::cout << ",\"Reads\":[" << reads.str() << "],\"After\":";
        WriteSnapshot(after);
        std::cout << ",\"Passed\":true}\n";
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        Require(argc >= 2, "Use --create, --snapshot, --self-test, --diagnose, --probe, --connected, or --mode");
        if (wcscmp(argv[1], L"--self-test") == 0)
        {
            RunProtocolTests();
        }
        else if (wcscmp(argv[1], L"--create") == 0)
        {
            CreateRoot();
        }
        else
        {
            Handle fixture;
            const bool connected = wcscmp(argv[1], L"--connected") == 0;
            const bool mode = wcscmp(argv[1], L"--mode") == 0;
            OpenFixture(fixture, connected || mode);
            if (connected || mode)
            {
                Require(argc == 4 && wcslen(argv[2]) == 1 && argv[2][0] >= L'0' && argv[2][0] <= L'1' &&
                            wcslen(argv[3]) == 1 && argv[3][0] >= L'0' && argv[3][0] <= (connected ? L'1' : L'2'),
                        "Invalid connector or fixture control value");
                Control(fixture.Value, argv[2][0] - L'0', connected ? FixtureOperation::SetConnected : FixtureOperation::SetMode, argv[3][0] - L'0');
                WriteSnapshot(Snapshot(fixture.Value));
                std::cout << '\n';
            }
            else if (wcscmp(argv[1], L"--snapshot") == 0)
            {
                WriteSnapshot(Snapshot(fixture.Value));
                std::cout << '\n';
            }
            else if (wcscmp(argv[1], L"--diagnose") == 0)
            {
                return Diagnose(fixture.Value);
            }
            else
            {
                Require(wcscmp(argv[1], L"--probe") == 0, "Unknown operation");
                Probe(fixture.Value);
            }
        }
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cout << "{\"Passed\":false,\"Error\":" << std::quoted(error.what()) << "}\n";
        std::cerr << "BLOCKED: " << error.what() << '\n';
        return 1;
    }
}
