#pragma once

#include "Zone.h"
#include "JsonHelpers.h"

/**
 * Class representing single zone layout. ZoneSet is responsible for actual calculation of rectangle coordinates
 * (whether is grid or canvas layout) and moving windows through them.
 */
interface __declspec(uuid("{E4839EB7-669D-49CF-84A9-71A2DFD851A3}")) IZoneSet : public IUnknown
{
    /**
     * @returns Unique identifier of zone layout.
     */
    IFACEMETHOD_(GUID, Id)() = 0;
    /**
     * @returns Type of the zone layout. Layout type can be focus, columns, rows, grid, priority grid or custom.
     */
    IFACEMETHOD_(JSONHelpers::ZoneSetLayoutType, LayoutType)() = 0;
    /**
     * Add zone to the zone layout.
     *
     * @param   zone Zone object (defining coordinates of the zone).
     */
    IFACEMETHOD(AddZone)(winrt::com_ptr<IZone> zone) = 0;
    /**
     * Get zone from cursor coordinates.
     *
     * @param   pt Cursor coordinates.
     * @returns Zone object (defining coordinates of the zone).
     */
    IFACEMETHOD_(winrt::com_ptr<IZone>, ZoneFromPoint)(POINT pt) = 0;
    /**
     * Get index of the zone inside zone layout by window assigned to it.
     *
     * @param   window Handle of window assigned to zone.
     * @returns Zone index withing zone layout.
     */
    IFACEMETHOD_(int, GetZoneIndexFromWindow)(HWND window) = 0;
    /**
     * @returns Array of zone objects (defining coordinates of the zone) inside this zone layout.
     */
    IFACEMETHOD_(std::vector<winrt::com_ptr<IZone>>, GetZones)() = 0;
    /**
     * Assign window to the zone based on zone index inside zone layout.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @param   index      Zone index within zone layout.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, HWND zoneWindow, int index) = 0;
    /**
     * Assign window to the zones based on the set of zone indices inside zone layout.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @param   indexSet   The set of zone indices within zone layout.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndexSet)(HWND window, HWND zoneWindow, const std::vector<int>& indexSet) = 0;
    /**
     * Assign window to the zone based on direction (using WIN + LEFT/RIGHT arrow).
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @param   vkCode     Pressed arrow key.
     * @param   cycle      Whether we should move window to the first zone if we reached last zone in layout.
     *
     * @returns Boolean which is always true if cycle argument is set, otherwise indicating if there is more
     *          zones left in the zone layout in which window can move.
     */
    IFACEMETHOD_(bool, MoveWindowIntoZoneByDirection)(HWND window, HWND zoneWindow, DWORD vkCode, bool cycle) = 0;
    /**
     * Assign window to the zone based on cursor coordinates.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @param   pt         Cursor coordinates.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByPoint)(HWND window, HWND zoneWindow, POINT ptClient) = 0;
    /**
     * Calculate zone coordinates within zone layout based on number of zones and spacing. Used for one of
     * the predefined layouts (focus, columns, rows, grid, priority grid) or for custom layout.
     *
     * @param   monitorInfo Information about monitor on which zone layout is applied.
     * @param   zoneCount   Number of zones inside zone layout.
     * @param   spacing     Spacing between zones in pixels.
     *
     * @returns Boolean indicating if calculation was successful.
     */
    IFACEMETHOD_(bool, CalculateZones)(MONITORINFO monitorInfo, int zoneCount, int spacing) = 0;
};

#define VERSION_PERSISTEDDATA 0x0000F00D
struct ZoneSetPersistedData
{
    static constexpr inline size_t MAX_ZONES = 40;

    DWORD Version{VERSION_PERSISTEDDATA};
    WORD LayoutId{};
    DWORD ZoneCount{};
    JSONHelpers::ZoneSetLayoutType Layout{};
    RECT Zones[MAX_ZONES]{};
};

struct ZoneSetConfig
{
    ZoneSetConfig(
        GUID id,
        JSONHelpers::ZoneSetLayoutType layoutType,
        HMONITOR monitor,
        PCWSTR resolutionKey) noexcept :
            Id(id),
            LayoutType(layoutType),
            Monitor(monitor),
            ResolutionKey(resolutionKey)
    {
    }

    GUID Id{};
    JSONHelpers::ZoneSetLayoutType LayoutType{};
    HMONITOR Monitor{};
    PCWSTR ResolutionKey{};
};

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept;