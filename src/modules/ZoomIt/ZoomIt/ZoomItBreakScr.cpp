//============================================================================
//
// ZoomItBreakScr.cpp
//
// ZoomIt break timer screensaver (.scr).  When launched by Winlogon on the
// Screen-saver desktop with password protection, the user must authenticate
// to dismiss it.  The break timer countdown and rendering are provided by
// the shared BreakTimer module.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================

#include <windows.h>
#include <windowsx.h>
#include <tchar.h>
#include <stdio.h>
#include <stdlib.h>
#include <scrnsave.h>
#define GDIPVER 0x0110
#include <gdiplus.h>

#include "BreakTimer.h"

static void DbgPrint( LPCTSTR fmt, ... )
{
    TCHAR buf[512];
    va_list ap;
    va_start( ap, fmt );
    _vsntprintf( buf, _countof(buf), fmt, ap );
    va_end( ap );
    buf[_countof(buf)-1] = 0;
    OutputDebugString( buf );
}

#pragma comment(lib, "scrnsavw.lib")
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "Msimg32.lib")
#pragma comment(lib, "Winmm.lib")

//----------------------------------------------------------------------------
// Globals
//----------------------------------------------------------------------------
static BreakTimerSettings   g_Settings;
static BreakTimerState      g_State;
static ULONG_PTR            g_GdiplusToken;
static TCHAR                g_ScreenshotPath[MAX_PATH] = { 0 };
static int                  g_LastSavedTimeout = 0;  // For state persistence

//----------------------------------------------------------------------------
// Load settings from the binary config file written by ZoomIt,
// falling back to hard-coded defaults if the file is missing.
//----------------------------------------------------------------------------
static void LoadSettings( void )
{
    BreakScrConfig config;
    if( BreakScrConfig_Read( &config ) )
    {
        g_Settings = config.settings;
        g_State.timeoutSeconds = config.timeoutSeconds;
        _tcscpy( g_ScreenshotPath, config.screenshotPath );
        DbgPrint( L"[BreakScr] Config loaded: timeout=%d, bgFile=%d, showDesktop=%d, screenshot=%s\n",
                  config.timeoutSeconds, config.settings.showBackgroundFile,
                  config.settings.showDesktop, config.screenshotPath );
        return;
    }

    DbgPrint( L"[BreakScr] Config file not found, using fallback defaults\n" );
    // Fallback defaults (for testing the .scr directly).
    memset( &g_Settings, 0, sizeof( g_Settings ) );
    g_Settings.penColor         = RGB( 255, 0, 0 );
    g_Settings.timerPosition    = 4;
    g_Settings.opacity          = 100;
    g_Settings.showExpiredTime  = 1;
    g_Settings.smoothImage      = TRUE;
    g_Settings.backgroundStretch = FALSE;
    g_Settings.showDesktop      = TRUE;
    g_Settings.showBackgroundFile = FALSE;
    g_State.timeoutSeconds      = 600;

    NONCLIENTMETRICS ncm = { sizeof( ncm ) };
    SystemParametersInfo( SPI_GETNONCLIENTMETRICS, sizeof( ncm ), &ncm, 0 );
    g_Settings.logFont = ncm.lfMessageFont;
}

//----------------------------------------------------------------------------
//
// ScreenSaverProc
//
// Main window procedure for the screensaver, called by Scrnsavw.lib.
//
//----------------------------------------------------------------------------
LRESULT WINAPI ScreenSaverProc( HWND hWnd, UINT msg, WPARAM wParam, LPARAM lParam )
{
    switch( msg )
    {
    case WM_CREATE:
    {
        DbgPrint( L"[BreakScr] WM_CREATE: hwnd=%p\n", hWnd );

        // Initialize GDI+.
        Gdiplus::GdiplusStartupInput startupIn;
        Gdiplus::GdiplusStartup( &g_GdiplusToken, &startupIn, NULL );

        LoadSettings();

        // Check if a previous screensaver instance already ran (resumed == TRUE).
        // On first launch, ZoomIt sets resumed = FALSE, so we skip the deduction.
        BreakScrConfig resumeConfig;
        if( BreakScrConfig_Read( &resumeConfig ) && resumeConfig.resumed )
        {
            // Subtract the screensaver idle timeout to compensate for
            // the time the screensaver wasn't running on the lock screen.
            UINT scrTimeout = 0;
            SystemParametersInfo( SPI_GETSCREENSAVETIMEOUT, 0, &scrTimeout, 0 );
            g_State.timeoutSeconds -= static_cast<int>( scrTimeout );
            if( g_State.timeoutSeconds < 0 && !g_Settings.showExpiredTime )
                g_State.timeoutSeconds = 0;
            DbgPrint( L"[BreakScr] Resumption: subtracted %u sec idle, timeout=%d\n",
                     scrTimeout, g_State.timeoutSeconds );
        }

        // Mark as resumed so subsequent screensaver launches know to deduct idle time.
        {
            BreakScrConfig markConfig;
            if( BreakScrConfig_Read( &markConfig ) )
            {
                markConfig.resumed = TRUE;
                BreakScrConfig_Write( &markConfig );
            }
        }

        // Load pre-captured screenshot if provided.
        HBITMAP hBgBmp = NULL;
        HDC     hBgDC  = NULL;
        if( g_ScreenshotPath[0] )
        {
            hBgBmp = BreakTimer_LoadImageFile( g_ScreenshotPath );
            DbgPrint( L"[BreakScr] LoadImageFile(%s) => %p\n", g_ScreenshotPath, hBgBmp );
            if( hBgBmp )
            {
                HDC hdcScreen = CreateDC( L"DISPLAY", NULL, NULL, NULL );
                hBgDC = CreateCompatibleDC( hdcScreen );
                SelectObject( hBgDC, hBgBmp );
                DeleteDC( hdcScreen );
            }
        }

        int timeout = g_State.timeoutSeconds;
        memset( &g_State, 0, sizeof( g_State ) );

        DbgPrint( L"[BreakScr] Calling BreakTimer_Init, timeout=%d\n", timeout );
        if( !BreakTimer_Init( hWnd, &g_State, &g_Settings, timeout, hBgBmp, hBgDC ) )
        {
            DbgPrint( L"[BreakScr] BreakTimer_Init FAILED\n" );
            PostMessage( hWnd, WM_CLOSE, 0, 0 );
            return 0;
        }
        DbgPrint( L"[BreakScr] BreakTimer_Init OK, active=%d\n", g_State.active );

        // Prevent the monitor from blanking due to power management.
        SetThreadExecutionState( ES_CONTINUOUS | ES_DISPLAY_REQUIRED | ES_SYSTEM_REQUIRED );

        // Kick off the first tick and start the 1-second timer.
        SendMessage( hWnd, WM_TIMER, 1, 0 );
        SetTimer( hWnd, 1, 1000, NULL );
        return 0;
    }

    case WM_TIMER:
        if( wParam == 1 )
        {
            BreakTimer_Tick( hWnd, &g_State, &g_Settings );

            // Periodically save state (every 5 seconds) for resumption after
            // credential provider timeout. This allows the screensaver to continue
            // from where it left off if a student triggers the login screen but
            // doesn't authenticate.
            if( g_State.timeoutSeconds != g_LastSavedTimeout &&
                g_State.timeoutSeconds % 5 == 0 )
            {
                BreakScrConfig config;
                if( BreakScrConfig_Read( &config ) )
                {
                    config.timeoutSeconds = g_State.timeoutSeconds;
                    if( BreakScrConfig_Write( &config ) )
                    {
                        g_LastSavedTimeout = g_State.timeoutSeconds;
                        DbgPrint( L"[BreakScr] Saved state: %d seconds remaining\n",
                                 g_State.timeoutSeconds );
                    }
                }
            }
        }
        return 0;

    case WM_PAINT:
    {
        PAINTSTRUCT ps;
        HDC hdc = BeginPaint( hWnd, &ps );
        if( g_State.active )
        {
            BreakTimer_Paint( hdc, &g_State, &g_Settings );
        }
        EndPaint( hWnd, &ps );
        return 0;
    }

    case WM_DESTROY:
        DbgPrint( L"[BreakScr] WM_DESTROY\n" );
        SetThreadExecutionState( ES_CONTINUOUS );  // Restore default power behavior
        KillTimer( hWnd, 1 );
        BreakTimer_Cleanup( &g_State, TRUE );
        Gdiplus::GdiplusShutdown( g_GdiplusToken );
        return 0;

    //------------------------------------------------------------------
    // Prevent DefScreenSaverProc from auto-closing on user input.
    // The screensaver must stay up until the break timer expires or
    // the user authenticates via Ctrl+Alt+Del.  DefScreenSaverProc
    // would close the window on mouse movement, clicks, keyboard,
    // or deactivation.
    //------------------------------------------------------------------
    case WM_MOUSEMOVE:
    case WM_LBUTTONDOWN:
    case WM_RBUTTONDOWN:
    case WM_MBUTTONDOWN:
    case WM_KEYDOWN:
    case WM_KEYUP:
    case WM_SYSKEYDOWN:
        return 0;

    case WM_ACTIVATE:
    case WM_ACTIVATEAPP:
        // Don't close on deactivation (e.g. LockWorkStation switches desktop).
        return 0;

    case WM_SYSCOMMAND:
        // Block SC_CLOSE from Alt+F4 etc.
        if( ( wParam & 0xFFF0 ) == SC_CLOSE )
            return 0;
        break;
    }

    return DefScreenSaverProc( hWnd, msg, wParam, lParam );
}

//----------------------------------------------------------------------------
//
// ScreenSaverConfigureDialog
//
// No configuration — ZoomIt handles all settings.
//
//----------------------------------------------------------------------------
BOOL WINAPI ScreenSaverConfigureDialog( HWND hDlg, UINT msg, WPARAM wParam, LPARAM lParam )
{
    return FALSE;
}

//----------------------------------------------------------------------------
//
// RegisterDialogClasses
//
// Nothing to register.
//
//----------------------------------------------------------------------------
BOOL WINAPI RegisterDialogClasses( HANDLE hInst )
{
    return TRUE;
}
