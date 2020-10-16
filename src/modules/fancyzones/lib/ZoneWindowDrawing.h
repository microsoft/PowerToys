#pragma once

#include <vector>
#include <wil\resource.h>
#include <winrt/base.h>

#include "util.h"
#include "Zone.h"

namespace ZoneWindowDrawing
{
    struct ColorSetting
    {
        BYTE fillAlpha{};
        COLORREF fill{};
        BYTE borderAlpha{};
        COLORREF border{};
        int thickness{};
    };

    void DrawBackdrop(wil::unique_hdc& hdc, RECT const& clientRect) noexcept;
    void DrawActiveZoneSet(wil::unique_hdc& hdc,
                           COLORREF zoneColor,
                           COLORREF zoneBorderColor,
                           COLORREF highlightColor,
                           int zoneOpacity,
                           const std::vector<winrt::com_ptr<IZone>>& zones,
                           const std::vector<size_t>& highlightZones,
                           bool flashMode) noexcept;
}
