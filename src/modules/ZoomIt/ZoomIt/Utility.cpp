//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Utility functions
//
//==============================================================================
#include "pch.h"
#include "Utility.h"

//----------------------------------------------------------------------------
//
// ForceRectInBounds
//
//----------------------------------------------------------------------------
RECT ForceRectInBounds( RECT rect, const RECT& bounds )
{
    if( rect.left < bounds.left )
    {
        rect.right += bounds.left - rect.left;
        rect.left = bounds.left;
    }
    if( rect.top < bounds.top )
    {
        rect.bottom += bounds.top - rect.top;
        rect.top = bounds.top;
    }
    if( rect.right > bounds.right )
    {
        rect.left -= rect.right - bounds.right;
        rect.right = bounds.right;
    }
    if( rect.bottom > bounds.bottom )
    {
        rect.top -= rect.bottom - bounds.bottom;
        rect.bottom = bounds.bottom;
    }
    return rect;
}

//----------------------------------------------------------------------------
//
// GetDpiForWindow
//
//----------------------------------------------------------------------------
UINT GetDpiForWindowHelper( HWND window )
{
    auto function = reinterpret_cast<UINT (WINAPI *)(HWND)>(GetProcAddress( GetModuleHandleW( L"user32.dll" ), "GetDpiForWindow" ));
    if( function )
    {
        return function( window );
    }

    wil::unique_hdc hdc{GetDC( nullptr )};
    return static_cast<UINT>(GetDeviceCaps( hdc.get(), LOGPIXELSX ));
}

//----------------------------------------------------------------------------
//
// GetMonitorRectFromCursor
//
//----------------------------------------------------------------------------
RECT GetMonitorRectFromCursor()
{
    POINT point;
    GetCursorPos( &point );
    MONITORINFO monitorInfo{};
    monitorInfo.cbSize = sizeof( monitorInfo );
    GetMonitorInfoW( MonitorFromPoint( point, MONITOR_DEFAULTTONEAREST ), &monitorInfo );
    return monitorInfo.rcMonitor;
}

//----------------------------------------------------------------------------
//
// RectFromPointsMinSize
//
//----------------------------------------------------------------------------
#ifdef _MSC_VER
    // avoid making RectFromPointsMinSize constexpr since that leads to link errors
    #pragma warning(push)
    #pragma warning(disable: 26497)
#endif

RECT RectFromPointsMinSize( POINT a, POINT b, LONG minSize )
{
    RECT rect;
    if( a.x <= b.x )
    {
        rect.left = a.x;
        rect.right = b.x + 1;
        if( (rect.right - rect.left) < minSize )
        {
            rect.right = rect.left + minSize;
        }
    }
    else
    {
        rect.left = b.x;
        rect.right = a.x + 1;
        if( (rect.right - rect.left) < minSize )
        {
            rect.left = rect.right - minSize;
        }
    }
    if( a.y <= b.y )
    {
        rect.top = a.y;
        rect.bottom = b.y + 1;
        if( (rect.bottom - rect.top) < minSize )
        {
            rect.bottom = rect.top + minSize;
        }
    }
    else
    {
        rect.top = b.y;
        rect.bottom = a.y + 1;
        if( (rect.bottom - rect.top) < minSize )
        {
            rect.top = rect.bottom - minSize;
        }
    }
    return rect;
}
#ifdef _MSC_VER
    #pragma warning(pop)
#endif
//----------------------------------------------------------------------------
//
// ScaleForDpi
//
//----------------------------------------------------------------------------
int ScaleForDpi( int value, UINT dpi )
{
    return MulDiv( value, static_cast<int>(dpi), USER_DEFAULT_SCREEN_DPI );
}

//----------------------------------------------------------------------------
//
// ScalePointInRects
//
//----------------------------------------------------------------------------
POINT ScalePointInRects( POINT point, const RECT& source, const RECT& target )
{
    const SIZE sourceSize{ source.right - source.left, source.bottom - source.top };
    const POINT sourceCenter{ source.left + sourceSize.cx / 2, source.top + sourceSize.cy / 2 };
    const SIZE targetSize{ target.right - target.left, target.bottom - target.top };
    const POINT targetCenter{ target.left + targetSize.cx / 2, target.top + targetSize.cy / 2 };

    return { targetCenter.x + MulDiv( point.x - sourceCenter.x, targetSize.cx, sourceSize.cx ),
             targetCenter.y + MulDiv( point.y - sourceCenter.y, targetSize.cy, sourceSize.cy ) };
}
