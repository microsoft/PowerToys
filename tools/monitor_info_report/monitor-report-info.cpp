#include <Windows.h>
#include <iostream>
#include <ctime>
#include <fstream>
#include <sstream>

std::wstring last_error()
{
    const DWORD error_code = GetLastError();
    wchar_t* error_msg;
    FormatMessageW(FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM,
                   nullptr,
                   error_code,
                   MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                   reinterpret_cast<LPWSTR>(&error_msg),
                   0,
                   nullptr);
    std::wstring result{ error_msg };
    LocalFree(error_msg);
    return result;
}

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
                os << "EnumDisplayDevices FAILED: " << last_error() << '\n';
            }
        }
        else
        {
            os << "GetMonitorInfo FAILED: " << last_error() << '\n';
        }
        return TRUE;
    };

    if (EnumDisplayMonitors(nullptr, nullptr, callback, (LPARAM)&os))
    {
        os << "EnumDisplayMonitors OK\n";
    }
    else
    {
        os << "EnumDisplayMonitors FAILED: " << last_error() << '\n';
    }
    return 0;
}

int main()
{
    time_t rawtime;
    struct tm* timeinfo;
    char buffer[1024];

    time(&rawtime);
    timeinfo = localtime(&rawtime);

    strftime(buffer, sizeof(buffer), "monitor-info-report-%d-%m-%Y-%H-%M-%S.txt", timeinfo);
    std::string str(buffer);

    std::wofstream of{ str };
    std::wostringstream oss;
    try
    {
        oss << "GetSystemMetrics = " << GetSystemMetrics(SM_CMONITORS) << '\n';
        report(oss);
    }
    catch (std::exception& ex)
    {
        oss << "exception: " << ex.what() << '\n';
    }
    catch (...)
    {
        oss << "unknown exception: " << last_error() << '\n';
    }
    of << oss.str();
    std::wcout << oss.str() << '\n';
    return 0;
}