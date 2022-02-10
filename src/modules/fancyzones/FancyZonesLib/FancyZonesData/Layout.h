#pragma once

#include <guiddef.h>

#include <FancyZonesLib/FancyZonesDataTypes.h>

struct Layout
{
    GUID uuid;
    FancyZonesDataTypes::ZoneSetLayoutType type;
    bool showSpacing;
    int spacing;
    int zoneCount;
    int sensitivityRadius;
};