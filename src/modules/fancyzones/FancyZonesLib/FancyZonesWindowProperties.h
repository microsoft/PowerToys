#pragma once

#include <vector>
#include <optional>

#include <FancyZonesLib/Zone.h>

// Zoned window properties are not localized.
namespace ZonedWindowProperties
{
    const wchar_t PropertyMultipleZoneID[] = L"FancyZones_zones";
    const wchar_t PropertySortKeyWithinZone[] = L"FancyZones_TabSortKeyWithinZone";
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

inline std::optional<size_t> GetTabSortKeyWithinZone(HWND window)
{
    auto rawTabSortKeyWithinZone = ::GetPropW(window, ZonedWindowProperties::PropertySortKeyWithinZone);
    if (rawTabSortKeyWithinZone == NULL)
    {
        return std::nullopt;
    }

    auto tabSortKeyWithinZone = reinterpret_cast<uint64_t>(rawTabSortKeyWithinZone) - 1;
    return tabSortKeyWithinZone;
}

inline void SetTabSortKeyWithinZone(HWND window, std::optional<size_t> tabSortKeyWithinZone)
{
    if (!tabSortKeyWithinZone.has_value())
    {
        ::RemovePropW(window, ZonedWindowProperties::PropertySortKeyWithinZone);
    }
    else
    {
        auto rawTabSortKeyWithinZone = reinterpret_cast<HANDLE>(tabSortKeyWithinZone.value() + 1);
        ::SetPropW(window, ZonedWindowProperties::PropertySortKeyWithinZone, rawTabSortKeyWithinZone);
    }
}