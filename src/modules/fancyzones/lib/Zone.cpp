#include "pch.h"

#include <common/dpi_aware.h>
#include <common/monitors.h>
#include "Zone.h"
#include "Settings.h"

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
    // Skip invisible windows
    if (!IsWindowVisible(window))
    {
        return;
    }

    // Take care of 1px border
    RECT newWindowRect = m_zoneRect;

    RECT windowRect{};
    ::GetWindowRect(window, &windowRect);

    RECT frameRect{};

    const auto level = DPIAware::GetAwarenessLevel(GetWindowDpiAwarenessContext(window));
    const bool accountForUnawareness = level < DPIAware::PER_MONITOR_AWARE;

    LONG leftMargin = 0;
    LONG rightMargin = 0;
    LONG bottomMargin = 0;

    if (SUCCEEDED(DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, &frameRect, sizeof(frameRect))))
    {
        leftMargin = frameRect.left - windowRect.left;
        rightMargin = frameRect.right - windowRect.right;
        bottomMargin = frameRect.bottom - windowRect.bottom;
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
        if (accountForUnawareness)
        {
            newWindowRect.left = max(mi.rcMonitor.left - leftMargin, newWindowRect.left);
            newWindowRect.right = min(mi.rcMonitor.right - taskbar_left_size - rightMargin, newWindowRect.right);
            newWindowRect.top = max(mi.rcMonitor.top, newWindowRect.top);
            newWindowRect.bottom = min(mi.rcMonitor.bottom - taskbar_top_size, newWindowRect.bottom);
        }
    }

    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        newWindowRect.right = newWindowRect.left + (windowRect.right - windowRect.left);
        newWindowRect.bottom = newWindowRect.top + (windowRect.bottom - windowRect.top);
    }

    WINDOWPLACEMENT placement{};
    ::GetWindowPlacement(window, &placement);

    //wait if SW_SHOWMINIMIZED would be removed from window (Issue #1685)    
    for (int i = 0; i < 5 && (placement.showCmd & SW_SHOWMINIMIZED) != 0; i++)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        ::GetWindowPlacement(window, &placement);
    }

    // Do not restore minimized windows. We change their placement though so they restore to the correct zone.
    if ((placement.showCmd & SW_SHOWMINIMIZED) == 0)
    {
        placement.showCmd = SW_RESTORE | SW_SHOWNA;
    }

    placement.rcNormalPosition = newWindowRect;
    placement.flags |= WPF_ASYNCWINDOWPLACEMENT;

    ::SetWindowPlacement(window, &placement);
    // Do it again, allowing Windows to resize the window and set correct scaling
    // This fixes Issue #365
    ::SetWindowPlacement(window, &placement);
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
