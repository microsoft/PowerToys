#pragma once
#include "windef.h"

struct DPIAware {
  static constexpr int DEFAULT_DPI = 96;

  static HRESULT GetScreenDPIForWindow(HWND hwnd, UINT & dpi_x, UINT & dpi_y);
  static HRESULT GetScreenDPIForPoint(POINT p, UINT& dpi_x, UINT& dpi_y);
  static void Convert(HMONITOR monitor_handle, int &width, int &height);
  static void EnableDPIAwarenessForThisProcess();
};
