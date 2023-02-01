#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/util.h>

namespace MonitorUtils
{
    namespace Display
    {
        std::vector<FancyZonesDataTypes::MonitorId> GetDisplays();
        FancyZonesDataTypes::DeviceId SplitDisplayDeviceId(const std::wstring& str) noexcept;
        FancyZonesDataTypes::DeviceId ConvertObsoleteDeviceId(const std::wstring& str) noexcept;
    }

    namespace WMI
    {
        std::vector<FancyZonesDataTypes::MonitorId> GetHardwareMonitorIds();
        FancyZonesDataTypes::DeviceId SplitWMIDeviceId(const std::wstring& str) noexcept;
    }

    std::vector<FancyZonesDataTypes::MonitorId> IdentifyMonitors() noexcept;
    void OpenWindowOnActiveMonitor(HWND window, HMONITOR monitor) noexcept;

    FancyZonesUtils::Rect GetWorkAreaRect(HMONITOR monitor);
};
