#pragma once

#include <FancyZonesLib/Zone.h>

struct ZoneIndexSetBitmask
{
    uint64_t part1{ 0 }; // represents 0-63 zones
    uint64_t part2{ 0 }; // represents 64-127 zones

    static ZoneIndexSetBitmask FromIndexSet(const ZoneIndexSet& set)
    {
        ZoneIndexSetBitmask bitmask{};

        for (const auto zoneIndex : set)
        {
            if (zoneIndex <= std::numeric_limits<ZoneIndex>::digits)
            {
                bitmask.part1 |= 1ull << zoneIndex;
            }
            else
            {
                ZoneIndex index = zoneIndex - std::numeric_limits<ZoneIndex>::digits - 1;
                bitmask.part2 |= 1ull << index;
            }
        }

        return bitmask;
    }

    ZoneIndexSet ToIndexSet() const noexcept
    {
        ZoneIndexSet zoneIndexSet;

        if (part1 != 0)
        {
            for (ZoneIndex i = 0; i <= std::numeric_limits<ZoneIndex>::digits; i++)
            {
                if ((1ull << i) & part1)
                {
                    zoneIndexSet.push_back(i);
                }
            }
        }

        if (part2 != 0)
        {
            for (ZoneIndex i = 0; i <= std::numeric_limits<ZoneIndex>::digits; i++)
            {
                if ((1ull << i) & part2)
                {
                    zoneIndexSet.push_back(i + std::numeric_limits<ZoneIndex>::digits + 1);
                }
            }
        }

        return zoneIndexSet;
    }
};