#pragma once

#include <guiddef.h>

#include  <FancyZonesLib/FancyZonesData/LayoutDefaults.h>
#include <FancyZonesLib/FancyZonesDataTypes.h>

struct Layout
{
    GUID uuid = GUID_NULL;
    FancyZonesDataTypes::ZoneSetLayoutType type = FancyZonesDataTypes::ZoneSetLayoutType::PriorityGrid;
    bool showSpacing = DefaultValues::ShowSpacing;
    int spacing = DefaultValues::Spacing;
    int zoneCount = DefaultValues::ZoneCount;
    int sensitivityRadius = DefaultValues::SensitivityRadius;
};