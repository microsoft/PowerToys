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
