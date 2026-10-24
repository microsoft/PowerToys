#pragma once

#include <FancyZonesLib/FancyZonesData/LayoutData.h>
#include <FancyZonesLib/util.h>

#include <FancyZonesLib/LayoutConfigurator.h> // ZonesMap

class Layout
{
public:
    Layout(const LayoutData& data);
    ~Layout() = default;

    bool Init(const FancyZonesUtils::Rect& workAreaRect, HMONITOR monitor) noexcept;

    GUID Id() const noexcept;
    FancyZonesDataTypes::ZoneSetLayoutType Type() const noexcept;

    const ZonesMap& Zones() const noexcept;
    ZoneIndexSet ZonesFromPoint(POINT pt) const noexcept;
    /**
     * Returns all zones spanned by the minimum bounding rectangle containing the two given zone index sets.
     */
    ZoneIndexSet GetCombinedZoneRange(const ZoneIndexSet& initialZones, const ZoneIndexSet& finalZones) const noexcept; 

    RECT GetCombinedZonesRect(const ZoneIndexSet& zones);

    /**
     * Returns this layout's configured fallback zone (or zone range) for a newly created window
     * with no zone history, but only if it is still valid for the zone count actually resolved on
     * this work area (per-monitor overrides on built-in templates can make it stale). Returns
     * nullopt if nothing is configured, or if it no longer refers to zones that exist here.
     */
    std::optional<ZoneIndexSet> ValidatedDefaultZoneIndexSet() const noexcept;

private:
    const LayoutData m_data;
    ZonesMap m_zones{};
};
