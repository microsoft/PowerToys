#pragma once
#include "ReportMonitorInfo.h"
#include <Windows.h>
#include "../../../src/common/utils/winapi_error.h"

int report(std::wostream& os)
{
    auto callback = [](HMONITOR monitor, HDC, RECT*, LPARAM prm) -> BOOL {
        std::wostream& os = *(std::wostream*)prm;
        MONITORINFOEX mi;
        mi.cbSize = sizeof(mi);
        if (GetMonitorInfo(monitor, &mi))
        {
            os << "GetMonitorInfo OK\n";
            DISPLAY_DEVICE displayDevice = { sizeof(displayDevice) };

            if (EnumDisplayDevices(mi.szDevice, 0, &displayDevice, 1))
            {
                if (displayDevice.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER)
                {
                    os << "EnumDisplayDevices OK[MIRRORING_DRIVER]: \n"
                        << "\tDeviceID = " << displayDevice.DeviceID << '\n'
                        << "\tDeviceKey = " << displayDevice.DeviceKey << '\n'
                        << "\tDeviceName = " << displayDevice.DeviceName << '\n'
                        << "\tDeviceString = " << displayDevice.DeviceString << '\n';
                }
                else
                {
                    os << "EnumDisplayDevices OK:\n"
                        << "\tDeviceID = " << displayDevice.DeviceID << '\n'
                        << "\tDeviceKey = " << displayDevice.DeviceKey << '\n'
                        << "\tDeviceName = " << displayDevice.DeviceName << '\n'
                        << "\tDeviceString = " << displayDevice.DeviceString << '\n';
                }
            }
            else
            {
                auto message = get_last_error_message(GetLastError());
                os << "EnumDisplayDevices FAILED: " << (message.has_value() ? message.value() : L"") << '\n';
            }
        }
        else
        {
            auto message = get_last_error_message(GetLastError());
            os << "GetMonitorInfo FAILED: " << (message.has_value() ? message.value() : L"") << '\n';
        }
        return TRUE;
    };

    if (EnumDisplayMonitors(nullptr, nullptr, callback, (LPARAM)&os))
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