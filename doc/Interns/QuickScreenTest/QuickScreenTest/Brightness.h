#pragma once

#include <PhysicalMonitorEnumerationAPI.h>   
#include <HighLevelMonitorConfigurationAPI.h>   
#include <windows.h>  

struct MonitorBrightnessSettings {
    WCHAR* monitorName;
    int minBrightness;
    int maxBrightness;
    int currentBrightness;

    MonitorBrightnessSettings(WCHAR* _monitorName, int _minBrightness, int _maxBrightness, int _currentBrightness) {
        monitorName = _monitorName;
        minBrightness = _minBrightness;
        maxBrightness = _maxBrightness;
        currentBrightness = _currentBrightness;
    }
};

BOOL CALLBACK MonitorEnumProc(HMONITOR hMonitor, HDC hdcMonitor, LPRECT lprcMonitor, LPARAM dwData);
std::vector<MonitorBrightnessSettings> getAllBrightnessSettings();