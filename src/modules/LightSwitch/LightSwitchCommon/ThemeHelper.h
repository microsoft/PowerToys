#pragma once
#include <string>

void SetSystemTheme(bool dark);
void SetAppsTheme(bool dark);
void SetThemeFile(const std::wstring& themeFilePath);
bool GetCurrentSystemTheme();
bool GetCurrentAppsTheme();
bool IsNightLightEnabled();

// Functions to manually control Settings app suppression monitoring
// Call StartSettingsMonitor() before any operations that might trigger the Settings app,
// then call StopSettingsMonitor() when done
void StartSettingsMonitor();
void StopSettingsMonitor();
