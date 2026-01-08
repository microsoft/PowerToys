#pragma once
#include <string>

void SetSystemTheme(bool dark);
void SetAppsTheme(bool dark);
void SetThemeFile(const std::wstring& themeFilePath);
bool GetCurrentSystemTheme();
bool GetCurrentAppsTheme();
bool IsNightLightEnabled();