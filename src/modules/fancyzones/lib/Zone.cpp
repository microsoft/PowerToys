#include "pch.h"


#include <Shellscalingapi.h>

#include <common/dpi_aware.h>
#include <common/monitors.h>
#include "Zone.h"
#include "Settings.h"
#include "util.h"

#include "common/monitors.h"

struct Zone : winrt::implements<Zone, IZone>
{
public:
    Zone(RECT zoneRect) :
        m_zoneRect(zoneRect)
    {
    }

    IFACEMETHODIMP_(RECT) GetZoneRect() noexcept { return m_zoneRect; }
    IFACEMETHODIMP_(void) SetId(size_t id) noexcept { m_id = id; }
    IFACEMETHODIMP_(size_t) Id() noexcept { return m_id; }
    IFACEMETHODIMP_(RECT) ComputeActualZoneRect(HWND window, HWND zoneWindow) noexcept;

private:
    RECT m_zoneRect{};
    size_t m_id{};
    std::map<HWND, RECT> m_windows{};
};

static BOOL CALLBACK saveDisplayToVector(HMONITOR monitor, HDC hdc, LPRECT rect, LPARAM data)
{
    reinterpret_cast<std::vector<HMONITOR>*>(data)->emplace_back(monitor);
    return true;
}

bool allMonitorsHaveSameDpiScaling()
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
            iteratedMonitorDpiX != firstMonitorDpiX)
        {
            return false;
        }
    }

    return true;
}

RECT Zone::ComputeActualZoneRect(HWND window, HWND zoneWindow) noexcept
{
    // Take care of 1px border
    RECT newWindowRect = m_zoneRect;

    RECT windowRect{};
    ::GetWindowRect(window, &windowRect);

    RECT frameRect{};

    const auto level = DPIAware::GetAwarenessLevel(GetWindowDpiAwarenessContext(window));
    const bool accountForUnawareness = level < DPIAware::PER_MONITOR_AWARE;

    if (SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(frameRect))))
    {
        LONG leftMargin = frameRect.left - windowRect.left;
        LONG rightMargin = frameRect.right - windowRect.right;
        LONG bottomMargin = frameRect.bottom - windowRect.bottom;
        newWindowRect.left -= leftMargin;
        newWindowRect.right -= rightMargin;
        newWindowRect.bottom -= bottomMargin;
    }

    // Map to screen coords
    MapWindowRect(zoneWindow, nullptr, &newWindowRect);

    MONITORINFO mi{ sizeof(mi) };
    if (GetMonitorInfoW(MonitorFromWindow(zoneWindow, MONITOR_DEFAULTTONEAREST), &mi))
    {
        const auto taskbar_left_size = std::abs(mi.rcMonitor.left - mi.rcWork.left);
        const auto taskbar_top_size = std::abs(mi.rcMonitor.top - mi.rcWork.top);
        OffsetRect(&newWindowRect, -taskbar_left_size, -taskbar_top_size);

        if (accountForUnawareness && !allMonitorsHaveSameDpiScaling())
        {
            newWindowRect.left = max(mi.rcMonitor.left, newWindowRect.left);
            newWindowRect.right = min(mi.rcMonitor.right - taskbar_left_size, newWindowRect.right);
            newWindowRect.top = max(mi.rcMonitor.top, newWindowRect.top);
            newWindowRect.bottom = min(mi.rcMonitor.bottom - taskbar_top_size, newWindowRect.bottom);
        }
    }

    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        newWindowRect.right = newWindowRect.left + (windowRect.right - windowRect.left);
        newWindowRect.bottom = newWindowRect.top + (windowRect.bottom - windowRect.top);
    }

    return newWindowRect;
}

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect) noexcept
{
    return winrt::make_self<Zone>(zoneRect);
}
