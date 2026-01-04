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
