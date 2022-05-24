#include "pch.h"

#include <WinUser.h>
#include <wingdi.h>

#include "Logger.h"

using namespace winrt;
using namespace Windows::Foundation;

namespace FancyZonesUtils
{
    template<RECT MONITORINFO::* member>
    std::vector<std::pair<HMONITOR, MONITORINFOEX>> GetAllMonitorInfo()
    {
        using result_t = std::vector<std::pair<HMONITOR, MONITORINFOEX>>;
        result_t result;

        auto enumMonitors = [](HMONITOR monitor, HDC, LPRECT, LPARAM param) -> BOOL {
            MONITORINFOEX mi;
            mi.cbSize = sizeof(mi);
            result_t& result = *reinterpret_cast<result_t*>(param);
            if (GetMonitorInfo(monitor, &mi))
            {
                result.push_back({ monitor, mi });
            }

            return TRUE;
        };

        EnumDisplayMonitors(NULL, NULL, enumMonitors, reinterpret_cast<LPARAM>(&result));
        return result;
    }
}

int main()
{
    init_apartment();
    
    auto allMonitors = FancyZonesUtils::GetAllMonitorInfo<&MONITORINFOEX::rcWork>();
    std::unordered_map<std::wstring, DWORD> displayDeviceIdxMap;

    for (auto& monitorData : allMonitors)
    {
        auto monitorInfo = monitorData.second;

        DISPLAY_DEVICE displayDevice{ .cb = sizeof(displayDevice) };
        std::wstring deviceId;
        auto enumRes = EnumDisplayDevicesW(monitorInfo.szDevice, displayDeviceIdxMap[monitorInfo.szDevice], &displayDevice, EDD_GET_DEVICE_INTERFACE_NAME);

        if (enumRes == 0)
        {
            Logger::log(get_last_error_or_default(GetLastError()));
        }
        else
        {
            Logger::log(displayDevice.DeviceID);
            Logger::log(displayDevice.DeviceKey);
            Logger::log(displayDevice.DeviceName);
            Logger::log(displayDevice.DeviceString);
        }
    }
}
