// Theme-aware rendering for classic Win32 popup menus (HMENU / TrackPopupMenu).
//
// Lets a Win32 tray/popup menu follow the OS light/dark theme. It puts the
// process into the matching app theme via uxtheme's preferred-app-mode entry
// points -- the same mechanism the OS uses to render dark menus, as already
// used by ZoomIt and File Explorer -- and then the system draws the real
// themed menu. Native keyboard, accessibility, checkmarks, separators and DPI
// are all preserved; only the colors change.
//
// Theme detection reads the documented AppsUseLightTheme value, fresh on each
// call, so a live light<->dark switch is reflected without restarting.
//
// Drop-in for any PowerToys system-tray utility:
//
//     theme::dark_menu::SetAppMode(theme::dark_menu::IsSystemDarkMode());
//     TrackPopupMenu(menu, ...);
#pragma once

#include <windows.h>

namespace theme::dark_menu
{
    namespace details
    {
        // uxtheme preferred-app-mode values (order matters).
        enum class PreferredAppMode
        {
            Default = 0,
            AllowDark,
            ForceDark,
            ForceLight,
            Max
        };

        using SetPreferredAppModeFn = PreferredAppMode(WINAPI*)(PreferredAppMode);
        using FlushMenuThemesFn = void(WINAPI*)();

        struct Ordinals
        {
            SetPreferredAppModeFn setPreferredAppMode = nullptr;
            FlushMenuThemesFn flushMenuThemes = nullptr;
        };

        // Resolved once per process: an inline function's local static is a single
        // shared instance across all translation units that include this header.
        inline const Ordinals& GetOrdinals() noexcept
        {
            static const Ordinals ordinals = []() noexcept {
                Ordinals result{};
                HMODULE uxtheme = GetModuleHandleW(L"uxtheme.dll");
                if (uxtheme == nullptr)
                {
                    uxtheme = LoadLibraryExW(L"uxtheme.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
                }
                if (uxtheme != nullptr)
                {
                    // Ordinal 135 = SetPreferredAppMode, ordinal 136 = FlushMenuThemes.
                    result.setPreferredAppMode = reinterpret_cast<SetPreferredAppModeFn>(
                        GetProcAddress(uxtheme, MAKEINTRESOURCEA(135)));
                    result.flushMenuThemes = reinterpret_cast<FlushMenuThemesFn>(
                        GetProcAddress(uxtheme, MAKEINTRESOURCEA(136)));
                }
                return result;
            }();
            return ordinals;
        }
    }

    // True if the user's app theme is dark, via the documented AppsUseLightTheme value.
    inline bool IsSystemDarkMode() noexcept
    {
        DWORD value = 1; // default to light if the value is missing
        DWORD size = sizeof(value);
        RegGetValueW(HKEY_CURRENT_USER,
                     L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize",
                     L"AppsUseLightTheme", RRF_RT_REG_DWORD, nullptr, &value, &size);
        return value == 0;
    }

    // Make this process's classic popup menus render dark or light. Cheap and
    // idempotent -- call right before TrackPopupMenu so the menu always matches
    // the current theme, including after a live theme switch. No-op if those
    // entry points are unavailable.
    inline void SetAppMode(bool dark) noexcept
    {
        const details::Ordinals& ordinals = details::GetOrdinals();
        if (ordinals.setPreferredAppMode != nullptr)
        {
            ordinals.setPreferredAppMode(dark ? details::PreferredAppMode::ForceDark
                                              : details::PreferredAppMode::ForceLight);
        }
        if (ordinals.flushMenuThemes != nullptr)
        {
            ordinals.flushMenuThemes();
        }
    }
}
