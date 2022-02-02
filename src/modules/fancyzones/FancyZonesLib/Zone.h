#pragma once

namespace ZoneConstants
{
    constexpr int MAX_NEGATIVE_SPACING = -20;
}

using ZoneIndex = int64_t;
using ZoneIndexSet = std::vector<ZoneIndex>;

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
};

winrt::com_ptr<IZone> MakeZone(const RECT& zoneRect, const ZoneIndex zoneId) noexcept;
