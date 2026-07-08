#pragma once

#define WIN32_LEAN_AND_MEAN
#include <Windows.h>

#include <string>

#include "theme_helpers.h"

HRESULT GetIconIndexFromPath(_In_ PCWSTR path, _Out_ int* index);
HBITMAP CreateBitmapFromIcon(_In_ HICON hIcon, _In_opt_ UINT width = 0, _In_opt_ UINT height = 0);

// Loads a tray icon handle. When themeAdaptive is true, loads whiteIconPath for dark shell
// and darkIconPath for light shell. Falls back to the embedded resource icon on failure.
HICON LoadThemeAdaptiveTrayIcon(
    bool themeAdaptive,
    _In_ PCWSTR whiteIconPath,
    _In_ PCWSTR darkIconPath,
    _In_ HINSTANCE fallbackInstance,
    _In_ LPCWSTR fallbackResourceName);