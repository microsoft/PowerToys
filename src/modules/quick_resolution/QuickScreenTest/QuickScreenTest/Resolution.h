#pragma once
#include <vector>
#include <windows.h>
#include <winuser.h>

struct Resolution
{
    int width;
    int height;

    Resolution() {}
    Resolution(int _width, int _height)
    {
        width = _width;
        height = _height;
    }
};

struct MonitorResolutionSettings {
    WCHAR* displayAdapterName; 
    WCHAR* monitorName;
    Resolution* possibleResolutions;
    Resolution currentResolution;

    MonitorResolutionSettings(WCHAR* _displayAdapterName, WCHAR* _monitorName) {
        displayAdapterName = _displayAdapterName;
        monitorName = _monitorName;
    }
};

MonitorResolutionSettings* getAllResolutionSettings();
bool setDisplayResolution(WCHAR* displayDeviceName, Resolution resolution);
