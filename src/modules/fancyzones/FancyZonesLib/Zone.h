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
class Zone
{
public:
    Zone(const RECT& zoneRect, const ZoneIndex zoneIndex);
    Zone(const Zone& other);
    ~Zone() = default;

    ZoneIndex Id() const noexcept;
    bool IsValid() const noexcept;
    RECT GetZoneRect() const noexcept;
    long GetZoneArea() const noexcept;

private:
    const RECT m_rect;
    const ZoneIndex m_index;

    bool isValid() const noexcept;
};
