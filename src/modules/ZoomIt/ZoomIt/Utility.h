//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Utility functions
//
//==============================================================================
#pragma once

#include "pch.h"
#include <uxtheme.h>

// DPI baseline for scaling calculations (dialog units are designed at 96 DPI)
constexpr UINT DPI_BASELINE = USER_DEFAULT_SCREEN_DPI;

RECT ForceRectInBounds( RECT rect, const RECT& bounds );
UINT GetDpiForWindowHelper( HWND window );
RECT GetMonitorRectFromCursor();
RECT RectFromPointsMinSize( POINT a, POINT b, LONG minSize );
int ScaleForDpi( int value, UINT dpi );
POINT ScalePointInRects( POINT point, const RECT& source, const RECT& target );

// Dialog DPI scaling functions
void ScaleDialogForDpi( HWND hDlg, UINT newDpi, UINT oldDpi = DPI_BASELINE );
void HandleDialogDpiChange( HWND hDlg, WPARAM wParam, LPARAM lParam, UINT& currentDpi );

//----------------------------------------------------------------------------
// Dark Mode Support
//----------------------------------------------------------------------------

// Dark mode colors
namespace DarkMode
{
    // Background colors
    constexpr COLORREF BackgroundColor = RGB(32, 32, 32);
    constexpr COLORREF SurfaceColor = RGB(45, 45, 48);
    constexpr COLORREF ControlColor = RGB(51, 51, 55);

    // Text colors
    constexpr COLORREF TextColor = RGB(255, 255, 255);
    constexpr COLORREF DisabledTextColor = RGB(160, 160, 160);
    constexpr COLORREF LinkColor = RGB(86, 156, 214);

    // Border/accent colors
    constexpr COLORREF BorderColor = RGB(67, 67, 70);
    constexpr COLORREF AccentColor = RGB(0, 120, 215);
    constexpr COLORREF HoverColor = RGB(62, 62, 66);

    // Light mode colors for contrast
    constexpr COLORREF LightBackgroundColor = RGB(255, 255, 255);
    constexpr COLORREF LightTextColor = RGB(0, 0, 0);
}

// Check if system dark mode is enabled
bool IsDarkModeEnabled();

// Refresh dark mode state (call when WM_SETTINGCHANGE received)
void RefreshDarkModeState();

// Enable dark mode title bar for a window
void SetDarkModeForWindow(HWND hWnd, bool enable);

// Apply dark mode to a dialog and enable dark title bar
void ApplyDarkModeToDialog(HWND hDlg);

// Get the appropriate background brush for dark/light mode
HBRUSH GetDarkModeBrush();
HBRUSH GetDarkModeControlBrush();
HBRUSH GetDarkModeSurfaceBrush();

// Handle WM_CTLCOLOR* messages for dark mode
// Returns the brush to use, or nullptr if default handling should be used
HBRUSH HandleDarkModeCtlColor(HDC hdc, HWND hCtrl, UINT message);

// Apply dark mode theme to a popup menu
void ApplyDarkModeToMenu(HMENU hMenu);

// Force redraw of a window and all its children for theme change
void RefreshWindowTheme(HWND hWnd);

// Cleanup dark mode resources (call at app exit)
void CleanupDarkModeResources();

// Initialize dark mode support early in app startup (call before creating windows)
void InitializeDarkMode();

// Subclass procedure for hotkey controls - needs to be accessible from Utility.cpp
LRESULT CALLBACK HotkeyControlSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);

// Subclass procedure for checkbox controls - needs to be accessible from Utility.cpp
LRESULT CALLBACK CheckboxSubclassProc(HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam, UINT_PTR uIdSubclass, DWORD_PTR dwRefData);
