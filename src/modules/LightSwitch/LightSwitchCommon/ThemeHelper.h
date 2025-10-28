#pragma once
#include <string>
void SetSystemTheme(bool dark) noexcept;
void SetAppsTheme(bool dark) noexcept;
bool GetCurrentSystemTheme() noexcept;
bool GetCurrentAppsTheme() noexcept;
// Returned 0 indicates success; otherwise, the reason is returned, see definition
int SetDesktopWallpaper(std::wstring const& wallpaperPath, int style, bool virtualDesktop) noexcept;