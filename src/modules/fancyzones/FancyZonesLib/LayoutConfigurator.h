#pragma once

#include <FancyZonesLib/Zone.h>
#include <FancyZonesLib/util.h>

// Mapping zone id to zone
using ZonesMap = std::map<ZoneIndex, Zone>;

namespace FancyZonesDataTypes
{
    struct CustomLayoutData;
}

class LayoutConfigurator
{
public:
    static ZonesMap Focus(FancyZonesUtils::Rect workArea, int zoneCount) noexcept;
    static ZonesMap Rows(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept;
    static ZonesMap Columns(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept;
    static ZonesMap Grid(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept;
    static ZonesMap PriorityGrid(FancyZonesUtils::Rect workArea, int zoneCount, int spacing) noexcept;
    static ZonesMap Custom(FancyZonesUtils::Rect workArea, HMONITOR monitor, const FancyZonesDataTypes::CustomLayoutData& data, int spacing) noexcept;
};
