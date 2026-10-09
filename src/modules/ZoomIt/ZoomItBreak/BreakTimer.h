//============================================================================
//
// BreakTimer.h
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
#pragma once

#include <windows.h>
#include <tchar.h>
#define GDIPVER 0x0110
#include <gdiplus.h>

//----------------------------------------------------------------------------
// BreakTimerSettings — read-only configuration, populated from globals
// or from command-line arguments in the screensaver.
//----------------------------------------------------------------------------
struct BreakTimerSettings
{
    DWORD       penColor;
    DWORD       backgroundColor;        // 0 = white, 1 = black
    DWORD       timerPosition;          // 0–8 (3×3 grid)
    DWORD       opacity;                // 0–100
    DWORD       showExpiredTime;        // 0 or 1
    BOOLEAN     smoothImage;
    BOOLEAN     backgroundStretch;
    BOOLEAN     playSound;
    TCHAR       soundFile[MAX_PATH];
    BOOLEAN     showDesktop;
    BOOLEAN     showBackgroundFile;
    TCHAR       backgroundFile[MAX_PATH];
    LOGFONT     logFont;
};

//----------------------------------------------------------------------------
// BreakTimerState — runtime state for an active break timer.
//----------------------------------------------------------------------------
struct BreakTimerState
{
    BOOLEAN     active;
    int         timeoutSeconds;         // counts down; goes negative if expired
    HFONT       hTimerFont;
    HFONT       hNegativeTimerFont;
    HBITMAP     hBackgroundBmp;
    HDC         hDcBackgroundFile;
    HDC         hdcScreen;
    HDC         hdcScreenCompat;
    HBITMAP     hbmpCompat;
    BITMAP      bmp;
    int         width;
    int         height;
    MONITORINFO monInfo;
};

//----------------------------------------------------------------------------
// Shared utility functions
//----------------------------------------------------------------------------

// Determine monitor geometry for the given screen point.
void BreakTimer_UpdateMonitorInfo( POINT point, MONITORINFO* monInfo );

// Load an image file via GDI+; returns an HBITMAP or NULL on failure.
HBITMAP BreakTimer_LoadImageFile( PTCHAR Filename );

// Capture a faded (alpha-blended with black) screenshot of the desktop.
HBITMAP BreakTimer_CreateFadedDesktopBackground( HDC hdc, LPRECT rcScreen, LPRECT rcCrop );

//----------------------------------------------------------------------------
// Break timer lifecycle
//----------------------------------------------------------------------------

// Create fonts, backing bitmap, and load background.
// The caller is responsible for creating/showing the window itself.
// |timeoutSeconds| is already in seconds (e.g. g_BreakTimeout * 60 + 1).
BOOLEAN BreakTimer_Init(
    HWND                        hWnd,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings,
    int                         timeoutSeconds,
    HBITMAP                     hExistingBackground,    // optional pre-captured background
    HDC                         hExistingBackgroundDC   // optional DC for above
);

// Called every second; decrements the counter and invalidates the window.
void BreakTimer_Tick(
    HWND                        hWnd,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings
);

// Render the timer into hdcScreenCompat then BitBlt to hdc (from BeginPaint).
void BreakTimer_Paint(
    HDC                         hdc,
    BreakTimerState*            state,
    const BreakTimerSettings*   settings
);

// Free fonts, DCs, bitmaps.  If |freeBackground| is false the background
// bitmap/DC are left for the caller to manage (e.g. shallow destroy).
void BreakTimer_Cleanup(
    BreakTimerState*            state,
    BOOLEAN                     freeBackground
);

// Adjust the remaining time by |deltaMinutes| (positive = add time).
// Resets the 1-second timer on hWnd.
void BreakTimer_AdjustTime(
    HWND                        hWnd,
    BreakTimerState*            state,
    int                         deltaMinutes
);

//----------------------------------------------------------------------------
// BreakScrConfig — binary blob written to a temp file by ZoomIt and
// read by the screensaver on startup.  This avoids command-line arg
// issues since Windows launches screensavers with only /s.
//----------------------------------------------------------------------------
#define BREAKSCR_CONFIG_MAGIC   0x5A4D4253  // 'ZMBS'
#define BREAKSCR_CONFIG_FILE    L"ZoomItBreakConfig.dat"

struct BreakScrConfig
{
    DWORD               magic;              // must be BREAKSCR_CONFIG_MAGIC
    int                 timeoutSeconds;
    BOOL                resumed;            // set TRUE by screensaver on first launch
    BreakTimerSettings  settings;
    TCHAR               screenshotPath[MAX_PATH];
};

// Write config to %TEMP%\BREAKSCR_CONFIG_FILE.
BOOLEAN BreakScrConfig_Write( const BreakScrConfig* config );

// Read config from %TEMP%\BREAKSCR_CONFIG_FILE.
BOOLEAN BreakScrConfig_Read( BreakScrConfig* config );
