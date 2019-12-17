#pragma once
#include "windef.h"

namespace DPIAware
{
    constexpr inline int DEFAULT_DPI = 96;

    HRESULT GetScreenDPIForWindow(HWND hwnd, UINT& dpi_x, UINT& dpi_y);
    HRESULT GetScreenDPIForPoint(POINT p, UINT& dpi_x, UINT& dpi_y);
    void Convert(HMONITOR monitor_handle, int& width, int& height);
    void EnableDPIAwarenessForThisProcess();

    enum AwarnessLevel
    {
        UNAWARE,
        SYSTEM_AWARE,
        PER_MONITOR_AWARE,
        PER_MONITOR_AWARE_V2,
        UNAWARE_GDISCALED
    };
    AwarnessLevel GetAwarenessLevel(DPI_AWARENESS_CONTEXT system_returned_value);
};
