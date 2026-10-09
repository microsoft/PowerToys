#include "pch.h"
#include "ThemeHelper.h"

extern "C" __declspec(dllexport) void __cdecl LightSwitch_SetSystemTheme(bool isLight)
{
    SetSystemTheme(isLight);
}

extern "C" __declspec(dllexport) void __cdecl LightSwitch_SetAppsTheme(bool isLight)
{
    SetAppsTheme(isLight);
}

extern "C" __declspec(dllexport) bool __cdecl LightSwitch_GetCurrentSystemTheme()
{
    return GetCurrentSystemTheme();
}

extern "C" __declspec(dllexport) bool __cdecl LightSwitch_GetCurrentAppsTheme()
{
    return GetCurrentAppsTheme();
}
