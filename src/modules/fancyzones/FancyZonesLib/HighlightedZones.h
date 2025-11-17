#pragma once

#include <FancyZonesLib/Zone.h>

class Layout;

class HighlightedZones
{
public:
    HighlightedZones() noexcept;
    ~HighlightedZones() = default;

    const ZoneIndexSet& Zones() const noexcept;
    bool Empty() const noexcept;

    bool Update(const Layout* layout, POINT const& point, bool selectManyZones) noexcept;
    void Reset() noexcept;

private:
    ZoneIndexSet m_initialHighlightZone;
    ZoneIndexSet m_highlightZone;
};
