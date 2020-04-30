#pragma once

#include "ZoneWindow.h"

namespace VirtualDesktopUtils
{
    bool GetWindowDesktopId(HWND topLevelWindow, GUID* desktopId);
    bool GetZoneWindowDesktopId(IZoneWindow* zoneWindow, GUID* desktopId);
    bool GetCurrentVirtualDesktopId(GUID* desktopId);
    bool GetVirtualDekstopIds(std::vector<GUID>& ids);
    HKEY GetVirtualDesktopsRegKey();
    void CloseVirtualDesktopsRegKey();
}
