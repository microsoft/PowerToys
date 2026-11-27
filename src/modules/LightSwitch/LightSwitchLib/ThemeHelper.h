#pragma once

#include <windows.h>
#include <optional>

inline constexpr wchar_t PERSONALIZATION_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize";
inline constexpr wchar_t LIGHT_SWITCH_STATE_REGISTRY_PATH[] = L"Software\\Microsoft\\PowerToys\\LightSwitch";
inline constexpr wchar_t SAVED_COLOR_PREVALENCE_VALUE[] = L"SavedColorPrevalence";
inline constexpr wchar_t NIGHT_LIGHT_REGISTRY_PATH[] = L"Software\\Microsoft\\Windows\\CurrentVersion\\CloudStore\\Store\\DefaultAccount\\Current\\default$windows.data.bluelightreduction.bluelightreductionstate\\windows.data.bluelightreduction.bluelightreductionstate";
inline constexpr wchar_t LIGHT_SWITCH_TOGGLE_REQUEST_SEMAPHORE[] = L"Local\\PowerToys-LightSwitch-ToggleRequest-49904";

// Reads report registry failures without guessing a theme.
LSTATUS TryGetSystemTheme(bool& isLight);
LSTATUS TryGetAppsTheme(bool& isLight);
// Writes report registry errors and broadcast changes. The service verifies the final theme snapshot.
LSTATUS TrySetSystemTheme(bool isLight);
LSTATUS TrySetAppsTheme(bool isLight);
// Leaves enabled unchanged when the registry value cannot be read or validated.
LSTATUS TryGetNightLightState(bool& enabled);

namespace LightSwitchThemeHelpers
{
    // The caller owns an open personalization key. These helpers do not broadcast.
    LSTATUS ReadThemeValue(HKEY key, const wchar_t* name, bool& isLight);
    LSTATUS WriteThemeValue(HKEY key, const wchar_t* name, bool isLight);

    // The caller owns open personalization and Light Switch state keys. A light system theme
    // turns off the accent color on Start and the taskbar (ColorPrevalence). The user's value
    // is saved when the system leaves dark mode and restored when it returns. Without a known
    // previous theme or a state key, light still clears the value but nothing is saved or restored.
    LSTATUS UpdateColorPrevalence(HKEY personalizationKey, HKEY stateKey, std::optional<bool> wasLight, bool isLight, bool& changed);

    // The caller owns an open Night Light key. Failure leaves enabled unchanged.
    LSTATUS ReadNightLightState(HKEY key, bool& enabled);
}

void SetSystemTheme(bool isLight);
void SetAppsTheme(bool isLight);
bool GetCurrentSystemTheme();
bool GetCurrentAppsTheme();
