#pragma once

#include <vector>

#include <FancyZonesLib/Zone.h>

// Zoned window properties are not localized.
namespace ZonedWindowProperties
{
    const wchar_t PropertyMultipleZoneID[] = L"FancyZones_zones";
    const wchar_t PropertyRestoreSizeID[] = L"FancyZones_RestoreSize";
    const wchar_t PropertyRestoreOriginID[] = L"FancyZones_RestoreOrigin";

    const wchar_t MultiMonitorDeviceID[] = L"FancyZones#MultiMonitorDevice";
}

inline ZoneIndexSet GetZoneIndexSet(HWND window)
{
    HANDLE handle = ::GetProp(window, ZonedWindowProperties::PropertyMultipleZoneID);
    ZoneIndexSet zoneIndexSet;

    std::array<int, 2> data;
    memcpy(data.data(), &handle, sizeof data);
    uint64_t bitmask = ((uint64_t)data[1] << 32) + data[0];
    
    if (bitmask != 0)
    {
        for (int i = 0; i < std::numeric_limits<ZoneIndex>::digits; i++)
        {
            if ((1ull << i) & bitmask)
            {
                zoneIndexSet.push_back(i);
            }
        }
    }

    return zoneIndexSet;
}

inline void StampWindow(HWND window, Bitmask bitmask) noexcept
{
    std::array<int, 2> data = { static_cast<int>(bitmask), (bitmask >> 32) };
    HANDLE rawData;
    memcpy(&rawData, data.data(), sizeof data);
    SetProp(window, ZonedWindowProperties::PropertyMultipleZoneID, rawData);
}
