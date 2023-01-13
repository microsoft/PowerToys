#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace DPIAware
{
    constexpr inline int DEFAULT_DPI = 96;

    HRESULT GetScreenDPIForMonitor(HMONITOR targetMonitor, UINT& dpi);
    HRESULT GetScreenDPIForWindow(HWND hwnd, UINT& dpi);
    HRESULT GetScreenDPIForPoint(POINT p, UINT& dpi);
    HRESULT GetScreenDPIForCursor(UINT& dpi);
    void Convert(HMONITOR monitor_handle, float& width, float& height);
    void ConvertByCursorPosition(float& width, float& height);
    void InverseConvert(HMONITOR monitor_handle, float& width, float& height);
    void EnableDPIAwarenessForThisProcess();

    enum AwarenessLevel
    {
        UNAWARE,
        SYSTEM_AWARE,
        PER_MONITOR_AWARE,
        PER_MONITOR_AWARE_V2,
        UNAWARE_GDISCALED
    };
    AwarenessLevel GetAwarenessLevel(DPI_AWARENESS_CONTEXT system_returned_value);
};
