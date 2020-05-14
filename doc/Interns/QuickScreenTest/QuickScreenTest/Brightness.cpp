#include <PhysicalMonitorEnumerationAPI.h>   
#include <HighLevelMonitorConfigurationAPI.h>   
#include <windows.h>   
#include <vector>

#include "Brightness.h"

// TODO check if monitor vendor supports this api. 
// TODO add propper error handling
std::vector<MonitorBrightnessSettings> getAllBrightnessSettings() {
    
    std::vector<MonitorBrightnessSettings> settings;
    std::vector<PHYSICAL_MONITOR> monitorArray;
    if (!EnumDisplayMonitors(NULL, NULL, &MonitorEnumProc, reinterpret_cast<LPARAM>(&monitorArray))) // TODO handle error case
        return settings;
  
    DWORD  maxBrightness, currentBrightness, minBrightness;
    HANDLE physicalMonitorHandle = NULL;

    for (int i = 0; i < monitorArray.size(); i++)
    {
        physicalMonitorHandle = monitorArray.at(i).hPhysicalMonitor;
        WCHAR* name = monitorArray.at(i).szPhysicalMonitorDescription;
        WCHAR nameBuffer[128];
        wcscpy_s(nameBuffer, name);
        BOOL success = GetMonitorBrightness(physicalMonitorHandle, &minBrightness, &currentBrightness, &maxBrightness);
        if (success)
        {
            settings.push_back(MonitorBrightnessSettings(nameBuffer, minBrightness, maxBrightness, currentBrightness));
        }
        else
        {
            wchar_t buf[256];
            FormatMessageW(FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS,
                NULL, GetLastError(), MAKELANGID(LANG_NEUTRAL, SUBLANG_DEFAULT),
                buf, (sizeof(buf) / sizeof(wchar_t)), NULL);
            printf("Error: %ls\n", buf);
        }
    }
    return settings;
}

//Note: The rectangle coordinates are virtual-screen coordinates.
BOOL CALLBACK MonitorEnumProc(HMONITOR hMonitor, HDC hdcMonitor, LPRECT lprcMonitor, LPARAM dwData)
{
    MONITORINFOEX iMonitor;
    iMonitor.cbSize = sizeof(MONITORINFOEX);
    GetMonitorInfo(hMonitor, &iMonitor);

    if (iMonitor.dwFlags == DISPLAY_DEVICE_MIRRORING_DRIVER)
    {
        return true;
    }
    else
    {
        DWORD numPhysicalMonitors;
        bool bSuccess = GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, &numPhysicalMonitors); // why would a monitor handle reference more than one physical monitor.

        LPPHYSICAL_MONITOR physicalMonitors = (LPPHYSICAL_MONITOR)malloc(numPhysicalMonitors * sizeof(PHYSICAL_MONITOR)); // TODO free this.
        bSuccess = GetPhysicalMonitorsFromHMONITOR(hMonitor, numPhysicalMonitors, physicalMonitors);

        for (int x = 0; x < numPhysicalMonitors; x++) {
            reinterpret_cast<std::vector<PHYSICAL_MONITOR>*>(dwData)->push_back(physicalMonitors[x]);
        }
        return true;
    };

    return TRUE;
}