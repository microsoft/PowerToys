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

RECT ForceRectInBounds( RECT rect, const RECT& bounds );
UINT GetDpiForWindowHelper( HWND window );
RECT GetMonitorRectFromCursor();
RECT RectFromPointsMinSize( POINT a, POINT b, LONG minSize );
int ScaleForDpi( int value, UINT dpi );
POINT ScalePointInRects( POINT point, const RECT& source, const RECT& target );
