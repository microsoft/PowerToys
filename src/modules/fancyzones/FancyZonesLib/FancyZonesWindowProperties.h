#pragma once

#include <vector>
#include <optional>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

#include <FancyZonesLib/Zone.h>

// Zoned window properties are not localized.
namespace ZonedWindowProperties
{
    const wchar_t PropertyMultipleZone64ID[] = L"FancyZones_zones"; // maximum possible zone count = 64
    const wchar_t PropertyMultipleZone128ID[] = L"FancyZones_zones_max128"; // additional property to allow maximum possible zone count = 128

    const wchar_t PropertySortKeyWithinZone[] = L"FancyZones_TabSortKeyWithinZone";
    const wchar_t PropertyRestoreSizeID[] = L"FancyZones_RestoreSize";
    const wchar_t PropertyRestoreOriginID[] = L"FancyZones_RestoreOrigin";

    const wchar_t MultiMonitorDeviceID[] = L"FancyZones#MultiMonitorDevice";
}

inline ZoneIndexSet GetZoneIndexSet(HWND window)
{
    HANDLE handle64 = ::GetProp(window, ZonedWindowProperties::PropertyMultipleZone64ID);
    HANDLE handle128 = ::GetProp(window, ZonedWindowProperties::PropertyMultipleZone128ID);

    ZoneIndexSetBitmask bitmask{};

    if (handle64)
    {
        std::array<int, 2> data;
        memcpy(data.data(), &handle64, sizeof data);
        bitmask.part1 = (static_cast<decltype(bitmask.part1)>(data[1]) << 32) + data[0];
    }
    
    if (handle128)
    {
        std::array<int, 2> data;
        memcpy(data.data(), &handle128, sizeof data);
        bitmask.part2 = (static_cast<decltype(bitmask.part2)>(data[1]) << 32) + data[0];
    }
        
    return bitmask.ToIndexSet();
}

inline void RemoveStampProperty(HWND window)
{
    ::RemoveProp(window, ZonedWindowProperties::PropertyMultipleZone64ID);
    ::RemoveProp(window, ZonedWindowProperties::PropertyMultipleZone128ID);
}

inline void StampWindow(HWND window, ZoneIndexSetBitmask bitmask) noexcept
{
    RemoveStampProperty(window);

    if (bitmask.part1 != 0)
    {
        std::array<int, 2> data{
            static_cast<int>(bitmask.part1),
            static_cast<int>(bitmask.part1 >> 32)
        };

        HANDLE rawData;
        memcpy(&rawData, data.data(), sizeof data);

        if (!SetProp(window, ZonedWindowProperties::PropertyMultipleZone64ID, rawData))
        {
            Logger::error(L"Failed to stamp window {}", get_last_error_or_default(GetLastError()));
        }
    }
    
    if (bitmask.part2 != 0)
    {
        std::array<int, 2> data{
            static_cast<int>(bitmask.part2),
            static_cast<int>(bitmask.part2 >> 32)
        };

        HANDLE rawData;
        memcpy(&rawData, data.data(), sizeof data);

        if (!SetProp(window, ZonedWindowProperties::PropertyMultipleZone128ID, rawData))
        {
            Logger::error(L"Failed to stamp window {}", get_last_error_or_default(GetLastError()));
        }    
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