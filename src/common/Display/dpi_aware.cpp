#include "dpi_aware.h"
#include "monitors.h"
#include <ShellScalingApi.h>
#include <array>

namespace DPIAware
{
    HRESULT GetScreenDPIForMonitor(HMONITOR targetMonitor, UINT& dpi)
    {
        if (targetMonitor != nullptr)
        {
            UINT dummy = 0;
            return GetDpiForMonitor(targetMonitor, MDT_EFFECTIVE_DPI, &dpi, &dummy);
        }
        else
        {
            dpi = DPIAware::DEFAULT_DPI;
            return E_FAIL;
        }
    }

    HRESULT GetScreenDPIForWindow(HWND hwnd, UINT& dpi)
    {
        auto targetMonitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        return GetScreenDPIForMonitor(targetMonitor, dpi);
    }

    HRESULT GetScreenDPIForPoint(POINT point, UINT& dpi)
    {
        auto targetMonitor = MonitorFromPoint(point, MONITOR_DEFAULTTONEAREST);
        return GetScreenDPIForMonitor(targetMonitor, dpi);
    }

    HRESULT GetScreenDPIForCursor(UINT& dpi)
    {
        HMONITOR targetMonitor = nullptr;
        POINT currentCursorPos{ 0 };

        if (GetCursorPos(&currentCursorPos))
        {
            targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
        }

        return GetScreenDPIForMonitor(targetMonitor, dpi);
    }

    void Convert(HMONITOR monitor_handle, float& width, float& height)
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

    void ConvertByCursorPosition(float& width, float& height)
    {
        HMONITOR targetMonitor = nullptr;
        POINT currentCursorPos{ 0 };

        if (GetCursorPos(&currentCursorPos))
        {
            targetMonitor = MonitorFromPoint(currentCursorPos, MONITOR_DEFAULTTOPRIMARY);
        }
        
        Convert(targetMonitor, width, height);
    }

    void InverseConvert(HMONITOR monitor_handle, float& width, float& height)
    {
        if (monitor_handle == NULL)
        {
            const POINT ptZero = { 0, 0 };
            monitor_handle = MonitorFromPoint(ptZero, MONITOR_DEFAULTTOPRIMARY);
        }

        UINT dpi_x, dpi_y;
        if (GetDpiForMonitor(monitor_handle, MDT_EFFECTIVE_DPI, &dpi_x, &dpi_y) == S_OK)
        {
            width = width * DEFAULT_DPI / dpi_x;
            height = height * DEFAULT_DPI / dpi_y;
        }
    }

    void EnableDPIAwarenessForThisProcess()
    {
        SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }

    AwarenessLevel GetAwarenessLevel(DPI_AWARENESS_CONTEXT system_returned_value)
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
                return static_cast<DPIAware::AwarenessLevel>(i);
            }
        }
        return AwarenessLevel::UNAWARE;
    }
}
