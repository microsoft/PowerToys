#include <iostream>

#include <vector>
#include <windows.h>
#include <winuser.h>
#include <comdef.h>
#include <algorithm>
#include "resolution.h"

 bool setResolution(wchar_t*, int, int);

 extern "C" __declspec(dllexport) void getAllDisplaySettings(MonitorResolutionSettings* settings)
{
    MonitorResolutionSettings* s = getAllResolutionSettings();
    settings->displayAdapterName = s->displayAdapterName;
    settings->monitorName = s->monitorName;
    settings->currentResolution.height = s->currentResolution.height;
    settings->currentResolution.width = s->currentResolution.width;
    settings->res1.width = 2560;
    settings->res1.height = 2048;
    settings->res2.width = 2560;
    settings->res2.height = 1920;
    settings->res3.width = 2048;
    settings->res3.height = 1536;
    settings->res4.width = 2560;
    settings->res4.height = 1600;
    settings->res5.width = 1920;
    settings->res5.height = 1080;

    /*for (int x = 0; x < 1; x++) {
        settings[x].displayAdapterName = s[x].displayAdapterName;
        settings[x].monitorName = s[x].monitorName;
        settings[x].currentResolution.height = s[x].currentResolution.height;
        settings[x].currentResolution.width = s[x].currentResolution.width;
        settings[x].res1.width = 2560;
        settings[x].res1.height = 2048;
        settings[x].res2.width = 2560;
        settings[x].res2.height = 1920;
        settings[x].res3.width = 2048;
        settings[x].res3.height = 1536;
        settings[x].res4.width = 2560;
        settings[x].res4.height = 1600;
        settings[x].res5.width = 1920;
        settings[x].res5.height = 1080;
    }*/
    
    /*settings->resolutionOptions = (Resolution*)malloc(sizeof(Resolution) * 10);
    for (int x = 0; x < 10; x++) {
        settings->resolutionOptions[x].height = s->resolutionOptions[x].height;
        settings->resolutionOptions[x].width = s->resolutionOptions[x].width;
    }*/
    free(s);
}

extern "C" __declspec(dllexport) bool setResolution(const wchar_t* displayName, int pixelWidth, int pixelHeight)
{
    wchar_t* wname = const_cast<wchar_t*>(L"\\\\.\\DISPLAY1");

    Resolution res = Resolution();
    res.width = pixelWidth;
    res.height = pixelHeight;

    if (setDisplayResolution(wname, res))
        return true;

    return false;
}
