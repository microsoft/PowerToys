#pragma once

#include <optional>

#include <FancyZonesLib/Zone.h>

// Zoned window properties are not localized.
namespace ZonedWindowProperties
{
    const wchar_t PropertyRestoreSizeID[] = L"FancyZones_RestoreSize";
    const wchar_t PropertyRestoreOriginID[] = L"FancyZones_RestoreOrigin";

    const wchar_t MultiMonitorDeviceID[] = L"FancyZones#MultiMonitorDevice";
}

namespace FancyZonesWindowProperties
{
    void StampZoneIndexProperty(HWND window, const ZoneIndexSet& zoneSet);
    void RemoveZoneIndexProperty(HWND window);
    ZoneIndexSet RetrieveZoneIndexProperty(HWND window);

    std::optional<size_t> GetTabSortKeyWithinZone(HWND window);
    void SetTabSortKeyWithinZone(HWND window, std::optional<size_t> tabSortKeyWithinZone);
}

