#include "Resolution.h"


// TODO add error handling
// TODO break up large method
MonitorResolutionSettings* getAllResolutionSettings() {   
    std::vector<MonitorResolutionSettings> monitorDisplayDevices;
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
                monitorDisplayDevices.push_back(MonitorResolutionSettings(DeviceNameBuff, DeviceStringBuff));

                // read the current resolution settings
                EnumDisplaySettingsExW(displayAdapter.DeviceName, ENUM_CURRENT_SETTINGS, &mode, 0);
                monitorDisplayDevices.at(devicesCount).currentResolution = Resolution(mode.dmPelsWidth, mode.dmPelsHeight);
                int currentDisplayFrequency = mode.dmDisplayFrequency;
                mode = { 0 };
                mode.dmSize = sizeof(DEVMODE);

                // read the resolution options
                graphicsMode = 0;
                while (EnumDisplaySettingsExW(displayAdapter.DeviceName, graphicsMode, &mode, 0))
                {
                    if(mode.dmDisplayFrequency == currentDisplayFrequency)
                        resolutionOptions.push_back(Resolution(mode.dmPelsWidth, mode.dmPelsHeight)); // remove duplicates. maybe use a set

                    mode = { 0 };
                    mode.dmSize = sizeof(DEVMODE);
                    ++graphicsMode;
                }
            }
            Resolution* ro = &resolutionOptions[0];
            monitorDisplayDevices.at(devicesCount).possibleResolutions = ro;
            ++devicesCount;
            display = { 0 };
            display.cb = sizeof(DISPLAY_DEVICE);
        }
        ++adapterCount;
        displayAdapter = { 0 };
        displayAdapter.cb = sizeof(DISPLAY_DEVICE);

    }
    MonitorResolutionSettings* mrs = &monitorDisplayDevices[0];
    return mrs;
}


// TODO add error handling
bool setDisplayResolution(WCHAR* displayDeviceName, Resolution resolution) {
    DEVMODE desiredMode = { 0 };
    desiredMode.dmSize = sizeof(DEVMODE);
    desiredMode.dmPelsWidth = resolution.width;
    desiredMode.dmPelsHeight = resolution.height;
    desiredMode.dmFields = DM_PELSHEIGHT | DM_PELSWIDTH;

    LONG res = ChangeDisplaySettingsExW(displayDeviceName, &desiredMode, NULL, CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_RESET, NULL);

    if (res == DISP_CHANGE_BADPARAM || res == DISP_CHANGE_BADFLAGS) {
        return FALSE;
    }
    else if (res) {
        return TRUE;
    }
    return FALSE;
}


