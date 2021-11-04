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

private:
    RECT m_zoneRect{};
    const ZoneIndex m_id{};
};

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
