#pragma once

#include <cstdint>
#include <set>

using ZoneIndex = int64_t;
using ZoneIndexSet = std::vector<ZoneIndex>;

struct Bitmask
{
    uint64_t part1{ 0 };
    uint64_t part2{ 0 };

    static Bitmask FromIndexSet(const ZoneIndexSet& set)
    {
        Bitmask bitmask{};

        for (const auto zoneIndex : set)
        {
            if (zoneIndex < std::numeric_limits<ZoneIndex>::digits)
            {
                bitmask.part2 |= 1ull << zoneIndex;
            }
            else
            {
                bitmask.part1 = 1ull << zoneIndex;
            }
        }

        return bitmask;
    }

    ZoneIndexSet ToIndexSet()
    {
        ZoneIndexSet zoneIndexSet;

        if (part2 != 0)
        {
            for (int i = 0; i < std::numeric_limits<ZoneIndex>::digits; i++)
            {
                if ((1ull << i) & part2)
                {
                    zoneIndexSet.push_back(i);
                }
            }
        }

        if (part1 != 0)
        {
            for (ZoneIndex i = 0; i < std::numeric_limits<ZoneIndex>::digits; i++)
            {
                if ((1ull << i) & part1)
                {
                    zoneIndexSet.push_back(i + std::numeric_limits<ZoneIndex>::digits);
                }
            }
        }

        return zoneIndexSet;
    }
};