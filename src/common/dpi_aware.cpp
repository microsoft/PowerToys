#include "pch.h"
#include "dpi_aware.h"
#include "monitors.h"
#include <ShellScalingApi.h>

namespace DPIAware
{
    HRESULT GetScreenDPIForWindow(HWND hwnd, UINT& dpi_x, UINT& dpi_y)
    {
        auto monitor_handle = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        dpi_x = 0;
        dpi_y = 0;
        if (monitor_handle != nullptr)
        {
            return GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y);
        }
        else
        {
            return E_FAIL;
        }
    }

    HRESULT GetScreenDPIForPoint(POINT p, UINT& dpi_x, UINT& dpi_y)
    {
        auto monitor_handle = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
        dpi_x = 0;
        dpi_y = 0;
        if (monitor_handle != nullptr)
        {
            return GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y);
        }
        else
        {
            return E_FAIL;
        }
    }

    void Convert(HMONITOR monitor_handle, int& width, int& height)
    {
        if (monitor_handle == NULL)
        {
            const POINT ptZero = { 0, 0 };
            monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
        }

        UINT dpi_x, dpi_y;
        if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK)
        {
            width = width * dpi_x / DEFAULT_DPI;
            height = height * dpi_y / DEFAULT_DPI;
        }
    }

    void EnableDPIAwarenessForThisProcess()
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }

    AwarnessLevel GetAwarenessLevel(DPI_AWARENESS_CONTEXT system_returned_value)
    {
        const std::array levels{ DPI_AWARENESS_CONTEXT_UNAWARE,
                                 DPI_AWARENESS_CONTEXT_SYSTEM_AWARE,
                                 DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE,
                                 DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
                                 DPI_AWARENESS_CONTEXT_UNAWARE_GDISCALED };
        for (size_t i = 0; i < size(levels); ++i)
        {
            if (AreDpiAwarenessContextsEqual(levels[i], system_returned_value))
            {
                return static_cast<AwarnessLevel>(i);
            }
        }
        return AwarnessLevel::UNAWARE;
    }
}
