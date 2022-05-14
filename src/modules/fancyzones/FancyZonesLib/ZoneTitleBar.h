#pragma once
#include "util.h"
#include "Window.h"
#include "Settings.h"


class IZoneTitleBar
{
public:
    virtual ~IZoneTitleBar() = default;
    virtual void Show(bool show) = 0;
    virtual void UpdateZoneWindows(std::vector<HWND> zoneWindows) = 0;
    virtual void ReadjustPos() = 0;
    virtual FancyZonesUtils::Rect GetInlineFrame() const = 0;
};

std::unique_ptr<IZoneTitleBar> MakeZoneTitleBar(ZoneTitleBarStyle style, HINSTANCE hinstance, FancyZonesUtils::Rect zone, UINT dpi);