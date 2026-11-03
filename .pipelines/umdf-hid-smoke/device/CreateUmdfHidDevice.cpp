// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <windows.h>
#include <setupapi.h>
#include <iostream>

namespace
{
    constexpr GUID HidClass = { 0x745a17a0, 0x74d3, 0x11d0, { 0xb6, 0xfe, 0x00, 0xa0, 0xc9, 0x0f, 0x57, 0xda } };
    constexpr wchar_t HardwareIds[] = L"ROOT\\PtUmdfHidPrototype\0";

    struct DeviceSet
    {
        HDEVINFO handle;

        ~DeviceSet()
        {
            if (handle != INVALID_HANDLE_VALUE)
            {
                SetupDiDestroyDeviceInfoList(handle);
            }
        }
    };

    int ReportError(const wchar_t* operation)
    {
        const DWORD error = GetLastError();
        std::wcerr << operation << L" failed with Win32 error " << error << L".\n";
        return 1;
    }
}

int wmain()
{
    DeviceSet devices{ SetupDiCreateDeviceInfoList(&HidClass, nullptr) };
    if (devices.handle == INVALID_HANDLE_VALUE)
    {
        return ReportError(L"SetupDiCreateDeviceInfoList");
    }

    SP_DEVINFO_DATA device{};
    device.cbSize = sizeof(device);
    if (!SetupDiCreateDeviceInfoW(
            devices.handle,
            L"HIDClass",
            &HidClass,
            L"PowerToys virtual HID test device",
            nullptr,
            DICD_GENERATE_ID,
            &device))
    {
        return ReportError(L"SetupDiCreateDeviceInfoW");
    }

    if (!SetupDiSetDeviceRegistryPropertyW(
            devices.handle,
            &device,
            SPDRP_HARDWAREID,
            reinterpret_cast<const BYTE*>(HardwareIds),
            sizeof(HardwareIds)))
    {
        return ReportError(L"SetupDiSetDeviceRegistryPropertyW");
    }

    if (!SetupDiCallClassInstaller(DIF_REGISTERDEVICE, devices.handle, &device))
    {
        return ReportError(L"SetupDiCallClassInstaller");
    }

    std::wcout << L"Created the PowerToys virtual HID root device.\n";
    return 0;
}
