#pragma once

#include "ZoneWindow.h"

namespace VirtualDesktopUtils
{
    bool GetWindowDesktopId(HWND topLevelWindow, GUID* desktopId);
    bool GetCurrentVirtualDesktopId(GUID* desktopId);
    bool GetVirtualDesktopIds(std::vector<GUID>& ids);
    HKEY GetVirtualDesktopsRegKey();
    void HandleVirtualDesktopUpdates(HWND window, UINT message, HANDLE terminateEvent);
}
