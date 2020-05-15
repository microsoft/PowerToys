#include "Resolution.h"
#include <comdef.h>

// TODO add error handling
// TODO break up large method
MonitorResolutionSettings* getAllResolutionSettings()
{
    MonitorResolutionSettings* monitorDisplayDevices = (MonitorResolutionSettings*)malloc(sizeof(MonitorResolutionSettings) * 10);

    int adapterCount = 0, devicesCount = 0, graphicsMode = 0;

    DISPLAY_DEVICE display = { 0 };
    DISPLAY_DEVICE displayAdapter = { 0 };
    DEVMODE mode = { 0 };

    display.cb = sizeof(DISPLAY_DEVICE);
    displayAdapter.cb = sizeof(DISPLAY_DEVICE);
    mode.dmSize = sizeof(DEVMODE);

    while (EnumDisplayDevicesW(NULL, adapterCount, &displayAdapter, 0))
    {
        devicesCount = 0;
        while (EnumDisplayDevicesW(displayAdapter.DeviceName, devicesCount, &display, 0))
        {
            //monitorDisplayDevices[devicesCount].resolutionOptions = (Resolution*)malloc(sizeof(Resolution) * 150);

            int resolutionOptionsCount = 0;

            // TODO check that this criteria filters out anything that isn't a monitor.
            if ((display.StateFlags & DISPLAY_DEVICE_ACTIVE) && (display.StateFlags & DISPLAY_DEVICE_ATTACHED_TO_DESKTOP) && !(display.StateFlags & DISPLAY_DEVICE_MIRRORING_DRIVER))
            {
                WCHAR DeviceNameBuff[40];
                WCHAR DeviceStringBuff[40];
                wcscpy_s(DeviceNameBuff, displayAdapter.DeviceName);
                wcscpy_s(DeviceStringBuff, display.DeviceString);
                //monitorDisplayDevices[devicesCount] = MonitorResolutionSettings(SysAllocString(DeviceNameBuff), SysAllocString(DeviceStringBuff));
                monitorDisplayDevices[devicesCount].displayAdapterName = SysAllocString(DeviceNameBuff);
                monitorDisplayDevices[devicesCount].monitorName = SysAllocString(DeviceStringBuff);

                // read the current resolution settings
                EnumDisplaySettingsExW(displayAdapter.DeviceName, ENUM_CURRENT_SETTINGS, &mode, 0);
                monitorDisplayDevices[devicesCount].currentResolution.width = mode.dmPelsWidth;
                monitorDisplayDevices[devicesCount].currentResolution.height = mode.dmPelsHeight;
                int currentDisplayFrequency = mode.dmDisplayFrequency;
                mode = { 0 };
                mode.dmSize = sizeof(DEVMODE);

                // read the resolution options
                graphicsMode = 0;
                while (EnumDisplaySettingsExW(displayAdapter.DeviceName, graphicsMode, &mode, 0))
                {
                    //if (mode.dmDisplayFrequency == currentDisplayFrequency)
                    //{
                    //    monitorDisplayDevices[devicesCount].resolutionOptions[graphicsMode].width = mode.dmPelsWidth;
                    //    monitorDisplayDevices[devicesCount].resolutionOptions[graphicsMode].height = mode.dmPelsHeight;
                    //}

                    mode = { 0 };
                    mode.dmSize = sizeof(DEVMODE);
                    ++graphicsMode;
                }
            }
            ++devicesCount;
            display = { 0 };
            display.cb = sizeof(DISPLAY_DEVICE);
        }
        ++adapterCount;
        displayAdapter = { 0 };
        displayAdapter.cb = sizeof(DISPLAY_DEVICE);
    }
    return monitorDisplayDevices;
}

// TODO add error handling
bool setDisplayResolution(WCHAR* displayDeviceName, Resolution resolution)
{
    DEVMODE desiredMode = { 0 };
    desiredMode.dmSize = sizeof(DEVMODE);
    desiredMode.dmPelsWidth = resolution.width;
    desiredMode.dmPelsHeight = resolution.height;
    desiredMode.dmFields = DM_PELSHEIGHT | DM_PELSWIDTH;

    LONG res = ChangeDisplaySettingsExW(displayDeviceName, &desiredMode, NULL, CDS_UPDATEREGISTRY | CDS_GLOBAL | CDS_RESET, NULL);

    if (res == DISP_CHANGE_BADPARAM || res == DISP_CHANGE_BADFLAGS)
    {
        return FALSE;
    }
    else if (res)
    {
        return TRUE;
    }
    return FALSE;
}
