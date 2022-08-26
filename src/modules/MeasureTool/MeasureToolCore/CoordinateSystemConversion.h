#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace convert
{
    // Converts a given point from multi-monitor coordinate system to the one relative to HWND
    inline POINT FromSystemToRelative(HWND window, POINT p)
    {
        ScreenToClient(window, &p);
        return p;
    }

    // Converts a given point from multi-monitor coordinate system to the one relative to HWND and also ready
    // to be used in Direct2D calls with AA mode set to aliased
    inline POINT FromSystemToRelativeForDirect2D(HWND window, POINT p)
    {
        ScreenToClient(window, &p);
        // Submitting DrawLine calls to Direct2D with thickness == 1.f and AA mode set to aliased causes
        // them to be drawn offset by [1,1] toward upper-left corner, so we must to compensate for that.
        ++p.x;
        ++p.y;
        return p;
    }
}
