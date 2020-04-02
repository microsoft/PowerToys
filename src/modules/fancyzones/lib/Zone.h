#pragma once

/**
 * Class representing one zone inside applied zone layout, which is basically wrapper around rectangle structure.
 */
interface __declspec(uuid("{8228E934-B6EF-402A-9892-15A1441BF8B0}")) IZone : public IUnknown
{
    /**
     * @returns Zone coordinates (top-left and bottom-right corner) represented as RECT structure.
     */
    IFACEMETHOD_(RECT, GetZoneRect)() = 0;
    /**
     * @returns Boolean indicating if zone is empty or there are windows assigned to it.
     */
    IFACEMETHOD_(bool, IsEmpty)() = 0;
    /**
     * @param   window Window handle.
     * @returns Boolean indicating if specified window is assigned to the zone.
     */
    IFACEMETHOD_(bool, ContainsWindow)(HWND window) = 0;
    /**
     * Assign single window to this zone.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @param   stampZone  Boolean indicating weather we should add special property on the
     *                     window. This property is used on display change to rearrange windows
     *                     to corresponding zones.
     */
    IFACEMETHOD_(void, AddWindowToZone)(HWND window, HWND zoneWindow, bool stampZone) = 0;
    /**
     * Remove window from this zone (if it is assigned to it).
     *
     * @param   window      Handle of window to be removed from this zone.
     * @param   restoreSize Boolean indicating that window should fall back to dimensions
     *                      before assigning to this zone.
     */
    IFACEMETHOD_(void, RemoveWindowFromZone)(HWND window, bool restoreSize) = 0;
    /**
     * @param   id Zone identifier.
     */
    IFACEMETHOD_(void, SetId)(size_t id) = 0;
    /**
     * @returns Zone identifier.
     */
    IFACEMETHOD_(size_t, Id)() = 0;

    /**
     * Compute the coordinates of the rectangle to which a window should be resized.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a ZoneWindow, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @returns a RECT structure, describing global coordinates to which a window should be resized
     */
    IFACEMETHODIMP_(RECT) ComputeActualZoneRect(HWND window, HWND zoneWindow) noexcept;

};

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect) noexcept;
