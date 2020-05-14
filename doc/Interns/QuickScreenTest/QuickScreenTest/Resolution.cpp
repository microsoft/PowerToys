#include "Resolution.h"


// TODO move this out, it's common to every graphics setting
// TODO fix memory management -- the display devices aren't being kept. use pointer to a pointer?
std::vector<MonitorDisplayDevice> getAllMonitorDisplayDevices() {   
    std::vector<MonitorDisplayDevice> monitorDisplayDevice;
    int adapterCount = 0, devicesCount = 0, graphicsMode = 0;

    DISPLAY_DEVICE display = { 0 };
    DISPLAY_DEVICE displayAdapter = { 0 };
    DEVMODE mode = { 0 };

    display.cb = sizeof(DISPLAY_DEVICE);
    displayAdapter.cb = sizeof(DISPLAY_DEVICE);
    mode.dmSize = sizeof(DEVMODE);

    while (EnumDisplayDevicesW(NULL, adapterCount, &displayAdapter, 0)){
        devicesCount = 0;
        while (EnumDisplayDevicesW(displayAdapter.DeviceName, devicesCount, &display, 0)) 
        {
            std::vector<Resolution> resolutionOptions;

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

                // read the display settings
                graphicsMode = 0;
                while (EnumDisplaySettingsEx(displayAdapter.DeviceName, graphicsMode, &mode, 0))
                {
                    //TODO only add the resolution setting if it's compatible with the current refresh rate. 
                    resolutionOptions.push_back(Resolution(mode.dmPelsWidth, mode.dmPelsHeight)); // remove duplicates. maybe use a set

                    mode = { 0 };
                    mode.dmSize = sizeof(DEVMODE);
                    ++graphicsMode;
                }
            }
            monitorDisplayDevice.at(devicesCount).possibleResolutions = resolutionOptions;
            ++devicesCount;
            display = { 0 };
            display.cb = sizeof(DISPLAY_DEVICE);
        }
        ++adapterCount;
        displayAdapter = { 0 };
        displayAdapter.cb = sizeof(DISPLAY_DEVICE);

    }
    return monitorDisplayDevice;
}