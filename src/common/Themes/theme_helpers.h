#pragma once
#include <windows.h>

enum class Theme
{
    Dark = 0,
    Light = 1
};

struct ThemeHelpers
{
    static Theme GetAppTheme();
    static Theme GetSystemTheme();
    static void SetImmersiveDarkMode(HWND window, bool enabled);

protected:
    static Theme ThemeRegistryHelper(LPCWSTR theme_key);
};