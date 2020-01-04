#include "pch.h"

#include "common.h"
#include "monitors.h"

bool operator==(const ScreenSize& lhs, const ScreenSize& rhs) {
  auto lhs_tuple = std::make_tuple(lhs.rect.left, lhs.rect.right, lhs.rect.top, lhs.rect.bottom);
  auto rhs_tuple = std::make_tuple(rhs.rect.left, rhs.rect.right, rhs.rect.top, rhs.rect.bottom);
  return lhs_tuple == rhs_tuple;
}

static BOOL CALLBACK get_displays_enum_cb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data) {
  MONITORINFOEX monitor_info;
  monitor_info.cbSize = sizeof(MONITORINFOEX);
  GetMonitorInfo(monitor, &monitor_info);
  reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitor_info.rcWork);
  return true;
};

static BOOL CALLBACK get_displays_enum_cb_with_toolbar(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data) {
  MONITORINFOEX monitor_info;
  monitor_info.cbSize = sizeof(MONITORINFOEX);
  GetMonitorInfo(monitor, &monitor_info);
  reinterpret_cast<std::vector<MonitorInfo>*>(data)->emplace_back(monitor, monitor_info.rcMonitor);
  return true;
};

std::vector<MonitorInfo> MonitorInfo::GetMonitors(bool include_toolbar) {
  std::vector<MonitorInfo> monitors;
  EnumDisplayMonitors(NULL, NULL, include_toolbar ? get_displays_enum_cb_with_toolbar : get_displays_enum_cb, reinterpret_cast<LPARAM>(&monitors));
  std::sort(begin(monitors), end(monitors), [](const MonitorInfo& lhs, const MonitorInfo& rhs) {
    return lhs.rect < rhs.rect;
    });
  return monitors;
}

static BOOL CALLBACK get_primary_display_enum_cb(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data) {
  MONITORINFOEX monitor_info;
  monitor_info.cbSize = sizeof(MONITORINFOEX);
  GetMonitorInfo(monitor, &monitor_info);
  if (monitor_info.dwFlags & MONITORINFOF_PRIMARY) {
    reinterpret_cast<MonitorInfo*>(data)->handle = monitor;
    reinterpret_cast<MonitorInfo*>(data)->rect = monitor_info.rcWork;
  }
  return true;
};

MonitorInfo MonitorInfo::GetPrimaryMonitor() {
  MonitorInfo primary({}, {});
  EnumDisplayMonitors(NULL, NULL, get_primary_display_enum_cb, reinterpret_cast<LPARAM>(&primary));
  return primary;
}

MonitorInfo MonitorInfo::GetFromWindow(HWND hwnd) {
  auto monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
  return GetFromHandle(monitor);
}

MonitorInfo MonitorInfo::GetFromPoint(POINT p) {
  auto monitor = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
  return GetFromHandle(monitor);
}

MonitorInfo MonitorInfo::GetFromHandle(HMONITOR monitor) {
  MONITORINFOEX monitor_info;
  monitor_info.cbSize = sizeof(MONITORINFOEX);
  GetMonitorInfo(monitor, &monitor_info);
  return MonitorInfo(monitor, monitor_info.rcWork);
}
