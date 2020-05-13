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

struct MonitorDisplayDevice {
    WCHAR* displayAdapterName;
    WCHAR* monitorName;
    Resolution* possibleResolutions;
    Resolution currentResolution;

    MonitorDisplayDevice(WCHAR* _displayAdapterName, WCHAR* _monitorName) {
        displayAdapterName = _displayAdapterName;
        monitorName = _monitorName;
    }
};


std::vector<MonitorDisplayDevice> getAllMonitorDisplayDevices();
std::vector<Resolution> getAllPossibleDeviceResolutions(MonitorDisplayDevice* monitorDisplayDevice);