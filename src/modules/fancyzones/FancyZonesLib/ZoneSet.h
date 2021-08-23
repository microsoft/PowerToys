#pragma once

#include "Zone.h"
#include "Settings.h"

namespace FancyZonesDataTypes
{
    enum class ZoneSetLayoutType;
}
/**
 * Class representing single zone layout. ZoneSet is responsible for actual calculation of rectangle coordinates
 * (whether is grid or canvas layout) and moving windows through them.
 */
interface __declspec(uuid("{E4839EB7-669D-49CF-84A9-71A2DFD851A3}")) IZoneSet : public IUnknown
{
    // Mapping zone id to zone
    using ZonesMap = std::map<ZoneIndex, winrt::com_ptr<IZone>>;

    /**
     * @returns Unique identifier of zone layout.
     */
    IFACEMETHOD_(GUID, Id)() const = 0;
    /**
     * @returns Type of the zone layout. Layout type can be focus, columns, rows, grid, priority grid or custom.
     */
    IFACEMETHOD_(FancyZonesDataTypes::ZoneSetLayoutType, LayoutType)() const = 0;
    /**
     * Add zone to the zone layout.
     *
     * @param   zone Zone object (defining coordinates of the zone).
     */
    IFACEMETHOD(AddZone)(winrt::com_ptr<IZone> zone) = 0;
    /**
     * Get zones from cursor coordinates.
     *
     * @param   pt Cursor coordinates.
     * @returns Vector of indices, corresponding to the current set of zones - the zones considered active.
     */
    IFACEMETHOD_(ZoneIndexSet, ZonesFromPoint)(POINT pt) const = 0;
    /**
     * Get index set of the zones to which the window was assigned.
     *
     * @param   window Handle of the window.
     * @returns A vector of ZoneIndex, 0-based index set.
     */
    IFACEMETHOD_(ZoneIndexSet, GetZoneIndexSetFromWindow)(HWND window) const = 0;
    /**
     * @returns Array of zone objects (defining coordinates of the zone) inside this zone layout.
     */
    IFACEMETHOD_(ZonesMap, GetZones) () const = 0;
    /**
     * Assign window to the zone based on zone index inside zone layout.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   index          Zone index within zone layout.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndex)(HWND window, HWND workAreaWindow, ZoneIndex index) = 0;
    /**
     * Assign window to the zones based on the set of zone indices inside zone layout.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   indexSet       The set of zone indices within zone layout.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByIndexSet)(HWND window, HWND workAreaWindow, const ZoneIndexSet& indexSet) = 0;
    /**
     * Assign window to the zone based on direction (using WIN + LEFT/RIGHT arrow), based on zone index numbers,
     * not their on-screen position.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   vkCode         Pressed arrow key.
     * @param   cycle          Whether we should move window to the first zone if we reached last zone in layout.
     *
     * @returns Boolean which is always true if cycle argument is set, otherwise indicating if there is more
     *          zones left in the zone layout in which window can move.
     */
    IFACEMETHOD_(bool, MoveWindowIntoZoneByDirectionAndIndex)(HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) = 0;
    /**
     * Assign window to the zone based on direction (using WIN + LEFT/RIGHT/UP/DOWN arrow), based on
     * their on-screen position.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   vkCode         Pressed arrow key.
     * @param   cycle          Whether we should move window to the first zone if we reached last zone in layout.
     *
     * @returns Boolean which is always true if cycle argument is set, otherwise indicating if there is more
     *          zones left in the zone layout in which window can move.
     */
    IFACEMETHOD_(bool, MoveWindowIntoZoneByDirectionAndPosition)
    (HWND window, HWND workAreaWindow, DWORD vkCode, bool cycle) = 0;
    /**
     * Extend or shrink the window to an adjacent zone based on direction (using CTRL+WIN+ALT + LEFT/RIGHT/UP/DOWN arrow), based on
     * their on-screen position.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   vkCode         Pressed arrow key.
     *
     * @returns Boolean indicating whether the window was rezoned. False could be returned when there are no more
     *          zones available in the given direction.
     */
    IFACEMETHOD_(bool, ExtendWindowByDirectionAndPosition)
    (HWND window, HWND workAreaWindow, DWORD vkCode) = 0;
    /**
     * Assign window to the zone based on cursor coordinates.
     *
     * @param   window         Handle of window which should be assigned to zone.
     * @param   workAreaWindow The m_window of a WorkArea, it's a hidden window representing the
     *                         current monitor desktop work area.
     * @param   pt             Cursor coordinates.
     */
    IFACEMETHOD_(void, MoveWindowIntoZoneByPoint)
    (HWND window, HWND workAreaWindow, POINT ptClient) = 0;
    /**
     * Calculate zone coordinates within zone layout based on number of zones and spacing.
     *
     * @param   workAreaRect The rectangular area on the screen on which the zone layout is applied.
     * @param   zoneCount    Number of zones inside zone layout.
     * @param   spacing      Spacing between zones in pixels.
     *
     * @returns Boolean indicating if calculation was successful.
     */
    IFACEMETHOD_(bool, CalculateZones)(RECT workAreaRect, int zoneCount, int spacing) = 0;
    /**
     * Check if the zone with the specified index is empty. Returns true if the zone with passed zoneIndex does not exist.
     * 
     * @param   zoneIndex   The index of of the zone within this zone set.
     *
     * @returns Boolean indicating whether the zone is empty.
     */
    IFACEMETHOD_(bool, IsZoneEmpty)(ZoneIndex zoneIndex) const = 0;
    /**
     * Returns all zones spanned by the minimum bounding rectangle containing the two given zone index sets.
     * 
     * @param   initialZones   The indices of the first chosen zone (the anchor).
     * @param   finalZones     The indices of the last chosen zone (the current window position).
     *
     * @returns A vector indicating describing the chosen zone index set.
     */
    IFACEMETHOD_(ZoneIndexSet, GetCombinedZoneRange)(const ZoneIndexSet& initialZones, const ZoneIndexSet& finalZones) const = 0;
};

struct ZoneSetConfig
{
    ZoneSetConfig(
        GUID id,
        FancyZonesDataTypes::ZoneSetLayoutType layoutType,
        HMONITOR monitor,
        int sensitivityRadius,
        OverlappingZonesAlgorithm selectionAlgorithm = {}) noexcept :
            Id(id),
            LayoutType(layoutType),
            Monitor(monitor),
            SensitivityRadius(sensitivityRadius),
            SelectionAlgorithm(selectionAlgorithm)
    {
    }

    GUID Id{};
    FancyZonesDataTypes::ZoneSetLayoutType LayoutType{};
    HMONITOR Monitor{};
    int SensitivityRadius;
    OverlappingZonesAlgorithm SelectionAlgorithm = OverlappingZonesAlgorithm::Smallest;
};

winrt::com_ptr<IZoneSet> MakeZoneSet(ZoneSetConfig const& config) noexcept;
