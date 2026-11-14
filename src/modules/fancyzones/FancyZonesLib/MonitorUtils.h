#pragma once

#include <FancyZonesLib/FancyZonesDataTypes.h>
#include <FancyZonesLib/util.h>

namespace MonitorUtils
{
    namespace Display
    {
        std::pair<bool, std::vector<FancyZonesDataTypes::MonitorId>> GetDisplays();
        FancyZonesDataTypes::DeviceId SplitDisplayDeviceId(const std::wstring& str) noexcept;
        FancyZonesDataTypes::DeviceId ConvertObsoleteDeviceId(const std::wstring& str) noexcept;
    }

    namespace WMI
    {
        std::vector<FancyZonesDataTypes::MonitorId> GetHardwareMonitorIds();
        FancyZonesDataTypes::DeviceId SplitWMIDeviceId(const std::wstring& str) noexcept;
    }

    std::vector<FancyZonesDataTypes::MonitorId> IdentifyMonitors() noexcept;
    void AssignSerialNumbers(std::vector<FancyZonesDataTypes::MonitorId>& displays, const std::vector<FancyZonesDataTypes::MonitorId>& hardwareMonitors) noexcept;

    enum class ConnectedMonitorSync
    {
        NotConnected,
        Unchanged,
        NumberUpdated,
        SerialNumberUpdated
    };

    // Updates the serial number and monitor number of a saved id from the connected monitor with the same device and instance id.
    ConnectedMonitorSync SyncWithConnectedMonitor(FancyZonesDataTypes::MonitorId& savedId, const std::vector<FancyZonesDataTypes::MonitorId>& connectedMonitors) noexcept;

    void OpenWindowOnActiveMonitor(HWND window, HMONITOR monitor) noexcept;

    FancyZonesUtils::Rect GetWorkAreaRect(HMONITOR monitor);
};
