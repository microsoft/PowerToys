#include "pch.h"

#include "monitors.h"

#include <Shellscalingapi.h>

#include "common.h"

bool operator==(const ScreenSize& lhs, const ScreenSize& rhs)
{
    auto lhs_tuple = std::make_tuple(lhs.rect.left, lhs.rect.right, lhs.rect.top, lhs.rect.bottom);
    auto rhs_tuple = std::make_tuple(rhs.rect.left, rhs.rect.right, rhs.rect.top, rhs.rect.bottom);
    return lhs_tuple == rhs_tuple;
}

static BOOL CALLBACK getDisplaysEnumCb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);
    if (GetMonitorInfo(monitor, &monitorInfo))
    {
        reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitorInfo.rcWork);
    }
    return true;
};

static BOOL CALLBACK getDisplaysEnumCbWithToolbar(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);
    if (GetMonitorInfo(monitor, &monitorInfo))
    {
        reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitorInfo.rcMonitor);
    }
    return true;
};


std::vector<MonitorInfo> MonitorInfo::GetMonitors(bool include_toolbar)
{
    std::vector<MonitorInfo> monitors;
    EnumDisplayMonitors(NULL, NULL, include_toolbar ? getDisplaysEnumCbWithToolbar : getDisplaysEnumCb, reinterpret_cast<LPARAM>(&monitors));
    std::sort(begin(monitors), end(monitors), [](const MonitorInfo& lhs, const MonitorInfo& rhs) {
        return lhs.rect < rhs.rect;
    });
    return monitors;
}

static BOOL CALLBACK saveDisplayToVector(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    reinterpret_cast<std::vector<HMONITOR>*>(data)->emplace_back(monitor);
    return true;
}


bool MonitorInfo::DoesAllMonitorsHaveSameDpiScaling()
{
    std::vector<HMONITOR> monitors;
    EnumDisplayMonitors(NULL, NULL, saveDisplayToVector, reinterpret_cast<LPARAM>(&monitors));

    if (monitors.size() < 2)
    {
        return true;
    }

    UINT firstMonitorDpiX;
    UINT firstMonitorDpiY;

    if (S_OK != GetDpiForMonitor(monitors[0], MDT_EFFECTIVE_DPI, &firstMonitorDpiX, &firstMonitorDpiY))
    {
        return false;
    }

    for (int i = 1; i < monitors.size(); i++)
    {
        UINT iteratedMonitorDpiX;
        UINT iteratedMonitorDpiY;

        if (S_OK != GetDpiForMonitor(monitors[i], MDT_EFFECTIVE_DPI, &iteratedMonitorDpiX, &iteratedMonitorDpiY) ||
            iteratedMonitorDpiX != firstMonitorDpiX ||
            iteratedMonitorDpiY != firstMonitorDpiY)
        {
            return false;
        }
    }

    return true;
}

static BOOL CALLBACK getPrimaryDisplayEnumCb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    MONITORINFOEX monitorInfo;
    monitorInfo.cbSize = sizeof(MONITORINFOEX);

    if (GetMonitorInfo(monitor, &monitorInfo) && monitorInfo.dwFlags & MONITORINFOF_PRIMARY)
    {
        reinterpret_cast<MonitorInfo*>(data)->handle = monitor;
        reinterpret_cast<MonitorInfo*>(data)->rect = monitorInfo.rcWork;
    }
    return true;
};

MonitorInfo MonitorInfo::GetPrimaryMonitor()
{
    MonitorInfo primary({}, {});
    EnumDisplayMonitors(NULL, NULL, getPrimaryDisplayEnumCb, reinterpret_cast<LPARAM>(&primary));
    return primary;
}
