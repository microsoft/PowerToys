#pragma once
#include <string>
#include <Winerror.h>
void SetSystemTheme(bool dark) noexcept;
void SetAppsTheme(bool dark) noexcept;
bool GetCurrentSystemTheme() noexcept;
bool GetCurrentAppsTheme() noexcept;
bool IsNightLightEnabled() noexcept;
HRESULT SetDesktopWallpaper(std::wstring const& wallpaperPath, int style) noexcept;