#pragma once

inline RECT ClientAreaInScreenSpace(HWND window)
{
    POINT clientOrigin = { 0, 0 };
    winrt::check_bool(ClientToScreen(window, &clientOrigin));
    RECT windowBounds = {};
    winrt::check_bool(GetClientRect(window, &windowBounds));
    windowBounds.left += clientOrigin.x;
    windowBounds.top += clientOrigin.y;
    windowBounds.right += clientOrigin.x;
    windowBounds.bottom += clientOrigin.y;
    return windowBounds;
}
