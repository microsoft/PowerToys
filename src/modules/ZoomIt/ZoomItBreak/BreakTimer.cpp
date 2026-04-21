//============================================================================
//
// BreakTimer.cpp
//
// Shared break timer rendering module used by both ZoomIt and the
// ZoomItBreak screensaver (.scr).
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================

// When built inside ZoomIt (with PCH), pch.h is included automatically.
// When built for the screensaver project, we include the headers we need.
#ifndef __ZOOMIT_SCREENSAVER__
#include "pch.h"
#endif

#include "BreakTimer.h"

#include <stdio.h>

#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "Msimg32.lib")
#pragma comment(lib, "Winmm.lib")

//----------------------------------------------------------------------------
//
// BreakTimer_UpdateMonitorInfo
//
// Determine monitor geometry for the given screen point.
//
//----------------------------------------------------------------------------
void BreakTimer_UpdateMonitorInfo( POINT point, MONITORINFO* monInfo )
{
    HMONITOR hMon = MonitorFromPoint( point, MONITOR_DEFAULTTONEAREST );
    if( hMon != nullptr )
    {
        monInfo->cbSize = sizeof *monInfo;
        GetMonitorInfo( hMon, monInfo );
    }
    else
    {
        *monInfo = {};
        HDC hdcScreen = CreateDC( L"DISPLAY", nullptr, nullptr, nullptr );
        if( hdcScreen != nullptr )
        {
            monInfo->rcMonitor.right = GetDeviceCaps( hdcScreen, HORZRES );
            monInfo->rcMonitor.bottom = GetDeviceCaps( hdcScreen, VERTRES );
            DeleteDC( hdcScreen );
        }
    }
}

//----------------------------------------------------------------------------
//
// BreakTimer_LoadImageFile
//
// Use GDI+ to load an image file and return an HBITMAP.
//
//----------------------------------------------------------------------------
HBITMAP BreakTimer_LoadImageFile( PTCHAR Filename )
{
    HBITMAP hBmp;
    Gdiplus::Bitmap* bitmap = Gdiplus::Bitmap::FromFile( Filename );
    if( bitmap == nullptr || bitmap->GetHBITMAP( NULL, &hBmp ) != Gdiplus::Ok )
    {
        delete bitmap;
        return NULL;
    }
    delete bitmap;
    return hBmp;
}

//----------------------------------------------------------------------------
//
// BreakTimer_CreateFadedDesktopBackground
//
// Creates a snapshot of the desktop that is faded and alpha-blended
// with black.
//
//----------------------------------------------------------------------------
HBITMAP BreakTimer_CreateFadedDesktopBackground( HDC hdc, LPRECT rcScreen, LPRECT rcCrop )
{
    int     width    = rcScreen->right - rcScreen->left;
    int     height   = rcScreen->bottom - rcScreen->top;
    HDC     hdcScreen = hdc;
    HDC     hdcMem   = CreateCompatibleDC( hdcScreen );
    HBITMAP hBitmap  = CreateCompatibleBitmap( hdcScreen, width, height );
    HBITMAP hOld     = static_cast<HBITMAP>( SelectObject( hdcMem, hBitmap ) );
    HBRUSH  hBrush   = CreateSolidBrush( RGB( 0, 0, 0 ) );

    // Start with black background.
    FillRect( hdcMem, rcScreen, hBrush );
    if( rcCrop != NULL && rcCrop->left != -1 )
    {
        // Copy screen contents that are not cropped.
        BitBlt( hdcMem, rcCrop->left, rcCrop->top,
                rcCrop->right - rcCrop->left,
                rcCrop->bottom - rcCrop->top,
                hdcScreen, rcCrop->left, rcCrop->top, SRCCOPY );
    }

    // Blend screen contents into the black background.
    BLENDFUNCTION blend = { 0 };
    blend.BlendOp             = AC_SRC_OVER;
    blend.BlendFlags          = 0;
    blend.SourceConstantAlpha = 0x4F;
    blend.AlphaFormat         = 0;
    AlphaBlend( hdcMem, 0, 0, width, height,
                hdcScreen, rcScreen->left, rcScreen->top,
                width, height, blend );

    SelectObject( hdcMem, hOld );
    DeleteDC( hdcMem );
    DeleteObject( hBrush );

    return hBitmap;
}

//----------------------------------------------------------------------------
//
// BreakTimer_Init
//
// Create fonts, backing bitmap, and optionally load background.
// Returns TRUE on success.
//
//----------------------------------------------------------------------------
BOOLEAN BreakTimer_Init(
    HWND                        hWnd,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings,
    int                         timeoutSeconds,
    HBITMAP                     hExistingBackground,
    HDC                         hExistingBackgroundDC )
{
    state->active = TRUE;
    state->timeoutSeconds = timeoutSeconds;

    // Get screen DC.
    state->hdcScreen = CreateDC( L"DISPLAY", static_cast<PTCHAR>( NULL ),
                                  static_cast<PTCHAR>( NULL ),
                                  static_cast<CONST DEVMODE*>( NULL ) );
    if( !state->hdcScreen )
        return FALSE;

    // Determine monitor.
    POINT cursorPos;
    GetCursorPos( &cursorPos );
    BreakTimer_UpdateMonitorInfo( cursorPos, &state->monInfo );
    state->width  = state->monInfo.rcMonitor.right - state->monInfo.rcMonitor.left;
    state->height = state->monInfo.rcMonitor.bottom - state->monInfo.rcMonitor.top;

    // Manage background bitmap.
    if( hExistingBackground )
    {
        // Caller supplied a pre-captured background (e.g. from command line).
        state->hBackgroundBmp    = hExistingBackground;
        state->hDcBackgroundFile = hExistingBackgroundDC;
    }
    else if( settings->showBackgroundFile && !settings->showDesktop )
    {
        // Load image file.
        state->hBackgroundBmp = BreakTimer_LoadImageFile(
            const_cast<PTCHAR>( settings->backgroundFile ) );
        if( !state->hBackgroundBmp )
            return FALSE;
        state->hDcBackgroundFile = CreateCompatibleDC( state->hdcScreen );
        SelectObject( state->hDcBackgroundFile, state->hBackgroundBmp );
    }
    else if( settings->showBackgroundFile && settings->showDesktop )
    {
        // Faded desktop screenshot.
        HDC hDcDesktop = GetDC( NULL );
        state->hBackgroundBmp = BreakTimer_CreateFadedDesktopBackground(
            hDcDesktop, &state->monInfo.rcMonitor, NULL );
        ReleaseDC( NULL, hDcDesktop );
        state->hDcBackgroundFile = CreateCompatibleDC( state->hdcScreen );
        SelectObject( state->hDcBackgroundFile, state->hBackgroundBmp );
    }
    else
    {
        state->hBackgroundBmp    = NULL;
        state->hDcBackgroundFile = NULL;
    }

    // Create fonts.
    LOGFONT lf = settings->logFont;
    lf.lfHeight = state->height / 5;
    state->hTimerFont = CreateFontIndirect( &lf );
    lf.lfHeight = state->height / 8;
    state->hNegativeTimerFont = CreateFontIndirect( &lf );

    // Create backing bitmap for double buffering.
    state->hdcScreenCompat = CreateCompatibleDC( state->hdcScreen );
    state->bmp.bmBitsPixel = static_cast<BYTE>( GetDeviceCaps( state->hdcScreen, BITSPIXEL ) );
    state->bmp.bmPlanes    = static_cast<BYTE>( GetDeviceCaps( state->hdcScreen, PLANES ) );
    state->bmp.bmWidth     = state->width;
    state->bmp.bmHeight    = state->height;
    state->bmp.bmWidthBytes = ( ( state->bmp.bmWidth + 15 ) & ~15 ) / 8;
    state->hbmpCompat = CreateBitmap( state->bmp.bmWidth, state->bmp.bmHeight,
        state->bmp.bmPlanes, state->bmp.bmBitsPixel, static_cast<CONST VOID*>( NULL ) );
    SelectObject( state->hdcScreenCompat, state->hbmpCompat );

    SetTextColor( state->hdcScreenCompat, settings->penColor );
    SetBkMode( state->hdcScreenCompat, TRANSPARENT );
    SelectObject( state->hdcScreenCompat, state->hTimerFont );

    return TRUE;
}

//----------------------------------------------------------------------------
//
// BreakTimer_Tick
//
// Decrement counter, invalidate window, play sound at zero.
//
//----------------------------------------------------------------------------
void BreakTimer_Tick(
    HWND                        hWnd,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings )
{
    state->timeoutSeconds -= 1;
    InvalidateRect( hWnd, NULL, FALSE );

    if( state->timeoutSeconds == 0 && settings->playSound )
    {
        PlaySound( settings->soundFile, NULL, SND_FILENAME | SND_ASYNC );
    }
}

//----------------------------------------------------------------------------
//
// BreakTimer_Paint
//
// Render the break timer into the back buffer and blit to the paint DC.
//
//----------------------------------------------------------------------------
void BreakTimer_Paint(
    HDC                         hdc,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings )
{
    RECT rc, rc1;
    TCHAR timerText[16];
    TCHAR negativeTimerText[16];

    // Fill background (white by default, black if backgroundColor == 1).
    rc.top = rc.left = 0;
    rc.bottom = state->height;
    rc.right  = state->width;
    if( settings->backgroundColor )
    {
        HBRUSH hBrush = CreateSolidBrush( RGB( 0, 0, 0 ) );
        FillRect( state->hdcScreenCompat, &rc, hBrush );
        DeleteObject( hBrush );
    }
    else
    {
        FillRect( state->hdcScreenCompat, &rc, GetSysColorBrush( COLOR_WINDOW ) );
    }

    // Draw background bitmap if present.
    if( state->hBackgroundBmp )
    {
        BITMAP local_bmp;
        GetObject( state->hBackgroundBmp, sizeof( local_bmp ), &local_bmp );
        SetStretchBltMode( state->hdcScreenCompat,
                           settings->smoothImage ? HALFTONE : COLORONCOLOR );
        if( settings->backgroundStretch )
        {
            StretchBlt( state->hdcScreenCompat, 0, 0, state->width, state->height,
                state->hDcBackgroundFile, 0, 0,
                local_bmp.bmWidth, local_bmp.bmHeight, SRCCOPY | CAPTUREBLT );
        }
        else
        {
            BitBlt( state->hdcScreenCompat,
                state->width / 2 - local_bmp.bmWidth / 2,
                state->height / 2 - local_bmp.bmHeight / 2,
                local_bmp.bmWidth, local_bmp.bmHeight,
                state->hDcBackgroundFile, 0, 0, SRCCOPY | CAPTUREBLT );
        }
    }

    // Format timer text.
    if( state->timeoutSeconds > 0 )
    {
        _stprintf( timerText, L"% 2d:%02d",
                   state->timeoutSeconds / 60, state->timeoutSeconds % 60 );
    }
    else
    {
        _tcscpy( timerText, L"0:00" );
    }

    // Measure timer text.
    rc.left = rc.top = 0;
    DrawText( state->hdcScreenCompat, timerText, -1, &rc,
              DT_NOCLIP | DT_LEFT | DT_NOPREFIX | DT_CALCRECT );

    // Measure expired text if needed.
    rc1.left = rc1.right = rc1.bottom = rc1.top = 0;
    if( settings->showExpiredTime && state->timeoutSeconds < 0 )
    {
        _stprintf( negativeTimerText, L"(-% 2d:%02d)",
                   -state->timeoutSeconds / 60, -state->timeoutSeconds % 60 );
        HFONT prevFont = static_cast<HFONT>(
            SelectObject( state->hdcScreenCompat, state->hNegativeTimerFont ) );
        DrawText( state->hdcScreenCompat, negativeTimerText, -1, &rc1,
                  DT_NOCLIP | DT_LEFT | DT_NOPREFIX | DT_CALCRECT );
        SelectObject( state->hdcScreenCompat, prevFont );
    }

    // Position vertically.
    switch( settings->timerPosition )
    {
    case 0: case 1: case 2:
        rc.top = 50;
        break;
    case 3: case 4: case 5:
        rc.top = ( state->height - ( rc.bottom - rc.top ) ) / 2;
        break;
    case 6: case 7: case 8:
        rc.top = state->height - rc.bottom - 50 - rc1.bottom;
        break;
    }

    // Position horizontally.
    switch( settings->timerPosition )
    {
    case 0: case 3: case 6:
        rc.left = 50;
        break;
    case 1: case 4: case 7:
        rc.left = ( state->width - ( rc.right - rc.left ) ) / 2;
        break;
    case 2: case 5: case 8:
        rc.left = state->width - rc.right - 50;
        break;
    }
    rc.bottom += rc.top;
    rc.right  += rc.left;

    // Draw timer text.
    DrawText( state->hdcScreenCompat, timerText, -1, &rc,
              DT_NOCLIP | DT_LEFT | DT_NOPREFIX );

    // Draw expired text below the timer.
    if( settings->showExpiredTime && state->timeoutSeconds < 0 )
    {
        rc1.top  = rc.bottom + 10;
        rc1.left = rc.left + ( ( rc.right - rc.left ) - ( rc1.right - rc1.left ) ) / 2;
        HFONT prevFont = static_cast<HFONT>(
            SelectObject( state->hdcScreenCompat, state->hNegativeTimerFont ) );
        DrawText( state->hdcScreenCompat, negativeTimerText, -1, &rc1,
                  DT_NOCLIP | DT_LEFT | DT_NOPREFIX );
        SelectObject( state->hdcScreenCompat, prevFont );
    }

    // Copy to screen.
    BitBlt( hdc, 0, 0, state->width, state->height,
            state->hdcScreenCompat, 0, 0, SRCCOPY | CAPTUREBLT );
}

//----------------------------------------------------------------------------
//
// BreakTimer_Cleanup
//
// Free the GDI resources used by the break timer.
//
//----------------------------------------------------------------------------
void BreakTimer_Cleanup(
    BreakTimerState*    state,
    BOOLEAN             freeBackground )
{
    if( freeBackground && state->hBackgroundBmp )
    {
        DeleteObject( state->hBackgroundBmp );
        DeleteDC( state->hDcBackgroundFile );
        state->hBackgroundBmp    = NULL;
        state->hDcBackgroundFile = NULL;
    }

    if( state->hTimerFont )
    {
        DeleteObject( state->hTimerFont );
        state->hTimerFont = NULL;
    }
    if( state->hNegativeTimerFont )
    {
        DeleteObject( state->hNegativeTimerFont );
        state->hNegativeTimerFont = NULL;
    }
    if( state->hdcScreen )
    {
        DeleteDC( state->hdcScreen );
        state->hdcScreen = NULL;
    }
    if( state->hdcScreenCompat )
    {
        DeleteDC( state->hdcScreenCompat );
        state->hdcScreenCompat = NULL;
    }
    if( state->hbmpCompat )
    {
        DeleteObject( state->hbmpCompat );
        state->hbmpCompat = NULL;
    }

    state->active = FALSE;
}

//----------------------------------------------------------------------------
//
// BreakTimer_AdjustTime
//
// Round to the nearest minute boundary and adjust by deltaMinutes.
// Resets the 1-second timer on the window.
//
//----------------------------------------------------------------------------
void BreakTimer_AdjustTime(
    HWND                hWnd,
    BreakTimerState*    state,
    int                 deltaMinutes )
{
    int breakTimeout = state->timeoutSeconds;

    if( deltaMinutes > 0 )
    {
        if( breakTimeout < 0 ) breakTimeout = 0;
        if( breakTimeout % 60 )
        {
            breakTimeout += ( 60 - breakTimeout % 60 );
            deltaMinutes--;
        }
        breakTimeout += deltaMinutes * 60;
    }
    else
    {
        int absDelta = -deltaMinutes;
        if( breakTimeout % 60 )
        {
            breakTimeout -= breakTimeout % 60;
            absDelta--;
        }
        breakTimeout -= absDelta * 60;
    }

    if( breakTimeout < 0 ) breakTimeout = 0;
    state->timeoutSeconds = breakTimeout;

    KillTimer( hWnd, 0 );
    SetTimer( hWnd, 0, 1000, NULL );
    InvalidateRect( hWnd, NULL, TRUE );
}

//----------------------------------------------------------------------------
//
// BreakScrConfig_GetPath
//
// Build the full path to the config file in %TEMP%.
//
//----------------------------------------------------------------------------
static void BreakScrConfig_GetPath( TCHAR* path, size_t cch )
{
    GetTempPath( static_cast<DWORD>( cch ), path );
    _tcscat( path, BREAKSCR_CONFIG_FILE );
}

//----------------------------------------------------------------------------
//
// BreakScrConfig_Write
//
//----------------------------------------------------------------------------
BOOLEAN BreakScrConfig_Write( const BreakScrConfig* config )
{
    TCHAR path[MAX_PATH];
    BreakScrConfig_GetPath( path, MAX_PATH );

    HANDLE hFile = CreateFile( path, GENERIC_WRITE, 0, NULL,
                               CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL );
    if( hFile == INVALID_HANDLE_VALUE )
        return FALSE;

    DWORD written;
    BOOL ok = WriteFile( hFile, config, sizeof( *config ), &written, NULL );
    CloseHandle( hFile );
    return ok && written == sizeof( *config );
}

//----------------------------------------------------------------------------
//
// BreakScrConfig_Read
//
//----------------------------------------------------------------------------
BOOLEAN BreakScrConfig_Read( BreakScrConfig* config )
{
    TCHAR path[MAX_PATH];
    BreakScrConfig_GetPath( path, MAX_PATH );

    HANDLE hFile = CreateFile( path, GENERIC_READ, FILE_SHARE_READ, NULL,
                               OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, NULL );
    if( hFile == INVALID_HANDLE_VALUE )
        return FALSE;

    DWORD bytesRead;
    BOOL ok = ReadFile( hFile, config, sizeof( *config ), &bytesRead, NULL );
    CloseHandle( hFile );

    if( !ok || bytesRead != sizeof( *config ) )
        return FALSE;
    if( config->magic != BREAKSCR_CONFIG_MAGIC )
        return FALSE;

    return TRUE;
}
