#include "monitors.h"

#include <algorithm>

Box MonitorInfo::GetScreenSize(const bool includeNonWorkingArea) const
{
    return includeNonWorkingArea ? Box{ info.rcMonitor } : Box{ info.rcWork };
}

bool MonitorInfo::IsPrimary() const
{
    return static_cast<bool>(info.dwFlags & MONITORINFOF_PRIMARY);
}

MonitorInfo::MonitorInfo(HMONITOR h) :
    handle{ h }
{
    info.cbSize = sizeof(MONITORINFOEX);
    GetMonitorInfoW(handle, &info);
}

static BOOL CALLBACK GetDisplaysEnumCb(HMONITOR monitor, HDC /*hdc*/, LPRECT /*rect*/, LPARAM data)
{
    auto* monitors = reinterpret_cast<std::vector<MonitorInfo>*>(data);
    monitors->emplace_back(monitor);
    return true;
};

std::vector<MonitorInfo> MonitorInfo::GetMonitors(bool includeNonWorkingArea)
{
    std::vector<MonitorInfo> monitors;
    EnumDisplayMonitors(nullptr, nullptr, GetDisplaysEnumCb, reinterpret_cast<LPARAM>(&monitors));
    std::sort(begin(monitors), end(monitors), [=](const MonitorInfo& lhs, const MonitorInfo& rhs) {
        const auto lhsSize = lhs.GetScreenSize(includeNonWorkingArea);
        const auto rhsSize = rhs.GetScreenSize(includeNonWorkingArea);

        return lhsSize < rhsSize;
    });
    return monitors;
}

MonitorInfo MonitorInfo::GetPrimaryMonitor()
{
    auto monitors = MonitorInfo::GetMonitors(false);
    if (monitors.size() > 1)
    {
        for (auto monitor : monitors)
        {
            if (monitor.IsPrimary())
                return monitor;
        }
    }
    return monitors[0];
}

MonitorInfo MonitorInfo::GetFromWindow(const HWND window)
{
    auto monitor = MonitorFromWindow(window, MONITOR_DEFAULTTONULL);
    return MonitorInfo::MonitorInfo(monitor);
}

MonitorInfo MonitorInfo::GetFromPoint(int32_t x, int32_t y)
{
    auto monitor = MonitorFromPoint(POINT{ x, y }, MONITOR_DEFAULTTONULL);
    return MonitorInfo::MonitorInfo(monitor);
}

MonitorInfo::Size MonitorInfo::GetSize(const MONITORINFOEX& monitorInfoEx)
{
    Size size = {};

    auto device_name = PCTSTR(monitorInfoEx.szDevice);

    auto hdc = CreateDC(device_name, nullptr, nullptr, nullptr);
    size.width_mm = static_cast<float>(GetDeviceCaps(hdc, HORZSIZE));
    size.height_mm = static_cast<float>(GetDeviceCaps(hdc, VERTSIZE));
    if (hdc != nullptr)
    {
        ReleaseDC(nullptr, hdc);
    }

    auto monitor = &monitorInfoEx.rcMonitor;
    size.width_logical = static_cast<uint32_t>(monitor->right - monitor->left);
    size.height_logical = static_cast<uint32_t>(monitor->bottom - monitor->top);

    DEVMODE dev_mode = { .dmSize = sizeof DEVMODE };
    if (EnumDisplaySettingsEx(device_name, ENUM_CURRENT_SETTINGS, &dev_mode, EDS_RAWMODE))
    {
        size.width_physical = dev_mode.dmPelsWidth;
        size.height_physical = dev_mode.dmPelsHeight;
    }

    return size;
}

MonitorInfo::Size MonitorInfo::GetSize() const
{
    if (this->handle)
    {
        return MonitorInfo::GetSize(this->info);
    }
    else
    {
        return MonitorInfo::Size{};
    }
}
