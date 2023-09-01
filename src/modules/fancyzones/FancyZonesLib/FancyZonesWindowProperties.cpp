#include "pch.h"
#include "FancyZonesWindowProperties.h"

#include <FancyZonesLib/ZoneIndexSetBitmask.h>

#include <common/logger/logger.h>
#include <common/utils/winapi_error.h>

// Zoned window properties are not localized.
namespace ZonedWindowProperties
{
    const wchar_t PropertyMultipleZone64ID[] = L"FancyZones_zones"; // maximum possible zone count = 64
    const wchar_t PropertyMultipleZone128ID[] = L"FancyZones_zones_max128"; // additional property to allow maximum possible zone count = 128

    const wchar_t PropertySortKeyWithinZone[] = L"FancyZones_TabSortKeyWithinZone";
}

bool FancyZonesWindowProperties::StampZoneIndexProperty(HWND window, const ZoneIndexSet& zoneSet)
{
    RemoveZoneIndexProperty(window);
    ZoneIndexSetBitmask bitmask = ZoneIndexSetBitmask::FromIndexSet(zoneSet);

    if (bitmask.part1 != 0)
    {
        std::array<int32_t, 2> data{
            static_cast<int>(bitmask.part1),
            static_cast<int>(bitmask.part1 >> 32)
        };

        HANDLE rawData;
        memcpy(&rawData, data.data(), sizeof data);

        if (!SetProp(window, ZonedWindowProperties::PropertyMultipleZone64ID, rawData))
        {
            Logger::error(L"Failed to stamp window {}", get_last_error_or_default(GetLastError()));
            return false;
        }
    }

    if (bitmask.part2 != 0)
    {
        std::array<int32_t, 2> data{
            static_cast<int>(bitmask.part2),
            static_cast<int>(bitmask.part2 >> 32)
        };

        HANDLE rawData;
        memcpy(&rawData, data.data(), sizeof data);

        if (!SetProp(window, ZonedWindowProperties::PropertyMultipleZone128ID, rawData))
        {
            Logger::error(L"Failed to stamp window {}", get_last_error_or_default(GetLastError()));
            return false;
        }
    }

    return true;
}

void FancyZonesWindowProperties::RemoveZoneIndexProperty(HWND window)
{
    ::RemoveProp(window, ZonedWindowProperties::PropertyMultipleZone64ID);
    ::RemoveProp(window, ZonedWindowProperties::PropertyMultipleZone128ID);
}

ZoneIndexSet FancyZonesWindowProperties::RetrieveZoneIndexProperty(HWND window)
{
    HANDLE handle64 = ::GetProp(window, ZonedWindowProperties::PropertyMultipleZone64ID);
    HANDLE handle128 = ::GetProp(window, ZonedWindowProperties::PropertyMultipleZone128ID);

    ZoneIndexSetBitmask bitmask{};

    if (handle64)
    {
        std::array<int32_t, 2> data;
        memcpy(data.data(), &handle64, sizeof data);
        bitmask.part1 = (static_cast<decltype(bitmask.part1)>(data[1]) << 32) + data[0];
    }

    if (handle128)
    {
        std::array<int32_t, 2> data;
        memcpy(data.data(), &handle128, sizeof data);
        bitmask.part2 = (static_cast<decltype(bitmask.part2)>(data[1]) << 32) + data[0];
    }

    return bitmask.ToIndexSet();
}

void FancyZonesWindowProperties::StampMovedOnOpeningProperty(HWND window)
{
    ::SetPropW(window, ZonedWindowProperties::PropertyMovedOnOpening, reinterpret_cast<HANDLE>(1));
}

bool FancyZonesWindowProperties::RetrieveMovedOnOpeningProperty(HWND window)
{
    HANDLE handle = ::GetProp(window, ZonedWindowProperties::PropertyMovedOnOpening);
    return handle != nullptr;
}

std::optional<size_t> FancyZonesWindowProperties::GetTabSortKeyWithinZone(HWND window)
{
    auto rawTabSortKeyWithinZone = ::GetPropW(window, ZonedWindowProperties::PropertySortKeyWithinZone);
    if (rawTabSortKeyWithinZone == NULL)
    {
        return std::nullopt;
    }

    auto tabSortKeyWithinZone = reinterpret_cast<uint64_t>(rawTabSortKeyWithinZone) - 1;
    return tabSortKeyWithinZone;
}

void FancyZonesWindowProperties::SetTabSortKeyWithinZone(HWND window, std::optional<size_t> tabSortKeyWithinZone)
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
