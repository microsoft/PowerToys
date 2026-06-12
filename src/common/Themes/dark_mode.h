#pragma once
#include <windows.h>

// Lightweight helpers for opting native Win32 popup menus into the
// system dark / light theme. Built on top of undocumented uxtheme.dll
// ordinals (SetPreferredAppMode / FlushMenuThemes / AllowDarkModeForWindow)
// that ship with Windows 10 1903+ and Windows 11.
struct DarkMode
{
    // Loads the uxtheme.dll ordinals (idempotent) and applies the current
    // system theme as the preferred app mode. Safe to call multiple times.
    static void Initialize();

    // Re-evaluates the current system theme and re-applies the preferred
    // app mode. Call this from a theme-change handler.
    static void Refresh();

    // Returns true if the system is currently in dark mode.
    static bool IsDarkModeEnabled();

    // Applies a dark or light background brush to the given menu and all of
    // its submenus, based on the current system theme. No-op when the
    // uxtheme.dll ordinals are unavailable.
    static void ApplyToMenu(HMENU menu);
};
