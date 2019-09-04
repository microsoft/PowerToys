#pragma once
#include "windef.h"

class DPIAware {
private:
  static const int DEFAULT_DPI = 96;

public:
  static HRESULT GetScreenDPIForWindow(HWND hwnd, UINT & dpi_x, UINT & dpi_y);
  static void Convert(HMONITOR monitor_handle, int &width, int &high);
};
