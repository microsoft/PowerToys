#pragma once
#include "ReportMonitorInfo.h"
#include <Windows.h>
#include "../../../src/common/utils/winapi_error.h"

int report(std::wostream& os)
{
    struct capture
    {
        std::wostream* os = nullptr;
    };

    auto callback = [](HMONITOR monitor, HDC, RECT*, LPARAM prm) -> BOOL {
        std::wostream& os = *((capture*)prm)->os;
        MONITORINFOEX mi;
        mi.cbSize = sizeof(mi);

        if (GetMonitorInfoW(monitor, &mi))
        {
            os << "GetMonitorInfo OK\n";
            DISPLAY_DEVICE displayDevice = { sizeof(displayDevice) };

            DWORD i = 0;
            while (EnumDisplayDevicesW(mi.szDevice, i++, &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME))
            {
                const bool active = displayDevice.StateFlags & DISPLAY_DEVICE_ACTIVE;
                const bool mirroring = displayDevice.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER;
                os << "EnumDisplayDevices OK:\n"
                   << "\tMirroring = " << mirroring << '\n'
                   << "\tActive = " << active << '\n'
                   << "\tDeviceID = " << displayDevice.DeviceID << '\n'
                   << "\tDeviceKey = " << displayDevice.DeviceKey << '\n'
                   << "\tDeviceName = " << displayDevice.DeviceName << '\n'
                   << "\tDeviceString = " << displayDevice.DeviceString << '\n';
            }
        }
        else
        {
            auto message = get_last_error_message(GetLastError());
            os << "GetMonitorInfo FAILED: " << (message.has_value() ? message.value() : L"") << '\n';
        }
        return TRUE;
    };
    capture c;
    c.os = &os;
    if (EnumDisplayMonitors(nullptr, nullptr, callback, (LPARAM)&c))
    {
        os << "EnumDisplayMonitors OK\n";
    }
    else
    {
        auto message = get_last_error_message(GetLastError());
        os << "EnumDisplayMonitors FAILED: " << (message.has_value() ? message.value() : L"") << '\n';
    }
    return 0;
}