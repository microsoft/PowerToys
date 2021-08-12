#pragma once

namespace ZoneConstants
{
    constexpr int MAX_NEGATIVE_SPACING = -10;
}

using ZoneIndex = int64_t;
using ZoneIndexSet = std::vector<ZoneIndex>;
using Bitmask = ZoneIndex;

/**
 * Class representing one zone inside applied zone layout, which is basically wrapper around rectangle structure.
 */
interface __declspec(uuid("{8228E934-B6EF-402A-9892-15A1441BF8B0}")) IZone : public IUnknown
{
    /**
     * @returns Zone coordinates (top-left and bottom-right corner) represented as RECT structure.
     */
    IFACEMETHOD_(RECT, GetZoneRect)() const = 0;
    /**
     * @returns Zone area calculated from zone rect
     */
    IFACEMETHOD_(long, GetZoneArea)() const = 0;
    /**
     * @returns Zone identifier.
     */
    IFACEMETHOD_(ZoneIndex, Id)() const = 0;
    /**
     * Compute the coordinates of the rectangle to which a window should be resized.
     *
     * @param   window     Handle of window which should be assigned to zone.
     * @param   zoneWindow The m_window of a WorkArea, it's a hidden window representing the
     *                     current monitor desktop work area.
     * @returns a RECT structure, describing global coordinates to which a window should be resized
     */
    IFACEMETHOD_(RECT, ComputeActualZoneRect)(HWND window, HWND zoneWindow) const = 0;

};

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect, const ZoneIndex zoneId) noexcept;
