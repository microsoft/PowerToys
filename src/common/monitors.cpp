#include "pch.h"

#include "monitors.h"

#include "common.h"

bool operator==(const ScreenSize& lhs, const ScreenSize& rhs)
{
    auto lhs_tuple = std::make_tuple(lhs.rect.left, lhs.rect.right, lhs.rect.top, lhs.rect.bottom);
    auto rhs_tuple = std::make_tuple(rhs.rect.left, rhs.rect.right, rhs.rect.top, rhs.rect.bottom);
    return lhs_tuple == rhs_tuple;
}

static BOOL CALLBACK GetDisplaysEnumCb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);
    if (GetMonitorInfo(monitor, &monitorInfo))
    {
        reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitorInfo.rcWork);
    }
    return true;
};

static BOOL CALLBACK GetDisplaysEnumCbWithNonWorkingArea(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);
    if (GetMonitorInfo(monitor, &monitorInfo))
    {
        reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitorInfo.rcMonitor);
    }
    return true;
};

std::vector<MonitorInfo> MonitorInfo::GetMonitors(bool includeNonWorkingArea)
{
    std::vector<MonitorInfo> monitors;
    EnumDisplayMonitors(NULL, NULL, includeNonWorkingArea ? GetDisplaysEnumCbWithNonWorkingArea : GetDisplaysEnumCb, reinterpret_cast<LPARAM>(&monitors));
    std::sort(begin(monitors), end(monitors), [](const MonitorInfo& lhs, const MonitorInfo& rhs) {
        return lhs.rect < rhs.rect;
    });
    return monitors;
}

static BOOL CALLBACK GetPrimaryDisplayEnumCb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);

    if (GetMonitorInfo(monitor, &monitorInfo) && (monitorInfo.dwFlags & MONITORINFOF_PRIMARY))
    {
        reinterpret_cast<MonitorInfo*>(data)->handle = monitor;
        reinterpret_cast<MonitorInfo*>(data)->rect = monitorInfo.rcWork;
    }
    return true;
};

MonitorInfo MonitorInfo::GetPrimaryMonitor()
{
    MonitorInfo primary({}, {});
    EnumDisplayMonitors(NULL, NULL, GetPrimaryDisplayEnumCb, reinterpret_cast<LPARAM>(&primary));
    return primary;
}
