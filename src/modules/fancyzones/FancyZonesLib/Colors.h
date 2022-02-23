#pragma once

#include <windef.h>

namespace Colors
{
    struct ZoneColors
    {
        COLORREF primaryColor;
        COLORREF borderColor;
        COLORREF highlightColor;
        COLORREF numberColor;
        int highlightOpacity;
    };

    ZoneColors GetZoneColors() noexcept;
}