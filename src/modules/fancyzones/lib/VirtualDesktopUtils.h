#pragma once

#include "ZoneWindow.h"

namespace VirtualDesktopUtils
{
    bool GetWindowDesktopId(HWND topLevelWindow, GUID* desktopId);
    bool GetZoneWindowDesktopId(IZoneWindow* zoneWindow, GUID* desktopId);
}
