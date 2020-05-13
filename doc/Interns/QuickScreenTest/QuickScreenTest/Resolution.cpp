#include "Resolution.h"


// TODO move this out, it's common to every graphics setting
// TODO fix memory management -- the display devices aren't being kept. use pointer to a pointer?
std::vector<MonitorDisplayDevice> getAllMonitorDisplayDevices() {   
    std::vector<MonitorDisplayDevice> monitorDisplayDevice;
    int adapterCount = 0, graphicsModeCount = 0, k = 0;

    DISPLAY_DEVICE display, displayAdapter;
    memset(&displayAdapter, 0, sizeof(DISPLAY_DEVICE));
    memset(&display, 0, sizeof(DISPLAY_DEVICE));
    display.cb = sizeof(DISPLAY_DEVICE);
    displayAdapter.cb = sizeof(DISPLAY_DEVICE);
    while (EnumDisplayDevicesW(NULL, adapterCount, &displayAdapter, 0)){
        graphicsModeCount = 0;

        while (EnumDisplayDevicesW(displayAdapter.DeviceName, graphicsModeCount, &display, 0)) 
        {
            // TODO check that this criteria filters out anything that isn't a monitor.
            if ((display.StateFlags & DISPLAY_DEVICE_ACTIVE)
                && (display.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP)
                && !(display.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER))
            {
 
                WCHAR DeviceNameBuff[40];
                WCHAR DeviceStringBuff[40];

                wcscpy_s(DeviceNameBuff, displayAdapter.DeviceName);
                wcscpy_s(DeviceStringBuff, display.DeviceString);

                monitorDisplayDevice.push_back(MonitorDisplayDevice(DeviceNameBuff, DeviceStringBuff)); 
            }
            ++graphicsModeCount;

        }
        ++adapterCount;
    }
    return monitorDisplayDevice;
}