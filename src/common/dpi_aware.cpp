#include "pch.h"
#include "dpi_aware.h"
#include "monitors.h"
#include <ShellScalingApi.h>

HRESULT DPIAware::GetScreenDPIForWindow(HWND hwnd, UINT &dpi_x, UINT &dpi_y) {
  auto monitor_handle = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
  dpi_x = 0;
  dpi_y = 0;
  if (monitor_handle != nullptr) {
    return GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y);
  } else {
    return E_FAIL;
  }
}

HRESULT DPIAware::GetScreenDPIForPoint(POINT p, UINT& dpi_x, UINT& dpi_y) {
  auto monitor_handle = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
  dpi_x = 0;
  dpi_y = 0;
  if (monitor_handle != nullptr) {
    return GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y);
  }
  else {
    return E_FAIL;
  }
}

void DPIAware::Convert(HMONITOR monitor_handle, int &width, int &height) {
  if (monitor_handle == NULL) {
    const POINT ptZero = { 0, 0 };
    monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
  }

  UINT dpi_x, dpi_y;
  if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK) {
    width = width * dpi_x / DEFAULT_DPI;
    height = height * dpi_y / DEFAULT_DPI;
  }
}