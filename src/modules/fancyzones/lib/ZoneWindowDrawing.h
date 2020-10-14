#pragma once

#include <map>
#include <vector>
#include <wil\resource.h>
#include <winrt/base.h>
#include <d2d1.h>

#include "util.h"
#include "Zone.h"
#include "ZoneSet.h"

namespace ZoneWindowDrawingNS
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
                           const IZoneSet::ZonesMap& zones,
                           const std::vector<size_t>& highlightZones,
                           bool flashMode) noexcept;
}

class ZoneWindowDrawing
{
    HWND m_window;
    winrt::com_ptr<IZoneWindowHost> m_host;

public:
    ZoneWindowDrawing(HWND window);
    void StartAnimation(unsigned millis);
    void DrawActiveZoneSet(const std::vector<winrt::com_ptr<IZone>>& zones, const std::vector<size_t>& highlightZones);
};