#pragma once
#include <vector>
#include <windows.h>
#include <winuser.h>

struct Resolution
{
    int width;
    int height;

    //Resolution() {}
    //Resolution(int _width, int _height)
    //{
    //    width = _width;
    //    height = _height;
    //}
};

struct MonitorResolutionSettings
{
    LPWSTR displayAdapterName;
    LPWSTR monitorName;
    Resolution currentResolution;
    Resolution* resolutionOptions;
    Resolution res1; // TODO this should be an array, but it requires more complex marshalling. 
    Resolution res2;
    Resolution res3;
    Resolution res4; 
    Resolution res5;

    //MonitorResolutionSettings(BSTR _displayAdapterName, BSTR _monitorName)
    //{
    //    displayAdapterName = _displayAdapterName;
    //    monitorName = _monitorName;
    //}
};

MonitorResolutionSettings* getAllResolutionSettings();
bool setDisplayResolution(WCHAR* displayDeviceName, Resolution resolution);
