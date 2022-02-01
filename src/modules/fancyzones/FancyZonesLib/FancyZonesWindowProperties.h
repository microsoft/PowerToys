#pragma once

#include <vector>
#include <optional>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

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
    
    std::array<int, 4> data;
    memcpy(data.data(), &handle, sizeof data);

    Bitmask bitmask{
        .part1 = (static_cast<decltype(bitmask.part1)>(data[1]) << 32) + data[0],
        .part2 = (static_cast<decltype(bitmask.part1)>(data[3]) << 32) + data[2]
    };
    
    return bitmask.ToIndexSet();
}

inline void StampWindow(HWND window, Bitmask bitmask) noexcept
{
    std::array<int, 4> data = { 
        static_cast<int>(bitmask.part1), 
        static_cast<int>(bitmask.part1 >> 32),
        static_cast<int>(bitmask.part2),
        static_cast<int>(bitmask.part2 >> 32)
    };

    if (!SetProp(window, ZonedWindowProperties::PropertyMultipleZoneID, (HANDLE)data.data()))
    {
        Logger::error(L"Failed to stamp window {}", get_last_error_or_default(GetLastError()));
    }
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