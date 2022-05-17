#pragma once
#include <windows.h>

enum class CurrentTheme
{
    Dark = 0,
    Light = 1
};

struct ThemeHelpers
{
    static CurrentTheme GetSystemTheme();
    static void ThemeHelpers::SetImmersiveDarkMode(HWND window, bool enabled);
    static void ThemeHelpers::RegisterForImmersiveDarkMode(HWND window);
};