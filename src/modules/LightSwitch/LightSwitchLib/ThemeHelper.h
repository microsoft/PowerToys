#pragma once

#include <windows.h>

inline constexpr wchar_t PERSONALIZATION_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
inline constexpr wchar_t NIGHT_LIGHT_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\Store\\DefaultAccount\\Current\\default$windows.data.bluelightreduction.bluelightreductionstate\\windows.data.bluelightreduction.bluelightreductionstate";
inline constexpr wchar_t LIGHT_SWITCH_TOGGLE_REQUEST_SEMAPHORE[] = L"Local\\PowerToys-LightSwitch-ToggleRequest-49904";

// These functions report registry and read-back failures without guessing a theme.
LSTATUS TryGetSystemTheme(bool& isLight);
LSTATUS TryGetAppsTheme(bool& isLight);
LSTATUS TrySetSystemTheme(bool isLight);
LSTATUS TrySetAppsTheme(bool isLight);

namespace LightSwitchThemeHelpers
{
    // The caller owns an open personalization key. These helpers do not broadcast.
    LSTATUS ReadThemeValue(HKEY key, const wchar_t* name, bool& isLight);
    LSTATUS WriteThemeValue(HKEY key, const wchar_t* name, bool isLight);
}

void SetSystemTheme(bool isLight);
void SetAppsTheme(bool isLight);
bool GetCurrentSystemTheme();
bool GetCurrentAppsTheme();
bool IsNightLightEnabled();
