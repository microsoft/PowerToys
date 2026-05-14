// Native Win32 dark-mode helpers built on top of the undocumented
// uxtheme.dll ordinals shipped with Windows 10 1903+ / Windows 11.
//
// Reference: https://github.com/microsoft/PowerToys/issues/31813
// Precedent: src/modules/ZoomIt/ZoomIt/Utility.cpp
#include "dark_mode.h"
#include "theme_helpers.h"

#include <mutex>

namespace
{
    enum class PreferredAppMode
    {
        Default,
        AllowDark,
        ForceDark,
        ForceLight,
        Max
    };

    using fnSetPreferredAppMode = PreferredAppMode(WINAPI*)(PreferredAppMode appMode);
    using fnShouldAppsUseDarkMode = bool(WINAPI*)();
    using fnFlushMenuThemes = void(WINAPI*)();

    fnSetPreferredAppMode pSetPreferredAppMode = nullptr;
    fnShouldAppsUseDarkMode pShouldAppsUseDarkMode = nullptr;
    fnFlushMenuThemes pFlushMenuThemes = nullptr;

    std::once_flag init_flag;
    HBRUSH dark_menu_brush = nullptr;

    // Mirrors the surface color used by ZoomIt's dark menus for visual
    // consistency across PowerToys-owned native menus.
    constexpr COLORREF DarkMenuSurfaceColor = RGB(45, 45, 45);

    void LoadOrdinals()
    {
        HMODULE hUxTheme = GetModuleHandleW(L"uxtheme.dll");
        if (!hUxTheme)
        {
            hUxTheme = LoadLibraryExW(L"uxtheme.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
        }
        if (!hUxTheme)
        {
            return;
        }

        pSetPreferredAppMode = reinterpret_cast<fnSetPreferredAppMode>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(135)));
        pShouldAppsUseDarkMode = reinterpret_cast<fnShouldAppsUseDarkMode>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(132)));
        pFlushMenuThemes = reinterpret_cast<fnFlushMenuThemes>(
            GetProcAddress(hUxTheme, MAKEINTRESOURCEA(136)));
    }

    void ApplyPreferredAppMode()
    {
        if (!pSetPreferredAppMode)
        {
            return;
        }

        const bool dark = DarkMode::IsDarkModeEnabled();
        pSetPreferredAppMode(dark ? PreferredAppMode::ForceDark : PreferredAppMode::ForceLight);

        if (pFlushMenuThemes)
        {
            pFlushMenuThemes();
        }
    }
}

void DarkMode::Initialize()
{
    std::call_once(init_flag, LoadOrdinals);
    ApplyPreferredAppMode();
}

void DarkMode::Refresh()
{
    Initialize();
}

bool DarkMode::IsDarkModeEnabled()
{
    if (pShouldAppsUseDarkMode)
    {
        return pShouldAppsUseDarkMode();
    }

    return ThemeHelpers::GetSystemTheme() == Theme::Dark;
}

void DarkMode::ApplyToMenu(HMENU menu)
{
    if (!menu)
    {
        return;
    }

    MENUINFO mi = { sizeof(mi) };
    mi.fMask = MIM_BACKGROUND | MIM_APPLYTOSUBMENUS;

    if (IsDarkModeEnabled())
    {
        if (!dark_menu_brush)
        {
            dark_menu_brush = CreateSolidBrush(DarkMenuSurfaceColor);
        }
        mi.hbrBack = dark_menu_brush;
    }
    else
    {
        mi.hbrBack = nullptr;
    }

    SetMenuInfo(menu, &mi);
}
