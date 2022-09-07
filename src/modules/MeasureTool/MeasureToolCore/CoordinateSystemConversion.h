#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

namespace convert
{
    // Converts a given point from multi-monitor coordinate system to the one relative to HWND
    inline POINT FromSystemToWindow(HWND window, POINT p)
    {
        ScreenToClient(window, &p);
        return p;
    }
}
