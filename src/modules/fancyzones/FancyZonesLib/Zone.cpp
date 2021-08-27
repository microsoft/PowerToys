#include "pch.h"


#include <Shellscalingapi.h>

#include <common/display/dpi_aware.h>
#include <common/display/monitors.h>
#include "Zone.h"
#include "Settings.h"
#include "util.h"

namespace
{
    bool ValidateZoneRect(const RECT& rect)
    {
        int width  = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        return rect.left   >= ZoneConstants::MAX_NEGATIVE_SPACING &&
               rect.right  >= ZoneConstants::MAX_NEGATIVE_SPACING &&
               rect.top    >= ZoneConstants::MAX_NEGATIVE_SPACING &&
               rect.bottom >= ZoneConstants::MAX_NEGATIVE_SPACING &&
               width >= 0 && height >= 0;
    }
}

struct Zone : winrt::implements<Zone, IZone>
{
public:
    Zone(RECT zoneRect, const ZoneIndex zoneId) :
        m_zoneRect(zoneRect),
        m_id(zoneId)
    {
    }

    IFACEMETHODIMP_(RECT) GetZoneRect() const noexcept { return m_zoneRect; }
    IFACEMETHODIMP_(long) GetZoneArea() const noexcept { return max(m_zoneRect.bottom - m_zoneRect.top, 0) * max(m_zoneRect.right - m_zoneRect.left, 0); }
    IFACEMETHODIMP_(ZoneIndex) Id() const noexcept { return m_id; }
    IFACEMETHODIMP_(RECT) ComputeActualZoneRect(HWND window, HWND zoneWindow) const noexcept;

private:
    RECT m_zoneRect{};
    const ZoneIndex m_id{};
    std::map<HWND, RECT> m_windows{};
};

RECT Zone::ComputeActualZoneRect(HWND window, HWND zoneWindow) const noexcept
{
    // Take care of 1px border
    RECT newWindowRect = m_zoneRect;

    RECT windowRect{};
    ::GetWindowRect(window, &windowRect);

    RECT frameRect{};

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

    if ((::GetWindowLong(window, GWL_STYLE) & WS_SIZEBOX) == 0)
    {
        newWindowRect.right = newWindowRect.left + (windowRect.right - windowRect.left);
        newWindowRect.bottom = newWindowRect.top + (windowRect.bottom - windowRect.top);
    }

    return newWindowRect;
}

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect, const ZoneIndex zoneId) noexcept
{
    if (ValidateZoneRect(zoneRect) && zoneId >= 0)
    {
        return winrt::make_self<Zone>(zoneRect, zoneId);
    }
    else
    {
        return nullptr;
    }
}
