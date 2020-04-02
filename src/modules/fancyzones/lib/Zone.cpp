#include "pch.h"

#include <common/dpi_aware.h>
#include <common/monitors.h>
#include "Zone.h"
#include "Settings.h"
#include "util.h"

struct Zone : winrt::implements<Zone, IZone>
{
public:
    Zone(RECT zoneRect) :
        m_zoneRect(zoneRect)
    {
    }

    IFACEMETHODIMP_(RECT) GetZoneRect() noexcept { return m_zoneRect; }
    IFACEMETHODIMP_(bool) IsEmpty() noexcept { return m_windows.empty(); };
    IFACEMETHODIMP_(bool) ContainsWindow(HWND window) noexcept;
    IFACEMETHODIMP_(void) AddWindowToZone(HWND window, HWND zoneWindow, bool stampZone) noexcept;
    IFACEMETHODIMP_(void) RemoveWindowFromZone(HWND window, bool restoreSize) noexcept;
    IFACEMETHODIMP_(void) SetId(size_t id) noexcept { m_id = id; }
    IFACEMETHODIMP_(size_t) Id() noexcept { return m_id; }
    IFACEMETHODIMP_(RECT) ComputeActualZoneRect(HWND window, HWND zoneWindow) noexcept;

private:
    void SizeWindowToZone(HWND window, HWND zoneWindow) noexcept;
    void StampZone(HWND window, bool stamp) noexcept;

    RECT m_zoneRect{};
    size_t m_id{};
    std::map<HWND, RECT> m_windows{};
};

IFACEMETHODIMP_(bool) Zone::ContainsWindow(HWND window) noexcept
{
    return (m_windows.find(window) != m_windows.end());
}

IFACEMETHODIMP_(void) Zone::AddWindowToZone(HWND window, HWND zoneWindow, bool stampZone) noexcept
{
    WINDOWPLACEMENT placement;
    ::GetWindowPlacement(window, &placement);
    ::GetWindowRect(window, &placement.rcNormalPosition);
    m_windows.emplace(std::pair<HWND, RECT>(window, placement.rcNormalPosition));

    SizeWindowToZone(window, zoneWindow);
    if (stampZone)
    {
        StampZone(window, true);
    }
}

IFACEMETHODIMP_(void) Zone::RemoveWindowFromZone(HWND window, bool restoreSize) noexcept
{
    auto iter = m_windows.find(window);
    if (iter != m_windows.end())
    {
        m_windows.erase(iter);
        StampZone(window, false);
    }
}

void Zone::SizeWindowToZone(HWND window, HWND zoneWindow) noexcept
{
    SizeWindowToRect(window, ComputeActualZoneRect(window, zoneWindow));
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
        const auto left_margin = frameRect.left - windowRect.left;
        const auto right_margin = frameRect.right - windowRect.right;
        const auto bottom_margin = frameRect.bottom - windowRect.bottom;
        newWindowRect.left -= left_margin;
        newWindowRect.right -= right_margin;
        newWindowRect.bottom -= bottom_margin;
    }

    // Map to screen coords
    MapWindowRect(zoneWindow, nullptr, &newWindowRect);

    MONITORINFO mi{ sizeof(mi) };
    if (GetMonitorInfoW(MonitorFromWindow(zoneWindow, MONITOR_DEFAULTTONEAREST), &mi))
    {
        const auto taskbar_left_size = std::abs(mi.rcMonitor.left - mi.rcWork.left);
        const auto taskbar_top_size = std::abs(mi.rcMonitor.top - mi.rcWork.top);
        OffsetRect(&newWindowRect, -taskbar_left_size, -taskbar_top_size);
        if (accountForUnawareness)
        {
            newWindowRect.left = max(mi.rcMonitor.left, newWindowRect.left);
            newWindowRect.right = min(mi.rcMonitor.right - taskbar_left_size, newWindowRect.right);
            newWindowRect.top = max(mi.rcMonitor.top, newWindowRect.top);
            newWindowRect.bottom = min(mi.rcMonitor.bottom - taskbar_top_size, newWindowRect.bottom);
        }
    }

    return zoneRect;
}

void Zone::StampZone(HWND window, bool stamp) noexcept
{
    if (stamp)
    {
        SetProp(window, ZONE_STAMP, reinterpret_cast<HANDLE>(m_id));
    }
    else
    {
        RemoveProp(window, ZONE_STAMP);
    }
}

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect) noexcept
{
    return winrt::make_self<Zone>(zoneRect);
}
