#include <iostream>

#include <vector>
#include <windows.h>
#include <winuser.h>

#include "resolution.h"


extern "C" __declspec(dllexport) bool setResolution(wchar_t*, int, int);

bool setResolution(const wchar_t* displayName, int pixelWidth, int pixelHeight)
{
    wchar_t* wname = const_cast<wchar_t*>(L"\\\\.\\DISPLAY1");
    if (setDisplayResolution(wname, Resolution(pixelWidth, pixelHeight)))
        return true;

    return false;
}
