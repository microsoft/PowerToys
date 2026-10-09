#pragma once
#include "ReportMonitorInfo.h"
#include <Windows.h>
#include <filesystem>
#include "../../../src/common/utils/winapi_error.h"
using namespace std;

namespace
{
    int BuildMonitorInfoReport(std::wostream& os)
    {
        struct capture
        {
            std::wostream* os = nullptr;
        };

        auto callback = [](HMONITOR monitor, HDC, RECT*, LPARAM prm) -> BOOL {
            std::wostream& os = *(reinterpret_cast<capture*>(prm))->os;
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
        if (EnumDisplayMonitors(nullptr, nullptr, callback, reinterpret_cast<LPARAM>(& c)))
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
}

void ReportMonitorInfo(const filesystem::path& tmpDir)
{
    auto monitorReportPath = tmpDir;
    monitorReportPath.append("monitor-report-info.txt");

    try
    {
        wofstream monitorReport(monitorReportPath);
        monitorReport << "GetSystemMetrics = " << GetSystemMetrics(SM_CMONITORS) << '\n';
        BuildMonitorInfoReport(monitorReport);
    }
    catch (std::exception& ex)
    {
        printf("Failed to report monitor info. %s\n", ex.what());
    }
    catch (...)
    {
        printf("Failed to report monitor info\n");
    }
}
