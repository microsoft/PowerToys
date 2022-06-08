#pragma once

namespace MonitorUtils
{
    void OpenWindowOnActiveMonitor(HWND window, HMONITOR monitor) noexcept;

    struct MonitorData
    {
        HMONITOR monitor;
        std::wstring deviceId;
        std::wstring serialNumber;
    };

    namespace Display
    {
        std::wstring TrimDeviceId(const std::wstring& deviceId) noexcept;
        std::vector<std::wstring> GetDisplays();
    }

    std::vector<MonitorData> IdentifyMonitors() noexcept;
};
