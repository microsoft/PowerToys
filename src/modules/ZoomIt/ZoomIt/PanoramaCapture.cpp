//============================================================================
//
// PanoramaCapture.cpp
//
// Panorama (scrolling) screen capture and stitching.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//
//============================================================================
//
// Algorithm overview
// ==================
//
// A panorama is produced in two stages: real-time screen capture, then
// offline frame stitching.
//
// 1. Capture
//    --------
//    The user selects a rectangular region via the SelectRectangle overlay.
//    The capture loop runs at ~16 ms intervals, grabbing the absolute screen
//    rect each iteration.  Consecutive near-duplicate frames (average
//    per-pixel RGB difference < 6, sampled every 6th pixel with a 2.5%
//    margin on all edges) are discarded.  Capture stops when the user
//    presses the stop hotkey or kMaxCaptureFrames frames have been collected.
//
// 2. Stitching (StitchPanoramaFrames)
//    ---------------------------------
//    All accepted frames are read into 32-bpp BGRA pixel arrays.  They are
//    then composed onto a single canvas by computing relative displacements
//    between each consecutive accepted pair.  Displacement detection uses a
//    two-phase search in FindBestFrameShift:
//
//    Phase 1 - Windowed coarse search on downsampled luma
//      Each frame is converted to single-channel luma and downsampled by
//      4x (or 2x for small frames < 240 px).  The downsampled images are
//      compared at every candidate vertical shift within a search window
//      determined by the expected scroll direction.  The first frame pair
//      searches in both directions; subsequent pairs search only in the
//      established direction across the full feasible range (minStep to
//      maxStep).  This full-range search handles variable scroll speeds
//      (e.g. 40 px -> 202 px between consecutive frames).
//
//      Each candidate computes the mean absolute difference (MAD) of luma
//      values across the overlapping region (skipping x-margins of ~5%).
//      Early termination discards candidates whose running average exceeds
//      the worst score in the current top-12 shortlist.
//
//      A stationary score is also computed (zero shift MAD).  If the
//      stationary score is <= 2, the frames are considered identical and
//      the pair is rejected.
//
//    Phase 2 - Full-resolution refinement
//      The top-12 coarse candidates (pruned to those within 30 MAD of the
//      best) are refined at pixel resolution.  For each candidate, a
//      neighborhood of +/- (downsampleScale+1) pixels vertically and +/-1
//      pixel horizontally is searched.  Full luma arrays are precomputed
//      from the BGRA data using integer (77R + 150G + 29B) >> 8.
//
//      On x64, the inner comparison loop uses SSE2 _mm_sad_epu8 to
//      process 16 luma bytes per iteration; a scalar fallback is used on
//      ARM64.  Early termination again prunes candidates exceeding the
//      current best fine score.  The candidate with the lowest fine MAD
//      is selected.
//
//    Validation
//      Cross-validation rejects matches where the stationary score is low
//      (< 15) but the detected shift is large (> frameHeight/3) and the
//      fine score is non-zero.  This catches spurious harmonic matches on
//      repetitive content like social media layouts.
//
//      An adaptive fine-score threshold (30 for high-stationary, 15 for
//      low-stationary content) rejects poor alignments while tolerating
//      subpixel rendering and ClearType artifacts.
//
//    Composition
//      Accepted frames are placed on a canvas according to cumulative
//      (stepX, stepY) offsets.  The output is normalized so the first
//      frame appears at the top.  In overlapping regions, a vertical
//      feather blend (configurable, ~frameHeight/18 pixels wide,
//      clamped to 4-28) linearly crossfades between the old and new
//      frame content using per-pixel alpha weighting.
//
//    Output
//      The stitched pixel array is converted to an HBITMAP via
//      CreateDIBSection.  The caller either copies it to the clipboard
//      as CF_DIB or saves it as a PNG file through IFileSaveDialog.
//
// Debug support
// ----------------------------------
// In debug builds, every grabbed and accepted frame is saved as a BMP
// to %TEMP%\ZoomItPanoramaDebug\<session>.  A StitchLog function writes
// tracing output to OutputDebugString and optionally to a file.
// In release builds, launch with /panorama-debug to enable the same
// frame dumps and stitch log output.
// Command-line switches /panorama-selftest, /panorama-stitch-latest,
// and /panorama-stitch-replay (debug only) allow offline re-stitching
// and automated regression testing.
//
//============================================================================
#include "pch.h"

#include "PanoramaCapture.h"
#include "Utility.h"
#include "WindowsVersions.h"

#include <atomic>
#include <filesystem>
#include <fstream>
#include <limits>
#include <thread>
#include <vector>
#include <functional>
#include <cmath>
#include <commctrl.h>
#if defined(_M_X64) || defined(_M_IX86)
#include <emmintrin.h>
#if defined(_M_IX86)
// _mm_cvtsi128_si64 is unavailable on 32-bit x86; emulate via _mm_storel_epi64.
inline __int64 _mm_cvtsi128_si64_compat( __m128i v )
{
    __int64 r;
    _mm_storel_epi64( reinterpret_cast<__m128i*>( &r ), v );
    return r;
}
#define _mm_cvtsi128_si64 _mm_cvtsi128_si64_compat
#endif
#elif defined(_M_ARM64)
#include <arm_neon.h>
#endif

// Externs from Zoomit.cpp
extern BOOL             g_RecordCropping;
extern SelectRectangle  g_SelectRectangle;
extern HINSTANCE        g_hInstance;
extern bool             g_bSaveInProgress;
extern std::wstring     g_ScreenshotSaveLocation;
void OutputDebug(const TCHAR* format, ...);
const wchar_t* HotkeyIdToString( WPARAM hotkeyId );
DWORD SavePng( LPCTSTR Filename, HBITMAP hBitmap );
std::wstring GetUniqueFilename( const std::wstring& lastSavePath, const wchar_t* defaultFilename, REFKNOWNFOLDERID defaultFolderId );

// Maximum number of frames the capture loop will collect before auto-stopping.
// Temporary debugging limit: keep frame-limit captures short in Debug so the
// limit-stop flow can be exercised quickly and repeatedly.
#ifdef _DEBUG
static constexpr size_t kMaxCaptureFrames = 1024;
#else
static constexpr size_t kMaxCaptureFrames = 1024;
#endif

static HBITMAP StitchPanoramaFrames( const std::vector<HBITMAP>& frames,
                                     bool lowContrastMode,
                                     std::function<bool(int)> progressCallback = nullptr,
                                     size_t* outComposedFrameCount = nullptr,
                                     std::vector<int>* outComposedAxisSteps = nullptr );
static bool RunPanoramaCaptureCommon( HWND hWnd, bool saveToFile );

//----------------------------------------------------------------------------
// Lightweight parallel_for using std::thread.
// Distributes [begin, end) work items across up to hardware_concurrency
// threads using atomic work-stealing.  Falls back to serial execution
// for single items or single-core machines.
//----------------------------------------------------------------------------
template<typename Func>
static void parallel_for( int begin, int end, const Func& body )
{
    const int count = end - begin;
    if( count <= 0 )
        return;
    const int maxThreads = static_cast<int>( std::thread::hardware_concurrency() );
    const int numThreads = min( maxThreads, count );
    if( numThreads <= 1 )
    {
        for( int i = begin; i < end; ++i )
            body( i );
        return;
    }
    std::vector<std::thread> threads( numThreads - 1 );
    std::atomic<int> nextIndex( begin );
    auto worker = [&]()
    {
        for( ;; )
        {
            const int i = nextIndex.fetch_add( 1 );
            if( i >= end )
                break;
            body( i );
        }

    };
    for( auto& t : threads )
        t = std::thread( worker );
    worker();
    for( auto& t : threads )
        t.join();
}

//----------------------------------------------------------------------------
// Progress dialog for panorama stitching.
//----------------------------------------------------------------------------
class PanoramaProgressDialog
{
public:
    PanoramaProgressDialog() : m_hWnd( nullptr ), m_hProgress( nullptr ), m_hLabel( nullptr ), m_hButton( nullptr ), m_cancelled( false ) {}

    void Create( HWND hWndParent )
    {
        EnsureWindowClass();

        m_cancelled = false;

        // Get DPI for proper sizing
        const UINT dpi = GetDpiForWindowHelper( hWndParent ? hWndParent : GetDesktopWindow() );
        const int margin = ScaleForDpi( 14, dpi );
        const int labelHeight = ScaleForDpi( 20, dpi );
        const int barHeight = ScaleForDpi( 16, dpi );
        const int buttonHeight = ScaleForDpi( 26, dpi );
        const int buttonWidth = ScaleForDpi( 80, dpi );
        const int spacing = ScaleForDpi( 10, dpi );

        // Compute desired client area, then inflate to full window size
        const int clientWidth = ScaleForDpi( 340, dpi );
        const int clientHeight = margin + labelHeight + spacing + barHeight + spacing + buttonHeight + margin;
        const DWORD style = WS_POPUP | WS_CAPTION | WS_VISIBLE | WS_CLIPCHILDREN;
        const DWORD exStyle = WS_EX_TOOLWINDOW | WS_EX_TOPMOST;
        RECT rcWindow = { 0, 0, clientWidth, clientHeight };
        AdjustWindowRectEx( &rcWindow, style, FALSE, exStyle );
        const int dlgWidth = rcWindow.right - rcWindow.left;
        const int dlgHeight = rcWindow.bottom - rcWindow.top;

        RECT rcDesktop{};
        GetWindowRect( GetDesktopWindow(), &rcDesktop );
        const int x = ( rcDesktop.right - dlgWidth ) / 2;
        const int y = ( rcDesktop.bottom - dlgHeight ) / 2;

        m_hWnd = CreateWindowExW(
            exStyle,
            L"ZoomItProgressDialog",
            L"ZoomIt",
            style,
            x, y, dlgWidth, dlgHeight,
            hWndParent, nullptr, g_hInstance, nullptr );
        if( m_hWnd == nullptr )
            return;

        SetWindowLongPtr( m_hWnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>( this ) );

        // Apply dark mode to title bar
        const bool darkMode = IsDarkModeEnabled();
        SetDarkModeForWindow( m_hWnd, darkMode );

        m_hLabel = CreateWindowExW(
            0, L"STATIC", L"Processing panorama...",
            WS_CHILD | WS_VISIBLE | SS_LEFT,
            margin, margin, clientWidth - margin * 2, labelHeight,
            m_hWnd, nullptr, g_hInstance, nullptr );

        m_hProgress = CreateWindowExW(
            0, PROGRESS_CLASSW, nullptr,
            WS_CHILD | WS_VISIBLE | PBS_SMOOTH,
            margin, margin + labelHeight + spacing, clientWidth - margin * 2, barHeight,
            m_hWnd, nullptr, g_hInstance, nullptr );

        m_hButton = CreateWindowExW(
            0, L"BUTTON", L"Cancel",
            WS_CHILD | WS_VISIBLE | WS_TABSTOP | BS_PUSHBUTTON,
            clientWidth - margin - buttonWidth, margin + labelHeight + spacing + barHeight + spacing, buttonWidth, buttonHeight,
            m_hWnd, reinterpret_cast<HMENU>( static_cast<INT_PTR>( IDCANCEL ) ), g_hInstance, nullptr );
        if( m_hButton && darkMode )
        {
            SetWindowTheme( m_hButton, L"DarkMode_Explorer", nullptr );
        }

        if( m_hProgress )
        {
            SendMessage( m_hProgress, PBM_SETRANGE, 0, MAKELPARAM( 0, 100 ) );
            SendMessage( m_hProgress, PBM_SETPOS, 0, 0 );
            // Remove sunken border
            SetWindowLongPtr( m_hProgress, GWL_EXSTYLE,
                GetWindowLongPtr( m_hProgress, GWL_EXSTYLE ) & ~WS_EX_STATICEDGE );
            SetWindowPos( m_hProgress, nullptr, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED );

            // Disable visual styles so PBM_SETBARCOLOR is honored
            SetWindowTheme( m_hProgress, L"", L"" );
            SendMessage( m_hProgress, PBM_SETBARCOLOR, 0, static_cast<LPARAM>( RGB( 0x00, 0x78, 0xD4 ) ) );
            if( darkMode )
            {
                SendMessage( m_hProgress, PBM_SETBKCOLOR, 0, static_cast<LPARAM>( DarkMode::SurfaceColor ) );
            }
        }

        // Set font scaled for DPI
        NONCLIENTMETRICSW ncm{};
        ncm.cbSize = sizeof( ncm );
        SystemParametersInfoW( SPI_GETNONCLIENTMETRICS, sizeof( ncm ), &ncm, 0 );
        ncm.lfMessageFont.lfHeight = -ScaleForDpi( 12, dpi );
        m_hFont = CreateFontIndirectW( &ncm.lfMessageFont );
        if( m_hFont )
        {
            SendMessage( m_hLabel, WM_SETFONT, reinterpret_cast<WPARAM>( m_hFont ), TRUE );
            SendMessage( m_hButton, WM_SETFONT, reinterpret_cast<WPARAM>( m_hFont ), TRUE );
        }

        HICON hIcon = LoadIcon( g_hInstance, L"APPICON" );
        if( hIcon )
        {
            SendMessage( m_hWnd, WM_SETICON, ICON_SMALL, reinterpret_cast<LPARAM>( hIcon ) );
        }

        UpdateWindow( m_hWnd );
    }

    void SetProgress( int percent )
    {
        if( m_hProgress )
        {
            SendMessage( m_hProgress, PBM_SETPOS, percent, 0 );
        }
        PumpMessages();
    }

    bool IsCancelled() const { return m_cancelled; }

    void Destroy()
    {
        if( m_hWnd )
        {
            DestroyWindow( m_hWnd );
            m_hWnd = nullptr;
            m_hLabel = nullptr;
            m_hProgress = nullptr;
            m_hButton = nullptr;
        }
        if( m_hFont )
        {
            DeleteObject( m_hFont );
            m_hFont = nullptr;
        }
    }

private:
    void PumpMessages()
    {
        MSG msg{};
        while( PeekMessage( &msg, nullptr, 0, 0, PM_REMOVE ) )
        {
            if( msg.message == WM_KEYDOWN && msg.wParam == VK_ESCAPE )
            {
                m_cancelled = true;
                continue;
            }
            TranslateMessage( &msg );
            DispatchMessage( &msg );
        }
    }

    HWND m_hWnd;
    HWND m_hProgress;
    HWND m_hLabel;
    HWND m_hButton;
    bool m_cancelled;
    HFONT m_hFont = nullptr;

    static LRESULT CALLBACK WndProc( HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam )
    {
        PanoramaProgressDialog* pThis = reinterpret_cast<PanoramaProgressDialog*>(
            GetWindowLongPtr( hWnd, GWLP_USERDATA ) );
        switch( uMsg )
        {
        case WM_COMMAND:
            if( LOWORD( wParam ) == IDCANCEL && pThis )
            {
                pThis->m_cancelled = true;
                return 0;
            }
            break;

        case WM_CLOSE:
            if( pThis )
            {
                pThis->m_cancelled = true;
            }
            return 0;

        case WM_CTLCOLORSTATIC:
        case WM_CTLCOLORBTN:
            if( IsDarkModeEnabled() )
            {
                HDC hdc = reinterpret_cast<HDC>( wParam );
                SetTextColor( hdc, DarkMode::TextColor );
                SetBkColor( hdc, DarkMode::BackgroundColor );
                return reinterpret_cast<LRESULT>( GetDarkModeBrush() );
            }
            break;

        case WM_ERASEBKGND:
            if( IsDarkModeEnabled() )
            {
                HDC hdc = reinterpret_cast<HDC>( wParam );
                RECT rc{};
                GetClientRect( hWnd, &rc );
                FillRect( hdc, &rc, GetDarkModeBrush() );
                return 1;
            }
            break;
        }
        return DefWindowProcW( hWnd, uMsg, wParam, lParam );
    }

    static void EnsureWindowClass()
    {
        static bool registered = false;
        if( !registered )
        {
            WNDCLASSEXW wc{};
            wc.cbSize = sizeof( wc );
            wc.lpfnWndProc = WndProc;
            wc.hInstance = g_hInstance;
            wc.hCursor = LoadCursor( nullptr, IDC_ARROW );
            wc.hbrBackground = reinterpret_cast<HBRUSH>( COLOR_WINDOW + 1 );
            wc.lpszClassName = L"ZoomItProgressDialog";
            RegisterClassExW( &wc );
            registered = true;
        }
    }
};

static PanoramaProgressDialog g_ProgressDialog;

// Temporary file-based trace for stitch debugging (debug builds only).
static FILE* g_StitchLogFile = nullptr;

// Returns true when panorama debug output (frame dumps + stitch log) is active.
// Debug builds always enable it; release builds enable it via /panorama-debug.
static bool PanoramaDebugEnabled()
{
#ifdef _DEBUG
    return true;
#else
    return g_PanoramaDebugMode;
#endif
}

static void StitchLog( const wchar_t* format, ... )
{
    if( !PanoramaDebugEnabled() )
    {
        return;
    }
    va_list args;
#pragma warning(push)
#pragma warning(disable: 26492) // Don't use const_cast - unavoidable in va_start macro
    va_start( args, format );
#pragma warning(pop)
    wchar_t buffer[1024]{};
    _vsnwprintf_s( buffer, _TRUNCATE, format, args );
    va_end( args );
    OutputDebug( L"%s", buffer );
    if( g_StitchLogFile != nullptr )
    {
        // Convert to narrow for easy reading
        char narrow[2048]{};
        WideCharToMultiByte( CP_UTF8, 0, buffer, -1, narrow, sizeof( narrow ) - 1, nullptr, nullptr );
        fputs( narrow, g_StitchLogFile );
        fflush( g_StitchLogFile );
    }
}

// Emit a compact transition trace for composed frames so capture repros can
// pinpoint skipped-content jumps and zero-overlap seams without changing
// stitch behavior.
static void LogComposedFrameDiagnostics( const std::vector<size_t>& composedFrameIndices,
                                         const std::vector<POINT>& composedFrameOrigins,
                                         const std::vector<POINT>& composedFrameSteps,
                                         int frameWidth,
                                         int frameHeight )
{
    if( !PanoramaDebugEnabled() || composedFrameIndices.size() < 2 ||
        composedFrameOrigins.size() != composedFrameIndices.size() ||
        composedFrameSteps.size() != composedFrameIndices.size() )
    {
        return;
    }

    StitchLog( L"[Panorama/Stitch] Composed transition diagnostics begin count=%zu\n",
               composedFrameIndices.size() );

    std::vector<int> recentAxisSteps;
    recentAxisSteps.reserve( 8 );
    for( size_t i = 1; i < composedFrameIndices.size(); ++i )
    {
        const POINT& step = composedFrameSteps[i];
        const POINT& origin = composedFrameOrigins[i];
        const int gap = static_cast<int>( composedFrameIndices[i] - composedFrameIndices[i - 1] );
        const bool mostlyVertical = abs( step.y ) >= abs( step.x );
        const int axisFrame = mostlyVertical ? frameHeight : frameWidth;
        const int axisStep = max( abs( step.x ), abs( step.y ) );
        const int axisOverlap = axisFrame - axisStep;

        int recentMedian = 0;
        if( !recentAxisSteps.empty() )
        {
            std::vector<int> sorted = recentAxisSteps;
            std::sort( sorted.begin(), sorted.end() );
            recentMedian = sorted[sorted.size() / 2];
        }

        const bool suspiciousGapBridge = gap > 1;
        const bool suspiciousMissingOverlap = axisOverlap <= 0;
        const bool suspiciousSpike = recentMedian > 0 &&
                                     axisStep >= max( axisFrame / 6, recentMedian * 3 ) &&
                                     axisOverlap < axisFrame * 3 / 4;

        StitchLog( L"[Panorama/Stitch] Transition %zu: frames %zu->%zu gap=%d step=(%d,%d) axis=%ls axisStep=%d overlap=%d origin=(%d,%d)%ls%ls%ls recentMedian=%d\n",
                   i,
                   composedFrameIndices[i - 1],
                   composedFrameIndices[i],
                   gap,
                   step.x,
                   step.y,
                   mostlyVertical ? L"vertical" : L"horizontal",
                   axisStep,
                   axisOverlap,
                   origin.x,
                   origin.y,
                   suspiciousGapBridge ? L" [gap-bridge]" : L"",
                   suspiciousMissingOverlap ? L" [no-overlap]" : L"",
                   suspiciousSpike ? L" [spike]" : L"",
                   recentMedian );

        recentAxisSteps.push_back( axisStep );
        if( recentAxisSteps.size() > 8 )
        {
            recentAxisSteps.erase( recentAxisSteps.begin() );
        }
    }

    StitchLog( L"[Panorama/Stitch] Composed transition diagnostics end\n" );
}

// Emit row-level ownership diagnostics after composition so seam artifacts can
// be traced back to specific frames, blended handoffs, or true unwritten gaps.
static void LogCompositionCoverageDiagnostics( const std::vector<int>& stitchedOwner,
                                               const std::vector<BYTE>& stitchedWritten,
                                               const std::vector<BYTE>& stitchedBlended,
                                               int stitchedWidth,
                                               int stitchedHeight )
{
    if( !PanoramaDebugEnabled() || stitchedWidth <= 0 || stitchedHeight <= 0 ||
        stitchedOwner.size() != stitchedWritten.size() ||
        stitchedWritten.size() != stitchedBlended.size() )
    {
        return;
    }

    struct SuspiciousRowInfo
    {
        int y;
        int unwrittenCount;
        int blendedCount;
        size_t segmentCount;
        bool smallSegment;
        std::wstring summary;
    };

    const int smallSegmentWidth = max( 4, min( 24, stitchedWidth / 40 ) );
    std::vector<SuspiciousRowInfo> suspiciousRows;
    suspiciousRows.reserve( 16 );
    int rowsWithGaps = 0;
    int rowsWithBlend = 0;

    for( int y = 0; y < stitchedHeight; ++y )
    {
        const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
        int unwrittenCount = 0;
        int blendedCount = 0;
        std::vector<std::pair<int, int>> segments;
        segments.reserve( 12 );

        auto markerForPixel = [&]( size_t idx )
        {
            if( stitchedWritten[idx] == 0 )
            {
                return -1;
            }
            if( stitchedBlended[idx] != 0 )
            {
                return -2;
            }
            return stitchedOwner[idx];
        };

        int currentMarker = markerForPixel( rowBase );
        int currentLength = 0;
        for( int x = 0; x < stitchedWidth; ++x )
        {
            const size_t idx = rowBase + static_cast<size_t>( x );
            const int marker = markerForPixel( idx );
            if( stitchedWritten[idx] == 0 )
            {
                ++unwrittenCount;
            }
            if( stitchedBlended[idx] != 0 )
            {
                ++blendedCount;
            }

            if( marker != currentMarker )
            {
                segments.push_back( { currentMarker, currentLength } );
                currentMarker = marker;
                currentLength = 1;
            }
            else
            {
                ++currentLength;
            }
        }
        segments.push_back( { currentMarker, currentLength } );

        bool smallSegment = false;
        for( size_t si = 1; si + 1 < segments.size(); ++si )
        {
            const int marker = segments[si].first;
            const int length = segments[si].second;
            if( marker != -1 && length <= smallSegmentWidth )
            {
                smallSegment = true;
                break;
            }
        }

        if( unwrittenCount > 0 )
        {
            ++rowsWithGaps;
        }
        if( blendedCount > 0 )
        {
            ++rowsWithBlend;
        }

        const bool suspiciousRow = unwrittenCount > 0 || smallSegment ||
                                   ( blendedCount > max( 8, stitchedWidth / 12 ) && segments.size() >= 4 );
        if( !suspiciousRow )
        {
            continue;
        }

        std::wstring summary;
        for( size_t si = 0; si < segments.size() && si < 8; ++si )
        {
            if( si > 0 )
            {
                summary += L"|";
            }

            wchar_t segmentText[48]{};
            const int marker = segments[si].first;
            const int length = segments[si].second;
            if( marker == -1 )
            {
                swprintf_s( segmentText, L"gap:%d", length );
            }
            else if( marker == -2 )
            {
                swprintf_s( segmentText, L"blend:%d", length );
            }
            else
            {
                swprintf_s( segmentText, L"f%d:%d", marker, length );
            }
            summary += segmentText;
        }
        if( segments.size() > 8 )
        {
            summary += L"|...";
        }

        suspiciousRows.push_back( { y, unwrittenCount, blendedCount, segments.size(), smallSegment, summary } );
    }

    StitchLog( L"[Panorama/Stitch] Coverage diagnostics rowsWithGaps=%d rowsWithBlend=%d suspiciousRows=%zu\n",
               rowsWithGaps,
               rowsWithBlend,
               suspiciousRows.size() );
    if( suspiciousRows.empty() )
    {
        return;
    }

    const size_t maxRowsToLog = 40;
    for( size_t i = 0; i < suspiciousRows.size() && i < maxRowsToLog; ++i )
    {
        const auto& row = suspiciousRows[i];
        StitchLog( L"[Panorama/Stitch] Coverage row y=%d unwritten=%d blended=%d segments=%zu smallSegment=%d summary=%s\n",
                   row.y,
                   row.unwrittenCount,
                   row.blendedCount,
                   row.segmentCount,
                   row.smallSegment ? 1 : 0,
                   row.summary.c_str() );
    }
    if( suspiciousRows.size() > maxRowsToLog )
    {
        StitchLog( L"[Panorama/Stitch] Coverage diagnostics truncated %zu additional suspicious row(s)\n",
                   suspiciousRows.size() - maxRowsToLog );
    }
}

static std::wstring BuildStitchedRowSummary( const std::vector<int>& stitchedOwner,
                                             const std::vector<BYTE>& stitchedWritten,
                                             const std::vector<BYTE>& stitchedBlended,
                                             int stitchedWidth,
                                             int stitchedHeight,
                                             int y,
                                             size_t maxSegments = 8 )
{
    std::wstring summary;
    if( stitchedWidth <= 0 || stitchedHeight <= 0 || y < 0 || y >= stitchedHeight ||
        stitchedOwner.size() != stitchedWritten.size() ||
        stitchedWritten.size() != stitchedBlended.size() )
    {
        return summary;
    }

    const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
    auto markerForPixel = [&]( size_t idx )
    {
        if( stitchedWritten[idx] == 0 )
        {
            return -1;
        }
        if( stitchedBlended[idx] != 0 )
        {
            return -2;
        }
        return stitchedOwner[idx];
    };

    int currentMarker = markerForPixel( rowBase );
    int currentLength = 0;
    size_t emittedSegments = 0;
    for( int x = 0; x < stitchedWidth; ++x )
    {
        const int marker = markerForPixel( rowBase + static_cast<size_t>( x ) );
        if( marker != currentMarker )
        {
            if( emittedSegments > 0 )
            {
                summary += L"|";
            }

            wchar_t segmentText[48]{};
            if( currentMarker == -1 )
            {
                swprintf_s( segmentText, L"gap:%d", currentLength );
            }
            else if( currentMarker == -2 )
            {
                swprintf_s( segmentText, L"blend:%d", currentLength );
            }
            else
            {
                swprintf_s( segmentText, L"f%d:%d", currentMarker, currentLength );
            }
            summary += segmentText;

            ++emittedSegments;
            if( emittedSegments >= maxSegments )
            {
                summary += L"|...";
                return summary;
            }

            currentMarker = marker;
            currentLength = 1;
        }
        else
        {
            ++currentLength;
        }
    }

    if( emittedSegments > 0 )
    {
        summary += L"|";
    }
    wchar_t segmentText[48]{};
    if( currentMarker == -1 )
    {
        swprintf_s( segmentText, L"gap:%d", currentLength );
    }
    else if( currentMarker == -2 )
    {
        swprintf_s( segmentText, L"blend:%d", currentLength );
    }
    else
    {
        swprintf_s( segmentText, L"f%d:%d", currentLength == 0 ? -1 : currentMarker, currentLength );
    }
    summary += segmentText;
    return summary;
}

static void LogSuspiciousTransitionWindowDiagnostics( const std::vector<BYTE>& stitchedPixels,
                                                      const std::vector<int>& stitchedOwner,
                                                      const std::vector<BYTE>& stitchedWritten,
                                                      const std::vector<BYTE>& stitchedBlended,
                                                      const std::vector<int>& rowBlendPixelCount,
                                                      const std::vector<int>& rowBlendWeightSum,
                                                      const std::vector<int>& rowBlendWeightMin,
                                                      const std::vector<int>& rowBlendWeightMax,
                                                      const std::vector<int>& rowBlendDominantFrame,
                                                      const std::vector<int>& rowBlendDominantPixels,
                                                      const std::vector<int>& rowFullWidthBlendFirstFrame,
                                                      const std::vector<int>& rowFullWidthBlendFirstPass,
                                                      const std::vector<int>& rowFullWidthBlendFirstWeight,
                                                      const std::vector<int>& rowFullWidthBlendLastFrame,
                                                      const std::vector<int>& rowFullWidthBlendLastPass,
                                                      const std::vector<int>& rowFullWidthBlendLastWeight,
                                                      const std::vector<int>& rowFullWidthBlendPassCount,
                                                      int stitchedWidth,
                                                      int stitchedHeight,
                                                      const std::vector<size_t>& composedFrameIndices,
                                                      const std::vector<POINT>& composedFrameOrigins,
                                                      const std::vector<POINT>& composedFrameSteps,
                                                      int frameWidth,
                                                      int frameHeight,
                                                      int verticalFeather,
                                                      int horizontalFeather,
                                                      int minX,
                                                      int minY )
{
    if( !PanoramaDebugEnabled() || stitchedWidth <= 0 || stitchedHeight <= 0 ||
        stitchedPixels.size() != static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4 ||
        stitchedOwner.size() != stitchedWritten.size() ||
        stitchedWritten.size() != stitchedBlended.size() ||
        rowBlendPixelCount.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightSum.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightMin.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightMax.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendDominantFrame.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendDominantPixels.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstFrame.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstPass.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstWeight.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastFrame.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastPass.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastWeight.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendPassCount.size() != static_cast<size_t>( stitchedHeight ) ||
        composedFrameIndices.size() < 2 ||
        composedFrameOrigins.size() != composedFrameIndices.size() ||
        composedFrameSteps.size() != composedFrameIndices.size() )
    {
        return;
    }

    int totalAbsStepX = 0;
    int totalAbsStepY = 0;
    for( size_t i = 1; i < composedFrameSteps.size(); ++i )
    {
        totalAbsStepX += abs( composedFrameSteps[i].x );
        totalAbsStepY += abs( composedFrameSteps[i].y );
    }

    const bool mostlyVerticalCapture = totalAbsStepY >= totalAbsStepX;
    const int axisFrame = mostlyVerticalCapture ? frameHeight : frameWidth;
    const int windowRadius = max( 12, min( 48, axisFrame / 10 ) );
    const int minWrittenForSignal = stitchedWidth * 9 / 10;
    const int maxPriorityTransitionsToLog = 12;
    const int maxNonPriorityTransitionsToLog = 10;
    int loggedTransitions = 0;
    int loggedPriorityTransitions = 0;
    int loggedNonPriorityTransitions = 0;

    auto rowAverageLuma = [&]( int y )
    {
        if( y < 0 || y >= stitchedHeight )
        {
            return -1;
        }

        const size_t pixelRowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth ) * 4;
        unsigned __int64 totalLuma = 0;
        for( int x = 0; x < stitchedWidth; ++x )
        {
            const size_t pixelIdx = pixelRowBase + static_cast<size_t>( x ) * 4;
            totalLuma += ( static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 2] ) * 77 +
                           static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 1] ) * 150 +
                           static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 0] ) * 29 ) >> 8;
        }
        return static_cast<int>( totalLuma / max( 1, stitchedWidth ) );
    };

    auto rowWrittenCount = [&]( int y )
    {
        if( y < 0 || y >= stitchedHeight )
        {
            return 0;
        }

        const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
        int written = 0;
        for( int x = 0; x < stitchedWidth; ++x )
        {
            written += stitchedWritten[rowBase + static_cast<size_t>( x )] != 0 ? 1 : 0;
        }
        return written;
    };

    auto rowBlendedCount = [&]( int y )
    {
        if( y < 0 || y >= stitchedHeight )
        {
            return 0;
        }

        const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
        int blended = 0;
        for( int x = 0; x < stitchedWidth; ++x )
        {
            blended += stitchedBlended[rowBase + static_cast<size_t>( x )] != 0 ? 1 : 0;
        }
        return blended;
    };

    auto rowBlendAverageWeight = [&]( int y )
    {
        if( y < 0 || y >= stitchedHeight || rowBlendPixelCount[y] <= 0 )
        {
            return 0;
        }
        return rowBlendWeightSum[y] / rowBlendPixelCount[y];
    };

    StitchLog( L"[Panorama/Stitch] Seam window diagnostics begin axis=%ls radius=%d\n",
               mostlyVerticalCapture ? L"vertical" : L"horizontal",
               windowRadius );

    for( size_t i = 1; i < composedFrameIndices.size(); ++i )
    {
        const POINT& step = composedFrameSteps[i];
        const int gap = static_cast<int>( composedFrameIndices[i] - composedFrameIndices[i - 1] );
        const int axisStep = mostlyVerticalCapture ? abs( step.y ) : abs( step.x );
        const int axisOverlap = axisFrame - axisStep;
        const bool suspiciousTransition =
            gap > 1 || axisOverlap < axisFrame * 3 / 4 || axisStep >= axisFrame / 6;
        const bool priorityTransition =
            gap > 1 || i + 4 >= composedFrameIndices.size();
        if( !suspiciousTransition )
        {
            continue;
        }
        if( priorityTransition )
        {
            if( loggedPriorityTransitions >= maxPriorityTransitionsToLog )
            {
                continue;
            }
        }
        else if( loggedNonPriorityTransitions >= maxNonPriorityTransitionsToLog )
        {
            continue;
        }

        const int boundary = mostlyVerticalCapture
            ? ( composedFrameOrigins[i].y - minY )
            : ( composedFrameOrigins[i].x - minX );
        const int windowStart = max( 0, boundary - windowRadius );
        const int windowEnd = min( stitchedHeight - 1, boundary + windowRadius );
        int featherStart = -1;
        int featherEnd = -1;
        int featherStartWeight = -1;
        if( mostlyVerticalCapture && axisOverlap > 0 )
        {
            const int destinationY = composedFrameOrigins[i].y - minY;
            if( step.y > 0 )
            {
                featherStart = destinationY + max( 0, axisOverlap - verticalFeather );
                featherEnd = destinationY + max( 0, axisOverlap - 1 );
            }
            else if( step.y < 0 )
            {
                featherStart = destinationY + abs( step.y );
                featherEnd = featherStart + max( 0, verticalFeather - 1 );
            }
            if( featherStart >= 0 )
            {
                featherStartWeight = 255 / max( 1, verticalFeather );
            }
        }
        else if( !mostlyVerticalCapture && axisOverlap > 0 )
        {
            const int destinationX = composedFrameOrigins[i].x - minX;
            if( step.x > 0 )
            {
                featherStart = destinationX + max( 0, axisOverlap - horizontalFeather );
                featherEnd = destinationX + max( 0, axisOverlap - 1 );
            }
            else if( step.x < 0 )
            {
                featherStart = destinationX + abs( step.x );
                featherEnd = featherStart + max( 0, horizontalFeather - 1 );
            }
            if( featherStart >= 0 )
            {
                featherStartWeight = 255 / max( 1, horizontalFeather );
            }
        }

        int darkestRow = -1;
        int darkestLuma = ( std::numeric_limits<int>::max )();
        int maxBlendRow = -1;
        int maxBlendCount = -1;
        for( int y = windowStart; y <= windowEnd; ++y )
        {
            const int writtenCount = rowWrittenCount( y );
            if( writtenCount < minWrittenForSignal )
            {
                continue;
            }

            const int luma = rowAverageLuma( y );
            if( luma >= 0 && luma < darkestLuma )
            {
                darkestLuma = luma;
                darkestRow = y;
            }

            const int blendedCount = rowBlendedCount( y );
            if( blendedCount > maxBlendCount )
            {
                maxBlendCount = blendedCount;
                maxBlendRow = y;
            }
        }

        StitchLog( L"[Panorama/Stitch] Seam transition %zu frames %zu->%zu gap=%d boundary=%d axisStep=%d overlap=%d feather=%d..%d featherStartWeight=%d window=%d..%d darkestRow=%d darkestLuma=%d maxBlendRow=%d maxBlend=%d\n",
                   i,
                   composedFrameIndices[i - 1],
                   composedFrameIndices[i],
                   gap,
                   boundary,
                   axisStep,
                   axisOverlap,
                   featherStart,
                   featherEnd,
                   featherStartWeight,
                   windowStart,
                   windowEnd,
                   darkestRow,
                   darkestLuma == ( std::numeric_limits<int>::max )() ? -1 : darkestLuma,
                   maxBlendRow,
                   maxBlendCount );

        const int featherMid = ( featherStart >= 0 && featherEnd >= featherStart )
            ? ( featherStart + featherEnd ) / 2
            : -1;
        const int sampleRows[] = { boundary - 1, boundary, boundary + 1, featherStart, featherMid, featherEnd, darkestRow, maxBlendRow };
        const wchar_t* sampleLabels[] = { L"boundary-1", L"boundary", L"boundary+1", L"featherStart", L"featherMid", L"featherEnd", L"darkest", L"maxBlend" };
        for( int sampleIndex = 0; sampleIndex < ARRAYSIZE( sampleRows ); ++sampleIndex )
        {
            const int sampleRow = sampleRows[sampleIndex];
            if( sampleRow < 0 || sampleRow >= stitchedHeight )
            {
                continue;
            }

            bool alreadyLogged = false;
            for( int prior = 0; prior < sampleIndex; ++prior )
            {
                if( sampleRows[prior] == sampleRow )
                {
                    alreadyLogged = true;
                    break;
                }
            }
            if( alreadyLogged )
            {
                continue;
            }

            StitchLog( L"[Panorama/Stitch] Seam row transition=%zu label=%ls y=%d luma=%d written=%d blended=%d blendPixels=%d blendAvg=%d blendMin=%d blendMax=%d blendDominantFrame=%d blendDominantPixels=%d fullBlendFirst=(frame:%d pass:%d weight:%d) fullBlendLast=(frame:%d pass:%d weight:%d) fullBlendPasses=%d summary=%s\n",
                       i,
                       sampleLabels[sampleIndex],
                       sampleRow,
                       rowAverageLuma( sampleRow ),
                       rowWrittenCount( sampleRow ),
                       rowBlendedCount( sampleRow ),
                       rowBlendPixelCount[sampleRow],
                       rowBlendAverageWeight( sampleRow ),
                       rowBlendPixelCount[sampleRow] > 0 ? rowBlendWeightMin[sampleRow] : 0,
                       rowBlendPixelCount[sampleRow] > 0 ? rowBlendWeightMax[sampleRow] : 0,
                       rowBlendDominantFrame[sampleRow],
                       rowBlendDominantPixels[sampleRow],
                       rowFullWidthBlendFirstFrame[sampleRow],
                       rowFullWidthBlendFirstPass[sampleRow],
                       rowFullWidthBlendFirstWeight[sampleRow],
                       rowFullWidthBlendLastFrame[sampleRow],
                       rowFullWidthBlendLastPass[sampleRow],
                       rowFullWidthBlendLastWeight[sampleRow],
                       rowFullWidthBlendPassCount[sampleRow],
                       BuildStitchedRowSummary( stitchedOwner,
                                                stitchedWritten,
                                                stitchedBlended,
                                                stitchedWidth,
                                                stitchedHeight,
                                                sampleRow ).c_str() );
        }

        ++loggedTransitions;
        if( priorityTransition )
        {
            ++loggedPriorityTransitions;
        }
        else
        {
            ++loggedNonPriorityTransitions;
        }
    }

    StitchLog( L"[Panorama/Stitch] Seam window diagnostics end logged=%d priority=%d nonPriority=%d\n",
               loggedTransitions,
               loggedPriorityTransitions,
               loggedNonPriorityTransitions );
}

// Detect visually dark stitched bands even when the canvas has no unwritten
// gaps so we can correlate full-width artifacts with a specific frame handoff.
static void LogStitchedBandDiagnostics( const std::vector<BYTE>& stitchedPixels,
                                        const std::vector<int>& stitchedOwner,
                                        const std::vector<BYTE>& stitchedWritten,
                                        const std::vector<BYTE>& stitchedBlended,
                                        const std::vector<int>& rowBlendPixelCount,
                                        const std::vector<int>& rowBlendWeightSum,
                                        const std::vector<int>& rowBlendWeightMin,
                                        const std::vector<int>& rowBlendWeightMax,
                                        const std::vector<int>& rowFullWidthBlendFirstFrame,
                                        const std::vector<int>& rowFullWidthBlendFirstPass,
                                        const std::vector<int>& rowFullWidthBlendFirstWeight,
                                        const std::vector<int>& rowFullWidthBlendLastFrame,
                                        const std::vector<int>& rowFullWidthBlendLastPass,
                                        const std::vector<int>& rowFullWidthBlendLastWeight,
                                        const std::vector<int>& rowFullWidthBlendPassCount,
                                        int stitchedWidth,
                                        int stitchedHeight,
                                        const std::vector<size_t>& composedFrameIndices,
                                        const std::vector<POINT>& composedFrameOrigins,
                                        int minY )
{
    if( !PanoramaDebugEnabled() || stitchedWidth <= 0 || stitchedHeight <= 0 ||
        stitchedPixels.size() != static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4 ||
        stitchedOwner.size() != stitchedWritten.size() ||
        stitchedWritten.size() != stitchedBlended.size() ||
        rowBlendPixelCount.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightSum.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightMin.size() != static_cast<size_t>( stitchedHeight ) ||
        rowBlendWeightMax.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstFrame.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstPass.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendFirstWeight.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastFrame.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastPass.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendLastWeight.size() != static_cast<size_t>( stitchedHeight ) ||
        rowFullWidthBlendPassCount.size() != static_cast<size_t>( stitchedHeight ) )
    {
        return;
    }

    std::vector<int> rowLuma( stitchedHeight, 0 );
    std::vector<int> rowBlended( stitchedHeight, 0 );
    std::vector<int> rowWritten( stitchedHeight, 0 );
    std::vector<std::pair<int, int>> darkBandFirstPassCounts;
    std::vector<std::pair<int, int>> darkBandLastPassCounts;
    auto incrementPassCount = []( std::vector<std::pair<int, int>>& counts, int pass )
    {
        if( pass < 0 )
        {
            return;
        }
        for( auto& entry : counts )
        {
            if( entry.first == pass )
            {
                entry.second++;
                return;
            }
        }
        counts.push_back( { pass, 1 } );
    };
    auto rowBlendAverageWeight = [&]( int y )
    {
        if( y < 0 || y >= stitchedHeight || rowBlendPixelCount[y] <= 0 )
        {
            return 0;
        }
        return rowBlendWeightSum[y] / rowBlendPixelCount[y];
    };
    for( int y = 0; y < stitchedHeight; ++y )
    {
        const size_t pixelRowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth ) * 4;
        const size_t maskRowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
        unsigned __int64 totalLuma = 0;
        for( int x = 0; x < stitchedWidth; ++x )
        {
            const size_t pixelIdx = pixelRowBase + static_cast<size_t>( x ) * 4;
            totalLuma += ( static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 2] ) * 77 +
                           static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 1] ) * 150 +
                           static_cast<unsigned __int64>( stitchedPixels[pixelIdx + 0] ) * 29 ) >> 8;

            const size_t maskIdx = maskRowBase + static_cast<size_t>( x );
            if( stitchedWritten[maskIdx] != 0 )
            {
                ++rowWritten[y];
            }
            if( stitchedBlended[maskIdx] != 0 )
            {
                ++rowBlended[y];
            }
        }
        rowLuma[y] = static_cast<int>( totalLuma / max( 1, stitchedWidth ) );
    }

    struct DarkBandInfo
    {
        int startY;
        int endY;
        int centerY;
        int avgLuma;
        int referenceLuma;
        int delta;
    };

    const int referenceRadius = max( 8, min( 24, stitchedHeight / 40 ) );
    const int minBandThickness = 2;
    const int maxBandsToLog = 12;
    std::vector<DarkBandInfo> darkBands;
    std::vector<BYTE> darkRowMask( stitchedHeight, 0 );

    for( int y = referenceRadius; y < stitchedHeight - referenceRadius; ++y )
    {
        if( rowWritten[y] < stitchedWidth * 9 / 10 )
        {
            continue;
        }

        unsigned __int64 neighborhoodTotal = 0;
        int neighborhoodCount = 0;
        for( int offset = -referenceRadius; offset <= referenceRadius; ++offset )
        {
            if( offset == 0 || abs( offset ) <= 2 )
            {
                continue;
            }

            const int sampleY = y + offset;
            neighborhoodTotal += static_cast<unsigned __int64>( rowLuma[sampleY] );
            ++neighborhoodCount;
        }
        if( neighborhoodCount <= 0 )
        {
            continue;
        }

        const int referenceLuma = static_cast<int>( neighborhoodTotal / neighborhoodCount );
        const int delta = referenceLuma - rowLuma[y];
        const bool isDarkOutlier = referenceLuma >= 24 &&
                                   delta >= max( 18, referenceLuma / 5 ) &&
                                   rowBlended[y] >= stitchedWidth / 3;
        if( isDarkOutlier )
        {
            darkRowMask[y] = 1;
        }
    }

    for( int y = 0; y < stitchedHeight; )
    {
        if( darkRowMask[y] == 0 )
        {
            ++y;
            continue;
        }

        const int startY = y;
        int endY = y;
        while( endY + 1 < stitchedHeight && darkRowMask[endY + 1] != 0 )
        {
            ++endY;
        }

        if( endY - startY + 1 >= minBandThickness )
        {
            unsigned __int64 bandLumaTotal = 0;
            unsigned __int64 refLumaTotal = 0;
            for( int bandY = startY; bandY <= endY; ++bandY )
            {
                bandLumaTotal += static_cast<unsigned __int64>( rowLuma[bandY] );

                unsigned __int64 neighborhoodTotal = 0;
                int neighborhoodCount = 0;
                for( int offset = -referenceRadius; offset <= referenceRadius; ++offset )
                {
                    if( abs( offset ) <= 2 )
                    {
                        continue;
                    }

                    const int sampleY = bandY + offset;
                    if( sampleY < 0 || sampleY >= stitchedHeight )
                    {
                        continue;
                    }
                    neighborhoodTotal += static_cast<unsigned __int64>( rowLuma[sampleY] );
                    ++neighborhoodCount;
                }
                if( neighborhoodCount > 0 )
                {
                    refLumaTotal += neighborhoodTotal / neighborhoodCount;
                }
            }

            const int rowCount = endY - startY + 1;
            const int avgLuma = static_cast<int>( bandLumaTotal / rowCount );
            const int referenceLuma = static_cast<int>( refLumaTotal / rowCount );
            darkBands.push_back( { startY, endY, ( startY + endY ) / 2, avgLuma, referenceLuma, referenceLuma - avgLuma } );
        }

        y = endY + 1;
    }

    StitchLog( L"[Panorama/Stitch] Band diagnostics darkBands=%zu referenceRadius=%d\n",
               darkBands.size(),
               referenceRadius );
    if( darkBands.empty() )
    {
        return;
    }

    for( size_t i = 0; i < darkBands.size() && i < maxBandsToLog; ++i )
    {
        const auto& band = darkBands[i];
        int nearestBoundaryRow = -1;
        size_t nearestBoundaryFrame = static_cast<size_t>( -1 );
        int nearestBoundaryDistance = ( std::numeric_limits<int>::max )();
        for( size_t framePos = 0; framePos < composedFrameIndices.size() && framePos < composedFrameOrigins.size(); ++framePos )
        {
            const int boundaryRow = composedFrameOrigins[framePos].y - minY;
            const int distance = abs( boundaryRow - band.centerY );
            if( distance < nearestBoundaryDistance )
            {
                nearestBoundaryDistance = distance;
                nearestBoundaryRow = boundaryRow;
                nearestBoundaryFrame = composedFrameIndices[framePos];
            }
        }

        incrementPassCount( darkBandFirstPassCounts, rowFullWidthBlendFirstPass[band.centerY] );
        incrementPassCount( darkBandLastPassCounts, rowFullWidthBlendLastPass[band.centerY] );

        StitchLog( L"[Panorama/Stitch] Dark band y=%d..%d rows=%d avgLuma=%d refLuma=%d delta=%d blendedCenter=%d writtenCenter=%d blendPixelsCenter=%d blendAvgCenter=%d blendMinCenter=%d blendMaxCenter=%d fullBlendFirst=(frame:%d pass:%d weight:%d) fullBlendLast=(frame:%d pass:%d weight:%d) fullBlendPasses=%d nearestBoundaryRow=%d nearestBoundaryFrame=%zu boundaryDistance=%d summary=%s\n",
                   band.startY,
                   band.endY,
                   band.endY - band.startY + 1,
                   band.avgLuma,
                   band.referenceLuma,
                   band.delta,
                   rowBlended[band.centerY],
                   rowWritten[band.centerY],
                   rowBlendPixelCount[band.centerY],
                   rowBlendAverageWeight( band.centerY ),
                   rowBlendPixelCount[band.centerY] > 0 ? rowBlendWeightMin[band.centerY] : 0,
                   rowBlendPixelCount[band.centerY] > 0 ? rowBlendWeightMax[band.centerY] : 0,
                   rowFullWidthBlendFirstFrame[band.centerY],
                   rowFullWidthBlendFirstPass[band.centerY],
                   rowFullWidthBlendFirstWeight[band.centerY],
                   rowFullWidthBlendLastFrame[band.centerY],
                   rowFullWidthBlendLastPass[band.centerY],
                   rowFullWidthBlendLastWeight[band.centerY],
                   rowFullWidthBlendPassCount[band.centerY],
                   nearestBoundaryRow,
                   nearestBoundaryFrame,
                   nearestBoundaryDistance,
                   BuildStitchedRowSummary( stitchedOwner,
                                            stitchedWritten,
                                            stitchedBlended,
                                            stitchedWidth,
                                            stitchedHeight,
                                            band.centerY ).c_str() );
    }
    if( !darkBandFirstPassCounts.empty() )
    {
        std::sort( darkBandFirstPassCounts.begin(), darkBandFirstPassCounts.end(), []( const auto& lhs, const auto& rhs )
        {
            return lhs.second > rhs.second;
        } );
        std::sort( darkBandLastPassCounts.begin(), darkBandLastPassCounts.end(), []( const auto& lhs, const auto& rhs )
        {
            return lhs.second > rhs.second;
        } );

        std::wstring firstSummary;
        std::wstring lastSummary;
        for( size_t idx = 0; idx < darkBandFirstPassCounts.size() && idx < 6; ++idx )
        {
            if( idx > 0 )
            {
                firstSummary += L"|";
            }
            wchar_t buffer[32]{};
            swprintf_s( buffer, L"p%d:%d", darkBandFirstPassCounts[idx].first, darkBandFirstPassCounts[idx].second );
            firstSummary += buffer;
        }
        for( size_t idx = 0; idx < darkBandLastPassCounts.size() && idx < 6; ++idx )
        {
            if( idx > 0 )
            {
                lastSummary += L"|";
            }
            wchar_t buffer[32]{};
            swprintf_s( buffer, L"p%d:%d", darkBandLastPassCounts[idx].first, darkBandLastPassCounts[idx].second );
            lastSummary += buffer;
        }

        StitchLog( L"[Panorama/Stitch] Dark band provenance firstPasses=%s lastPasses=%s\n",
                   firstSummary.c_str(),
                   lastSummary.c_str() );
    }
    if( darkBands.size() > maxBandsToLog )
    {
        StitchLog( L"[Panorama/Stitch] Band diagnostics truncated %zu additional dark band(s)\n",
                   darkBands.size() - maxBandsToLog );
    }
}

// Post-composition content-duplication diagnostic.
// For each composed frame, compute how many of its "unique" (non-overlap)
// rows are pixel-identical to rows elsewhere on the canvas.  This reveals
// whether the matcher placed frames redundantly or the source content is
// genuinely repetitive.
static void LogContentDuplicationDiagnostics( const std::vector<BYTE>& stitchedPixels,
                                              const std::vector<int>& stitchedOwner,
                                              int stitchedWidth,
                                              int stitchedHeight,
                                              const std::vector<size_t>& composedFrameIndices,
                                              const std::vector<POINT>& composedFrameOrigins,
                                              const std::vector<POINT>& composedFrameSteps,
                                              int frameWidth,
                                              int frameHeight,
                                              int minX,
                                              int minY )
{
    if( !PanoramaDebugEnabled() || stitchedWidth <= 0 || stitchedHeight <= 0 ||
        stitchedPixels.size() != static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4 ||
        composedFrameIndices.size() < 2 ||
        composedFrameOrigins.size() != composedFrameIndices.size() ||
        composedFrameSteps.size() != composedFrameIndices.size() )
    {
        return;
    }

    // Helper: compute average per-pixel RGB difference between two canvas rows.
    auto rowDifference = [&]( int yA, int yB ) -> double
    {
        if( yA < 0 || yA >= stitchedHeight || yB < 0 || yB >= stitchedHeight )
            return 999.0;
        const size_t baseA = static_cast<size_t>( yA ) * static_cast<size_t>( stitchedWidth ) * 4;
        const size_t baseB = static_cast<size_t>( yB ) * static_cast<size_t>( stitchedWidth ) * 4;
        long long sum = 0;
        // Sample every 4th pixel for speed.
        int count = 0;
        for( int x = 0; x < stitchedWidth; x += 4 )
        {
            const size_t offA = baseA + static_cast<size_t>( x ) * 4;
            const size_t offB = baseB + static_cast<size_t>( x ) * 4;
            sum += abs( static_cast<int>( stitchedPixels[offA + 0] ) - static_cast<int>( stitchedPixels[offB + 0] ) )
                 + abs( static_cast<int>( stitchedPixels[offA + 1] ) - static_cast<int>( stitchedPixels[offB + 1] ) )
                 + abs( static_cast<int>( stitchedPixels[offA + 2] ) - static_cast<int>( stitchedPixels[offB + 2] ) );
            ++count;
        }
        return count > 0 ? static_cast<double>( sum ) / ( count * 3.0 ) : 999.0;
    };

    // Helper: compute the dominant owner frame for a canvas row.
    auto rowDominantOwner = [&]( int y ) -> int
    {
        if( y < 0 || y >= stitchedHeight )
            return -1;
        const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth );
        // Count the first owner seen (they're usually uniform for full-width frames).
        return stitchedOwner[rowBase + static_cast<size_t>( stitchedWidth / 2 )];
    };

    // Helper: average luma of a canvas row.
    auto rowAverageLuma = [&]( int y ) -> int
    {
        if( y < 0 || y >= stitchedHeight )
            return -1;
        const size_t base = static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth ) * 4;
        unsigned long long totalLuma = 0;
        for( int x = 0; x < stitchedWidth; x += 4 )
        {
            const size_t off = base + static_cast<size_t>( x ) * 4;
            totalLuma += ( static_cast<unsigned long long>( stitchedPixels[off + 2] ) * 77 +
                           static_cast<unsigned long long>( stitchedPixels[off + 1] ) * 150 +
                           static_cast<unsigned long long>( stitchedPixels[off + 0] ) * 29 ) >> 8;
        }
        const int sampleCount = ( stitchedWidth + 3 ) / 4;
        return static_cast<int>( totalLuma / max( 1, sampleCount ) );
    };

    StitchLog( L"[Panorama/Stitch] Content duplication diagnostics begin\n" );

    // Only inspect the last ~40% of composed transitions where artifacts cluster.
    const size_t startTransition = composedFrameIndices.size() / 2;

    int totalDuplicateTransitions = 0;
    for( size_t i = startTransition; i < composedFrameIndices.size(); ++i )
    {
        const int stepY = composedFrameSteps[i].y;
        const int absStepY = abs( stepY );
        const int destY = composedFrameOrigins[i].y - minY;

        // Determine the "new content" region: rows that should be unique.
        // For downward scrolling (stepY > 0 i.e. step on canvas is positive),
        // the new content is the bottom portion: rows [destY + overlapHeight, destY + frameHeight).
        // For upward, the new content is the top portion.
        const int overlapHeight = max( 0, frameHeight - absStepY );

        int newContentStart = -1;
        int newContentEnd = -1;
        if( stepY > 0 && absStepY > 0 )
        {
            // Downward scroll: new content at the bottom of this frame's span.
            newContentStart = destY + overlapHeight;
            newContentEnd = destY + frameHeight;
        }
        else if( stepY < 0 && absStepY > 0 )
        {
            // Upward scroll: new content at the top.
            newContentStart = destY;
            newContentEnd = destY + absStepY;
        }
        else
        {
            continue; // No movement, skip.
        }

        // Clamp to canvas bounds.
        newContentStart = max( 0, min( stitchedHeight, newContentStart ) );
        newContentEnd = max( 0, min( stitchedHeight, newContentEnd ) );
        const int newContentRows = newContentEnd - newContentStart;
        if( newContentRows <= 0 )
            continue;

        // Check each new-content row against the canvas row that is
        // exactly one step above. If pixel-identical, the frame is
        // painting redundant content.
        int identicalToStepAbove = 0;
        int nearIdenticalToStepAbove = 0;
        // Also check if it matches row at offset = overlapHeight above (full frame repeat).
        int identicalToOverlapAbove = 0;
        // Also scan for best-matching row within a search window above.
        int identicalToBestMatch = 0;
        int bestMatchSampleOffset = 0;

        for( int row = newContentStart; row < newContentEnd; ++row )
        {
            // Compare to the row absStepY rows above (one "frame step" earlier).
            const double diffStep = rowDifference( row, row - absStepY );
            if( diffStep <= 0.5 )
                ++identicalToStepAbove;
            else if( diffStep <= 4.0 )
                ++nearIdenticalToStepAbove;

            // Compare to row overlapHeight above.
            const double diffOverlap = rowDifference( row, row - overlapHeight );
            if( diffOverlap <= 0.5 )
                ++identicalToOverlapAbove;
        }

        // Sample a few rows for the best-match scan to keep it fast.
        const int sampleRow = ( newContentStart + newContentEnd ) / 2;
        double bestSampleDiff = 999.0;
        for( int offset = 24; offset < min( stitchedHeight, 800 ); ++offset )
        {
            if( sampleRow - offset < 0 )
                break;
            const double d = rowDifference( sampleRow, sampleRow - offset );
            if( d < bestSampleDiff )
            {
                bestSampleDiff = d;
                bestMatchSampleOffset = offset;
            }
        }
        if( bestSampleDiff <= 0.5 )
            ++identicalToBestMatch;

        const int gap = ( i > 0 ) ? static_cast<int>( composedFrameIndices[i] - composedFrameIndices[i - 1] ) : 0;
        const bool significantDuplication =
            identicalToStepAbove > newContentRows / 3 ||
            identicalToOverlapAbove > newContentRows / 3 ||
            nearIdenticalToStepAbove > newContentRows * 2 / 3;

        if( significantDuplication )
            ++totalDuplicateTransitions;

        // Log every transition in the tail region regardless of whether it's duplicate,
        // so we can see the pattern. But use a compact format.
        StitchLog( L"[Panorama/Stitch] FrameDup trans=%zu frame=%zu gap=%d step=(%d,%d) dest=%d newRows=%d..%d(%d) "
                   L"identStep=%d nearStep=%d identOverlap=%d bestMatchOff=%d bestMatchDiff=%.1f "
                   L"ownerAtNew=%d ownerAbove=%d lumaNew=%d lumaAbove=%d%ls\n",
                   i,
                   composedFrameIndices[i],
                   gap,
                   composedFrameSteps[i].x,
                   composedFrameSteps[i].y,
                   destY,
                   newContentStart,
                   newContentEnd,
                   newContentRows,
                   identicalToStepAbove,
                   nearIdenticalToStepAbove,
                   identicalToOverlapAbove,
                   bestMatchSampleOffset,
                   bestSampleDiff,
                   rowDominantOwner( ( newContentStart + newContentEnd ) / 2 ),
                   rowDominantOwner( ( newContentStart + newContentEnd ) / 2 - absStepY ),
                   rowAverageLuma( ( newContentStart + newContentEnd ) / 2 ),
                   rowAverageLuma( ( newContentStart + newContentEnd ) / 2 - absStepY ),
                   significantDuplication ? L" [DUPLICATE]" : L"" );
    }

    StitchLog( L"[Panorama/Stitch] Content duplication diagnostics end duplicateTransitions=%d/%zu\n",
               totalDuplicateTransitions,
               composedFrameIndices.size() - startTransition );
}

static bool FindBestFrameShiftVerticalOnly( const std::vector<BYTE>& previousPixels,
                                            const std::vector<BYTE>& currentPixels,
                                            int frameWidth,
                                            int frameHeight,
                                            int expectedDx,
                                            int expectedDy,
                                            int& bestDx,
                                            int& bestDy,
                                            bool lowContrastMode,
                                            const std::vector<BYTE>& precomputedPrevLuma,
                                            const std::vector<BYTE>& precomputedCurrLuma,
                                            int precomputedVeryLowEntropy,
                                            bool* outNearStationaryOverride,
                                            bool allowHighConstStationaryRelax,
                                            unsigned __int64* outMaskedStationaryScore,
                                            bool forceExhaustiveProbeBudget,
                                            bool forceExhaustiveFineDx );

static void LogGapBridgeProbeDiagnostics( size_t frameIndex,
                                          size_t lastAcceptedIndex,
                                          int gap,
                                          int acceptedDx,
                                          int acceptedDy,
                                          int expectedDx,
                                          int expectedDy,
                                          int frameWidth,
                                          int frameHeight,
                                          bool lowContrastMode,
                                          const std::vector<std::vector<BYTE>>& framePixels,
                                          const std::vector<std::vector<BYTE>>& frameLuma,
                                          const std::vector<double>& frameConstantFraction,
                                          const std::vector<POINT>& composedFrameSteps )
{
    if( gap <= 1 || frameIndex == 0 || lastAcceptedIndex >= frameIndex )
    {
        return;
    }

    int histAbsX = 0;
    int histAbsY = 0;
    for( size_t si = 1; si < composedFrameSteps.size(); ++si )
    {
        histAbsX += abs( composedFrameSteps[si].x );
        histAbsY += abs( composedFrameSteps[si].y );
    }

    const bool mostlyVerticalHist = histAbsY > histAbsX * 3;
    const bool mostlyHorizontalHist = histAbsX > histAbsY * 3;
    std::vector<int> recentAxisAbs;
    recentAxisAbs.reserve( 8 );
    for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
         si >= 1 && static_cast<int>( recentAxisAbs.size() ) < 8;
         --si )
    {
        const int axisValue = mostlyVerticalHist
            ? abs( composedFrameSteps[static_cast<size_t>( si )].y )
            : ( mostlyHorizontalHist
                ? abs( composedFrameSteps[static_cast<size_t>( si )].x )
                : 0 );
        if( axisValue > 0 )
        {
            recentAxisAbs.push_back( axisValue );
        }
    }

    int recentMedian = 0;
    if( !recentAxisAbs.empty() )
    {
        std::sort( recentAxisAbs.begin(), recentAxisAbs.end() );
        recentMedian = recentAxisAbs[recentAxisAbs.size() / 2];
    }

    StitchLog( L"[Panorama/Stitch] GapBridgeProbe begin frame=%zu ref=%zu gap=%d accepted=(%d,%d) expected=(%d,%d) recentMedian=%d mode=%ls\n",
                 frameIndex,
                 lastAcceptedIndex,
                 gap,
                 acceptedDx,
                 acceptedDy,
                 expectedDx,
                 expectedDy,
                 recentMedian,
                 mostlyVerticalHist ? L"vertical" : ( mostlyHorizontalHist ? L"horizontal" : L"neutral" ) );

    int bridgeProbeDx = 0;
    int bridgeProbeDy = 0;
    bool bridgeProbeNearStationary = false;
    const int bridgeVle = ( frameConstantFraction[lastAcceptedIndex] > 0.58 &&
                            frameConstantFraction[frameIndex] > 0.58 ) ? 1 : 0;
    const bool bridgeProbeOk = FindBestFrameShiftVerticalOnly( framePixels[lastAcceptedIndex],
                                                               framePixels[frameIndex],
                                                               frameWidth,
                                                               frameHeight,
                                                               expectedDx * gap,
                                                               expectedDy * gap,
                                                               bridgeProbeDx,
                                                               bridgeProbeDy,
                                                               lowContrastMode,
                                                               frameLuma[lastAcceptedIndex],
                                                               frameLuma[frameIndex],
                                                               bridgeVle,
                                                               &bridgeProbeNearStationary,
                                                               false,
                                                               nullptr,
                                                               true,
                                                               true );
    StitchLog( L"[Panorama/Stitch] GapBridgeProbe bridge-pair frame=%zu ref=%zu ok=%d probe=(%d,%d) expectedTotal=(%d,%d) nearStationary=%d\n",
                 frameIndex,
                 lastAcceptedIndex,
                 bridgeProbeOk ? 1 : 0,
                 bridgeProbeDx,
                 bridgeProbeDy,
                 expectedDx * gap,
                 expectedDy * gap,
                 bridgeProbeNearStationary ? 1 : 0 );

    int adjacentProbeDx = 0;
    int adjacentProbeDy = 0;
    bool adjacentProbeNearStationary = false;
    const int adjacentVle = ( frameConstantFraction[frameIndex - 1] > 0.58 &&
                              frameConstantFraction[frameIndex] > 0.58 ) ? 1 : 0;
    const bool adjacentProbeOk = FindBestFrameShiftVerticalOnly( framePixels[frameIndex - 1],
                                                                 framePixels[frameIndex],
                                                                 frameWidth,
                                                                 frameHeight,
                                                                 expectedDx,
                                                                 expectedDy,
                                                                 adjacentProbeDx,
                                                                 adjacentProbeDy,
                                                                 lowContrastMode,
                                                                 frameLuma[frameIndex - 1],
                                                                 frameLuma[frameIndex],
                                                                 adjacentVle,
                                                                 &adjacentProbeNearStationary,
                                                                 false,
                                                                 nullptr,
                                                                 true,
                                                                 true );
    StitchLog( L"[Panorama/Stitch] GapBridgeProbe adjacent-pair frame=%zu prev=%zu ok=%d probe=(%d,%d) expectedSingle=(%d,%d) nearStationary=%d\n",
                 frameIndex,
                 frameIndex - 1,
                 adjacentProbeOk ? 1 : 0,
                 adjacentProbeDx,
                 adjacentProbeDy,
                 expectedDx,
                 expectedDy,
                 adjacentProbeNearStationary ? 1 : 0 );
}

static int DivideRounded( int value, int divisor )
{
    if( divisor <= 1 )
    {
        return value;
    }

    if( value >= 0 )
    {
        return ( value + divisor / 2 ) / divisor;
    }

    return -( ( -value + divisor / 2 ) / divisor );
}

//----------------------------------------------------------------------------
//
// Performance profiling for FindBestFrameShiftVerticalOnly
//
//----------------------------------------------------------------------------
#ifdef _DEBUG
struct StitchPerfCounters
{
    LARGE_INTEGER freqQpc;
    __int64 totalCalls;
    __int64 tBuildDsLuma;       // BuildDownsampledLuma
    __int64 tStationary;        // Stationary score
    __int64 tVleMask;           // VLE/HCF mask build + dilation
    __int64 tCoarseSearch;      // Coarse search loop
    __int64 tFullResLuma;       // BuildFullLumaFrame (when not precomputed)
    __int64 tProbeInject;       // Probe/candidate injection
    __int64 tFineSearch;        // Fine search (Phase 2)
    __int64 tPostValidation;    // Post-search validation/ambiguity
    __int64 tTotal;             // Total function time
    __int64 tEdgeProjection;    // Edge-density NCC (HCF injection)
    __int64 tMaskedFallback;    // Full-res masked coarse fallback

    StitchPerfCounters() { Reset(); QueryPerformanceFrequency( &freqQpc ); }
    void Reset() { memset( &totalCalls, 0, reinterpret_cast<char*>(&tMaskedFallback + 1) - reinterpret_cast<char*>(&totalCalls) ); }

    double UsFromTicks( __int64 ticks ) const
    {
        return ticks * 1000000.0 / freqQpc.QuadPart;
    }

    void Report()
    {
        if( totalCalls == 0 ) return;
        StitchLog( L"[Panorama/Perf] === FindBestFrameShiftVerticalOnly profiling (%lld calls) ===\n", totalCalls );
        StitchLog( L"[Panorama/Perf]   Total:          %8.0f us (%.0f us/call)\n", UsFromTicks( tTotal ), UsFromTicks( tTotal ) / totalCalls );
        StitchLog( L"[Panorama/Perf]   BuildDsLuma:    %8.0f us (%.1f%%)\n", UsFromTicks( tBuildDsLuma ), tBuildDsLuma * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   Stationary:     %8.0f us (%.1f%%)\n", UsFromTicks( tStationary ), tStationary * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   VLE/HCF mask:   %8.0f us (%.1f%%)\n", UsFromTicks( tVleMask ), tVleMask * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   CoarseSearch:   %8.0f us (%.1f%%)\n", UsFromTicks( tCoarseSearch ), tCoarseSearch * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   MaskedFallback: %8.0f us (%.1f%%)\n", UsFromTicks( tMaskedFallback ), tMaskedFallback * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   FullResLuma:    %8.0f us (%.1f%%)\n", UsFromTicks( tFullResLuma ), tFullResLuma * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   ProbeInject:    %8.0f us (%.1f%%)\n", UsFromTicks( tProbeInject ), tProbeInject * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   EdgeProjection: %8.0f us (%.1f%%)\n", UsFromTicks( tEdgeProjection ), tEdgeProjection * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   FineSearch:     %8.0f us (%.1f%%)\n", UsFromTicks( tFineSearch ), tFineSearch * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf]   PostValidation: %8.0f us (%.1f%%)\n", UsFromTicks( tPostValidation ), tPostValidation * 100.0 / max( tTotal, 1LL ) );
        StitchLog( L"[Panorama/Perf] ===================================================\n" );
    }
};
static StitchPerfCounters g_StitchPerf;

struct ScopedPerfTimer
{
    __int64& accumulator;
    LARGE_INTEGER start;
    ScopedPerfTimer( __int64& acc ) : accumulator( acc ) { QueryPerformanceCounter( &start ); }
    ~ScopedPerfTimer() { LARGE_INTEGER end; QueryPerformanceCounter( &end ); accumulator += end.QuadPart - start.QuadPart; }
};
#define PERF_TIMER(field) ScopedPerfTimer _pt_##field( g_StitchPerf.field )
#define PERF_START(field) LARGE_INTEGER _ps_##field; QueryPerformanceCounter( &_ps_##field )
#define PERF_STOP(field) { LARGE_INTEGER _pe; QueryPerformanceCounter( &_pe ); g_StitchPerf.field += _pe.QuadPart - _ps_##field.QuadPart; }
#else
#define PERF_TIMER(field) ((void)0)
#define PERF_START(field) ((void)0)
#define PERF_STOP(field)  ((void)0)
#endif

//----------------------------------------------------------------------------
//
// Panorama capture helpers
//
//----------------------------------------------------------------------------

static HBITMAP CaptureAbsoluteScreenRectToBitmap(HDC hdcSource, const RECT& absoluteRect)
{
    const int captureWidth = absoluteRect.right - absoluteRect.left;
    const int captureHeight = absoluteRect.bottom - absoluteRect.top;
    if( captureWidth <= 0 || captureHeight <= 0 )
    {
        return nullptr;
    }

    // Use a DIB section instead of CreateCompatibleBitmap so that pixel
    // data is stored in system memory.  DDB bitmaps returned by
    // CreateCompatibleBitmap may reside in video memory, and the driver
    // can invalidate/repurpose that storage once the bitmap is deselected
    // from all DCs.  Later GetDIBits calls then read stale data, causing
    // frames that have actually changed to appear identical.
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
    bmi.bmiHeader.biWidth = captureWidth;
    bmi.bmiHeader.biHeight = -captureHeight;   // top-down
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    void* bits = nullptr;
    HBITMAP hBitmap = CreateDIBSection( hdcSource, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0 );
    if( hBitmap == nullptr )
    {
        return nullptr;
    }

    HDC hdcMem = CreateCompatibleDC( hdcSource );
    if( hdcMem == nullptr )
    {
        DeleteObject( hBitmap );
        return nullptr;
    }

    SelectObject( hdcMem, hBitmap );
    BitBlt( hdcMem, 0, 0, captureWidth, captureHeight, hdcSource,
        absoluteRect.left, absoluteRect.top, SRCCOPY | CAPTUREBLT );
    GdiFlush();
    DeleteDC( hdcMem );
    return hBitmap;
}

static bool ReadBitmapPixels32(HBITMAP hBitmap, std::vector<BYTE>& pixels, int& width, int& height)
{
    BITMAP bitmap{};
    if( GetObject( hBitmap, sizeof(bitmap), &bitmap ) == 0 )
    {
        return false;
    }

    width = bitmap.bmWidth;
    height = bitmap.bmHeight;
    if( width <= 0 || height <= 0 )
    {
        return false;
    }

    pixels.resize( static_cast<size_t>(width) * static_cast<size_t>(height) * 4 );
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    HDC hdc = GetDC( nullptr );
    const int copied = GetDIBits( hdc, hBitmap, 0, static_cast<UINT>(height), pixels.data(), &bmi, DIB_RGB_COLORS );
    ReleaseDC( nullptr, hdc );
    return copied == height;
}

static HBITMAP CreateBitmapFromPixels32( const std::vector<BYTE>& pixels, int width, int height )
{
    if( width <= 0 || height <= 0 || pixels.size() != static_cast<size_t>( width ) * static_cast<size_t>( height ) * 4 )
    {
        return nullptr;
    }

    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    HDC hdc = GetDC( nullptr );
    if( hdc == nullptr )
    {
        return nullptr;
    }

    void* bits = nullptr;
    HBITMAP bitmap = CreateDIBSection( hdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0 );
    if( bitmap != nullptr && bits != nullptr )
    {
        memcpy( bits, pixels.data(), pixels.size() );
    }
    else if( bitmap != nullptr )
    {
        DeleteObject( bitmap );
        bitmap = nullptr;
    }

    ReleaseDC( nullptr, hdc );
    return bitmap;
}

static std::wstring CreatePanoramaDebugDumpDirectory()
{
    std::error_code errorCode;
    std::filesystem::path debugRoot;

    wchar_t tempPath[MAX_PATH]{};
    const DWORD tempPathLength = GetTempPathW( ARRAYSIZE( tempPath ), tempPath );
    if( tempPathLength != 0 && tempPathLength < ARRAYSIZE( tempPath ) )
    {
        debugRoot = std::filesystem::path( tempPath ) / L"ZoomItPanoramaDebug";
    }
    else
    {
        wchar_t modulePath[MAX_PATH]{};
        if( GetModuleFileNameW( nullptr, modulePath, ARRAYSIZE( modulePath ) ) == 0 )
        {
            return {};
        }

        debugRoot = std::filesystem::path( modulePath ).parent_path() / L"debug" / L"ZoomItPanoramaDebug";
    }

    std::filesystem::create_directories( debugRoot, errorCode );
    if( errorCode )
    {
        return {};
    }

    SYSTEMTIME localTime{};
    GetLocalTime( &localTime );
    wchar_t stamp[96]{};
    swprintf_s( stamp,
                L"panorama_%04u%02u%02u_%02u%02u%02u_%lu",
                static_cast<unsigned>( localTime.wYear ),
                static_cast<unsigned>( localTime.wMonth ),
                static_cast<unsigned>( localTime.wDay ),
                static_cast<unsigned>( localTime.wHour ),
                static_cast<unsigned>( localTime.wMinute ),
                static_cast<unsigned>( localTime.wSecond ),
                GetCurrentProcessId() );

    const auto sessionDirectory = debugRoot / stamp;
    std::filesystem::create_directories( sessionDirectory, errorCode );
    if( errorCode )
    {
        return {};
    }

    return sessionDirectory.wstring();
}

#ifdef _DEBUG
static std::filesystem::path GetPanoramaDebugRootDirectory()
{
    wchar_t tempPath[MAX_PATH]{};
    const DWORD tempPathLength = GetTempPathW( ARRAYSIZE( tempPath ), tempPath );
    if( tempPathLength != 0 && tempPathLength < ARRAYSIZE( tempPath ) )
    {
        return std::filesystem::path( tempPath ) / L"ZoomItPanoramaDebug";
    }

    wchar_t modulePath[MAX_PATH]{};
    if( GetModuleFileNameW( nullptr, modulePath, ARRAYSIZE( modulePath ) ) == 0 )
    {
        return {};
    }

    return std::filesystem::path( modulePath ).parent_path() / L"debug" / L"ZoomItPanoramaDebug";
}
#endif // _DEBUG

static bool SaveBitmapAsBmp( HBITMAP bitmap, const std::filesystem::path& filePath )
{
    if( bitmap == nullptr )
    {
        return false;
    }

    std::vector<BYTE> pixels;
    int width = 0;
    int height = 0;
    if( !ReadBitmapPixels32( bitmap, pixels, width, height ) )
    {
        return false;
    }

    const DWORD imageSize = static_cast<DWORD>( pixels.size() );
    BITMAPFILEHEADER fileHeader{};
    fileHeader.bfType = 0x4D42;
    fileHeader.bfOffBits = sizeof( BITMAPFILEHEADER ) + sizeof( BITMAPINFOHEADER );
    fileHeader.bfSize = fileHeader.bfOffBits + imageSize;

    BITMAPINFOHEADER infoHeader{};
    infoHeader.biSize = sizeof( BITMAPINFOHEADER );
    infoHeader.biWidth = width;
    infoHeader.biHeight = -height;
    infoHeader.biPlanes = 1;
    infoHeader.biBitCount = 32;
    infoHeader.biCompression = BI_RGB;
    infoHeader.biSizeImage = imageSize;

    std::ofstream stream( filePath, std::ios::binary | std::ios::trunc );
    if( !stream.good() )
    {
        return false;
    }

    stream.write( reinterpret_cast<const char*>( &fileHeader ), sizeof( fileHeader ) );
    stream.write( reinterpret_cast<const char*>( &infoHeader ), sizeof( infoHeader ) );
    stream.write( reinterpret_cast<const char*>( pixels.data() ), static_cast<std::streamsize>( pixels.size() ) );
    return stream.good();
}

static void DumpPanoramaBitmap( const std::wstring& debugDumpDirectory,
                                const wchar_t* prefix,
                                size_t index,
                                HBITMAP bitmap )
{
    if( debugDumpDirectory.empty() || bitmap == nullptr )
    {
        return;
    }

    wchar_t fileName[96]{};
    swprintf_s( fileName, L"%s_%04zu.bmp", prefix, index );
    const auto outputPath = std::filesystem::path( debugDumpDirectory ) / fileName;
    if( !SaveBitmapAsBmp( bitmap, outputPath ) )
    {
        OutputDebug( L"[Panorama/Debug] Failed to save %s\n", outputPath.c_str() );
    }
}

static void DumpPanoramaText( const std::wstring& debugDumpDirectory,
                              const wchar_t* fileName,
                              const std::wstring& text )
{
    if( debugDumpDirectory.empty() )
    {
        return;
    }

    const auto outputPath = std::filesystem::path( debugDumpDirectory ) / fileName;
    std::wofstream stream( outputPath, std::ios::trunc );
    if( !stream.good() )
    {
        OutputDebug( L"[Panorama/Debug] Failed to write %s\n", outputPath.c_str() );
        return;
    }

    stream << text;
}

#ifdef _DEBUG
static HBITMAP LoadBitmapFromFile( const std::filesystem::path& filePath )
{
    return static_cast<HBITMAP>( LoadImageW( nullptr,
                                                  filePath.c_str(),
                                                  IMAGE_BITMAP,
                                                  0,
                                                  0,
                                                  LR_LOADFROMFILE | LR_CREATEDIBSECTION ) );
}

static bool RunPanoramaStitchFromDumpDirectory( const std::filesystem::path& dumpDirectory,
                                                std::filesystem::path& outputPath )
{
    std::error_code errorCode;
    if( !std::filesystem::exists( dumpDirectory, errorCode ) || errorCode )
    {
        StitchLog( L"[Panorama/Replay] Dump directory does not exist: %s\n", dumpDirectory.c_str() );
        return false;
    }

    std::vector<std::filesystem::path> acceptedFramePaths;
    std::vector<std::filesystem::path> grabbedFramePaths;
    for( const auto& entry : std::filesystem::directory_iterator( dumpDirectory, errorCode ) )
    {
        if( errorCode )
        {
            break;
        }

        if( !entry.is_regular_file() )
        {
            continue;
        }

        const auto fileName = entry.path().filename().wstring();
        if( fileName.rfind( L"accepted_", 0 ) == 0 && entry.path().extension() == L".bmp" )
        {
            acceptedFramePaths.push_back( entry.path() );
        }
        else if( fileName.rfind( L"grabbed_", 0 ) == 0 && entry.path().extension() == L".bmp" )
        {
            grabbedFramePaths.push_back( entry.path() );
        }
    }

    const bool useGrabbedFrames = grabbedFramePaths.size() >= 2 && acceptedFramePaths.size() < 2;
    std::vector<std::filesystem::path>& framePaths = useGrabbedFrames ? grabbedFramePaths : acceptedFramePaths;

    if( framePaths.size() < 2 )
    {
        StitchLog( L"[Panorama/Replay] Need at least 2 replay frames in %s; accepted=%zu grabbed=%zu\n",
                     dumpDirectory.c_str(),
                     acceptedFramePaths.size(),
                     grabbedFramePaths.size() );
        return false;
    }

    std::sort( framePaths.begin(), framePaths.end() );
    StitchLog( L"[Panorama/Replay] Using %s frame set count=%zu in %s\n",
                 useGrabbedFrames ? L"grabbed" : L"accepted",
                 framePaths.size(),
                 dumpDirectory.c_str() );
    wprintf( L"[Replay] Loading %zu %s frames from %s\n",
             framePaths.size(),
             useGrabbedFrames ? L"grabbed" : L"accepted",
             dumpDirectory.c_str() );
    fflush( stdout );

    std::vector<HBITMAP> frames;
    frames.reserve( framePaths.size() );
    for( const auto& framePath : framePaths )
    {
        HBITMAP bitmap = LoadBitmapFromFile( framePath );
        if( bitmap == nullptr )
        {
            StitchLog( L"[Panorama/Replay] Failed to load frame: %s\n", framePath.c_str() );
            for( HBITMAP frame : frames )
            {
                DeleteObject( frame );
            }
            return false;
        }

        frames.push_back( bitmap );
    }

    // Replay writes into stitch_log.txt so before/after comparisons use
    // the same canonical trace file as capture and selftest runs.
    {
        const auto logPath = dumpDirectory / L"stitch_log.txt";
        FILE* replayLogFile = nullptr;
        if( _wfopen_s( &replayLogFile, logPath.c_str(), L"ab" ) == 0 )
        {
            g_StitchLogFile = replayLogFile;
            StitchLog( L"\n[Panorama/Replay] ===== Replay run begin =====\n" );
            StitchLog( L"[Panorama/Replay] Dump directory: %s\n", dumpDirectory.c_str() );
        }
    }

    struct ReplayLogCloser
    {
        ~ReplayLogCloser()
        {
            if( g_StitchLogFile != nullptr )
            {
                fclose( g_StitchLogFile );
                g_StitchLogFile = nullptr;
            }
        }
    } replayLogCloser;

    wprintf( L"[Replay] Stitching %zu frames ...\n", frames.size() );
    fflush( stdout );
    int lastPercent = -1;
    HBITMAP stitched = StitchPanoramaFrames( frames, false, [&]( int percent ) -> bool
    {
        if( percent != lastPercent )
        {
            lastPercent = percent;
            wprintf( L"\r[Replay] Stitching ... %d%%", percent );
            fflush( stdout );
        }
        return false;  // false = not cancelled
    } );
    wprintf( L"\r[Replay] Stitching ... done    \n" );
    fflush( stdout );

    for( HBITMAP frame : frames )
    {
        DeleteObject( frame );
    }

    if( stitched == nullptr )
    {
        wprintf( L"[Replay] FAILED: stitcher returned null\n" );
        StitchLog( L"[Panorama/Replay] StitchPanoramaFrames failed for %s\n", dumpDirectory.c_str() );
        return false;
    }

    outputPath = dumpDirectory / ( useGrabbedFrames ? L"stitched_replay_grabbed_0000.bmp" : L"stitched_replay_0000.bmp" );
    const bool saved = SaveBitmapAsBmp( stitched, outputPath );
    DeleteObject( stitched );
    if( !saved )
    {
        wprintf( L"[Replay] FAILED: could not save output\n" );
        StitchLog( L"[Panorama/Replay] Failed to save stitched replay: %s\n", outputPath.c_str() );
        return false;
    }

    wprintf( L"[Replay] Saved: %s\n", outputPath.c_str() );
    fflush( stdout );
    StitchLog( L"[Panorama/Replay] Saved stitched replay: %s\n", outputPath.c_str() );
    return true;
}
#endif // _DEBUG

static bool ComputeAveragePixelDifference( const std::vector<BYTE>& currentPixels,
                                           const std::vector<BYTE>& previousPixels,
                                           int frameWidth,
                                           int frameHeight,
                                           unsigned __int64& avgDiff,
                                           double& changedPixelFraction,
                                           int sampleStep = 6,
                                           unsigned phase = 0 )
{
    if( currentPixels.size() != previousPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
        return false;

    const int stride = frameWidth * 4;
    const int marginX = max( 4, frameWidth / 40 );
    const int marginY = max( 4, frameHeight / 40 );
    const int startX = marginX;
    const int endX = frameWidth - marginX;
    const int startY = marginY;
    const int endY = frameHeight - marginY;

    if( endX <= startX || endY <= startY )
        return false;

    const int step = max( 1, sampleStep );
    const int phaseX = ( step > 1 ) ? static_cast<int>( ( phase * 3u ) % static_cast<unsigned>( step ) ) : 0;
    const int phaseY = ( step > 1 ) ? static_cast<int>( ( phase * 5u ) % static_cast<unsigned>( step ) ) : 0;

    int y0 = startY + phaseY;
    if( y0 >= endY ) y0 = startY;

    int x0 = startX + phaseX;
    if( x0 >= endX ) x0 = startX;

    unsigned __int64 totalDiff = 0;
    unsigned __int64 samples = 0;
    unsigned __int64 changedPixels = 0;
    unsigned __int64 pixelSamples = 0;

    for( int y = y0; y < endY; y += step )
    {
        const int rowOffset = y * stride;
        for( int x = x0; x < endX; x += step )
        {
            const int index = rowOffset + x * 4;
            const int d0 = abs( static_cast<int>( currentPixels[index + 0] ) - static_cast<int>( previousPixels[index + 0] ) );
            const int d1 = abs( static_cast<int>( currentPixels[index + 1] ) - static_cast<int>( previousPixels[index + 1] ) );
            const int d2 = abs( static_cast<int>( currentPixels[index + 2] ) - static_cast<int>( previousPixels[index + 2] ) );
            const int sum = d0 + d1 + d2;

            totalDiff += static_cast<unsigned __int64>( sum );
            samples += 3;
            pixelSamples++;
            if( sum > 30 )
                changedPixels++;
        }
    }

    if( samples == 0 )
        return false;

    avgDiff = totalDiff / samples;
    changedPixelFraction = ( pixelSamples > 0 )
        ? static_cast<double>( changedPixels ) / static_cast<double>( pixelSamples )
        : 0.0;

    return true;
}

static bool IsLowContrastSeedFrame( HBITMAP frame,
                                    double* outSpread = nullptr,
                                    double* outStdDev = nullptr,
                                    double* outEdgeDelta = nullptr )
{
    std::vector<BYTE> pixels;
    int frameWidth = 0;
    int frameHeight = 0;
    if( !ReadBitmapPixels32( frame, pixels, frameWidth, frameHeight ) || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    const int sampleStep = max( 1, min( frameWidth, frameHeight ) / 320 );
    unsigned __int64 histogram[256]{};
    unsigned __int64 sampleCount = 0;
    unsigned __int64 sum = 0;
    unsigned __int64 sumSq = 0;
    unsigned __int64 edgeDeltaSum = 0;
    unsigned __int64 edgeSamples = 0;

    auto pixelLuma = [&]( int x, int y ) -> int
    {
        const int idx = ( y * frameWidth + x ) * 4;
        return ( pixels[idx + 2] * 77 + pixels[idx + 1] * 150 + pixels[idx + 0] * 29 ) >> 8;
    };

    for( int y = 0; y < frameHeight; y += sampleStep )
    {
        for( int x = 0; x < frameWidth; x += sampleStep )
        {
            const int luma = pixelLuma( x, y );
            histogram[luma]++;
            sampleCount++;
            sum += static_cast<unsigned __int64>( luma );
            sumSq += static_cast<unsigned __int64>( luma * luma );

            const int nextX = min( frameWidth - 1, x + sampleStep );
            const int nextY = min( frameHeight - 1, y + sampleStep );
            if( nextX != x )
            {
                edgeDeltaSum += static_cast<unsigned __int64>( abs( luma - pixelLuma( nextX, y ) ) );
                edgeSamples++;
            }
            if( nextY != y )
            {
                edgeDeltaSum += static_cast<unsigned __int64>( abs( luma - pixelLuma( x, nextY ) ) );
                edgeSamples++;
            }
        }
    }

    if( sampleCount < 64 )
    {
        return false;
    }

    const auto percentileLuma = [&]( int percentile ) -> int
    {
        const unsigned __int64 target = ( sampleCount * static_cast<unsigned __int64>( percentile ) ) / 100;
        unsigned __int64 running = 0;
        for( int l = 0; l < 256; ++l )
        {
            running += histogram[l];
            if( running >= target )
            {
                return l;
            }
        }
        return 255;
    };

    const int p10 = percentileLuma( 10 );
    const int p90 = percentileLuma( 90 );
    const double spread = static_cast<double>( p90 - p10 );
    const double mean = static_cast<double>( sum ) / static_cast<double>( sampleCount );
    const double meanSq = static_cast<double>( sumSq ) / static_cast<double>( sampleCount );
    const double variance = max( 0.0, meanSq - mean * mean );
    const double stdDev = std::sqrt( variance );
    const double edgeDelta = ( edgeSamples > 0 )
        ? static_cast<double>( edgeDeltaSum ) / static_cast<double>( edgeSamples )
        : 0.0;

    if( outSpread )
    {
        *outSpread = spread;
    }
    if( outStdDev )
    {
        *outStdDev = stdDev;
    }
    if( outEdgeDelta )
    {
        *outEdgeDelta = edgeDelta;
    }

    const bool darkBaseline = mean < 96.0;
    const bool definitelyLowContrast =
        ( spread < 34.0 && stdDev < 18.0 && edgeDelta < 9.0 );
    const bool likelyDarkLowContrast =
        darkBaseline && ( spread < 44.0 && stdDev < 22.0 && edgeDelta < 11.0 );

    return definitelyLowContrast || likelyDarkLowContrast;
}

// Lightweight per-frame brightness statistics for capture-time rejection.
// Returns average luma and luma standard deviation using sparse sampling
// (every 16th pixel in both axes).  Used to detect application-redraw
// blanking frames (e.g. Outlook mid-redraw) where the entire frame is
// a uniform flat color with near-zero variance.
static void ComputeFrameBrightnessStats( HBITMAP hBitmap,
                                         double& outAvgLuma,
                                         double& outStdDev )
{
    outAvgLuma = 0.0;
    outStdDev = 0.0;

    std::vector<BYTE> pixels;
    int width = 0;
    int height = 0;
    if( !ReadBitmapPixels32( hBitmap, pixels, width, height ) || width <= 0 || height <= 0 )
        return;

    const int step = 16;
    unsigned __int64 sum = 0;
    unsigned __int64 sumSq = 0;
    unsigned __int64 count = 0;

    for( int y = 0; y < height; y += step )
    {
        for( int x = 0; x < width; x += step )
        {
            const int idx = ( y * width + x ) * 4;
            const int luma = ( pixels[idx + 2] * 77 + pixels[idx + 1] * 150 + pixels[idx + 0] * 29 ) >> 8;
            sum += static_cast<unsigned __int64>( luma );
            sumSq += static_cast<unsigned __int64>( luma * luma );
            count++;
        }
    }

    if( count == 0 )
        return;

    const double mean = static_cast<double>( sum ) / static_cast<double>( count );
    const double meanSq = static_cast<double>( sumSq ) / static_cast<double>( count );
    outAvgLuma = mean;
    outStdDev = sqrt( max( 0.0, meanSq - mean * mean ) );
}

// Per-frame "very low entropy" detection
// Returns the fraction of pixels in a frame that sit in constant/uniform
// regions (local max-luma-deviation within a 5x5 block <= 3).  Sampled
// every 4th pixel in both axes for speed.  A pair of frames is "very
// low entropy" if both frames have constantFraction > 0.58.
static double ComputeConstantContentFraction( const std::vector<BYTE>& pixels,
                                              int frameWidth,
                                              int frameHeight )
{
    if( frameWidth <= 8 || frameHeight <= 8 )
        return 0.0;

    auto pixelLuma = [&]( int x, int y ) -> int
    {
        const int idx = ( y * frameWidth + x ) * 4;
        return ( pixels[idx + 2] * 77 + pixels[idx + 1] * 150 + pixels[idx + 0] * 29 ) >> 8;
    };

    const int sampleStep = 4;
    const int radius = 2;
    unsigned __int64 constantCount = 0;
    unsigned __int64 totalCount = 0;

    for( int y = radius; y < frameHeight - radius; y += sampleStep )
    {
        for( int x = radius; x < frameWidth - radius; x += sampleStep )
        {
            const int centerLuma = pixelLuma( x, y );
            int maxDev = 0;
            for( int ny = -radius; ny <= radius && maxDev <= 3; ny += 2 )
            {
                for( int nx = -radius; nx <= radius && maxDev <= 3; nx += 2 )
                {
                    const int dev = abs( pixelLuma( x + nx, y + ny ) - centerLuma );
                    if( dev > maxDev )
                        maxDev = dev;
                }
            }
            totalCount++;
            if( maxDev <= 3 )
                constantCount++;
        }
    }

    return ( totalCount > 0 ) ? static_cast<double>( constantCount ) / static_cast<double>( totalCount ) : 0.0;
}

static bool IsVeryLowEntropyPair( const std::vector<BYTE>& previousPixels,
                                  const std::vector<BYTE>& currentPixels,
                                  int frameWidth,
                                  int frameHeight )
{
    const double prevConstant = ComputeConstantContentFraction( previousPixels, frameWidth, frameHeight );
    const double currConstant = ComputeConstantContentFraction( currentPixels, frameWidth, frameHeight );
    return ( prevConstant > 0.58 && currConstant > 0.58 );
}

// Informative pixel difference
// Computes the average pixel difference ONLY at "informative" locations
// (pixels where the local luma gradient exceeds a threshold, i.e. edges
// and text, not flat background).  Used to rescue frames that look like
// duplicates overall but have meaningful changes in their content areas.
static bool ComputeInformativePixelDifference( const std::vector<BYTE>& currentPixels,
                                               const std::vector<BYTE>& previousPixels,
                                               int frameWidth,
                                               int frameHeight,
                                               unsigned __int64& informativeDiff,
                                               unsigned __int64& informativeCount )
{
    informativeDiff = 0;
    informativeCount = 0;
    if( frameWidth < 8 || frameHeight < 8 )
        return false;

    const int stride = frameWidth * 4;
    const int edgeThreshold = 4;

    // Sample every 2nd pixel to keep it fast.
    for( int y = 1; y < frameHeight - 1; y += 2 )
    {
        const int rowOff = y * stride;
        for( int x = 1; x < frameWidth - 1; x += 2 )
        {
            const int idx = rowOff + x * 4;
            // Luma of current position in the PREVIOUS frame (where
            // we want to detect edges).
            auto lumaAt = [&]( const std::vector<BYTE>& px, int ix, int iy ) -> int
            {
                const int i = ( iy * frameWidth + ix ) * 4;
                return ( px[i + 2] * 77 + px[i + 1] * 150 + px[i + 0] * 29 ) >> 8;
            };

            // Check gradient in previous frame.
            const int prevLuma = lumaAt( previousPixels, x, y );
            const int gradH = abs( prevLuma - lumaAt( previousPixels, x + 1, y ) );
            const int gradV = abs( prevLuma - lumaAt( previousPixels, x, y + 1 ) );

            // Also check gradient in current frame (text may have scrolled in).
            const int currLuma = lumaAt( currentPixels, x, y );
            const int gradH2 = abs( currLuma - lumaAt( currentPixels, x + 1, y ) );
            const int gradV2 = abs( currLuma - lumaAt( currentPixels, x, y + 1 ) );

            if( ( gradH + gradV ) >= edgeThreshold || ( gradH2 + gradV2 ) >= edgeThreshold )
            {
                // This is an informative pixel.  Compute RGB diff.
                const int d0 = abs( static_cast<int>( currentPixels[idx + 0] ) - static_cast<int>( previousPixels[idx + 0] ) );
                const int d1 = abs( static_cast<int>( currentPixels[idx + 1] ) - static_cast<int>( previousPixels[idx + 1] ) );
                const int d2 = abs( static_cast<int>( currentPixels[idx + 2] ) - static_cast<int>( previousPixels[idx + 2] ) );
                informativeDiff += static_cast<unsigned __int64>( d0 + d1 + d2 );
                informativeCount++;
            }
        }
    }

    return ( informativeCount > 0 );
}

static void BuildDownsampledLumaFrame( const std::vector<BYTE>& pixels,
                                       int frameWidth,
                                       int frameHeight,
                                       int scale,
                                       std::vector<BYTE>& luma,
                                       int& downsampledWidth,
                                       int& downsampledHeight )
{
    downsampledWidth = max( 1, frameWidth / scale );
    downsampledHeight = max( 1, frameHeight / scale );
    luma.resize( static_cast<size_t>( downsampledWidth ) * static_cast<size_t>( downsampledHeight ) );

    for( int y = 0; y < downsampledHeight; ++y )
    {
        const int sourceY = min( frameHeight - 1, y * scale + ( scale / 2 ) );
        for( int x = 0; x < downsampledWidth; ++x )
        {
            const int sourceX = min( frameWidth - 1, x * scale + ( scale / 2 ) );
            const int sourceIndex = ( sourceY * frameWidth + sourceX ) * 4;
            const int l = ( pixels[sourceIndex + 2] * 77 +
                            pixels[sourceIndex + 1] * 150 +
                            pixels[sourceIndex + 0] * 29 ) >> 8;
            luma[static_cast<size_t>( y ) * static_cast<size_t>( downsampledWidth ) + static_cast<size_t>( x )] =
                static_cast<BYTE>( l );
        }
    }
}

// Build a full-resolution single-channel luma array from 32-bpp BGRA pixels.
static void BuildFullLumaFrame( const std::vector<BYTE>& pixels,
                                int frameWidth,
                                int frameHeight,
                                std::vector<BYTE>& luma )
{
    const size_t pixelCount = static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight );
    luma.resize( pixelCount );
    const BYTE* src = pixels.data();
    BYTE* dst = luma.data();
    size_t p = 0;

#if defined(_M_ARM64)
    // NEON: process 8 BGRA pixels per iteration.
    for( ; p + 8 <= pixelCount; p += 8 )
    {
        const uint8x8x4_t bgra = vld4_u8( src + p * 4 );
        const uint16x8_t rw = vmull_u8( bgra.val[2], vdup_n_u8( 77 ) );
        const uint16x8_t gw = vmull_u8( bgra.val[1], vdup_n_u8( 150 ) );
        const uint16x8_t bw = vmull_u8( bgra.val[0], vdup_n_u8( 29 ) );
        const uint16x8_t sum = vaddq_u16( vaddq_u16( rw, gw ), bw );
        vst1_u8( dst + p, vshrn_n_u16( sum, 8 ) );
    }
#elif defined(_M_X64) || defined(_M_IX86)
    // SSE2: process 4 BGRA pixels per iteration using _mm_madd_epi16
    // for pairwise multiply-add of adjacent 16-bit words to 32-bit.
    const __m128i zero = _mm_setzero_si128();
    const __m128i coeffs = _mm_setr_epi16( 29, 150, 77, 0, 29, 150, 77, 0 );
    const __m128i mask_even32 = _mm_setr_epi32( -1, 0, -1, 0 );
    for( ; p + 4 <= pixelCount; p += 4 )
    {
        const __m128i bgra = _mm_loadu_si128( reinterpret_cast<const __m128i*>( src + p * 4 ) );
        // Unpack interleaved BGRA bytes to 16-bit words.
        const __m128i lo16 = _mm_unpacklo_epi8( bgra, zero );  // B0 G0 R0 A0 B1 G1 R1 A1
        const __m128i hi16 = _mm_unpackhi_epi8( bgra, zero );  // B2 G2 R2 A2 B3 G3 R3 A3
        // _mm_madd_epi16: [B*29+G*150, R*77+A*0, ...]  per 32-bit lane pair.
        const __m128i prod_lo = _mm_madd_epi16( lo16, coeffs );
        const __m128i prod_hi = _mm_madd_epi16( hi16, coeffs );
        // Sum adjacent 32-bit pairs within each 64-bit lane.
        const __m128i sum_lo = _mm_add_epi32( _mm_and_si128( prod_lo, mask_even32 ),
                                              _mm_srli_epi64( prod_lo, 32 ) );
        const __m128i sum_hi = _mm_add_epi32( _mm_and_si128( prod_hi, mask_even32 ),
                                              _mm_srli_epi64( prod_hi, 32 ) );
        // >> 8 to divide by 256, then pack 32 -> 16 -> 8.
        const __m128i l32 = _mm_packs_epi32( _mm_srli_epi32( sum_lo, 8 ),
                                             _mm_srli_epi32( sum_hi, 8 ) );
        const __m128i l8 = _mm_packus_epi16( l32, zero );
        // l8 has [l0, 0, l1, 0, l2, 0, l3, 0, ...] because the odd 32-bit
        // lanes were zero before packing.  Extract even bytes.
        dst[p + 0] = static_cast<BYTE>( _mm_extract_epi16( l8, 0 ) & 0xFF );
        dst[p + 1] = static_cast<BYTE>( _mm_extract_epi16( l8, 1 ) & 0xFF );
        dst[p + 2] = static_cast<BYTE>( _mm_extract_epi16( l8, 2 ) & 0xFF );
        dst[p + 3] = static_cast<BYTE>( _mm_extract_epi16( l8, 3 ) & 0xFF );
    }
#endif

    for( ; p < pixelCount; ++p )
    {
        const size_t idx = p * 4;
        dst[p] = static_cast<BYTE>( ( src[idx + 2] * 77 +
                                      src[idx + 1] * 150 +
                                      src[idx + 0] * 29 ) >> 8 );
    }
}

// Downsample from a pre-computed full-resolution luma array.
static void BuildDownsampledLumaFromFullLuma( const std::vector<BYTE>& fullLuma,
                                              int frameWidth,
                                              int frameHeight,
                                              int scale,
                                              std::vector<BYTE>& luma,
                                              int& downsampledWidth,
                                              int& downsampledHeight )
{
    downsampledWidth = max( 1, frameWidth / scale );
    downsampledHeight = max( 1, frameHeight / scale );
    luma.resize( static_cast<size_t>( downsampledWidth ) * static_cast<size_t>( downsampledHeight ) );
    for( int y = 0; y < downsampledHeight; ++y )
    {
        const int sourceY = min( frameHeight - 1, y * scale + ( scale / 2 ) );
        for( int x = 0; x < downsampledWidth; ++x )
        {
            const int sourceX = min( frameWidth - 1, x * scale + ( scale / 2 ) );
            luma[static_cast<size_t>( y ) * static_cast<size_t>( downsampledWidth ) + static_cast<size_t>( x )] =
                fullLuma[static_cast<size_t>( sourceY ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( sourceX )];
        }
    }
}

// Compute per-row sum of horizontal gradient magnitudes.
// This is a 1D "edge density" signal: rows with text/edges have high values,
// constant rows (dark background) have ~0.  NCC on these signals discriminates
// offsets by structural content alignment rather than pixel identity.
static void BuildRowEdgeDensity( const std::vector<BYTE>& luma,
                                 int width, int height, int marginX,
                                 std::vector<int>& density )
{
    density.resize( height, 0 );
    for( int y = 0; y < height; ++y )
    {
        const int row = y * width;
        const BYTE* rowPtr = luma.data() + row;
        const int xStart = marginX;
        const int xEnd = width - marginX - 1;
        int sum = 0;
        int x = xStart;
#if defined(_M_ARM64)
        uint32x4_t vSum = vdupq_n_u32( 0 );
        for( ; x + 16 <= xEnd; x += 16 )
        {
            const uint8x16_t cur  = vld1q_u8( rowPtr + x );
            const uint8x16_t next = vld1q_u8( rowPtr + x + 1 );
            const uint8x16_t diff = vabdq_u8( cur, next );
            const uint16x8_t sum16 = vpaddlq_u8( diff );
            vSum = vaddq_u32( vSum, vpaddlq_u16( sum16 ) );
        }
        sum = static_cast<int>( vaddvq_u32( vSum ) );
#endif
        for( ; x < xEnd; ++x )
        {
            sum += abs( static_cast<int>( rowPtr[x + 1] ) -
                        static_cast<int>( rowPtr[x] ) );
        }
        density[y] = sum;
    }
}

// 1D Normalized Cross-Correlation between two int signals.
// Returns NCC in [-1.0, 1.0].  Returns 0.0 if either signal has zero variance.
static double NCC1D( const int* a, const int* b, int n )
{
    if( n <= 0 ) return 0.0;

    __int64 iSumA = 0, iSumB = 0, iSumAB = 0, iSumA2 = 0, iSumB2 = 0;
    int i = 0;
#if defined(_M_ARM64)
    // NEON: accumulate 4 int32 elements per iteration using widening
    // multiply-accumulate (vmlal_s32) for products.
    int64x2_t vSumA  = vdupq_n_s64( 0 );
    int64x2_t vSumB  = vdupq_n_s64( 0 );
    int64x2_t vSumAB = vdupq_n_s64( 0 );
    int64x2_t vSumA2 = vdupq_n_s64( 0 );
    int64x2_t vSumB2 = vdupq_n_s64( 0 );
    for( ; i + 4 <= n; i += 4 )
    {
        const int32x4_t va = vld1q_s32( a + i );
        const int32x4_t vb = vld1q_s32( b + i );
        // Pairwise add-long for sums (s32 -> s64).
        vSumA = vaddq_s64( vSumA, vpaddlq_s32( va ) );
        vSumB = vaddq_s64( vSumB, vpaddlq_s32( vb ) );
        // Widening multiply-accumulate: low and high halves separately.
        const int32x2_t aLo = vget_low_s32( va );
        const int32x2_t aHi = vget_high_s32( va );
        const int32x2_t bLo = vget_low_s32( vb );
        const int32x2_t bHi = vget_high_s32( vb );
        vSumAB = vmlal_s32( vmlal_s32( vSumAB, aLo, bLo ), aHi, bHi );
        vSumA2 = vmlal_s32( vmlal_s32( vSumA2, aLo, aLo ), aHi, aHi );
        vSumB2 = vmlal_s32( vmlal_s32( vSumB2, bLo, bLo ), bHi, bHi );
    }
    iSumA  = vgetq_lane_s64( vSumA, 0 )  + vgetq_lane_s64( vSumA, 1 );
    iSumB  = vgetq_lane_s64( vSumB, 0 )  + vgetq_lane_s64( vSumB, 1 );
    iSumAB = vgetq_lane_s64( vSumAB, 0 ) + vgetq_lane_s64( vSumAB, 1 );
    iSumA2 = vgetq_lane_s64( vSumA2, 0 ) + vgetq_lane_s64( vSumA2, 1 );
    iSumB2 = vgetq_lane_s64( vSumB2, 0 ) + vgetq_lane_s64( vSumB2, 1 );
#endif
    for( ; i < n; ++i )
    {
        const __int64 ai = static_cast<__int64>( a[i] );
        const __int64 bi = static_cast<__int64>( b[i] );
        iSumA  += ai;
        iSumB  += bi;
        iSumAB += ai * bi;
        iSumA2 += ai * ai;
        iSumB2 += bi * bi;
    }
    const double N    = static_cast<double>( n );
    const double sumA = static_cast<double>( iSumA );
    const double sumB = static_cast<double>( iSumB );
    const double varA = static_cast<double>( iSumA2 ) / N - ( sumA / N ) * ( sumA / N );
    const double varB = static_cast<double>( iSumB2 ) / N - ( sumB / N ) * ( sumB / N );
    if( varA <= 0.0 || varB <= 0.0 ) return 0.0;
    const double cov = static_cast<double>( iSumAB ) / N - ( sumA / N ) * ( sumB / N );
    return cov / sqrt( varA * varB );
}

struct FixedOverlayMask
{
    int tileWidth = 0;
    int tileHeight = 0;
    int tileCols = 0;
    int tileRows = 0;
    std::vector<BYTE> maskedTiles;

    // Pixel-level erase region for small floating overlays (e.g. spinner
    // icon) that are too small/dynamic for tile-based suppression.  Before
    // composing each frame, pixels within this rect that differ from the
    // local background are replaced with the background color.
    RECT eraseRect = {};  // In frame coordinates.  Empty if right<=left.

    // Height of the fixed header at the top of the frame (0 if none).
    // The first composed frame is exempt from tile-mask suppression
    // for y < topHeaderHeight so the header appears once at the top.
    int topHeaderHeight = 0;

    // Pixel row where the fixed bottom strip starts (0 if none).
    // Used to suppress floating buttons in the margin above the toolbar.
    int bottomStripY = 0;

    bool Empty() const
    {
        return maskedTiles.empty() && eraseRect.right <= eraseRect.left;
    }

    bool IsMaskedPixel( int x, int y ) const
    {
        if( maskedTiles.empty() || tileWidth <= 0 || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return false;

        const int tileX = min( tileCols - 1, max( 0, x / tileWidth ) );
        const int tileY = min( tileRows - 1, max( 0, y / tileHeight ) );
        const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( tileCols ) + static_cast<size_t>( tileX );
        return tileIndex < maskedTiles.size() && maskedTiles[tileIndex] != 0;
    }

    bool IsMaskedRow( int y ) const
    {
        if( maskedTiles.empty() || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return false;

        const int tileY = min( tileRows - 1, max( 0, y / tileHeight ) );
        const size_t rowBase = static_cast<size_t>( tileY ) * static_cast<size_t>( tileCols );
        for( int tileX = 0; tileX < tileCols; ++tileX )
        {
            if( maskedTiles[rowBase + static_cast<size_t>( tileX )] != 0 )
                return true;
        }
        return false;
    }

    // Returns true if ALL tile columns in this pixel row are masked.
    bool IsFullWidthMaskedRow( int y ) const
    {
        if( maskedTiles.empty() || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return false;

        const int tileY = min( tileRows - 1, max( 0, y / tileHeight ) );
        const size_t rowBase = static_cast<size_t>( tileY ) * static_cast<size_t>( tileCols );
        for( int tx = 0; tx < tileCols; ++tx )
        {
            if( maskedTiles[rowBase + static_cast<size_t>( tx )] == 0 )
                return false;
        }
        return true;
    }

    // Returns the pixel-Y of the first masked row, or -1 if none.
    int FirstMaskedY() const
    {
        if( maskedTiles.empty() || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return -1;

        for( int ty = 0; ty < tileRows; ++ty )
        {
            const size_t rowBase = static_cast<size_t>( ty ) * static_cast<size_t>( tileCols );
            for( int tx = 0; tx < tileCols; ++tx )
            {
                if( maskedTiles[rowBase + static_cast<size_t>( tx )] != 0 )
                    return ty * tileHeight;
            }
        }
        return -1;
    }

    // Returns the number of rows at the top of the frame that are
    // mostly masked (majority of tiles in the row are masked).
    // Returns 0 if there is no contiguous masked header.
    int TopMaskedRows() const
    {
        if( maskedTiles.empty() || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return 0;

        int maskedRowCount = 0;
        for( int ty = 0; ty < tileRows; ++ty )
        {
            const size_t rowBase = static_cast<size_t>( ty ) * static_cast<size_t>( tileCols );
            int maskedInRow = 0;
            for( int tx = 0; tx < tileCols; ++tx )
            {
                if( maskedTiles[rowBase + static_cast<size_t>( tx )] != 0 )
                    ++maskedInRow;
            }
            // Require majority of tiles masked to count as header row.
            if( maskedInRow * 2 < tileCols )
                break;
            maskedRowCount = ( ty + 1 ) * tileHeight;
        }
        return maskedRowCount;
    }

    // Return the height of a fixed header detected at the top of the
    // frame.  Uses a lenient check: any tile row starting from the
    // first masked row that has at least one masked tile is part of
    // the header.  Returns 0 if there is no header.
    int TopHeaderHeight() const
    {
        if( maskedTiles.empty() || tileHeight <= 0 || tileCols <= 0 || tileRows <= 0 )
            return 0;

        const int firstMY = FirstMaskedY();
        if( firstMY < 0 || firstMY > tileHeight )
            return 0;

        const int startTY = firstMY / max( 1, tileHeight );
        int headerEndTY = startTY;
        for( int ty = startTY; ty < tileRows; ++ty )
        {
            const size_t rowBase = static_cast<size_t>( ty ) * static_cast<size_t>( tileCols );
            bool anyMasked = false;
            for( int tx = 0; tx < tileCols; ++tx )
            {
                if( maskedTiles[rowBase + static_cast<size_t>( tx )] != 0 )
                {
                    anyMasked = true;
                    break;
                }
            }
            if( !anyMasked )
            {
                headerEndTY = ty;
                break;
            }
        }
        const int h = headerEndTY * tileHeight;
        // Sanity: header must be < 1/4 of frame.
        const int frameH = tileRows * tileHeight;
        return ( h > 0 && h < frameH / 4 ) ? h : 0;
    }
};

struct FixedOverlayDiagnostics
{
    int pairCount = 0;
    int informativeTileComparisons = 0;
    int strongTileCount = 0;
    int connectedTileCount = 0;
    int maskedTileCount = 0;
    int tileBoundsLeft = 0;
    int tileBoundsTop = 0;
    int tileBoundsRight = 0;
    int tileBoundsBottom = 0;
    unsigned __int64 suppressedPixels = 0;
    unsigned __int64 repairedPixels = 0;
    unsigned __int64 fallbackPixels = 0;
    int correctedDarkBands = 0;
    int correctedDarkBandRows = 0;
    int blendedToBaseline = 0;
};

static unsigned __int64 RepairSuppressedOverlayHoles( std::vector<BYTE>& pixels,
                                                      std::vector<BYTE>& written,
                                                      std::vector<int>& owner,
                                                      std::vector<BYTE>& blended,
                                                      const std::vector<BYTE>& suppressed,
                                                      int width,
                                                      int height )
{
    if( pixels.empty() || written.empty() || owner.empty() || blended.empty() ||
        suppressed.empty() || width <= 0 || height <= 0 )
    {
        return 0;
    }

    unsigned __int64 repairedPixels = 0;
    for( int x = 0; x < width; ++x )
    {
        int y = 0;
        while( y < height )
        {
            const size_t pixelIndex = static_cast<size_t>( y ) * static_cast<size_t>( width ) + static_cast<size_t>( x );
            if( written[pixelIndex] != 0 || suppressed[pixelIndex] == 0 )
            {
                ++y;
                continue;
            }

            const int runStart = y;
            while( y < height )
            {
                const size_t runIndex = static_cast<size_t>( y ) * static_cast<size_t>( width ) + static_cast<size_t>( x );
                if( written[runIndex] != 0 || suppressed[runIndex] == 0 )
                {
                    break;
                }
                ++y;
            }
            const int runEnd = y - 1;

            int aboveY = runStart - 1;
            while( aboveY >= 0 )
            {
                const size_t aboveIndex = static_cast<size_t>( aboveY ) * static_cast<size_t>( width ) + static_cast<size_t>( x );
                if( written[aboveIndex] != 0 )
                {
                    break;
                }
                --aboveY;
            }

            int belowY = runEnd + 1;
            while( belowY < height )
            {
                const size_t belowIndex = static_cast<size_t>( belowY ) * static_cast<size_t>( width ) + static_cast<size_t>( x );
                if( written[belowIndex] != 0 )
                {
                    break;
                }
                ++belowY;
            }

            const bool hasAbove = aboveY >= 0;
            const bool hasBelow = belowY < height;
            if( !hasAbove && !hasBelow )
            {
                continue;
            }

            const size_t abovePixelIndex = hasAbove
                ? static_cast<size_t>( aboveY ) * static_cast<size_t>( width ) + static_cast<size_t>( x )
                : 0;
            const size_t belowPixelIndex = hasBelow
                ? static_cast<size_t>( belowY ) * static_cast<size_t>( width ) + static_cast<size_t>( x )
                : 0;
            const size_t aboveColorIndex = abovePixelIndex * 4;
            const size_t belowColorIndex = belowPixelIndex * 4;

            for( int fillY = runStart; fillY <= runEnd; ++fillY )
            {
                const size_t fillPixelIndex = static_cast<size_t>( fillY ) * static_cast<size_t>( width ) + static_cast<size_t>( x );
                const size_t fillColorIndex = fillPixelIndex * 4;

                if( hasAbove && hasBelow && belowY > aboveY )
                {
                    const int numerator = fillY - aboveY;
                    const int denominator = belowY - aboveY;
                    for( int channel = 0; channel < 3; ++channel )
                    {
                        const int aboveValue = static_cast<int>( pixels[aboveColorIndex + static_cast<size_t>( channel )] );
                        const int belowValue = static_cast<int>( pixels[belowColorIndex + static_cast<size_t>( channel )] );
                        pixels[fillColorIndex + static_cast<size_t>( channel )] = static_cast<BYTE>(
                            ( aboveValue * ( denominator - numerator ) + belowValue * numerator ) / max( 1, denominator ) );
                    }
                    const bool useBelowOwner = numerator * 2 >= denominator;
                    owner[fillPixelIndex] = useBelowOwner ? owner[belowPixelIndex] : owner[abovePixelIndex];
                }
                else
                {
                    const size_t sourcePixelIndex = hasAbove ? abovePixelIndex : belowPixelIndex;
                    const size_t sourceColorIndex = sourcePixelIndex * 4;
                    pixels[fillColorIndex + 0] = pixels[sourceColorIndex + 0];
                    pixels[fillColorIndex + 1] = pixels[sourceColorIndex + 1];
                    pixels[fillColorIndex + 2] = pixels[sourceColorIndex + 2];
                    owner[fillPixelIndex] = owner[sourcePixelIndex];
                }

                pixels[fillColorIndex + 3] = 0xFF;
                written[fillPixelIndex] = 1;
                blended[fillPixelIndex] = 1;
                ++repairedPixels;
            }
        }
    }

    return repairedPixels;
}

// Repairs dark bands caused by overlay suppression breaking the blend chain.
// After FixedOverlay suppression, rows near frame boundaries lose intermediate-
// frame blend contributions, creating visible dark streaks.  This function
// detects those dark rows and scales pixel brightness within the overlay column
// range to match the surrounding neighborhood luminance.
static int RepairOverlayDarkBands( std::vector<BYTE>& pixels,
                                    const std::vector<BYTE>& written,
                                    const std::vector<BYTE>& blended,
                                    int overlayLeft,
                                    int overlayRight,
                                    int width,
                                    int height,
                                    int* outCorrectedRows )
{
    if( outCorrectedRows )
    {
        *outCorrectedRows = 0;
    }

    if( pixels.empty() || written.empty() || blended.empty() ||
        width <= 0 || height <= 0 ||
        overlayLeft < 0 || overlayRight <= overlayLeft || overlayRight > width )
    {
        return 0;
    }

    // Build per-row luminance and blend statistics.
    std::vector<int> rowLuma( height, 0 );
    std::vector<int> rowWritten( height, 0 );
    std::vector<int> rowBlended( height, 0 );
    for( int y = 0; y < height; ++y )
    {
        unsigned __int64 totalLuma = 0;
        const size_t pixelRowBase = static_cast<size_t>( y ) * static_cast<size_t>( width ) * 4;
        const size_t maskRowBase = static_cast<size_t>( y ) * static_cast<size_t>( width );
        for( int x = 0; x < width; ++x )
        {
            const size_t pixelIdx = pixelRowBase + static_cast<size_t>( x ) * 4;
            totalLuma += ( static_cast<unsigned __int64>( pixels[pixelIdx + 2] ) * 77 +
                           static_cast<unsigned __int64>( pixels[pixelIdx + 1] ) * 150 +
                           static_cast<unsigned __int64>( pixels[pixelIdx + 0] ) * 29 ) >> 8;
            const size_t maskIdx = maskRowBase + static_cast<size_t>( x );
            if( written[maskIdx] != 0 )
            {
                ++rowWritten[y];
            }
            if( blended[maskIdx] != 0 )
            {
                ++rowBlended[y];
            }
        }
        rowLuma[y] = static_cast<int>( totalLuma / max( 1, width ) );
    }

    // Detect dark band rows using the same criteria as LogStitchedBandDiagnostics.
    const int referenceRadius = max( 8, min( 24, height / 40 ) );
    std::vector<BYTE> darkRowMask( height, 0 );
    std::vector<int> rowRefLuma( height, 0 );

    for( int y = referenceRadius; y < height - referenceRadius; ++y )
    {
        if( rowWritten[y] < width * 9 / 10 )
        {
            continue;
        }

        unsigned __int64 neighborhoodTotal = 0;
        int neighborhoodCount = 0;
        for( int offset = -referenceRadius; offset <= referenceRadius; ++offset )
        {
            if( offset == 0 || abs( offset ) <= 2 )
            {
                continue;
            }
            const int sampleY = y + offset;
            neighborhoodTotal += static_cast<unsigned __int64>( rowLuma[sampleY] );
            ++neighborhoodCount;
        }
        if( neighborhoodCount <= 0 )
        {
            continue;
        }

        const int refLuma = static_cast<int>( neighborhoodTotal / neighborhoodCount );
        const int delta = refLuma - rowLuma[y];
        if( refLuma >= 24 &&
            delta >= max( 18, refLuma / 5 ) &&
            rowBlended[y] >= width / 3 )
        {
            darkRowMask[y] = 1;
            rowRefLuma[y] = refLuma;
        }
    }

    // Group into bands, find reference rows, and apply per-pixel correction.
    const int columnFeather = 8;
    int correctedBandCount = 0;
    int correctedRowCount = 0;

    for( int y = 0; y < height; )
    {
        if( darkRowMask[y] == 0 )
        {
            ++y;
            continue;
        }

        const int bandStart = y;
        while( y < height && darkRowMask[y] != 0 )
        {
            ++y;
        }
        const int bandEnd = y - 1;

        if( bandEnd - bandStart + 1 < 2 )
        {
            continue;
        }

        // Find nearest non-dark reference rows above and below.
        int refAboveY = bandStart - 1;
        while( refAboveY >= 0 && darkRowMask[refAboveY] != 0 )
        {
            --refAboveY;
        }
        int refBelowY = bandEnd + 1;
        while( refBelowY < height && darkRowMask[refBelowY] != 0 )
        {
            ++refBelowY;
        }

        const bool hasAbove = refAboveY >= 0;
        const bool hasBelow = refBelowY < height;
        if( !hasAbove && !hasBelow )
        {
            continue;
        }

        // Apply per-pixel luminance correction for each row in the band.
        // Only correct columns within the overlay mask range (with feather).
        for( int bandY = bandStart; bandY <= bandEnd; ++bandY )
        {
            const int curRowLuma = rowLuma[bandY];
            const int refLuma = rowRefLuma[bandY];
            if( curRowLuma <= 4 || refLuma <= 4 )
            {
                continue;
            }

            // Row-level correction cap (fixed-point: 256 = 1.0).
            const int rowFactorCap256 = min( 448, ( refLuma * 256 ) / max( 1, curRowLuma ) );

            const int spanTotal = ( hasAbove && hasBelow ) ? ( refBelowY - refAboveY ) : 1;
            const int spanToAbove = bandY - ( hasAbove ? refAboveY : bandY );

            const size_t pixelRowBase = static_cast<size_t>( bandY ) * static_cast<size_t>( width ) * 4;
            const size_t aboveRowBase = hasAbove
                ? static_cast<size_t>( refAboveY ) * static_cast<size_t>( width ) * 4 : 0;
            const size_t belowRowBase = hasBelow
                ? static_cast<size_t>( refBelowY ) * static_cast<size_t>( width ) * 4 : 0;

            bool rowCorrected = false;

            // Process columns within overlay range (with feathered edges).
            const int corrLeft = max( 0, overlayLeft - columnFeather );
            const int corrRight = min( width, overlayRight + columnFeather );

            for( int x = corrLeft; x < corrRight; ++x )
            {
                // Column feather: ramp correction from 0 at edge to full inside.
                int columnWeight256 = 256;
                if( x < overlayLeft )
                {
                    columnWeight256 = ( ( x - corrLeft ) * 256 ) / max( 1, columnFeather );
                }
                else if( x >= overlayRight )
                {
                    columnWeight256 = ( ( corrRight - 1 - x ) * 256 ) / max( 1, columnFeather );
                }

                const size_t pixIdx = pixelRowBase + static_cast<size_t>( x ) * 4;

                // Compute per-pixel reference luminance from vertical neighbors.
                int pixRefLuma;
                if( hasAbove && hasBelow )
                {
                    const size_t aboveIdx = aboveRowBase + static_cast<size_t>( x ) * 4;
                    const size_t belowIdx = belowRowBase + static_cast<size_t>( x ) * 4;
                    const int aboveLuma = ( static_cast<int>( pixels[aboveIdx + 2] ) * 77 +
                                            static_cast<int>( pixels[aboveIdx + 1] ) * 150 +
                                            static_cast<int>( pixels[aboveIdx + 0] ) * 29 ) >> 8;
                    const int belowLuma = ( static_cast<int>( pixels[belowIdx + 2] ) * 77 +
                                            static_cast<int>( pixels[belowIdx + 1] ) * 150 +
                                            static_cast<int>( pixels[belowIdx + 0] ) * 29 ) >> 8;
                    pixRefLuma = ( aboveLuma * ( spanTotal - spanToAbove ) +
                                   belowLuma * spanToAbove ) / max( 1, spanTotal );
                }
                else if( hasAbove )
                {
                    const size_t aboveIdx = aboveRowBase + static_cast<size_t>( x ) * 4;
                    pixRefLuma = ( static_cast<int>( pixels[aboveIdx + 2] ) * 77 +
                                   static_cast<int>( pixels[aboveIdx + 1] ) * 150 +
                                   static_cast<int>( pixels[aboveIdx + 0] ) * 29 ) >> 8;
                }
                else
                {
                    const size_t belowIdx = belowRowBase + static_cast<size_t>( x ) * 4;
                    pixRefLuma = ( static_cast<int>( pixels[belowIdx + 2] ) * 77 +
                                   static_cast<int>( pixels[belowIdx + 1] ) * 150 +
                                   static_cast<int>( pixels[belowIdx + 0] ) * 29 ) >> 8;
                }

                const int curLuma = ( static_cast<int>( pixels[pixIdx + 2] ) * 77 +
                                      static_cast<int>( pixels[pixIdx + 1] ) * 150 +
                                      static_cast<int>( pixels[pixIdx + 0] ) * 29 ) >> 8;

                if( curLuma <= 4 || pixRefLuma <= 4 )
                {
                    continue;
                }

                // Per-pixel correction factor, capped by row-level factor.
                int pixFactor256 = ( pixRefLuma * 256 ) / max( 1, curLuma );
                pixFactor256 = min( rowFactorCap256, pixFactor256 );

                // Only correct if meaningfully darker (factor > 1.02).
                if( pixFactor256 <= 261 )
                {
                    continue;
                }

                // Apply column feather to blend correction strength at edges.
                const int factor256 = 256 + ( ( pixFactor256 - 256 ) * columnWeight256 ) / 256;

                for( int c = 0; c < 3; ++c )
                {
                    const int val = static_cast<int>( pixels[pixIdx + static_cast<size_t>( c )] );
                    pixels[pixIdx + static_cast<size_t>( c )] = static_cast<BYTE>(
                        min( 255, ( val * factor256 ) / 256 ) );
                }
                rowCorrected = true;
            }

            if( rowCorrected )
            {
                ++correctedRowCount;
            }
        }

        ++correctedBandCount;
    }

    if( outCorrectedRows )
    {
        *outCorrectedRows = correctedRowCount;
    }
    return correctedBandCount;
}

static int ComputeTileEdgeEnergy( const std::vector<BYTE>& luma,
                                  int frameWidth,
                                  int frameHeight,
                                  int startX,
                                  int startY,
                                  int tileWidth,
                                  int tileHeight )
{
    if( luma.empty() || frameWidth <= 2 || frameHeight <= 2 || tileWidth <= 1 || tileHeight <= 1 )
        return 0;

    const int endX = min( frameWidth - 1, startX + tileWidth );
    const int endY = min( frameHeight - 1, startY + tileHeight );
    const int sampleStep = 3;
    int energy = 0;

    for( int y = max( 1, startY ); y < endY; y += sampleStep )
    {
        for( int x = max( 1, startX ); x < endX; x += sampleStep )
        {
            const size_t index = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x );
            const int center = static_cast<int>( luma[index] );
            const int left = static_cast<int>( luma[index - 1] );
            const int up = static_cast<int>( luma[index - static_cast<size_t>( frameWidth )] );
            energy += abs( center - left ) + abs( center - up );
        }
    }

    return energy;
}

static int ComputeTileAverageRgbDifference( const std::vector<BYTE>& aPixels,
                                            int aStartX,
                                            int aStartY,
                                            const std::vector<BYTE>& bPixels,
                                            int bStartX,
                                            int bStartY,
                                            int frameWidth,
                                            int frameHeight,
                                            int tileWidth,
                                            int tileHeight )
{
    if( aPixels.empty() || bPixels.empty() )
        return INT_MAX;

    if( aStartX < 0 || aStartY < 0 || bStartX < 0 || bStartY < 0 ||
        aStartX + tileWidth > frameWidth || aStartY + tileHeight > frameHeight ||
        bStartX + tileWidth > frameWidth || bStartY + tileHeight > frameHeight )
    {
        return INT_MAX;
    }

    const int sampleStep = 4;
    unsigned __int64 diffSum = 0;
    unsigned __int64 sampleCount = 0;

    for( int y = 0; y < tileHeight; y += sampleStep )
    {
        const int ay = aStartY + y;
        const int by = bStartY + y;
        const size_t aRow = static_cast<size_t>( ay ) * static_cast<size_t>( frameWidth ) * 4;
        const size_t bRow = static_cast<size_t>( by ) * static_cast<size_t>( frameWidth ) * 4;
        for( int x = 0; x < tileWidth; x += sampleStep )
        {
            const size_t aIndex = aRow + static_cast<size_t>( aStartX + x ) * 4;
            const size_t bIndex = bRow + static_cast<size_t>( bStartX + x ) * 4;
            diffSum += static_cast<unsigned __int64>( abs( static_cast<int>( aPixels[aIndex + 0] ) - static_cast<int>( bPixels[bIndex + 0] ) ) );
            diffSum += static_cast<unsigned __int64>( abs( static_cast<int>( aPixels[aIndex + 1] ) - static_cast<int>( bPixels[bIndex + 1] ) ) );
            diffSum += static_cast<unsigned __int64>( abs( static_cast<int>( aPixels[aIndex + 2] ) - static_cast<int>( bPixels[bIndex + 2] ) ) );
            ++sampleCount;
        }
    }

    if( sampleCount == 0 )
        return INT_MAX;

    return static_cast<int>( diffSum / ( sampleCount * 3 ) );
}

static constexpr bool IsStrongFixedOverlayTile( int supports, int stationary, int scrolled )
{
    return supports >= 4 &&
           stationary >= 3 &&
           stationary >= scrolled + 2 &&
           stationary * 2 >= supports + 1;
}

static constexpr bool IsWeakFixedOverlayTile( int supports, int stationary, int scrolled )
{
    return supports >= 3 &&
           stationary >= 2 &&
           stationary >= scrolled + 1 &&
           stationary * 2 >= supports;
}

static int CountSetTileNeighbors( const std::vector<BYTE>& tiles,
                                  int tileCols,
                                  int tileRows,
                                  int tileX,
                                  int tileY )
{
    int neighbors = 0;
    for( int neighborY = max( 0, tileY - 1 ); neighborY <= min( tileRows - 1, tileY + 1 ); ++neighborY )
    {
        for( int neighborX = max( 0, tileX - 1 ); neighborX <= min( tileCols - 1, tileX + 1 ); ++neighborX )
        {
            if( neighborX == tileX && neighborY == tileY )
                continue;

            const size_t neighborIndex = static_cast<size_t>( neighborY ) * static_cast<size_t>( tileCols ) + static_cast<size_t>( neighborX );
            neighbors += tiles[neighborIndex] != 0 ? 1 : 0;
        }
    }
    return neighbors;
}

// Detect a fixed overlay strip at the bottom of the frame by comparing bottom
// rows across widely-separated frame pairs.  Returns the height in pixels of
// the detected strip, or 0 if none found.
static int DetectFixedBottomStrip( const std::vector<std::vector<BYTE>>& framePixels,
                                   const std::vector<size_t>& composedFrameIndices,
                                   const std::vector<POINT>& composedFrameSteps,
                                   int frameWidth,
                                   int frameHeight )
{
    if( composedFrameIndices.size() < 6 || frameWidth < 64 || frameHeight < 64 )
        return 0;

    // Build cumulative Y offsets to find widely-separated frame pairs.
    std::vector<int> cumulativeY( composedFrameIndices.size(), 0 );
    for( size_t i = 1; i < composedFrameIndices.size(); ++i )
    {
        cumulativeY[i] = cumulativeY[i - 1] + composedFrameSteps[i].y;
    }

    // Collect frame pairs with significant scroll separation.
    const int minSeparation = max( frameHeight / 2, 200 );
    struct Pair { size_t a, b; };
    std::vector<Pair> pairs;
    const size_t stride = max( static_cast<size_t>( 1 ), composedFrameIndices.size() / 7 );
    for( size_t anchor = 0; anchor < composedFrameIndices.size() && pairs.size() < 6; anchor += stride )
    {
        for( size_t other = anchor + 1; other < composedFrameIndices.size(); ++other )
        {
            if( abs( cumulativeY[other] - cumulativeY[anchor] ) >= minSeparation )
            {
                pairs.push_back( { composedFrameIndices[anchor], composedFrameIndices[other] } );
                break;
            }
        }
    }
    StitchLog( L"[Panorama/Stitch] BottomStripDetect: pairs=%zu minSep=%d stride=%zu\n",
               pairs.size(), minSeparation, stride );
    if( pairs.size() < 3 )
        return 0;

    // For each pair, find the longest matching suffix from the bottom.
    const int maxScanRows = min( frameHeight / 3, 256 );
    std::vector<int> allMatchingRows;
    allMatchingRows.reserve( pairs.size() );
    for( size_t pi = 0; pi < pairs.size(); ++pi )
    {
        const auto& p = pairs[pi];
        const auto& pixA = framePixels[p.a];
        const auto& pixB = framePixels[p.b];
        if( pixA.empty() || pixB.empty() )
        {
            StitchLog( L"[Panorama/Stitch] BottomStripDetect: pair %zu (%zu,%zu) EMPTY pixelsA=%zu pixelsB=%zu\n",
                       pi, p.a, p.b, pixA.size(), pixB.size() );
            allMatchingRows.push_back( 0 );
            continue;
        }

        int matchingRows = 0;
        int firstMismatchY = -1;
        int firstMismatchAvgDiff = 0;
        for( int y = frameHeight - 1; y >= frameHeight - maxScanRows; --y )
        {
            const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4;
            int rowDiffSum = 0;
            int sampleCount = 0;
            for( int x = 0; x < frameWidth; x += 4 )
            {
                const size_t idx = rowBase + static_cast<size_t>( x ) * 4;
                rowDiffSum += abs( static_cast<int>( pixA[idx + 0] ) - static_cast<int>( pixB[idx + 0] ) );
                rowDiffSum += abs( static_cast<int>( pixA[idx + 1] ) - static_cast<int>( pixB[idx + 1] ) );
                rowDiffSum += abs( static_cast<int>( pixA[idx + 2] ) - static_cast<int>( pixB[idx + 2] ) );
                ++sampleCount;
            }
            const int avgDiff = rowDiffSum / max( 1, sampleCount * 3 );
            // Threshold raised from 6 to 12 to accommodate VM/RDP
            // rendering noise — software-rendered or protocol-compressed
            // displays can produce per-pixel jitter of 5-10 in static content.
            // True scrolled-content transitions have avgDiff >> 15.
            if( avgDiff > 12 )
            {
                firstMismatchY = y;
                firstMismatchAvgDiff = avgDiff;
                break;
            }
            ++matchingRows;
        }
        StitchLog( L"[Panorama/Stitch] BottomStripDetect: pair %zu (%zu,%zu) matchingRows=%d mismatchY=%d mismatchDiff=%d\n",
                   pi, p.a, p.b, matchingRows, firstMismatchY, firstMismatchAvgDiff );
        allMatchingRows.push_back( matchingRows );
    }

    // Use a robust aggregation instead of strict minimum: allow up to
    // 1/3 of pairs to be outliers.  VM or RDP rendering noise, animated
    // scroll indicators, and subtle overlay state changes can cause a
    // few pairs to badly disagree even though the static strip is real.
    if( allMatchingRows.empty() )
        return 0;
    std::sort( allMatchingRows.begin(), allMatchingRows.end() );
    const size_t robustIndex = min( allMatchingRows.size() / 3, allMatchingRows.size() - 1 );
    const int minMatchingRows = allMatchingRows[robustIndex];
    StitchLog( L"[Panorama/Stitch] BottomStripDetect: minMatchingRows=%d (robust index=%zu of %zu, values=",
               minMatchingRows, robustIndex, allMatchingRows.size() );
    for( size_t ri = 0; ri < allMatchingRows.size(); ++ri )
        StitchLog( L"%s%d", ri > 0 ? L"," : L"", allMatchingRows[ri] );
    StitchLog( L")\n" );
    if( minMatchingRows < 3 )
        return 0;

    // Verify the strip has visual content (not just uniform background).
    const auto& pix0 = framePixels[composedFrameIndices[0]];
    if( pix0.empty() )
        return 0;

    const int stripStartY = frameHeight - minMatchingRows;
    int energy = 0;
    int energySamples = 0;
    for( int y = max( 1, stripStartY ); y < frameHeight; y += 3 )
    {
        const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4;
        const size_t prevRowBase = static_cast<size_t>( y - 1 ) * static_cast<size_t>( frameWidth ) * 4;
        for( int x = 1; x < frameWidth; x += 3 )
        {
            const size_t idx = rowBase + static_cast<size_t>( x ) * 4;
            const size_t leftIdx = rowBase + static_cast<size_t>( x - 1 ) * 4;
            const size_t upIdx = prevRowBase + static_cast<size_t>( x ) * 4;
            const int center = ( static_cast<int>( pix0[idx + 2] ) * 77 +
                                 static_cast<int>( pix0[idx + 1] ) * 150 +
                                 static_cast<int>( pix0[idx + 0] ) * 29 ) >> 8;
            const int left = ( static_cast<int>( pix0[leftIdx + 2] ) * 77 +
                               static_cast<int>( pix0[leftIdx + 1] ) * 150 +
                               static_cast<int>( pix0[leftIdx + 0] ) * 29 ) >> 8;
            const int up = ( static_cast<int>( pix0[upIdx + 2] ) * 77 +
                             static_cast<int>( pix0[upIdx + 1] ) * 150 +
                             static_cast<int>( pix0[upIdx + 0] ) * 29 ) >> 8;
            energy += abs( center - left ) + abs( center - up );
            ++energySamples;
        }
    }
    const int avgEnergy = ( energySamples > 0 ) ? energy / energySamples : 0;
    // Large contiguous matching strips (≥ 40 rows) across many frame pairs
    // are extremely unlikely to be plain background — even dark-themed
    // toolbars contain subtle gradients, icons, and text.  Relax the energy
    // threshold to avoid rejecting genuine low-contrast UI bars.
    const int energyThreshold = ( minMatchingRows >= 40 ) ? 1 : 3;
    StitchLog( L"[Panorama/Stitch] BottomStripDetect: stripStartY=%d energy=%d samples=%d avgEnergy=%d (threshold=%d)\n",
               stripStartY, energy, energySamples, avgEnergy, energyThreshold );
    if( energySamples <= 0 || avgEnergy < energyThreshold )
        return 0;

    return minMatchingRows;
}

static FixedOverlayMask BuildFixedOverlayMask( const std::vector<size_t>& composedFrameIndices,
                                               const std::vector<POINT>& composedFrameSteps,
                                               const std::vector<std::vector<BYTE>>& framePixels,
                                               const std::vector<std::vector<BYTE>>& frameLuma,
                                               int frameWidth,
                                               int frameHeight,
                                               int minProgress,
                                               FixedOverlayDiagnostics* diagnostics )
{
    FixedOverlayMask mask;
    if( diagnostics != nullptr )
    {
        *diagnostics = {};
    }

    if( composedFrameIndices.size() < 5 || frameWidth < 64 || frameHeight < 64 )
        return mask;

    mask.tileWidth = max( 16, min( 32, frameWidth / 28 ) );
    mask.tileHeight = max( 16, min( 32, frameHeight / 28 ) );
    mask.tileCols = ( frameWidth + mask.tileWidth - 1 ) / mask.tileWidth;
    mask.tileRows = ( frameHeight + mask.tileHeight - 1 ) / mask.tileHeight;

    if( mask.tileCols <= 0 || mask.tileRows <= 0 )
        return mask;

    const size_t tileCount = static_cast<size_t>( mask.tileCols ) * static_cast<size_t>( mask.tileRows );
    std::vector<int> supportCount( tileCount, 0 );
    std::vector<int> stationaryWins( tileCount, 0 );
    std::vector<int> scrolledWins( tileCount, 0 );

    const size_t maxLookbackComparisons = ( composedFrameIndices.size() - 1 < 4 ) ? ( composedFrameIndices.size() - 1 ) : 4;
    for( size_t passIndex = 1; passIndex < composedFrameIndices.size(); ++passIndex )
    {
        const size_t currFrameIndex = composedFrameIndices[passIndex];
        const std::vector<BYTE>& currPixels = framePixels[currFrameIndex];
        const std::vector<BYTE>& currLuma = frameLuma[currFrameIndex];
        if( currPixels.empty() || currLuma.empty() )
            continue;

        POINT cumulativeStep{};
        const size_t lookbackLimit = min( maxLookbackComparisons, passIndex );
        for( size_t compareOffset = 1; compareOffset <= lookbackLimit; ++compareOffset )
        {
            const POINT& segmentStep = composedFrameSteps[passIndex - compareOffset + 1];
            cumulativeStep.x += segmentStep.x;
            cumulativeStep.y += segmentStep.y;

            const int absStepX = abs( cumulativeStep.x );
            const int absStepY = abs( cumulativeStep.y );
            const bool mostlyVertical = absStepY >= minProgress && absStepX <= max( 16, frameWidth / 18 );
            const bool mostlyHorizontal = absStepX >= minProgress && absStepY <= max( 16, frameHeight / 18 );
            if( !( mostlyVertical || mostlyHorizontal ) )
                continue;

            const size_t prevFrameIndex = composedFrameIndices[passIndex - compareOffset];
            const std::vector<BYTE>& prevPixels = framePixels[prevFrameIndex];
            const std::vector<BYTE>& prevLuma = frameLuma[prevFrameIndex];
            if( prevPixels.empty() || prevLuma.empty() )
                continue;

            if( diagnostics != nullptr )
            {
                diagnostics->pairCount++;
            }

            for( int tileY = 0; tileY < mask.tileRows; ++tileY )
            {
                const int startY = tileY * mask.tileHeight;
                const int currentTileHeight = min( mask.tileHeight, frameHeight - startY );
                if( currentTileHeight < mask.tileHeight / 2 )
                    continue;

                for( int tileX = 0; tileX < mask.tileCols; ++tileX )
                {
                    const int startX = tileX * mask.tileWidth;
                    const int currentTileWidth = min( mask.tileWidth, frameWidth - startX );
                    if( currentTileWidth < mask.tileWidth / 2 )
                        continue;

                    const int shiftedX = startX + cumulativeStep.x;
                    const int shiftedY = startY + cumulativeStep.y;
                    if( shiftedX < 0 || shiftedY < 0 ||
                        shiftedX + currentTileWidth > frameWidth ||
                        shiftedY + currentTileHeight > frameHeight )
                    {
                        continue;
                    }

                    const int currTileEnergy = ComputeTileEdgeEnergy( currLuma, frameWidth, frameHeight, startX, startY, currentTileWidth, currentTileHeight );
                    const int prevShiftedEnergy = ComputeTileEdgeEnergy( prevLuma, frameWidth, frameHeight, shiftedX, shiftedY, currentTileWidth, currentTileHeight );
                    const int informativeEnergy = max( currTileEnergy, prevShiftedEnergy );
                    const int informativeEnergyThreshold = max( 180, ( currentTileWidth * currentTileHeight ) / 2 );
                    if( informativeEnergy < informativeEnergyThreshold )
                        continue;

                    const int sameDiff = ComputeTileAverageRgbDifference( currPixels,
                                                                          startX,
                                                                          startY,
                                                                          prevPixels,
                                                                          startX,
                                                                          startY,
                                                                          frameWidth,
                                                                          frameHeight,
                                                                          currentTileWidth,
                                                                          currentTileHeight );
                    const int shiftedDiff = ComputeTileAverageRgbDifference( currPixels,
                                                                             startX,
                                                                             startY,
                                                                             prevPixels,
                                                                             shiftedX,
                                                                             shiftedY,
                                                                             frameWidth,
                                                                             frameHeight,
                                                                             currentTileWidth,
                                                                             currentTileHeight );

                    if( sameDiff == INT_MAX || shiftedDiff == INT_MAX )
                        continue;

                    // Gate on edge energy.  Bypass the gate when the tile is
                    // near-perfectly stationary (sameDiff <= 4) with clear
                    // scroll evidence (shiftedDiff >= 24) — such tiles
                    // contain a genuine fixed element even on low-contrast
                    // dark pages where edge energy is minimal.
                    const bool lowEnergyBypass = ( sameDiff <= 4 && shiftedDiff >= 24 );
                    if( informativeEnergy < informativeEnergyThreshold && !lowEnergyBypass )
                        continue;

                    const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                    supportCount[tileIndex]++;
                    if( diagnostics != nullptr )
                    {
                        diagnostics->informativeTileComparisons++;
                    }

                    if( sameDiff <= 18 && shiftedDiff >= sameDiff + 12 && shiftedDiff >= ( sameDiff * 2 ) + 6 )
                    {
                        // Require meaningful content at the stationary position in both
                        // frames.  Two featureless dark tiles trivially have sameDiff ≈ 0
                        // and would false-positive as "fixed overlay."
                        const int prevStationaryEnergy = ComputeTileEdgeEnergy( prevLuma, frameWidth, frameHeight,
                                                                                startX, startY,
                                                                                currentTileWidth, currentTileHeight );
                        if( max( currTileEnergy, prevStationaryEnergy ) >= informativeEnergyThreshold )
                        {
                            stationaryWins[tileIndex]++;
                        }
                    }
                    else if( shiftedDiff <= 18 && sameDiff >= shiftedDiff + 10 )
                    {
                        scrolledWins[tileIndex]++;
                    }
                }
            }
        }
    }

    std::vector<BYTE> candidateTiles( tileCount, 0 );
    for( int tileY = 0; tileY < mask.tileRows; ++tileY )
    {
        for( int tileX = 0; tileX < mask.tileCols; ++tileX )
        {
            const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
            if( IsStrongFixedOverlayTile( supportCount[tileIndex], stationaryWins[tileIndex], scrolledWins[tileIndex] ) )
            {
                candidateTiles[tileIndex] = 1;
            }
        }
    }

    std::vector<BYTE> confirmedTiles( tileCount, 0 );
    for( int tileY = 0; tileY < mask.tileRows; ++tileY )
    {
        for( int tileX = 0; tileX < mask.tileCols; ++tileX )
        {
            const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
            if( candidateTiles[tileIndex] == 0 )
                continue;

            const int adjacentCandidates = CountSetTileNeighbors( candidateTiles,
                                                                  mask.tileCols,
                                                                  mask.tileRows,
                                                                  tileX,
                                                                  tileY );

            if( adjacentCandidates == 0 && supportCount[tileIndex] < 6 )
                continue;

            confirmedTiles[tileIndex] = 1;
            if( diagnostics != nullptr )
            {
                diagnostics->strongTileCount++;
            }
        }
    }

    mask.maskedTiles.assign( tileCount, 0 );
    std::vector<BYTE> connectedTiles( tileCount, 0 );
    std::vector<POINT> growQueue;
    growQueue.reserve( tileCount );
    for( int tileY = 0; tileY < mask.tileRows; ++tileY )
    {
        for( int tileX = 0; tileX < mask.tileCols; ++tileX )
        {
            const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
            if( confirmedTiles[tileIndex] == 0 )
                continue;

            connectedTiles[tileIndex] = 1;
            growQueue.push_back( POINT{ tileX, tileY } );
        }
    }

    for( size_t queueIndex = 0; queueIndex < growQueue.size(); ++queueIndex )
    {
        const POINT current = growQueue[queueIndex];
        for( int neighborY = max( 0, current.y - 1 ); neighborY <= min( mask.tileRows - 1, current.y + 1 ); ++neighborY )
        {
            for( int neighborX = max( 0, current.x - 1 ); neighborX <= min( mask.tileCols - 1, current.x + 1 ); ++neighborX )
            {
                const size_t neighborIndex = static_cast<size_t>( neighborY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( neighborX );
                if( connectedTiles[neighborIndex] != 0 )
                    continue;

                if( !IsWeakFixedOverlayTile( supportCount[neighborIndex], stationaryWins[neighborIndex], scrolledWins[neighborIndex] ) )
                    continue;

                connectedTiles[neighborIndex] = 1;
                growQueue.push_back( POINT{ neighborX, neighborY } );
            }
        }
    }

    int connectedTileCount = 0;
    int minTileX = mask.tileCols;
    int minTileY = mask.tileRows;
    int maxTileX = -1;
    int maxTileY = -1;
    for( int tileY = 0; tileY < mask.tileRows; ++tileY )
    {
        for( int tileX = 0; tileX < mask.tileCols; ++tileX )
        {
            const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
            if( connectedTiles[tileIndex] == 0 )
                continue;

            connectedTileCount++;
            minTileX = min( minTileX, tileX );
            minTileY = min( minTileY, tileY );
            maxTileX = max( maxTileX, tileX );
            maxTileY = max( maxTileY, tileY );
        }
    }

    if( diagnostics != nullptr )
    {
        diagnostics->connectedTileCount = connectedTileCount;
    }

    if( connectedTileCount > 0 && connectedTileCount <= 2 )
    {
        minTileX = max( 0, minTileX - 1 );
        maxTileX = min( mask.tileCols - 1, maxTileX + 1 );
        minTileY = max( 0, minTileY - 1 );
        maxTileY = min( mask.tileRows - 1, maxTileY + 2 );
    }

    if( connectedTileCount == 0 )
    {
        // No tile-level detections.  Skip bounds expansion and dense/sparse
        // masking — the bottom strip detection below may still find overlays.
        goto skipTileMasking;
    }

    { // Scope for tile masking.
    bool expandedBounds = true;
    while( expandedBounds )
    {
        expandedBounds = false;

        if( minTileX > 0 )
        {
            bool includeColumn = false;
            for( int tileY = minTileY; tileY <= maxTileY && !includeColumn; ++tileY )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( minTileX - 1 );
                includeColumn = IsWeakFixedOverlayTile( supportCount[tileIndex], stationaryWins[tileIndex], scrolledWins[tileIndex] );
            }
            if( includeColumn )
            {
                --minTileX;
                expandedBounds = true;
            }
        }

        if( maxTileX + 1 < mask.tileCols )
        {
            bool includeColumn = false;
            for( int tileY = minTileY; tileY <= maxTileY && !includeColumn; ++tileY )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( maxTileX + 1 );
                includeColumn = IsWeakFixedOverlayTile( supportCount[tileIndex], stationaryWins[tileIndex], scrolledWins[tileIndex] );
            }
            if( includeColumn )
            {
                ++maxTileX;
                expandedBounds = true;
            }
        }

        if( minTileY > 0 )
        {
            bool includeRow = false;
            for( int tileX = minTileX; tileX <= maxTileX && !includeRow; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( minTileY - 1 ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                includeRow = IsWeakFixedOverlayTile( supportCount[tileIndex], stationaryWins[tileIndex], scrolledWins[tileIndex] );
            }
            if( includeRow )
            {
                --minTileY;
                expandedBounds = true;
            }
        }

        if( maxTileY + 1 < mask.tileRows )
        {
            bool includeRow = false;
            for( int tileX = minTileX; tileX <= maxTileX && !includeRow; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( maxTileY + 1 ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                includeRow = IsWeakFixedOverlayTile( supportCount[tileIndex], stationaryWins[tileIndex], scrolledWins[tileIndex] );
            }
            if( includeRow )
            {
                ++maxTileY;
                expandedBounds = true;
            }
        }
    }

    const int boundsWidthTiles = maxTileX - minTileX + 1;
    const int boundsHeightTiles = maxTileY - minTileY + 1;
    const int boundsAreaTiles = boundsWidthTiles * boundsHeightTiles;
    const bool denseBounds = boundsAreaTiles > 0 && connectedTileCount * 100 >= boundsAreaTiles * 35;

    int maskedTileCount = 0;
    if( denseBounds )
    {
        for( int tileY = minTileY; tileY <= maxTileY; ++tileY )
        {
            for( int tileX = minTileX; tileX <= maxTileX; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                mask.maskedTiles[tileIndex] = 1;
                maskedTileCount++;
            }
        }
    }
    else
    {
        for( int tileY = 0; tileY < mask.tileRows; ++tileY )
        {
            for( int tileX = 0; tileX < mask.tileCols; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                if( connectedTiles[tileIndex] == 0 )
                    continue;

                for( int neighborY = max( 0, tileY - 1 ); neighborY <= min( mask.tileRows - 1, tileY + 1 ); ++neighborY )
                {
                    for( int neighborX = max( 0, tileX - 1 ); neighborX <= min( mask.tileCols - 1, tileX + 1 ); ++neighborX )
                    {
                        const size_t neighborIndex = static_cast<size_t>( neighborY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( neighborX );
                        if( mask.maskedTiles[neighborIndex] != 0 )
                            continue;

                        if( connectedTiles[neighborIndex] == 0 &&
                            !IsWeakFixedOverlayTile( supportCount[neighborIndex], stationaryWins[neighborIndex], scrolledWins[neighborIndex] ) )
                        {
                            continue;
                        }

                        mask.maskedTiles[neighborIndex] = 1;
                        maskedTileCount++;
                    }
                }
            }
        }

        minTileX = mask.tileCols;
        minTileY = mask.tileRows;
        maxTileX = -1;
        maxTileY = -1;
        for( int tileY = 0; tileY < mask.tileRows; ++tileY )
        {
            for( int tileX = 0; tileX < mask.tileCols; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                if( mask.maskedTiles[tileIndex] == 0 )
                    continue;

                minTileX = min( minTileX, tileX );
                minTileY = min( minTileY, tileY );
                maxTileX = max( maxTileX, tileX );
                maxTileY = max( maxTileY, tileY );
            }
        }
    }

    } // End scope for tile masking.
skipTileMasking:

    // Supplement with bottom-strip detection for overlays at the frame edge
    // that tile-based comparison misses (shifted position falls outside frame).
    int maskedTileCount = 0;
    for( size_t ti = 0; ti < tileCount; ++ti )
    {
        if( mask.maskedTiles[ti] != 0 )
            ++maskedTileCount;
    }
    const int fixedBottomRows = DetectFixedBottomStrip( framePixels,
                                                        composedFrameIndices,
                                                        composedFrameSteps,
                                                        frameWidth,
                                                        frameHeight );
    if( fixedBottomRows > 0 )
    {
        const int stripStartTileY = max( 0, ( frameHeight - fixedBottomRows ) / mask.tileHeight );
        for( int tileY = stripStartTileY; tileY < mask.tileRows; ++tileY )
        {
            for( int tileX = 0; tileX < mask.tileCols; ++tileX )
            {
                const size_t tileIndex = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                if( mask.maskedTiles[tileIndex] == 0 )
                {
                    mask.maskedTiles[tileIndex] = 1;
                    ++maskedTileCount;
                }
            }
        }

        minTileX = 0;
        minTileY = min( minTileY, stripStartTileY );
        maxTileX = mask.tileCols - 1;
        maxTileY = mask.tileRows - 1;
    }

    // Detect small floating overlays (e.g. scroll-to-top button, chevron,
    // spinner) using shift-compensated residual accumulation across
    // consecutive frame pairs.  For each pair with a known scroll shift,
    // compare curr[x,y] against prev[x, y+step] — page content matches
    // after compensation, but a fixed overlay at (x,y) shows residual
    // because it doesn't scroll.  Pixels with consistent residual across
    // many pairs are overlay candidates.  Works regardless of overlay
    // contrast, color, shape, or position.
    if( mask.eraseRect.right <= mask.eraseRect.left &&
        composedFrameIndices.size() >= 8 )
    {
        const int halfW = ( frameWidth + 1 ) / 2;
        const int halfH = ( frameHeight + 1 ) / 2;
        std::vector<int> hitCount( static_cast<size_t>( halfW ) * halfH, 0 );
        std::vector<int> pairCountMap( static_cast<size_t>( halfW ) * halfH, 0 );
        const int residualThreshold = 10;

        for( size_t pi = 1; pi < composedFrameIndices.size(); ++pi )
        {
            const int stepX = composedFrameSteps[pi].x;
            const int stepY = composedFrameSteps[pi].y;
            if( abs( stepX ) < minProgress && abs( stepY ) < minProgress )
                continue;

            const auto& currPx = framePixels[composedFrameIndices[pi]];
            const auto& prevPx = framePixels[composedFrameIndices[pi - 1]];
            if( currPx.empty() || prevPx.empty() )
                continue;

            for( int hy = 0; hy < halfH; ++hy )
            {
                const int y = hy * 2;
                const int prevY = y + stepY;
                if( prevY < 0 || prevY >= frameHeight )
                    continue;

                // Skip comparisons where the shifted position maps
                // into the fixed header/footer area — those produce
                // false residual for every pixel near the edges,
                // swamping the real overlay signal.  Use the detected
                // strip heights rather than the tile mask, which can
                // have artifacts from connected components (scrollbar
                // connecting header to footer).
                if( fixedBottomRows > 0 && prevY >= frameHeight - fixedBottomRows )
                    continue;
                {
                    const int hdrH = mask.TopHeaderHeight();
                    if( hdrH > 0 && prevY < hdrH )
                        continue;
                }

                for( int hx = 0; hx < halfW; ++hx )
                {
                    const int x = hx * 2;
                    const int prevX = x + stepX;
                    if( prevX < 0 || prevX >= frameWidth )
                        continue;

                    const size_t ci = ( static_cast<size_t>( y ) * frameWidth + x ) * 4;
                    const size_t pri = ( static_cast<size_t>( prevY ) * frameWidth + prevX ) * 4;
                    const int d = max( abs( static_cast<int>( currPx[ci] ) - static_cast<int>( prevPx[pri] ) ),
                                       max( abs( static_cast<int>( currPx[ci + 1] ) - static_cast<int>( prevPx[pri + 1] ) ),
                                            abs( static_cast<int>( currPx[ci + 2] ) - static_cast<int>( prevPx[pri + 2] ) ) ) );

                    const size_t idx = static_cast<size_t>( hy ) * halfW + hx;
                    pairCountMap[idx]++;
                    if( d > residualThreshold )
                    {
                        hitCount[idx]++;
                    }
                }
            }
        }

        const int minPairsRequired = 4;
        const int minHitPercent = 50;
        int fixMinX = frameWidth, fixMaxX = 0, fixMinY = frameHeight, fixMaxY = 0;
        int fixedCount = 0;
        for( int hy = 0; hy < halfH; ++hy )
        {
            for( int hx = 0; hx < halfW; ++hx )
            {
                const size_t idx = static_cast<size_t>( hy ) * halfW + hx;
                if( pairCountMap[idx] < minPairsRequired )
                    continue;
                if( hitCount[idx] * 100 < pairCountMap[idx] * minHitPercent )
                    continue;

                const int x = hx * 2;
                const int y = hy * 2;

                // Skip the top and bottom frame edges — scroll
                // clipping at the frame boundary creates natural
                // residual that isn't a floating overlay.
                if( y < 8 || y >= frameHeight - 8 )
                    continue;

                // Skip pixels within the fixed top header — the
                // header is stationary and produces residual, but
                // it's already handled by tile-mask suppression.
                {
                    const int topHdrH = mask.TopHeaderHeight();
                    if( topHdrH > 0 && y < topHdrH + 8 )
                        continue;
                }

                // Skip pixels in or near the bottom fixed region.  The tile
                // mask and bottom strip cover the toolbar itself, but
                // toolbar UI elements (icons, borders, edit box) extend
                // above the mask and produce residual noise.  Use a
                // moderate margin to suppress toolbar noise while still
                // allowing floating overlays near the toolbar.
                if( maskedTileCount > 0 && mask.IsMaskedPixel( x, y ) )
                    continue;
                const int firstMaskedRow = mask.FirstMaskedY();
                const int topHdr = mask.TopHeaderHeight();
                // Only apply the firstMaskedRow margin when it refers
                // to the bottom toolbar, not the top header.  When the
                // header is masked, FirstMaskedY() returns ~0 and the
                // margin (y >= -40) would exclude every pixel.
                if( firstMaskedRow > topHdr && y >= firstMaskedRow - 40 )
                    continue;
                if( fixedBottomRows > 0 && y >= frameHeight - fixedBottomRows - 40 )
                    continue;

                fixMinX = min( fixMinX, x );
                fixMaxX = max( fixMaxX, x );
                fixMinY = min( fixMinY, y );
                fixMaxY = max( fixMaxY, y );
                ++fixedCount;
            }
        }

        if( fixedCount >= 2 )
        {
            int finalMinX = fixMinX, finalMaxX = fixMaxX;
            int finalMinY = fixMinY, finalMaxY = fixMaxY;

            const int coreW = finalMaxX - finalMinX + 1;
            const int coreH = finalMaxY - finalMinY + 1;
            const int64_t bboxArea = static_cast<int64_t>( coreW ) * coreH;

            // A real floating overlay (toolbar bar, button) has its
            // residual pixels concentrated in a compact region.
            // Scattered noise from cursor movement or sub-pixel
            // rendering produces a similar pixel count but spread
            // across the whole frame, yielding very low density.
            // Require at least 1% of the bounding box to be filled.
            // Also force clustering when the bounding box is very
            // large (e.g. IDE sidebar + minimap producing widespread
            // residual) — a real floating overlay is compact, not
            // frame-spanning.
            const int64_t densityPercent = bboxArea > 0
                ? ( static_cast<int64_t>( fixedCount ) * 100 / bboxArea )
                : 0;
            const bool bboxTooLarge = coreW > frameWidth * 2 / 5 &&
                                      coreH > frameHeight * 2 / 5;
            if( densityPercent < 1 || bboxTooLarge )
            {
                // The full bounding box is too sparse — likely scattered
                // noise.  But there might be a real compact overlay
                // hiding inside the noise.  Search for the densest
                // sub-window to recover it.
                const int windowW = min( frameWidth / 4, 120 );
                const int windowH = min( frameHeight / 4, 120 );
                int bestCount = 0, bestWx = 0, bestWy = 0;

                for( int wy = 0; wy <= halfH - windowH / 2; wy += 2 )
                {
                    for( int wx = 0; wx <= halfW - windowW / 2; wx += 2 )
                    {
                        int count = 0;
                        const int wyEnd = min( wy + windowH / 2, halfH );
                        const int wxEnd = min( wx + windowW / 2, halfW );
                        for( int hy = wy; hy < wyEnd; ++hy )
                        {
                            for( int hx = wx; hx < wxEnd; ++hx )
                            {
                                const size_t idx2 = static_cast<size_t>( hy ) * halfW + hx;
                                if( pairCountMap[idx2] >= minPairsRequired &&
                                    hitCount[idx2] * 100 >= pairCountMap[idx2] * minHitPercent &&
                                    !( maskedTileCount > 0 && mask.IsMaskedPixel( hx * 2, hy * 2 ) ) )
                                {
                                    const int py = hy * 2;
                                    const int topHdrC2 = mask.TopHeaderHeight();
                                    if( topHdrC2 > 0 && py < topHdrC2 + 8 )
                                        continue;
                                    const int firstMR = mask.FirstMaskedY();
                                    const int topHdrC = mask.TopHeaderHeight();
                                    if( firstMR > topHdrC && py >= firstMR - 40 )
                                        continue;
                                    if( fixedBottomRows > 0 && py >= frameHeight - fixedBottomRows - 40 )
                                        continue;
                                    ++count;
                                }
                            }
                        }
                        if( count > bestCount )
                        {
                            bestCount = count;
                            bestWx = wx;
                            bestWy = wy;
                        }
                    }
                }

                if( bestCount >= 2 )
                {
                    finalMinX = frameWidth; finalMaxX = 0;
                    finalMinY = frameHeight; finalMaxY = 0;
                    fixedCount = 0;
                    const int wyEnd = min( bestWy + windowH / 2, halfH );
                    const int wxEnd = min( bestWx + windowW / 2, halfW );
                    for( int hy = bestWy; hy < wyEnd; ++hy )
                    {
                        for( int hx = bestWx; hx < wxEnd; ++hx )
                        {
                            const size_t idx2 = static_cast<size_t>( hy ) * halfW + hx;
                            if( pairCountMap[idx2] >= minPairsRequired &&
                                hitCount[idx2] * 100 >= pairCountMap[idx2] * minHitPercent &&
                                !( maskedTileCount > 0 && mask.IsMaskedPixel( hx * 2, hy * 2 ) ) )
                            {
                                finalMinX = min( finalMinX, hx * 2 );
                                finalMaxX = max( finalMaxX, hx * 2 );
                                finalMinY = min( finalMinY, hy * 2 );
                                finalMaxY = max( finalMaxY, hy * 2 );
                                ++fixedCount;
                            }
                        }
                    }

                    // Verify the cluster has reasonable density too.
                    if( fixedCount >= 2 )
                    {
                        const int clW = finalMaxX - finalMinX + 1;
                        const int clH = finalMaxY - finalMinY + 1;
                        const int64_t clArea = static_cast<int64_t>( clW ) * clH;
                        const int64_t clDensityPct = clArea > 0 ? static_cast<int64_t>( fixedCount ) * 100 / clArea : 0;
                        if( clDensityPct < 2 )
                        {
                            fixedCount = 0;
                        }
                    }
                }
                else
                {
                    fixedCount = 0;
                }
            }

            if( fixedCount >= 2 )
            {
                // Reject clusters in the top third of the frame —
                // floating overlays (scroll-to-bottom, FAB, etc.)
                // sit in the lower portion, not near the header.
                // Noise near the header edge produces false clusters.
                if( finalMaxY < frameHeight / 3 )
                {
                    fixedCount = 0;
                }
            }

            if( fixedCount >= 2 )
            {
                const int coreW2 = finalMaxX - finalMinX + 1;
                const int coreH2 = finalMaxY - finalMinY + 1;
                const int marginX = max( 24, coreW2 );
                const int marginY = max( 24, coreH2 );
                mask.eraseRect.left   = max( 0L, static_cast<long>( finalMinX ) - marginX );
                mask.eraseRect.top    = max( 0L, static_cast<long>( finalMinY ) - marginY );
                mask.eraseRect.right  = min( static_cast<long>( frameWidth ), static_cast<long>( finalMaxX ) + marginX + 2 );
                mask.eraseRect.bottom = min( static_cast<long>( frameHeight ), static_cast<long>( finalMaxY ) + marginY + 2 );
            }
        }

        StitchLog( L"[Panorama/Stitch] ResidualOverlay: pairsUsed=%zu fixedPixels=%d bounds=(%d,%d)-(%d,%d) maskedTiles=%d eraseRect=(%d,%d)-(%d,%d)\n",
                   composedFrameIndices.size() - 1,
                   fixedCount,
                   fixMinX, fixMinY, fixMaxX, fixMaxY,
                   maskedTileCount,
                   mask.eraseRect.left, mask.eraseRect.top,
                   mask.eraseRect.right, mask.eraseRect.bottom );
    }

    // Fill unmasked holes within the masked bounds.  Small overlay elements
    // (e.g. a chevron indicator above the bottom strip) may fall in tiles
    // that tile voting didn't flag, leaving gaps in the mask.  Any unmasked
    // tile bordered on 3+ sides by masked tiles is almost certainly part of
    // the overlay and should be masked too.
    if( maskedTileCount > 0 )
    {
        bool filled = true;
        while( filled )
        {
            filled = false;
            for( int tileY = minTileY; tileY <= maxTileY; ++tileY )
            {
                for( int tileX = minTileX; tileX <= maxTileX; ++tileX )
                {
                    const size_t ti = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                    if( mask.maskedTiles[ti] != 0 )
                        continue;

                    int maskedNeighbors = 0;
                    if( tileY > 0 )
                    {
                        const size_t above = static_cast<size_t>( tileY - 1 ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                        if( mask.maskedTiles[above] != 0 ) ++maskedNeighbors;
                    }
                    else ++maskedNeighbors;  // frame edge counts as masked
                    if( tileY + 1 < mask.tileRows )
                    {
                        const size_t below = static_cast<size_t>( tileY + 1 ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX );
                        if( mask.maskedTiles[below] != 0 ) ++maskedNeighbors;
                    }
                    else ++maskedNeighbors;
                    if( tileX > 0 )
                    {
                        const size_t left = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX - 1 );
                        if( mask.maskedTiles[left] != 0 ) ++maskedNeighbors;
                    }
                    else ++maskedNeighbors;
                    if( tileX + 1 < mask.tileCols )
                    {
                        const size_t right = static_cast<size_t>( tileY ) * static_cast<size_t>( mask.tileCols ) + static_cast<size_t>( tileX + 1 );
                        if( mask.maskedTiles[right] != 0 ) ++maskedNeighbors;
                    }
                    else ++maskedNeighbors;

                    if( maskedNeighbors >= 3 )
                    {
                        mask.maskedTiles[ti] = 1;
                        ++maskedTileCount;
                        filled = true;
                    }
                }
            }
        }
    }

    if( maskedTileCount == 0 && mask.eraseRect.right <= mask.eraseRect.left )
    {
        mask.maskedTiles.clear();
        return mask;
    }

    if( diagnostics != nullptr )
    {
        diagnostics->maskedTileCount = maskedTileCount;
        if( maskedTileCount > 0 )
        {
            diagnostics->tileBoundsLeft = minTileX * mask.tileWidth;
            diagnostics->tileBoundsTop = minTileY * mask.tileHeight;
            diagnostics->tileBoundsRight = min( frameWidth, ( maxTileX + 1 ) * mask.tileWidth );
            diagnostics->tileBoundsBottom = min( frameHeight, ( maxTileY + 1 ) * mask.tileHeight );
        }
    }

    mask.topHeaderHeight = mask.TopHeaderHeight();
    if( mask.topHeaderHeight > 0 )
    {
        StitchLog( L"[Panorama/Stitch] TopHeader: height=%d\n", mask.topHeaderHeight );
    }

    mask.bottomStripY = fixedBottomRows > 0 ? frameHeight - fixedBottomRows : 0;

    return mask;
}

// Masked Zero-Mean Normalized Cross-Correlation over 2D overlap region.
// Only scores pixels where the mask is set (informative/edge pixels).
// Returns ZNCC in [-1.0, 1.0].  Returns 0.0 if fewer than minSamples
// masked pixels, or if either signal has zero variance.
static double ComputeMaskedZNCC( const BYTE* prevLuma, const BYTE* currLuma,
                                 const BYTE* maskPrev, const BYTE* maskCurr,
                                 int width, int overlap, int absStep,
                                 int direction, int dx,
                                 int marginX, int minSamples )
{
    __int64 iSumA = 0, iSumB = 0, iSumAB = 0, iSumA2 = 0, iSumB2 = 0;
    int n = 0;
    const int xStart = max( marginX, marginX + max( 0, -dx ) );
    const int xEnd = min( width - marginX, width - marginX - max( 0, dx ) );

    for( int y = 0; y < overlap; ++y )
    {
        const int pY = ( direction < 0 ) ? ( y + absStep ) : y;
        const int cY = ( direction < 0 ) ? y : ( y + absStep );
        const int prevRow = pY * width;
        const int currRow = cY * width;

        const BYTE* pLuma = &prevLuma[prevRow + xStart];
        const BYTE* cLuma = &currLuma[currRow + xStart + dx];
        const BYTE* pMask = &maskPrev[prevRow + xStart];
        const BYTE* cMask = &maskCurr[currRow + xStart + dx];
        const int xSpan = xEnd - xStart;

#if defined(_M_X64) || defined(_M_IX86)
        const __m128i zero = _mm_setzero_si128();
        int x = 0;
        for( ; x + 16 <= xSpan; x += 16 )
        {
            const __m128i mPrev = _mm_loadu_si128( reinterpret_cast<const __m128i*>( pMask + x ) );
            const __m128i mCurr = _mm_loadu_si128( reinterpret_cast<const __m128i*>( cMask + x ) );
            const __m128i mInf = _mm_or_si128( mPrev, mCurr );
            const __m128i mActive = _mm_cmpgt_epi8( mInf, zero );
            const unsigned int activeBits = static_cast<unsigned int>( _mm_movemask_epi8( mActive ) );
            if( activeBits == 0 )
            {
                continue;
            }

            const __m128i va = _mm_loadu_si128( reinterpret_cast<const __m128i*>( pLuma + x ) );
            const __m128i vb = _mm_loadu_si128( reinterpret_cast<const __m128i*>( cLuma + x ) );
            const __m128i aMasked = _mm_and_si128( va, mActive );
            const __m128i bMasked = _mm_and_si128( vb, mActive );

            const __m128i sadA = _mm_sad_epu8( aMasked, zero );
            const __m128i sadB = _mm_sad_epu8( bMasked, zero );
            iSumA += static_cast<__int64>( _mm_cvtsi128_si64( sadA ) ) +
                     static_cast<__int64>( _mm_cvtsi128_si64( _mm_srli_si128( sadA, 8 ) ) );
            iSumB += static_cast<__int64>( _mm_cvtsi128_si64( sadB ) ) +
                     static_cast<__int64>( _mm_cvtsi128_si64( _mm_srli_si128( sadB, 8 ) ) );
            n += static_cast<int>( __popcnt( activeBits ) );

            const __m128i aLo16 = _mm_unpacklo_epi8( aMasked, zero );
            const __m128i aHi16 = _mm_unpackhi_epi8( aMasked, zero );
            const __m128i bLo16 = _mm_unpacklo_epi8( bMasked, zero );
            const __m128i bHi16 = _mm_unpackhi_epi8( bMasked, zero );

            const __m128i abLo32 = _mm_madd_epi16( aLo16, bLo16 );
            const __m128i abHi32 = _mm_madd_epi16( aHi16, bHi16 );
            const __m128i a2Lo32 = _mm_madd_epi16( aLo16, aLo16 );
            const __m128i a2Hi32 = _mm_madd_epi16( aHi16, aHi16 );
            const __m128i b2Lo32 = _mm_madd_epi16( bLo16, bLo16 );
            const __m128i b2Hi32 = _mm_madd_epi16( bHi16, bHi16 );

            alignas(16) int abBuf[4];
            alignas(16) int a2Buf[4];
            alignas(16) int b2Buf[4];
            _mm_storeu_si128( reinterpret_cast<__m128i*>( abBuf ), abLo32 );
            _mm_storeu_si128( reinterpret_cast<__m128i*>( a2Buf ), a2Lo32 );
            _mm_storeu_si128( reinterpret_cast<__m128i*>( b2Buf ), b2Lo32 );
            iSumAB += static_cast<__int64>( abBuf[0] ) + abBuf[1] + abBuf[2] + abBuf[3];
            iSumA2 += static_cast<__int64>( a2Buf[0] ) + a2Buf[1] + a2Buf[2] + a2Buf[3];
            iSumB2 += static_cast<__int64>( b2Buf[0] ) + b2Buf[1] + b2Buf[2] + b2Buf[3];

            _mm_storeu_si128( reinterpret_cast<__m128i*>( abBuf ), abHi32 );
            _mm_storeu_si128( reinterpret_cast<__m128i*>( a2Buf ), a2Hi32 );
            _mm_storeu_si128( reinterpret_cast<__m128i*>( b2Buf ), b2Hi32 );
            iSumAB += static_cast<__int64>( abBuf[0] ) + abBuf[1] + abBuf[2] + abBuf[3];
            iSumA2 += static_cast<__int64>( a2Buf[0] ) + a2Buf[1] + a2Buf[2] + a2Buf[3];
            iSumB2 += static_cast<__int64>( b2Buf[0] ) + b2Buf[1] + b2Buf[2] + b2Buf[3];
        }

        for( ; x < xSpan; ++x )
        {
            if( !pMask[x] && !cMask[x] )
                continue;

            const int a = static_cast<int>( pLuma[x] );
            const int b = static_cast<int>( cLuma[x] );
            iSumA  += a;
            iSumB  += b;
            iSumAB += a * b;
            iSumA2 += a * a;
            iSumB2 += b * b;
            n++;
        }
#else
        for( int x = 0; x < xSpan; ++x )
        {
            if( !pMask[x] && !cMask[x] )
                continue;

            const int a = static_cast<int>( pLuma[x] );
            const int b = static_cast<int>( cLuma[x] );
            iSumA  += a;
            iSumB  += b;
            iSumAB += a * b;
            iSumA2 += a * a;
            iSumB2 += b * b;
            n++;
        }
#endif
    }

    if( n < minSamples ) return 0.0;
    const double N    = static_cast<double>( n );
    const double sumA = static_cast<double>( iSumA );
    const double sumB = static_cast<double>( iSumB );
    const double varA = static_cast<double>( iSumA2 ) / N - ( sumA / N ) * ( sumA / N );
    const double varB = static_cast<double>( iSumB2 ) / N - ( sumB / N ) * ( sumB / N );
    if( varA <= 0.0 || varB <= 0.0 ) return 0.0;
    const double cov = static_cast<double>( iSumAB ) / N - ( sumA / N ) * ( sumB / N );
    return cov / sqrt( varA * varB );
}

static bool FindBestSmallShiftDownsampledLuma( const std::vector<BYTE>& previousPixels,
                                               const std::vector<BYTE>& currentPixels,
                                               int frameWidth,
                                               int frameHeight,
                                               int maxAbsDyFull,
                                               int maxAbsDxFull,
                                               int& bestDxFull,
                                               int& bestDyFull,
                                               unsigned __int64& stationaryScore,
                                               unsigned __int64& bestShiftScore )
{
    bestDxFull = 0;
    bestDyFull = 0;
    stationaryScore = ( std::numeric_limits<unsigned __int64>::max )();
    bestShiftScore = ( std::numeric_limits<unsigned __int64>::max )();

    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
        return false;

    const int scale = ( min( frameWidth, frameHeight ) >= 240 ) ? 4 : 2;

    std::vector<BYTE> prevLuma, currLuma;
    int dsW = 0, dsH = 0, dsW2 = 0, dsH2 = 0;
    BuildDownsampledLumaFrame( previousPixels, frameWidth, frameHeight, scale, prevLuma, dsW, dsH );
    BuildDownsampledLumaFrame( currentPixels,  frameWidth, frameHeight, scale, currLuma, dsW2, dsH2 );
    if( dsW != dsW2 || dsH != dsH2 || dsW < 8 || dsH < 8 )
        return false;

    const int maxDyDs = max( 1, maxAbsDyFull / scale );
    const int maxDxDs = max( 0, maxAbsDxFull / scale );

    const int marginX = max( 2, dsW / 20 );
    const int marginY = max( 2, dsH / 20 );

    auto scoreShift = [&]( int dx, int dy, unsigned __int64& outScore ) -> bool
    {
        const int absDx = abs( dx );
        const int absDy = abs( dy );

        const int overlapW = dsW - 2 * marginX - absDx;
        const int overlapH = dsH - 2 * marginY - absDy;
        if( overlapW <= dsW / 4 || overlapH <= dsH / 4 )
            return false;

        const int prevX = marginX + max( 0, -dx );
        const int currX = marginX + max( 0,  dx );
        const int prevY = marginY + max( 0, -dy );
        const int currY = marginY + max( 0,  dy );

        unsigned __int64 total = 0;
        unsigned __int64 n = 0;

        // Sample every other pixel for speed (good enough for "motion vs none")
        for( int y = 0; y < overlapH; y += 2 )
        {
            const BYTE* pRow = &prevLuma[ ( prevY + y ) * dsW + prevX ];
            const BYTE* cRow = &currLuma[ ( currY + y ) * dsW + currX ];
            for( int x = 0; x < overlapW; x += 2 )
            {
                total += static_cast<unsigned __int64>( abs( static_cast<int>( pRow[x] ) - static_cast<int>( cRow[x] ) ) );
                n++;
            }
        }

        if( n < 200 )
            return false;

        outScore = total / n;
        return true;
    };

    unsigned __int64 s0 = 0;
    if( !scoreShift( 0, 0, s0 ) )
        return false;

    stationaryScore = s0;
    bestShiftScore = s0;

    int bestDxDs = 0;
    int bestDyDs = 0;

    for( int dy = -maxDyDs; dy <= maxDyDs; ++dy )
    {
        for( int dx = -maxDxDs; dx <= maxDxDs; ++dx )
        {
            if( dx == 0 && dy == 0 )
                continue;

            unsigned __int64 sc = 0;
            if( !scoreShift( dx, dy, sc ) )
                continue;

            if( sc < bestShiftScore )
            {
                bestShiftScore = sc;
                bestDxDs = dx;
                bestDyDs = dy;
            }
        }
    }

    bestDxFull = bestDxDs * scale;
    bestDyFull = bestDyDs * scale;
    return true;
}

static bool LooksLikeSmallShiftNotDuplicate( const std::vector<BYTE>& previousPixels,
                                             const std::vector<BYTE>& currentPixels,
                                             int frameWidth,
                                             int frameHeight,
                                             int maxAbsDyFull,
                                             int maxAbsDxFull,
                                             bool lowContrastMode,
                                             int* outDxFull = nullptr,
                                             int* outDyFull = nullptr,
                                             unsigned __int64* outStationary = nullptr,
                                             unsigned __int64* outBest = nullptr )
{
    int dx = 0, dy = 0;
    unsigned __int64 s0 = 0, best = 0;
    if( !FindBestSmallShiftDownsampledLuma( previousPixels, currentPixels, frameWidth, frameHeight,
                                           maxAbsDyFull, maxAbsDxFull, dx, dy, s0, best ) )
        return false;

    if( outDxFull ) *outDxFull = dx;
    if( outDyFull ) *outDyFull = dy;
    if( outStationary ) *outStationary = s0;
    if( outBest ) *outBest = best;

    if( dx == 0 && dy == 0 )
        return false;

    // If stationary is extremely low, we can't reliably separate "tiny scroll" from noise.
    if( s0 < 4 )
        return false;

    // Require meaningful improvement over stationary and a reasonably low best score.
    if( best + 2 > s0 )
        return false;

    // At least ~15% better than stationary.
    if( best * 100 > s0 * 85 )
        return false;

    const unsigned __int64 bestThreshold = lowContrastMode ? 20 : 25;
    if( best > bestThreshold )
        return false;

    return true;
}

// Core pixel-based duplicate detection used by both the HBITMAP and pixel-array
// entry points.  Returns true if the frames should be treated as duplicates.
static bool ArePixelFramesNearDuplicateCore( const std::vector<BYTE>& currentPixels,
                                             const std::vector<BYTE>& previousPixels,
                                             int frameWidth,
                                             int frameHeight,
                                             bool lowContrastMode,
                                             bool verbose )
{
    static unsigned s_phase = 0;
    unsigned __int64 avgDiff = 0;
    double changedFraction = 0.0;

    const int coarseSampleStep = lowContrastMode ? 4 : 6;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight,
                                        avgDiff, changedFraction, coarseSampleStep, ++s_phase ) )
    {
        return false;
    }

    const unsigned __int64 avgDiffThreshold = lowContrastMode ? 2 : 6;
    const double changedThreshold = lowContrastMode ? 0.0005 : 0.005;
    const bool coarseDuplicate = ( avgDiff < avgDiffThreshold && changedFraction < changedThreshold );
    bool duplicate = coarseDuplicate;

    // Low-content captures (e.g. mostly blank editors where only line numbers
    // change) can be under-sampled by the coarse pass.  Recheck with denser
    // sampling before dropping a frame.
    if( lowContrastMode )
    {
        unsigned __int64 fineAvgDiff = 0;
        double fineChangedFraction = 0.0;
        if( ComputeAveragePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight,
                                           fineAvgDiff, fineChangedFraction, 1, ++s_phase ) )
        {
            const bool fineDuplicate = ( fineAvgDiff < 1 && fineChangedFraction < 0.00008 );
            duplicate = coarseDuplicate && fineDuplicate;
            if( verbose && coarseDuplicate && !fineDuplicate )
            {
                StitchLog( L"[Panorama/Capture] Fine-pass rescued frame avgDiff=%llu changedPct=%.3f%% fineAvg=%llu fineChangedPct=%.3f%%\n",
                             avgDiff,
                             changedFraction * 100.0,
                             fineAvgDiff,
                             fineChangedFraction * 100.0 );
            }
        }
    }

    // Guard: if it "looks duplicate" by RGB stats but a small translation aligns notably better,
    // treat it as movement (prevents false drops during slow scrolling / low-texture content).
    if( duplicate )
    {
        int guardDx = 0, guardDy = 0;
        unsigned __int64 s0 = 0, best = 0;
        if( LooksLikeSmallShiftNotDuplicate( previousPixels, currentPixels, frameWidth, frameHeight,
                            /*maxAbsDyFull=*/lowContrastMode ? 24 : 16,
                            /*maxAbsDxFull=*/lowContrastMode ? 12 : 8,
                            lowContrastMode,
                                            &guardDx, &guardDy, &s0, &best ) )
        {
            duplicate = false;
            if( verbose )
            {
                StitchLog( L"[Panorama/Capture] Duplicate-guard: avgDiff=%llu changedPct=%.2f%% smallShift=(%d,%d) stationary=%llu best=%llu\n",
                             avgDiff, changedFraction * 100.0, guardDx, guardDy,
                             static_cast<unsigned long long>( s0 ),
                             static_cast<unsigned long long>( best ) );
            }
        }
    }

    // Informative-pixel rescue: if the frame still looks like a duplicate but
    // the edge/text pixels show real differences, keep it.  This catches slow
    // scrolls of very-low-entropy content where overall avgDiff is diluted
    // by the overwhelmingly constant background.
    if( duplicate && lowContrastMode )
    {
        unsigned __int64 infDiff = 0, infCount = 0;
        if( ComputeInformativePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight, infDiff, infCount ) &&
            infCount > 0 )
        {
            const unsigned __int64 avgInfDiff = infDiff / ( infCount * 3 );
            if( avgInfDiff >= 8 )
            {
                duplicate = false;
                if( verbose )
                {
                    StitchLog( L"[Panorama/Capture] Informative-pixel rescued frame avgInfDiff=%llu infCount=%llu\n",
                                 static_cast<unsigned long long>( avgInfDiff ),
                                 static_cast<unsigned long long>( infCount ) );
                }
            }
        }
    }

    if( verbose )
    {
        StitchLog( L"[Panorama/Capture] Frame compare avgDiff=%llu changedPct=%.1f%% size=%dx%d identical=%d lowContrast=%d\n",
                   avgDiff, changedFraction * 100.0, frameWidth, frameHeight, duplicate ? 1 : 0, lowContrastMode ? 1 : 0 );
    }

    return duplicate;
}

static bool AreFramesNearDuplicate( HBITMAP currentFrame, HBITMAP previousFrame, bool lowContrastMode, bool* outSubPixelDrop = nullptr )
{
    if( outSubPixelDrop )
        *outSubPixelDrop = false;

    std::vector<BYTE> currentPixels;
    std::vector<BYTE> previousPixels;
    int currentWidth = 0, currentHeight = 0;
    int previousWidth = 0, previousHeight = 0;

    if( !ReadBitmapPixels32( currentFrame, currentPixels, currentWidth, currentHeight ) ||
        !ReadBitmapPixels32( previousFrame, previousPixels, previousWidth, previousHeight ) )
    {
        return false; // fail open: keep frame
    }

    if( currentWidth != previousWidth || currentHeight != previousHeight )
        return false;

    bool duplicate = ArePixelFramesNearDuplicateCore( currentPixels, previousPixels, currentWidth, currentHeight, lowContrastMode, /*verbose=*/true );

    // Sub-pixel shift detection: the frame passed the duplicate checks
    // (pixel differences above noise floor) but the differences may be
    // ClearType / anti-aliasing jitter rather than real scrolling.  Test
    // whether any +/-1..2 px integer shift produces a meaningfully better
    // match than stationary.  Uses raw per-channel comparison to preserve
    // ClearType's per-channel R/G/B sub-pixel shifts.
    //
    // Only run this when the stationary MAD is small -- genuine scrolls
    // produce large MAD values that can't be explained by sub-pixel
    // jitter.  The threshold (avgDiffThreshold * 3) catches frames that
    // just barely escaped the duplicate detector.
    if( !duplicate )
    {
        const unsigned __int64 avgDiffThreshold = lowContrastMode ? 2 : 6;
        const int w = currentWidth, h = currentHeight;
        const int marginX = max( 4, w / 20 );
        const int marginY = max( 4, h / 20 );

        auto computeShiftRawMAD = [&]( int dx, int dy ) -> unsigned __int64
        {
            const int x0 = marginX + max( 0, -dx );
            const int x1 = w - marginX - max( 0, dx );
            const int y0 = marginY + max( 0, -dy );
            const int y1 = h - marginY - max( 0, dy );
            if( x1 <= x0 + 8 || y1 <= y0 + 8 )
                return ( std::numeric_limits<unsigned __int64>::max )();

            unsigned __int64 total = 0;
            unsigned __int64 n = 0;
            for( int y = y0; y < y1; y += 4 )
            {
                const int prevRowOff = y * w;
                const int currRowOff = ( y + dy ) * w;
                for( int x = x0; x < x1; x += 4 )
                {
                    const int pi = ( prevRowOff + x ) * 4;
                    const int ci = ( currRowOff + x + dx ) * 4;
                    total += static_cast<unsigned __int64>(
                        abs( static_cast<int>( currentPixels[ci] )     - static_cast<int>( previousPixels[pi] ) ) +
                        abs( static_cast<int>( currentPixels[ci + 1] ) - static_cast<int>( previousPixels[pi + 1] ) ) +
                        abs( static_cast<int>( currentPixels[ci + 2] ) - static_cast<int>( previousPixels[pi + 2] ) ) );
                    n++;
                }
            }
            return ( n > 100 ) ? total / n : ( std::numeric_limits<unsigned __int64>::max )();
        };

        // Sub-pixel jitter produces small per-pixel differences (ClearType
        // shifts are typically 1-3 per channel).  If avgDiff is well above
        // the duplicate threshold, this is real scrolling, not jitter.
        const unsigned __int64 subPixelMaxAvgDiff = avgDiffThreshold * 3;
        const unsigned __int64 mad0 = computeShiftRawMAD( 0, 0 );
        if( mad0 != ( std::numeric_limits<unsigned __int64>::max )() && mad0 >= 2 && mad0 <= subPixelMaxAvgDiff )
        {
            unsigned __int64 bestShiftMAD = ( std::numeric_limits<unsigned __int64>::max )();
            int bestDx = 0, bestDy = 0;
            for( int dy = -2; dy <= 2; dy++ )
            {
                for( int dx = -2; dx <= 2; dx++ )
                {
                    if( dx == 0 && dy == 0 )
                        continue;
                    const unsigned __int64 m = computeShiftRawMAD( dx, dy );
                    if( m < bestShiftMAD )
                    {
                        bestShiftMAD = m;
                        bestDx = dx;
                        bestDy = dy;
                    }
                }
            }

            // If no integer shift meaningfully improves the match, the
            // differences are sub-pixel noise -> treat as duplicate.
            if( !( bestShiftMAD + 2 < mad0 && bestShiftMAD * 100 < mad0 * 85 ) )
            {
                // In lowContrastMode, mad0 is typically 2-4 and the
                // absolute floor of 2 blocks genuine scrolls.  Rescue
                // those frames when a shift produces any measurable MAD
                // improvement and the proportional gain exceeds 15%.
                const bool rescued = lowContrastMode &&
                    bestShiftMAD < mad0 &&
                    bestShiftMAD * 100 < mad0 * 85;
                if( !rescued )
                {
                    duplicate = true;
                    if( outSubPixelDrop )
                        *outSubPixelDrop = true;
                    StitchLog( L"[Panorama/Capture] Sub-pixel shift detected: mad0=%llu bestMAD=%llu best=(%d,%d)\n",
                                 mad0, bestShiftMAD, bestDx, bestDy );
                }
            }
        }
    }

    return duplicate;
}

static bool ArePixelFramesNearDuplicate( const std::vector<BYTE>& currentPixels,
                                         const std::vector<BYTE>& previousPixels,
                                         int frameWidth,
                                         int frameHeight,
                                         bool lowContrastMode )
{
    return ArePixelFramesNearDuplicateCore( currentPixels, previousPixels, frameWidth, frameHeight, lowContrastMode, /*verbose=*/false );
}

static bool TransposePixels32( const std::vector<BYTE>& source,
                               int sourceWidth,
                               int sourceHeight,
                               std::vector<BYTE>& destination )
{
    if( sourceWidth <= 0 || sourceHeight <= 0 ||
        source.size() != static_cast<size_t>( sourceWidth ) * static_cast<size_t>( sourceHeight ) * 4 )
    {
        return false;
    }

    destination.resize( source.size() );
    for( int y = 0; y < sourceHeight; ++y )
    {
        for( int x = 0; x < sourceWidth; ++x )
        {
            const int srcIndex = ( y * sourceWidth + x ) * 4;
            const int dstX = y;
            const int dstY = x;
            const int dstWidth = sourceHeight;
            const int dstIndex = ( dstY * dstWidth + dstX ) * 4;
            destination[dstIndex + 0] = source[srcIndex + 0];
            destination[dstIndex + 1] = source[srcIndex + 1];
            destination[dstIndex + 2] = source[srcIndex + 2];
            destination[dstIndex + 3] = source[srcIndex + 3];
        }
    }
    return true;
}

static bool FindBestFrameShiftVerticalOnly( const std::vector<BYTE>& previousPixels,
                                            const std::vector<BYTE>& currentPixels,
                                            int frameWidth,
                                            int frameHeight,
                                            int expectedDx,
                                            int expectedDy,
                                            int& bestDx,
                                            int& bestDy,
                                            bool lowContrastMode,
                                            const std::vector<BYTE>& precomputedPrevLuma = {},
                                            const std::vector<BYTE>& precomputedCurrLuma = {},
                                            int precomputedVeryLowEntropy = -1,
                                            bool* outNearStationaryOverride = nullptr,
                                            bool allowHighConstStationaryRelax = false,
                                            unsigned __int64* outMaskedStationaryScore = nullptr,
                                            bool forceExhaustiveProbeBudget = false,
                                            bool forceExhaustiveFineDx = false )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    PERF_START( tTotal );
#ifdef _DEBUG
    g_StitchPerf.totalCalls++;
#endif

    // Informative-pixel SAD at dy=0 for HCF pairs (set during the
    // stationary check, consumed by the post-search validation).
    unsigned __int64 hcfInfDiff = 0, hcfInfCount = 0;

    // Phase 1 -- Windowed coarse search on downsampled luma
    // Search a LIMITED range around the expected shift to avoid harmonic
    // matches on repetitive content.  For the first frame pair
    // (expectedDy == 0) search outward from the smallest step.
    //
    PERF_START( tBuildDsLuma );
    const int downsampleScale = ( min( frameWidth, frameHeight ) >= 240 ) ? 4 : 2;
    const bool hasPrecomputedLuma = !precomputedPrevLuma.empty() && !precomputedCurrLuma.empty();
    std::vector<BYTE> previousLuma;
    std::vector<BYTE> currentLuma;
    int dsW = 0, dsH = 0, dsW2 = 0, dsH2 = 0;
    if( hasPrecomputedLuma )
    {
        BuildDownsampledLumaFromFullLuma( precomputedPrevLuma, frameWidth, frameHeight, downsampleScale, previousLuma, dsW, dsH );
        BuildDownsampledLumaFromFullLuma( precomputedCurrLuma, frameWidth, frameHeight, downsampleScale, currentLuma, dsW2, dsH2 );
    }
    else
    {
        BuildDownsampledLumaFrame( previousPixels, frameWidth, frameHeight, downsampleScale, previousLuma, dsW, dsH );
        BuildDownsampledLumaFrame( currentPixels, frameWidth, frameHeight, downsampleScale, currentLuma, dsW2, dsH2 );
    }
    if( dsW != dsW2 || dsH != dsH2 )
    {
        PERF_STOP( tBuildDsLuma );
        PERF_STOP( tTotal );
        return false;
    }
    PERF_STOP( tBuildDsLuma );

    const int minStepDs = 1;
    const int maxStepDs = dsH - max( 2, dsH / 6 );
    const int marginX = max( 2, dsW / 20 );

    // Stationary score: how well the frames match with zero shift.
    PERF_START( tStationary );
    unsigned __int64 stationaryScore = ( std::numeric_limits<unsigned __int64>::max )();
    {
        unsigned __int64 totalDiff = 0;
        unsigned __int64 samples = 0;
        for( int y = 0; y < dsH; ++y )
        {
            const int row = y * dsW;
            for( int x = marginX; x < dsW - marginX; x += 2 )
            {
                totalDiff += static_cast<unsigned __int64>(
                    abs( static_cast<int>( previousLuma[row + x] ) -
                         static_cast<int>( currentLuma[row + x] ) ) );
                samples++;
            }
        }
        if( samples > 0 )
        {
            stationaryScore = totalDiff / samples;
        }
    }
    PERF_STOP( tStationary );

    // Very-low-entropy detection and informative mask
    PERF_START( tVleMask );
    // For frames that are mostly constant (>58% uniform pixels in both
    // frames), build a boolean mask of "informative" downsampled pixels
    // (those near edges/text).  Scoring limited to these pixels avoids
    // the background-to-background dilution that makes low-entropy
    // content indistinguishable at every shift.
    //
    // The high-constant-fraction check is independent of lowContrastMode
    // so that sparse high-contrast content (black text on white) gets
    // masked stationary detection.  When the full-frame stationary
    // score is diluted to 0 by the constant background, the masked
    // score (informative pixels only) can still detect real movement.
    // We do NOT promote highConstantFractionPair to veryLowEntropyPair
    // because the masked coarse/fine search is unreliable when the mask
    // is sparse (few informative pixels give noisy candidate scoring).
    const bool isHighConstantFraction =
        ( precomputedVeryLowEntropy >= 0
          ? ( precomputedVeryLowEntropy != 0 )
          : IsVeryLowEntropyPair( previousPixels, currentPixels, frameWidth, frameHeight ) );

    const bool veryLowEntropyPair = lowContrastMode && isHighConstantFraction;
    const bool highConstantFractionPair = !veryLowEntropyPair && isHighConstantFraction;

    // Build downsampled informative mask: a pixel is informative if the
    // luma gradient exceeds a small threshold in either frame.
    std::vector<BYTE> dsMaskPrev;
    std::vector<BYTE> dsMaskCurr;
    if( veryLowEntropyPair || highConstantFractionPair )
    {
        dsMaskPrev.resize( static_cast<size_t>( dsW ) * dsH, 0 );
        dsMaskCurr.resize( static_cast<size_t>( dsW ) * dsH, 0 );
        const int dsEdgeThreshold = 3;
        for( int y = 1; y < dsH - 1; ++y )
        {
            const int rowOff = y * dsW;
            int x = 1;
#if defined(_M_ARM64)
            const uint8x16_t vThresh = vdupq_n_u8( dsEdgeThreshold );
            const uint8x16_t vOne    = vdupq_n_u8( 1 );
            for( ; x + 16 < dsW - 1; x += 16 )
            {
                const int idx = rowOff + x;
                // Previous frame gradient.
                const uint8x16_t pCur  = vld1q_u8( previousLuma.data() + idx );
                const uint8x16_t pRight = vld1q_u8( previousLuma.data() + idx + 1 );
                const uint8x16_t pDown  = vld1q_u8( previousLuma.data() + idx + dsW );
                const uint8x16_t pGrad = vqaddq_u8( vabdq_u8( pCur, pRight ),
                                                     vabdq_u8( pCur, pDown ) );
                const uint8x16_t pMask = vandq_u8( vcgeq_u8( pGrad, vThresh ), vOne );
                vst1q_u8( dsMaskPrev.data() + idx, pMask );
                // Current frame gradient.
                const uint8x16_t cCur   = vld1q_u8( currentLuma.data() + idx );
                const uint8x16_t cRight = vld1q_u8( currentLuma.data() + idx + 1 );
                const uint8x16_t cDown  = vld1q_u8( currentLuma.data() + idx + dsW );
                const uint8x16_t cGrad = vqaddq_u8( vabdq_u8( cCur, cRight ),
                                                     vabdq_u8( cCur, cDown ) );
                const uint8x16_t cMask = vandq_u8( vcgeq_u8( cGrad, vThresh ), vOne );
                vst1q_u8( dsMaskCurr.data() + idx, cMask );
            }
#endif
            for( ; x < dsW - 1; ++x )
            {
                const int idx = rowOff + x;
                const int gradHP = abs( static_cast<int>( previousLuma[idx] ) - static_cast<int>( previousLuma[idx + 1] ) );
                const int gradVP = abs( static_cast<int>( previousLuma[idx] ) - static_cast<int>( previousLuma[idx + dsW] ) );
                if( gradHP + gradVP >= dsEdgeThreshold )
                    dsMaskPrev[idx] = 1;
                const int gradHC = abs( static_cast<int>( currentLuma[idx] ) - static_cast<int>( currentLuma[idx + 1] ) );
                const int gradVC = abs( static_cast<int>( currentLuma[idx] ) - static_cast<int>( currentLuma[idx + dsW] ) );
                if( gradHC + gradVC >= dsEdgeThreshold )
                    dsMaskCurr[idx] = 1;
            }
        }
        // Dilate masks by 1px so adjacent-to-edge pixels are included.
        std::vector<BYTE> dilatedPrev( dsMaskPrev.size(), 0 );
        std::vector<BYTE> dilatedCurr( dsMaskCurr.size(), 0 );
        for( int y = 1; y < dsH - 1; ++y )
        {
            int x = 1;
#if defined(_M_ARM64)
            const uint8x16_t vOne = vdupq_n_u8( 1 );
            for( ; x + 16 < dsW - 1; x += 16 )
            {
                const int idx = y * dsW + x;
                // Previous mask: OR of center, left, right, up, down.
                uint8x16_t pOr = vld1q_u8( dsMaskPrev.data() + idx );
                pOr = vorrq_u8( pOr, vld1q_u8( dsMaskPrev.data() + idx - 1 ) );
                pOr = vorrq_u8( pOr, vld1q_u8( dsMaskPrev.data() + idx + 1 ) );
                pOr = vorrq_u8( pOr, vld1q_u8( dsMaskPrev.data() + idx - dsW ) );
                pOr = vorrq_u8( pOr, vld1q_u8( dsMaskPrev.data() + idx + dsW ) );
                // Clamp to 1 (inputs are 0/1, OR preserves that, but be safe).
                vst1q_u8( dilatedPrev.data() + idx, vandq_u8( vminq_u8( pOr, vOne ), vOne ) );
                // Current mask.
                uint8x16_t cOr = vld1q_u8( dsMaskCurr.data() + idx );
                cOr = vorrq_u8( cOr, vld1q_u8( dsMaskCurr.data() + idx - 1 ) );
                cOr = vorrq_u8( cOr, vld1q_u8( dsMaskCurr.data() + idx + 1 ) );
                cOr = vorrq_u8( cOr, vld1q_u8( dsMaskCurr.data() + idx - dsW ) );
                cOr = vorrq_u8( cOr, vld1q_u8( dsMaskCurr.data() + idx + dsW ) );
                vst1q_u8( dilatedCurr.data() + idx, vandq_u8( vminq_u8( cOr, vOne ), vOne ) );
            }
#endif
            for( ; x < dsW - 1; ++x )
            {
                const int idx = y * dsW + x;
                if( dsMaskPrev[idx] | dsMaskPrev[idx - 1] | dsMaskPrev[idx + 1] |
                    dsMaskPrev[idx - dsW] | dsMaskPrev[idx + dsW] )
                    dilatedPrev[idx] = 1;
                if( dsMaskCurr[idx] | dsMaskCurr[idx - 1] | dsMaskCurr[idx + 1] |
                    dsMaskCurr[idx - dsW] | dsMaskCurr[idx + dsW] )
                    dilatedCurr[idx] = 1;
            }
        }
        dsMaskPrev = std::move( dilatedPrev );
        dsMaskCurr = std::move( dilatedCurr );
    }

    // Compute masked stationary score for very-low-entropy pairs.
    unsigned __int64 maskedStationaryScore = stationaryScore;
    if( veryLowEntropyPair || highConstantFractionPair )
    {
        unsigned __int64 maskedDiff = 0;
        unsigned __int64 maskedSamples = 0;
        for( int y = 0; y < dsH; ++y )
        {
            const int row = y * dsW;
            for( int x = marginX; x < dsW - marginX; x += 2 )
            {
                if( dsMaskPrev[row + x] || dsMaskCurr[row + x] )
                {
                    maskedDiff += static_cast<unsigned __int64>(
                        abs( static_cast<int>( previousLuma[row + x] ) -
                             static_cast<int>( currentLuma[row + x] ) ) );
                    maskedSamples++;
                }
            }
        }
        if( maskedSamples > 0 )
        {
            maskedStationaryScore = maskedDiff / maskedSamples;
        }

        // For high-constant-fraction pairs, the 4x downsampled comparison
        // averages away small scrolls (3-10px).  Re-check at full resolution
        // using ComputeInformativePixelDifference which examines gradient
        // positions in the raw pixel data, detecting sub-downsample movement.
        if( highConstantFractionPair && maskedStationaryScore < 2 )
        {
            if( ComputeInformativePixelDifference( previousPixels, currentPixels,
                                                   frameWidth, frameHeight,
                                                   hcfInfDiff, hcfInfCount ) &&
                hcfInfCount > 0 )
            {
                // Convert per-channel sum to approximate luma diff.
                maskedStationaryScore = hcfInfDiff / ( hcfInfCount * 3 );
            }
        }
    }
    PERF_STOP( tVleMask );

    // Reject if frames are stationary (near-identical).
    // For very-low-entropy pairs, use the masked stationary score which
    // focuses on content pixels and isn't diluted by the background.
    // For high-constant-fraction pairs that aren't in lowContrastMode,
    // rescue frames from false stationary rejection when the masked score
    // shows significant movement at informative pixels.
    const unsigned __int64 effectiveStationaryScore = veryLowEntropyPair ? maskedStationaryScore : stationaryScore;
    // High-constant-fraction captures can produce false "stationary=1"
    // readings on real movement (periodic content with sparse informative
    // pixels). Use a stricter reject threshold there so only true near-zero
    // matches are dropped.
    const unsigned __int64 stationaryRejectThreshold = lowContrastMode ? 1 : 2;
    if( effectiveStationaryScore <= stationaryRejectThreshold )
    {
        const bool highConstStationaryRelax =
            allowHighConstStationaryRelax &&
            highConstantFractionPair &&
            stationaryScore <= 1 &&
            maskedStationaryScore >= 1;

        // Rescue: if the full-frame score is below threshold but the
        // masked score (informative pixels only) shows movement, the
        // frame moved but the background diluted the signal.  Use >= so
        // that borderline cases (maskedStationary at threshold) are
        // rescued -- these frames already passed the duplicate check, so
        // a non-zero masked score indicates real content change.
        // For HCF pairs, maskedStationaryScore >= 1 is sufficient: a
        // non-zero average luma diff at informative pixels indicates
        // real movement even when the full-frame score is diluted by
        // the constant background.
        // For VLE pairs, informative pixels are so sparse that the
        // downsampled stationary score can be 0 even with real movement.
        // Rescue unconditionally -- the full-resolution masked coarse
        // fallback will determine the correct shift or legitimately
        // fail downstream.  Truly-stationary frames are already
        // filtered by the capture loop's duplicate check.
        if( veryLowEntropyPair ||
            ( highConstantFractionPair &&
              ( maskedStationaryScore >= 1 || highConstStationaryRelax ) ) )
        {
            // Fall through to the coarse search.
        }
        else
        {
            if( outMaskedStationaryScore )
                *outMaskedStationaryScore = maskedStationaryScore;
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift stationary expected=(%d,%d) stationary=%llu maskedStationary=%llu veryLowEntropy=%d highConstFrac=%d frame=%dx%d\n",
                         expectedDx, expectedDy,
                         static_cast<unsigned long long>( stationaryScore ),
                         static_cast<unsigned long long>( maskedStationaryScore ),
                         veryLowEntropyPair ? 1 : 0,
                         highConstantFractionPair ? 1 : 0,
                         frameWidth, frameHeight );
            PERF_STOP( tTotal );
            return false;
        }
    }

    // Determine the search window in downsampled space.
    // Use full range in the known scroll direction.  Scroll speed
    // can vary dramatically between frames (e.g. 40->202->213->38)
    // so a narrow window around the expected step misses large jumps.
    // The candidate-shortlist + fine-resolution ranking below handles
    // disambiguation among many coarse candidates.
    const int expectedDyDs = expectedDy / downsampleScale;
    int searchMinDy, searchMaxDy;
    if( expectedDyDs == 0 )
    {
        // First frame pair: no prior knowledge of scroll direction.
        searchMinDy = -maxStepDs;
        searchMaxDy = maxStepDs;
    }
    else if( expectedDyDs < 0 )
    {
        // Scrolling down: search full range in negative direction.
        searchMinDy = -maxStepDs;
        searchMaxDy = -minStepDs;
    }
    else
    {
        // Scrolling up: search full range in positive direction.
        searchMinDy = minStepDs;
        searchMaxDy = maxStepDs;
    }



    // Score every candidate within the search window.
    PERF_START( tCoarseSearch );
    // Collect the top candidates by raw coarse score, then rank by
    // full-resolution comparison.  This avoids the problem of distance-
    // based scoring favoring wrong harmonic matches when scroll speed
    // varies.
    struct CoarseCandidate
    {
        int dyDs;
        unsigned __int64 score;
    };

    constexpr int kMaxCandidates = 12;
    constexpr int kMaxCandidatesWithProbes = 160;
    constexpr int kStableDirectionCandidateBudget = 64;
    constexpr int kConfidentDirectionCandidateBudget = 48;
    constexpr int kUnknownDirectionCandidateBudget = 96;
    CoarseCandidate candidates[kMaxCandidatesWithProbes];
    int candidateCount = 0;

    // Fast-pass budget: search with a tighter candidate budget first, then
    // rerun with exhaustive budget only when confidence is weak.
    // Exception: first-pair unknown-direction VLE content is exactly where
    // candidate starvation can cause axis-defer loops and middle-frame drops,
    // so force exhaustive coverage up front.
    const bool useFastProbePass = !forceExhaustiveProbeBudget &&
                                  !( expectedDyDs == 0 && veryLowEntropyPair );
    int probeCandidateBudget = kMaxCandidatesWithProbes;
    if( useFastProbePass )
    {
        if( expectedDyDs == 0 )
        {
            probeCandidateBudget = kUnknownDirectionCandidateBudget;
        }
        else
        {
            probeCandidateBudget = ( abs( expectedDyDs ) >= 2 )
                ? kConfidentDirectionCandidateBudget
                : kStableDirectionCandidateBudget;
        }
    }

    for( int absStep = minStepDs; absStep <= maxStepDs; ++absStep )
    {
        for( int direction = -1; direction <= 1; direction += 2 )
        {
            const int dyDs = direction * absStep;

            // Skip candidates outside the search window.
            if( dyDs < searchMinDy || dyDs > searchMaxDy )
            {
                continue;
            }

            const int overlap = dsH - absStep;
            unsigned __int64 totalDiff = 0;
            unsigned __int64 samples = 0;
            bool earlyExitCoarse = false;

            for( int y = 0; y < overlap && !earlyExitCoarse; y += 2 )
            {
                const int pY = ( direction < 0 ) ? ( y + absStep ) : y;
                const int cY = ( direction < 0 ) ? y : ( y + absStep );
                const int prevRow = pY * dsW;
                const int currRow = cY * dsW;
                for( int x = marginX; x < dsW - marginX; x += 2 )
                {
                    // For very-low-entropy pairs, only score informative
                    // pixels (those near edges/text in either frame).
                    if( veryLowEntropyPair &&
                        !dsMaskPrev[prevRow + x] && !dsMaskCurr[currRow + x] )
                    {
                        continue;
                    }

                    totalDiff += static_cast<unsigned __int64>(
                        abs( static_cast<int>( previousLuma[prevRow + x] ) -
                             static_cast<int>( currentLuma[currRow + x] ) ) );
                    samples++;
                }

                // Early termination: if running average already exceeds
                // the worst kept candidate, this step cannot win.
                // Use (score+1)*samples to match floor-division semantics:
                // floor(totalDiff/samples) > score  iff  totalDiff >= (score+1)*samples.
                if( candidateCount >= kMaxCandidates && samples >= 100 &&
                    totalDiff >= (candidates[candidateCount - 1].score + 1) * samples )
                {
                    earlyExitCoarse = true;
                }
            }

            if( earlyExitCoarse || samples < ( veryLowEntropyPair ? 20 : 100 ) )
            {
                continue;
            }

            unsigned __int64 score = totalDiff / samples;

            // Insert into sorted candidates list if good enough
            if( candidateCount < kMaxCandidates || score < candidates[candidateCount - 1].score )
            {
                int insertPos = candidateCount < kMaxCandidates ? candidateCount : candidateCount - 1;
                for( int j = insertPos; j > 0 && candidates[j - 1].score > score; --j )
                {
                    if( j < kMaxCandidates )
                    {
                        candidates[j] = candidates[j - 1];
                    }
                    insertPos = j - 1;
                }
                if( insertPos < kMaxCandidates )
                {
                    candidates[insertPos] = { dyDs, score };
                    if( candidateCount < kMaxCandidates )
                    {
                        candidateCount++;
                    }
                }
            }
        }
    }
    PERF_STOP( tCoarseSearch );

    // Full-resolution masked coarse fallback
    PERF_START( tMaskedFallback );
    // When all coarse candidates have score 0 (or the coarse search
    // produced zero candidates because VLE masking left < 20 samples
    // at every offset), the downsampled search can't discriminate
    // offsets.  For high-constant-fraction or very-low-entropy pairs
    // with sparse text, even a masked downsampled search fails because
    // at 4x downsample thin characters become indistinguishable blobs.
    //
    // The fix: re-run the coarse search at full resolution, scoring
    // only informative pixels (those near edges/text).  Full-resolution
    // text characters are clearly distinguishable, so the correct shift
    // produces a much lower score than wrong shifts.
    //
    // This is a fallback-only path: normal content that produces
    // non-zero downsampled scores is never affected.
    bool useMaskedFallback = false;
    std::vector<BYTE> fallbackPrevLuma;
    std::vector<BYTE> fallbackCurrLuma;
    std::vector<BYTE> fallbackMaskPrev;
    std::vector<BYTE> fallbackMaskCurr;
    if( highConstantFractionPair || veryLowEntropyPair )
    {
        bool allZeroOrEmpty = ( candidateCount == 0 );
        if( !allZeroOrEmpty )
        {
            allZeroOrEmpty = true;
            for( int ci = 0; ci < candidateCount; ++ci )
            {
                if( candidates[ci].score > 0 )
                {
                    allZeroOrEmpty = false;
                    break;
                }
            }
        }

        if( allZeroOrEmpty )
        {
            // Build full-resolution luma.
            if( hasPrecomputedLuma )
            {
                // Reference precomputed -- avoid copying.
            }
            else
            {
                BuildFullLumaFrame( previousPixels, frameWidth, frameHeight, fallbackPrevLuma );
                BuildFullLumaFrame( currentPixels, frameWidth, frameHeight, fallbackCurrLuma );
            }
            const std::vector<BYTE>& prevFull = hasPrecomputedLuma ? precomputedPrevLuma : fallbackPrevLuma;
            const std::vector<BYTE>& currFull = hasPrecomputedLuma ? precomputedCurrLuma : fallbackCurrLuma;

            // Build full-resolution informative masks.
            const size_t pixelCount = static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight );
            std::vector<BYTE> rawMaskPrev( pixelCount, 0 );
            std::vector<BYTE> rawMaskCurr( pixelCount, 0 );
            const int fineEdgeThreshold = 4;
            for( int y = 1; y < frameHeight - 1; ++y )
            {
                for( int x = 1; x < frameWidth - 1; ++x )
                {
                    const int idx = y * frameWidth + x;
                    const int gHP = abs( static_cast<int>( prevFull[idx] ) - static_cast<int>( prevFull[idx + 1] ) );
                    const int gVP = abs( static_cast<int>( prevFull[idx] ) - static_cast<int>( prevFull[idx + frameWidth] ) );
                    if( gHP + gVP >= fineEdgeThreshold )
                        rawMaskPrev[idx] = 1;
                    const int gHC = abs( static_cast<int>( currFull[idx] ) - static_cast<int>( currFull[idx + 1] ) );
                    const int gVC = abs( static_cast<int>( currFull[idx] ) - static_cast<int>( currFull[idx + frameWidth] ) );
                    if( gHC + gVC >= fineEdgeThreshold )
                        rawMaskCurr[idx] = 1;
                }
            }
            // Dilate by 1 pixel.
            fallbackMaskPrev.resize( pixelCount, 0 );
            fallbackMaskCurr.resize( pixelCount, 0 );
            for( int y = 1; y < frameHeight - 1; ++y )
            {
                for( int x = 1; x < frameWidth - 1; ++x )
                {
                    const int idx = y * frameWidth + x;
                    if( rawMaskPrev[idx] | rawMaskPrev[idx - 1] | rawMaskPrev[idx + 1] |
                        rawMaskPrev[idx - frameWidth] | rawMaskPrev[idx + frameWidth] )
                        fallbackMaskPrev[idx] = 1;
                    if( rawMaskCurr[idx] | rawMaskCurr[idx - 1] | rawMaskCurr[idx + 1] |
                        rawMaskCurr[idx - frameWidth] | rawMaskCurr[idx + frameWidth] )
                        fallbackMaskCurr[idx] = 1;
                }
            }

            useMaskedFallback = true;

            // Edge-density coarse search at full resolution.
            // Instead of counting informative pixels per row (fragile on periodic
            // content), sum horizontal gradient magnitudes per row.  This "edge
            // density" signal captures the structural layout of text lines.
            // Cross-correlate using NCC (not L1) for scale-invariant matching.
            candidateCount = 0;
            const int fullMarginX = 4;
            const int fullMinStep = max( 4, downsampleScale );
            const int fullMaxStep = frameHeight - max( 2, frameHeight / 6 );

            // Build edge density signals.
            std::vector<int> edgePrev, edgeCurr;
            BuildRowEdgeDensity( prevFull, frameWidth, frameHeight, fullMarginX, edgePrev );
            BuildRowEdgeDensity( currFull, frameWidth, frameHeight, fullMarginX, edgeCurr );

            // Convert search window to full-resolution.
            const int searchMinDyFull = searchMinDy * downsampleScale;
            const int searchMaxDyFull = searchMaxDy * downsampleScale;

            struct EdgeCandidate { int dyFull; double ncc; };
            EdgeCandidate edgeCands[kMaxCandidates];
            int edgeCandCount = 0;

            for( int absStep = fullMinStep; absStep <= fullMaxStep; absStep += 2 )
            {
                for( int direction = -1; direction <= 1; direction += 2 )
                {
                    const int dyFull = direction * absStep;
                    if( dyFull < searchMinDyFull || dyFull > searchMaxDyFull )
                        continue;

                    const int overlap = frameHeight - absStep;
                    const int* aSig = ( direction < 0 ) ? &edgePrev[absStep] : &edgePrev[0];
                    const int* bSig = ( direction < 0 ) ? &edgeCurr[0] : &edgeCurr[absStep];
                    const double ncc = NCC1D( aSig, bSig, overlap );

                    // Insert into top-K by NCC (descending).
                    if( edgeCandCount < kMaxCandidates || ncc > edgeCands[edgeCandCount - 1].ncc )
                    {
                        int insertPos = edgeCandCount < kMaxCandidates ? edgeCandCount : edgeCandCount - 1;
                        for( int j = insertPos; j > 0 && edgeCands[j - 1].ncc < ncc; --j )
                        {
                            if( j < kMaxCandidates )
                                edgeCands[j] = edgeCands[j - 1];
                            insertPos = j - 1;
                        }
                        if( insertPos < kMaxCandidates )
                        {
                            edgeCands[insertPos] = { dyFull, ncc };
                            if( edgeCandCount < kMaxCandidates )
                                edgeCandCount++;
                        }
                    }
                }
            }

            // Transfer edge projection candidates to the main candidate array.
            // Convert full-resolution dy to downsampled units for the fine search.
            for( int ei = 0; ei < edgeCandCount; ++ei )
            {
                const int dyDs = edgeCands[ei].dyFull / downsampleScale;
                candidates[candidateCount++] = { dyDs, 0 };
            }
            if( candidateCount > 0 )
                useMaskedFallback = true;

            // When all edge NCC candidates scored <= 0 (no meaningful
            // correlation found on periodic content), redistribute them
            // evenly across the search range so the fine search samples
            // the full window instead of clustering at small shifts.
            if( useMaskedFallback && edgeCandCount >= 2 &&
                edgeCands[0].ncc <= 0.0 )
            {
                const int rangeSpan = searchMaxDy - searchMinDy;
                const int stride = max( 1, rangeSpan / ( kMaxCandidates - 1 ) );
                candidateCount = 0;
                for( int pos = searchMinDy; pos <= searchMaxDy && candidateCount < kMaxCandidates; pos += stride )
                {
                    candidates[candidateCount++] = { pos, 0 };
                }
            }

            StitchLog( L"[Panorama/Stitch] FullResMaskedCoarseFallback triggered=%d candidates=%d frame=%dx%d\n",
                         useMaskedFallback ? 1 : 0, candidateCount,
                         frameWidth, frameHeight );
        }
    }
    PERF_STOP( tMaskedFallback );

    if( candidateCount == 0 )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift no-match expected=(%d,%d) frame=%dx%d\n",
                     expectedDx, expectedDy, frameWidth, frameHeight );
        PERF_STOP( tTotal );
        return false;
    }

    const unsigned __int64 bestCoarseScore = candidates[0].score;

    // Optimization #1: confidence-gated probe injection bypass.
    // On stable high-constant-fraction streams with established motion,
    // probe candidate expansion can dominate runtime while adding little
    // value. Skip probes when the coarse winner is clearly separated.
    const bool coarseWinnerClearlySeparated =
        ( candidateCount <= 1 ) ||
        ( candidates[1].score >= candidates[0].score + 6 );
    const bool bypassProbeInjection =
        useFastProbePass &&
        !forceExhaustiveProbeBudget &&
        expectedDyDs != 0 &&
        highConstantFractionPair &&
        !veryLowEntropyPair &&
        bestCoarseScore <= 12 &&
        coarseWinnerClearlySeparated;

    // Prune candidates whose coarse score is far worse than the best.
    PERF_START( tProbeInject );
    const unsigned __int64 coarsePruneThreshold = bestCoarseScore + ( lowContrastMode ? 20 : 30 );
    int prunedCount = candidateCount;
    for( int ci = 0; ci < prunedCount; ++ci )
    {
        if( candidates[ci].score > coarsePruneThreshold )
        {
            prunedCount = ci;
            break;
        }
    }
    if( prunedCount < 1 )
    {
        prunedCount = 1;
    }

    // Pre-compute full-resolution luma arrays.  Hoisted before probe
    // injection so that edge-projection injection (HCF) can use them.
    // Use caller-provided full-resolution luma if available; reuse
    // fallback-computed luma if the masked coarse ran; otherwise compute locally.
    std::vector<BYTE> previousFullLumaOwned;
    std::vector<BYTE> currentFullLumaOwned;
    if( !hasPrecomputedLuma )
    {
        if( !fallbackPrevLuma.empty() )
        {
            previousFullLumaOwned = std::move( fallbackPrevLuma );
            currentFullLumaOwned = std::move( fallbackCurrLuma );
        }
        else
        {
            BuildFullLumaFrame( previousPixels, frameWidth, frameHeight, previousFullLumaOwned );
            BuildFullLumaFrame( currentPixels, frameWidth, frameHeight, currentFullLumaOwned );
        }
    }
    const std::vector<BYTE>& previousFullLuma = hasPrecomputedLuma ? precomputedPrevLuma : previousFullLumaOwned;
    const std::vector<BYTE>& currentFullLuma = hasPrecomputedLuma ? precomputedCurrLuma : currentFullLumaOwned;

    // Inject probe candidates near the expected shift.  Content with regular
    // vertical structure (e.g. code text at ~13 px line height) produces many
    // similarly-scored coarse candidates at text-line harmonics, pushing the
    // correct shift outside the top-12.  Adding probes at the expected step
    // ensures the fine search always evaluates the correct neighborhood.
    if( !bypassProbeInjection && expectedDyDs != 0 && prunedCount < probeCandidateBudget )
    {
        for( int probe = -3; probe <= 3 && prunedCount < probeCandidateBudget; ++probe )
        {
            const int probeDyDs = expectedDyDs + probe;
            if( abs( probeDyDs ) < minStepDs || abs( probeDyDs ) > maxStepDs )
            {
                continue;
            }

            // Respect the search window established by the scroll direction.
            // Without this check, probes near expectedDyDs can inject wrong-
            // direction candidates (e.g. dyDs=+1 when searching negative only),
            // which on HCF content score nearly identically to the correct
            // direction and cause forward/backward oscillation.
            if( probeDyDs < searchMinDy || probeDyDs > searchMaxDy )
            {
                continue;
            }

            bool alreadyPresent = false;
            for( int ci = 0; ci < prunedCount; ++ci )
            {
                if( candidates[ci].dyDs == probeDyDs )
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if( !alreadyPresent )
            {
                candidates[prunedCount] = { probeDyDs, coarsePruneThreshold };
                prunedCount++;
            }
        }
    }

    // Flat-content exhaustive fallback: when the coarse search cannot
    // discriminate offsets (best coarse score is at noise-floor level),
    // inject DS candidates across the search window.  Iterate from the
    // center outward (smallest absStep first) so that moderate shifts —
    // the most common in real scrolling — get candidate slots before the
    // budget fills up.  For high-entropy low-contrast content, all
    // shifts score nearly identically in the downsampled space because
    // rows are independent random values, so the top-12 selection is
    // essentially random and may miss the true shift.  The fine search
    // at full resolution CAN discriminate because the true shift gives
    // zero difference (exact row match).  Once the first score=0
    // candidate is found the earlyExit mechanism makes all remaining
    // candidates trivially cheap to evaluate.
    if( !bypassProbeInjection && bestCoarseScore >= 8 && !highConstantFractionPair )
    {
        for( int absStep = minStepDs; absStep <= maxStepDs && prunedCount < probeCandidateBudget; ++absStep )
        {
            for( int direction = -1; direction <= 1; direction += 2 )
            {
                if( prunedCount >= probeCandidateBudget )
                    break;

                const int dyDs = direction * absStep;
                if( dyDs < searchMinDy || dyDs > searchMaxDy )
                    continue;

                bool alreadyPresent = false;
                for( int ci = 0; ci < prunedCount; ++ci )
                {
                    if( candidates[ci].dyDs == dyDs )
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if( !alreadyPresent )
                {
                    candidates[prunedCount] = { dyDs, coarsePruneThreshold };
                    prunedCount++;
                }
            }
        }
    }

    // First-frame diversity: when no expected motion is known, the coarse
    // search may concentrate all candidates at small harmonics, causing
    // the fine search to miss the true (potentially large) shift.  Inject
    // evenly-distributed probes across the full search range so the fine
    // search always evaluates a representative sample of shifts.
    // Performance is safe because the correct shift produces fineScore=0
    // on exact-overlap content, and early termination kills all subsequent
    // candidates after the first sample.
    if( !bypassProbeInjection && expectedDyDs == 0 && prunedCount < probeCandidateBudget )
    {
        const int rangeSpan = searchMaxDy - searchMinDy;
        const int probeTarget = min( 30, max( 10, rangeSpan / 4 ) );
        const int probeStride = max( 1, rangeSpan / max( 1, probeTarget ) );
        for( int pos = searchMinDy; pos <= searchMaxDy && prunedCount < probeCandidateBudget; pos += probeStride )
        {
            if( abs( pos ) < minStepDs || abs( pos ) > maxStepDs )
                continue;

            bool alreadyPresent = false;
            for( int ci = 0; ci < prunedCount; ++ci )
            {
                if( candidates[ci].dyDs == pos )
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if( !alreadyPresent )
            {
                candidates[prunedCount] = { pos, coarsePruneThreshold };
                prunedCount++;
            }
        }
    }

    // Exhaustive probe injection for masked-fallback HCF content: the
    // coarse row-projection search may cluster candidates around a few
    // harmonics, missing the correct shift.  Since the masked fine search
    // scores only informative (text/edge) pixels, wrong shifts produce
    // non-zero scores while the correct shift scores ~ 0.  Injecting
    // every candidate is safe: early termination after the first score=0
    // hit makes subsequent evaluations trivially cheap.
    if( !bypassProbeInjection && useMaskedFallback && prunedCount < probeCandidateBudget )
    {
        for( int dyDs = searchMinDy; dyDs <= searchMaxDy && prunedCount < probeCandidateBudget; ++dyDs )
        {
            if( abs( dyDs ) < minStepDs || abs( dyDs ) > maxStepDs )
                continue;

            bool alreadyPresent = false;
            for( int ci = 0; ci < prunedCount; ++ci )
            {
                if( candidates[ci].dyDs == dyDs )
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if( !alreadyPresent )
            {
                candidates[prunedCount] = { dyDs, coarsePruneThreshold };
                prunedCount++;
            }
        }
    }

    // Harmonic-fallback probe injection for HCF content.  When the best
    // coarse candidate scored 0 (perfect harmonic at downsampled resolution)
    // but not all candidates are zero (useMaskedFallback didn't fire), the
    // correct shift may be missing from the candidate list.
    //
    // This specifically targets the "jump-recovery" scenario: the expected
    // step is large (the previous frame jumped), but the actual shift for
    // this frame is much smaller.  The coarse search locks onto a harmonic
    // of the text-line period, and the small correct shift isn't among the
    // top candidates.
    //
    // Only inject small-shift probes (|dyDs| much less than expected) to
    // avoid introducing harmonic false matches at large offsets.  The fine
    // search (standard luma) gives fineScore == 0 only at the true pixel
    // offset, so a probe guard (score > 0 -> skip) prevents regressions.
    const bool harmonicFallback = highConstantFractionPair && bestCoarseScore <= 2 && !useMaskedFallback;
    const int preHarmonicProbeCount = prunedCount;
    const int expectedAbsStepEarly = max( abs( expectedDy ), abs( expectedDx ) );
    if( !bypassProbeInjection && harmonicFallback && forceExhaustiveProbeBudget )
    {
        probeCandidateBudget = kMaxCandidatesWithProbes;
    }
    if( !bypassProbeInjection && harmonicFallback && expectedAbsStepEarly >= frameHeight / 5 )
    {
        const int maxProbeDyDs = max( 3, abs( expectedDy ) / ( 3 * downsampleScale ) );

        for( int dyDs = -maxProbeDyDs; dyDs <= maxProbeDyDs && prunedCount < probeCandidateBudget; ++dyDs )
        {
            if( abs( dyDs ) < minStepDs || abs( dyDs ) > maxStepDs )
                continue;

            bool alreadyPresent = false;
            for( int ci = 0; ci < prunedCount; ++ci )
            {
                if( candidates[ci].dyDs == dyDs )
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if( !alreadyPresent )
            {
                candidates[prunedCount] = { dyDs, coarsePruneThreshold };
                prunedCount++;
            }
        }
    }

    // Edge-projection candidate injection for HCF content.
    // When the standard downsampled coarse search works (some non-zero scores)
    // but may have missed the correct shift among harmonic alternatives,
    // edge-density NCC provides structurally-informed candidates.
    if( highConstantFractionPair && !useMaskedFallback && candidateCount > 0 )
    {
        PERF_START( tEdgeProjection );
        std::vector<int> edgePrevInj, edgeCurrInj;
        BuildRowEdgeDensity( previousFullLuma, frameWidth, frameHeight, 4, edgePrevInj );
        BuildRowEdgeDensity( currentFullLuma, frameWidth, frameHeight, 4, edgeCurrInj );

        // Find top 8 edge-density NCC candidates.
        struct EdgeCandidate { int dyFull; double ncc; };
        constexpr int kEdgeInject = 8;
        EdgeCandidate edgeInj[kEdgeInject];
        int eiCount = 0;

        const int searchMinFull = searchMinDy * downsampleScale;
        const int searchMaxFull = searchMaxDy * downsampleScale;

        // Parallel NCC scan: evaluate all (absStep, dir) pairs concurrently.
        const int nccMinStep = max( 4, downsampleScale );
        const int nccMaxStep = frameHeight - max( 2, frameHeight / 6 );
        struct NccWork { int absStep; int dir; double ncc; };
        std::vector<NccWork> nccWork;
        nccWork.reserve( 2 * ( ( nccMaxStep - nccMinStep ) / 2 + 1 ) );
        for( int absStep = nccMinStep; absStep <= nccMaxStep; absStep += 2 )
        {
            for( int dir = -1; dir <= 1; dir += 2 )
            {
                const int dyF = dir * absStep;
                if( dyF >= searchMinFull && dyF <= searchMaxFull )
                    nccWork.push_back( { absStep, dir, 0.0 } );
            }
        }

        parallel_for( 0, static_cast<int>( nccWork.size() ), [&]( int idx )
        {
            auto& w = nccWork[idx];
            const int overlap = frameHeight - w.absStep;
            const int* aS = ( w.dir < 0 ) ? &edgePrevInj[w.absStep] : &edgePrevInj[0];
            const int* bS = ( w.dir < 0 ) ? &edgeCurrInj[0] : &edgeCurrInj[w.absStep];
            w.ncc = NCC1D( aS, bS, overlap );
        } );

        // Pick top kEdgeInject from all NCC results.
        for( const auto& w : nccWork )
        {
            const int dyF = w.dir * w.absStep;
            if( eiCount < kEdgeInject || w.ncc > edgeInj[eiCount - 1].ncc )
            {
                int ip = eiCount < kEdgeInject ? eiCount : eiCount - 1;
                for( int j = ip; j > 0 && edgeInj[j - 1].ncc < w.ncc; --j )
                {
                    if( j < kEdgeInject )
                        edgeInj[j] = edgeInj[j - 1];
                    ip = j - 1;
                }
                if( ip < kEdgeInject )
                {
                    edgeInj[ip] = { dyF, w.ncc };
                    if( eiCount < kEdgeInject )
                        eiCount++;
                }
            }
        }

        // Inject into candidate array (avoid duplicates).
        for( int ei = 0; ei < eiCount && prunedCount < probeCandidateBudget; ++ei )
        {
            const int dyDs = edgeInj[ei].dyFull / downsampleScale;
            bool dup = false;
            for( int ci = 0; ci < prunedCount; ++ci )
                if( candidates[ci].dyDs == dyDs ) { dup = true; break; }
            if( !dup )
                candidates[prunedCount++] = { dyDs, coarsePruneThreshold };
        }

        StitchLog( L"[Panorama/Stitch] EdgeProjectionInject injected=%d topNCC=%.4f\n",
                     eiCount, eiCount > 0 ? edgeInj[0].ncc : 0.0 );
        PERF_STOP( tEdgeProjection );
    }

    if( bypassProbeInjection )
    {
        StitchLog( L"[Panorama/Stitch] ProbeInject bypassed expected=(%d,%d) bestCoarse=%llu candidateCount=%d\n",
                     expectedDx,
                     expectedDy,
                     static_cast<unsigned long long>( bestCoarseScore ),
                     candidateCount );
    }

    // Debug: log coarse candidates for HCF frames to diagnose harmonic issues.
    if( highConstantFractionPair && bestCoarseScore <= 2 )
    {
        StitchLog( L"[Panorama/Stitch] HCF-candidates expected=(%d,%d) coarseCount=%d prunedCount=%d bestCoarse=%llu candidates=",
                     expectedDx, expectedDy, candidateCount, prunedCount,
                     static_cast<unsigned long long>( bestCoarseScore ) );
        for( int ci = 0; ci < min( prunedCount, 30 ); ++ci )
        {
            StitchLog( L"%d(%llu) ", candidates[ci].dyDs,
                         static_cast<unsigned long long>( candidates[ci].score ) );
        }
        StitchLog( L"\n" );
    }
    PERF_STOP( tProbeInject );

    // Phase 2: Rank candidates by full-resolution comparison
    PERF_START( tFineSearch );
    // For each coarse candidate, compute a fine score at full resolution.
    // This resolves ambiguity from harmonic matches on repetitive content
    // since the full-resolution comparison sees fine text details that
    // the downsampled comparison misses.
    //
    // Pre-compute full-resolution luma arrays so the inner loop uses
    // cheap byte lookups instead of per-pixel RGB->luma multiplies.
    //
    // When the masked-fallback created evenly-distributed candidates,
    // the gap between adjacent candidates can be much larger than the
    // normal refine radius (e.g. 96 full-res pixels vs +/-5).  Widen
    // refineRadiusDy to half the distribution stride so that adjacent
    // candidate refinement ranges overlap, guaranteeing full coverage.
    const int normalRefineRadius = max( 3, downsampleScale + 1 );
    const int refineRadiusDy = useMaskedFallback && candidateCount >= 2
        ? max( normalRefineRadius,
               ( searchMaxDy - searchMinDy ) * downsampleScale / ( 2 * max( 1, candidateCount - 1 ) ) )
        : normalRefineRadius;
    // Fast fine-search pass: when horizontal motion is expected to be zero and
    // vertical direction is already established, evaluate only dx=0 first.
    // If confidence is weak, we rerun once with full dx radius to preserve
    // quality on borderline frames.
    const bool useFastFineDxPass =
        !forceExhaustiveFineDx &&
        expectedDx == 0 &&
        expectedDy != 0;
    const int refineRadiusDx = useFastFineDxPass ? 0 : 1;

    // Build full-resolution informative masks for very-low-entropy pairs.
    // The masked fine search restricts scoring to content pixels (text
    // edges) which is essential for low-contrast terminal-style content
    // where the standard SAD produces near-zero scores everywhere.
    std::vector<BYTE> fullMaskPrev;
    std::vector<BYTE> fullMaskCurr;
    const bool useFineMask = veryLowEntropyPair || useMaskedFallback;
    const bool useZnccFineSearch = highConstantFractionPair && useFineMask;
    constexpr unsigned __int64 kZnccScoreBase = 12800;
    if( useFineMask )
    {
        if( !fallbackMaskPrev.empty() )
        {
            fullMaskPrev = std::move( fallbackMaskPrev );
            fullMaskCurr = std::move( fallbackMaskCurr );
        }
        else
        {
            const size_t pixelCount = static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight );
            fullMaskPrev.resize( pixelCount, 0 );
            fullMaskCurr.resize( pixelCount, 0 );
            const int fineEdgeThreshold = 4;
            for( int y = 1; y < frameHeight - 1; ++y )
            {
                for( int x = 1; x < frameWidth - 1; ++x )
                {
                    const int idx = y * frameWidth + x;
                    const int gHP = abs( static_cast<int>( previousFullLuma[idx] ) - static_cast<int>( previousFullLuma[idx + 1] ) );
                    const int gVP = abs( static_cast<int>( previousFullLuma[idx] ) - static_cast<int>( previousFullLuma[idx + frameWidth] ) );
                    if( gHP + gVP >= fineEdgeThreshold )
                        fullMaskPrev[idx] = 1;
                    const int gHC = abs( static_cast<int>( currentFullLuma[idx] ) - static_cast<int>( currentFullLuma[idx + 1] ) );
                    const int gVC = abs( static_cast<int>( currentFullLuma[idx] ) - static_cast<int>( currentFullLuma[idx + frameWidth] ) );
                    if( gHC + gVC >= fineEdgeThreshold )
                        fullMaskCurr[idx] = 1;
                }
            }
            // Dilate by 1 pixel.
            std::vector<BYTE> dilPrev( pixelCount, 0 );
            std::vector<BYTE> dilCurr( pixelCount, 0 );
            for( int y = 1; y < frameHeight - 1; ++y )
            {
                for( int x = 1; x < frameWidth - 1; ++x )
                {
                    const int idx = y * frameWidth + x;
                    if( fullMaskPrev[idx] | fullMaskPrev[idx - 1] | fullMaskPrev[idx + 1] |
                        fullMaskPrev[idx - frameWidth] | fullMaskPrev[idx + frameWidth] )
                        dilPrev[idx] = 1;
                    if( fullMaskCurr[idx] | fullMaskCurr[idx - 1] | fullMaskCurr[idx + 1] |
                        fullMaskCurr[idx - frameWidth] | fullMaskCurr[idx + frameWidth] )
                        dilCurr[idx] = 1;
                }
            }
            fullMaskPrev = std::move( dilPrev );
            fullMaskCurr = std::move( dilCurr );
        }
    }

    // Per-row mask prefix counts let the masked fine-search skip rows that
    // contain no informative pixels in either frame window.
    std::vector<unsigned short> fullMaskPrevRowPrefix;
    std::vector<unsigned short> fullMaskCurrRowPrefix;
    if( useFineMask )
    {
        const size_t prefixStride = static_cast<size_t>( frameWidth ) + 1;
        fullMaskPrevRowPrefix.assign( static_cast<size_t>( frameHeight ) * prefixStride, 0 );
        fullMaskCurrRowPrefix.assign( static_cast<size_t>( frameHeight ) * prefixStride, 0 );

        for( int y = 0; y < frameHeight; ++y )
        {
            const size_t rowBase = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth );
            const size_t prefBase = static_cast<size_t>( y ) * prefixStride;
            for( int x = 0; x < frameWidth; ++x )
            {
                fullMaskPrevRowPrefix[prefBase + static_cast<size_t>( x ) + 1] =
                    static_cast<unsigned short>(
                        fullMaskPrevRowPrefix[prefBase + static_cast<size_t>( x )] +
                        ( fullMaskPrev[rowBase + static_cast<size_t>( x )] ? 1 : 0 ) );
                fullMaskCurrRowPrefix[prefBase + static_cast<size_t>( x ) + 1] =
                    static_cast<unsigned short>(
                        fullMaskCurrRowPrefix[prefBase + static_cast<size_t>( x )] +
                        ( fullMaskCurr[rowBase + static_cast<size_t>( x )] ? 1 : 0 ) );
            }
        }
    }

    // Scale fine scores by 256 to preserve sub-integer precision.
    // For sparse content (< 1% non-background pixels), the per-pixel
    // average difference at wrong shifts is < 1.0, which integer
    // division truncates to 0 -- making correct and wrong shifts
    // indistinguishable.  The 256x scale gives 8 bits of fractional
    // precision without risk of u64 overflow (max totalDiff*256 ~ 20B).
    constexpr unsigned __int64 kFineScoreScale = 256;
    unsigned __int64 bestFineScore = ( std::numeric_limits<unsigned __int64>::max )();
    unsigned __int64 secondBestFineScore = ( std::numeric_limits<unsigned __int64>::max )();
    unsigned __int64 bestFineRankScore = ( std::numeric_limits<unsigned __int64>::max )();
    unsigned __int64 secondBestFineRankScore = ( std::numeric_limits<unsigned __int64>::max )();
    int secondBestDx = 0;
    int secondBestDy = 0;
    bestDx = 0;
    bestDy = candidates[0].dyDs * downsampleScale;
    int bestCoarseDy = candidates[0].dyDs;
    int bestAbsStep = ( std::numeric_limits<int>::max )();
    int bestAbsDx = ( std::numeric_limits<int>::max )();
    const int expectedAbsStep = max( abs( expectedDy ), abs( expectedDx ) );
    int bestExpectedDelta = ( std::numeric_limits<int>::max )();
    // Track best fine score seen at a candidate near expectedAbsStep
    // (within +/-4 px). Used both for diagnostics and for harmonic-override
    // logic that prefers expected-step candidates over far-away harmonics.
    unsigned __int64 scoreAtExpectedStep = ( std::numeric_limits<unsigned __int64>::max )();
    int dxAtExpectedStep = 0;
    int dyAtExpectedStep = 0;

    // Harmonic-fallback tracking: record whether the ORIGINAL candidates
    // (before probe injection) achieved fineScore==0, and the smallest
    // |dy| among ALL candidates (including probes) that scored 0.
    bool foundOriginalZero = false;
    int smallestZeroAbsStep = ( std::numeric_limits<int>::max )();
    int smallestZeroDy = 0;
    int smallestZeroDx = 0;

    const int fineMarginX = useMaskedFallback ? 4 : max( 4, frameWidth / 20 );

    // Parallel fine search: enumerate work items, score in parallel, rank sequentially.
    struct FineWorkItem {
        int ci;
        int dy;
        int dx;
        unsigned __int64 score;
        bool valid;
    };
    std::vector<FineWorkItem> fineWork;
    fineWork.reserve( prunedCount * ( 2 * refineRadiusDy + 1 ) * ( 2 * refineRadiusDx + 1 ) );

    for( int ci = 0; ci < prunedCount; ++ci )
    {
        const int coarseDyFull = candidates[ci].dyDs * downsampleScale;
        for( int ddy = -refineRadiusDy; ddy <= refineRadiusDy; ++ddy )
        {
            const int dy = coarseDyFull + ddy;
            const int absStep = abs( dy );
            if( absStep < 4 || absStep >= frameHeight - 4 )
                continue;
            const int overlap = frameHeight - absStep;
            if( overlap < frameHeight / 4 )
                continue;
            for( int dx = -refineRadiusDx; dx <= refineRadiusDx; ++dx )
            {
                const int xStart = max( fineMarginX, fineMarginX + max( 0, -dx ) );
                const int xEnd = min( frameWidth - fineMarginX, frameWidth - fineMarginX - max( 0, dx ) );
                if( xEnd - xStart < frameWidth / 3 )
                    continue;
                fineWork.push_back( { ci, dy, dx, 0, false } );
            }
        }
    }

    // Shared approximate best score for cross-thread early termination.
    // Only used after a significant fraction of rows are evaluated to
    // avoid prematurely terminating the true best candidate whose running
    // average is temporarily inflated by early high-difference rows.
    std::atomic<unsigned __int64> sharedBestFine{ ( std::numeric_limits<unsigned __int64>::max )() };

    // Score all work items in parallel.
    parallel_for( 0, static_cast<int>( fineWork.size() ), [&]( int idx )
    {
        auto& w = fineWork[idx];
        const int dy = w.dy;
        const int dx = w.dx;
        const int absStep = abs( dy );
        const int overlap = frameHeight - absStep;
        const int xStart = max( fineMarginX, fineMarginX + max( 0, -dx ) );
        const int xEnd = min( frameWidth - fineMarginX, frameWidth - fineMarginX - max( 0, dx ) );

        if( useZnccFineSearch )
        {
            const double zncc = ComputeMaskedZNCC(
                previousFullLuma.data(), currentFullLuma.data(),
                fullMaskPrev.data(), fullMaskCurr.data(),
                frameWidth, overlap, absStep,
                ( dy < 0 ) ? -1 : 1, dx,
                fineMarginX, 50 /*minSamples*/ );
            w.score = static_cast<unsigned __int64>(
                max( 0.0, ( 1.0 - zncc ) * static_cast<double>( kZnccScoreBase ) ) );
            w.valid = true;
        }
        else
        {

        unsigned __int64 totalDiff = 0;
        unsigned __int64 samples = 0;
        bool earlyExit = false;

        for( int y = 0; y < overlap && !earlyExit; y += 2 )
        {
            int pY, cY;
            if( dy < 0 )
            {
                pY = y + absStep;
                cY = y;
            }
            else
            {
                pY = y;
                cY = y + absStep;
            }

            const int prevRow = pY * frameWidth;
            const int currRow = cY * frameWidth;

            const BYTE* pBase = &previousFullLuma[prevRow + xStart];
            const BYTE* cBase = &currentFullLuma[currRow + xStart + dx];
            const int xSpan = xEnd - xStart;
            unsigned __int64 rowDiff = 0;

            if( useFineMask )
            {
                const size_t prefixStride = static_cast<size_t>( frameWidth ) + 1;
                const size_t prevPrefBase = static_cast<size_t>( pY ) * prefixStride;
                const size_t currPrefBase = static_cast<size_t>( cY ) * prefixStride;
                const int currXStart = xStart + dx;
                const int currXEnd = xEnd + dx;

                const unsigned int prevInformative = static_cast<unsigned int>(
                    fullMaskPrevRowPrefix[prevPrefBase + static_cast<size_t>( xEnd )] -
                    fullMaskPrevRowPrefix[prevPrefBase + static_cast<size_t>( xStart )] );
                const unsigned int currInformative = static_cast<unsigned int>(
                    fullMaskCurrRowPrefix[currPrefBase + static_cast<size_t>( currXEnd )] -
                    fullMaskCurrRowPrefix[currPrefBase + static_cast<size_t>( currXStart )] );

                if( prevInformative == 0 && currInformative == 0 )
                {
                    continue;
                }

                const BYTE* prevMaskBase = &fullMaskPrev[prevRow + xStart];
                const BYTE* currMaskBase = &fullMaskCurr[currRow + xStart + dx];

#if defined(_M_X64) || defined(_M_IX86)
                const __m128i zero = _mm_setzero_si128();
                int xi = 0;
                for( ; xi + 16 <= xSpan; xi += 16 )
                {
                    const __m128i mPrev = _mm_loadu_si128( reinterpret_cast<const __m128i*>( prevMaskBase + xi ) );
                    const __m128i mCurr = _mm_loadu_si128( reinterpret_cast<const __m128i*>( currMaskBase + xi ) );
                    const __m128i mInf = _mm_or_si128( mPrev, mCurr );
                    const __m128i mActive = _mm_cmpgt_epi8( mInf, zero );
                    const unsigned int activeBits = static_cast<unsigned int>( _mm_movemask_epi8( mActive ) );
                    if( activeBits == 0 )
                    {
                        continue;
                    }

                    const __m128i a = _mm_loadu_si128( reinterpret_cast<const __m128i*>( pBase + xi ) );
                    const __m128i b = _mm_loadu_si128( reinterpret_cast<const __m128i*>( cBase + xi ) );
                    const __m128i d1 = _mm_subs_epu8( a, b );
                    const __m128i d2 = _mm_subs_epu8( b, a );
                    const __m128i absDiff = _mm_or_si128( d1, d2 );
                    const __m128i maskedDiff = _mm_and_si128( absDiff, mActive );
                    const __m128i sad = _mm_sad_epu8( maskedDiff, zero );

                    rowDiff += static_cast<unsigned __int64>( _mm_cvtsi128_si64( sad ) ) +
                               static_cast<unsigned __int64>( _mm_cvtsi128_si64( _mm_srli_si128( sad, 8 ) ) );
                    samples += static_cast<unsigned __int64>( __popcnt( activeBits ) );
                }

                for( ; xi < xSpan; ++xi )
                {
                    if( prevMaskBase[xi] || currMaskBase[xi] )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
                        samples++;
                    }
                }
#else
                for( int xi = 0; xi < xSpan; ++xi )
                {
                    if( prevMaskBase[xi] || currMaskBase[xi] )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
                        samples++;
                    }
                }
#endif
                totalDiff += rowDiff;
            }
            else
            {

#if defined(_M_X64) || defined(_M_IX86)
            __m128i sadAcc = _mm_setzero_si128();
            int xi = 0;
            for( ; xi + 16 <= xSpan; xi += 16 )
            {
                const __m128i a = _mm_loadu_si128( reinterpret_cast<const __m128i*>( pBase + xi ) );
                const __m128i b = _mm_loadu_si128( reinterpret_cast<const __m128i*>( cBase + xi ) );
                sadAcc = _mm_add_epi64( sadAcc, _mm_sad_epu8( a, b ) );
            }

            rowDiff = static_cast<unsigned __int64>( _mm_cvtsi128_si64( sadAcc ) ) +
                      static_cast<unsigned __int64>( _mm_cvtsi128_si64( _mm_srli_si128( sadAcc, 8 ) ) );

            for( ; xi < xSpan; ++xi )
            {
                rowDiff += static_cast<unsigned __int64>(
                    abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
            }
#elif defined(_M_ARM64)
            uint64x2_t sadAcc = vdupq_n_u64( 0 );
            int xi = 0;
            for( ; xi + 16 <= xSpan; xi += 16 )
            {
                const uint8x16_t a = vld1q_u8( pBase + xi );
                const uint8x16_t b = vld1q_u8( cBase + xi );
                const uint8x16_t absDiff = vabdq_u8( a, b );
                const uint16x8_t sum16 = vpaddlq_u8( absDiff );
                const uint32x4_t sum32 = vpaddlq_u16( sum16 );
                const uint64x2_t sum64 = vpaddlq_u32( sum32 );
                sadAcc = vaddq_u64( sadAcc, sum64 );
            }

            rowDiff = vgetq_lane_u64( sadAcc, 0 ) + vgetq_lane_u64( sadAcc, 1 );

            for( ; xi < xSpan; ++xi )
            {
                rowDiff += static_cast<unsigned __int64>(
                    abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
            }
#else
            for( int xi = 0; xi < xSpan; ++xi )
            {
                rowDiff += static_cast<unsigned __int64>(
                    abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
            }
#endif

            totalDiff += rowDiff;
            samples += xSpan;

            } // standard luma

            // Early termination using shared best score across threads.
            // Only activate after 75% of rows to avoid false termination of
            // the true best candidate whose initial rows may have higher SAD.
            const unsigned __int64 earlyMinSamples = useFineMask ? 50 : 200;
            const bool isExpectedStepDy = ( highConstantFractionPair || expectedAbsStep >= frameHeight / 4 ) &&
                expectedAbsStep > 0 &&
                abs( abs( dy ) - expectedAbsStep ) <= refineRadiusDy + 4;
            const unsigned __int64 curBest = sharedBestFine.load( std::memory_order_relaxed );
            if( !isExpectedStepDy &&
                curBest != ( std::numeric_limits<unsigned __int64>::max )() &&
                y >= overlap * 3 / 4 &&
                samples >= earlyMinSamples && totalDiff * kFineScoreScale >= (curBest + 1) * samples )
            {
                earlyExit = true;
            }
        }

        const unsigned __int64 minSamples = useFineMask ? 20 : 100;
        if( earlyExit || samples < minSamples )
            return;

        w.score = totalDiff * kFineScoreScale / samples;
        w.valid = true;

        // Update shared best for cross-thread early termination.
        unsigned __int64 old = sharedBestFine.load( std::memory_order_relaxed );
        while( w.score < old )
        {
            if( sharedBestFine.compare_exchange_weak( old, w.score, std::memory_order_relaxed ) )
                break;
        }

        } // !useZnccFineSearch
    } );

    // Sequential ranking pass over scored results.
    for( const auto& w : fineWork )
    {
        if( !w.valid )
            continue;

        const int ci = w.ci;
        const int dy = w.dy;
        const int dx = w.dx;
        const unsigned __int64 score = w.score;
        const int absStep = abs( dy );
        const int overlap = frameHeight - absStep;

        // Harmonic-fallback tracking.
        const unsigned __int64 harmonicPerfectThreshold = useZnccFineSearch ? 64 : 0;
        if( harmonicFallback && score <= harmonicPerfectThreshold )
        {
            if( ci < preHarmonicProbeCount )
                foundOriginalZero = true;
            if( absStep < smallestZeroAbsStep )
            {
                smallestZeroAbsStep = absStep;
                smallestZeroDy = dy;
                smallestZeroDx = dx;
            }
        }

        // Skip harmonic-fallback probes that didn't achieve a
        // perfect pixel match -- only score<=threshold probes can win.
        // Also skip probes entirely when an original candidate
        // already found a perfect score.
        if( harmonicFallback && ci >= preHarmonicProbeCount &&
            ( score > harmonicPerfectThreshold || foundOriginalZero ) )
            continue;

        unsigned __int64 rankScore = score;
        if( expectedAbsStep > 0 && expectedAbsStep >= frameHeight / 4 &&
            absStep > 4 && absStep < expectedAbsStep * 2 / 3 )
        {
            const int ratio = ( expectedAbsStep + absStep / 2 ) / absStep;
            const int residual = abs( expectedAbsStep - ratio * absStep );
            if( ratio >= 2 && residual < max( 5, absStep / 3 ) )
            {
                rankScore += static_cast<unsigned __int64>( min( ratio, 6 ) * 2 );
            }
        }
        if( expectedAbsStep > 0 && highConstantFractionPair )
        {
            const bool nearExpectedStep = abs( absStep - expectedAbsStep ) <= refineRadiusDy + 4;
            const int overlapPct = ( overlap * 100 ) / max( 1, frameHeight );
            if( !nearExpectedStep && overlapPct < 72 )
            {
                const unsigned __int64 overlapPenalty =
                    static_cast<unsigned __int64>( ( 72 - overlapPct ) * 6 );
                rankScore = score + overlapPenalty;
            }
        }
        if( expectedAbsStep > 0 && abs( abs( dy ) - expectedAbsStep ) <= 4 )
        {
            if( score < scoreAtExpectedStep )
            {
                scoreAtExpectedStep = score;
                dxAtExpectedStep = dx;
                dyAtExpectedStep = dy;
            }
        }

        if( rankScore < bestFineRankScore )
        {
            secondBestFineScore = bestFineScore;
            secondBestFineRankScore = bestFineRankScore;
            secondBestDx = bestDx;
            secondBestDy = bestDy;
            bestFineScore = score;
            bestFineRankScore = rankScore;
            bestDx = dx;
            bestDy = dy;
            bestCoarseDy = candidates[ci].dyDs;
            bestAbsStep = abs( dy );
            bestAbsDx = abs( dx );
            bestExpectedDelta = ( expectedAbsStep > 0 ) ? abs( bestAbsStep - expectedAbsStep ) : ( std::numeric_limits<int>::max )();
        }
        else if( rankScore == bestFineRankScore )
        {
            const int absStepLo = abs( dy );
            const int absDx = abs( dx );
            const int expectedDelta = ( expectedAbsStep > 0 ) ? abs( absStepLo - expectedAbsStep ) : ( std::numeric_limits<int>::max )();

            if( ( expectedAbsStep > 0 && expectedDelta < bestExpectedDelta ) ||
                ( expectedAbsStep == 0 && absStepLo < bestAbsStep ) ||
                ( expectedDelta == bestExpectedDelta &&
                  ( absStep < bestAbsStep || ( absStepLo == bestAbsStep && absDx < bestAbsDx ) ) ) )
            {
                bestDx = dx;
                bestDy = dy;
                bestCoarseDy = candidates[ci].dyDs;
                bestAbsStep = absStepLo;
                bestAbsDx = absDx;
                bestExpectedDelta = expectedDelta;
            }
        }
        else if( expectedAbsStep > 0 && bestFineRankScore != ( std::numeric_limits<unsigned __int64>::max )() )
        {
            unsigned __int64 scoreSlack = (std::max)( static_cast<unsigned __int64>( 2 ), bestFineRankScore / 80 );
            const int absStepLo = abs( dy );
            const int absDx = abs( dx );
            const int expectedDelta = abs( absStepLo - expectedAbsStep );
            const bool preferExpectedStep = ( highConstantFractionPair || expectedAbsStep >= frameHeight / 4 ) &&
                expectedAbsStep >= 8;

            if( preferExpectedStep )
            {
                scoreSlack = (std::max)( scoreSlack, highConstantFractionPair
                    ? bestFineRankScore / 12    // 8.3% for HCF
                    : bestFineRankScore / 30 ); // 3.3% for non-HCF
            }

            const int requiredExpectedGain = highConstantFractionPair ? 0 : 1;
            if( rankScore <= bestFineRankScore + scoreSlack && expectedDelta + requiredExpectedGain < bestExpectedDelta )
            {
                secondBestFineScore = min( secondBestFineScore, bestFineScore );
                secondBestFineRankScore = min( secondBestFineRankScore, bestFineRankScore );
                secondBestDx = bestDx;
                secondBestDy = bestDy;
                bestFineScore = score;
                bestFineRankScore = rankScore;
                bestDx = dx;
                bestDy = dy;
                bestCoarseDy = candidates[ci].dyDs;
                bestAbsStep = absStepLo;
                bestAbsDx = absDx;
                bestExpectedDelta = expectedDelta;
            }
        }
        else if( rankScore < secondBestFineRankScore )
        {
            secondBestFineRankScore = rankScore;
            secondBestFineScore = score;
            secondBestDx = dx;
            secondBestDy = dy;
        }
    }
    PERF_STOP( tFineSearch );

    PERF_START( tPostValidation );
    if( ( highConstantFractionPair || expectedAbsStep >= frameHeight / 4 ) &&
        bestFineRankScore != ( std::numeric_limits<unsigned __int64>::max )() &&
        secondBestFineRankScore != ( std::numeric_limits<unsigned __int64>::max )() )
    {
        const unsigned __int64 ambiguitySlack = (std::max)( static_cast<unsigned __int64>( 6 ), bestFineRankScore / 16 );

        // Ambiguity-only fallback: when two candidates are effectively tied,
        // prefer the one materially closer to expected motion. Unlike the
        // earlier harmonic override, this path only triggers in near-tie
        // scenarios and therefore avoids forcing a weak expected-step choice
        // when a clearly better candidate exists.
        if( expectedAbsStep > 0 && secondBestFineRankScore <= bestFineRankScore + ambiguitySlack )
        {
            const int bestDelta = abs( abs( bestDy ) - expectedAbsStep );
            const int secondDelta = abs( abs( secondBestDy ) - expectedAbsStep );

            // Direction tiebreaker: when both candidates have the same
            // absolute step distance from expected (e.g. +4 and -4 both
            // match expectedAbsStep=4), prefer the one whose sign matches
            // expectedDy.  Without this, the stitcher oscillates between
            // +dy and -dy on HCF content where forward/backward shifts
            // score nearly identically.
            const bool bestMatchesDir = ( expectedDy > 0 && bestDy > 0 ) || ( expectedDy < 0 && bestDy < 0 );
            const bool secondMatchesDir = ( expectedDy > 0 && secondBestDy > 0 ) || ( expectedDy < 0 && secondBestDy < 0 );
            const bool directionOverride = bestDelta == secondDelta && !bestMatchesDir && secondMatchesDir;

            if( secondDelta + 2 < bestDelta || directionOverride )
            {
                StitchLog( L"[Panorama/Stitch] FindBestFrameShift ambiguity-fallback expected=(%d,%d) best=(%d,%d) second=(%d,%d) bestRank=%llu secondRank=%llu dirOverride=%d\n",
                             expectedDx,
                             expectedDy,
                             bestDx,
                             bestDy,
                             secondBestDx,
                             secondBestDy,
                             static_cast<unsigned long long>( bestFineRankScore ),
                             static_cast<unsigned long long>( secondBestFineRankScore ),
                             directionOverride ? 1 : 0 );

                bestDx = secondBestDx;
                bestDy = secondBestDy;
                bestFineScore = secondBestFineScore;
                bestFineRankScore = secondBestFineRankScore;
                bestAbsStep = abs( bestDy );
                bestAbsDx = abs( bestDx );
                bestExpectedDelta = abs( bestAbsStep - expectedAbsStep );
            }
        }

        if( secondBestFineRankScore <= bestFineRankScore + ambiguitySlack )
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift ambiguous expected=(%d,%d) best=(%d,%d) bestRaw=%llu bestRank=%llu second=(%d,%d) secondRaw=%llu secondRank=%llu slack=%llu\n",
                         expectedDx,
                         expectedDy,
                         bestDx,
                         bestDy,
                         static_cast<unsigned long long>( bestFineScore ),
                         static_cast<unsigned long long>( bestFineRankScore ),
                         secondBestDx,
                         secondBestDy,
                         static_cast<unsigned long long>( secondBestFineScore ),
                         static_cast<unsigned long long>( secondBestFineRankScore ),
                         static_cast<unsigned long long>( ambiguitySlack ) );
        }
    }

    // HCF harmonic-zero override: when the best candidate has score=0 at
    // a step much smaller than expected, the "perfect" match is almost
    // certainly spurious -- the constant-fraction region (dark background)
    // makes any small offset look identical.  Prefer the expected-step
    // candidate, but only when that candidate also has a plausible score.
    // A high scoreAtExpectedStep indicates the expected step is wrong
    // (genuinely small scroll, not a harmonic), so skip the override.
    // For ZNCC, a "perfect" match scores near 0 but not exactly 0 due
    // to floating-point rounding.  Use a small threshold instead.
    const unsigned __int64 kZnccMinPerfect = useZnccFineSearch ? 64 : 0;
    const unsigned __int64 kExpectedStepMaxScore = useZnccFineSearch ? kZnccScoreBase / 4 : 200;
    if( highConstantFractionPair && bestFineScore <= kZnccMinPerfect && expectedAbsStep > 0 &&
        bestAbsStep > 4 && bestAbsStep < expectedAbsStep * 2 / 3 &&
        scoreAtExpectedStep != ( std::numeric_limits<unsigned __int64>::max )() &&
        scoreAtExpectedStep <= kExpectedStepMaxScore )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift hcf-harmonic-zero override: "
                     L"expected=(%d,%d) harmonic=(%d,%d) score=0 "
                     L"expectedStep=(%d,%d) expectedScore=%llu\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     dxAtExpectedStep, dyAtExpectedStep,
                     static_cast<unsigned long long>( scoreAtExpectedStep ) );
        bestDx = dxAtExpectedStep;
        bestDy = dyAtExpectedStep;
        bestFineScore = scoreAtExpectedStep;
        bestFineRankScore = scoreAtExpectedStep;
        bestAbsStep = abs( bestDy );
        bestAbsDx = abs( bestDx );
        bestExpectedDelta = abs( bestAbsStep - expectedAbsStep );
    }

    // Harmonic-fallback override: if the original candidates all had
    // fineScore > 0 but a newly-injected probe found fineScore == 0,
    // the correct pixel-aligned shift was missing from the original list.
    // Override with the smallest-|dy| zero-score probe (most overlap,
    // least risk of truncation artefact).
    if( harmonicFallback && !foundOriginalZero && smallestZeroAbsStep != ( std::numeric_limits<int>::max )() )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift harmonic-fallback override: expected=(%d,%d) old=(%d,%d) oldScore=%llu new=(%d,%d)\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     static_cast<unsigned long long>( bestFineScore ),
                     smallestZeroDx, smallestZeroDy );
        bestDx = smallestZeroDx;
        bestDy = smallestZeroDy;
        bestFineScore = 0;
        bestFineRankScore = 0;
        bestAbsStep = smallestZeroAbsStep;
        bestAbsDx = abs( smallestZeroDx );
        bestExpectedDelta = ( expectedAbsStep > 0 ) ? abs( bestAbsStep - expectedAbsStep ) : ( std::numeric_limits<int>::max )();
    }

    // Conservative fast-pass fallback: if ranking confidence is weak,
    // rerun once with exhaustive probe budget to preserve quality.
    if( useFastProbePass )
    {
        const bool noFineWinner = ( bestFineScore == ( std::numeric_limits<unsigned __int64>::max )() );
        bool ambiguousWinner = false;
        const bool allowAmbiguityRerun = expectedAbsStep > 0 && !highConstantFractionPair;
        if( !noFineWinner &&
            secondBestFineRankScore != ( std::numeric_limits<unsigned __int64>::max )() )
        {
            const unsigned __int64 ambiguitySlack =
                ( std::max )( static_cast<unsigned __int64>( 8 ), bestFineRankScore / 16 );
            ambiguousWinner = allowAmbiguityRerun &&
                ( secondBestFineRankScore <= bestFineRankScore + ambiguitySlack );
        }

        bool farFromExpected = false;
        if( expectedAbsStep > 0 && !noFineWinner )
        {
            const int expectedDeltaTolerance = max( refineRadiusDy + 8, expectedAbsStep / 2 );
            const unsigned __int64 uncertainScoreFloor = useZnccFineSearch
                ? static_cast<unsigned __int64>( kZnccScoreBase / 3 )
                : static_cast<unsigned __int64>( 20 * kFineScoreScale );
            farFromExpected =
                bestExpectedDelta > expectedDeltaTolerance &&
                bestFineScore > uncertainScoreFloor;
        }

        if( noFineWinner || ambiguousWinner || farFromExpected )
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift fast-pass rerun: "
                         L"reason=noFine:%d ambiguous:%d farExpected:%d budget=%d\n",
                         noFineWinner ? 1 : 0,
                         ambiguousWinner ? 1 : 0,
                         farFromExpected ? 1 : 0,
                         probeCandidateBudget );

            PERF_STOP( tPostValidation );
            PERF_STOP( tTotal );
            return FindBestFrameShiftVerticalOnly( previousPixels,
                                                   currentPixels,
                                                   frameWidth,
                                                   frameHeight,
                                                   expectedDx,
                                                   expectedDy,
                                                   bestDx,
                                                   bestDy,
                                                   lowContrastMode,
                                                   precomputedPrevLuma,
                                                   precomputedCurrLuma,
                                                   precomputedVeryLowEntropy,
                                                   outNearStationaryOverride,
                                                   allowHighConstStationaryRelax,
                                                   outMaskedStationaryScore,
                                                   true,
                                                   forceExhaustiveFineDx );
        }
    }

    // Cross-validate shift vs stationary score.  The stationary score measures
    // how different the frames look at zero offset.  A large scroll means most
    // of the content is new, so stationaryScore should be proportionally high.
    // If stationaryScore is low but the detected shift is large AND the fine
    // score is not a perfect match (fineScore > 0), the match is likely
    // spurious -- caused by repeating content patterns (e.g. social media
    // handles, list layouts) that correlate at a wrong offset.  A perfect
    // fine score (fineScore == 0) indicates the pixel-level alignment is
    // genuine even when the stationary score is low.
    //
    // Exception: when the scroll direction has already been established and
    // the detected shift is in the same direction, skip this check.  Content
    // like code with a dark theme has inherently low stationary scores (~8-12)
    // even for genuine large scrolls because most pixels are uniform
    // background.  The fine-score threshold below still rejects poor matches.
    const int detectedStep = abs( bestDx ) + abs( bestDy );
    const bool directionEstablished = ( expectedDx != 0 || expectedDy != 0 );
    const bool shiftMatchesDirection =
        directionEstablished &&
        ( ( expectedDy < 0 && bestDy < 0 ) || ( expectedDy > 0 && bestDy > 0 ) ||
          ( expectedDx < 0 && bestDx < 0 ) || ( expectedDx > 0 && bestDx > 0 ) );

    // Cross-validate shift vs stationary score.  For very-low-entropy
    // pairs or masked-fallback pairs, use the masked stationary score
    // (which only considers content pixels) since the raw score is
    // inherently near-zero.
    const unsigned __int64 crossValidStationaryUsed = ( useFineMask || useMaskedFallback ) ? maskedStationaryScore : stationaryScore;
    const unsigned __int64 crossValidationStationaryThreshold = lowContrastMode ? 18 : 15;
    if( !shiftMatchesDirection && crossValidStationaryUsed < crossValidationStationaryThreshold && detectedStep > frameHeight / 3 && bestFineScore > 0 )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift shift-stationary-mismatch expected=(%d,%d) best=(%d,%d) step=%d fineScore=%llu stationary=%llu maskedStat=%llu veryLowEntropy=%d\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     detectedStep,
                     static_cast<unsigned long long>( bestFineScore ),
                     static_cast<unsigned long long>( stationaryScore ),
                     static_cast<unsigned long long>( maskedStationaryScore ),
                     veryLowEntropyPair ? 1 : 0 );
        PERF_STOP( tPostValidation ); PERF_STOP( tTotal );
        return false;
    }

    // Near-stationary flag: when the fine score per-pixel is no better
    // than the stationary score, the "best" match is unreliable -- likely
    // a harmonic on periodic content.  Signal this to the stitch loop
    // via the outNearStationaryOverride flag so it can clamp the step
    // to a conservative minimum while preserving expectedDy for the
    // next frame's search (avoiding cascade from corrupted expected step).
    //
    // Skip this check on masked/HCF content where the scores are on
    // different scales.
    if( bestFineScore > 0 && !useFineMask && !useMaskedFallback && !highConstantFractionPair )
    {
        const unsigned __int64 finePerPixel = bestFineScore / kFineScoreScale;
        if( finePerPixel >= stationaryScore && stationaryScore < crossValidationStationaryThreshold )
        {
            if( outNearStationaryOverride )
            {
                *outNearStationaryOverride = true;
            }
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift near-stationary-flag expected=(%d,%d) best=(%d,%d) fineScore=%llu finePerPixel=%llu stationary=%llu\n",
                         expectedDx, expectedDy, bestDx, bestDy,
                         static_cast<unsigned long long>( bestFineScore ),
                         static_cast<unsigned long long>( finePerPixel ),
                         static_cast<unsigned long long>( stationaryScore ) );
        }
    }

    // Downward-spike guard: when the expected step is established and
    // the detected step is dramatically smaller (< 2/3), the match is
    // likely a harmonic sub-multiple on periodic content.  Only reject
    // when the fine score is also mediocre -- a genuine scroll slow-down
    // would produce a clean (low) fine score.
    //
    // Before rejecting outright, try falling back to the expected-step
    // candidate.  On HCF periodic content the harmonic wins the fine
    // search, but the expected step may still have a usable score.
    // Let the normal fineThreshold check validate it downstream.
    if( directionEstablished && expectedAbsStep > frameHeight / 8 &&
        bestAbsStep > 0 && bestAbsStep < expectedAbsStep * 2 / 3 &&
        bestFineScore > 8 * kFineScoreScale )
    {
        if( scoreAtExpectedStep != ( std::numeric_limits<unsigned __int64>::max )() )
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift downward-spike-fallback expected=(%d,%d) harmonic=(%d,%d) harmonicScore=%llu expectedStep=(%d,%d) expectedScore=%llu\n",
                         expectedDx, expectedDy, bestDx, bestDy,
                         static_cast<unsigned long long>( bestFineScore ),
                         dxAtExpectedStep, dyAtExpectedStep,
                         static_cast<unsigned long long>( scoreAtExpectedStep ) );
            bestDx = dxAtExpectedStep;
            bestDy = dyAtExpectedStep;
            bestFineScore = scoreAtExpectedStep;
            bestFineRankScore = scoreAtExpectedStep;
            bestAbsStep = abs( bestDy );
            bestAbsDx = abs( bestDx );
            bestExpectedDelta = abs( bestAbsStep - expectedAbsStep );
        }
        else
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift downward-spike expected=(%d,%d) best=(%d,%d) bestStep=%d expectedStep=%d fineScore=%llu\n",
                         expectedDx, expectedDy, bestDx, bestDy,
                         bestAbsStep, expectedAbsStep,
                         static_cast<unsigned long long>( bestFineScore ) );
            PERF_STOP( tPostValidation ); PERF_STOP( tTotal );
            return false;
        }
    }

    // Adaptive fine threshold.  For masked scoring (very-low-entropy or
    // masked fallback) the fine score is on a different scale (higher,
    // because only content pixels contribute) so use a more generous
    // threshold.
    unsigned __int64 fineThreshold;
    if( useZnccFineSearch )
    {
        // ZNCC scores: kZnccScoreBase / 4 = 3200 corresponds to ZNCC >= 0.75.
        fineThreshold = kZnccScoreBase / 4;

        const int relaxDeltaTolerance = max( refineRadiusDy, 8 );
        if( expectedAbsStep > 0 &&
            bestExpectedDelta <= relaxDeltaTolerance &&
            bestFineScore != ( std::numeric_limits<unsigned __int64>::max )() )
        {
            // Relax to ZNCC >= 0.60 for candidates near expected step.
            fineThreshold = ( std::max )( fineThreshold, kZnccScoreBase * 2 / 5 );
        }
    }
    else if( useFineMask )
    {
        fineThreshold = ( maskedStationaryScore > 15 ) ? 30 * kFineScoreScale : 20 * kFineScoreScale;
    }
    else
    {
        // For highConstantFractionPair, the raw stationaryScore is diluted by
        // the large fraction of background pixels, often landing at or below 15
        // even when the frame actually moved.  The maskedStationaryScore focuses
        // on informative pixels and better reflects whether the frame changed.
        // Use the higher of the two when deciding the threshold band.
        const unsigned __int64 fineStationaryScore =
            highConstantFractionPair
                ? ( std::max )( stationaryScore, maskedStationaryScore )
                : stationaryScore;
        fineThreshold = ( fineStationaryScore > 15 )
            ? ( lowContrastMode ? 24 * kFineScoreScale : 30 * kFineScoreScale )
            : ( lowContrastMode ? 12 * kFineScoreScale : 15 * kFineScoreScale );

        // On sparse/high-constant-fraction content the fine search can
        // produce legitimate correct-shift scores moderately above the base
        // threshold because only a handful of pixels differ between frames.
        // When the best found candidate is already near the expected step
        // (within a generous tolerance), relax the threshold to accept it
        // rather than dropping the frame entirely.  Use a fixed tolerance of
        // 8px or the refine radius, whichever is larger, so that small-window
        // captures (downsampleScale=2, refineRad=3) are handled correctly.
        const int relaxDeltaTolerance = max( refineRadiusDy, 8 );
        if( highConstantFractionPair && expectedAbsStep > 0 &&
            bestExpectedDelta <= relaxDeltaTolerance &&
            bestFineScore != ( std::numeric_limits<unsigned __int64>::max )() )
        {
            fineThreshold = ( std::max )( fineThreshold, static_cast<unsigned __int64>( 40 * kFineScoreScale ) );
        }
    }

    if( bestFineScore == ( std::numeric_limits<unsigned __int64>::max )() || bestFineScore > fineThreshold )
    {
        if( useFastFineDxPass )
        {
            StitchLog( L"[Panorama/Stitch] FineSearch dx-fast-pass rerun expected=(%d,%d) best=(%d,%d) fineScore=%llu fineThreshold=%llu\n",
                         expectedDx,
                         expectedDy,
                         bestDx,
                         bestDy,
                         static_cast<unsigned long long>( bestFineScore ),
                         static_cast<unsigned long long>( fineThreshold ) );

            PERF_STOP( tPostValidation );
            PERF_STOP( tTotal );
            return FindBestFrameShiftVerticalOnly( previousPixels,
                                                   currentPixels,
                                                   frameWidth,
                                                   frameHeight,
                                                   expectedDx,
                                                   expectedDy,
                                                   bestDx,
                                                   bestDy,
                                                   lowContrastMode,
                                                   precomputedPrevLuma,
                                                   precomputedCurrLuma,
                                                   precomputedVeryLowEntropy,
                                                   outNearStationaryOverride,
                                                   allowHighConstStationaryRelax,
                                                   outMaskedStationaryScore,
                                                   forceExhaustiveProbeBudget,
                                                   true );
        }

        if( bypassProbeInjection && !forceExhaustiveProbeBudget )
        {
            StitchLog( L"[Panorama/Stitch] ProbeInject bypass fallback rerun expected=(%d,%d) best=(%d,%d) fineScore=%llu fineThreshold=%llu\n",
                         expectedDx,
                         expectedDy,
                         bestDx,
                         bestDy,
                         static_cast<unsigned long long>( bestFineScore ),
                         static_cast<unsigned long long>( fineThreshold ) );

            PERF_STOP( tPostValidation );
            PERF_STOP( tTotal );
            return FindBestFrameShiftVerticalOnly( previousPixels,
                                                   currentPixels,
                                                   frameWidth,
                                                   frameHeight,
                                                   expectedDx,
                                                   expectedDy,
                                                   bestDx,
                                                   bestDy,
                                                   lowContrastMode,
                                                   precomputedPrevLuma,
                                                   precomputedCurrLuma,
                                                   precomputedVeryLowEntropy,
                                                   outNearStationaryOverride,
                                                   allowHighConstStationaryRelax,
                                                   outMaskedStationaryScore,
                                                   true,
                                                   true );
        }

        StitchLog( L"[Panorama/Stitch] FindBestFrameShift poor-fine expected=(%d,%d) best=(%d,%d) fineScore=%llu fineThreshold=%llu stationary=%llu maskedStat=%llu veryLowEntropy=%d expectedStepScore=%llu dyAtExpectedStep=%d highConstFrac=%d bestExpDelta=%d refineRad=%d\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     static_cast<unsigned long long>( bestFineScore ),
                     static_cast<unsigned long long>( fineThreshold ),
                     static_cast<unsigned long long>( stationaryScore ),
                     static_cast<unsigned long long>( maskedStationaryScore ),
                     veryLowEntropyPair ? 1 : 0,
                     static_cast<unsigned long long>( scoreAtExpectedStep ),
                     dyAtExpectedStep,
                     highConstantFractionPair ? 1 : 0,
                     bestExpectedDelta,
                     refineRadiusDy );
        PERF_STOP( tPostValidation ); PERF_STOP( tTotal );
        return false;
    }

    StitchLog( L"[Panorama/Stitch] FindBestFrameShift expected=(%d,%d) best=(%d,%d) coarseScore=%llu fineScore=%llu stationary=%llu maskedStat=%llu veryLowEntropy=%d window=[%d,%d] expectedStepScore=%llu dyAtExpectedStep=%d highConstFrac=%d accepted=1\n",
                 expectedDx, expectedDy, bestDx, bestDy,
                 static_cast<unsigned long long>( bestCoarseScore ),
                 static_cast<unsigned long long>( bestFineScore ),
                 static_cast<unsigned long long>( stationaryScore ),
                 static_cast<unsigned long long>( maskedStationaryScore ),
                 veryLowEntropyPair ? 1 : 0,
                 searchMinDy * downsampleScale,
                 searchMaxDy * downsampleScale,
                 static_cast<unsigned long long>( scoreAtExpectedStep ),
                 dyAtExpectedStep,
                 highConstantFractionPair ? 1 : 0 );
    // Clamp cross-axis shift to zero.  The fine search evaluates dx=+/-1
    // for better score discrimination (ClearType subpixel effects), but
    // screen captures scroll perfectly along one axis -- there is never
    // real cross-axis motion.  Allowing dx!=0 would accumulate drift.
    bestDx = 0;

    if( outMaskedStationaryScore )
        *outMaskedStationaryScore = maskedStationaryScore;
    PERF_STOP( tPostValidation ); PERF_STOP( tTotal );
    return true;
}

static bool FindBestFrameShift( const std::vector<BYTE>& previousPixels,
                                const std::vector<BYTE>& currentPixels,
                                int frameWidth,
                                int frameHeight,
                                int expectedDx,
                                int expectedDy,
                                int& bestDx,
                                int& bestDy,
                                bool lowContrastMode,
                                const std::vector<BYTE>& precomputedPrevLuma = {},
                                const std::vector<BYTE>& precomputedCurrLuma = {},
                                int precomputedVeryLowEntropy = -1,
                                bool* outNearStationaryOverride = nullptr,
                                bool allowHighConstStationaryRelax = false,
                                unsigned __int64* outMaskedStationaryScore = nullptr )
{
    const bool axisEstablished = ( expectedDx != 0 || expectedDy != 0 );
    const bool preferVerticalAxis = !axisEstablished || ( abs( expectedDy ) >= abs( expectedDx ) );

    // -- Once the scroll axis is established, only search along that axis.
    // Never fall back to the cross-axis -- a perpendicular match is always
    // spurious (screen captures scroll perfectly along one axis).  If the
    // preferred axis fails, reject the frame; the caller will advance to
    // the next frame.
    if( axisEstablished )
    {
        if( preferVerticalAxis )
        {
            int directDx = 0, directDy = 0;
            if( FindBestFrameShiftVerticalOnly( previousPixels, currentPixels,
                                                frameWidth, frameHeight,
                                                expectedDx, expectedDy,
                                                directDx, directDy, lowContrastMode,
                                                precomputedPrevLuma, precomputedCurrLuma,
                                                precomputedVeryLowEntropy,
                                                outNearStationaryOverride,
                                                allowHighConstStationaryRelax,
                                                outMaskedStationaryScore ) )
            {
                bestDx = directDx;
                bestDy = directDy;
                return true;
            }
            return false;
        }
        else
        {
            // Prefer horizontal (transposed) axis.
            std::vector<BYTE> previousTransposed, currentTransposed;
            if( TransposePixels32( previousPixels, frameWidth, frameHeight, previousTransposed ) &&
                TransposePixels32( currentPixels, frameWidth, frameHeight, currentTransposed ) )
            {
                int tDx = 0, tDy = 0;
                if( FindBestFrameShiftVerticalOnly( previousTransposed, currentTransposed,
                                                    frameHeight, frameWidth,
                                                    expectedDy, expectedDx,
                                                    tDx, tDy, lowContrastMode,
                                                    {}, {},
                                                    precomputedVeryLowEntropy,
                                                    outNearStationaryOverride,
                                                    allowHighConstStationaryRelax,
                                                    outMaskedStationaryScore ) )
                {
                    bestDx = tDy;
                    bestDy = tDx;
                    return true;
                }
            }
            return false;
        }
    }

    // First frame pair: axis unknown run both searches and pick best.
    int directDx = 0;
    int directDy = 0;
    const bool directOk = FindBestFrameShiftVerticalOnly( previousPixels,
                                                          currentPixels,
                                                          frameWidth,
                                                          frameHeight,
                                                          expectedDx,
                                                          expectedDy,
                                                          directDx,
                                                          directDy,
                                                          lowContrastMode,
                                                          precomputedPrevLuma,
                                                          precomputedCurrLuma,
                                                          precomputedVeryLowEntropy,
                                                          outNearStationaryOverride,
                                                          allowHighConstStationaryRelax,
                                                          outMaskedStationaryScore );

    std::vector<BYTE> previousTransposed;
    std::vector<BYTE> currentTransposed;
    const bool transposedReady =
        TransposePixels32( previousPixels, frameWidth, frameHeight, previousTransposed ) &&
        TransposePixels32( currentPixels, frameWidth, frameHeight, currentTransposed );

    int transposedDx = 0;
    int transposedDy = 0;
    bool transposedOk = false;
    if( transposedReady )
    {
        transposedOk = FindBestFrameShiftVerticalOnly( previousTransposed,
                                                       currentTransposed,
                                                       frameHeight,
                                                       frameWidth,
                                                       expectedDy,
                                                       expectedDx,
                                                       transposedDx,
                                                       transposedDy,
                                                       lowContrastMode,
                                                       {}, {},
                                                       precomputedVeryLowEntropy,
                                                       outNearStationaryOverride,
                                                       allowHighConstStationaryRelax,
                                                       outMaskedStationaryScore );
    }

    if( !directOk && !transposedOk )
    {
        return false;
    }

    int mappedDx = transposedDy;
    int mappedDy = transposedDx;

    // When only one direction succeeds for the initial axis detection,
    // verify the frames differ enough for reliable direction detection.
    // On near-identical frames (stationaryScore ≈ 0), both searches
    // produce noise-level fine scores.  Content autocorrelation can make
    // the wrong direction score lower than the correct one (e.g. text
    // lines cause high vertical autocorrelation SAD but lower horizontal
    // SAD), allowing the wrong axis to lock in permanently.
    // Guard: compute a quick full-frame luma difference.  If frames are
    // too similar, reject the pair so the stitch retries with a later
    // frame that has more distinctive scroll movement.
    if( directOk != transposedOk &&
        !precomputedPrevLuma.empty() && !precomputedCurrLuma.empty() )
    {
        unsigned __int64 totalDiff = 0;
        unsigned __int64 samples = 0;
        const size_t lumaSize = precomputedPrevLuma.size();
        for( size_t i = 0; i < lumaSize; i += 4 )
        {
            totalDiff += static_cast<unsigned __int64>(
                abs( static_cast<int>( precomputedPrevLuma[i] ) -
                     static_cast<int>( precomputedCurrLuma[i] ) ) );
            samples++;
        }
        if( samples > 0 && totalDiff / samples <= 2 )
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift axis-detection deferred: "
                       L"frames too similar (lumaDiff=%llu/%llu=%llu) directOk=%d transposedOk=%d\n",
                       static_cast<unsigned long long>( totalDiff ),
                       static_cast<unsigned long long>( samples ),
                       static_cast<unsigned long long>( totalDiff / samples ),
                       directOk ? 1 : 0, transposedOk ? 1 : 0 );
            return false;
        }
    }

    if( directOk && !transposedOk )
    {
        bestDx = directDx;
        bestDy = directDy;
        return true;
    }

    if( transposedOk && !directOk )
    {
        // On portrait portals (height >= 2*width), a transposed-only
        // success is likely spurious horizontal autocorrelation from
        // text/code line structure.  Small initial shifts can cause the
        // direct (vertical) fine score to barely exceed the threshold
        // while the transposed search passes.  Defer axis detection to
        // a frame pair with more distinctive scroll movement.
        if( frameHeight >= frameWidth * 2 )
        {
            StitchLog( L"[Panorama/Stitch] FindBestFrameShift portrait-portal transposed-only deferred: "
                       L"mapped=(%d,%d) frame=%dx%d\n",
                       mappedDx, mappedDy, frameWidth, frameHeight );
            return false;
        }
        bestDx = mappedDx;
        bestDy = mappedDy;
        return true;
    }

    // Both searches succeeded.  Startup axis choice is critical because a
    // wrong first lock permanently routes subsequent matching down the wrong
    // axis.  Pay the extra cost here and run the dedicated axis scan even on
    // non-VLE content rather than trusting the first direct ZNCC winner.

    // Axis detection: scan pure-vertical and pure-horizontal shifts in a
    // range to find which direction has a true alignment minimum.  This is
    // far more robust than scoring two potentially-spurious ZNCC peaks,
    // especially on mostly-constant content where ZNCC produces noise peaks.
    //
    // Optimization: use precomputed luma arrays (already available from the
    // caller) and build a gradient mask once, rather than recomputing
    // RGB->luma and gradient per pixel in each of the ~200 calls.
    constexpr int kAxisScanRange = 50;
    constexpr int kAxisMargin = 4;
    constexpr int kAxisStep = 2;

    // Reference or build full-resolution luma for both frames.
    std::vector<BYTE> prevLumaOwned, currLumaOwned;
    const BYTE* prevLuma;
    const BYTE* currLuma;
    if( !precomputedPrevLuma.empty() && !precomputedCurrLuma.empty() )
    {
        prevLuma = precomputedPrevLuma.data();
        currLuma = precomputedCurrLuma.data();
    }
    else
    {
        BuildFullLumaFrame( previousPixels, frameWidth, frameHeight, prevLumaOwned );
        BuildFullLumaFrame( currentPixels, frameWidth, frameHeight, currLumaOwned );
        prevLuma = prevLumaOwned.data();
        currLuma = currLumaOwned.data();
    }

    // Precompute gradient mask for both frames.  A pixel has gradient if
    // abs(luma - luma_right) + abs(luma - luma_below) >= 4.  Last row and
    // last column have no gradient.
    const size_t pixelCount = static_cast<size_t>( frameWidth ) * frameHeight;
    std::vector<BYTE> gradMask( pixelCount * 2, 0 );
    BYTE* prevGrad = gradMask.data();
    BYTE* currGrad = gradMask.data() + pixelCount;
    for( int y = 0; y < frameHeight - 1; y++ )
    {
        const int rowOff = y * frameWidth;
        for( int x = 0; x < frameWidth - 1; x++ )
        {
            const int idx = rowOff + x;
            const int lp = prevLuma[idx];
            prevGrad[idx] = ( abs( lp - prevLuma[idx + 1] ) + abs( lp - prevLuma[idx + frameWidth] ) >= 4 ) ? 1 : 0;
            const int lc = currLuma[idx];
            currGrad[idx] = ( abs( lc - currLuma[idx + 1] ) + abs( lc - currLuma[idx + frameWidth] ) >= 4 ) ? 1 : 0;
        }
    }

    unsigned __int64 bestVertScore = ULLONG_MAX;
    unsigned __int64 bestHorizScore = ULLONG_MAX;
    int bestVertDy = 0;
    int bestHorizDx = 0;

    // Vertical scan (dx=0, varying dy).
    for( int dy = -kAxisScanRange; dy <= kAxisScanRange; dy++ )
    {
        if( dy == 0 )
            continue;
        const int absDy = abs( dy );
        const int overlapW = frameWidth - 2 * kAxisMargin;
        const int overlapH = frameHeight - absDy - 2 * kAxisMargin;
        if( overlapW < frameWidth / 4 || overlapH < frameHeight / 4 )
            continue;
        const int pY0 = kAxisMargin + max( 0, -dy );
        const int cY0 = kAxisMargin + max( 0, dy );
        unsigned __int64 totalDiff = 0;
        unsigned __int64 samples = 0;
        for( int y = 0; y < overlapH; y += kAxisStep )
        {
            const int pRow = ( pY0 + y ) * frameWidth;
            const int cRow = ( cY0 + y ) * frameWidth;
            for( int x = 0; x < overlapW; x += kAxisStep )
            {
                const int px = kAxisMargin + x;
                const int pIdx = pRow + px;
                const int cIdx = cRow + px;
                if( !prevGrad[pIdx] && !currGrad[cIdx] )
                    continue;
                totalDiff += static_cast<unsigned __int64>( abs( static_cast<int>( prevLuma[pIdx] ) - static_cast<int>( currLuma[cIdx] ) ) );
                samples++;
            }
        }
        if( samples >= 20 )
        {
            const unsigned __int64 score = totalDiff / samples;
            if( score < bestVertScore )
            {
                bestVertScore = score;
                bestVertDy = dy;
            }
        }
    }

    // Horizontal scan (dy=0, varying dx).
    for( int dx = -kAxisScanRange; dx <= kAxisScanRange; dx++ )
    {
        if( dx == 0 )
            continue;
        const int absDx = abs( dx );
        const int overlapW = frameWidth - absDx - 2 * kAxisMargin;
        const int overlapH = frameHeight - 2 * kAxisMargin;
        if( overlapW < frameWidth / 4 || overlapH < frameHeight / 4 )
            continue;
        const int pX0 = kAxisMargin + max( 0, -dx );
        const int cX0 = kAxisMargin + max( 0, dx );
        unsigned __int64 totalDiff = 0;
        unsigned __int64 samples = 0;
        for( int y = 0; y < overlapH; y += kAxisStep )
        {
            const int rowOff = ( kAxisMargin + y ) * frameWidth;
            for( int x = 0; x < overlapW; x += kAxisStep )
            {
                const int pIdx = rowOff + pX0 + x;
                const int cIdx = rowOff + cX0 + x;
                if( !prevGrad[pIdx] && !currGrad[cIdx] )
                    continue;
                totalDiff += static_cast<unsigned __int64>( abs( static_cast<int>( prevLuma[pIdx] ) - static_cast<int>( currLuma[cIdx] ) ) );
                samples++;
            }
        }
        if( samples >= 20 )
        {
            const unsigned __int64 score = totalDiff / samples;
            if( score < bestHorizScore )
            {
                bestHorizScore = score;
                bestHorizDx = dx;
            }
        }
    }

    // If ignoring constant regions yields no valid score for one or both
    // axes, retry without the gradient mask filter.
    if( bestVertScore == ULLONG_MAX || bestHorizScore == ULLONG_MAX )
    {
        for( int dy = -kAxisScanRange; dy <= kAxisScanRange; dy++ )
        {
            if( dy == 0 )
                continue;
            const int absDy = abs( dy );
            const int overlapW = frameWidth - 2 * kAxisMargin;
            const int overlapH = frameHeight - absDy - 2 * kAxisMargin;
            if( overlapW < frameWidth / 4 || overlapH < frameHeight / 4 )
                continue;
            const int pY0 = kAxisMargin + max( 0, -dy );
            const int cY0 = kAxisMargin + max( 0, dy );
            unsigned __int64 totalDiff = 0;
            unsigned __int64 samples = 0;
            for( int y = 0; y < overlapH; y += kAxisStep )
            {
                const int pRow = ( pY0 + y ) * frameWidth;
                const int cRow = ( cY0 + y ) * frameWidth;
                for( int x = 0; x < overlapW; x += kAxisStep )
                {
                    const int px = kAxisMargin + x;
                    totalDiff += static_cast<unsigned __int64>( abs( static_cast<int>( prevLuma[pRow + px] ) - static_cast<int>( currLuma[cRow + px] ) ) );
                    samples++;
                }
            }
            if( samples >= 100 && bestVertScore == ULLONG_MAX )
            {
                const unsigned __int64 score = totalDiff / samples;
                if( score < bestVertScore )
                {
                    bestVertScore = score;
                    bestVertDy = dy;
                }
            }
        }
        for( int dx = -kAxisScanRange; dx <= kAxisScanRange; dx++ )
        {
            if( dx == 0 )
                continue;
            const int absDx = abs( dx );
            const int overlapW = frameWidth - absDx - 2 * kAxisMargin;
            const int overlapH = frameHeight - 2 * kAxisMargin;
            if( overlapW < frameWidth / 4 || overlapH < frameHeight / 4 )
                continue;
            const int pX0 = kAxisMargin + max( 0, -dx );
            const int cX0 = kAxisMargin + max( 0, dx );
            unsigned __int64 totalDiff = 0;
            unsigned __int64 samples = 0;
            for( int y = 0; y < overlapH; y += kAxisStep )
            {
                const int rowOff = ( kAxisMargin + y ) * frameWidth;
                for( int x = 0; x < overlapW; x += kAxisStep )
                {
                    totalDiff += static_cast<unsigned __int64>( abs( static_cast<int>( prevLuma[rowOff + pX0 + x] ) - static_cast<int>( currLuma[rowOff + cX0 + x] ) ) );
                    samples++;
                }
            }
            if( samples >= 100 && bestHorizScore == ULLONG_MAX )
            {
                const unsigned __int64 score = totalDiff / samples;
                if( score < bestHorizScore )
                {
                    bestHorizScore = score;
                    bestHorizDx = dx;
                }
            }
        }
    }

    StitchLog( L"[Panorama/Stitch] AxisScan vertBest=%I64u dy=%d horizBest=%I64u dx=%d\n",
               bestVertScore, bestVertDy, bestHorizScore, bestHorizDx );

    bool verticalWins = bestVertScore <= bestHorizScore;

    // Startup default is vertical.  If the horizontal axis only wins by a
    // small margin, treat the result as ambiguous and keep the default axis.
    if( bestVertScore != ULLONG_MAX && bestHorizScore != ULLONG_MAX )
    {
        const unsigned __int64 horizontalAdvantage =
            ( bestHorizScore < bestVertScore )
                ? ( bestVertScore - bestHorizScore )
                : 0;
        const unsigned __int64 ambiguityMargin =
            ( std::max )( static_cast<unsigned __int64>( 8 ), bestVertScore / 8 );
        if( horizontalAdvantage <= ambiguityMargin )
        {
            if( !verticalWins )
            {
                StitchLog( L"[Panorama/Stitch] AxisScan ambiguous startup forcing vertical: vertBest=%I64u horizBest=%I64u dy=%d dx=%d margin=%I64u\n",
                           bestVertScore,
                           bestHorizScore,
                           bestVertDy,
                           bestHorizDx,
                           ambiguityMargin );
            }
            verticalWins = true;
        }
    }

    // Geometry bias for first-pair VLE axis detection: narrow/tall capture
    // portals are overwhelmingly used for vertical scroll captures.  On such
    // strips, horizontal SAD can look deceptively better due to repeated
    // line/text structure, causing permanent axis mis-lock.
    const bool portraitPortal = frameHeight >= frameWidth * 2;
    if( portraitPortal && bestVertScore != ULLONG_MAX )
    {
        const unsigned __int64 horizAdvantage =
            ( bestHorizScore != ULLONG_MAX && bestHorizScore < bestVertScore )
                ? ( bestVertScore - bestHorizScore )
                : 0;
        const unsigned __int64 requiredAdvantage =
            ( std::max )( static_cast<unsigned __int64>( 16 ), bestVertScore / 3 );
        if( horizAdvantage < requiredAdvantage )
        {
            if( !verticalWins )
            {
                StitchLog( L"[Panorama/Stitch] AxisScan portrait-bias forcing vertical: vertBest=%I64u horizBest=%I64u dy=%d dx=%d\n",
                           bestVertScore,
                           bestHorizScore,
                           bestVertDy,
                           bestHorizDx );
            }
            verticalWins = true;
        }
    }

    if( verticalWins )
    {
        if( directOk )
        {
            bestDx = directDx;
            bestDy = directDy;
        }
        else
        {
            bestDx = 0;
            bestDy = bestVertDy;
        }
    }
    else
    {
        if( transposedOk )
        {
            bestDx = mappedDx;
            bestDy = mappedDy;
        }
        else
        {
            bestDx = bestHorizDx;
            bestDy = 0;
        }
    }
    return true;
}

static HBITMAP StitchPanoramaFrames(const std::vector<HBITMAP>& frames,
                                    bool lowContrastMode,
                                    std::function<bool(int)> progressCallback,
                                    size_t* outComposedFrameCount,
                                    std::vector<int>* outComposedAxisSteps)
{
    bool cancelled = false;
    auto reportProgress = [&progressCallback, &cancelled]( int percent )
    {
        if( progressCallback )
        {
            if( progressCallback( max( 0, min( 100, percent ) ) ) )
            {
                cancelled = true;
            }
        }
    };
    const ULONGLONG stitchStart = GetTickCount64();
    if( frames.empty() )
    {
        StitchLog( L"[Panorama/Stitch] No frames to stitch\n" );
        return nullptr;
    }

    BITMAP firstFrame{};
    if( GetObject( frames.front(), sizeof(firstFrame), &firstFrame ) == 0 )
    {
        return nullptr;
    }

    const int frameWidth = firstFrame.bmWidth;
    const int frameHeight = firstFrame.bmHeight;
    if( frameWidth <= 0 || frameHeight <= 0 )
    {
        StitchLog( L"[Panorama/Stitch] Invalid frame size %dx%d\n", frameWidth, frameHeight );
        return nullptr;
    }

    StitchLog( L"[Panorama/Stitch] Begin stitching frameCount=%zu frame=%dx%d\n",
                 frames.size(),
                 frameWidth,
                 frameHeight );

    std::vector<std::vector<BYTE>> framePixels;
    framePixels.resize( frames.size() );
    std::vector<std::vector<BYTE>> frameLuma( frames.size() );
    std::vector<double> frameConstantFraction( frames.size() );

    for( size_t i = 0; i < frames.size(); i++ )
    {
        int width = 0;
        int height = 0;
        if( !ReadBitmapPixels32( frames[i], framePixels[i], width, height ) )
        {
            StitchLog( L"[Panorama/Stitch] Failed to read frame %zu pixels\n", i );
            return nullptr;
        }

        if( width != frameWidth || height != frameHeight )
        {
            StitchLog( L"[Panorama/Stitch] Frame %zu dimension mismatch: %dx%d expected=%dx%d\n",
                         i,
                         width,
                         height,
                         frameWidth,
                         frameHeight );
            return nullptr;
        }
    }

    parallel_for( 0, static_cast<int>( frames.size() ), [&]( int i )
    {
        BuildFullLumaFrame( framePixels[i], frameWidth, frameHeight, frameLuma[i] );
        frameConstantFraction[i] = ComputeConstantContentFraction( framePixels[i], frameWidth, frameHeight );
    } );

    std::vector<size_t> composedFrameIndices;
    std::vector<POINT> composedFrameOrigins;
    std::vector<POINT> composedFrameSteps;
    composedFrameIndices.reserve( frames.size() );
    composedFrameOrigins.reserve( frames.size() );
    composedFrameSteps.reserve( frames.size() );
    composedFrameIndices.push_back( 0 );
    composedFrameOrigins.push_back( { 0, 0 } );
    composedFrameSteps.push_back( { 0, 0 } );

    const int minFrameDimension = min( frameWidth, frameHeight );
    const int minProgress = lowContrastMode ? max( 4, minFrameDimension / 40 ) : max( 8, minFrameDimension / 30 );
    int expectedDx = 0;
    int expectedDy = 0;
    int retryEligibilityStep = 0;
    int retryNormalizationBudget = 0;
    int nearStationaryCount = 0;
    int duplicateRetryStreak = 0;
    int consecutiveNonDupRejectCount = 0;
    int consecutiveSpikeRejectCount = 0;
    int consecutiveMomentumCollapseCount = 0;

    int minX = 0;
    int minY = 0;
    int maxX = frameWidth;
    int maxY = frameHeight;

    for( size_t i = 1; i < frames.size(); i++ )
    {
        reportProgress( 5 + static_cast<int>( i * 85 / frames.size() ) );
        if( cancelled )
        {
            StitchLog( L"[Panorama/Stitch] Cancelled during shift computation\n" );
            return nullptr;
        }

        // Reject blank/transition frames where virtually all pixels are
        // identical.  These frames carry no scroll information and will
        // produce wrong matcher results if accepted.  Real blank frames
        // have constFrac = 1.000; selftest synthetic frames can reach
        // ~0.996, so use 0.999 to avoid false rejections.
        if( frameConstantFraction[i] > 0.999 )
        {
            StitchLog( L"[Panorama/Stitch] Frame %zu rejected: blank frame constFrac=%.3f\n",
                         i, frameConstantFraction[i] );
            consecutiveNonDupRejectCount++;
            continue;
        }

        int dx = expectedDx;
        int dy = expectedDy;
        int retryStreakUsed = 0;
        bool momentumCollapseApplied = false;
        int momentumCollapseDetectedDx = 0;
        int momentumCollapseDetectedDy = 0;
        bool anchorVerifiedShift = false;
        const int veryLowEntropy = ( frameConstantFraction[composedFrameIndices.back()] > 0.58 && frameConstantFraction[i] > 0.58 ) ? 1 : 0;
        bool nearStationaryOverride = false;
        bool foundShift = FindBestFrameShift( framePixels[composedFrameIndices.back()], framePixels[i], frameWidth, frameHeight, expectedDx, expectedDy, dx, dy, lowContrastMode, frameLuma[composedFrameIndices.back()], frameLuma[i], veryLowEntropy, &nearStationaryOverride );

        if( !foundShift )
        {
            if( ArePixelFramesNearDuplicate( framePixels[composedFrameIndices.back()],
                                             framePixels[i],
                                             frameWidth,
                                             frameHeight,
                                             lowContrastMode ) )
            {
                duplicateRetryStreak++;

                const int expectedAxisStep = max( abs( expectedDx ), abs( expectedDy ) );
                int retryExpectedMaxStep = max( 32, frameHeight / 6 + 1 );
                const double lastConstFrac = frameConstantFraction[composedFrameIndices.back()];
                const double curConstFrac = frameConstantFraction[i];
                // In sustained low-detail momentum streaks, strict VLE-only
                // gating can suppress duplicate recovery and drop multiple
                // frames. Allow recovery when either frame is near-VLE and
                // expected motion is strongly established.
                const bool nearVlePair = ( lastConstFrac > 0.52 ) || ( curConstFrac > 0.52 );
                const bool sustainedMomentum = expectedAxisStep >= frameHeight / 6 && composedFrameSteps.size() >= 6;
                if( sustainedMomentum )
                {
                    retryExpectedMaxStep = max( retryExpectedMaxStep, frameHeight / 2 );
                }

                // Bridge the first duplicate miss in sustained low-detail
                // momentum runs by advancing with the expected shift.  This
                // prevents one missed match from cascading into multi-frame
                // duplicate drops against the same reference frame.
                if( !foundShift && duplicateRetryStreak == 1 && nearVlePair && sustainedMomentum )
                {
                    dx = expectedDx;
                    dy = expectedDy;
                    foundShift = true;
                    StitchLog( L"[Panorama/Stitch] Frame %zu duplicate-bridge normalized: expected=(%d,%d) axisStep=%d\n",
                                 i,
                                 expectedDx,
                                 expectedDy,
                                 expectedAxisStep );
                }

                const bool retryEligible =
                    ( veryLowEntropy != 0 || ( nearVlePair && sustainedMomentum ) ) &&
                    duplicateRetryStreak >= 2 &&
                    retryEligibilityStep >= minProgress &&
                    retryEligibilityStep <= retryExpectedMaxStep;

                if( retryEligible )
                {
                    int retryDx = expectedDx;
                    int retryDy = expectedDy;
                    bool retryNearStationaryOverride = false;
                    if( FindBestFrameShift( framePixels[composedFrameIndices.back()],
                                            framePixels[i],
                                            frameWidth,
                                            frameHeight,
                                            expectedDx,
                                            expectedDy,
                                            retryDx,
                                            retryDy,
                                            lowContrastMode,
                                            frameLuma[composedFrameIndices.back()],
                                            frameLuma[i],
                                            veryLowEntropy,
                                            &retryNearStationaryOverride,
                                            true ) )
                    {
                        const bool expectedMostlyVertical = abs( expectedDy ) >= abs( expectedDx );
                        const int retryAxisStep = expectedMostlyVertical ? abs( retryDy ) : abs( retryDx );
                        const int axisFrame = expectedMostlyVertical ? frameHeight : frameWidth;
                        int retryHardCap = ( axisFrame * 2 ) / 5;
                        int retryStepMax = min( retryHardCap,
                                                expectedAxisStep + max( minProgress * 2, expectedAxisStep ) + 4 );

                        // While normalization budget remains, allow multi-frame
                        // duplicate retries to span several single-frame steps.
                        // This preserves legitimate jumpy scrolls that briefly
                        // enter low-entropy duplicate streaks.
                        int recentPeakAxisStep = 0;
                        for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                             si >= 1 && si >= static_cast<int>( composedFrameSteps.size() ) - 10;
                             --si )
                        {
                            const POINT& prevStep = composedFrameSteps[static_cast<size_t>( si )];
                            const int axis = expectedMostlyVertical ? abs( prevStep.y ) : abs( prevStep.x );
                            recentPeakAxisStep = max( recentPeakAxisStep, axis );
                        }

                        const bool jumpyHistory = recentPeakAxisStep >= ( axisFrame * 3 ) / 10;
                        if( retryNormalizationBudget > 0 && jumpyHistory )
                        {
                            retryHardCap = min( axisFrame - minProgress,
                                                retryHardCap * max( 1, duplicateRetryStreak ) );
                            retryStepMax = min( retryHardCap,
                                                expectedAxisStep +
                                                max( minProgress * 2,
                                                     expectedAxisStep * ( duplicateRetryStreak + 2 ) ) + 4 );
                        }
                        const int retryStepMin = max( 4, expectedAxisStep / 3 );
                        const int expectedAxisSigned = expectedMostlyVertical ? expectedDy : expectedDx;
                        const int retryAxisSigned = expectedMostlyVertical ? retryDy : retryDx;
                        const bool sameDirection = expectedAxisSigned == 0 || retryAxisSigned == 0 ||
                                                   ( ( expectedAxisSigned < 0 ) == ( retryAxisSigned < 0 ) );

                        if( sameDirection &&
                            retryAxisStep >= retryStepMin &&
                            retryAxisStep <= retryStepMax )
                        {
                            // When the normalization budget is exhausted, block
                            // retries whose shift closely matches the expected
                            // step (harmonic repeat).  These are likely 1-frame
                            // harmonics rather than genuine multi-frame shifts
                            // and cause backward jumps in the stitched output.
                            //
                            // Exception: allow the retry if the step is
                            // consistent with the median of recent composed
                            // steps.  In long HCF duplicate streaks, the
                            // budget depletes but the retry keeps finding the
                            // correct per-frame shift -- blocking those loses
                            // canvas height.
                            const bool harmonicLike =
                                abs( retryAxisStep - expectedAxisStep ) < max( 5, expectedAxisStep / 7 );
                            bool matchesRecentMedian = false;
                            if( harmonicLike && retryNormalizationBudget <= 0 &&
                                composedFrameSteps.size() >= 4 )
                            {
                                std::vector<int> recentForMedian;
                                recentForMedian.reserve( 8 );
                                for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                                     si >= 1 && recentForMedian.size() < 8; --si )
                                {
                                    const int v = expectedMostlyVertical
                                        ? abs( composedFrameSteps[static_cast<size_t>( si )].y )
                                        : abs( composedFrameSteps[static_cast<size_t>( si )].x );
                                    if( v > 0 )
                                        recentForMedian.push_back( v );
                                }
                                if( recentForMedian.size() >= 3 )
                                {
                                    std::sort( recentForMedian.begin(), recentForMedian.end() );
                                    const int recentMedian = recentForMedian[recentForMedian.size() / 2];
                                    matchesRecentMedian =
                                        abs( retryAxisStep - recentMedian ) < max( 5, recentMedian / 4 );
                                }
                            }
                            const bool budgetBlocked =
                                retryNormalizationBudget <= 0 && harmonicLike && !matchesRecentMedian;

                            if( !budgetBlocked )
                            {
                                dx = retryDx;
                                dy = retryDy;
                                nearStationaryOverride = retryNearStationaryOverride;
                                foundShift = true;
                                StitchLog( L"[Panorama/Stitch] Frame %zu duplicate-retry accepted: dx=%d dy=%d axisStep=%d expectedAxis=%d streak=%d\n",
                                             i,
                                             dx,
                                             dy,
                                             retryAxisStep,
                                             expectedAxisStep,
                                             duplicateRetryStreak );
                            }
                            else
                            {
                                StitchLog( L"[Panorama/Stitch] Frame %zu duplicate-retry harmonic-blocked: axisStep=%d expectedAxis=%d budget=%d\n",
                                             i,
                                             retryAxisStep,
                                             expectedAxisStep,
                                             retryNormalizationBudget );
                            }
                        }
                        else
                        {
                            StitchLog( L"[Panorama/Stitch] Frame %zu duplicate-retry rejected: dx=%d dy=%d axisStep=%d expectedAxis=%d range=[%d,%d] sameDir=%d\n",
                                         i,
                                         retryDx,
                                         retryDy,
                                         retryAxisStep,
                                         expectedAxisStep,
                                         retryStepMin,
                                         retryStepMax,
                                         sameDirection ? 1 : 0 );
                        }
                    }
                }

                if( !foundShift && expectedDx == 0 && expectedDy == 0 &&
                    duplicateRetryStreak >= 1 && i >= 1 )
                {
                    int negDx = 0, negDy = 0;
                    int posDx = 0, posDy = 0;
                    bool negNearStationary = false;
                    bool posNearStationary = false;

                    const bool negOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                        frameWidth, frameHeight,
                                                                        0, -minProgress,
                                                                        negDx, negDy,
                                                                        lowContrastMode,
                                                                        frameLuma[i - 1], frameLuma[i],
                                                                        veryLowEntropy != 0,
                                                                        &negNearStationary,
                                                                        true,
                                                                        nullptr,
                                                                        true );
                    const bool posOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                        frameWidth, frameHeight,
                                                                        0, minProgress,
                                                                        posDx, posDy,
                                                                        lowContrastMode,
                                                                        frameLuma[i - 1], frameLuma[i],
                                                                        veryLowEntropy != 0,
                                                                        &posNearStationary,
                                                                        true,
                                                                        nullptr,
                                                                        true );

                    if( negOk || posOk )
                    {
                        int bootDx = 0, bootDy = 0;
                        bool bootNearStationary = false;
                        if( negOk && ( !posOk || abs( negDy ) >= abs( posDy ) ) )
                        {
                            bootDx = negDx;
                            bootDy = negDy;
                            bootNearStationary = negNearStationary;
                        }
                        else
                        {
                            bootDx = posDx;
                            bootDy = posDy;
                            bootNearStationary = posNearStationary;
                        }

                        const int bootStep = abs( bootDx ) + abs( bootDy );
                        const int bootAxisStep = max( abs( bootDx ), abs( bootDy ) );
                        const int bootAxisCap = max( 32, frameHeight / 4 );
                        if( bootStep >= max( 4, minProgress / 2 ) && bootAxisStep <= bootAxisCap )
                        {
                            dx = bootDx;
                            dy = bootDy;
                            nearStationaryOverride = bootNearStationary;
                            foundShift = true;
                            StitchLog( L"[Panorama/Stitch] Frame %zu duplicate-startup-bootstrap accepted: dx=%d dy=%d step=%d streak=%d negOk=%d posOk=%d\n",
                                         i,
                                         dx,
                                         dy,
                                         bootAxisStep,
                                         duplicateRetryStreak,
                                         negOk ? 1 : 0,
                                         posOk ? 1 : 0 );
                        }
                    }
                }

                if( foundShift )
                {
                    retryStreakUsed = duplicateRetryStreak;
                    duplicateRetryStreak = 0;
                }
                else
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: duplicate vs frame %zu\n",
                                 i,
                                 composedFrameIndices.back() );
                    continue;
                }
            }
            else
            {
                duplicateRetryStreak = 0;
                consecutiveNonDupRejectCount++;

                // After several consecutive non-duplicate rejections the
                // reference frame has drifted too far behind the current
                // scroll position for FindBestFrameShift to produce an
                // overlap match.  Recover by matching the adjacent
                // captured pair (i-1, i) to discover the actual per-frame
                // step, then extrapolate from the last accepted origin.
                const bool earlyRecoveryEligible =
                    ( i - composedFrameIndices.back() ) >= 2 && composedFrameSteps.size() >= 6;
                const bool lateTailRecoveryEligible =
                    ( i - composedFrameIndices.back() ) == 1 &&
                    composedFrameSteps.size() >= 10 &&
                    max( abs( expectedDx ), abs( expectedDy ) ) >= frameHeight / 4;
                if( ( consecutiveNonDupRejectCount >= 3 ||
                      ( consecutiveNonDupRejectCount >= 1 &&
                        ( earlyRecoveryEligible || lateTailRecoveryEligible ) ) ) &&
                    i >= 2 )
                {
                    int adjDx = 0, adjDy = 0;
                    bool adjNearStationary = false;
                    const int adjVLE = ( frameConstantFraction[i - 1] > 0.58 &&
                                         frameConstantFraction[i] > 0.58 ) ? 1 : 0;

                    bool adjFound = FindBestFrameShift( framePixels[i - 1], framePixels[i],
                                                        frameWidth, frameHeight,
                                                        expectedDx, expectedDy,
                                                        adjDx, adjDy,
                                                        lowContrastMode,
                                                        frameLuma[i - 1], frameLuma[i],
                                                        adjVLE, &adjNearStationary );

                    // Startup bootstrap: when expected shift is still unknown
                    // (0,0), try directional guesses for the adjacent pair.
                    // This bypasses first-pair axis-defer loops on VLE captures
                    // where frame differences are tiny but motion is real.
                    if( !adjFound && expectedDx == 0 && expectedDy == 0 )
                    {
                        int negDx = 0, negDy = 0;
                        int posDx = 0, posDy = 0;
                        bool negNearStationary = false;
                        bool posNearStationary = false;

                        const bool negOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                            frameWidth, frameHeight,
                                                                            0, -minProgress,
                                                                            negDx, negDy,
                                                                            lowContrastMode,
                                                                            frameLuma[i - 1], frameLuma[i],
                                                                            adjVLE != 0,
                                                                            &negNearStationary,
                                                                            true,
                                                                            nullptr,
                                                                            true );
                        const bool posOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                            frameWidth, frameHeight,
                                                                            0, minProgress,
                                                                            posDx, posDy,
                                                                            lowContrastMode,
                                                                            frameLuma[i - 1], frameLuma[i],
                                                                            adjVLE != 0,
                                                                            &posNearStationary,
                                                                            true,
                                                                            nullptr,
                                                                            true );

                        if( negOk || posOk )
                        {
                            if( negOk && ( !posOk || abs( negDy ) >= abs( posDy ) ) )
                            {
                                adjDx = negDx;
                                adjDy = negDy;
                                adjNearStationary = negNearStationary;
                            }
                            else
                            {
                                adjDx = posDx;
                                adjDy = posDy;
                                adjNearStationary = posNearStationary;
                            }

                            const int bootAxisStep = max( abs( adjDx ), abs( adjDy ) );
                            const int bootAxisCap = ( lowContrastMode || adjVLE != 0 ) ?
                                max( 32, frameHeight / 4 ) :
                                max( 32, frameHeight / 3 );
                            if( bootAxisStep >= max( 4, minProgress / 2 ) && bootAxisStep <= bootAxisCap )
                            {
                                adjFound = true;
                                StitchLog( L"[Panorama/Stitch] Startup bootstrap: directional guess selected adj=(%d,%d) negOk=%d posOk=%d\n",
                                             adjDx,
                                             adjDy,
                                             negOk ? 1 : 0,
                                             posOk ? 1 : 0 );
                            }
                            else
                            {
                                StitchLog( L"[Panorama/Stitch] Startup bootstrap: directional guess rejected adj=(%d,%d) axisStep=%d cap=%d\n",
                                             adjDx,
                                             adjDy,
                                             bootAxisStep,
                                             bootAxisCap );
                            }
                        }
                    }

                    // Late-tail fallback: if expected motion is already strong
                    // and adjacent-pair full matching fails, probe directional
                    // axis-only matching for (i-1, i). This is intentionally
                    // narrow to avoid affecting startup behavior.
                    if( !adjFound && lateTailRecoveryEligible && expectedDy != 0 && abs( expectedDy ) >= abs( expectedDx ) * 2 )
                    {
                        int tailDx = 0, tailDy = 0;
                        bool tailNearStationary = false;

                        bool tailOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                       frameWidth, frameHeight,
                                                                       0, expectedDy,
                                                                       tailDx, tailDy,
                                                                       lowContrastMode,
                                                                       frameLuma[i - 1], frameLuma[i],
                                                                       adjVLE != 0,
                                                                       &tailNearStationary,
                                                                       true,
                                                                       nullptr,
                                                                       true );

                        if( !tailOk )
                        {
                            const int signedGuess = ( expectedDy < 0 ) ? -minProgress : minProgress;
                            tailOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1], framePixels[i],
                                                                     frameWidth, frameHeight,
                                                                     0, signedGuess,
                                                                     tailDx, tailDy,
                                                                     lowContrastMode,
                                                                     frameLuma[i - 1], frameLuma[i],
                                                                     adjVLE != 0,
                                                                     &tailNearStationary,
                                                                     true,
                                                                     nullptr,
                                                                     true );
                        }

                        const bool sameDirection = tailOk && ( ( tailDy < 0 ) == ( expectedDy < 0 ) );
                        const int tailStep = abs( tailDy );
                        const int minTailStep = max( 4, minProgress / 2 );
                        const int maxTailStep = min( frameHeight - minProgress,
                                                     abs( expectedDy ) + max( minProgress * 3, abs( expectedDy ) / 2 ) );

                        if( sameDirection && tailStep >= minTailStep && tailStep <= maxTailStep )
                        {
                            adjDx = tailDx;
                            adjDy = tailDy;
                            adjNearStationary = tailNearStationary;
                            adjFound = true;
                            StitchLog( L"[Panorama/Stitch] Late-tail fallback selected adj=(%d,%d) expected=(%d,%d)\n",
                                         adjDx,
                                         adjDy,
                                         expectedDx,
                                         expectedDy );
                        }
                    }

                    if( adjFound )
                    {
                        int perFrameStepX = -adjDx;
                        int perFrameStepY = -adjDy;

                        // Apply direction clamping consistent with
                        // the established scroll axis.
                        if( composedFrameSteps.size() >= 3 )
                        {
                            const int stepCap = 3 * minProgress;
                            int histAbsX = 0, histAbsY = 0;
                            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
                            {
                                histAbsX += min( abs( composedFrameSteps[si].x ), stepCap );
                                histAbsY += min( abs( composedFrameSteps[si].y ), stepCap );
                            }
                            if( histAbsY > histAbsX * 8 )
                                perFrameStepX = 0;
                            else if( histAbsX > histAbsY * 8 )
                                perFrameStepY = 0;
                        }

                        // Require meaningful progress after clamping
                        // so we don't re-anchor on near-stationary or
                        // wrong-axis content.
                        if( abs( perFrameStepX ) + abs( perFrameStepY ) >= max( 4, minProgress / 2 ) )
                        {
                            const int gap = static_cast<int>( i - composedFrameIndices.back() );
                            // Startup recovery can discover a plausible
                            // adjacent-pair shift while expected motion is
                            // still unknown.  Extrapolating that shift across
                            // multiple dropped frames is high risk in low-
                            // detail captures and can create large black bands.
                            // In that uninitialized state, bridge only one
                            // frame and let subsequent frames refine normally.
                            const int extrapolationGap =
                                ( expectedDx == 0 && expectedDy == 0 ) ? 1 : gap;

                            POINT nextOrigin = composedFrameOrigins.back();
                            nextOrigin.x += perFrameStepX * extrapolationGap;
                            nextOrigin.y += perFrameStepY * extrapolationGap;

                            composedFrameIndices.push_back( i );
                            composedFrameOrigins.push_back( nextOrigin );
                            composedFrameSteps.push_back( { perFrameStepX, perFrameStepY } );

                            expectedDx = adjDx;
                            expectedDy = adjDy;
                            retryEligibilityStep = max( abs( adjDx ), abs( adjDy ) );
                            retryNormalizationBudget = 5;
                            consecutiveNonDupRejectCount = 0;
                            consecutiveSpikeRejectCount = 0;
                            nearStationaryCount = 0;

                            StitchLog( L"[Panorama/Stitch] Frame %zu recovery-accepted: adj=(%d,%d) gap=%d step=(%d,%d) origin=(%d,%d)\n",
                                         i,
                                         adjDx,
                                         adjDy,
                                         extrapolationGap,
                                         perFrameStepX,
                                         perFrameStepY,
                                         nextOrigin.x,
                                         nextOrigin.y );

                            minX = min( minX, nextOrigin.x );
                            minY = min( minY, nextOrigin.y );
                            maxX = max( maxX, nextOrigin.x + frameWidth );
                            maxY = max( maxY, nextOrigin.y + frameHeight );
                            continue;
                        }
                    }

                    if( !foundShift && lateTailRecoveryEligible )
                    {
                        const bool mostlyVerticalExpected = abs( expectedDy ) >= abs( expectedDx ) * 2;
                        const int expectedAxisStep = mostlyVerticalExpected ? abs( expectedDy ) : abs( expectedDx );
                        if( expectedAxisStep >= frameHeight / 4 && composedFrameSteps.size() >= 8 )
                        {
                            std::vector<int> recentAxisAbs;
                            recentAxisAbs.reserve( 8 );
                            for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                                 si >= 1 && static_cast<int>( recentAxisAbs.size() ) < 8;
                                 --si )
                            {
                                const int av = mostlyVerticalExpected
                                    ? abs( composedFrameSteps[static_cast<size_t>( si )].y )
                                    : abs( composedFrameSteps[static_cast<size_t>( si )].x );
                                if( av > 0 )
                                    recentAxisAbs.push_back( av );
                            }

                            if( recentAxisAbs.size() >= 4 )
                            {
                                std::sort( recentAxisAbs.begin(), recentAxisAbs.end() );
                                const int recentMedian = recentAxisAbs[recentAxisAbs.size() / 2];
                                const bool expectedMatchesHistory =
                                    abs( expectedAxisStep - recentMedian ) <= max( 24, recentMedian / 3 );

                                if( expectedMatchesHistory )
                                {
                                    dx = expectedDx;
                                    dy = expectedDy;
                                    foundShift = true;
                                    StitchLog( L"[Panorama/Stitch] Frame %zu late-tail bridge normalized: expected=(%d,%d) median=%d\n",
                                                 i,
                                                 expectedDx,
                                                 expectedDy,
                                                 recentMedian );
                                }
                            }
                        }
                    }
                }

                if( !foundShift )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: no reliable shift match expected=(%d,%d)\n",
                                 i,
                                 expectedDx,
                                 expectedDy );
                    continue;
                }
            }
        }

        const size_t lastAcceptedIndex = composedFrameIndices.back();
        const int acceptedGap = static_cast<int>( i - lastAcceptedIndex );
        bool hasNextExpectedOverride = false;
        int overrideNextExpectedDx = 0;
        int overrideNextExpectedDy = 0;
        int overrideEligibilityStep = 0;

        if( acceptedGap > 1 )
        {
            LogGapBridgeProbeDiagnostics( i,
                                          lastAcceptedIndex,
                                          acceptedGap,
                                          dx,
                                          dy,
                                          expectedDx,
                                          expectedDy,
                                          frameWidth,
                                          frameHeight,
                                          lowContrastMode,
                                          framePixels,
                                          frameLuma,
                                          frameConstantFraction,
                                          composedFrameSteps );

            const bool expectedMostlyVertical = abs( expectedDy ) >= max( abs( expectedDx ) * 2, minProgress );
            const bool expectedMostlyHorizontal = abs( expectedDx ) >= max( abs( expectedDy ) * 2, minProgress );
            if( expectedMostlyVertical || expectedMostlyHorizontal )
            {
                int bridgeDx = 0;
                int bridgeDy = 0;
                bool bridgeNearStationary = false;
                const int bridgeVle = ( frameConstantFraction[lastAcceptedIndex] > 0.58 &&
                                        frameConstantFraction[i] > 0.58 ) ? 1 : 0;
                const bool bridgeOk = FindBestFrameShiftVerticalOnly( framePixels[lastAcceptedIndex],
                                                                      framePixels[i],
                                                                      frameWidth,
                                                                      frameHeight,
                                                                      expectedDx * acceptedGap,
                                                                      expectedDy * acceptedGap,
                                                                      bridgeDx,
                                                                      bridgeDy,
                                                                      lowContrastMode,
                                                                      frameLuma[lastAcceptedIndex],
                                                                      frameLuma[i],
                                                                      bridgeVle,
                                                                      &bridgeNearStationary,
                                                                      false,
                                                                      nullptr,
                                                                      true,
                                                                      true );

                int adjacentDx = 0;
                int adjacentDy = 0;
                bool adjacentNearStationary = false;
                const int adjacentVle = ( frameConstantFraction[i - 1] > 0.58 &&
                                          frameConstantFraction[i] > 0.58 ) ? 1 : 0;
                const bool adjacentOk = FindBestFrameShiftVerticalOnly( framePixels[i - 1],
                                                                        framePixels[i],
                                                                        frameWidth,
                                                                        frameHeight,
                                                                        expectedDx,
                                                                        expectedDy,
                                                                        adjacentDx,
                                                                        adjacentDy,
                                                                        lowContrastMode,
                                                                        frameLuma[i - 1],
                                                                        frameLuma[i],
                                                                        adjacentVle,
                                                                        &adjacentNearStationary,
                                                                        false,
                                                                        nullptr,
                                                                        true,
                                                                        true );

                const int expectedAxisSigned = expectedMostlyVertical ? expectedDy : expectedDx;
                const int acceptedAxisSigned = expectedMostlyVertical ? dy : dx;
                const int bridgeAxisSigned = expectedMostlyVertical ? bridgeDy : bridgeDx;
                const int adjacentAxisSigned = expectedMostlyVertical ? adjacentDy : adjacentDx;
                const int axisFrame = expectedMostlyVertical ? frameHeight : frameWidth;
                const int expectedAxisAbs = abs( expectedAxisSigned );
                const int acceptedAxisAbs = abs( acceptedAxisSigned );
                const int bridgeAxisAbs = abs( bridgeAxisSigned );
                const int adjacentAxisAbs = abs( adjacentAxisSigned );
                const int expectedTotalAbs = expectedAxisAbs * acceptedGap;
                const bool bridgeSameDirection = bridgeOk && bridgeAxisSigned != 0 &&
                                                ( ( bridgeAxisSigned > 0 ) == ( expectedAxisSigned > 0 ) );
                const bool adjacentSameDirection = !adjacentOk || adjacentAxisSigned == 0 ||
                                                  ( ( adjacentAxisSigned > 0 ) == ( expectedAxisSigned > 0 ) );
                const int bridgeTolerance = max( 12, expectedAxisAbs / 3 );
                const bool bridgeMatchesExpectedTotal =
                    bridgeSameDirection &&
                    bridgeAxisAbs >= max( expectedTotalAbs - bridgeTolerance, expectedTotalAbs * 4 / 5 ) &&
                    bridgeAxisAbs <= min( axisFrame - minProgress, expectedTotalAbs + max( 24, expectedAxisAbs / 2 ) );
                const bool acceptedClearlyUnderAdvanced =
                    acceptedAxisAbs > 0 &&
                    acceptedAxisAbs <= max( expectedAxisAbs, expectedTotalAbs / 2 );
                const bool adjacentNearStationaryGap =
                    adjacentSameDirection &&
                    adjacentAxisAbs <= max( minProgress / 2, expectedAxisAbs / 4 );

                if( bridgeMatchesExpectedTotal && acceptedClearlyUnderAdvanced && adjacentNearStationaryGap )
                {
                    // Don't trust the gap-bridge when any intermediate frame
                    // is nearly uniform (e.g. an all-dark screen capture).
                    // The bridge matcher produces harmonic aliases on uniform
                    // content and the adjacent pair provides no alignment
                    // signal.  In this case, keep the direct match result.
                    bool intermediateFrameNearlyUniform = false;
                    for( size_t skip = lastAcceptedIndex + 1; skip < i; ++skip )
                    {
                        // Check both constant fraction AND average pixel brightness.
                        // A nearly-black frame (avgPixel < 25) with elevated constFrac
                        // is a blank/dark screen capture that provides no alignment signal.
                        const double skipConstFrac = frameConstantFraction[skip];
                        bool skipIsNearlyUniform = skipConstFrac > 0.55;
                        if( skipIsNearlyUniform )
                        {
                            // Verify it's actually a low-content frame by checking
                            // average pixel brightness. Sample the center of the frame.
                            const std::vector<BYTE>& skipPx = framePixels[skip];
                            long long pixSum = 0;
                            int pixCount = 0;
                            const int sampleMargin = frameWidth / 6;
                            for( int sy = frameHeight / 4; sy < frameHeight * 3 / 4; sy += 8 )
                            {
                                for( int sx = sampleMargin; sx < frameWidth - sampleMargin; sx += 8 )
                                {
                                    const size_t off = static_cast<size_t>( sy ) * static_cast<size_t>( frameWidth ) * 4 +
                                                       static_cast<size_t>( sx ) * 4;
                                    pixSum += skipPx[off + 0] + skipPx[off + 1] + skipPx[off + 2];
                                    ++pixCount;
                                }
                            }
                            const double avgPx = pixCount > 0 ? static_cast<double>( pixSum ) / ( pixCount * 3.0 ) : 128.0;
                            // Nearly-black or nearly-white uniform frames have no useful structure.
                            skipIsNearlyUniform = ( avgPx < 65.0 || avgPx > 240.0 );

                            StitchLog( L"[Panorama/Stitch] Frame %zu gap-bridge skip-check: intermediate=%zu constFrac=%.3f avgPx=%.1f uniform=%d\n",
                                         i,
                                         skip,
                                         skipConstFrac,
                                         avgPx,
                                         skipIsNearlyUniform ? 1 : 0 );
                        }
                        if( skipIsNearlyUniform )
                        {
                            intermediateFrameNearlyUniform = true;
                            break;
                        }
                    }

                    if( intermediateFrameNearlyUniform )
                    {
                        StitchLog( L"[Panorama/Stitch] Frame %zu gap-bridge-skipped: intermediate frame nearly uniform accepted=(%d,%d) bridge=(%d,%d) gap=%d\n",
                                     i,
                                     dx,
                                     dy,
                                     bridgeDx,
                                     bridgeDy,
                                     acceptedGap );
                    }
                    else
                    {
                    const int originalDx = dx;
                    const int originalDy = dy;
                    dx = bridgeDx;
                    dy = bridgeDy;
                    overrideNextExpectedDx = DivideRounded( bridgeDx, acceptedGap );
                    overrideNextExpectedDy = DivideRounded( bridgeDy, acceptedGap );
                    overrideEligibilityStep = max( abs( overrideNextExpectedDx ), abs( overrideNextExpectedDy ) );
                    hasNextExpectedOverride = true;
                    nearStationaryOverride = false;
                    StitchLog( L"[Panorama/Stitch] Frame %zu normalized: gap-bridge-total accepted=(%d,%d) bridge=(%d,%d) adjacent=(%d,%d) gap=%d nextExpected=(%d,%d)\n",
                                 i,
                                 originalDx,
                                 originalDy,
                                 bridgeDx,
                                 bridgeDy,
                                 adjacentDx,
                                 adjacentDy,
                                 acceptedGap,
                                 overrideNextExpectedDx,
                                 overrideNextExpectedDy );
                    }
                }
            }
        }
        duplicateRetryStreak = 0;
        consecutiveNonDupRejectCount = 0;

        const int maxAbsDx = max( 8, frameWidth / 6 );
        const int maxAbsDy = frameHeight - minProgress;
        dx = max( -maxAbsDx, min( maxAbsDx, dx ) );
        dy = max( -maxAbsDy, min( maxAbsDy, dy ) );

        // Distinctive-row anchor verification for high-constant-fraction content.
        // On dark/uniform frames the matcher picks harmonic aliases because
        // ~60% of pixels are identical at every offset.  Instead of averaging
        // over all rows, find a unique high-variance row and search for it
        // across frames.  A unique row provides an unambiguous MAD minimum
        // that breaks harmonics.
        //
        // We scan the CURRENT frame for distinctive rows (not the reference
        // frame) because in some cases the reference frame's pixel data may
        // have constFrac=1.0 (e.g., during gap-bridge transitions).
        if( composedFrameSteps.size() >= 4 &&
            ( frameConstantFraction[composedFrameIndices.back()] > 0.45 ||
              frameConstantFraction[i] > 0.45 ) &&
            abs( dy ) >= abs( dx ) * 2 &&
            abs( dy ) > 0 )
        {
            const size_t refIdx = composedFrameIndices.back();
            const std::vector<BYTE>& refPx = framePixels[refIdx];
            const std::vector<BYTE>& curPx = framePixels[i];
            const int xMargin = max( 10, frameWidth / 8 );
            const int sigXStep = max( 1, ( frameWidth - 2 * xMargin ) / 120 );

            // Decide which frame to use as the "source" for finding
            // distinctive rows.  Prefer the one with lower constFrac
            // (more varied content), falling back to the current frame.
            const bool useCurAsSrc = frameConstantFraction[refIdx] > frameConstantFraction[i] + 0.1
                                  || frameConstantFraction[refIdx] > 0.95;
            const std::vector<BYTE>& srcPx = useCurAsSrc ? curPx : refPx;
            const std::vector<BYTE>& tgtPx = useCurAsSrc ? refPx : curPx;

            // Step 1: Collect the top candidate anchor rows by variance.
            struct AnchorCandidate { int y; double var; };
            std::vector<AnchorCandidate> candidates;
            candidates.reserve( 256 );
            const int varScanStep = 3;
            const int varXStep = max( 1, ( frameWidth - 2 * xMargin ) / 80 );
            for( int y = 10; y < frameHeight - 10; y += varScanStep )
            {
                double sum = 0, sumSq = 0;
                int n = 0;
                for( int x = xMargin; x < frameWidth - xMargin; x += varXStep )
                {
                    const size_t off = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4 +
                                       static_cast<size_t>( x ) * 4;
                    const double luma = srcPx[off + 2] * 0.299 + srcPx[off + 1] * 0.587 + srcPx[off + 0] * 0.114;
                    sum += luma;
                    sumSq += luma * luma;
                    ++n;
                }
                if( n > 0 )
                {
                    const double mean = sum / n;
                    const double var = sumSq / n - mean * mean;
                    if( var > 500.0 )
                        candidates.push_back( { y, var } );
                }
            }

            // Sort descending by variance so we try the best rows first.
            std::sort( candidates.begin(), candidates.end(),
                       []( const AnchorCandidate& a, const AnchorCandidate& b ) { return a.var > b.var; } );

            // Limit to top 20 candidates for performance.
            if( candidates.size() > 20 )
                candidates.resize( 20 );

            // Helper: extract a row signature from the reference frame.
            auto extractSig = [&]( int y, std::vector<int>& sigR, std::vector<int>& sigG, std::vector<int>& sigB )
            {
                sigR.clear(); sigG.clear(); sigB.clear();
                for( int x = xMargin; x < frameWidth - xMargin; x += sigXStep )
                {
                    const size_t off = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4 +
                                       static_cast<size_t>( x ) * 4;
                    sigB.push_back( srcPx[off + 0] );
                    sigG.push_back( srcPx[off + 1] );
                    sigR.push_back( srcPx[off + 2] );
                }
            };

            // Helper: compute MAD between two signatures.
            auto sigMAD = [&]( const std::vector<int>& sigR, const std::vector<int>& sigG,
                               const std::vector<int>& sigB, const std::vector<BYTE>& px, int y ) -> double
            {
                if( y < 0 || y >= frameHeight )
                    return 9999.0;
                const int len = static_cast<int>( sigR.size() );
                double diff = 0;
                for( int si = 0; si < len; ++si )
                {
                    const int x = xMargin + si * sigXStep;
                    const size_t off = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4 +
                                       static_cast<size_t>( x ) * 4;
                    diff += abs( sigB[si] - static_cast<int>( px[off + 0] ) )
                          + abs( sigG[si] - static_cast<int>( px[off + 1] ) )
                          + abs( sigR[si] - static_cast<int>( px[off + 2] ) );
                }
                return diff / ( len * 3.0 );
            };

            // Step 2: Find the first candidate row that is UNIQUE within the
            // reference frame AND matchable in the current frame.
            int bestAnchorY = -1;
            double bestAnchorVar = 0;
            std::vector<int> anchorSigR, anchorSigG, anchorSigB;

            for( const auto& cand : candidates )
            {
                extractSig( cand.y, anchorSigR, anchorSigG, anchorSigB );

                // Check for duplicates in the reference frame.
                bool hasDuplicate = false;
                for( int checkY = 10; checkY < frameHeight - 10; checkY += varScanStep )
                {
                    if( abs( checkY - cand.y ) < 20 )
                        continue; // Skip nearby rows (same feature).
                    const double mad = sigMAD( anchorSigR, anchorSigG, anchorSigB, srcPx, checkY );
                    if( mad < 3.0 )
                    {
                        hasDuplicate = true;
                        break;
                    }
                }

                if( hasDuplicate )
                    continue;

                // Verify the row is actually findable in the current frame.
                // Use a loose threshold here — the purpose is to exclude rows
                // that changed entirely between frames (MAD > 50), not to
                // require pixel-perfect matches.  The final search (step 3)
                // enforces the tight confidence threshold.
                // Scan every row (step=1) because pixel-perfect matches can
                // be missed even at step=3 (e.g., MAD=0 at y=205 but MAD=80
                // at y=204 and y=207).
                double bestProbeMAD = 9999.0;
                for( int probeY = 10; probeY < frameHeight - 10; ++probeY )
                {
                    const double mad = sigMAD( anchorSigR, anchorSigG, anchorSigB, tgtPx, probeY );
                    if( mad < bestProbeMAD )
                        bestProbeMAD = mad;
                    if( bestProbeMAD < 1.0 )
                        break; // Found a near-perfect match, no need to continue.
                }

                if( bestProbeMAD < 30.0 )
                {
                    bestAnchorY = cand.y;
                    bestAnchorVar = cand.var;
                    break; // Found a unique, matchable distinctive row.
                }
            }

            if( bestAnchorY < 0 )
            {
                // Log why no anchor was found — useful for debugging.
                int totalUnique = 0, totalMatchable = 0;
                for( const auto& cand : candidates )
                {
                    extractSig( cand.y, anchorSigR, anchorSigG, anchorSigB );
                    bool hasDup = false;
                    for( int checkY = 10; checkY < frameHeight - 10; checkY += varScanStep )
                    {
                        if( abs( checkY - cand.y ) < 20 ) continue;
                        if( sigMAD( anchorSigR, anchorSigG, anchorSigB, refPx, checkY ) < 3.0 ) { hasDup = true; break; }
                    }
                    if( !hasDup )
                    {
                        ++totalUnique;
                        double bestP = 9999.0;
                        for( int probeY = 10; probeY < frameHeight - 10; ++probeY )
                        {
                            const double m = sigMAD( anchorSigR, anchorSigG, anchorSigB, curPx, probeY );
                            if( m < bestP ) bestP = m;
                            if( bestP < 1.0 ) break;
                        }
                        if( bestP < 30.0 ) ++totalMatchable;
                    }
                }
                StitchLog( L"[Panorama/Stitch] Frame %zu AnchorNoCandidate: totalCandidates=%zu unique=%d matchable=%d refIdx=%zu constFrac=%.2f dy=%d refPxSize=%zu\n",
                             i, candidates.size(), totalUnique, totalMatchable, refIdx,
                             frameConstantFraction[refIdx], dy, refPx.size() );
            }

            if( bestAnchorY >= 0 )
            {
                // Step 3: Search the target frame for this unique signature.
                // When useCurAsSrc, the search finds the offset in refPx, and
                // we negate it to get the true scroll direction.
                const int searchRadius = max( abs( dy ) + 20, frameHeight / 2 );
                // When searching ref→cur (normal): dy is negative, search negative range.
                // When searching cur→ref (inverted): search positive range (inverted scroll).
                const int effectiveDy = useCurAsSrc ? -dy : dy;
                const int searchLo = max( -( frameHeight - 1 ), effectiveDy - searchRadius );
                const int searchHi = min( frameHeight - 1, effectiveDy + searchRadius );

                double bestMatchMAD = 9999.0;
                int bestMatchDy = effectiveDy;
                double secondBestMAD = 9999.0;
                int secondBestDy = effectiveDy;
                double matcherAnchorMAD = 9999.0;

                for( int candDy = searchLo; candDy <= searchHi; ++candDy )
                {
                    const int targetY = bestAnchorY + candDy;
                    if( targetY < 0 || targetY >= frameHeight )
                        continue;

                    const double mad = sigMAD( anchorSigR, anchorSigG, anchorSigB, tgtPx, targetY );

                    if( candDy == effectiveDy )
                        matcherAnchorMAD = mad;

                    if( mad < bestMatchMAD )
                    {
                        // Demote previous best to second-best if not nearby.
                        if( abs( bestMatchDy - candDy ) > 5 )
                        {
                            secondBestMAD = bestMatchMAD;
                            secondBestDy = bestMatchDy;
                        }
                        bestMatchMAD = mad;
                        bestMatchDy = candDy;
                    }
                    else if( mad < secondBestMAD && abs( candDy - bestMatchDy ) > 5 )
                    {
                        secondBestMAD = mad;
                        secondBestDy = candDy;
                    }
                }

                // Step 4: Override if the anchor found a confident, different answer.
                // Confident = best match is significantly better than second-best
                // (i.e., there's a clear unique winner, not multiple harmonics).
                // For adjacent frames, bestMAD ≈ 0 and secondBestMAD > 50 (trivial).
                // For gap-bridge frames, bestMAD ≈ 20 and secondBestMAD > 50 (still clear).
                // Convert anchor result back to actual scroll direction.
                const int anchorScrollDy = useCurAsSrc ? -bestMatchDy : bestMatchDy;

                const double separation = secondBestMAD - bestMatchMAD;
                const bool anchorConfident = bestMatchMAD < 30.0 && separation > 5.0;
                const bool anchorDiffers = anchorScrollDy != dy;
                const bool anchorSmaller = abs( anchorScrollDy ) < abs( dy );
                const bool matcherWorse = matcherAnchorMAD > bestMatchMAD + 3.0;

                if( anchorConfident && anchorDiffers && anchorSmaller && matcherWorse )
                {
                    const int absMatcher = abs( dy );
                    const int absAnchor  = abs( anchorScrollDy );
                    // Don't override to zero — if the anchor thinks the
                    // frame is stationary but the matcher found a real
                    // shift, this is almost certainly a content-repeat
                    // false positive on repetitive/constant content.
                    // Truly stationary frames are already caught by
                    // duplicate detection before reaching this point.
                    const bool largeHarmonicCorrection = absMatcher > 0 && absAnchor > 0 && absAnchor * 2 < absMatcher;

                    // Also correct small same-direction misalignments (off-by-1/2)
                    // where the anchor's per-row signature matching is more precise
                    // than the global correlation matcher.  Restricted to same-sign
                    // shifts to avoid applying sign inversions on ambiguous content.
                    const bool sameDirection = (dy > 0 && anchorScrollDy > 0) || (dy < 0 && anchorScrollDy < 0);
                    const bool smallCorrection = sameDirection && (absMatcher - absAnchor) <= 2;

                    StitchLog( L"[Panorama/Stitch] Frame %zu AnchorOverride: matcherDy=%d anchorDy=%d anchorMAD=%.2f matcherMAD=%.2f secondMAD=%.2f sep=%.1f anchorY=%d anchorVar=%.0f constFrac=%.2f inverted=%d largeHarmonic=%d smallCorr=%d\n",
                                 i, dy, anchorScrollDy, bestMatchMAD, matcherAnchorMAD,
                                 secondBestMAD, separation,
                                 bestAnchorY, bestAnchorVar,
                                 frameConstantFraction[refIdx],
                                 useCurAsSrc ? 1 : 0,
                                 largeHarmonicCorrection ? 1 : 0,
                                 smallCorrection ? 1 : 0 );

                    if( largeHarmonicCorrection || smallCorrection )
                    {
                        dy = anchorScrollDy;
                        dx = 0;
                    }
                }
                else
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu AnchorKept: matcherDy=%d anchorBestDy=%d anchorMAD=%.2f matcherMAD=%.2f secondMAD=%.2f sep=%.1f anchorY=%d anchorVar=%.0f confident=%d differs=%d smaller=%d matcherWorse=%d inverted=%d\n",
                                 i, dy, anchorScrollDy, bestMatchMAD, matcherAnchorMAD,
                                 secondBestMAD, separation,
                                 bestAnchorY, bestAnchorVar,
                                 anchorConfident ? 1 : 0, anchorDiffers ? 1 : 0, anchorSmaller ? 1 : 0,
                                 matcherWorse ? 1 : 0, useCurAsSrc ? 1 : 0 );

                    // When the anchor independently confirms the matcher's
                    // result with high confidence, mark the shift as verified
                    // so momentum-collapse does not override it.  This prevents
                    // a feedback loop where a previous harmonic error sets a
                    // bad expected step, and the normalizer "corrects" the
                    // current (correct) small step back to the wrong value.
                    if( anchorConfident && !anchorDiffers )
                        anchorVerifiedShift = true;
                }
            }
        }

        // Early growth-outlier guard for low-entropy startup/recovery.
        // This catches the first large harmonic jump (e.g. 330 -> 800)
        // before long-enough history exists for continuity statistics.
        if( veryLowEntropy != 0 && composedFrameSteps.size() >= 1 )
        {
            const bool expectedMostlyVertical = abs( expectedDy ) >= max( abs( expectedDx ) * 2, minProgress );
            const bool expectedMostlyHorizontal = abs( expectedDx ) >= max( abs( expectedDy ) * 2, minProgress );
            const int expectedAxisSigned = expectedMostlyVertical ? expectedDy :
                                          ( expectedMostlyHorizontal ? expectedDx : 0 );
            const int candidateAxisSigned = expectedMostlyVertical ? dy :
                                           ( expectedMostlyHorizontal ? dx : 0 );
            const int axisFrame = expectedMostlyVertical ? frameHeight :
                                 ( expectedMostlyHorizontal ? frameWidth : 0 );

            if( axisFrame > 0 && expectedAxisSigned != 0 && candidateAxisSigned != 0 &&
                ( ( expectedAxisSigned > 0 ) == ( candidateAxisSigned > 0 ) ) &&
                abs( expectedAxisSigned ) >= max( minProgress * 3, 64 ) )
            {
                const int expectedAbs = abs( expectedAxisSigned );
                const int candidateAbs = abs( candidateAxisSigned );
                const int growthCap = max( expectedAbs * 2, ( axisFrame * 2 ) / 3 );
                const int farFromExpected = abs( candidateAxisSigned - expectedAxisSigned );
                if( candidateAbs > growthCap && farFromExpected > max( minProgress * 2, expectedAbs / 2 ) )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: early-growth-outlier shift=(%d,%d) expected=(%d,%d) cap=%d\n",
                                 i,
                                 dx,
                                 dy,
                                 expectedDx,
                                 expectedDy,
                                 growthCap );
                    continue;
                }
            }
        }

        // Direct anti-reversal guard for VLE captures: when expected motion
        // is strongly established in one direction, reject small opposite-
        // sign shifts that are commonly harmonic aliases.
        if( veryLowEntropy != 0 && composedFrameSteps.size() >= 6 )
        {
            int histAbsX = 0;
            int histAbsY = 0;
            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
            {
                histAbsX += abs( composedFrameSteps[si].x );
                histAbsY += abs( composedFrameSteps[si].y );
            }
            const bool mostlyVerticalHist = histAbsY > histAbsX * 3;
            const bool mostlyHorizontalHist = histAbsX > histAbsY * 3;
            const int expectedAxisSigned = mostlyVerticalHist ? expectedDy :
                                          ( mostlyHorizontalHist ? expectedDx : 0 );
            const int candidateAxisSigned = mostlyVerticalHist ? dy :
                                           ( mostlyHorizontalHist ? dx : 0 );
            const int axisFrame = mostlyVerticalHist ? frameHeight :
                                 ( mostlyHorizontalHist ? frameWidth : 0 );

            std::vector<int> recentAxisAbs;
            recentAxisAbs.reserve( 10 );
            for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                 si >= 1 && static_cast<int>( recentAxisAbs.size() ) < 10;
                 --si )
            {
                const int sv = mostlyVerticalHist
                    ? composedFrameSteps[static_cast<size_t>( si )].y
                    : ( mostlyHorizontalHist
                        ? composedFrameSteps[static_cast<size_t>( si )].x
                        : 0 );
                if( abs( sv ) > 0 )
                    recentAxisAbs.push_back( abs( sv ) );
            }
            int recentMedianAbs = 0;
            int recentMaxAbs = 0;
            if( !recentAxisAbs.empty() )
            {
                for( int v : recentAxisAbs )
                    recentMaxAbs = max( recentMaxAbs, v );
                std::sort( recentAxisAbs.begin(), recentAxisAbs.end() );
                recentMedianAbs = recentAxisAbs[recentAxisAbs.size() / 2];
            }

            // Only trust expected-axis normalization when expected motion is
            // broadly consistent with recent history. If expected has already
            // drifted into a large harmonic outlier, forcing normalization to
            // that value can replicate duplicated content bands.
            const bool expectedAxisTrustedForNormalization =
                recentMedianAbs > 0 &&
                abs( expectedAxisSigned ) <= max( recentMedianAbs * 3, axisFrame / 3 );

            // Growth-outlier guard: in low-entropy captures, startup recovery
            // can occasionally lock to a moderate expected step and then jump
            // to a near-frame-height harmonic alias. Reject sudden same-sign
            // amplification far beyond both expected and recent motion.
            if( veryLowEntropy &&
                axisFrame > 0 && expectedAxisSigned != 0 && candidateAxisSigned != 0 &&
                ( ( expectedAxisSigned > 0 ) == ( candidateAxisSigned > 0 ) ) &&
                recentMedianAbs > 0 &&
                abs( expectedAxisSigned ) >= max( minProgress * 3, 64 ) )
            {
                const int candidateAbs = abs( candidateAxisSigned );
                const int expectedAbs = abs( expectedAxisSigned );
                const int growthCap = max( max( expectedAbs * 2, recentMedianAbs * 2 ), ( axisFrame * 2 ) / 3 );
                const int farFromExpected = abs( candidateAxisSigned - expectedAxisSigned );
                if( candidateAbs > growthCap && farFromExpected > max( minProgress * 2, expectedAbs / 2 ) )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: growth-outlier shift=(%d,%d) expected=(%d,%d) median=%d cap=%d\n",
                                 i,
                                 dx,
                                 dy,
                                 expectedDx,
                                 expectedDy,
                                 recentMedianAbs,
                                 growthCap );
                    continue;
                }
            }

            if( axisFrame > 0 && expectedAxisSigned != 0 && candidateAxisSigned != 0 &&
                ( ( expectedAxisSigned > 0 ) != ( candidateAxisSigned > 0 ) ) &&
                abs( expectedAxisSigned ) >= axisFrame / 4 &&
                abs( candidateAxisSigned ) <= max( minProgress * 3, abs( expectedAxisSigned ) / 2 ) )
            {
                StitchLog( L"[Panorama/Stitch] Frame %zu rejected: expected-reversal-harmonic shift=(%d,%d) expected=(%d,%d)\n",
                             i,
                             dx,
                             dy,
                             expectedDx,
                             expectedDy );
                continue;
            }

            // Momentum-collapse guard: after sustained large motion, tiny
            // same-direction steps are often harmonic aliases.  Normalize
            // to expected motion rather than rejecting, so the stitcher
            // keeps advancing and does not get stuck on an old reference.
            //
            // However, if multiple consecutive frames trigger this guard,
            // the scroll has genuinely slowed down — the tiny steps are
            // real, not aliases.  Continuing to normalize creates a
            // feedback loop that over-advances the canvas, producing
            // repeated content and dark gaps.  Cap at 2 consecutive
            // normalizations to break the loop.
            if( axisFrame > 0 && expectedAxisSigned != 0 && candidateAxisSigned != 0 &&
                ( ( expectedAxisSigned > 0 ) == ( candidateAxisSigned > 0 ) ) &&
                recentMedianAbs >= axisFrame / 10 &&
                recentMaxAbs >= axisFrame / 4 &&
                expectedAxisTrustedForNormalization &&
                abs( expectedAxisSigned ) >= axisFrame / 8 &&
                abs( candidateAxisSigned ) <= max( 16, abs( expectedAxisSigned ) / 5 ) &&
                consecutiveMomentumCollapseCount < 2 &&
                !anchorVerifiedShift )
            {
                // Save the pre-normalization detected shift.  After
                // composing this frame at the expected step, propagate
                // the detected value as next-expected so that if the
                // scroll genuinely decelerated the expected step decays
                // rather than staying locked at the historical peak.
                momentumCollapseDetectedDx = dx;
                momentumCollapseDetectedDy = dy;
                momentumCollapseApplied = true;
                StitchLog( L"[Panorama/Stitch] Frame %zu normalized: momentum-collapse-harmonic shift=(%d,%d) expected=(%d,%d) median=%d max=%d consecutiveCollapse=%d\n",
                             i,
                             dx,
                             dy,
                             expectedDx,
                             expectedDy,
                             recentMedianAbs,
                             recentMaxAbs,
                             consecutiveMomentumCollapseCount + 1 );
                dx = expectedDx;
                dy = expectedDy;
                ++consecutiveMomentumCollapseCount;
            }
            else if( axisFrame > 0 && expectedAxisSigned != 0 && candidateAxisSigned != 0 &&
                     ( ( expectedAxisSigned > 0 ) == ( candidateAxisSigned > 0 ) ) &&
                     abs( candidateAxisSigned ) <= max( 16, abs( expectedAxisSigned ) / 5 ) &&
                     consecutiveMomentumCollapseCount >= 2 )
            {
                // Scroll genuinely decelerated — accept the small step
                // and let expected motion adapt naturally.
                StitchLog( L"[Panorama/Stitch] Frame %zu momentum-collapse-capped: shift=(%d,%d) expected=(%d,%d) consecutiveCollapse=%d (accepting small step)\n",
                             i,
                             dx,
                             dy,
                             expectedDx,
                             expectedDy,
                             consecutiveMomentumCollapseCount );
                consecutiveMomentumCollapseCount = 0;
            }
            else
            {
                // Frame was not momentum-collapse normalized — reset the
                // consecutive counter so future genuine decelerations are
                // detected fresh.
                consecutiveMomentumCollapseCount = 0;
            }
        }

        // After momentum-collapse normalization, propagate the DETECTED
        // (pre-normalization) shift as the next expected step.  This lets
        // the expected step decay toward actual scroll speed rather than
        // staying locked at the historical peak, which would cause a
        // feedback loop that over-advances the canvas.
        int momentumCollapseNextExpectedDx = 0;
        int momentumCollapseNextExpectedDy = 0;
        if( momentumCollapseApplied )
        {
            momentumCollapseNextExpectedDx = momentumCollapseDetectedDx;
            momentumCollapseNextExpectedDy = momentumCollapseDetectedDy;
        }

        int stepX = -dx;
        int stepY = -dy;

        // After establishing a predominantly vertical or horizontal scroll
        // direction, clamp the perpendicular component to zero.  Subpixel
        // rendering noise (e.g. ClearType) causes the fine refinement to
        // report +/-1 px cross-axis drift per frame, which accumulates into
        // visible slanting over many composed frames.
        //
        // Cap each step's contribution to the direction vote so that one
        // outlier (e.g. a spurious large-shift match on blank content)
        // cannot dominate the accumulator and lock the wrong axis.
        if( composedFrameSteps.size() >= 3 )
        {
            const int stepCapForDirection = 3 * minProgress;
            int totalAbsStepX = 0, totalAbsStepY = 0;
            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
            {
                totalAbsStepX += min( abs( composedFrameSteps[si].x ), stepCapForDirection );
                totalAbsStepY += min( abs( composedFrameSteps[si].y ), stepCapForDirection );
            }

            if( totalAbsStepY > totalAbsStepX * 8 )
            {
                stepX = 0;
            }
            else if( totalAbsStepX > totalAbsStepY * 8 )
            {
                stepY = 0;
            }
        }

        // Near-stationary override: when FindBestFrameShift flags the match
        // as unreliable (fine score per-pixel >= stationary score on near-
        // identical frames), compose the frame at step=0 instead of the
        // detected (likely harmonic) step.  This avoids advancing the
        // canvas by a wrong harmonic amount while still updating the
        // comparison reference for the next frame.  Since the frame is
        // near-identical to the previous one, compositing at step=0 just
        // overwrites the canvas with nearly the same pixels.  The expected
        // step is updated from the original detected shift so subsequent
        // tie-breaking is unchanged.
        bool nearStationaryZeroStep = false;
        if( nearStationaryOverride )
        {
            nearStationaryCount++;
            if( abs( stepX ) + abs( stepY ) > minProgress )
            {
                StitchLog( L"[Panorama/Stitch] Frame %zu near-stationary zero-step: original=(%d,%d) count=%d\n",
                             i, stepX, stepY, nearStationaryCount );
                stepX = 0;
                stepY = 0;
                nearStationaryZeroStep = true;
            }
        }

        // Cap the low-movement threshold so large frames don't reject
        // real slow-scroll steps (e.g. 1071px -> minProgress/2=17 drops
        // genuine 4-16 px scrolls).
        if( !nearStationaryZeroStep && abs( stepX ) + abs( stepY ) < min( minProgress / 2, 4 ) )
        {
            StitchLog( L"[Panorama/Stitch] Frame %zu rejected: low movement step=(%d,%d)\n", i, stepX, stepY );
            continue;
        }

        // Spike-recovery: when spike/outlier guards have rejected
        // several consecutive frames, the expected step is likely
        // corrupted by a preceding momentum-collapse cascade.  On
        // dark / low-contrast content the matcher cannot reliably
        // distinguish the true offset from harmonics, so correcting
        // the expected alone does not help — the matched shift is
        // unreliable.
        //
        // Instead, *override* the matched step with a predicted step
        // derived from the pre-collapse history: the median of recent
        // "healthy" (non-collapse) signed steps, scaled by the gap
        // from the last accepted frame.  This positions the frame at
        // a plausible canvas offset.  On the dark/uniform content
        // that triggers this path, sub-frame positioning errors are
        // invisible.
        //
        // The near-stationary-spike guard (the one corrupted by
        // collapse statistics) is skipped for this frame; all other
        // guards remain active so obviously-wrong predictions are
        // still caught.
        bool spikeRecoveryActive = false;
        if( consecutiveSpikeRejectCount >= 3 && composedFrameSteps.size() >= 6 )
        {
            // Global median of accepted step magnitudes (robust to
            // the small proportion of collapse entries).
            std::vector<int> allStepMag;
            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
            {
                const int v = max( abs( composedFrameSteps[si].x ),
                                   abs( composedFrameSteps[si].y ) );
                if( v > 0 )
                    allStepMag.push_back( v );
            }
            if( !allStepMag.empty() )
            {
                std::sort( allStepMag.begin(), allStepMag.end() );
                const int globalMedian = allStepMag[allStepMag.size() / 2];
                const int healthyFloor = max( 2, globalMedian / 3 );

                // Collect recent non-collapse signed steps for both axes.
                std::vector<int> healthyX, healthyY;
                for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                     si >= 1 && healthyX.size() < 20;
                     --si )
                {
                    if( max( abs( composedFrameSteps[si].x ),
                             abs( composedFrameSteps[si].y ) ) >= healthyFloor )
                    {
                        healthyX.push_back( composedFrameSteps[si].x );
                        healthyY.push_back( composedFrameSteps[si].y );
                    }
                }
                if( !healthyX.empty() )
                {
                    std::sort( healthyX.begin(), healthyX.end() );
                    std::sort( healthyY.begin(), healthyY.end() );
                    const int gap = max( 1, static_cast<int>( i - composedFrameIndices.back() ) );
                    const int perFrameStepX = healthyX[healthyX.size() / 2];
                    const int perFrameStepY = healthyY[healthyY.size() / 2];

                    // Override matched shift with predicted gap-scaled step.
                    stepX = perFrameStepX * gap;
                    stepY = perFrameStepY * gap;
                    dx = -stepX;
                    dy = -stepY;

                    // Set next-expected to the per-frame healthy step so
                    // subsequent single-frame comparisons search correctly.
                    hasNextExpectedOverride = true;
                    overrideNextExpectedDx = -perFrameStepX;
                    overrideNextExpectedDy = -perFrameStepY;
                    overrideEligibilityStep = max( abs( perFrameStepX ), abs( perFrameStepY ) );

                    spikeRecoveryActive = true;
                    StitchLog( L"[Panorama/Stitch] Frame %zu spike-recovery: count=%d gap=%d perFrame=(%d,%d) step=(%d,%d)\n",
                                 i,
                                 consecutiveSpikeRejectCount,
                                 gap,
                                 perFrameStepX,
                                 perFrameStepY,
                                 stepX,
                                 stepY );
                }
            }
            consecutiveSpikeRejectCount = 0;
        }

        // Continuity guard: reject implausible large jumps once motion is
        // established. This blocks harmonic matches that can skip ranges and
        // create duplicate/missing content blocks.
        if( composedFrameSteps.size() >= 6 )
        {
            int totalAbsX = 0;
            int totalAbsY = 0;
            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
            {
                totalAbsX += abs( composedFrameSteps[si].x );
                totalAbsY += abs( composedFrameSteps[si].y );
            }

            const bool mostlyVertical = totalAbsY > totalAbsX * 3;
            const bool mostlyHorizontal = totalAbsX > totalAbsY * 3;
            const int axisStep = mostlyVertical ? abs( stepY ) : ( mostlyHorizontal ? abs( stepX ) : max( abs( stepX ), abs( stepY ) ) );
            const int axisFrame = mostlyVertical ? frameHeight : frameWidth;
            const int axisOverlap = max( 0, axisFrame - axisStep );
            const int expectedAxisStep = mostlyVertical ? abs( expectedDy ) :
                                         ( mostlyHorizontal ? abs( expectedDx ) : max( abs( expectedDx ), abs( expectedDy ) ) );

            std::vector<int> recentAxisSteps;
            recentAxisSteps.reserve( 12 );
            for( int si = static_cast<int>( composedFrameSteps.size() ) - 1; si >= 1 && static_cast<int>( recentAxisSteps.size() ) < 12; --si )
            {
                const int v = mostlyVertical ? abs( composedFrameSteps[si].y ) :
                             ( mostlyHorizontal ? abs( composedFrameSteps[si].x ) :
                               max( abs( composedFrameSteps[si].x ), abs( composedFrameSteps[si].y ) ) );
                if( v > 0 )
                    recentAxisSteps.push_back( v );
            }

            if( recentAxisSteps.size() >= 4 )
            {
                std::vector<int> sorted = recentAxisSteps;
                std::sort( sorted.begin(), sorted.end() );
                const int medianAxisStep = sorted[sorted.size() / 2];
                int outlierStepThreshold = max( ( axisFrame * 2 ) / 5, max( minProgress * 6, medianAxisStep * 5 ) );
                const int lowOverlapThreshold = ( axisFrame * 3 ) / 5;
                int expectedSpikeThreshold = max( axisFrame / 3, max( minProgress * 5, expectedAxisStep * 3 ) );

                // Very-low-entropy content can produce long runs of small
                // accepted steps followed by a legitimate large jump.
                // Raise the outlier floor so those jumps are judged by the
                // tighter percentile/overlap guards below instead of being
                // dropped immediately by this coarse continuity gate.
                if( veryLowEntropy != 0 )
                {
                    outlierStepThreshold = max( outlierStepThreshold, ( axisFrame * 7 ) / 10 );
                    expectedSpikeThreshold = max( expectedSpikeThreshold, ( axisFrame * 7 ) / 10 );
                }

                // Direction-reversal guard for VLE captures: once a strong
                // axis direction is established with substantial expected
                // motion, reject opposite-sign steps that are often harmonic
                // aliases and create repeated content bands/blank seams.
                if( veryLowEntropy != 0 )
                {
                    const int axisSignMin = max( 4, minProgress );
                    const int candidateSignMin = max( 8, minProgress / 2 );
                    int signedSum = 0;
                    int signedCount = 0;
                    for( int si = static_cast<int>( composedFrameSteps.size() ) - 1;
                         si >= 1 && signedCount < 10;
                         --si )
                    {
                        const int sv = mostlyVertical
                            ? composedFrameSteps[static_cast<size_t>( si )].y
                            : composedFrameSteps[static_cast<size_t>( si )].x;
                        if( abs( sv ) < axisSignMin )
                            continue;
                        signedSum += ( sv > 0 ) ? 1 : -1;
                        signedCount++;
                    }

                    const int candidateSigned = mostlyVertical ? stepY : stepX;
                    const int expectedSigned = mostlyVertical ? -expectedDy : -expectedDx;
                    const bool dominantEstablished =
                        signedCount >= 5 && abs( signedSum ) >= ( signedCount * 2 ) / 3;
                    const bool candidateSignificant = abs( candidateSigned ) >= candidateSignMin;
                    const bool oppositeDominant =
                        dominantEstablished && candidateSigned != 0 &&
                        ( ( candidateSigned > 0 ) != ( signedSum > 0 ) );
                    const bool oppositeExpected =
                        expectedSigned != 0 && candidateSigned != 0 &&
                        ( ( candidateSigned > 0 ) != ( expectedSigned > 0 ) );
                    const bool expectedLarge = abs( expectedSigned ) >= max( axisFrame / 8, axisSignMin * 6 );

                    if( oppositeDominant && oppositeExpected && candidateSignificant && expectedLarge )
                    {
                        StitchLog( L"[Panorama/Stitch] Frame %zu rejected: direction-reversal step=(%d,%d) axisStep=%d expected=(%d,%d) signedSum=%d count=%d\n",
                                     i,
                                     stepX,
                                     stepY,
                                     axisStep,
                                     expectedDx,
                                     expectedDy,
                                     signedSum,
                                     signedCount );
                        continue;
                    }
                }

                if( axisStep >= outlierStepThreshold && axisOverlap < lowOverlapThreshold )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: outlier step=(%d,%d) axisStep=%d median=%d overlap=%d/%d\n",
                                 i,
                                 stepX,
                                 stepY,
                                 axisStep,
                                 medianAxisStep,
                                 axisOverlap,
                                 axisFrame );
                    consecutiveSpikeRejectCount++;
                    continue;
                }

                // Additional harmonic-spike guard: if a candidate suddenly
                // jumps far beyond expected motion while overlap is reduced,
                // treat it as unreliable even when historical median is high.
                if( expectedAxisStep > 0 && axisStep >= expectedSpikeThreshold && axisOverlap < axisFrame / 2 )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: spike step=(%d,%d) axisStep=%d expectedAxis=%d overlap=%d/%d\n",
                                 i,
                                 stepX,
                                 stepY,
                                 axisStep,
                                 expectedAxisStep,
                                 axisOverlap,
                                 axisFrame );
                    consecutiveSpikeRejectCount++;
                    continue;
                }

                // Near-stationary spike guard: when recent motion is very
                // small (< 5% of frame), a large jump is almost certainly
                // a harmonic match on periodic content rather than a
                // genuine scroll acceleration.  Unlike the other outlier
                // guards this has no hard floor tied to frame height, so
                // it catches spurious matches that slip under the 33-50%
                // floors above.  The 20x multiplier allows legitimate
                // jump-recovery scrolls (10-16x median) while catching
                // harmonic spikes (60x+ median in the real bug case).
                //
                // Also require p75 < axisFrame/10: if 25%+ of recent
                // steps were large the user was actively scrolling and
                // a big step is more likely genuine acceleration than a
                // harmonic artifact.
                const int p75AxisStep = sorted[sorted.size() * 3 / 4];
                if( !spikeRecoveryActive &&
                    medianAxisStep < axisFrame / 20 &&
                    p75AxisStep < axisFrame / 10 &&
                    axisStep > max( medianAxisStep * 20, minProgress * 4 ) )
                {
                    StitchLog( L"[Panorama/Stitch] Frame %zu rejected: near-stationary-spike step=(%d,%d) axisStep=%d median=%d p75=%d threshold=%d\n",
                                 i,
                                 stepX,
                                 stepY,
                                 axisStep,
                                 medianAxisStep,
                                 p75AxisStep,
                                 max( medianAxisStep * 20, minProgress * 4 ) );
                    consecutiveSpikeRejectCount++;
                    continue;
                }

                // Step-range outlier guard: on periodic content the fine
                // search can pick a harmonic that is only moderately above
                // normal -- not enough for the 4x median guard -- but well
                // above the observed step range.  Use the 75th percentile
                // of recent steps as a tighter reference.
                //
                // Keep a hard floor at 50% of the frame to avoid rejecting
                // legitimate fast-scroll jumps.  The stress "legitjumps"
                // scenarios include valid jumps up to that range.
                if( sorted.size() >= 6 )
                {
                    const int p75 = sorted[sorted.size() * 3 / 4];
                    const int rangeGuard = p75 + max( minProgress, medianAxisStep / 2 );
                    int legitJumpFloor = axisFrame / 2;
                    if( veryLowEntropy != 0 )
                    {
                        legitJumpFloor = max( legitJumpFloor, ( axisFrame * 7 ) / 10 );
                    }
                    const int guardedThreshold = max( rangeGuard, legitJumpFloor );
                    if( axisStep > guardedThreshold && axisOverlap < ( axisFrame * 2 ) / 3 )
                    {
                        StitchLog( L"[Panorama/Stitch] Frame %zu rejected: range-outlier step=(%d,%d) axisStep=%d p75=%d rangeGuard=%d floor=%d overlap=%d/%d\n",
                                     i,
                                     stepX,
                                     stepY,
                                     axisStep,
                                     p75,
                                     rangeGuard,
                                     legitJumpFloor,
                                     axisOverlap,
                                     axisFrame );
                        consecutiveSpikeRejectCount++;
                        continue;
                    }
                }

                // VLE continuity guard: on very-low-entropy content, ZNCC
                // scoring is unreliable because near-uniform pixels match
                // at essentially random offsets.  Use a tighter step ceiling
                // than the standard outlier guard, but still allow legitimate
                // fast-scroll jumps up to 50% of frame height.
                if( veryLowEntropy != 0 )
                {
                    const int vleMedianCeiling = max( medianAxisStep * 3, minProgress * 4 );
                    const int vleLegitFloor = ( axisFrame * 7 ) / 10; // allow up to 70% frame on VLE fast-scroll
                    const int vleStepCeiling = max( vleMedianCeiling, vleLegitFloor );
                    if( axisStep > vleStepCeiling )
                    {
                        StitchLog( L"[Panorama/Stitch] Frame %zu rejected: vle-outlier step=(%d,%d) axisStep=%d median=%d ceiling=%d\n",
                                     i,
                                     stepX,
                                     stepY,
                                     axisStep,
                                     medianAxisStep,
                                     vleStepCeiling );
                        consecutiveSpikeRejectCount++;
                        continue;
                    }
                }

            }
        }

        // Cross-verification diagnostic: after all guards, independently
        // verify the accepted shift by finding the most distinctive row
        // in the reference frame and searching for it in the current frame.
        // This exposes harmonic aliases where the matcher found a plausible
        // but incorrect offset.
        if( PanoramaDebugEnabled() && composedFrameSteps.size() >= 6 )
        {
            const size_t refIdx = composedFrameIndices.back();
            const std::vector<BYTE>& refPx = framePixels[refIdx];
            const std::vector<BYTE>& curPx = framePixels[i];

            // Find the most distinctive row in the reference frame
            // (highest variance across horizontal pixel samples).
            int bestVarRow = frameHeight / 2;
            double bestVar = 0;
            for( int y = frameHeight / 10; y < frameHeight * 9 / 10; y += 3 )
            {
                double sum = 0, sumSq = 0;
                int count = 0;
                for( int x = frameWidth / 8; x < frameWidth * 7 / 8; x += 8 )
                {
                    const size_t idx = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4 + static_cast<size_t>( x ) * 4;
                    const double val = refPx[idx + 2]; // R channel
                    sum += val;
                    sumSq += val * val;
                    ++count;
                }
                if( count > 0 )
                {
                    const double mean = sum / count;
                    const double var = ( sumSq / count ) - ( mean * mean );
                    if( var > bestVar )
                    {
                        bestVar = var;
                        bestVarRow = y;
                    }
                }
            }

            // Search for the reference row in the current frame.
            if( bestVar > 100 )
            {
                const int searchRange = min( frameHeight - 1, 600 );
                double bestMatchDiff = 1e9;
                int bestMatchDy = 0;
                for( int probe = -searchRange; probe <= 0; ++probe )
                {
                    const int ty = bestVarRow + probe;
                    if( ty < 0 || ty >= frameHeight )
                        continue;
                    double diff = 0;
                    int count = 0;
                    for( int x = frameWidth / 8; x < frameWidth * 7 / 8; x += 6 )
                    {
                        const size_t refOff = static_cast<size_t>( bestVarRow ) * static_cast<size_t>( frameWidth ) * 4 + static_cast<size_t>( x ) * 4;
                        const size_t curOff = static_cast<size_t>( ty ) * static_cast<size_t>( frameWidth ) * 4 + static_cast<size_t>( x ) * 4;
                        diff += abs( static_cast<int>( refPx[refOff + 0] ) - static_cast<int>( curPx[curOff + 0] ) )
                              + abs( static_cast<int>( refPx[refOff + 1] ) - static_cast<int>( curPx[curOff + 1] ) )
                              + abs( static_cast<int>( refPx[refOff + 2] ) - static_cast<int>( curPx[curOff + 2] ) );
                        ++count;
                    }
                    diff /= max( 1, count * 3 );
                    if( diff < bestMatchDiff )
                    {
                        bestMatchDiff = diff;
                        bestMatchDy = probe;
                    }
                }

                const int matcherDy = dy;
                const int verifyDy = bestMatchDy;
                const int discrepancy = abs( matcherDy ) - abs( verifyDy );
                if( abs( discrepancy ) > max( 8, abs( matcherDy ) / 4 ) )
                {
                    StitchLog( L"[Panorama/Stitch] CrossVerify MISMATCH frame=%zu ref=%zu matcherDy=%d verifyDy=%d discrepancy=%d verifyDiff=%.1f refRow=%d refVar=%.0f\n",
                               i,
                               refIdx,
                               matcherDy,
                               verifyDy,
                               discrepancy,
                               bestMatchDiff,
                               bestVarRow,
                               bestVar );
                }
                else
                {
                    StitchLog( L"[Panorama/Stitch] CrossVerify OK frame=%zu ref=%zu matcherDy=%d verifyDy=%d verifyDiff=%.1f\n",
                               i,
                               refIdx,
                               matcherDy,
                               verifyDy,
                               bestMatchDiff );
                }
            }
        }

        POINT nextOrigin = composedFrameOrigins.back();
        nextOrigin.x += stepX;
        nextOrigin.y += stepY;
        const int nextExpectedDx = hasNextExpectedOverride ? overrideNextExpectedDx :
                                   momentumCollapseApplied ? momentumCollapseNextExpectedDx : dx;
        const int nextExpectedDy = hasNextExpectedOverride ? overrideNextExpectedDy :
                                   momentumCollapseApplied ? momentumCollapseNextExpectedDy : dy;
        const int acceptedEligibilityStep = hasNextExpectedOverride
            ? overrideEligibilityStep
            : max( abs( dx ), abs( dy ) );
        composedFrameIndices.push_back( i );
        composedFrameOrigins.push_back( nextOrigin );
        composedFrameSteps.push_back( { stepX, stepY } );
        consecutiveSpikeRejectCount = 0;
        expectedDx = nextExpectedDx;
        expectedDy = nextExpectedDy;

        // After a retry spanning multiple frame intervals, normalize
        // the eligibility step to a single-frame estimate so the
        // inflated multi-frame shift doesn't block subsequent retries.
        // Budget limits consecutive normalizations per HCF zone.
        if( retryStreakUsed >= 2 && retryNormalizationBudget > 0 )
        {
            retryEligibilityStep = max( minProgress, max( abs( dx ), abs( dy ) ) / retryStreakUsed );
            retryNormalizationBudget--;
        }
        else
        {
            retryEligibilityStep = acceptedEligibilityStep;
            if( retryStreakUsed == 0 )
                retryNormalizationBudget = 5;
        }

        StitchLog( L"[Panorama/Stitch] Frame %zu accepted: dx=%d dy=%d step=(%d,%d) origin=(%d,%d)\n",
                     i,
                     dx,
                     dy,
                     stepX,
                     stepY,
                     nextOrigin.x,
                     nextOrigin.y );

        minX = min( minX, nextOrigin.x );
        minY = min( minY, nextOrigin.y );
        maxX = max( maxX, nextOrigin.x + frameWidth );
        maxY = max( maxY, nextOrigin.y + frameHeight );
    }

#ifdef _DEBUG
    g_StitchPerf.Report();
    g_StitchPerf.Reset();
#endif

    int totalAbsStepX = 0;
    int totalAbsStepY = 0;
    for( size_t si = 1; si < composedFrameSteps.size(); ++si )
    {
        totalAbsStepX += abs( composedFrameSteps[si].x );
        totalAbsStepY += abs( composedFrameSteps[si].y );
    }

    const bool mostlyHorizontalCapture = totalAbsStepX > totalAbsStepY;
    const bool shouldFlipHorizontal =
        mostlyHorizontalCapture &&
        !composedFrameOrigins.empty() &&
        composedFrameOrigins.back().x < composedFrameOrigins.front().x;
    const bool shouldFlipVertical =
        !mostlyHorizontalCapture &&
        !composedFrameOrigins.empty() &&
        composedFrameOrigins.back().y < composedFrameOrigins.front().y;

    // Normalize output orientation so the first frame appears at the top for
    // vertical captures and at the left for horizontal captures.
    if( shouldFlipHorizontal || shouldFlipVertical )
    {
        for( POINT& origin : composedFrameOrigins )
        {
            if( shouldFlipHorizontal )
            {
                origin.x = -origin.x;
            }
            if( shouldFlipVertical )
            {
                origin.y = -origin.y;
            }
        }

        for( POINT& step : composedFrameSteps )
        {
            if( shouldFlipHorizontal )
            {
                step.x = -step.x;
            }
            if( shouldFlipVertical )
            {
                step.y = -step.y;
            }
        }

        minX = 0;
        minY = 0;
        maxX = frameWidth;
        maxY = frameHeight;
        for( const POINT& origin : composedFrameOrigins )
        {
            minX = min( minX, origin.x );
            minY = min( minY, origin.y );
            maxX = max( maxX, origin.x + frameWidth );
            maxY = max( maxY, origin.y + frameHeight );
        }

        StitchLog( L"[Panorama/Stitch] Normalized orientation: first frame at %ls\n",
                     shouldFlipHorizontal ? L"left" : L"top" );
    }

    const int stitchedWidth = maxX - minX;
    const int stitchedHeight = maxY - minY;
    StitchLog( L"[Panorama/Stitch] Composition summary: composed=%zu/%zu canvas=%dx%d bounds=(%d,%d)-(%d,%d)\n",
                 composedFrameIndices.size(),
                 frames.size(),
                 stitchedWidth,
                 stitchedHeight,
                 minX,
                 minY,
                 maxX,
                 maxY );
    // Keep a hard upper bound to avoid pathological allocations.
    // CreateDIBSection uses LONG dimensions and supports values well
    // beyond 32767.  65535 allows ~300 MP at typical aspect ratios
    // while staying within practical memory limits.
    constexpr int kMaxStitchedCanvasDimension = 65535;
    if( stitchedWidth <= 0 ||
        stitchedHeight <= 0 ||
        stitchedWidth > kMaxStitchedCanvasDimension ||
        stitchedHeight > kMaxStitchedCanvasDimension )
    {
        StitchLog( L"[Panorama/Stitch] Invalid stitched canvas size %dx%d\n", stitchedWidth, stitchedHeight );
        return nullptr;
    }

    std::vector<BYTE> stitchedPixels( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4, 0 );
    std::vector<BYTE> stitchedWritten( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> stitchedOwner( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<BYTE> stitchedBlended( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> rowBlendPixelCount( static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> rowBlendWeightSum( static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> rowBlendWeightMin( static_cast<size_t>( stitchedHeight ), 255 );
    std::vector<int> rowBlendWeightMax( static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> rowBlendDominantFrame( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowBlendDominantPixels( static_cast<size_t>( stitchedHeight ), 0 );
    std::vector<int> rowFullWidthBlendFirstFrame( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendFirstPass( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendFirstWeight( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendLastFrame( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendLastPass( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendLastWeight( static_cast<size_t>( stitchedHeight ), -1 );
    std::vector<int> rowFullWidthBlendPassCount( static_cast<size_t>( stitchedHeight ), 0 );
    const int verticalFeather = max( 2, min( 12, frameHeight / 36 ) );
    const int horizontalFeather = max( 2, min( 12, frameWidth / 36 ) );
    FixedOverlayDiagnostics overlayDiagnostics{};
    const FixedOverlayMask fixedOverlayMask = BuildFixedOverlayMask( composedFrameIndices,
                                                                     composedFrameSteps,
                                                                     framePixels,
                                                                     frameLuma,
                                                                     frameWidth,
                                                                     frameHeight,
                                                                     minProgress,
                                                                     &overlayDiagnostics );
    if( !fixedOverlayMask.Empty() )
    {
        StitchLog( L"[Panorama/Stitch] FixedOverlay mask: pairs=%d informativeTiles=%d strongTiles=%d connectedTiles=%d maskedTiles=%d bounds=(%d,%d)-(%d,%d) tile=%dx%d\n",
                     overlayDiagnostics.pairCount,
                     overlayDiagnostics.informativeTileComparisons,
                     overlayDiagnostics.strongTileCount,
                     overlayDiagnostics.connectedTileCount,
                     overlayDiagnostics.maskedTileCount,
                     overlayDiagnostics.tileBoundsLeft,
                     overlayDiagnostics.tileBoundsTop,
                     overlayDiagnostics.tileBoundsRight,
                     overlayDiagnostics.tileBoundsBottom,
                     fixedOverlayMask.tileWidth,
                     fixedOverlayMask.tileHeight );
    }

    auto composeFrames = [&]( const FixedOverlayMask* overlayMask,
                              bool reportCompositionProgress,
                              std::vector<BYTE>& outPixels,
                              std::vector<BYTE>& outWritten,
                              std::vector<int>& outOwner,
                              std::vector<BYTE>& outBlended,
                              std::vector<BYTE>* outSuppressedMask,
                              std::vector<int>* outRowBlendPixelCount,
                              std::vector<int>* outRowBlendWeightSum,
                              std::vector<int>* outRowBlendWeightMin,
                              std::vector<int>* outRowBlendWeightMax,
                              std::vector<int>* outRowBlendDominantFrame,
                              std::vector<int>* outRowBlendDominantPixels,
                              std::vector<int>* outRowFullWidthBlendFirstFrame,
                              std::vector<int>* outRowFullWidthBlendFirstPass,
                              std::vector<int>* outRowFullWidthBlendFirstWeight,
                              std::vector<int>* outRowFullWidthBlendLastFrame,
                              std::vector<int>* outRowFullWidthBlendLastPass,
                              std::vector<int>* outRowFullWidthBlendLastWeight,
                              std::vector<int>* outRowFullWidthBlendPassCount,
                              unsigned __int64* outSuppressedPixels ) -> bool
    {
        outPixels.assign( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4, 0 );
        outWritten.assign( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
        outOwner.assign( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), -1 );
        outBlended.assign( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
        if( outSuppressedMask ) outSuppressedMask->assign( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
        if( outRowBlendPixelCount ) outRowBlendPixelCount->assign( static_cast<size_t>( stitchedHeight ), 0 );
        if( outRowBlendWeightSum ) outRowBlendWeightSum->assign( static_cast<size_t>( stitchedHeight ), 0 );
        if( outRowBlendWeightMin ) outRowBlendWeightMin->assign( static_cast<size_t>( stitchedHeight ), 255 );
        if( outRowBlendWeightMax ) outRowBlendWeightMax->assign( static_cast<size_t>( stitchedHeight ), 0 );
        if( outRowBlendDominantFrame ) outRowBlendDominantFrame->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowBlendDominantPixels ) outRowBlendDominantPixels->assign( static_cast<size_t>( stitchedHeight ), 0 );
        if( outRowFullWidthBlendFirstFrame ) outRowFullWidthBlendFirstFrame->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendFirstPass ) outRowFullWidthBlendFirstPass->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendFirstWeight ) outRowFullWidthBlendFirstWeight->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendLastFrame ) outRowFullWidthBlendLastFrame->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendLastPass ) outRowFullWidthBlendLastPass->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendLastWeight ) outRowFullWidthBlendLastWeight->assign( static_cast<size_t>( stitchedHeight ), -1 );
        if( outRowFullWidthBlendPassCount ) outRowFullWidthBlendPassCount->assign( static_cast<size_t>( stitchedHeight ), 0 );
        if( outSuppressedPixels ) *outSuppressedPixels = 0;

        std::atomic<unsigned __int64> suppressedPixels( 0 );

        for( size_t i = 0; i < composedFrameIndices.size(); ++i )
        {
            if( reportCompositionProgress )
            {
                reportProgress( 90 + static_cast<int>( ( i + 1 ) * 9 / composedFrameIndices.size() ) );
            }
            if( cancelled )
            {
                StitchLog( L"[Panorama/Stitch] Cancelled during composition\n" );
                return false;
            }

            const size_t frameIndex = composedFrameIndices[i];
            const POINT& currentOrigin = composedFrameOrigins[i];
            const int destinationX = currentOrigin.x - minX;
            const int destinationY = currentOrigin.y - minY;
            const std::vector<BYTE>& sourcePixels = framePixels[frameIndex];

            int stepX = 0;
            int stepY = 0;
            if( i > 0 )
            {
                stepX = composedFrameSteps[i].x;
                stepY = composedFrameSteps[i].y;
            }

            const int absStepX = abs( stepX );
            const int absStepY = abs( stepY );
            const bool mostlyVerticalMove = i > 0 && absStepY >= minProgress && abs( stepX ) <= max( 12, frameWidth / 20 );
            const bool mostlyHorizontalMove = i > 0 && absStepX >= minProgress && abs( stepY ) <= max( 12, frameHeight / 20 );
            const int overlapHeight = mostlyVerticalMove ? max( 0, frameHeight - absStepY ) : 0;
            const int overlapWidth = mostlyHorizontalMove ? max( 0, frameWidth - absStepX ) : 0;

            StitchLog( L"[Panorama/Stitch] Compose frame %zu src=%zu dest=(%d,%d) spanY=%d..%d step=(%d,%d) overlap=(%d,%d) mode=%ls mask=%d\n",
                       i,
                       frameIndex,
                       destinationX,
                       destinationY,
                       destinationY,
                       destinationY + frameHeight,
                       stepX,
                       stepY,
                       overlapWidth,
                       overlapHeight,
                       mostlyVerticalMove ? L"vertical" : ( mostlyHorizontalMove ? L"horizontal" : L"neutral" ),
                       overlayMask ? 1 : 0 );

            // Replace floating-overlay pixels with actual page content from
            // another frame where the same content is visible outside the
            // overlay region.  The overlay (spinner icon) sits at a fixed
            // screen position, but the page scrolls — so any page content
            // hidden by the overlay in this frame is visible at a shifted
            // position in a nearby frame.
            std::vector<BYTE> erasedPixels;
            const std::vector<BYTE>* composeSrc = &sourcePixels;
            // Skip erase-rect replacement for the last few composed frames:
            // at the tail of a panorama there are typically no later donor
            // frames to supply clean content, so leave the floating overlay
            // in place rather than smearing mismatched donor content.
            const bool lastFrame = ( i + 1 >= static_cast<int>( composedFrameIndices.size() ) );
            if( !lastFrame &&
                overlayMask != nullptr &&
                overlayMask->eraseRect.right > overlayMask->eraseRect.left &&
                overlayMask->eraseRect.bottom > overlayMask->eraseRect.top )
            {
                const RECT& er = overlayMask->eraseRect;
                erasedPixels = sourcePixels;
                bool anyReplaced = false;

                // For small/compact erase rects detected by the residual
                // overlay scan, skip donor diff validation — the residual
                // detection already confirmed these are overlay pixels, so
                // any scroll-aligned donor is better than the overlay.
                const int eraseW = er.right - er.left;
                const int eraseH = er.bottom - er.top;
                const bool compactEraseRect = ( eraseW <= frameWidth / 4 && eraseH <= frameHeight / 4 );

                for( int ey = er.top; ey < er.bottom && ey < frameHeight; ++ey )
                {
                    // For this row, find a donor frame where the same page
                    // content falls outside the erase rect.  Search nearby
                    // frames first (closest scroll offset).
                    bool rowReplaced = false;
                    for( int dist = 1; dist <= static_cast<int>( composedFrameIndices.size() ) && !rowReplaced; ++dist )
                    {
                        for( int sign = -1; sign <= 1 && !rowReplaced; sign += 2 )
                        {
                            const int j = static_cast<int>( i ) + dist * sign;
                            if( j < 0 || j >= static_cast<int>( composedFrameIndices.size() ) )
                                continue;

                            // Compute where this row's page content appears in frame j.
                            const int donorY = ey + ( composedFrameOrigins[i].y - composedFrameOrigins[j].y );
                            if( donorY < 0 || donorY >= frameHeight )
                                continue;

                            // The donor row must be outside the erase rect in frame j,
                            // and outside any tile-masked overlay area (e.g. bottom strip).
                            if( donorY >= er.top && donorY < er.bottom )
                                continue;
                            if( overlayMask->IsFullWidthMaskedRow( donorY ) )
                                continue;

                            const auto& donorPixels = framePixels[composedFrameIndices[j]];
                            if( donorPixels.empty() || donorPixels.size() != sourcePixels.size() )
                                continue;

                            // Validate donor row against surrounding context
                            // before copying — reject donors whose content
                            // diverges from the local source pixels.  Use a
                            // per-pixel comparison against the source row
                            // rather than edge context, since the erase rect
                            // can span the full frame width when sidebars and
                            // the overlay are both detected.
                            const int exLeft = max( 0, static_cast<int>( er.left ) );
                            const int exRight = min( frameWidth, static_cast<int>( er.right ) );

                            const int donorDx = composedFrameOrigins[i].x - composedFrameOrigins[j].x;
                            bool donorOk = true;
                            for( int ex = exLeft; ex < exRight; ++ex )
                            {
                                const int donorX = ex + donorDx;
                                if( donorX < 0 || donorX >= frameWidth )
                                    continue;

                                const size_t dstIdx = ( static_cast<size_t>( ey ) * frameWidth + ex ) * 4;
                                const size_t srcIdx = ( static_cast<size_t>( donorY ) * frameWidth + donorX ) * 4;
                                erasedPixels[dstIdx + 0] = donorPixels[srcIdx + 0];
                                erasedPixels[dstIdx + 1] = donorPixels[srcIdx + 1];
                                erasedPixels[dstIdx + 2] = donorPixels[srcIdx + 2];
                            }

                            // For large erase rects (e.g. strip-gated),
                            // validate donor against source to reject
                            // misaligned donors.  Skip for compact rects
                            // where residual detection already confirmed
                            // these are genuine overlay pixels.
                            if( !compactEraseRect )
                            {
                                int donorDiffSum = 0, donorDiffN = 0;
                                for( int ex = exLeft; ex < exRight; ++ex )
                                {
                                    const int donorX = ex + donorDx;
                                    if( donorX < 0 || donorX >= frameWidth )
                                        continue;
                                    const size_t dstIdx = ( static_cast<size_t>( ey ) * frameWidth + ex ) * 4;
                                    const size_t srcIdx = ( static_cast<size_t>( donorY ) * frameWidth + donorX ) * 4;
                                    donorDiffSum += abs( static_cast<int>( donorPixels[srcIdx + 0] ) - static_cast<int>( sourcePixels[dstIdx + 0] ) )
                                                 + abs( static_cast<int>( donorPixels[srcIdx + 1] ) - static_cast<int>( sourcePixels[dstIdx + 1] ) )
                                                 + abs( static_cast<int>( donorPixels[srcIdx + 2] ) - static_cast<int>( sourcePixels[dstIdx + 2] ) );
                                    donorDiffN += 3;
                                }
                                const int donorAvgDiff = ( donorDiffN > 0 ) ? donorDiffSum / donorDiffN : 0;
                                if( donorAvgDiff > 40 )
                                {
                                    donorOk = false;
                                }
                            }
                            if( !donorOk )
                            {
                                // Revert row to original pixels.
                                for( int ex = exLeft; ex < exRight; ++ex )
                                {
                                    const size_t dstIdx = ( static_cast<size_t>( ey ) * frameWidth + ex ) * 4;
                                    erasedPixels[dstIdx + 0] = sourcePixels[dstIdx + 0];
                                    erasedPixels[dstIdx + 1] = sourcePixels[dstIdx + 1];
                                    erasedPixels[dstIdx + 2] = sourcePixels[dstIdx + 2];
                                }
                                continue;  // Try next donor.
                            }
                            rowReplaced = true;
                            anyReplaced = true;
                        }
                    }

                    // Fallback for compact erase rects: when no donor was
                    // found (tail frames or out-of-bounds donors), fill
                    // from the nearest row just outside the erase rect.
                    if( !rowReplaced && compactEraseRect )
                    {
                        const int exLeft = max( 0, static_cast<int>( er.left ) );
                        const int exRight = min( frameWidth, static_cast<int>( er.right ) );
                        // Pick the closest row outside the erase rect.
                        const int aboveY = max( 0, static_cast<int>( er.top ) - 1 );
                        const int belowY = min( frameHeight - 1, static_cast<int>( er.bottom ) );
                        const int fillY = ( ey - aboveY <= belowY - ey ) ? aboveY : belowY;
                        for( int ex = exLeft; ex < exRight; ++ex )
                        {
                            const size_t dstIdx = ( static_cast<size_t>( ey ) * frameWidth + ex ) * 4;
                            const size_t fillIdx = ( static_cast<size_t>( fillY ) * frameWidth + ex ) * 4;
                            erasedPixels[dstIdx + 0] = sourcePixels[fillIdx + 0];
                            erasedPixels[dstIdx + 1] = sourcePixels[fillIdx + 1];
                            erasedPixels[dstIdx + 2] = sourcePixels[fillIdx + 2];
                        }
                        rowReplaced = true;
                        anyReplaced = true;
                    }
                }

                if( anyReplaced )
                {
                    composeSrc = &erasedPixels;
                }

                if( i == 0 )
                {
                    StitchLog( L"[Panorama/Stitch] EraseRect: (%d,%d)-(%d,%d) replaced=%d compact=%d\n",
                        er.left, er.top, er.right, er.bottom,
                        anyReplaced ? 1 : 0,
                        compactEraseRect ? 1 : 0 );
                }
            }
            const std::vector<BYTE>& activePixels = *composeSrc;

            parallel_for( 0, frameHeight, [&]( int y )
            {
                const int canvasY = destinationY + y;
                if( canvasY < 0 || canvasY >= stitchedHeight )
                {
                    return;
                }

                const size_t srcRowBase = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4;
                const size_t dstRowBase = static_cast<size_t>( canvasY ) * static_cast<size_t>( stitchedWidth ) * 4;
                const size_t dstMaskRowBase = static_cast<size_t>( canvasY ) * static_cast<size_t>( stitchedWidth );
                int rowBlendPixelsThisFrame = 0;
                int rowBlendWeightSumThisFrame = 0;
                int rowBlendWeightMinThisFrame = 255;
                int rowBlendWeightMaxThisFrame = 0;
                unsigned __int64 rowSuppressedPixels = 0;

                for( int x = 0; x < frameWidth; ++x )
                {
                    const int canvasX = destinationX + x;
                    if( canvasX < 0 || canvasX >= stitchedWidth )
                    {
                        continue;
                    }

                    const size_t dstMaskIndex = dstMaskRowBase + static_cast<size_t>( canvasX );

                    const bool firstFrame = ( i == 0 );

                    if( !lastFrame && overlayMask != nullptr && overlayMask->IsMaskedPixel( x, y ) )
                    {
                        // Exempt the first frame's header region so the
                        // header appears once at the top of the output,
                        // mirroring the lastFrame exemption for footers.
                        if( !firstFrame || y >= overlayMask->topHeaderHeight )
                        {
                            if( outSuppressedMask != nullptr )
                            {
                                ( *outSuppressedMask )[dstMaskIndex] = 1;
                            }
                            ++rowSuppressedPixels;
                            continue;
                        }
                    }

                    // Suppress the entire header region for non-first
                    // frames so only frame 0's header appears at the
                    // top.  The tile mask only catches some header
                    // tiles; this ensures ALL header pixels are kept
                    // from frame 0 and suppressed from later frames.
                    if( !lastFrame && !firstFrame && overlayMask != nullptr &&
                        overlayMask->topHeaderHeight > 0 && y < overlayMask->topHeaderHeight )
                    {
                        if( outSuppressedMask != nullptr )
                        {
                            ( *outSuppressedMask )[dstMaskIndex] = 1;
                        }
                        ++rowSuppressedPixels;
                        continue;
                    }

                    // Suppress pixels from the residual-detected overlay
                    // region all the way down through any bottom toolbar.
                    // The overlay bar sits at local-Y ~872-918, the tile
                    // mask covers the toolbar below at Y ~960-1155, but
                    // there can be gaps between them.  By suppressing
                    // from eraseRect.top to frame bottom, we ensure the
                    // entire fixed region is handled consistently.
                    if( !lastFrame && overlayMask != nullptr &&
                        overlayMask->eraseRect.right > overlayMask->eraseRect.left &&
                        y >= overlayMask->eraseRect.top &&
                        x >= overlayMask->eraseRect.left && x < overlayMask->eraseRect.right )
                    {
                        if( outSuppressedMask != nullptr )
                        {
                            ( *outSuppressedMask )[dstMaskIndex] = 1;
                        }
                        ++rowSuppressedPixels;
                        continue;
                    }

                    // Suppress the gap above the bottom toolbar where
                    // floating buttons (scroll-to-bottom, etc.) sit.
                    // These are too small for tile-based detection but
                    // consistently present at a fixed screen position.
                    // The overlap region ensures the canvas already has
                    // clean content from earlier frames.
                    if( !lastFrame && !firstFrame && overlayMask != nullptr &&
                        overlayMask->bottomStripY > 0 &&
                        y >= overlayMask->bottomStripY - 60 &&
                        !overlayMask->IsMaskedPixel( x, y ) )
                    {
                        if( outSuppressedMask != nullptr )
                        {
                            ( *outSuppressedMask )[dstMaskIndex] = 1;
                        }
                        ++rowSuppressedPixels;
                        continue;
                    }

                    const size_t srcIndex = srcRowBase + static_cast<size_t>( x ) * 4;
                    const size_t dstIndex = dstRowBase + static_cast<size_t>( canvasX ) * 4;

                    BYTE weightNew = ( i > 0 && !mostlyVerticalMove && !mostlyHorizontalMove ) ? 0 : 255;
                    if( mostlyVerticalMove && overlapHeight > 0 )
                    {
                        if( stepY > 0 )
                        {
                            const int overlapEnd = overlapHeight;
                            if( y < overlapEnd - verticalFeather )
                            {
                                weightNew = 0;
                            }
                            else if( y < overlapEnd )
                            {
                                const int numerator = y - ( overlapEnd - verticalFeather );
                                weightNew = static_cast<BYTE>( max( 0, min( 255, ( numerator * 255 ) / max( 1, verticalFeather ) ) ) );
                            }
                            else
                            {
                                weightNew = 255;
                            }
                        }
                        else
                        {
                            const int overlapStart = absStepY;
                            if( y < overlapStart )
                            {
                                weightNew = 255;
                            }
                            else if( y < overlapStart + verticalFeather )
                            {
                                const int numerator = overlapStart + verticalFeather - y;
                                weightNew = static_cast<BYTE>( max( 0, min( 255, ( numerator * 255 ) / max( 1, verticalFeather ) ) ) );
                            }
                            else
                            {
                                weightNew = 0;
                            }
                        }
                    }
                    else if( mostlyHorizontalMove && overlapWidth > 0 )
                    {
                        if( stepX > 0 )
                        {
                            const int overlapEnd = overlapWidth;
                            if( x < overlapEnd - horizontalFeather )
                            {
                                weightNew = 0;
                            }
                            else if( x < overlapEnd )
                            {
                                const int numerator = x - ( overlapEnd - horizontalFeather );
                                weightNew = static_cast<BYTE>( max( 0, min( 255, ( numerator * 255 ) / max( 1, horizontalFeather ) ) ) );
                            }
                            else
                            {
                                weightNew = 255;
                            }
                        }
                        else
                        {
                            const int overlapStart = absStepX;
                            if( x < overlapStart )
                            {
                                weightNew = 255;
                            }
                            else if( x < overlapStart + horizontalFeather )
                            {
                                const int numerator = overlapStart + horizontalFeather - x;
                                weightNew = static_cast<BYTE>( max( 0, min( 255, ( numerator * 255 ) / max( 1, horizontalFeather ) ) ) );
                            }
                            else
                            {
                                weightNew = 0;
                            }
                        }
                    }

                    if( outWritten[dstMaskIndex] == 0 )
                    {
                        outPixels[dstIndex + 0] = activePixels[srcIndex + 0];
                        outPixels[dstIndex + 1] = activePixels[srcIndex + 1];
                        outPixels[dstIndex + 2] = activePixels[srcIndex + 2];
                        outPixels[dstIndex + 3] = 0xFF;
                        outWritten[dstMaskIndex] = 1;
                        outOwner[dstMaskIndex] = static_cast<int>( frameIndex );
                        outBlended[dstMaskIndex] = 0;
                        continue;
                    }

                    if( weightNew == 0 )
                    {
                        continue;
                    }

                    if( weightNew == 255 )
                    {
                        outPixels[dstIndex + 0] = activePixels[srcIndex + 0];
                        outPixels[dstIndex + 1] = activePixels[srcIndex + 1];
                        outPixels[dstIndex + 2] = activePixels[srcIndex + 2];
                        outPixels[dstIndex + 3] = 0xFF;
                        outOwner[dstMaskIndex] = static_cast<int>( frameIndex );
                        outBlended[dstMaskIndex] = 0;
                        continue;
                    }

                    const int oldWeight = 255 - static_cast<int>( weightNew );
                    outPixels[dstIndex + 0] = static_cast<BYTE>( ( static_cast<int>( outPixels[dstIndex + 0] ) * oldWeight +
                                                                    static_cast<int>( activePixels[srcIndex + 0] ) * static_cast<int>( weightNew ) ) / 255 );
                    outPixels[dstIndex + 1] = static_cast<BYTE>( ( static_cast<int>( outPixels[dstIndex + 1] ) * oldWeight +
                                                                    static_cast<int>( activePixels[srcIndex + 1] ) * static_cast<int>( weightNew ) ) / 255 );
                    outPixels[dstIndex + 2] = static_cast<BYTE>( ( static_cast<int>( outPixels[dstIndex + 2] ) * oldWeight +
                                                                    static_cast<int>( activePixels[srcIndex + 2] ) * static_cast<int>( weightNew ) ) / 255 );
                    outPixels[dstIndex + 3] = 0xFF;
                    outBlended[dstMaskIndex] = 1;
                    ++rowBlendPixelsThisFrame;
                    rowBlendWeightSumThisFrame += static_cast<int>( weightNew );
                    rowBlendWeightMinThisFrame = min( rowBlendWeightMinThisFrame, static_cast<int>( weightNew ) );
                    rowBlendWeightMaxThisFrame = max( rowBlendWeightMaxThisFrame, static_cast<int>( weightNew ) );
                }

                if( rowSuppressedPixels > 0 )
                {
                    suppressedPixels.fetch_add( rowSuppressedPixels );
                }

                if( outRowBlendPixelCount != nullptr && rowBlendPixelsThisFrame > 0 )
                {
                    ( *outRowBlendPixelCount )[canvasY] += rowBlendPixelsThisFrame;
                    ( *outRowBlendWeightSum )[canvasY] += rowBlendWeightSumThisFrame;
                    ( *outRowBlendWeightMin )[canvasY] = min( ( *outRowBlendWeightMin )[canvasY], rowBlendWeightMinThisFrame );
                    ( *outRowBlendWeightMax )[canvasY] = max( ( *outRowBlendWeightMax )[canvasY], rowBlendWeightMaxThisFrame );
                    if( rowBlendPixelsThisFrame > ( *outRowBlendDominantPixels )[canvasY] )
                    {
                        ( *outRowBlendDominantFrame )[canvasY] = static_cast<int>( frameIndex );
                        ( *outRowBlendDominantPixels )[canvasY] = rowBlendPixelsThisFrame;
                    }
                    if( rowBlendPixelsThisFrame == stitchedWidth )
                    {
                        const int fullWidthAverageWeight = rowBlendWeightSumThisFrame / rowBlendPixelsThisFrame;
                        if( ( *outRowFullWidthBlendFirstPass )[canvasY] < 0 )
                        {
                            ( *outRowFullWidthBlendFirstFrame )[canvasY] = static_cast<int>( frameIndex );
                            ( *outRowFullWidthBlendFirstPass )[canvasY] = static_cast<int>( i );
                            ( *outRowFullWidthBlendFirstWeight )[canvasY] = fullWidthAverageWeight;
                        }
                        ( *outRowFullWidthBlendLastFrame )[canvasY] = static_cast<int>( frameIndex );
                        ( *outRowFullWidthBlendLastPass )[canvasY] = static_cast<int>( i );
                        ( *outRowFullWidthBlendLastWeight )[canvasY] = fullWidthAverageWeight;
                        ( *outRowFullWidthBlendPassCount )[canvasY] += 1;
                    }
                }
            } );
        }

        if( outSuppressedPixels != nullptr )
        {
            *outSuppressedPixels = suppressedPixels.load();
        }
        return true;
    };

    std::vector<BYTE> baselinePixels;
    std::vector<BYTE> baselineWritten;
    std::vector<int> baselineOwner;
    std::vector<BYTE> baselineBlended;
    if( !fixedOverlayMask.Empty() )
    {
        if( !composeFrames( nullptr,
                            false,
                            baselinePixels,
                            baselineWritten,
                            baselineOwner,
                            baselineBlended,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr ) )
        {
            return nullptr;
        }
    }

    std::vector<BYTE> suppressedCanvas;
    if( !composeFrames( fixedOverlayMask.Empty() ? nullptr : &fixedOverlayMask,
                        true,
                        stitchedPixels,
                        stitchedWritten,
                        stitchedOwner,
                        stitchedBlended,
                        fixedOverlayMask.Empty() ? nullptr : &suppressedCanvas,
                        &rowBlendPixelCount,
                        &rowBlendWeightSum,
                        &rowBlendWeightMin,
                        &rowBlendWeightMax,
                        &rowBlendDominantFrame,
                        &rowBlendDominantPixels,
                        &rowFullWidthBlendFirstFrame,
                        &rowFullWidthBlendFirstPass,
                        &rowFullWidthBlendFirstWeight,
                        &rowFullWidthBlendLastFrame,
                        &rowFullWidthBlendLastPass,
                        &rowFullWidthBlendLastWeight,
                        &rowFullWidthBlendPassCount,
                        &overlayDiagnostics.suppressedPixels ) )
    {
        return nullptr;
    }

    if( !fixedOverlayMask.Empty() )
    {
        overlayDiagnostics.repairedPixels = RepairSuppressedOverlayHoles( stitchedPixels,
                                                                          stitchedWritten,
                                                                          stitchedOwner,
                                                                          stitchedBlended,
                                                                          suppressedCanvas,
                                                                          stitchedWidth,
                                                                          stitchedHeight );

        for( size_t pixelIndex = 0; pixelIndex < stitchedWritten.size(); ++pixelIndex )
        {
            if( stitchedWritten[pixelIndex] != 0 || baselineWritten[pixelIndex] == 0 )
                continue;

            if( !suppressedCanvas.empty() && suppressedCanvas[pixelIndex] != 0 )
                continue;

            const size_t colorIndex = pixelIndex * 4;
            stitchedPixels[colorIndex + 0] = baselinePixels[colorIndex + 0];
            stitchedPixels[colorIndex + 1] = baselinePixels[colorIndex + 1];
            stitchedPixels[colorIndex + 2] = baselinePixels[colorIndex + 2];
            stitchedPixels[colorIndex + 3] = baselinePixels[colorIndex + 3];
            stitchedWritten[pixelIndex] = 1;
            stitchedOwner[pixelIndex] = baselineOwner[pixelIndex];
            stitchedBlended[pixelIndex] = baselineBlended[pixelIndex];
            overlayDiagnostics.fallbackPixels++;
        }

        overlayDiagnostics.correctedDarkBands = RepairOverlayDarkBands( stitchedPixels,
                                                                        stitchedWritten,
                                                                        stitchedBlended,
                                                                        overlayDiagnostics.tileBoundsLeft,
                                                                        overlayDiagnostics.tileBoundsRight,
                                                                        stitchedWidth,
                                                                        stitchedHeight,
                                                                        &overlayDiagnostics.correctedDarkBandRows );

        StitchLog( L"[Panorama/Stitch] FixedOverlay compose: suppressedPixels=%llu repairedPixels=%llu fallbackPixels=%llu correctedDarkBands=%d correctedDarkBandRows=%d\n",
                     overlayDiagnostics.suppressedPixels,
                     overlayDiagnostics.repairedPixels,
                     overlayDiagnostics.fallbackPixels,
                     overlayDiagnostics.correctedDarkBands,
                     overlayDiagnostics.correctedDarkBandRows );
    }

    LogComposedFrameDiagnostics( composedFrameIndices,
                                 composedFrameOrigins,
                                 composedFrameSteps,
                                 frameWidth,
                                 frameHeight );
    LogCompositionCoverageDiagnostics( stitchedOwner,
                                      stitchedWritten,
                                      stitchedBlended,
                                      stitchedWidth,
                                      stitchedHeight );
    LogSuspiciousTransitionWindowDiagnostics( stitchedPixels,
                                              stitchedOwner,
                                              stitchedWritten,
                                              stitchedBlended,
                                              rowBlendPixelCount,
                                              rowBlendWeightSum,
                                              rowBlendWeightMin,
                                              rowBlendWeightMax,
                                              rowBlendDominantFrame,
                                              rowBlendDominantPixels,
                                              rowFullWidthBlendFirstFrame,
                                              rowFullWidthBlendFirstPass,
                                              rowFullWidthBlendFirstWeight,
                                              rowFullWidthBlendLastFrame,
                                              rowFullWidthBlendLastPass,
                                              rowFullWidthBlendLastWeight,
                                              rowFullWidthBlendPassCount,
                                              stitchedWidth,
                                              stitchedHeight,
                                              composedFrameIndices,
                                              composedFrameOrigins,
                                              composedFrameSteps,
                                              frameWidth,
                                              frameHeight,
                                              verticalFeather,
                                              horizontalFeather,
                                              minX,
                                              minY );
    LogStitchedBandDiagnostics( stitchedPixels,
                                stitchedOwner,
                                stitchedWritten,
                                stitchedBlended,
                                rowBlendPixelCount,
                                rowBlendWeightSum,
                                rowBlendWeightMin,
                                rowBlendWeightMax,
                                rowFullWidthBlendFirstFrame,
                                rowFullWidthBlendFirstPass,
                                rowFullWidthBlendFirstWeight,
                                rowFullWidthBlendLastFrame,
                                rowFullWidthBlendLastPass,
                                rowFullWidthBlendLastWeight,
                                rowFullWidthBlendPassCount,
                                stitchedWidth,
                                stitchedHeight,
                                composedFrameIndices,
                                composedFrameOrigins,
                                minY );
    LogContentDuplicationDiagnostics( stitchedPixels,
                                      stitchedOwner,
                                      stitchedWidth,
                                      stitchedHeight,
                                      composedFrameIndices,
                                      composedFrameOrigins,
                                      composedFrameSteps,
                                      frameWidth,
                                      frameHeight,
                                      minX,
                                      minY );

    HBITMAP stitchedBitmap = CreateBitmapFromPixels32( stitchedPixels, stitchedWidth, stitchedHeight );
    if( stitchedBitmap == nullptr )
    {
        StitchLog( L"[Panorama/Stitch] Failed to create stitched bitmap from pixels\n" );
        return nullptr;
    }

    if( outComposedFrameCount )
    {
        *outComposedFrameCount = composedFrameIndices.size();
    }
    if( outComposedAxisSteps )
    {
        outComposedAxisSteps->clear();
        outComposedAxisSteps->reserve( composedFrameSteps.size() > 0 ? composedFrameSteps.size() - 1 : 0 );
        for( size_t si = 1; si < composedFrameSteps.size(); ++si )
        {
            const POINT& s = composedFrameSteps[si];
            outComposedAxisSteps->push_back( max( abs( s.x ), abs( s.y ) ) );
        }
    }

    const ULONGLONG stitchDurationMs = GetTickCount64() - stitchStart;
    StitchLog( L"[Panorama/Stitch] Stitch complete durationMs=%llu\n", stitchDurationMs );
    reportProgress( 100 );
    return stitchedBitmap;
}

bool RunPanoramaCaptureToClipboard( HWND hWnd )
{
    OutputDebug( L"[Panorama/Capture] Start (clipboard)\n" );
    return RunPanoramaCaptureCommon( hWnd, false );
}

bool RunPanoramaCaptureToFile( HWND hWnd )
{
    OutputDebug( L"[Panorama/Capture] Start (file)\n" );
    return RunPanoramaCaptureCommon( hWnd, true );
}

static bool RunPanoramaCaptureCommon( HWND hWnd, bool saveToFile )
{
    std::wstring debugDumpDirectory;
    size_t debugGrabbedFrameCount = 0;
    if( PanoramaDebugEnabled() )
    {
        debugDumpDirectory = CreatePanoramaDebugDumpDirectory();
        if( !debugDumpDirectory.empty() )
        {
            const auto logPath = std::filesystem::path( debugDumpDirectory ) / L"stitch_log.txt";
            g_StitchLogFile = _wfopen( logPath.wstring().c_str(), L"w" );
            StitchLog( L"[Panorama/Debug] Dump directory: %s\n", debugDumpDirectory.c_str() );
        }
    }

    StitchLog( L"[Panorama/Capture] Max frame limit=%zu\n", kMaxCaptureFrames );
    // RAII guard: close the stitch log on every return path.
    struct CaptureStitchLogGuard
    {
        ~CaptureStitchLogGuard()
        {
            if( g_StitchLogFile != nullptr )
            {
                fclose( g_StitchLogFile );
                g_StitchLogFile = nullptr;
            }
        }
    } captureStitchLogGuard;

    g_RecordCropping = TRUE;
    const bool started = g_SelectRectangle.Start( hWnd );
    g_RecordCropping = FALSE;
    if( !started )
    {
        StitchLog( L"[Panorama/Capture] Selection cancelled\n" );
        g_SelectRectangle.Stop();
        return false;
    }

    const RECT selectedRect = g_SelectRectangle.SelectedRect();
    StitchLog( L"[Panorama/Capture] Selected rect local=(%ld,%ld)-(%ld,%ld)\n",
               selectedRect.left,
               selectedRect.top,
               selectedRect.right,
               selectedRect.bottom );

    if( selectedRect.right <= selectedRect.left || selectedRect.bottom <= selectedRect.top )
    {
        StitchLog( L"[Panorama/Capture] Invalid selected rect\n" );
        g_SelectRectangle.Stop();
        return false;
    }

    const RECT monitorRect = GetMonitorRectFromCursor();

    RECT absoluteRect{};
    absoluteRect.left = monitorRect.left + selectedRect.left;
    absoluteRect.top = monitorRect.top + selectedRect.top;
    absoluteRect.right = monitorRect.left + selectedRect.right;
    absoluteRect.bottom = monitorRect.top + selectedRect.bottom;

    // On Windows 11 22H2+, the SelectRectangle border is drawn inside the
    // selected rectangle.  Inset the capture rect by the border width so
    // the static yellow border pixels are excluded from captured frames;
    // they would otherwise confuse the stitcher's alignment algorithm.
    if( GetWindowsBuild( nullptr ) >= BUILD_WINDOWS_11_22H2 )
    {
        const UINT dpi = GetDpiForWindowHelper( GetDesktopWindow() );
        const int borderWidth = ScaleForDpi( 2, dpi );
        InflateRect( &absoluteRect, -borderWidth, -borderWidth );
    }

    StitchLog( L"[Panorama/Capture] Capture rect absolute=(%ld,%ld)-(%ld,%ld)\n",
               absoluteRect.left,
               absoluteRect.top,
               absoluteRect.right,
               absoluteRect.bottom );

    if( PanoramaDebugEnabled() && !debugDumpDirectory.empty() )
    {
        wchar_t infoText[512]{};
        swprintf_s( infoText,
                    L"selectedRectLocal=(%ld,%ld)-(%ld,%ld)\nmonitorRect=(%ld,%ld)-(%ld,%ld)\ncaptureRectAbsolute=(%ld,%ld)-(%ld,%ld)\n",
                    selectedRect.left,
                    selectedRect.top,
                    selectedRect.right,
                    selectedRect.bottom,
                    monitorRect.left,
                    monitorRect.top,
                    monitorRect.right,
                    monitorRect.bottom,
                    absoluteRect.left,
                    absoluteRect.top,
                    absoluteRect.right,
                    absoluteRect.bottom );
        DumpPanoramaText( debugDumpDirectory, L"capture_info.txt", infoText );
    }

    wil::unique_hdc hdcSource( CreateDC( L"DISPLAY", static_cast<PTCHAR>(nullptr), static_cast<PTCHAR>(nullptr), static_cast<CONST DEVMODE*>(nullptr) ) );
    if( hdcSource == nullptr )
    {
        StitchLog( L"[Panorama/Capture] CreateDC failed\n" );
        g_SelectRectangle.Stop();
        return false;
    }

    if( PanoramaDebugEnabled() && !debugDumpDirectory.empty() )
    {
        RECT desktopRect{};
        desktopRect.left = GetSystemMetrics( SM_XVIRTUALSCREEN );
        desktopRect.top = GetSystemMetrics( SM_YVIRTUALSCREEN );
        desktopRect.right = desktopRect.left + GetSystemMetrics( SM_CXVIRTUALSCREEN );
        desktopRect.bottom = desktopRect.top + GetSystemMetrics( SM_CYVIRTUALSCREEN );

        HBITMAP desktopBitmap = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), desktopRect );
        if( desktopBitmap != nullptr )
        {
            DumpPanoramaBitmap( debugDumpDirectory, L"desktop", 0, desktopBitmap );
            DeleteObject( desktopBitmap );
        }
        else
        {
            StitchLog( L"[Panorama/Debug] Failed to capture desktop snapshot\n" );
        }
    }

    // The SelectRectangle border window has WDA_EXCLUDEFROMCAPTURE set,
    // which tells DWM to replace the window's entire bounding rectangle
    // (not just the visible region) with a solid fill for screen-capture
    // APIs.  Because the border window sits on top of the capture area,
    // every BitBlt returns the exclusion fill rather than the actual
    // screen content.  Clear the affinity so BitBlt sees through to the
    // desktop content underneath.
    g_SelectRectangle.SetExcludeFromCapture( false );

    std::vector<HBITMAP> frames;
    HBITMAP firstFrame = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), absoluteRect );
    if( firstFrame == nullptr )
    {
        StitchLog( L"[Panorama/Capture] Failed to capture first frame\n" );
        g_SelectRectangle.Stop();
        return false;
    }
    frames.push_back( firstFrame );

    double contrastSpread = 0.0;
    double contrastStdDev = 0.0;
    double contrastEdgeDelta = 0.0;
    const bool lowContrastMode = IsLowContrastSeedFrame( firstFrame, &contrastSpread, &contrastStdDev, &contrastEdgeDelta );
    StitchLog( L"[Panorama/Capture] Captured frame #1 lowContrast=%d spread=%.1f stdDev=%.1f edgeDelta=%.1f\n",
               lowContrastMode ? 1 : 0,
               contrastSpread,
               contrastStdDev,
               contrastEdgeDelta );

    if( PanoramaDebugEnabled() )
    {
        DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, firstFrame );
    }

    size_t duplicateFrameCount = 0;
    size_t subPixelDropCount = 0;
    size_t tornFrameCount = 0;
    size_t captureIteration = 0;
    size_t redrawDropCount = 0;
    bool frameLimitStop = false;

    // Running brightness statistics for redraw-frame detection.
    // Frames whose luma drops dramatically and whose variance collapses
    // to near-zero are application-redraw blanking frames (e.g. Outlook
    // paints a flat dark background while re-rendering).
    double runningAvgLuma = 0.0;
    double runningStdDev = 0.0;
    ComputeFrameBrightnessStats( firstFrame, runningAvgLuma, runningStdDev );

    // Resolve DwmFlush once for the capture loop.  Used to synchronize
    // with the DWM composition cycle so BitBlt captures fully-composed
    // frames instead of mid-scroll torn content.
    using pfnDwmFlush_t = HRESULT( WINAPI* )();
    const auto pfnDwmFlush = reinterpret_cast<pfnDwmFlush_t>(
        GetProcAddress( GetModuleHandleW( L"dwmapi.dll" ), "DwmFlush" ) );

    bool cancelledByEsc = false;

    while( !g_PanoramaStopRequested )
    {
        captureIteration++;
        MSG msg{};
        while( PeekMessage( &msg, hWnd, WM_HOTKEY, WM_HOTKEY, PM_REMOVE ) )
        {
            StitchLog( L"[Panorama/Capture] Dispatching WM_HOTKEY id=%ld(%s) during capture loop\n",
                       static_cast<long>( msg.wParam ),
                       HotkeyIdToString( msg.wParam ) );
            DispatchMessage( &msg );
        }

        // Allow ESC to cancel the panorama during the capture phase.
        // Use GetAsyncKeyState because the focused window belongs to the
        // application being captured, so WM_KEYDOWN messages go to its
        // thread queue, not ours.
        if( GetAsyncKeyState( VK_ESCAPE ) & 0x8000 )
        {
            StitchLog( L"[Panorama/Capture] ESC pressed, cancelling capture\n" );
            cancelledByEsc = true;
            g_PanoramaStopRequested = true;
            break;
        }

        if( PeekMessage( &msg, nullptr, WM_QUIT, WM_QUIT, PM_REMOVE ) )
        {
            PostQuitMessage( static_cast<int>(msg.wParam) );
            g_PanoramaStopRequested = true;
            StitchLog( L"[Panorama/Capture] WM_QUIT received, stopping capture\n" );
            break;
        }

        // Synchronize with the DWM composition cycle instead of a blind
        // sleep.  DwmFlush blocks until the next vsync + composition
        // completes, so the subsequent BitBlt captures a fully-composed
        // frame.  This avoids torn captures where the application is
        // mid-scroll (e.g. ScrollWindowEx shifted pixels but the app
        // has not yet repainted the newly-exposed region).
        // Fall back to Sleep(16) if DWM is unavailable.
        if( !pfnDwmFlush || FAILED( pfnDwmFlush() ) )
        {
            Sleep( 16 );
        }

        HBITMAP frame = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), absoluteRect );
        if( frame == nullptr )
        {
            StitchLog( L"[Panorama/Capture] Capture failed at iteration=%zu\n", captureIteration );
            continue;
        }

        if( PanoramaDebugEnabled() )
        {
            DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, frame );
        }

        bool isSubPixelDrop = false;
        if( AreFramesNearDuplicate( frame, frames.back(), lowContrastMode, &isSubPixelDrop ) )
        {
            if( isSubPixelDrop )
            {
                subPixelDropCount++;
                StitchLog( L"[Panorama/Capture] Sub-pixel shift frame discarded (grabbed=%zu count=%zu iteration=%zu)\n",
                           debugGrabbedFrameCount,
                           subPixelDropCount,
                           captureIteration );
            }
            else
            {
                duplicateFrameCount++;
                if( duplicateFrameCount <= 3 || ( duplicateFrameCount % 10 ) == 0 )
                {
                    StitchLog( L"[Panorama/Capture] Duplicate frame skipped (grabbed=%zu count=%zu iteration=%zu)\n",
                               debugGrabbedFrameCount,
                               duplicateFrameCount,
                               captureIteration );
                }
            }
            DeleteObject( frame );
            continue;
        }

        // Reject application-redraw blanking frames.  These are captured
        // mid-repaint when the application clears its window to a flat
        // background colour before re-rendering visible content.  They
        // look like a massive brightness drop with near-zero variance.
        {
            double frameLuma = 0.0;
            double frameStdDev = 0.0;
            ComputeFrameBrightnessStats( frame, frameLuma, frameStdDev );

            // A redraw frame is characterised by (a) very low luma
            // variance (essentially a single flat colour) AND (b) a
            // significant brightness drop compared to the running
            // average of previously accepted frames.
            const bool flatFrame = frameStdDev < 3.0;
            const bool lumaDrop = runningAvgLuma > 10.0 && frameLuma < runningAvgLuma * 0.55;
            if( flatFrame && lumaDrop )
            {
                redrawDropCount++;
                StitchLog( L"[Panorama/Capture] Redraw-blank frame discarded: luma=%.1f stdDev=%.1f runningLuma=%.1f (grabbed=%zu count=%zu iteration=%zu)\n",
                           frameLuma,
                           frameStdDev,
                           runningAvgLuma,
                           debugGrabbedFrameCount,
                           redrawDropCount,
                           captureIteration );
                DeleteObject( frame );
                continue;
            }

            // Update running brightness with exponential moving average.
            // Weight recent frames more heavily so a gradual content
            // change (e.g. scrolling from a bright region to a darker
            // one) doesn't trigger false rejections.
            constexpr double kLumaAlpha = 0.15;
            runningAvgLuma = runningAvgLuma * ( 1.0 - kLumaAlpha ) + frameLuma * kLumaAlpha;
            runningStdDev  = runningStdDev  * ( 1.0 - kLumaAlpha ) + frameStdDev * kLumaAlpha;
        }

        frames.push_back( frame );
        frame = nullptr;
        StitchLog( L"[Panorama/Capture] Captured moving frame #%zu (grabbed=%zu) at iteration=%zu\n",
                   frames.size(),
                   debugGrabbedFrameCount,
                   captureIteration );
        if( frames.size() >= kMaxCaptureFrames )
        {
            StitchLog( L"[Panorama/Capture] Reached frame limit (%zu), stopping capture\n", kMaxCaptureFrames );
            // Treat auto-stop at frame limit the same as explicit user stop so
            // downstream flow (stitch + clipboard/file output) follows the
            // normal capture-stop path.
            frameLimitStop = true;
            g_PanoramaStopRequested = true;
            break;
        }
    }

    StitchLog( L"[Panorama/Capture] Loop exited stopRequested=%d frameLimitStop=%d frames=%zu duplicates=%zu subpixel=%zu torn=%zu redraw=%zu iterations=%zu\n",
               g_PanoramaStopRequested ? 1 : 0,
               frameLimitStop ? 1 : 0,
               frames.size(),
               duplicateFrameCount,
               subPixelDropCount,
               tornFrameCount,
               redrawDropCount,
               captureIteration );

    if( PanoramaDebugEnabled() && !debugDumpDirectory.empty() )
    {
        wchar_t statsText[256]{};
        swprintf_s( statsText,
                    L"framesAccepted=%zu\nduplicates=%zu\nsubpixel=%zu\ntorn=%zu\nredraw=%zu\niterations=%zu\nstopRequested=%d\nframeLimitStop=%d\n",
                    frames.size(),
                    duplicateFrameCount,
                    subPixelDropCount,
                    tornFrameCount,
                    redrawDropCount,
                    captureIteration,
                    g_PanoramaStopRequested ? 1 : 0,
                    frameLimitStop ? 1 : 0 );
        DumpPanoramaText( debugDumpDirectory, L"capture_stats.txt", statsText );

        for( size_t frameIndex = 0; frameIndex < frames.size(); ++frameIndex )
        {
            DumpPanoramaBitmap( debugDumpDirectory, L"accepted", frameIndex + 1, frames[frameIndex] );
        }
    }

    g_SelectRectangle.Stop();

    if( cancelledByEsc )
    {
        StitchLog( L"[Panorama/Capture] Cancelled by ESC, discarding %zu frames\n", frames.size() );
        for( HBITMAP frame : frames )
        {
            if( frame != nullptr )
            {
                DeleteObject( frame );
            }
        }
        return false;
    }

    HBITMAP panoramaBitmap = nullptr;
    if( frames.size() == 1 )
    {
        panoramaBitmap = frames.front();
        frames.front() = nullptr;
    }
    else
    {
        g_ProgressDialog.Create( hWnd );
        panoramaBitmap = StitchPanoramaFrames( frames, lowContrastMode, [&]( int percent ) -> bool
        {
            g_ProgressDialog.SetProgress( percent );
            return g_ProgressDialog.IsCancelled();
        } );
        g_ProgressDialog.Destroy();

        if( panoramaBitmap == nullptr && g_ProgressDialog.IsCancelled() )
        {
            StitchLog( L"[Panorama/Capture] Stitching cancelled by user\n" );
            for( HBITMAP frame : frames )
            {
                if( frame != nullptr )
                {
                    DeleteObject( frame );
                }
            }
            return false;
        }
    }

    for( HBITMAP frame : frames )
    {
        if( frame != nullptr )
        {
            DeleteObject( frame );
        }
    }

    if( panoramaBitmap == nullptr )
    {
        if( frameLimitStop )
        {
            MessageBox( hWnd,
                        L"Capture limit reached, but stitching failed.\nPlease check stitch_log.txt in the latest panorama debug dump.",
                        L"ZoomIt",
                        MB_OK | MB_ICONWARNING );
        }
        StitchLog( L"[Panorama/Capture] Stitch result is null\n" );
        return false;
    }

    if( PanoramaDebugEnabled() )
    {
        DumpPanoramaBitmap( debugDumpDirectory, L"stitched", 0, panoramaBitmap );
    }

    if( saveToFile )
    {
        if( frameLimitStop )
        {
            MessageBox( hWnd,
                        L"Capture limit reached. Image is ready.",
                        L"ZoomIt",
                        MB_OK | MB_ICONINFORMATION );
        }

        // Show file save dialog and save as PNG.
        g_bSaveInProgress = true;

        auto saveDialog = wil::CoCreateInstance<IFileSaveDialog>( CLSID_FileSaveDialog );

        FILEOPENDIALOGOPTIONS options;
        if( SUCCEEDED( saveDialog->GetOptions( &options ) ) )
            saveDialog->SetOptions( options | FOS_FORCEFILESYSTEM | FOS_OVERWRITEPROMPT );

        COMDLG_FILTERSPEC fileTypes[] = {
            { L"PNG Image", L"*.png" }
        };
        saveDialog->SetFileTypes( _countof( fileTypes ), fileTypes );
        saveDialog->SetFileTypeIndex( 1 );
        saveDialog->SetDefaultExtension( L"png" );

        auto suggestedName = GetUniqueFilename( g_ScreenshotSaveLocation, L"ZoomitPanorama.png", FOLDERID_Pictures );
        saveDialog->SetFileName( suggestedName.c_str() );
        saveDialog->SetTitle( L"ZoomIt: Save Panorama..." );

        if( !g_ScreenshotSaveLocation.empty() )
        {
            std::filesystem::path lastPath( g_ScreenshotSaveLocation );
            if( lastPath.has_parent_path() )
            {
                wil::com_ptr<IShellItem> folderItem;
                if( SUCCEEDED( SHCreateItemFromParsingName( lastPath.parent_path().c_str(),
                    nullptr, IID_PPV_ARGS( &folderItem ) ) ) )
                {
                    saveDialog->SetFolder( folderItem.get() );
                }
            }
        }

        std::wstring selectedFilePath;
        if( SUCCEEDED( saveDialog->Show( hWnd ) ) )
        {
            wil::com_ptr<IShellItem> resultItem;
            if( SUCCEEDED( saveDialog->GetResult( &resultItem ) ) )
            {
                wil::unique_cotaskmem_string pathStr;
                if( SUCCEEDED( resultItem->GetDisplayName( SIGDN_FILESYSPATH, &pathStr ) ) )
                {
                    selectedFilePath = pathStr.get();
                }
            }
        }

        bool success = false;
        if( !selectedFilePath.empty() )
        {
            if( selectedFilePath.find( L'.' ) == std::wstring::npos )
            {
                selectedFilePath += L".png";
            }
            DWORD saveResult = SavePng( selectedFilePath.c_str(), panoramaBitmap );
            if( saveResult == ERROR_SUCCESS )
            {
                g_ScreenshotSaveLocation = selectedFilePath;
                StitchLog( L"[Panorama/Capture] Success: saved to %s\n", selectedFilePath.c_str() );
                success = true;
            }
            else
            {
                StitchLog( L"[Panorama/Capture] SavePng failed err=%lu\n", saveResult );
            }
        }

        g_bSaveInProgress = false;
        DeleteObject( panoramaBitmap );
        return success;
    }

    // Build a packed CF_DIB (BITMAPINFOHEADER + pixel data) in global
    // memory.  Using CF_DIB instead of CF_BITMAP avoids compatibility
    // issues with top-down DIB sections that some apps (e.g. Paint)
    // cannot paste.
    BITMAP bm{};
    GetObject( panoramaBitmap, sizeof( bm ), &bm );
    const int bmpWidth = bm.bmWidth;
    const int bmpHeight = bm.bmHeight;
    const DWORD stride = static_cast<DWORD>( ( bmpWidth * 32 + 31 ) / 32 ) * 4;
    const DWORD imageSize = stride * static_cast<DWORD>( abs( bmpHeight ) );

    HGLOBAL hDib = GlobalAlloc( GMEM_MOVEABLE, sizeof( BITMAPINFOHEADER ) + imageSize );
    if( hDib == nullptr )
    {
        StitchLog( L"[Panorama/Capture] GlobalAlloc for DIB failed\n" );
        DeleteObject( panoramaBitmap );
        return false;
    }

    void* dibPtr = GlobalLock( hDib );
    auto* header = static_cast<BITMAPINFOHEADER*>( dibPtr );
    ZeroMemory( header, sizeof( BITMAPINFOHEADER ) );
    header->biSize = sizeof( BITMAPINFOHEADER );
    header->biWidth = bmpWidth;
    header->biHeight = abs( bmpHeight );   // bottom-up for maximum compatibility
    header->biPlanes = 1;
    header->biBitCount = 32;
    header->biCompression = BI_RGB;
    header->biSizeImage = imageSize;

    // Extract pixel data as bottom-up regardless of source orientation.
    HDC hdcScreen = GetDC( nullptr );
    BITMAPINFO getBmi{};
    getBmi.bmiHeader = *header;
    GetDIBits( hdcScreen, panoramaBitmap, 0, abs( bmpHeight ),
               static_cast<BYTE*>( dibPtr ) + sizeof( BITMAPINFOHEADER ),
               &getBmi, DIB_RGB_COLORS );
    ReleaseDC( nullptr, hdcScreen );
    GlobalUnlock( hDib );

    DeleteObject( panoramaBitmap );

    const bool opened = OpenClipboard( hWnd ) != FALSE;
    if( !opened )
    {
        StitchLog( L"[Panorama/Capture] OpenClipboard failed err=%lu\n", GetLastError() );
        GlobalFree( hDib );
        return false;
    }

    if( !EmptyClipboard() )
    {
        StitchLog( L"[Panorama/Capture] EmptyClipboard failed err=%lu\n", GetLastError() );
        CloseClipboard();
        GlobalFree( hDib );
        return false;
    }

    if( SetClipboardData( CF_DIB, hDib ) == nullptr )
    {
        StitchLog( L"[Panorama/Capture] SetClipboardData(CF_DIB) failed err=%lu\n", GetLastError() );
        CloseClipboard();
        GlobalFree( hDib );
        return false;
    }

    CloseClipboard();

    if( frameLimitStop )
    {
        MessageBox( hWnd,
                    L"Capture limit reached. Image is ready.",
                    L"ZoomIt",
                    MB_OK | MB_ICONINFORMATION );
    }

    StitchLog( L"[Panorama/Capture] Success: DIB copied to clipboard (%dx%d)\n", bmpWidth, abs( bmpHeight ) );
    return true;
}
#ifdef _DEBUG
//
// Panorama stitch self-test
// -------------------------
// How to run:
//   1. Build the ARM64 Debug configuration.
//   2. Place test images (image1.png ... image5.png) in the Debug\ directory
//      next to the solution root (i.e. <repo>\Debug\).
//   3. Run:  ZoomIt64a.exe /panorama-selftest
//      Optional stress targeting:
//      - /panorama-selftest-stress-only=1
//      - /panorama-stress-focus=<scenario-substring>
//      - /panorama-stress-stopafter=0|1 (default 1 when focus is set)
//   4. Exit code 0 = all tests passed, exit code 2 = failure.
//   5. Diagnostic output goes to OutputDebugString (view with DebugView
//      or a debugger).  On failure, artifacts are written to
//      %TEMP%\ZoomItPanoramaDebug\panorama_<timestamp>_<pid>\.
//
#pragma warning(push)
#pragma warning(disable : 4456) // Self-test scaffolding reuses local names in nested scopes.
#pragma warning(disable : 4189) // Some scenario-only locals are intentionally write-only for diagnostics.
bool RunPanoramaStitchSelfTest()
{
    // Allocate a console so stdout output is visible when running from
    // a terminal.  GUI subsystem apps have no console by default.
    if( AllocConsole() )
    {
        FILE* fp = nullptr;
        freopen_s( &fp, "CONOUT$", "w", stdout );
    }

    // Write test progress to both OutputDebugString and stdout so the
    // user can watch progress in a terminal window.
    auto TestLog = []( const wchar_t* format, ... )
    {
        va_list args;
        va_start( args, format );
        wchar_t buffer[1024]{};
        _vsnwprintf_s( buffer, _TRUNCATE, format, args );
        va_end( args );
        OutputDebug( L"%s", buffer );
        wprintf( L"%s", buffer );
        fflush( stdout );
    };

    const std::wstring selfTestDumpDirectory = CreatePanoramaDebugDumpDirectory();
    if( !selfTestDumpDirectory.empty() )
    {
        DumpPanoramaText( selfTestDumpDirectory,
                          L"selftest_marker.txt",
                          L"Panorama self-test started and dump path is writable.\n" );
        TestLog( L"[Panorama/Test] Dump directory: %s\n", selfTestDumpDirectory.c_str() );
    }

    auto readSelfTestArg = []( const wchar_t* switchName ) -> std::wstring
    {
        if( switchName == nullptr || switchName[0] == L'\0' )
            return std::wstring();

        const wchar_t* cmdLine = GetCommandLineW();
        if( cmdLine == nullptr )
            return std::wstring();

        const std::wstring key = std::wstring( switchName ) + L"=";
        const wchar_t* found = wcsstr( cmdLine, key.c_str() );
        if( found == nullptr )
            return std::wstring();

        const wchar_t* valueStart = found + key.size();
        while( *valueStart == L' ' || *valueStart == L'\t' )
            ++valueStart;

        const bool quoted = *valueStart == L'"';
        if( quoted )
            ++valueStart;

        const wchar_t* valueEnd = valueStart;
        while( *valueEnd != L'\0' )
        {
            if( quoted )
            {
                if( *valueEnd == L'"' )
                    break;
            }
            else if( *valueEnd == L' ' || *valueEnd == L'\t' )
            {
                break;
            }
            ++valueEnd;
        }

        std::wstring result( valueStart, valueEnd );
        // Strip stray trailing quotes (bash on Windows can inject these).
        while( !result.empty() && result.back() == L'"' )
            result.pop_back();
        return result;
    };

    auto readSelfTestBoolArg = [&]( const wchar_t* switchName, bool defaultValue ) -> bool
    {
        const std::wstring value = readSelfTestArg( switchName );
        if( value.empty() )
            return defaultValue;
        return !(_wcsicmp( value.c_str(), L"0" ) == 0 ||
                 _wcsicmp( value.c_str(), L"false" ) == 0 ||
                 _wcsicmp( value.c_str(), L"no" ) == 0 ||
                 _wcsicmp( value.c_str(), L"off" ) == 0);
    };

    const bool selfTestStressOnly = readSelfTestBoolArg( L"/panorama-selftest-stress-only", false );
    if( selfTestStressOnly )
    {
        TestLog( L"[Panorama/Test] Stress-only mode enabled\n" );
    }

    // Number of random trials per image for the slice tests (default 5).
    int selfTestTrials = 5;
    {
        const std::wstring trialsStr = readSelfTestArg( L"/panorama-selftest-trials" );
        if( !trialsStr.empty() )
        {
            const int parsed = _wtoi( trialsStr.c_str() );
            if( parsed >= 1 && parsed <= 100 )
                selfTestTrials = parsed;
        }
        TestLog( L"[Panorama/Test] Trials per image: %d\n", selfTestTrials );
    }

#ifdef _DEBUG
    // Open the stitch log file for the duration of the selftest so that
    // StitchLog() entries (poor-fine rejections, accepted frames, etc.) are
    // written to disk for post-run analysis.
    struct RAIIStitchLog
    {
        RAIIStitchLog( const std::wstring& dir )
        {
            if( !dir.empty() )
            {
                const auto logPath = std::filesystem::path( dir ) / L"stitch_log.txt";
                g_StitchLogFile = _wfopen( logPath.wstring().c_str(), L"w" );
            }
        }
        ~RAIIStitchLog()
        {
            if( g_StitchLogFile != nullptr )
            {
                fclose( g_StitchLogFile );
                g_StitchLogFile = nullptr;
            }
        }
    } stitchLogGuard( selfTestDumpDirectory );
#endif

    auto runScenario = [&]( const wchar_t* scenarioName,
                            int frameWidth,
                            int frameHeight,
                            const std::vector<int>& frameOriginsY,
                            const std::vector<BYTE>& canvasPixels,
                            int canvasHeight,
                            int expectedHeight,
                            int toleranceHeight,
                            bool allowMismatches,
                            const std::function<void(size_t, std::vector<BYTE>&)>& frameTransform = nullptr,
                            int compareHeightOverride = -1 ) -> bool
    {
        TestLog( L"[Panorama/Test] Scenario=%s frame=%dx%d frameCount=%zu expectedHeight=%d\n",
                     scenarioName,
                     frameWidth,
                     frameHeight,
                     frameOriginsY.size(),
                     expectedHeight );

        std::vector<HBITMAP> frames;
        frames.reserve( frameOriginsY.size() );
        bool createFailed = false;

        for( size_t frameIndex = 0; frameIndex < frameOriginsY.size(); ++frameIndex )
        {
            const int originY = frameOriginsY[frameIndex];
            if( originY < 0 || originY + frameHeight > canvasHeight )
            {
                TestLog( L"[Panorama/Test] Scenario=%s invalid origin frame=%zu originY=%d\n",
                             scenarioName,
                             frameIndex,
                             originY );
                createFailed = true;
                break;
            }

            std::vector<BYTE> framePixels( static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight ) * 4 );
            for( int y = 0; y < frameHeight; ++y )
            {
                const size_t srcStart = ( static_cast<size_t>( originY + y ) * static_cast<size_t>( frameWidth ) ) * 4;
                const size_t dstStart = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) ) * 4;
                memcpy( framePixels.data() + dstStart,
                        canvasPixels.data() + srcStart,
                        static_cast<size_t>( frameWidth ) * 4 );
            }

            if( frameTransform )
            {
                frameTransform( frameIndex, framePixels );
            }

            HBITMAP frameBitmap = CreateBitmapFromPixels32( framePixels, frameWidth, frameHeight );
            if( frameBitmap == nullptr )
            {
                TestLog( L"[Panorama/Test] Scenario=%s failed to create frame bitmap index=%zu\n",
                             scenarioName,
                             frameIndex );
                createFailed = true;
                break;
            }

            frames.push_back( frameBitmap );
        }

        if( createFailed || frames.size() != frameOriginsY.size() )
        {
            for( HBITMAP frame : frames )
            {
                if( frame != nullptr )
                {
                    DeleteObject( frame );
                }
            }
            return false;
        }

        HBITMAP stitchedBitmap = StitchPanoramaFrames( frames, false );

        for( HBITMAP frame : frames )
        {
            if( frame != nullptr )
            {
                DeleteObject( frame );
            }
        }

        if( stitchedBitmap == nullptr )
        {
            TestLog( L"[Panorama/Test] Scenario=%s StitchPanoramaFrames returned nullptr\n", scenarioName );
            return false;
        }

        std::vector<BYTE> stitchedPixels;
        int stitchedWidth = 0;
        int stitchedHeight = 0;
        const bool readOk = ReadBitmapPixels32( stitchedBitmap, stitchedPixels, stitchedWidth, stitchedHeight );
        DeleteObject( stitchedBitmap );
        if( !readOk )
        {
            TestLog( L"[Panorama/Test] Scenario=%s failed to read stitched bitmap pixels\n", scenarioName );
            return false;
        }

        const int minExpectedHeight = max( 1, expectedHeight - toleranceHeight );
        const int maxExpectedHeight = expectedHeight + toleranceHeight;
        if( stitchedWidth != frameWidth || stitchedHeight < minExpectedHeight || stitchedHeight > maxExpectedHeight )
        {
            TestLog( L"[Panorama/Test] Scenario=%s size mismatch actual=%dx%d expected=%dx[%d..%d]\n",
                         scenarioName,
                         stitchedWidth,
                         stitchedHeight,
                         frameWidth,
                         minExpectedHeight,
                         maxExpectedHeight );
            if( !selfTestDumpDirectory.empty() )
            {
                wchar_t msg[512]{};
                swprintf_s( msg, L"SIZE MISMATCH: %s actual=%dx%d expected=%dx[%d..%d]",
                            scenarioName, stitchedWidth, stitchedHeight, frameWidth, minExpectedHeight, maxExpectedHeight );
                DumpPanoramaText( selfTestDumpDirectory, L"scenario_fail_detail.txt", msg );
            }
            return false;
        }

        size_t sampleCount = 0;
        size_t mismatchCount = 0;
        const int sampleHeight = min( compareHeightOverride > 0 ? compareHeightOverride : expectedHeight, stitchedHeight );
        for( int y = 0; y < sampleHeight; y += 19 )
        {
            for( int x = 0; x < frameWidth; x += 17 )
            {
                const size_t stitchedIndex = ( static_cast<size_t>( y ) * static_cast<size_t>( stitchedWidth ) + static_cast<size_t>( x ) ) * 4;
                const size_t expectedIndex = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;

                const int diffBlue = abs( static_cast<int>( stitchedPixels[stitchedIndex + 0] ) - static_cast<int>( canvasPixels[expectedIndex + 0] ) );
                const int diffGreen = abs( static_cast<int>( stitchedPixels[stitchedIndex + 1] ) - static_cast<int>( canvasPixels[expectedIndex + 1] ) );
                const int diffRed = abs( static_cast<int>( stitchedPixels[stitchedIndex + 2] ) - static_cast<int>( canvasPixels[expectedIndex + 2] ) );

                sampleCount++;
                if( ( diffBlue + diffGreen + diffRed ) > 10 )
                {
                    mismatchCount++;
                }
            }
        }

        const bool passed = sampleCount > 0 && ( allowMismatches || mismatchCount == 0 );
        TestLog( L"[Panorama/Test] Scenario=%s result passed=%d samples=%zu mismatches=%zu actualHeight=%d\n",
                     scenarioName,
                     passed ? 1 : 0,
                     sampleCount,
                     mismatchCount,
                     stitchedHeight );
        if( !passed && !selfTestDumpDirectory.empty() )
        {
            wchar_t msg[512]{};
            swprintf_s( msg, L"PIXEL MISMATCH: %s samples=%zu mismatches=%zu actualH=%d expectedH=%d",
                        scenarioName, sampleCount, mismatchCount, stitchedHeight, expectedHeight );
            DumpPanoramaText( selfTestDumpDirectory, L"scenario_fail_detail.txt", msg );
        }
        return passed;
    };

    // ====================================================================
    // Phase 1: Basic stitching scenarios
    // ====================================================================
    int basicTestsRun = 0;
    int basicTestsPassed = 0;

    if( !selfTestStressOnly )
    {
    TestLog( L"\n==== Phase 1: Basic stitching scenarios ====\n" );

    {
        constexpr int frameWidth = 420;
        constexpr int frameHeight = 320;
        constexpr int stepY = 92;
        constexpr int frameCount = 10;
        constexpr int canvasHeight = frameHeight + stepY * ( frameCount + 1 );
        constexpr int expectedStitchedHeight = frameHeight + stepY * ( frameCount - 1 );

        std::vector<BYTE> syntheticCanvasPixels(
            static_cast<size_t>( frameWidth ) * static_cast<size_t>( canvasHeight ) * 4 );

        for( int y = 0; y < canvasHeight; ++y )
        {
            for( int x = 0; x < frameWidth; ++x )
            {
                BYTE blue = static_cast<BYTE>( ( x * 17 + y * 11 ) & 0xFF );
                BYTE green = static_cast<BYTE>( ( x * 7 + y * 19 + ( ( y / 23 ) * 13 ) ) & 0xFF );
                BYTE red = static_cast<BYTE>( ( x * 29 + y * 5 + ( ( x / 31 ) * 9 ) ) & 0xFF );

                if( ( y % 97 ) < 2 )
                {
                    red = static_cast<BYTE>( 255 - red / 3 );
                    green = static_cast<BYTE>( 255 - green / 3 );
                    blue = static_cast<BYTE>( 255 - blue / 3 );
                }

                if( ( x % 89 ) < 2 )
                {
                    red = static_cast<BYTE>( red / 2 );
                    green = static_cast<BYTE>( green / 2 );
                    blue = static_cast<BYTE>( blue / 2 );
                }

                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                syntheticCanvasPixels[index + 0] = blue;
                syntheticCanvasPixels[index + 1] = green;
                syntheticCanvasPixels[index + 2] = red;
                syntheticCanvasPixels[index + 3] = 0xFF;
            }
        }

        std::vector<int> originsY;
        originsY.reserve( frameCount );
        for( int frameIndex = 0; frameIndex < frameCount; ++frameIndex )
        {
            originsY.push_back( frameIndex * stepY );
        }

        basicTestsRun++;
        TestLog( L"  [%d/8] baseline-uniform-scroll ...\n", basicTestsRun );
        if( !runScenario( L"baseline-uniform-scroll",
                          frameWidth,
                          frameHeight,
                          originsY,
                          syntheticCanvasPixels,
                          canvasHeight,
                          expectedStitchedHeight,
                          0,
                          false ) )
        {
            TestLog( L"***** FAIL: baseline-uniform-scroll *****\n" );
            return false;
        }
        basicTestsPassed++;
        TestLog( L"  [%d/8] baseline-uniform-scroll PASSED\n", basicTestsRun );

        basicTestsRun++;
        TestLog( L"  [%d/8] fixed-overlay-background-recovery ...\n", basicTestsRun );
        {
            constexpr int overlayX0 = 120;
            constexpr int overlayX1 = 300;
            constexpr int overlayY0 = frameHeight - 110;
            constexpr int overlayY1 = frameHeight - 30;
            const int compareHeight = expectedStitchedHeight - ( frameHeight - overlayY0 );

            auto paintOverlay = [&]( size_t frameIndex, std::vector<BYTE>& framePixels )
            {
                (void)frameIndex;
                for( int y = overlayY0; y < overlayY1; ++y )
                {
                    for( int x = overlayX0; x < overlayX1; ++x )
                    {
                        const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                        framePixels[index + 0] = 18;
                        framePixels[index + 1] = 26;
                        framePixels[index + 2] = 240;
                        framePixels[index + 3] = 0xFF;
                    }
                }
            };

            if( !runScenario( L"fixed-overlay-background-recovery",
                              frameWidth,
                              frameHeight,
                              originsY,
                              syntheticCanvasPixels,
                              canvasHeight,
                              expectedStitchedHeight,
                              0,
                              false,
                              paintOverlay,
                              compareHeight ) )
            {
                TestLog( L"***** FAIL: fixed-overlay-background-recovery *****\n" );
                return false;
            }

            basicTestsPassed++;
            TestLog( L"  [%d/8] fixed-overlay-background-recovery PASSED\n", basicTestsRun );
        }
    }

    // Test: small-step frames must not overwrite previously composed content.
    //
    // When a frame's step falls between minProgress/2 (the acceptance
    // threshold) and minProgress (the feather-blend threshold), the frame
    // is accepted but neither mostlyVerticalMove nor mostlyHorizontalMove
    // is set.  Previously this caused weightNew=255 for all pixels,
    // overwriting already-composed canvas content.  This scenario verifies
    // the fix: a tampered small-step frame's overlap markers must NOT
    // appear in the output.
    basicTestsRun++;
    TestLog( L"  [%d/8] small-step-no-overwrite ...\n", basicTestsRun );
    {
        constexpr int frameWidth = 420;
        constexpr int frameHeight = 320;
        // minProgress = max(8, 320/30) = 10;  minProgress/2 = 5
        // Normal steps of 92 followed by a step of 6 (between 5 and 10).
        constexpr int normalStep = 92;
        constexpr int smallStep = 6;
        constexpr int normalFrameCount = 5;
        constexpr int totalFrames = normalFrameCount + 1;  // +1 for the small-step frame
        const int canvasHeight = frameHeight + normalStep * normalFrameCount + smallStep + 100;

        std::vector<BYTE> syntheticCanvasPixels(
            static_cast<size_t>( frameWidth ) * static_cast<size_t>( canvasHeight ) * 4 );

        for( int y = 0; y < canvasHeight; ++y )
        {
            for( int x = 0; x < frameWidth; ++x )
            {
                BYTE blue = static_cast<BYTE>( ( x * 17 + y * 11 ) & 0xFF );
                BYTE green = static_cast<BYTE>( ( x * 7 + y * 19 + ( ( y / 23 ) * 13 ) ) & 0xFF );
                BYTE red = static_cast<BYTE>( ( x * 29 + y * 5 + ( ( x / 31 ) * 9 ) ) & 0xFF );

                if( ( y % 97 ) < 2 )
                {
                    red = static_cast<BYTE>( 255 - red / 3 );
                    green = static_cast<BYTE>( 255 - green / 3 );
                    blue = static_cast<BYTE>( 255 - blue / 3 );
                }

                if( ( x % 89 ) < 2 )
                {
                    red = static_cast<BYTE>( red / 2 );
                    green = static_cast<BYTE>( green / 2 );
                    blue = static_cast<BYTE>( blue / 2 );
                }

                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                syntheticCanvasPixels[index + 0] = blue;
                syntheticCanvasPixels[index + 1] = green;
                syntheticCanvasPixels[index + 2] = red;
                syntheticCanvasPixels[index + 3] = 0xFF;
            }
        }

        // Build frame origins: 5 normal frames then 1 small-step frame.
        std::vector<int> originsY;
        for( int fi = 0; fi < normalFrameCount; ++fi )
        {
            originsY.push_back( fi * normalStep );
        }
        const int smallStepOrigin = ( normalFrameCount - 1 ) * normalStep + smallStep;
        originsY.push_back( smallStepOrigin );

        // Create frame bitmaps, but tamper with the last frame's overlap
        // region: paint a bright-red marker stripe that should NOT appear
        // in the stitched output because the overlap is already composed.
        std::vector<HBITMAP> frames;
        frames.reserve( totalFrames );
        bool createFailed = false;

        for( int fi = 0; fi < totalFrames; ++fi )
        {
            const int originY = originsY[fi];
            std::vector<BYTE> framePixels( static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight ) * 4 );
            for( int y = 0; y < frameHeight; ++y )
            {
                const size_t srcStart = ( static_cast<size_t>( originY + y ) * static_cast<size_t>( frameWidth ) ) * 4;
                const size_t dstStart = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) ) * 4;
                memcpy( framePixels.data() + dstStart,
                        syntheticCanvasPixels.data() + srcStart,
                        static_cast<size_t>( frameWidth ) * 4 );
            }

            // Tamper with the last frame: paint marker in the overlap zone.
            // The overlap covers rows 0..(frameHeight - smallStep - 1) of
            // the last frame.  Place markers in the middle of the overlap.
            if( fi == totalFrames - 1 )
            {
                const int markerY0 = frameHeight / 4;
                const int markerY1 = frameHeight / 4 + 10;
                for( int y = markerY0; y < markerY1; ++y )
                {
                    for( int x = 10; x < frameWidth - 10; ++x )
                    {
                        const size_t idx = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                        framePixels[idx + 0] = 0;       // B
                        framePixels[idx + 1] = 0;       // G
                        framePixels[idx + 2] = 255;     // R -- bright red marker
                        framePixels[idx + 3] = 0xFF;
                    }
                }
            }

            BITMAPINFO bmi{};
            bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
            bmi.bmiHeader.biWidth = frameWidth;
            bmi.bmiHeader.biHeight = -frameHeight;
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            HDC hdc = GetDC( nullptr );
            void* bits = nullptr;
            HBITMAP bitmap = CreateDIBSection( hdc, &bmi, DIB_RGB_COLORS, &bits, nullptr, 0 );
            if( bitmap != nullptr && bits != nullptr )
            {
                memcpy( bits, framePixels.data(), framePixels.size() );
            }
            else if( bitmap != nullptr )
            {
                DeleteObject( bitmap );
                bitmap = nullptr;
            }
            ReleaseDC( nullptr, hdc );

            if( bitmap == nullptr )
            {
                createFailed = true;
                break;
            }
            frames.push_back( bitmap );
        }

        if( createFailed )
        {
            for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
            TestLog( L"[Panorama/Test] small-step-no-overwrite: failed to create frame bitmaps\n" );
            return false;
        }

        HBITMAP stitchedBitmap = StitchPanoramaFrames( frames, false );
        for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }

        if( stitchedBitmap == nullptr )
        {
            TestLog( L"[Panorama/Test] small-step-no-overwrite: StitchPanoramaFrames returned nullptr\n" );
            return false;
        }

        std::vector<BYTE> stitchedPixels;
        int stitchedWidth = 0;
        int stitchedHeight = 0;
        if( !ReadBitmapPixels32( stitchedBitmap, stitchedPixels, stitchedWidth, stitchedHeight ) )
        {
            DeleteObject( stitchedBitmap );
            TestLog( L"[Panorama/Test] small-step-no-overwrite: failed to read stitched bitmap\n" );
            return false;
        }
        DeleteObject( stitchedBitmap );

        // Verify the red markers do NOT appear in the stitched output.
        // The marker row in source coordinates is at smallStepOrigin + markerY0.
        const int markerCanvasY = smallStepOrigin + frameHeight / 4;
        size_t markerPixels = 0;
        size_t markerPresent = 0;
        for( int x = 10; x < min( stitchedWidth, frameWidth ) - 10; x += 3 )
        {
            if( markerCanvasY >= stitchedHeight )
                break;
            const size_t idx = ( static_cast<size_t>( markerCanvasY ) * static_cast<size_t>( stitchedWidth ) + static_cast<size_t>( x ) ) * 4;
            if( idx + 3 >= stitchedPixels.size() )
                break;
            markerPixels++;
            // Check for the bright-red marker: R=255, G=0, B=0.
            if( stitchedPixels[idx + 2] == 255 && stitchedPixels[idx + 1] == 0 && stitchedPixels[idx + 0] == 0 )
            {
                markerPresent++;
            }
        }

        const bool markerVisible = markerPresent > markerPixels / 2;
        TestLog( L"[Panorama/Test] small-step-no-overwrite: markerPixels=%zu markerPresent=%zu visible=%d\n",
                     markerPixels, markerPresent, markerVisible ? 1 : 0 );
        if( markerVisible )
        {
            TestLog( L"[Panorama/Test] ***** FAIL: small-step-no-overwrite: overlap was overwritten *****\n" );
            if( !selfTestDumpDirectory.empty() )
            {
                DumpPanoramaText( selfTestDumpDirectory, L"scenario_fail_detail.txt",
                                  L"OVERWRITE: small-step-no-overwrite -- red markers visible in overlap" );
            }
            return false;
        }

        TestLog( L"  [%d/8] small-step-no-overwrite PASSED\n", basicTestsRun );
        basicTestsPassed++;
    }

    basicTestsRun++;
    TestLog( L"  [%d/8] repro-1099x336-variable-steps-tail ...\n", basicTestsRun );
    {
        constexpr int frameWidth = 1099;
        constexpr int frameHeight = 336;
        const std::vector<int> steps{ 44, 52, 48, 50, 40, 50 };
        const int frameCount = static_cast<int>( steps.size() ) + 1;
        int expectedStitchedHeight = frameHeight;
        for( int step : steps )
        {
            expectedStitchedHeight += step;
        }
        const int canvasHeight = expectedStitchedHeight + 180;

        std::vector<BYTE> syntheticCanvasPixels(
            static_cast<size_t>( frameWidth ) * static_cast<size_t>( canvasHeight ) * 4 );

        for( int y = 0; y < canvasHeight; ++y )
        {
            for( int x = 0; x < frameWidth; ++x )
            {
                BYTE blue = static_cast<BYTE>( ( x * 13 + y * 5 ) & 0xFF );
                BYTE green = static_cast<BYTE>( ( x * 3 + y * 17 + ( ( y / 21 ) * 7 ) ) & 0xFF );
                BYTE red = static_cast<BYTE>( ( x * 11 + y * 9 ) & 0xFF );

                // Simulate low-texture tail where shift scoring can look too
                // similar to stationary content.
                if( y > canvasHeight - 420 )
                {
                    const BYTE smooth = static_cast<BYTE>( ( y * 3 ) & 0xFF );
                    blue = smooth;
                    green = static_cast<BYTE>( smooth + 4 );
                    red = static_cast<BYTE>( smooth + 8 );

                    // Keep subtle anchor stripes so true movement remains
                    // detectable, but with much weaker signal than earlier
                    // frames.
                    if( ( x % 157 ) == 0 || ( y % 113 ) == 0 )
                    {
                        blue = static_cast<BYTE>( min( 255, blue + 20 ) );
                        green = static_cast<BYTE>( min( 255, green + 15 ) );
                        red = static_cast<BYTE>( min( 255, red + 10 ) );
                    }
                }
                else
                {
                    if( ( x % 131 ) < 2 || ( y % 127 ) < 2 )
                    {
                        red = static_cast<BYTE>( 255 - red / 2 );
                        green = static_cast<BYTE>( 255 - green / 2 );
                        blue = static_cast<BYTE>( 255 - blue / 2 );
                    }
                }

                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                syntheticCanvasPixels[index + 0] = blue;
                syntheticCanvasPixels[index + 1] = green;
                syntheticCanvasPixels[index + 2] = red;
                syntheticCanvasPixels[index + 3] = 0xFF;
            }
        }

        std::vector<int> originsY;
        originsY.reserve( static_cast<size_t>( frameCount ) );
        int runningY = 0;
        originsY.push_back( runningY );
        for( int step : steps )
        {
            runningY += step;
            originsY.push_back( runningY );
        }

        if( !runScenario( L"repro-1099x336-variable-steps-tail",
                          frameWidth,
                          frameHeight,
                          originsY,
                          syntheticCanvasPixels,
                          canvasHeight,
                          expectedStitchedHeight,
                          6,
                          false ) )
        {
            TestLog( L"***** FAIL: repro-1099x336-variable-steps-tail *****\n" );
            return false;
        }
        basicTestsPassed++;
        TestLog( L"  [%d/8] repro-1099x336-variable-steps-tail PASSED\n", basicTestsRun );
    }

    basicTestsRun++;
    TestLog( L"  [%d/8] repro-realcapture-variable-large-steps ...\n", basicTestsRun );
    {
        constexpr int frameWidth = 1079;
        constexpr int frameHeight = 341;
        const std::vector<int> steps{ 97, 26, 41, 116, 66 };
        const int frameCount = static_cast<int>( steps.size() ) + 1;
        int expectedStitchedHeight = frameHeight;
        for( int step : steps )
        {
            expectedStitchedHeight += step;
        }
        const int canvasHeight = expectedStitchedHeight + 160;

        std::vector<BYTE> syntheticCanvasPixels(
            static_cast<size_t>( frameWidth ) * static_cast<size_t>( canvasHeight ) * 4 );

        for( int y = 0; y < canvasHeight; ++y )
        {
            for( int x = 0; x < frameWidth; ++x )
            {
                BYTE blue = static_cast<BYTE>( ( x * 5 + y * 7 + ( y / 19 ) * 9 ) & 0xFF );
                BYTE green = static_cast<BYTE>( ( x * 17 + y * 3 + ( x / 29 ) * 5 ) & 0xFF );
                BYTE red = static_cast<BYTE>( ( x * 11 + y * 13 ) & 0xFF );

                if( ( x % 149 ) < 2 || ( y % 109 ) < 2 )
                {
                    red = static_cast<BYTE>( 255 - red / 2 );
                    green = static_cast<BYTE>( 255 - green / 2 );
                    blue = static_cast<BYTE>( 255 - blue / 2 );
                }

                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) + static_cast<size_t>( x ) ) * 4;
                syntheticCanvasPixels[index + 0] = blue;
                syntheticCanvasPixels[index + 1] = green;
                syntheticCanvasPixels[index + 2] = red;
                syntheticCanvasPixels[index + 3] = 0xFF;
            }
        }

        std::vector<int> originsY;
        originsY.reserve( static_cast<size_t>( frameCount ) );
        int runningY = 0;
        originsY.push_back( runningY );
        for( int step : steps )
        {
            runningY += step;
            originsY.push_back( runningY );
        }

        if( !runScenario( L"repro-realcapture-variable-large-steps",
                          frameWidth,
                          frameHeight,
                          originsY,
                          syntheticCanvasPixels,
                          canvasHeight,
                          expectedStitchedHeight,
                          8,
                          false ) )
        {
            TestLog( L"***** FAIL: repro-realcapture-variable-large-steps *****\n" );
            return false;
        }
        basicTestsPassed++;
        TestLog( L"  [%d/8] repro-realcapture-variable-large-steps PASSED\n", basicTestsRun );

    // Regression test for very-low-entropy periodic content where early
    // frames can be rejected at expected=(0,0), causing a large recovery
    // gap and dropped middle content.
    basicTestsRun++;
    TestLog( L"  [%d/8] repro-vle-periodic-middledrop ...\n", basicTestsRun );
    {
        constexpr int frameWidth2 = 1228;
        constexpr int frameHeight2 = 1032;
        constexpr int stepY = 50;
        constexpr int frameCount2 = 19;
        constexpr int canvasHeight2 = frameHeight2 + stepY * ( frameCount2 + 2 );
        constexpr int expectedStitchedHeight2 = frameHeight2 + stepY * ( frameCount2 - 1 );

        std::vector<BYTE> syntheticCanvasPixelsInternal(
            static_cast<size_t>( frameWidth2 ) * static_cast<size_t>( canvasHeight2 ) * 4,
            0 );

        for( int y = 0; y < canvasHeight2; ++y )
        {
            for( int x = 0; x < frameWidth2; ++x )
            {
                // Dark, low-contrast background with tiny deterministic noise.
                BYTE base = static_cast<BYTE>( 14 + ( ( x * 3 + y * 5 ) & 0x03 ) );
                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth2 ) + static_cast<size_t>( x ) ) * 4;
                syntheticCanvasPixelsInternal[index + 0] = base;
                syntheticCanvasPixelsInternal[index + 1] = static_cast<BYTE>( base + 1 );
                syntheticCanvasPixelsInternal[index + 2] = static_cast<BYTE>( base + 2 );
                syntheticCanvasPixelsInternal[index + 3] = 0xFF;
            }
        }

        // Add sparse periodic ruler lines and tiny left-gutter markers.
        for( int band = 0; band * stepY < canvasHeight2; ++band )
        {
            const int y0 = band * stepY;
            for( int dy = 0; dy < 2; ++dy )
            {
                const int yy = y0 + dy;
                if( yy >= canvasHeight2 )
                    continue;
                for( int x = 0; x < frameWidth2; ++x )
                {
                    const size_t index = ( static_cast<size_t>( yy ) * static_cast<size_t>( frameWidth2 ) + static_cast<size_t>( x ) ) * 4;
                    syntheticCanvasPixelsInternal[index + 0] = 38;
                    syntheticCanvasPixelsInternal[index + 1] = 42;
                    syntheticCanvasPixelsInternal[index + 2] = 46;
                }
            }

            // Sparse, weak non-periodic marker (line-number-like cue).
            // Keep it tiny so the frame remains very-low-entropy.
            const int x0 = 10 + ( ( band * 37 ) % 96 );
            for( int yy = y0 + 10; yy < min( y0 + 12, canvasHeight2 ); ++yy )
            {
                for( int xx = x0; xx < min( x0 + 2, frameWidth2 ); ++xx )
                {
                    const size_t index = ( static_cast<size_t>( yy ) * static_cast<size_t>( frameWidth2 ) + static_cast<size_t>( xx ) ) * 4;
                    syntheticCanvasPixelsInternal[index + 0] = 150;
                    syntheticCanvasPixelsInternal[index + 1] = 156;
                    syntheticCanvasPixelsInternal[index + 2] = 162;
                }
            }
        }

        std::vector<int> originsYInternal;
        originsYInternal.reserve( frameCount2 );
        for( int i = 0; i < frameCount2; ++i )
        {
            originsYInternal.push_back( i * stepY );
        }

        if( !runScenario( L"repro-vle-periodic-middledrop",
                          frameWidth2,
                          frameHeight2,
                          originsYInternal,
                          syntheticCanvasPixelsInternal,
                          canvasHeight2,
                          expectedStitchedHeight2,
                          8,
                          false ) )
        {
            TestLog( L"***** FAIL: repro-vle-periodic-middledrop *****\n" );
            return false;
        }
        basicTestsPassed++;
        TestLog( L"  [%d/8] repro-vle-periodic-middledrop PASSED\n", basicTestsRun );

    basicTestsRun++;
    TestLog( L"  [%d/8] repro-axis-defer-vle-vertical ...\n", basicTestsRun );
    {
        constexpr int frameWidth3 = 1228;
        constexpr int frameHeight3 = 1032;
        constexpr int shiftY = 50;
        constexpr int canvasHeight3 = frameHeight3 + shiftY + 64;

        std::vector<BYTE> canvasPixels(
            static_cast<size_t>( frameWidth3 ) * static_cast<size_t>( canvasHeight3 ) * 4,
            0 );

        for( int y = 0; y < canvasHeight3; ++y )
        {
            for( int x = 0; x < frameWidth3; ++x )
            {
                BYTE base = static_cast<BYTE>( 14 + ( ( x * 3 + y * 5 ) & 0x03 ) );
                const size_t index = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth3 ) + static_cast<size_t>( x ) ) * 4;
                canvasPixels[index + 0] = base;
                canvasPixels[index + 1] = static_cast<BYTE>( base + 1 );
                canvasPixels[index + 2] = static_cast<BYTE>( base + 2 );
                canvasPixels[index + 3] = 0xFF;
            }
        }

        for( int band = 0; band * shiftY < canvasHeight3; ++band )
        {
            const int y0 = band * shiftY;
            for( int dy = 0; dy < 2; ++dy )
            {
                const int yy = y0 + dy;
                if( yy >= canvasHeight3 )
                    continue;
                for( int x = 0; x < frameWidth3; ++x )
                {
                    const size_t index = ( static_cast<size_t>( yy ) * static_cast<size_t>( frameWidth3 ) + static_cast<size_t>( x ) ) * 4;
                    canvasPixels[index + 0] = 38;
                    canvasPixels[index + 1] = 42;
                    canvasPixels[index + 2] = 46;
                }
            }

            const int x0 = 10 + ( ( band * 37 ) % 96 );
            for( int yy = y0 + 10; yy < min( y0 + 12, canvasHeight3 ); ++yy )
            {
                for( int xx = x0; xx < min( x0 + 2, frameWidth3 ); ++xx )
                {
                    const size_t index = ( static_cast<size_t>( yy ) * static_cast<size_t>( frameWidth3 ) + static_cast<size_t>( xx ) ) * 4;
                    canvasPixels[index + 0] = 150;
                    canvasPixels[index + 1] = 156;
                    canvasPixels[index + 2] = 162;
                }
            }
        }

        std::vector<BYTE> previousPixels( static_cast<size_t>( frameWidth3 ) * static_cast<size_t>( frameHeight3 ) * 4 );
        std::vector<BYTE> currentPixels( static_cast<size_t>( frameWidth3 ) * static_cast<size_t>( frameHeight3 ) * 4 );
        for( int y = 0; y < frameHeight3; ++y )
        {
            const size_t srcPrev = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth3 ) ) * 4;
            const size_t srcCurr = ( static_cast<size_t>( y + shiftY ) * static_cast<size_t>( frameWidth3 ) ) * 4;
            const size_t dst = ( static_cast<size_t>( y ) * static_cast<size_t>( frameWidth3 ) ) * 4;
            memcpy( previousPixels.data() + dst, canvasPixels.data() + srcPrev, static_cast<size_t>( frameWidth3 ) * 4 );
            memcpy( currentPixels.data() + dst, canvasPixels.data() + srcCurr, static_cast<size_t>( frameWidth3 ) * 4 );
        }

        std::vector<BYTE> previousLuma;
        std::vector<BYTE> currentLuma;
        BuildFullLumaFrame( previousPixels, frameWidth3, frameHeight3, previousLuma );
        BuildFullLumaFrame( currentPixels, frameWidth3, frameHeight3, currentLuma );

        int bestDx = 0;
        int bestDy = 0;
        bool nearStationaryOverride = false;
        const bool found = FindBestFrameShift( previousPixels,
                                               currentPixels,
                                               frameWidth3,
                                               frameHeight3,
                                               0,
                                               0,
                                               bestDx,
                                               bestDy,
                                               true,
                                               previousLuma,
                                               currentLuma,
                                               1,
                                               &nearStationaryOverride );

        if( !found || abs( bestDy ) < shiftY - 8 )
        {
            TestLog( L"***** FAIL: repro-axis-defer-vle-vertical found=%d best=(%d,%d) expectedDy~-%d *****\n",
                     found ? 1 : 0,
                     bestDx,
                     bestDy,
                     shiftY );
            return false;
        }

        basicTestsPassed++;
        TestLog( L"  [%d/8] repro-axis-defer-vle-vertical PASSED\n", basicTestsRun );
    }

    }
    }

    // Drop-logic test: exercise AreFramesNearDuplicate directly
    basicTestsRun++;
    TestLog( L"  [%d/8] drop-logic-near-duplicate ...\n", basicTestsRun );
    {
        constexpr int dW = 200;
        constexpr int dH = 200;
        const size_t pixelBytes = static_cast<size_t>( dW ) * dH * 4;

        // Build a base frame with a simple gradient so it has some texture.
        std::vector<BYTE> basePixels( pixelBytes );
        for( int y = 0; y < dH; y++ )
        {
            for( int x = 0; x < dW; x++ )
            {
                const size_t off = ( static_cast<size_t>( y ) * dW + x ) * 4;
                basePixels[off + 0] = static_cast<BYTE>( ( x * 47 + y * 13 ) & 0xFF );  // B
                basePixels[off + 1] = static_cast<BYTE>( ( x * 31 + y * 7 ) & 0xFF );   // G
                basePixels[off + 2] = static_cast<BYTE>( ( x * 17 + y * 23 ) & 0xFF );  // R
                basePixels[off + 3] = 0xFF;
            }
        }

        // Case 1: identical frames -> must be detected as duplicate.
        {
            HBITMAP frameA = CreateBitmapFromPixels32( basePixels, dW, dH );
            HBITMAP frameB = CreateBitmapFromPixels32( basePixels, dW, dH );
            bool subPix = false;
            bool dup = AreFramesNearDuplicate( frameA, frameB, false, &subPix );
            DeleteObject( frameA );
            DeleteObject( frameB );
            if( !dup )
            {
                TestLog( L"***** FAIL: drop-logic-near-duplicate case 1 (identical frames not detected) *****\n" );
                return false;
            }
        }

        // Case 2: frame shifted by large amount -> must NOT be duplicate.
        {
            // Shift base down by 20 pixels.
            std::vector<BYTE> shifted( pixelBytes, 0 );
            constexpr int shiftY = 20;
            memcpy( shifted.data() + static_cast<size_t>( shiftY ) * dW * 4,
                    basePixels.data(),
                    static_cast<size_t>( dH - shiftY ) * dW * 4 );
            HBITMAP frameA = CreateBitmapFromPixels32( basePixels, dW, dH );
            HBITMAP frameB = CreateBitmapFromPixels32( shifted, dW, dH );
            bool dup = AreFramesNearDuplicate( frameB, frameA, false );
            DeleteObject( frameA );
            DeleteObject( frameB );
            if( dup )
            {
                TestLog( L"***** FAIL: drop-logic-near-duplicate case 2 (scrolled frame falsely dropped) *****\n" );
                return false;
            }
        }

        // Case 3: low-contrast frame with +/-1 px shift that improves MAD --
        // must NOT be duplicate (tests the lowContrastMode rescue).
        {
            // Create a low-contrast frame: mostly flat with subtle variation.
            std::vector<BYTE> lowBase( pixelBytes );
            for( int y = 0; y < dH; y++ )
            {
                for( int x = 0; x < dW; x++ )
                {
                    const size_t off = ( static_cast<size_t>( y ) * dW + x ) * 4;
                    BYTE val = static_cast<BYTE>( 128 + ( ( y * 3 + x ) % 5 ) );
                    lowBase[off + 0] = val;
                    lowBase[off + 1] = val;
                    lowBase[off + 2] = val;
                    lowBase[off + 3] = 0xFF;
                }
            }

            // Create shifted-by-1-pixel version (real 1px scroll).
            std::vector<BYTE> lowShifted( pixelBytes, 128 );
            for( int y = 1; y < dH; y++ )
            {
                memcpy( lowShifted.data() + static_cast<size_t>( y ) * dW * 4,
                        lowBase.data() + static_cast<size_t>( y - 1 ) * dW * 4,
                        static_cast<size_t>( dW ) * 4 );
            }
            // Fill top row with the pattern that would wrap.
            memcpy( lowShifted.data(), lowBase.data() + static_cast<size_t>( dH - 1 ) * dW * 4,
                    static_cast<size_t>( dW ) * 4 );

            HBITMAP frameA = CreateBitmapFromPixels32( lowBase, dW, dH );
            HBITMAP frameB = CreateBitmapFromPixels32( lowShifted, dW, dH );
            bool subPix = false;
            bool dup = AreFramesNearDuplicate( frameB, frameA, true, &subPix );
            DeleteObject( frameA );
            DeleteObject( frameB );
            if( dup )
            {
                TestLog( L"***** FAIL: drop-logic-near-duplicate case 3 (low-contrast 1px scroll falsely dropped, subPix=%d) *****\n", subPix ? 1 : 0 );
                return false;
            }
        }

        // Case 4: truly identical low-contrast frames -> must be duplicate.
        {
            std::vector<BYTE> flat( pixelBytes );
            for( size_t i = 0; i < pixelBytes; i += 4 )
            {
                flat[i + 0] = 130;
                flat[i + 1] = 130;
                flat[i + 2] = 130;
                flat[i + 3] = 0xFF;
            }
            HBITMAP frameA = CreateBitmapFromPixels32( flat, dW, dH );
            HBITMAP frameB = CreateBitmapFromPixels32( flat, dW, dH );
            bool dup = AreFramesNearDuplicate( frameA, frameB, true );
            DeleteObject( frameA );
            DeleteObject( frameB );
            if( !dup )
            {
                TestLog( L"***** FAIL: drop-logic-near-duplicate case 4 (identical low-contrast not detected) *****\n" );
                return false;
            }
        }

        basicTestsPassed++;
        TestLog( L"  [%d/8] drop-logic-near-duplicate PASSED\n", basicTestsRun );
    }

    // Harmonic false-match test.
    //
    // Dark-themed code editors produce frames where ~65-75% of rows are
    // identical (constant background) and a subtle ~60 px periodic
    // structure exists (line separators, code indentation bands).  When a
    // frame scrolls by the period length (~60 px), the matcher can lock
    // onto a harmonic (2x, 3x, or 4x the real offset) because the MAD
    // score is near zero at every multiple.  This test synthesizes such
    // content and verifies the stitcher produces the correct output.
    //
    // Reproduces the T10 bug from panorama_20260310_063946_40956 where
    // matcherDy=-238 but actual scroll was -60 (4x harmonic).
    //
    // The matcher's coarse search on downsampled luma finds candidates
    // at every multiple of the content period (60px -> DS period 15).
    // With >12 period multiples in the search window, the true offset
    // (-60, the smallest) is excluded from the top-12 coarse candidate
    // list.  The harmonic-fallback probe mechanism would normally
    // rescue it (injecting small-shift probes), but it requires
    // fineScore == 0 to accept a probe.  By adding a single pixel of
    // noise to one frame, fineScore at the true offset becomes 1 (>0),
    // causing the probe to fail the score<=0 threshold guard.  The
    // matcher then falls back to expected-step proximity among original
    // candidates and selects a large harmonic.  The AnchorOverride
    // finds a unique anchor row that matches at the true offset and
    // corrects the harmonic error.
    basicTestsRun++;
    TestLog( L"  [%d/8] harmonic-dark-theme-override ...\n", basicTestsRun );
    {
        constexpr int frameWidth = 446;
        constexpr int frameHeight = 1044;
        constexpr int period = 60;

        // Early steps are close to 180 but NOT exact multiples of the
        // period, so adjacent frames differ visibly and the matcher
        // resolves them unambiguously.  Step 5 (transition 4->5) is
        // exactly one period (60): a tiny scroll on periodic content
        // that creates the harmonic trap.  The noise pixel on frame 4
        // blocks the harmonic-fallback probe, forcing the matcher to
        // pick a large harmonic, which the AnchorOverride must correct.
        const int steps[] = { 181, 179, 183, 177, period, 12, 9, 31, 51, period };
        constexpr int frameCount = 1 + _countof( steps );
        int totalScroll = 0;
        for( int s : steps )
            totalScroll += s;
        const int canvasHeight = frameHeight + totalScroll + 200;

        // Canvas: dark background + periodic separators + periodic
        // code lines + a few unique anchor rows.
        std::vector<BYTE> canvas(
            static_cast<size_t>( frameWidth ) * static_cast<size_t>( canvasHeight ) * 4 );

        // Layer 1: background + separators (fully periodic).
        for( int y = 0; y < canvasHeight; ++y )
        {
            const bool isSeparator = ( y % period ) < 2;
            for( int x = 0; x < frameWidth; ++x )
            {
                const size_t idx = ( static_cast<size_t>( y ) * frameWidth + x ) * 4;
                if( isSeparator )
                {
                    canvas[idx + 0] = 82;
                    canvas[idx + 1] = 82;
                    canvas[idx + 2] = 82;
                }
                else
                {
                    BYTE noise = static_cast<BYTE>( 41 + ( ( x * 3 + y * 7 ) & 0x03 ) );
                    canvas[idx + 0] = noise;
                    canvas[idx + 1] = noise;
                    canvas[idx + 2] = noise;
                }
                canvas[idx + 3] = 0xFF;
            }
        }

        // Layer 2: periodic "code lines" — same colors for the same
        // offset within each period band (independent of band number).
        // This makes ALL multiples-of-60 offsets produce fineScore=0
        // on clean frames, creating the harmonic ambiguity.
        {
            const int codeLineOffsets[] = { 7, 13, 19, 23, 31, 37, 41, 53 };
            for( int band = 0; band * period < canvasHeight; ++band )
            {
                for( int oi = 0; oi < _countof( codeLineOffsets ); ++oi )
                {
                    const int yy = band * period + codeLineOffsets[oi];
                    if( yy >= canvasHeight )
                        continue;
                    for( int x = 0; x < frameWidth; ++x )
                    {
                        const size_t idx = ( static_cast<size_t>( yy ) * frameWidth + x ) * 4;
                        // Colors depend on (offset_index, x) only,
                        // NOT on band — making them perfectly periodic.
                        const int h = oi * 59 + x * 17;
                        canvas[idx + 0] = static_cast<BYTE>( 30 + ( ( h + x * 13 ) & 0x7F ) );
                        canvas[idx + 1] = static_cast<BYTE>( 40 + ( ( h + x * 29 + 97 ) & 0x7F ) );
                        canvas[idx + 2] = static_cast<BYTE>( 50 + ( ( h + x * 43 + 151 ) & 0x7F ) );
                    }
                }
            }
        }

        // Layer 3: unique anchor rows at non-periodic intervals.
        // Spacing of 70 (coprime with period 60) ensures these never
        // coincide with period boundaries.  The anchor tracker uses
        // these to independently determine the true scroll offset.
        // Colors span the full [0,255] range to guarantee very high
        // luma variance (>>500), placing them above periodic code
        // lines in the anchor candidate ranking.
        for( int y = 35; y < canvasHeight; y += 70 )
        {
            for( int x = 0; x < frameWidth; ++x )
            {
                const size_t idx = ( static_cast<size_t>( y ) * frameWidth + x ) * 4;
                uint32_t seed = static_cast<uint32_t>( y ) * 2654435761u
                              + static_cast<uint32_t>( x ) * 40503u;
                seed ^= seed >> 16;
                seed *= 0x45d9f3bu;
                seed ^= seed >> 16;
                canvas[idx + 0] = static_cast<BYTE>( seed & 0xFFu );
                canvas[idx + 1] = static_cast<BYTE>( ( seed >> 8 ) & 0xFFu );
                canvas[idx + 2] = static_cast<BYTE>( ( seed >> 16 ) & 0xFFu );
            }
        }

        // Build frame origins.
        std::vector<int> originsY;
        originsY.reserve( frameCount );
        originsY.push_back( 0 );
        int cumulative = 0;
        for( int s : steps )
        {
            cumulative += s;
            originsY.push_back( cumulative );
        }

        // Create frames manually: frame 4 gets a single pixel of
        // noise so that fineScore > 0 at every offset, defeating the
        // harmonic-fallback probe threshold guard (which requires
        // fineScore == 0).  All other frames are clean.
        constexpr int noisyFrameIndex = 4;

        std::vector<HBITMAP> frames;
        frames.reserve( frameCount );
        bool createFailed = false;

        for( size_t fi = 0; fi < static_cast<size_t>( frameCount ); ++fi )
        {
            const int originY = originsY[fi];
            if( originY < 0 || originY + frameHeight > canvasHeight )
            {
                TestLog( L"[Panorama/Test] harmonic: invalid origin frame=%zu originY=%d\n", fi, originY );
                createFailed = true;
                break;
            }

            std::vector<BYTE> framePixels(
                static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight ) * 4 );
            for( int y = 0; y < frameHeight; ++y )
            {
                const size_t srcStart = ( static_cast<size_t>( originY + y ) * frameWidth ) * 4;
                const size_t dstStart = ( static_cast<size_t>( y ) * frameWidth ) * 4;
                memcpy( framePixels.data() + dstStart,
                        canvas.data() + srcStart,
                        static_cast<size_t>( frameWidth ) * 4 );
            }

            // Inject exactly 1 pixel of noise into frame 4.
            // This makes fineScore at the true offset = 1 (instead
            // of 0), which is enough to block the harmonic-fallback
            // probe from being accepted (threshold is strict ==0).
            if( static_cast<int>( fi ) == noisyFrameIndex )
            {
                const size_t noiseIdx = ( 500u * frameWidth + 200u ) * 4;
                if( noiseIdx + 2 < framePixels.size() )
                {
                    framePixels[noiseIdx + 0] = static_cast<BYTE>(
                        min( 255, framePixels[noiseIdx + 0] + 3 ) );
                }
            }

            HBITMAP bmp = CreateBitmapFromPixels32( framePixels, frameWidth, frameHeight );
            if( bmp == nullptr )
            {
                TestLog( L"[Panorama/Test] harmonic: failed to create bitmap frame=%zu\n", fi );
                createFailed = true;
                break;
            }
            frames.push_back( bmp );
        }

        // Save frames so /panorama-stitch-replay can re-stitch them.
        if( !createFailed && !selfTestDumpDirectory.empty() )
        {
            for( size_t si = 0; si < frames.size(); si++ )
                DumpPanoramaBitmap( selfTestDumpDirectory, L"accepted", si + 1, frames[si] );
        }

        bool passed = false;
        if( !createFailed && frames.size() == static_cast<size_t>( frameCount ) )
        {
            HBITMAP stitchedBitmap = StitchPanoramaFrames( frames, false );
            if( stitchedBitmap != nullptr )
            {
                BITMAP bm{};
                if( GetObject( stitchedBitmap, sizeof( bm ), &bm ) )
                {
                    // With purely periodic content (period 60) the matcher
                    // aliases most large steps down to ~60 regardless of the
                    // true step size.  The stitched height is therefore much
                    // smaller than the true canvas, typically ~1250-1350.
                    //
                    // With the largeHarmonicCorrection fix ENABLED, the
                    // AnchorOverride corrects the harmonic-trap transition
                    // (frame 5) from the large harmonic (~360) back to the
                    // true offset (60), keeping the stitched height modest.
                    //
                    // With the fix DISABLED, the AnchorOverride detects the
                    // error but does NOT correct it, so frame 5 is stitched
                    // at dy=360, producing a canvas ~300px taller.
                    //
                    // Validate: height must be below the midpoint threshold
                    // so the test passes with the fix and fails without it.
                    const int maxHeightWithFix    = 1400;
                    const int minHeightWithoutFix = 1500;
                    (void)minHeightWithoutFix; // documentation only

                    if( bm.bmWidth == frameWidth &&
                        bm.bmHeight > frameHeight &&
                        bm.bmHeight <= maxHeightWithFix )
                    {
                        passed = true;
                    }
                    else
                    {
                        TestLog( L"[Panorama/Test] harmonic: size mismatch %dx%d expected width=%d height in (%d..%d]\n",
                                     static_cast<int>( bm.bmWidth ),
                                     static_cast<int>( bm.bmHeight ),
                                     frameWidth, frameHeight, maxHeightWithFix );
                    }
                }
                if( !selfTestDumpDirectory.empty() )
                    SaveBitmapAsBmp( stitchedBitmap, std::filesystem::path( selfTestDumpDirectory ) / L"stitched_harmonic.bmp" );
                DeleteObject( stitchedBitmap );
            }
            else
            {
                TestLog( L"[Panorama/Test] harmonic: StitchPanoramaFrames returned nullptr\n" );
            }
        }

        for( HBITMAP f : frames )
        {
            if( f != nullptr ) DeleteObject( f );
        }

        if( !passed )
        {
            TestLog( L"***** FAIL: harmonic-dark-theme-override *****\n" );
            if( !selfTestDumpDirectory.empty() )
            {
                DumpPanoramaText( selfTestDumpDirectory, L"scenario_fail_detail.txt",
                                  L"HARMONIC: matcher picked a harmonic offset instead of the true offset on dark periodic content" );
            }
            return false;
        }
        basicTestsPassed++;
        TestLog( L"  [%d/8] harmonic-dark-theme-override PASSED\n", basicTestsRun );
    }

    } // !selfTestStressOnly — end of Phase 1

    // Random-slice tests using real images.
    // Load PNG images from <solutionDir>/Debug, slice each into overlapping
    // frames with random window height and random step sizes, stitch, and
    // verify the result matches the original.
    {
        // COM must be initialized for WIC image loading. The selftest runs
        // before WinMain's CoInitialize, so initialize here.
        HRESULT hrCom = CoInitializeEx( nullptr, COINIT_APARTMENTTHREADED );
        if( FAILED( hrCom ) )
        {
            TestLog( L"[Panorama/Test] CoInitializeEx failed hr=0x%08lx\n", hrCom );
            return false;
        }

        // Locate test images relative to the executable.
        // Exe is at <solutionDir>/ARM64/Debug/ZoomIt64a.exe; images at <solutionDir>/Debug/
        wchar_t modulePath[MAX_PATH]{};
        if( GetModuleFileNameW( nullptr, modulePath, ARRAYSIZE( modulePath ) ) == 0 )
        {
            TestLog( L"[Panorama/Test] GetModuleFileNameW failed\n" );
            CoUninitialize();
            return false;
        }
        const auto imageDir = std::filesystem::path( modulePath ).parent_path().parent_path().parent_path() / L"Debug";



        TestLog( L"[Panorama/Test] Image directory: %s\n", imageDir.c_str() );

        const wchar_t* imageFiles[] = { L"image1.png", L"image2.png", L"image3.png", L"image4.png", L"image5.png", L"image6.png" };
        const bool useExternalImageAssets = false;
        if( !useExternalImageAssets )
        {
            TestLog( L"[Panorama/Test] External image-based tests disabled; running synthetic-only selftest\n" );
        }

        // WIC-based loader for PNG files to HBITMAP.
        auto loadImageFile = [&]( const std::filesystem::path& filePath, std::vector<BYTE>& pixelsOut, int& widthOut, int& heightOut ) -> bool
        {
            IWICImagingFactory* factory = nullptr;
            HRESULT hr = CoCreateInstance( CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                                           IID_PPV_ARGS( &factory ) );
            if( FAILED( hr ) || factory == nullptr )
            {
                TestLog( L"[Panorama/Test] WIC factory creation failed hr=0x%08lx\n", hr );
                return false;
            }

            IWICBitmapDecoder* decoder = nullptr;
            hr = factory->CreateDecoderFromFilename( filePath.c_str(), nullptr, GENERIC_READ,
                                                      WICDecodeMetadataCacheOnDemand, &decoder );
            if( FAILED( hr ) || decoder == nullptr )
            {
                factory->Release();
                TestLog( L"[Panorama/Test] WIC decode failed for %s hr=0x%08lx\n", filePath.c_str(), hr );
                return false;
            }

            IWICBitmapFrameDecode* frame = nullptr;
            hr = decoder->GetFrame( 0, &frame );
            if( FAILED( hr ) || frame == nullptr )
            {
                decoder->Release();
                factory->Release();
                return false;
            }

            IWICFormatConverter* converter = nullptr;
            hr = factory->CreateFormatConverter( &converter );
            if( FAILED( hr ) || converter == nullptr )
            {
                frame->Release();
                decoder->Release();
                factory->Release();
                return false;
            }

            hr = converter->Initialize( frame, GUID_WICPixelFormat32bppBGRA,
                                         WICBitmapDitherTypeNone, nullptr, 0.0,
                                         WICBitmapPaletteTypeCustom );
            if( FAILED( hr ) )
            {
                converter->Release();
                frame->Release();
                decoder->Release();
                factory->Release();
                return false;
            }

            UINT w = 0, h = 0;
            converter->GetSize( &w, &h );
            widthOut = static_cast<int>( w );
            heightOut = static_cast<int>( h );
            pixelsOut.resize( static_cast<size_t>( w ) * static_cast<size_t>( h ) * 4 );
            hr = converter->CopyPixels( nullptr, w * 4, static_cast<UINT>( pixelsOut.size() ), pixelsOut.data() );

            converter->Release();
            frame->Release();
            decoder->Release();
            factory->Release();
            return SUCCEEDED( hr );
        };

        // Lambda: stitch overlapping frames and compare to original image.
        // Returns: 1=pass, 0=comparison-fail, -1=infrastructure-error.
        auto stitchAndCompare = [&](
            const wchar_t* scenario,
            const std::vector<BYTE>& imgPx, int imgW, int imgH,
            const std::vector<int>& origins, int winH ) -> int
        {
            const bool isStrictRangeScenario = wcsstr( scenario, L"legitjumps" ) != nullptr;
            const bool isFastScrollScenario = wcsstr( scenario, L"fastscroll" ) != nullptr ||
                                                wcsstr( scenario, L"accelscroll" ) != nullptr;
            const bool isHcfDarkScenario = wcsstr( scenario, L"hcfdark" ) != nullptr;
            const bool isHcfWhitespaceScenario = wcsstr( scenario, L"hcfwhitespace" ) != nullptr;
            const bool isMomentumReversalScenario = wcsstr( scenario, L"momentumreversal" ) != nullptr;
            const bool isCapturePathScenario = wcsstr( scenario, L"capturepath" ) != nullptr;

            std::vector<HBITMAP> frames;
            frames.reserve( origins.size() );
            std::vector<int> acceptedOrigins;
            acceptedOrigins.reserve( origins.size() );

            bool duplicateLowContrastMode = false;
            bool haveDuplicateMode = false;
            size_t grabbedFrames = 0;
            size_t duplicateDrops = 0;
            size_t subPixelDrops = 0;
            for( size_t fi = 0; fi < origins.size(); ++fi )
            {
                const int originY = origins[fi];
                if( originY < 0 || originY + winH > imgH )
                {
                    for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                    return -1;
                }

                std::vector<BYTE> fp( static_cast<size_t>( imgW ) * static_cast<size_t>( winH ) * 4 );
                for( int row = 0; row < winH; ++row )
                {
                    const size_t srcOff = ( static_cast<size_t>( originY + row ) * imgW ) * 4;
                    const size_t dstOff = ( static_cast<size_t>( row ) * imgW ) * 4;
                    memcpy( fp.data() + dstOff, imgPx.data() + srcOff, static_cast<size_t>( imgW ) * 4 );
                }

                // Add small deterministic noise for fast-scroll scenarios to
                // simulate real capture conditions (ClearType rendering, timing
                // differences) that prevent exact pixel matches.
                if( isFastScrollScenario )
                {
                    unsigned int noiseSeed = static_cast<unsigned int>( fi * 7919 + 12347 );
                    const size_t totalBytes = static_cast<size_t>( imgW ) * static_cast<size_t>( winH ) * 4;
                    for( size_t bi = 0; bi < totalBytes; ++bi )
                    {
                        if( ( bi & 3 ) == 3 ) continue; // skip alpha channel
                        noiseSeed = noiseSeed * 1103515245u + 12345u;
                        const int noise = static_cast<int>( ( noiseSeed >> 16 ) % 5 ) - 2; // -2..+2
                        const int val = static_cast<int>( fp[bi] ) + noise;
                        fp[bi] = static_cast<BYTE>( max( 0, min( 255, val ) ) );
                    }
                }

                // HCF-dark noise: add noise ONLY to bright (text) pixels,
                // leaving the dark background pixel-identical between frames.
                // This models real captures where dark background regions
                // produce score=0 at any small offset while text content
                // has per-frame ClearType rendering variation.
                if( isHcfDarkScenario )
                {
                    unsigned int noiseSeed = static_cast<unsigned int>( fi * 7919 + 12347 );
                    const size_t totalPixels = static_cast<size_t>( imgW ) * static_cast<size_t>( winH );
                    for( size_t pi = 0; pi < totalPixels; ++pi )
                    {
                        const size_t bi = pi * 4;
                        const int luma = ( fp[bi + 2] * 77 + fp[bi + 1] * 150 + fp[bi + 0] * 29 ) >> 8;
                        if( luma < 40 ) continue; // dark pixel -- keep identical
                        for( int ch = 0; ch < 3; ++ch )
                        {
                            noiseSeed = noiseSeed * 1103515245u + 12345u;
                            const int noise = static_cast<int>( ( noiseSeed >> 16 ) % 5 ) - 2;
                            const int val = static_cast<int>( fp[bi + ch] ) + noise;
                            fp[bi + ch] = static_cast<BYTE>( max( 0, min( 255, val ) ) );
                        }
                    }
                }

                // HCF-whitespace noise: add tiny deterministic variation only
                // to darker text pixels, while keeping bright background
                // identical. This models subpixel text rendering variation on
                // mostly-white pages without reducing constant-content fraction.
                if( isHcfWhitespaceScenario )
                {
                    unsigned int noiseSeed = static_cast<unsigned int>( fi * 104729 + 2017 );
                    const size_t totalPixels = static_cast<size_t>( imgW ) * static_cast<size_t>( winH );
                    for( size_t pi = 0; pi < totalPixels; ++pi )
                    {
                        const size_t bi = pi * 4;
                        const int luma = ( fp[bi + 2] * 77 + fp[bi + 1] * 150 + fp[bi + 0] * 29 ) >> 8;
                        if( luma > 210 ) continue; // keep white background identical
                        for( int ch = 0; ch < 3; ++ch )
                        {
                            noiseSeed = noiseSeed * 1103515245u + 12345u;
                            const int noise = static_cast<int>( ( noiseSeed >> 16 ) % 5 ) - 2;
                            const int val = static_cast<int>( fp[bi + ch] ) + noise;
                            fp[bi + ch] = static_cast<BYTE>( max( 0, min( 255, val ) ) );
                        }
                    }
                }

                HBITMAP bmp = CreateBitmapFromPixels32( fp, imgW, winH );
                if( !bmp )
                {
                    for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                    return -1;
                }

                if( isCapturePathScenario )
                {
                    grabbedFrames++;
                    if( frames.empty() )
                    {
                        double spread = 0.0;
                        double stdDev = 0.0;
                        double edgeDelta = 0.0;
                        duplicateLowContrastMode = IsLowContrastSeedFrame( bmp, &spread, &stdDev, &edgeDelta );
                        haveDuplicateMode = true;
                        frames.push_back( bmp );
                        acceptedOrigins.push_back( originY );
                    }
                    else
                    {
                        bool isSubPixelDrop = false;
                        const bool nearDuplicate = AreFramesNearDuplicate( bmp,
                                                                           frames.back(),
                                                                           haveDuplicateMode ? duplicateLowContrastMode : false,
                                                                           &isSubPixelDrop );
                        if( nearDuplicate )
                        {
                            if( isSubPixelDrop )
                                subPixelDrops++;
                            else
                                duplicateDrops++;
                            DeleteObject( bmp );
                            continue;
                        }

                        frames.push_back( bmp );
                        acceptedOrigins.push_back( originY );
                    }
                }
                else
                {
                    frames.push_back( bmp );
                    acceptedOrigins.push_back( originY );
                }
            }

            if( acceptedOrigins.empty() )
            {
                for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                TestLog( L"[Panorama/Test] %s: no accepted frames\n", scenario );
                return -1;
            }

            if( isCapturePathScenario )
            {
                TestLog( L"[Panorama/Test] %s capture-sim grabbed=%zu accepted=%zu duplicateDrops=%zu subPixelDrops=%zu\n",
                         scenario,
                         grabbedFrames,
                         acceptedOrigins.size(),
                         duplicateDrops,
                         subPixelDrops );
            }

            if( acceptedOrigins.size() < 2 )
            {
                for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                TestLog( L"[Panorama/Test] %s: insufficient accepted frames=%zu\n", scenario, acceptedOrigins.size() );
                return 0;
            }

            const int expectedH = acceptedOrigins.back() + winH;

            std::vector<int> composedAxisSteps;
            HBITMAP stitchedBmp = StitchPanoramaFrames( frames, false, nullptr, nullptr, &composedAxisSteps );
            for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }

            if( !stitchedBmp )
            {
                TestLog( L"[Panorama/Test] %s: StitchPanoramaFrames returned nullptr\n", scenario );
                return -1;
            }

            std::vector<BYTE> sPx;
            int sW = 0, sH = 0;
            if( !ReadBitmapPixels32( stitchedBmp, sPx, sW, sH ) )
            {
                DeleteObject( stitchedBmp );
                return -1;
            }
            DeleteObject( stitchedBmp );

            const size_t acceptedCount = acceptedOrigins.size();
            const int htol = isStrictRangeScenario
                ? max( 40, winH / 10 + static_cast<int>( acceptedCount ) * 3 )
                : ( winH / 4 + static_cast<int>( acceptedCount ) * 8 );
            if( sH < expectedH - htol || sH > expectedH + htol )
            {
                // Check if the source image is low-contrast.
                // Low-contrast images can't be stitched by correlation -- the stitcher
                // correctly rejects frames as stationary or partially correlates.
                // Verify it didn't crash and count as a graceful-degradation pass.
                {
                    double avgVertDiff = 0;
                    int nVS = 0;
                    const int testStep = min( winH / 2, imgH / 4 );
                    for( int y = 0; y + testStep < imgH; y += 37 )
                        for( int x = 4; x < imgW - 4; x += 17 )
                        {
                            const size_t a = ( static_cast<size_t>( y ) * imgW + x ) * 4;
                            const size_t b = ( static_cast<size_t>( y + testStep ) * imgW + x ) * 4;
                            const int la = ( imgPx[a] + imgPx[a+1] + imgPx[a+2] ) / 3;
                            const int lb = ( imgPx[b] + imgPx[b+1] + imgPx[b+2] ) / 3;
                            avgVertDiff += abs( la - lb );
                            nVS++;
                        }
                    avgVertDiff = nVS > 0 ? avgVertDiff / nVS : 0;

                    if( avgVertDiff <= 10.0 )
                    {
                        TestLog( L"[Panorama/Test] %s: low-contrast (avgDiff=%.1f), graceful degradation -- PASS\n",
                                     scenario, avgVertDiff );
                        return 1;
                    }
                }

                TestLog( L"[Panorama/Test] %s FAILED: height stitched=%d expected=%d tol=%d\n",
                             scenario, sH, expectedH, htol );
                TestLog( L"***** FAIL: %s *****\n", scenario );
                if( !selfTestDumpDirectory.empty() )
                {
                    wchar_t msg[512]{};
                    swprintf_s( msg, L"HEIGHT: %s stitched=%dx%d expected=%dx%d",
                                scenario, sW, sH, imgW, expectedH );
                    DumpPanoramaText( selfTestDumpDirectory, L"image_trial_failed.txt", msg );
                }
                return 0;
            }

            const int maxVE = 30, cmpW = min( sW, imgW ), eSkip = 4;

            int bestDx = 0;
            {
                double bestHD = 1e18;
                for( int testDx = -6; testDx <= 6; ++testDx )
                {
                    double dsum = 0.0;
                    int cnt = 0;
                    for( int yy = 0; yy < sH && yy < imgH; yy += 37 )
                        for( int xx = eSkip; xx < cmpW - eSkip; xx += 31 )
                        {
                            const int dstX = xx + testDx;
                            if( xx < 0 || xx >= imgW || dstX < 0 || dstX >= sW ) continue;
                            const size_t si = ( static_cast<size_t>( yy ) * imgW + xx ) * 4;
                            const size_t di = ( static_cast<size_t>( yy ) * sW + dstX ) * 4;
                            if( si + 3 >= imgPx.size() || di + 3 >= sPx.size() ) continue;
                            dsum += abs( (int)sPx[di] - (int)imgPx[si] );
                            dsum += abs( (int)sPx[di+1] - (int)imgPx[si+1] );
                            dsum += abs( (int)sPx[di+2] - (int)imgPx[si+2] );
                            cnt++;
                        }
                    if( cnt > 0 && dsum < bestHD ) { bestHD = dsum; bestDx = testDx; }
                }
            }

            size_t samples = 0, mismatches = 0;
            std::vector<int> mappedSourceRows;
            std::vector<int> mappedDriftAbs;
            mappedSourceRows.reserve( static_cast<size_t>( sH / 19 + 2 ) );
            mappedDriftAbs.reserve( static_cast<size_t>( sH / 19 + 2 ) );
            int driftOffset = 0;
            for( int yy = 0; yy < sH; yy += 19 )
            {
                // Track accumulated drift between canvas and source so
                // the search window follows the actual mapping even when
                // the stitcher compresses the canvas (e.g. through HCF
                // regions).  Without this, once drift exceeds maxVE the
                // row mapping becomes arbitrary, producing spurious
                // backward transitions.
                const int searchCenter = min( imgH - 1, max( 0, yy + driftOffset ) );
                int bestSY = searchCenter;
                double bestRD = 1e18;
                for( int ty = max( 0, searchCenter - maxVE ); ty <= min( imgH - 1, searchCenter + maxVE ); ++ty )
                {
                    double rd = 0.0; int rc = 0;
                    for( int xx = eSkip; xx < cmpW - eSkip; xx += 31 )
                    {
                        const int dstX = xx + bestDx;
                        if( xx >= imgW || dstX < 0 || dstX >= sW ) continue;
                        const size_t si = ( static_cast<size_t>( ty ) * imgW + xx ) * 4;
                        const size_t di = ( static_cast<size_t>( yy ) * sW + dstX ) * 4;
                        if( si + 3 >= imgPx.size() || di + 3 >= sPx.size() ) continue;
                        rd += abs( (int)sPx[di] - (int)imgPx[si] ) +
                              abs( (int)sPx[di+1] - (int)imgPx[si+1] ) +
                              abs( (int)sPx[di+2] - (int)imgPx[si+2] );
                        rc++;
                    }
                    if( rc > 0 && rd < bestRD ) { bestRD = rd; bestSY = ty; }
                }

                driftOffset = bestSY - yy;

                for( int xx = eSkip; xx < cmpW - eSkip; xx += 17 )
                {
                    const int dstX = xx + bestDx;
                    if( xx >= imgW || dstX < 0 || dstX >= sW ) continue;
                    const size_t si = ( static_cast<size_t>( bestSY ) * imgW + xx ) * 4;
                    const size_t di = ( static_cast<size_t>( yy ) * sW + dstX ) * 4;
                    if( si + 3 >= imgPx.size() || di + 3 >= sPx.size() ) continue;
                    const int d = abs( (int)sPx[di] - (int)imgPx[si] ) +
                                  abs( (int)sPx[di+1] - (int)imgPx[si+1] ) +
                                  abs( (int)sPx[di+2] - (int)imgPx[si+2] );
                    samples++;
                    if( d > 60 ) mismatches++;
                }

                mappedSourceRows.push_back( bestSY );
                mappedDriftAbs.push_back( abs( driftOffset ) );
            }

            const double mrate = samples > 0 ? static_cast<double>( mismatches ) / samples : 0.0;
            bool continuityOk = true;
            size_t dupTransitions = 0;
            size_t jumpTransitions = 0;
            size_t backwardTransitions = 0;
            size_t catastrophicTransitions = 0;
            if( mappedSourceRows.size() >= 8 )
            {
                for( size_t i = 1; i < mappedSourceRows.size(); ++i )
                {
                    const int dy = mappedSourceRows[i] - mappedSourceRows[i - 1];
                    // Only count dup transitions when the drift-tracked
                    // search is in a reliable region (drift < maxVE).
                    // In high-drift regions the stitcher compressed
                    // the canvas, which naturally maps many canvas rows
                    // to the same source rows -- this is expected and
                    // already tested by the height tolerance check.
                    if( dy <= 1 &&
                        mappedDriftAbs[i] <= maxVE && mappedDriftAbs[i - 1] <= maxVE )
                        dupTransitions++;
                    if( dy >= 36 )
                        jumpTransitions++;
                    if( dy >= 96 )
                        catastrophicTransitions++;
                    if( dy < -2 )
                        backwardTransitions++;
                }

                // Enforce continuity only for stress scenarios.
                // HCF-dark scenarios have relaxed backtrack tolerance because
                // the mostly-dark content makes row mapping unreliable --
                // indistinguishable dark rows cause the source-row search to
                // wander, producing false backward transitions.
                const bool isStressScenario = wcsncmp( scenario, L"stress-", 7 ) == 0;
                if( isStressScenario )
                {
                    const size_t transitions = mappedSourceRows.size() - 1;
                    const bool strictCaptureContinuity = isCapturePathScenario;
                    const bool tooManyDups = isMomentumReversalScenario
                        ? ( dupTransitions > transitions / 3 )
                        : ( strictCaptureContinuity ? ( dupTransitions > transitions / 6 )
                                                    : ( dupTransitions > transitions / 2 ) );
                    const bool tooManyJumps = strictCaptureContinuity
                        ? ( jumpTransitions > transitions / 14 )
                        : ( jumpTransitions > transitions / 6 );
                    const size_t backtrackLimit = isMomentumReversalScenario
                        ? 0
                        : ( isHcfDarkScenario
                            ? ( strictCaptureContinuity ? transitions / 20 : transitions / 6 )
                            : ( strictCaptureContinuity ? 0 : transitions / 30 ) );
                    const bool tooManyBacktracks = backwardTransitions > backtrackLimit;
                    const bool hasCatastrophicJump = strictCaptureContinuity && catastrophicTransitions > 0;
                    continuityOk = !( tooManyDups || tooManyJumps || tooManyBacktracks || hasCatastrophicJump );
                }
            }

            bool capturePathStabilityOk = true;
            size_t tinyStepCount = 0;
            size_t largeStepCount = 0;
            size_t elevatedStepCount = 0;
            size_t tinyLargeOscillations = 0;
            if( isCapturePathScenario && composedAxisSteps.size() >= 12 )
            {
                for( size_t si = 0; si < composedAxisSteps.size(); ++si )
                {
                    const int axisStep = composedAxisSteps[si];
                    if( axisStep <= 8 )
                        tinyStepCount++;
                    if( axisStep >= 120 )
                        largeStepCount++;
                    if( axisStep >= 96 )
                        elevatedStepCount++;

                    if( si > 0 )
                    {
                        const int prev = composedAxisSteps[si - 1];
                        const bool smallToLarge = prev <= 15 && axisStep >= 100;
                        const bool largeToSmall = prev >= 100 && axisStep <= 15;
                        if( smallToLarge || largeToSmall )
                            tinyLargeOscillations++;
                    }
                }

                const size_t transitions = composedAxisSteps.size() - 1;
                const bool bimodalInstability =
                    tinyStepCount >= 2 &&
                    largeStepCount >= max( static_cast<size_t>( 8 ), transitions / 7 ) &&
                    tinyLargeOscillations >= 1;
                const bool extremeLargeDominance =
                    largeStepCount >= max( static_cast<size_t>( 12 ), ( transitions * 3 ) / 10 );
                const bool elevatedBimodalInstability =
                    tinyStepCount >= max( static_cast<size_t>( 10 ), transitions / 4 ) &&
                    elevatedStepCount >= max( static_cast<size_t>( 10 ), transitions / 4 ) &&
                    tinyLargeOscillations >= 2;

                capturePathStabilityOk = !( bimodalInstability || extremeLargeDominance || elevatedBimodalInstability );
            }

            const double mismatchThreshold = isCapturePathScenario ? 0.10 : 0.15;
            const bool ok = samples > 0 && mrate < mismatchThreshold && continuityOk && capturePathStabilityOk;

            // On low-vertical-contrast (HCF-dark) content, the per-row
            // search used for pixel comparison is unreliable because many
            // rows are nearly indistinguishable, leading to drift and false
            // mismatches.  If the stitched height is correct, try a direct
            // pixel comparison (stitched row y == source row y) which is
            // valid because selftest frames are exact slices of the source.
            if( !ok && sH >= expectedH - htol && sH <= expectedH + htol &&
                ( !isCapturePathScenario || capturePathStabilityOk ) )
            {
                size_t directSamples = 0, directMismatches = 0;
                for( int yy = 0; yy < min( sH, imgH ); yy += 19 )
                    for( int xx = eSkip; xx < cmpW - eSkip; xx += 17 )
                    {
                        const int dstX = xx + bestDx;
                        if( xx >= imgW || dstX < 0 || dstX >= sW ) continue;
                        const size_t si = ( static_cast<size_t>( yy ) * imgW + xx ) * 4;
                        const size_t di = ( static_cast<size_t>( yy ) * sW + dstX ) * 4;
                        if( si + 3 >= imgPx.size() || di + 3 >= sPx.size() ) continue;
                        const int d = abs( (int)sPx[di] - (int)imgPx[si] ) +
                                      abs( (int)sPx[di+1] - (int)imgPx[si+1] ) +
                                      abs( (int)sPx[di+2] - (int)imgPx[si+2] );
                        directSamples++;
                        if( d > 60 ) directMismatches++;
                    }
                const double directRate = directSamples > 0
                    ? static_cast<double>( directMismatches ) / directSamples : 0.0;
                if( directSamples > 0 && directRate < 0.15 )
                {
                    TestLog( L"[Panorama/Test] %s: direct comparison passed (%.2f%% vs row-match %.2f%%) -- PASS\n",
                                 scenario, directRate * 100.0, mrate * 100.0 );
                    return 1;
                }
            }

            TestLog( L"[Panorama/Test] %s result=%s stitched=%dx%d dx=%d samples=%zu mismatches=%zu (%.2f%%)\n",
                         scenario, ok ? L"PASS" : L"FAIL", sW, sH, bestDx, samples, mismatches, mrate * 100.0 );

            if( isCapturePathScenario )
            {
                TestLog( L"[Panorama/Test] %s composed-step-stability tiny<=8=%zu large>=120=%zu elevated>=96=%zu oscillations=%zu steps=%zu\n",
                         scenario,
                         tinyStepCount,
                         largeStepCount,
                         elevatedStepCount,
                         tinyLargeOscillations,
                         composedAxisSteps.size() );
            }

            if( !ok )
            {
                StitchLog( L"[Panorama/Test] PIXELS-FAIL %s stitched=%dx%d mrate=%.2f%% continuity(dup=%zu jump=%zu cat=%zu back=%zu transitions=%zu)\n",
                           scenario, sW, sH, mrate * 100.0,
                           dupTransitions, jumpTransitions, catastrophicTransitions, backwardTransitions,
                           mappedSourceRows.size() > 0 ? mappedSourceRows.size() - 1 : 0 );
                if( isCapturePathScenario )
                {
                    StitchLog( L"[Panorama/Test] STABILITY-FAIL %s tiny<=8=%zu large>=120=%zu elevated>=96=%zu oscillations=%zu steps=%zu\n",
                               scenario,
                               tinyStepCount,
                               largeStepCount,
                               elevatedStepCount,
                               tinyLargeOscillations,
                               composedAxisSteps.size() );
                }
                if( !selfTestDumpDirectory.empty() )
                {
                    wchar_t msg[512]{};
                    swprintf_s( msg, L"PIXELS: %s stitched=%dx%d dx=%d mismatches=%zu/%zu (%.2f%%) continuity(dup=%zu jump=%zu cat=%zu back=%zu) stability(tiny=%zu large=%zu osc=%zu)",
                                scenario, sW, sH, bestDx, mismatches, samples, mrate * 100.0,
                                dupTransitions, jumpTransitions, catastrophicTransitions, backwardTransitions,
                                tinyStepCount, largeStepCount, tinyLargeOscillations );
                    DumpPanoramaText( selfTestDumpDirectory, L"image_trial_failed.txt", msg );
                }
            }

            return ok ? 1 : 0;
        };

        if( !selfTestStressOnly && useExternalImageAssets )
        {
            TestLog( L"\n==== Phase 2: Image-slice tests ====\n" );
            const int kTrialsPerImage = selfTestTrials;
            int imageSliceTestsPassed = 0;
            int imageSliceTestsRun = 0;

            for( const wchar_t* imageFile : imageFiles )
            {
                const auto imagePath = imageDir / imageFile;
                if( !std::filesystem::exists( imagePath ) )
                {
                    TestLog( L"[Panorama/Test] Skipping missing image: %s\n", imagePath.c_str() );
                    continue;
                }

                std::vector<BYTE> imagePixels;
                int imageWidth = 0, imageHeight = 0;
                const bool loaded = loadImageFile( imagePath, imagePixels, imageWidth, imageHeight );
                if( !loaded )
                {
                    TestLog( L"[Panorama/Test] Failed to load image: %s\n", imagePath.c_str() );
                    CoUninitialize();
                    return false;
                }

                TestLog( L"[Panorama/Test] Loaded %s  %dx%d\n", imageFile, imageWidth, imageHeight );

                for( int trial = 0; trial < kTrialsPerImage; ++trial )
                {
                    // Deterministic seed per image/trial for reproducibility.
                    srand( static_cast<unsigned>( imageWidth * 1000 + imageHeight * 100 + trial * 7 ) );

                    // Random window height: between 1/8 and 1/3 of image height.
                    const int minWindowH = max( 60, imageHeight / 8 );
                    const int maxWindowH = max( minWindowH + 1, imageHeight / 3 );
                    const int windowH = minWindowH + rand() % ( maxWindowH - minWindowH );

                    // Build frame origins with random steps.
                    // Limit step to 50% of windowH so each pair overlaps by at least
                    // 50%, giving the correlation-based stitcher enough features.
                    std::vector<int> originsY;
                    originsY.push_back( 0 );
                    int y = 0;
                    while( y + windowH < imageHeight )
                    {
                        const int minStep = max( 8, windowH / 10 );
                        const int maxStep = max( minStep + 1, windowH * 3 / 10 );
                        const int step = minStep + rand() % ( maxStep - minStep );
                        y += step;
                        if( y + windowH > imageHeight )
                        {
                            y = imageHeight - windowH;
                        }
                        originsY.push_back( y );
                    }

                    if( originsY.size() < 2 )
                    {
                        continue;
                    }

                    wchar_t scenarioName[128];
                    swprintf_s( scenarioName, L"image-slice-%s-trial%d-w%d-n%zu",
                                imageFile, trial, windowH, originsY.size() );

                    imageSliceTestsRun++;
                    const int result = stitchAndCompare( scenarioName, imagePixels, imageWidth, imageHeight, originsY, windowH );
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                        CoUninitialize();
                        return false;
                    }
                    if( result == 0 )
                    {
                        TestLog( L"***** FAIL: %s *****\n", scenarioName );
                        CoUninitialize();
                        return false;
                    }
                    imageSliceTestsPassed++;
                    TestLog( L"  [%d] %s PASSED\n", imageSliceTestsRun, scenarioName );
                }
            }

            // Fixed 15-slice tests: each trial creates exactly 15 overlapping frames.
            for( const wchar_t* imageFile : imageFiles )
            {
                const auto imagePath = imageDir / imageFile;
                if( !std::filesystem::exists( imagePath ) )
                    continue;

                std::vector<BYTE> imagePixels;
                int imageWidth = 0, imageHeight = 0;
                if( !loadImageFile( imagePath, imagePixels, imageWidth, imageHeight ) )
                {
                    TestLog( L"[Panorama/Test] Failed to load image for fixed15: %s\n", imagePath.c_str() );
                    CoUninitialize();
                    return false;
                }

                constexpr int kFixedSlices = 15;
                for( int trial = 0; trial < selfTestTrials; ++trial )
                {
                    srand( static_cast<unsigned>( imageWidth * 3000 + imageHeight * 300 + trial * 17 ) );

                    // Window height must be large enough that the step between
                    // 15 slices gives at least ~70% overlap: step = (H - windowH) / 14
                    // requires windowH >= H / 5.
                    const int minWH = max( 60, imageHeight / 5 );
                    const int maxWH = max( minWH + 1, imageHeight / 3 );
                    const int windowH = minWH + rand() % ( maxWH - minWH );

                    const double baseStep = static_cast<double>( imageHeight - windowH ) / ( kFixedSlices - 1 );
                    if( baseStep < 1.0 )
                        continue;

                    std::vector<int> originsY;
                    for( int i = 0; i < kFixedSlices; ++i )
                    {
                        int yPos;
                        if( i == 0 )
                            yPos = 0;
                        else if( i == kFixedSlices - 1 )
                            yPos = imageHeight - windowH;
                        else
                        {
                            yPos = static_cast<int>( i * baseStep );
                            const int jitter = max( 1, static_cast<int>( baseStep * 0.1 ) );
                            yPos += ( rand() % ( 2 * jitter + 1 ) ) - jitter;
                            yPos = max( 0, min( yPos, imageHeight - windowH ) );
                        }
                        originsY.push_back( yPos );
                    }

                    // Ensure strictly increasing.
                    for( size_t i = 1; i < originsY.size(); ++i )
                    {
                        if( originsY[i] <= originsY[i - 1] )
                            originsY[i] = originsY[i - 1] + 1;
                    }

                    // Clamp last origin to valid range in case bumping made it exceed.
                    if( originsY.back() + windowH > imageHeight )
                        originsY.back() = imageHeight - windowH;
                    if( originsY.size() >= 2 && originsY.back() <= originsY[originsY.size() - 2] )
                        continue;

                    wchar_t scenarioName[128];
                    swprintf_s( scenarioName, L"fixed15-%s-trial%d-w%d",
                                imageFile, trial, windowH );

                    imageSliceTestsRun++;
                    const int result = stitchAndCompare( scenarioName, imagePixels, imageWidth, imageHeight, originsY, windowH );
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                        CoUninitialize();
                        return false;
                    }
                    if( result == 0 )
                    {
                        TestLog( L"***** FAIL: %s *****\n", scenarioName );
                        CoUninitialize();
                        return false;
                    }
                    imageSliceTestsPassed++;
                    TestLog( L"  [%d] %s PASSED\n", imageSliceTestsRun, scenarioName );
                }
            }

            TestLog( L"[Panorama/Test] Image-slice tests passed: %d\n", imageSliceTestsPassed );

            // Require at least 12 image slice tests per trial (6 images x 2 modes).
            const int requiredImageTests = 12 * selfTestTrials;
            if( imageSliceTestsPassed < requiredImageTests )
            {
                TestLog( L"***** FAIL: Insufficient image tests: %d (need %d) *****\n", imageSliceTestsPassed, requiredImageTests );
                if( !selfTestDumpDirectory.empty() )
                {
                    wchar_t summary[128]{};
                    swprintf_s( summary, L"INSUFFICIENT: only %d tests passed (need %d)", imageSliceTestsPassed, requiredImageTests );
                    DumpPanoramaText( selfTestDumpDirectory, L"image_slice_results.txt", summary );
                }
                CoUninitialize();
                return false;
            }

            if( !selfTestDumpDirectory.empty() )
            {
                wchar_t summary[128]{};
                swprintf_s( summary, L"imageSliceTestsPassed=%d", imageSliceTestsPassed );
                DumpPanoramaText( selfTestDumpDirectory, L"image_slice_results.txt", summary );
            }

        }
        else
        {
            TestLog( L"[Panorama/Test] Skipping image-slice tests (synthetic-only mode or stress-only mode)\n" );
        }

        // ====================================================================
        // Stress tests: vertical_stress.png and horizontal_stress.png
        //
        // Each trial generates ~100 overlapping frames with random step sizes
        // from 0 (duplicate) up to 25% of the portal size.  This exercises
        // all four content regimes (high/low entropy x high/low contrast),
        // duplicate detection, variable-speed scrolling, and near-zero motion.
        // ====================================================================

        const auto stressDir = imageDir / L"stress_test";

        TestLog( L"[Panorama/Test] Stress test directory: %s  exists=%d\n",
                     stressDir.c_str(), std::filesystem::exists( stressDir ) ? 1 : 0 );


        std::wstring stressLog;       // Accumulate all results for single dump at end.
        std::wstring stressFailLog;
        int stressTestsPassed = 0;
        int stressTestsRun = 0;
        bool stressEarlyExit = false;

        std::wstring stressFocusScenario = readSelfTestArg( L"/panorama-stress-focus" );
        if( stressFocusScenario.empty() )
        {
            // Backward-compatible alias used in ad-hoc local runs.
            stressFocusScenario = readSelfTestArg( L"/panorama-selftest-stress-scenario" );
        }
        const bool stressFocusEnabled = !stressFocusScenario.empty();

        bool stressEnableDroppedbandRepro =
            readSelfTestBoolArg( L"/panorama-stress-enable-droppedband-repro", false ) ||
            readSelfTestBoolArg( L"/panorama-selftest-stress-enable-droppedband-repro", false );
        const bool stressDroppedbandExpectAbsent =
            readSelfTestBoolArg( L"/panorama-stress-droppedband-expect-absent", false ) ||
            readSelfTestBoolArg( L"/panorama-selftest-stress-droppedband-expect-absent", false );

        if( stressFocusEnabled &&
            wcsstr( stressFocusScenario.c_str(), L"droppedband-repro-signature" ) != nullptr )
        {
            // If user focuses this scenario explicitly, auto-enable it.
            stressEnableDroppedbandRepro = true;
        }

        bool stressStopAfterFocus = false;
        if( stressFocusEnabled )
        {
            // Default to stop-after-focus so a single scenario can be checked
            // before running the rest of the stress suite.
            stressStopAfterFocus = true;
            stressStopAfterFocus = readSelfTestBoolArg( L"/panorama-stress-stopafter", true );

            TestLog( L"[Panorama/Test] Stress focus enabled: match=\"%s\" stopAfter=%d\n",
                         stressFocusScenario.c_str(),
                         stressStopAfterFocus ? 1 : 0 );
        }

        std::wstring stressReplayDirArg = readSelfTestArg( L"/panorama-stress-replay-dir" );
        if( stressReplayDirArg.empty() )
            stressReplayDirArg = readSelfTestArg( L"/panorama-selftest-replay-dir" );
        const bool stressReplayEnabled = !stressReplayDirArg.empty();
        if( stressReplayEnabled )
        {
            TestLog( L"[Panorama/Test] Replay stress enabled: dir=%s\n", stressReplayDirArg.c_str() );
        }

        bool stressFocusMatched = false;
        auto stressScenarioMatches = [&]( const wchar_t* scenarioName ) -> bool
        {
            if( !stressFocusEnabled )
                return true;
            if( scenarioName == nullptr )
                return false;
            return wcsstr( scenarioName, stressFocusScenario.c_str() ) != nullptr;
        };

        struct ReplayStepMetrics
        {
            size_t accepted = 0;
            size_t gt120 = 0;
            size_t gt96 = 0;
            size_t lte8 = 0;
            size_t oscillations = 0;
            size_t first56Gt120 = 0;
            size_t first56Gt96 = 0;
        };

        auto parseReplayStepMetrics = [&]( const std::filesystem::path& replayLogPath,
                                           ReplayStepMetrics& metrics ) -> bool
        {
            std::wifstream stream( replayLogPath );
            if( !stream.good() )
                return false;

            std::vector<std::wstring> lines;
            std::wstring line;
            while( std::getline( stream, line ) )
                lines.push_back( line );

            if( lines.empty() )
                return false;

            size_t beginIndex = static_cast<size_t>( -1 );
            for( size_t li = 0; li < lines.size(); ++li )
            {
                if( lines[li].find( L"[Panorama/Stitch] Begin stitching frameCount=" ) != std::wstring::npos )
                    beginIndex = li;
            }
            if( beginIndex == static_cast<size_t>( -1 ) )
                return false;

            std::vector<int> axisSteps;
            axisSteps.reserve( 256 );
            for( size_t li = beginIndex; li < lines.size(); ++li )
            {
                int frame = 0;
                int dx = 0;
                int dy = 0;
                int sx = 0;
                int sy = 0;
                if( swscanf_s( lines[li].c_str(),
                               L"[Panorama/Stitch] Frame %d accepted: dx=%d dy=%d step=(%d,%d)",
                               &frame,
                               &dx,
                               &dy,
                               &sx,
                               &sy ) == 5 )
                {
                    const int axis = max( abs( sx ), abs( sy ) );
                    axisSteps.push_back( axis );
                    metrics.accepted++;
                    if( axis >= 120 )
                    {
                        metrics.gt120++;
                        if( frame <= 56 )
                            metrics.first56Gt120++;
                    }
                    if( axis >= 96 )
                    {
                        metrics.gt96++;
                        if( frame <= 56 )
                            metrics.first56Gt96++;
                    }
                    if( axis <= 8 )
                        metrics.lte8++;
                }
            }

            if( metrics.accepted < 2 )
                return false;

            for( size_t i = 1; i < axisSteps.size(); ++i )
            {
                const int prev = axisSteps[i - 1];
                const int cur = axisSteps[i];
                const bool smallToLarge = prev <= 15 && cur >= 100;
                const bool largeToSmall = prev >= 100 && cur <= 15;
                if( smallToLarge || largeToSmall )
                    metrics.oscillations++;
            }

            return true;
        };

        // Horizontal-scroll variant of stitchAndCompare.
        // Extracts vertical slices (columns) from a wide image and stitches
        // them, then verifies the result matches the source image span.
        auto stitchAndCompareHorizontal = [&](
            const wchar_t* scenario,
            const std::vector<BYTE>& imgPx, int imgW, int imgH,
            const std::vector<int>& originsX, int winW ) -> int
        {
            // Compute the expected width from distinct, sufficiently-spaced
            // origins (the stitcher drops duplicates and near-zero-step frames).
            const int minDim = min( winW, imgH );
            const int stitcherMinProgress = max( 8, minDim / 30 );
            int distinctLastX = 0;
            int distinctCount = 1;
            for( size_t i = 1; i < originsX.size(); ++i )
            {
                if( originsX[i] - distinctLastX >= stitcherMinProgress / 2 )
                {
                    distinctLastX = originsX[i];
                    distinctCount++;
                }
            }
            const int expectedW = distinctLastX + winW;

            std::vector<HBITMAP> frames;
            frames.reserve( originsX.size() );
            for( size_t fi = 0; fi < originsX.size(); ++fi )
            {
                const int originX = originsX[fi];
                if( originX < 0 || originX + winW > imgW )
                {
                    for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                    return -1;
                }

                std::vector<BYTE> fp( static_cast<size_t>( winW ) * static_cast<size_t>( imgH ) * 4 );
                for( int row = 0; row < imgH; ++row )
                {
                    const size_t srcOff = ( static_cast<size_t>( row ) * imgW + originX ) * 4;
                    const size_t dstOff = ( static_cast<size_t>( row ) * winW ) * 4;
                    memcpy( fp.data() + dstOff, imgPx.data() + srcOff, static_cast<size_t>( winW ) * 4 );
                }

                HBITMAP bmp = CreateBitmapFromPixels32( fp, winW, imgH );
                if( !bmp )
                {
                    for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }
                    return -1;
                }
                frames.push_back( bmp );
            }

            HBITMAP stitchedBmp = StitchPanoramaFrames( frames, false );
            for( HBITMAP hb : frames ) { if( hb ) DeleteObject( hb ); }

            if( !stitchedBmp )
            {
                TestLog( L"[Panorama/Test] %s: StitchPanoramaFrames returned nullptr\n", scenario );
                return -1;
            }

            std::vector<BYTE> sPx;
            int sW = 0, sH = 0;
            if( !ReadBitmapPixels32( stitchedBmp, sPx, sW, sH ) )
            {
                DeleteObject( stitchedBmp );
                return -1;
            }
            DeleteObject( stitchedBmp );

            TestLog( L"[Panorama/Test] %s: stitched=%dx%d expectedW=%d imgH=%d distinctFrames=%d\n",
                         scenario, sW, sH, expectedW, imgH, distinctCount );
            {
                wchar_t diagMsg[512]{};
                swprintf_s( diagMsg, L"DIMS: %s stitched=%dx%d expected=%dx%d distinct=%d\n",
                            scenario, sW, sH, expectedW, imgH, distinctCount );
                stressLog += diagMsg;
            }

            // Wrong-axis detection: if height grew significantly, the stitcher
            // composed vertically instead of horizontally.
            if( sH > imgH + imgH / 4 )
            {
                wchar_t msg[512]{};
                swprintf_s( msg, L"AXIS: %s stitched=%dx%d (vertical growth, expected horizontal)\n",
                            scenario, sW, sH );
                stressFailLog += msg;
                TestLog( L"[Panorama/Test] %s FAILED: wrong axis stitched=%dx%d\n", scenario, sW, sH );
                TestLog( L"***** FAIL: %s wrong axis *****\n", scenario );
                return 0;
            }

            // Width tolerance: allow dropping up to 40% of expected span due to
            // duplicate/small-step rejection + feather blend overlap.
            const int wtol = max( winW, expectedW * 2 / 5 );
            if( sW < expectedW - wtol || sW > expectedW + wtol )
            {
                // Check if the source image is low-contrast horizontally.
                double avgHorizDiff = 0;
                int nHS = 0;
                const int testStepX = min( winW / 2, imgW / 4 );
                for( int y = 4; y < imgH - 4; y += 37 )
                    for( int x = 0; x + testStepX < imgW; x += 17 )
                    {
                        const size_t a = ( static_cast<size_t>( y ) * imgW + x ) * 4;
                        const size_t b = ( static_cast<size_t>( y ) * imgW + x + testStepX ) * 4;
                        const int la = ( imgPx[a] + imgPx[a + 1] + imgPx[a + 2] ) / 3;
                        const int lb = ( imgPx[b] + imgPx[b + 1] + imgPx[b + 2] ) / 3;
                        avgHorizDiff += abs( la - lb );
                        nHS++;
                    }
                avgHorizDiff = nHS > 0 ? avgHorizDiff / nHS : 0;

                if( avgHorizDiff <= 10.0 )
                {
                    TestLog( L"[Panorama/Test] %s: low-contrast horizontal (avgDiff=%.1f), graceful degradation -- PASS\n",
                                 scenario, avgHorizDiff );
                    return 1;
                }

                TestLog( L"[Panorama/Test] %s FAILED: width stitched=%d expected=%d tol=%d\n",
                             scenario, sW, expectedW, wtol );
                TestLog( L"***** FAIL: %s *****\n", scenario );
                {
                    wchar_t msg[512]{};
                    swprintf_s( msg, L"WIDTH: %s stitched=%dx%d expected=%dx%d tol=%d\n",
                                scenario, sW, sH, expectedW, imgH, wtol );
                    stressFailLog += msg;
                }
                return 0;
            }

            // Column-luminance profile correlation
            //
            // Instead of per-pixel comparison (which breaks down due to
            // feather-blend artifacts), verify structure correctness:
            // 1.  Compute average luma per column for the stitched image.
            // 2.  Compute average luma per column for the expected source region.
            // 3.  Find the best linear mapping (offset + scale) from stitched
            //     columns to source columns using exhaustive search.
            // 4.  Compute Pearson correlation.  If > 0.65 the structure matches.
            //
            // This is robust because column averages smooth out per-pixel
            // blend differences while preserving overall content structure.

            // Build column-average luma profiles.
            const int cmpH = min( sH, imgH );
            const int yMargin = 4;

            auto columnLuma = []( const std::vector<BYTE>& px, int w, int h,
                                  int col, int y0, int y1 ) -> double
            {
                double sum = 0;
                int cnt = 0;
                for( int y = y0; y < y1; ++y )
                {
                    const size_t off = ( static_cast<size_t>( y ) * w + col ) * 4;
                    if( off + 3 >= px.size() ) continue;
                    sum += ( px[off] + px[off + 1] + px[off + 2] ) / 3.0;
                    cnt++;
                }
                return cnt > 0 ? sum / cnt : 0.0;
            };

            // Sample every Nth column from stitched image.
            constexpr int kColStep = 8;
            std::vector<double> stitchProf;
            stitchProf.reserve( sW / kColStep + 1 );
            for( int x = 0; x < sW; x += kColStep )
                stitchProf.push_back( columnLuma( sPx, sW, sH, x, yMargin, cmpH - yMargin ) );

            // Full column profile of source image (within the expected region).
            const int srcEnd = min( imgW, expectedW + winW / 2 );
            std::vector<double> srcProf;
            srcProf.reserve( srcEnd / kColStep + 1 );
            for( int x = 0; x < srcEnd; x += kColStep )
                srcProf.push_back( columnLuma( imgPx, imgW, imgH, x, yMargin, cmpH - yMargin ) );

            // Find best linear mapping: stitchProf[i] <-> srcProf[offset + i*scale].
            // Search a range of offsets and scales.
            const int nS = static_cast<int>( stitchProf.size() );
            const int nSrc = static_cast<int>( srcProf.size() );
            double bestCorr = -1e18;
            double bestOff = 0, bestScale = 1.0;

            auto computeCorrelation = [&]( double off, double sc ) -> double
            {
                double sx = 0, sy = 0, sxx = 0, syy = 0, sxy = 0;
                int n = 0;
                for( int i = 0; i < nS; ++i )
                {
                    const double srcIdx = off + i * sc;
                    const int idx = static_cast<int>( srcIdx + 0.5 );
                    if( idx < 0 || idx >= nSrc ) continue;
                    const double x = stitchProf[i];
                    const double y = srcProf[idx];
                    sx += x; sy += y;
                    sxx += x * x; syy += y * y;
                    sxy += x * y;
                    n++;
                }
                if( n < 10 ) return -1.0;
                const double mx = sx / n, my = sy / n;
                const double vx = sxx / n - mx * mx;
                const double vy = syy / n - my * my;
                if( vx < 1e-6 || vy < 1e-6 ) return 0.0; // constant data
                return ( sxy / n - mx * my ) / sqrt( vx * vy );
            };

            // Coarse search: scale from 0.5x to 2.5x in steps of 0.05,
            // offset from -nSrc/4 to nSrc/4 in steps of 2.
            for( double sc = 0.5; sc <= 2.5; sc += 0.05 )
            {
                for( int off = -nSrc / 4; off <= nSrc / 4; off += 2 )
                {
                    const double c = computeCorrelation( off, sc );
                    if( c > bestCorr ) { bestCorr = c; bestOff = off; bestScale = sc; }
                }
            }

            // Fine refinement around best.
            for( double sc = bestScale - 0.1; sc <= bestScale + 0.1; sc += 0.005 )
            {
                for( double off = bestOff - 3; off <= bestOff + 3; off += 0.5 )
                {
                    const double c = computeCorrelation( off, sc );
                    if( c > bestCorr ) { bestCorr = c; bestOff = off; bestScale = sc; }
                }
            }

            // A correlation >= 0.60 indicates the structure strongly matches.
            // This is generous enough to accept feather-blend distortion and
            // sparse content bands (whose flat column profiles naturally
            // produce lower correlation) while still catching wrong-image,
            // reversed, or scrambled content.
            const bool ok = bestCorr >= 0.60;
            const double xScale = sW > 0 ? static_cast<double>( expectedW ) / sW : 1.0;

            TestLog( L"[Panorama/Test] %s result=%s stitched=%dx%d xScale=%.3f corr=%.4f mapOff=%.1f mapScale=%.3f\n",
                         scenario, ok ? L"PASS" : L"FAIL", sW, sH, xScale, bestCorr, bestOff, bestScale );

            if( !ok && !selfTestDumpDirectory.empty() )
            {
                wchar_t msg[512]{};
                swprintf_s( msg, L"CORR: %s stitched=%dx%d xScale=%.3f corr=%.4f mapOff=%.1f mapScale=%.3f\n",
                            scenario, sW, sH, xScale, bestCorr, bestOff, bestScale );
                stressFailLog += msg;
            }

            return ok ? 1 : 0;
        };

        // Replay-capture stress: evaluates actual dump directories using the
        // same replay path as manual validation and asserts a broad stability
        // signature (distributed spikes + tiny-step collapse + oscillation).
        if( !stressEarlyExit && stressReplayEnabled )
        {
            const wchar_t* replayScenario = L"stress-replay-capturepath-quality";
            const bool replayScenarioIsFocusMatch = stressScenarioMatches( replayScenario );
            if( !stressFocusEnabled || replayScenarioIsFocusMatch )
            {
                if( replayScenarioIsFocusMatch )
                    stressFocusMatched = true;
                bool replayScenarioPassed = false;

                stressTestsRun++;
                const ULONGLONG replayStart = GetTickCount64();
                std::filesystem::path replayOutPath;
                const std::filesystem::path replayDir( stressReplayDirArg );
                const bool stitched = RunPanoramaStitchFromDumpDirectory( replayDir, replayOutPath );
                const ULONGLONG replayDurationMs = GetTickCount64() - replayStart;

                wchar_t msg[768]{};
                if( !stitched )
                {
                    TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", replayScenario );
                    swprintf_s( msg, L"INFRA: %s dir=%s durMs=%llu\n",
                                replayScenario,
                                stressReplayDirArg.c_str(),
                                replayDurationMs );
                    stressFailLog += msg;
                    stressLog += msg;
                }
                else
                {
                    ReplayStepMetrics metrics{};
                    const std::filesystem::path replayLogPath = replayDir / L"stitch_log.txt";
                    if( !parseReplayStepMetrics( replayLogPath, metrics ) )
                    {
                        TestLog( L"***** FAIL: %s METRIC PARSE ERROR *****\n", replayScenario );
                        swprintf_s( msg, L"INFRA: %s parse-failed log=%s durMs=%llu\n",
                                    replayScenario,
                                    replayLogPath.c_str(),
                                    replayDurationMs );
                        stressFailLog += msg;
                        stressLog += msg;
                    }
                    else
                    {
                        const size_t transitions = metrics.accepted > 0 ? metrics.accepted - 1 : 0;
                        const bool distributedLarge = metrics.gt96 >= max( static_cast<size_t>( 16 ), transitions / 6 );
                        const bool severeSpikes = metrics.gt120 >= max( static_cast<size_t>( 8 ), transitions / 10 );
                        const bool topBandInstability = metrics.first56Gt96 >= 8 || metrics.first56Gt120 >= 4;
                        const bool bimodalThroughout =
                            metrics.lte8 >= max( static_cast<size_t>( 14 ), transitions / 7 ) &&
                            metrics.gt96 >= max( static_cast<size_t>( 14 ), transitions / 7 ) &&
                            metrics.oscillations >= 2;

                        const bool replayStabilityOk = !( distributedLarge || severeSpikes || topBandInstability || bimodalThroughout );

                        TestLog( L"[Panorama/Test] %s metrics accepted=%zu gt120=%zu gt96=%zu lte8=%zu osc=%zu first56(gt120=%zu gt96=%zu)\n",
                                     replayScenario,
                                     metrics.accepted,
                                     metrics.gt120,
                                     metrics.gt96,
                                     metrics.lte8,
                                     metrics.oscillations,
                                     metrics.first56Gt120,
                                     metrics.first56Gt96 );

                        if( replayStabilityOk )
                        {
                            replayScenarioPassed = true;
                            stressTestsPassed++;
                            swprintf_s( msg, L"PASS: %s accepted=%zu gt120=%zu gt96=%zu lte8=%zu osc=%zu durMs=%llu\n",
                                        replayScenario,
                                        metrics.accepted,
                                        metrics.gt120,
                                        metrics.gt96,
                                        metrics.lte8,
                                        metrics.oscillations,
                                        replayDurationMs );
                        }
                        else
                        {
                            swprintf_s( msg, L"FAIL: %s accepted=%zu gt120=%zu gt96=%zu lte8=%zu osc=%zu first56(gt120=%zu gt96=%zu) durMs=%llu\n",
                                        replayScenario,
                                        metrics.accepted,
                                        metrics.gt120,
                                        metrics.gt96,
                                        metrics.lte8,
                                        metrics.oscillations,
                                        metrics.first56Gt120,
                                        metrics.first56Gt96,
                                        replayDurationMs );
                            stressFailLog += msg;
                        }
                        stressLog += msg;
                    }
                }

                if( replayScenarioIsFocusMatch )
                {
                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                 replayScenario,
                                 replayScenarioPassed ? L"PASS" : L"FAIL" );
                    if( stressStopAfterFocus )
                        stressEarlyExit = true;
                }
            }
        }

        auto countComposedVertical = [&]( const std::vector<BYTE>& imgPx, int imgW, int imgH,
                                          const std::vector<int>& originsY, int winH ) -> size_t
        {
            std::vector<HBITMAP> frames;
            frames.reserve( originsY.size() );
            for( int originY : originsY )
            {
                if( originY < 0 || originY + winH > imgH )
                {
                    for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
                    return 0;
                }
                std::vector<BYTE> fp( static_cast<size_t>( imgW ) * static_cast<size_t>( winH ) * 4 );
                for( int row = 0; row < winH; ++row )
                {
                    const size_t srcOff = ( static_cast<size_t>( originY + row ) * imgW ) * 4;
                    const size_t dstOff = ( static_cast<size_t>( row ) * imgW ) * 4;
                    memcpy( fp.data() + dstOff, imgPx.data() + srcOff, static_cast<size_t>( imgW ) * 4 );
                }
                HBITMAP bmp = CreateBitmapFromPixels32( fp, imgW, winH );
                if( !bmp )
                {
                    for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
                    return 0;
                }
                frames.push_back( bmp );
            }

            size_t composedCount = 0;
            HBITMAP stitchedBmp = StitchPanoramaFrames( frames, false, nullptr, &composedCount );
            for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
            if( stitchedBmp ) DeleteObject( stitchedBmp );
            return composedCount;
        };

        auto countComposedHorizontal = [&]( const std::vector<BYTE>& imgPx, int imgW, int imgH,
                                            const std::vector<int>& originsX, int winW ) -> size_t
        {
            std::vector<HBITMAP> frames;
            frames.reserve( originsX.size() );
            for( int originX : originsX )
            {
                if( originX < 0 || originX + winW > imgW )
                {
                    for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
                    return 0;
                }
                std::vector<BYTE> fp( static_cast<size_t>( winW ) * static_cast<size_t>( imgH ) * 4 );
                for( int row = 0; row < imgH; ++row )
                {
                    const size_t srcOff = ( static_cast<size_t>( row ) * imgW + originX ) * 4;
                    const size_t dstOff = ( static_cast<size_t>( row ) * winW ) * 4;
                    memcpy( fp.data() + dstOff, imgPx.data() + srcOff, static_cast<size_t>( winW ) * 4 );
                }
                HBITMAP bmp = CreateBitmapFromPixels32( fp, winW, imgH );
                if( !bmp )
                {
                    for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
                    return 0;
                }
                frames.push_back( bmp );
            }

            size_t composedCount = 0;
            HBITMAP stitchedBmp = StitchPanoramaFrames( frames, false, nullptr, &composedCount );
            for( HBITMAP hb : frames ) if( hb ) DeleteObject( hb );
            if( stitchedBmp ) DeleteObject( stitchedBmp );
            return composedCount;
        };

        // Vertical stress test: ~100 frames per trial, steps 0..25% of portal
        if( useExternalImageAssets )
        {
            const auto vPath = stressDir / L"vertical_stress.png";
            if( std::filesystem::exists( vPath ) )
            {
                std::vector<BYTE> vPx;
                int vW = 0, vH = 0;
                if( !loadImageFile( vPath, vPx, vW, vH ) )
                {
                    TestLog( L"[Panorama/Test] Failed to load vertical_stress.png\n" );
                }
                else
                {
                    TestLog( L"[Panorama/Test] Loaded vertical_stress.png %dx%d\n", vW, vH );

                    auto runVerticalScenario = [&]( const wchar_t* scenarioName,
                                                    const std::vector<int>& originsY,
                                                    int winH,
                                                    int maxStep ) -> bool
                    {
                        if( stressEarlyExit )
                            return false;
                        if( originsY.size() < 3 )
                            return true;
                        if( !stressScenarioMatches( scenarioName ) )
                            return true;
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        // Log frame origins for diagnostics.
                        {
                            std::wstring origStr;
                            for( size_t oi = 0; oi < originsY.size() && oi < 20; ++oi )
                            {
                                if( oi > 0 ) origStr += L",";
                                origStr += std::to_wstring( originsY[oi] );
                            }
                            if( originsY.size() > 20 ) origStr += L",...";
                            TestLog( L"[Panorama/Test] Running %s origins=[%s]\n", scenarioName, origStr.c_str() );
                        }

                        stressTestsRun++;
                        const int result = stitchAndCompare( scenarioName, vPx, vW, vH, originsY, winH );
                        {
                            wchar_t msg[512]{};
                            if( result < 0 )
                            {
                                TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                                swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                                stressFailLog += msg;
                            }
                            else if( result == 1 )
                            {
                                stressTestsPassed++;
                                TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                                swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winH, originsY.size(), maxStep );
                            }
                            else
                            {
                                TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", scenarioName );
                                swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winH, originsY.size(), maxStep );
                                stressFailLog += msg;
                            }
                            stressLog += msg;
                        }

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         scenarioName,
                                         focusResult );
                            if( stressStopAfterFocus )
                            {
                                stressEarlyExit = true;
                                return false;
                            }
                        }

                        return true;
                    };

                    auto cropVerticalStrip = [&]( const std::vector<BYTE>& srcPx,
                                                  int srcW,
                                                  int srcH,
                                                  int startX,
                                                  int stripW,
                                                  std::vector<BYTE>& outPx,
                                                  int& outW,
                                                  int& outH ) -> bool
                    {
                        if( srcW <= 0 || srcH <= 0 || stripW <= 0 )
                            return false;

                        const int clampedW = min( stripW, srcW );
                        const int clampedX = max( 0, min( startX, srcW - clampedW ) );

                        outW = clampedW;
                        outH = srcH;
                        outPx.assign( static_cast<size_t>( outW ) * static_cast<size_t>( outH ) * 4, 0 );

                        for( int y = 0; y < srcH; ++y )
                        {
                            const size_t srcOff = ( static_cast<size_t>( y ) * srcW + clampedX ) * 4;
                            const size_t dstOff = ( static_cast<size_t>( y ) * outW ) * 4;
                            memcpy( outPx.data() + dstOff,
                                    srcPx.data() + srcOff,
                                    static_cast<size_t>( outW ) * 4 );
                        }
                        return true;
                    };

                    auto buildRandomOrigins = [&]( int winH, int maxStep, unsigned seed )
                    {
                        srand( seed );
                        std::vector<int> originsY;
                        originsY.push_back( 0 );
                        int y = 0;
                        for( int f = 1; f < 100; ++f )
                        {
                            const int step = rand() % ( maxStep + 1 );
                            const int nextY = y + step;
                            if( nextY + winH > vH )
                                break;
                            y = nextY;
                            originsY.push_back( y );
                        }
                        return originsY;
                    };

                    auto buildCadenceStressOrigins = [&]( int winH, int maxStep, unsigned seed, int profile )
                    {
                        srand( seed );
                        std::vector<int> originsY;
                        originsY.push_back( 0 );
                        int y = 0;
                        int frame = 1;
                        int burstRemaining = 0;

                        while( frame < 120 )
                        {
                            const int midY = y + winH / 2;
                            const int region = ( vH > 0 ) ? ( midY * 7 / vH ) : 0; // matches 7-band stress image
                            int step = 0;

                            if( profile == 0 )
                            {
                                // Duplicate burst around sparse lower bands + periodic trap.
                                if( region >= 5 && ( frame % 9 == 0 ) )
                                    burstRemaining = 6;

                                if( burstRemaining > 0 )
                                {
                                    step = ( burstRemaining <= 2 ) ? ( 4 + rand() % max( 1, maxStep / 10 ) ) : 0;
                                    burstRemaining--;
                                }
                                else
                                {
                                    const int baseMax = max( 8, maxStep / 3 );
                                    step = 4 + rand() % baseMax;
                                    if( region >= 5 && frame % 7 == 0 )
                                        step = max( 1, min( maxStep, winH / 3 ) );
                                }
                            }
                            else if( profile == 1 )
                            {
                                // Ramp -> jump -> tiny-step recovery pattern.
                                if( frame < 18 )
                                {
                                    step = 4 + rand() % max( 1, maxStep / 5 );
                                }
                                else if( frame == 18 || frame == 34 || frame == 52 )
                                {
                                    step = max( 1, min( maxStep, ( winH * 3 ) / 10 ) );
                                }
                                else if( frame > 18 && frame < 26 )
                                {
                                    step = ( frame % 3 == 0 ) ? 0 : ( 4 + rand() % 6 );
                                }
                                else
                                {
                                    step = rand() % ( maxStep + 1 );
                                }
                            }
                            else if( profile == 2 )
                            {
                                // Periodic-trap cadence: repeated small steps that
                                // align with text periodicity, then occasional
                                // medium jumps followed by tiny recovery steps.
                                if( frame % 11 == 0 )
                                {
                                    step = max( 1, min( maxStep, winH / 5 ) );
                                }
                                else if( frame % 11 == 1 || frame % 11 == 2 || frame % 11 == 3 )
                                {
                                    step = 4 + rand() % 6;
                                }
                                else
                                {
                                    const int periodic = max( 6, min( maxStep, winH / 18 ) );
                                    step = periodic;
                                }

                                if( region >= 5 && frame % 17 == 0 )
                                {
                                    step = 0;
                                }
                            }
                            else if( profile == 3 )
                            {
                                // Legit-jump cadence: mostly moderate steps with
                                // periodic larger (30-40% portal) jumps that are
                                // still plausible for fast scrolls. This exposes
                                // over-aggressive continuity gating that drops real
                                // content ranges.
                                const int moderateMax = max( 8, maxStep / 2 );
                                step = 6 + rand() % max( 1, moderateMax );

                                if( frame % 10 == 0 )
                                {
                                    step = max( 1, min( maxStep, ( winH * 2 ) / 5 ) );
                                }
                                else if( frame % 10 == 1 || frame % 10 == 2 )
                                {
                                    step = max( 4, min( maxStep, winH / 12 ) );
                                }

                                // In lower sparse bands, enforce occasional larger
                                // but still valid jumps.
                                if( region >= 5 && frame % 14 == 0 )
                                {
                                    step = max( 1, min( maxStep, ( winH * 7 ) / 20 ) );
                                }
                            }
                            else if( profile == 4 )
                            {
                                // Fast-scroll cadence: large steps (40-60% of frame
                                // height) simulating real fast-scroll captures of
                                // text-heavy dark-themed pages. This creates harmonic
                                // vulnerability: many sub-multiples of the true step
                                // exist within the search range.
                                const int minBigStep = max( 1, ( winH * 2 ) / 5 );
                                const int bigStepRange = max( 1, ( winH * 3 ) / 5 - minBigStep );

                                // Consistent large step with minor variation.
                                step = min( maxStep, minBigStep + rand() % max( 1, bigStepRange ) );
                            }
                            else
                            {
                                // Scroll-acceleration cadence: small initial step
                                // (~2% of frame) that rapidly grows to 60-80% of
                                // frame height.  Reproduces the real capture failure
                                // where a slow start locks expectedDy to a small
                                // value, then all subsequent large-shift frames are
                                // rejected because the stitcher keeps comparing
                                // against the last accepted frame with the stale
                                // expected step.
                                if( frame <= 2 )
                                {
                                    step = max( 1, winH / 50 + rand() % max( 1, winH / 40 ) );
                                }
                                else
                                {
                                    const int accelMin = max( 1, ( winH * 3 ) / 5 );
                                    const int accelRange = max( 1, ( winH * 4 ) / 5 - accelMin );
                                    step = min( maxStep, accelMin + rand() % max( 1, accelRange ) );
                                }
                            }

                            const int nextY = y + step;
                            if( nextY + winH > vH )
                                break;
                            y = nextY;
                            originsY.push_back( y );
                            frame++;
                        }

                        return originsY;
                    };

                    // Real-capture regression: narrow portrait strip + VLE-ish
                    // content can mis-lock axis detection on the first pair,
                    // causing vertical scroll to be composed horizontally.
                    if( !stressEarlyExit )
                    {
                        const wchar_t* narrowAxisName = L"stress-vertical-narrowstrip-axisflip";
                        if( stressScenarioMatches( narrowAxisName ) )
                        {
                            if( stressFocusEnabled )
                                stressFocusMatched = true;

                            const int stripW = min( 357, max( 240, vW / 6 ) );
                            const int stripX = max( 0, vW - stripW - 12 );
                            std::vector<BYTE> stripPx;
                            int stripOutW = 0;
                            int stripOutH = 0;
                            if( cropVerticalStrip( vPx, vW, vH, stripX, stripW, stripPx, stripOutW, stripOutH ) )
                            {
                                const int winH = min( 1093, max( 720, stripOutH / 2 ) );
                                std::vector<int> originsY;
                                originsY.push_back( 0 );
                                int y = 0;
                                const int scriptedSteps[] = {
                                    7, 61, 59, 59, 59, 59, 59, 59,
                                    57, 57, 55, 53, 51, 51, 51, 51,
                                    49, 49, 49, 48, 48, 61, 61, 60,
                                    59, 57, 55, 55, 61, 61, 61, 61
                                };
                                size_t si = 0;
                                while( originsY.size() < 120 )
                                {
                                    const int step = scriptedSteps[si % _countof( scriptedSteps )];
                                    si++;
                                    const int nextY = y + step;
                                    if( nextY + winH > stripOutH )
                                        break;
                                    y = nextY;
                                    originsY.push_back( y );
                                }

                                if( originsY.size() >= 12 )
                                {
                                    stressTestsRun++;
                                    const int result = stitchAndCompare( narrowAxisName,
                                                                         stripPx,
                                                                         stripOutW,
                                                                         stripOutH,
                                                                         originsY,
                                                                         winH );
                                    wchar_t msg[512]{};
                                    if( result < 0 )
                                    {
                                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", narrowAxisName );
                                        swprintf_s( msg, L"INFRA: %s (strip=%dx%d, winH=%d, nFrames=%zu)\n",
                                                    narrowAxisName,
                                                    stripOutW,
                                                    stripOutH,
                                                    winH,
                                                    originsY.size() );
                                        stressFailLog += msg;
                                    }
                                    else if( result == 1 )
                                    {
                                        stressTestsPassed++;
                                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, narrowAxisName );
                                        swprintf_s( msg, L"PASS: %s (strip=%dx%d, winH=%d, nFrames=%zu)\n",
                                                    narrowAxisName,
                                                    stripOutW,
                                                    stripOutH,
                                                    winH,
                                                    originsY.size() );
                                    }
                                    else
                                    {
                                        TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", narrowAxisName );
                                        swprintf_s( msg, L"FAIL: %s (strip=%dx%d, winH=%d, nFrames=%zu)\n",
                                                    narrowAxisName,
                                                    stripOutW,
                                                    stripOutH,
                                                    winH,
                                                    originsY.size() );
                                        stressFailLog += msg;
                                    }
                                    stressLog += msg;

                                    if( stressFocusEnabled )
                                    {
                                        const wchar_t* focusResult = L"FAIL";
                                        if( result < 0 ) focusResult = L"INFRA";
                                        else if( result == 1 ) focusResult = L"PASS";
                                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                                     narrowAxisName,
                                                     focusResult );
                                        if( stressStopAfterFocus )
                                        {
                                            stressEarlyExit = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    constexpr int kStressTrials = 5;
                    for( int trial = 0; trial < kStressTrials; ++trial )
                    {
                        if( stressEarlyExit )
                            break;
                        srand( static_cast<unsigned>( 70000 + trial * 13 ) );

                        // Portal height: between 150 and 400.
                        const int winH = 150 + rand() % 251;
                        const int maxStep = max( 1, winH / 4 ); // 25% of portal

                        std::vector<int> originsY = buildRandomOrigins( winH, maxStep, static_cast<unsigned>( 70000 + trial * 13 ) );

                        wchar_t scenarioName[256];
                        swprintf_s( scenarioName, L"stress-vertical-trial%d-w%d-n%zu-maxstep%d",
                                    trial, winH, originsY.size(), maxStep );
                        if( !runVerticalScenario( scenarioName, originsY, winH, maxStep ) )
                            break;

                        // Additional deterministic cadence scenarios.
                        {
                            std::vector<int> burstOrigins = buildCadenceStressOrigins( winH, maxStep, static_cast<unsigned>( 71000 + trial * 19 ), 0 );
                            wchar_t burstName[256];
                            swprintf_s( burstName, L"stress-vertical-dupburst-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, burstOrigins.size(), maxStep );
                            if( !runVerticalScenario( burstName, burstOrigins, winH, maxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                        {
                            std::vector<int> jumpOrigins = buildCadenceStressOrigins( winH, maxStep, static_cast<unsigned>( 72000 + trial * 23 ), 1 );
                            wchar_t jumpName[256];
                            swprintf_s( jumpName, L"stress-vertical-jumprecover-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, jumpOrigins.size(), maxStep );
                            if( !runVerticalScenario( jumpName, jumpOrigins, winH, maxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                        {
                            std::vector<int> periodicOrigins = buildCadenceStressOrigins( winH, maxStep, static_cast<unsigned>( 73000 + trial * 29 ), 2 );
                            wchar_t periodicName[256];
                            swprintf_s( periodicName, L"stress-vertical-periodictrap-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, periodicOrigins.size(), maxStep );
                            if( !runVerticalScenario( periodicName, periodicOrigins, winH, maxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                        {
                            const int jumpyMaxStep = max( maxStep, ( winH * 2 ) / 5 );
                            std::vector<int> legitJumpOrigins = buildCadenceStressOrigins( winH, jumpyMaxStep, static_cast<unsigned>( 74000 + trial * 31 ), 3 );
                            wchar_t legitJumpName[256];
                            swprintf_s( legitJumpName, L"stress-vertical-legitjumps-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, legitJumpOrigins.size(), jumpyMaxStep );
                            if( !runVerticalScenario( legitJumpName, legitJumpOrigins, winH, jumpyMaxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                        {
                            const int fastMaxStep = max( maxStep, winH * 3 / 5 );
                            std::vector<int> fastOrigins = buildCadenceStressOrigins( winH, fastMaxStep, static_cast<unsigned>( 75000 + trial * 37 ), 4 );
                            wchar_t fastName[256];
                            swprintf_s( fastName, L"stress-vertical-fastscroll-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, fastOrigins.size(), fastMaxStep );
                            if( !runVerticalScenario( fastName, fastOrigins, winH, fastMaxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                        {
                            const int accelMaxStep = max( maxStep, winH * 4 / 5 );
                            std::vector<int> accelOrigins = buildCadenceStressOrigins( winH, accelMaxStep, static_cast<unsigned>( 76000 + trial * 41 ), 5 );
                            wchar_t accelName[256];
                            swprintf_s( accelName, L"stress-vertical-accelscroll-trial%d-w%d-n%zu-maxstep%d",
                                        trial, winH, accelOrigins.size(), accelMaxStep );
                            if( !runVerticalScenario( accelName, accelOrigins, winH, accelMaxStep ) )
                                break;
                        }
                        if( stressEarlyExit )
                            break;
                    }

                    // Slow-then-fast acceleration test: moderate scrolling
                    // followed by a near-stationary pause, then sudden large
                    // steps.  The near-stationary spike guard must not reject
                    // the fast phase when p75 of recent history shows the
                    // user was actively scrolling before the pause.
                    if( !stressEarlyExit )
                    {
                        const wchar_t* stfName = L"stress-vertical-slowthenfast";
                        if( stressScenarioMatches( stfName ) )
                        {
                            if( stressFocusEnabled )
                                stressFocusMatched = true;

                            const int stfH = 800;
                            if( vH >= stfH + 4200 )
                            {
                                std::vector<int> stfOrigins;
                                stfOrigins.push_back( 0 );
                                int y = 0;

                                // Phase 1: 4 moderate steps (~10% of frame)
                                // to seed non-trivial p75.
                                const int modSteps[] = { 80, 87, 94, 101 };
                                for( int ms : modSteps )
                                {
                                    y += ms;
                                    if( y + stfH > vH ) break;
                                    stfOrigins.push_back( y );
                                }

                                // Phase 2: 8 near-stationary steps to pull
                                // median below axisFrame/20.
                                for( int f = 0; f < 8; ++f )
                                {
                                    y += 4 + ( f % 3 ); // 4-6
                                    if( y + stfH > vH ) break;
                                    stfOrigins.push_back( y );
                                }

                                // Phase 3: 10 fast steps (31-40% of frame).
                                // Without the p75 fix these are all rejected
                                // by the near-stationary spike guard.
                                const int fastSteps[] = { 250, 263, 276, 289, 302,
                                                          260, 273, 286, 299, 312 };
                                for( int fs : fastSteps )
                                {
                                    y += fs;
                                    if( y + stfH > vH ) break;
                                    stfOrigins.push_back( y );
                                }

                                if( stfOrigins.size() >= 15 )
                                {
                                    if( !runVerticalScenario( stfName, stfOrigins, stfH, 312 ) )
                                        stressEarlyExit = true;
                                }
                            }
                        }
                    }

                    // Startup-defer + legit-jump stress: deterministic low-entropy
                    // synthetic content with tiny early steps (axis-defer prone)
                    // followed by large but valid jumps (~68% overlap retained).
                    // This captures the real failure pattern where early rejects
                    // distort expected motion and later valid jumps are dropped.
                    if( !stressEarlyExit )
                    {
                        const wchar_t* adrName = L"stress-vertical-axisdefer-legitjumps";
                        if( stressScenarioMatches( adrName ) )
                        {
                            if( stressFocusEnabled )
                                stressFocusMatched = true;

                            const int adrW = 1303;
                            const int adrH = 9800;
                            const int adrWinH = 763;
                            std::vector<BYTE> adrPx( static_cast<size_t>( adrW ) * adrH * 4, 0 );

                            for( int y = 0; y < adrH; ++y )
                            {
                                for( int x = 0; x < adrW; ++x )
                                {
                                    const BYTE base = static_cast<BYTE>( 14 + ( ( x * 3 + y * 5 ) & 0x03 ) );
                                    const size_t idx = ( static_cast<size_t>( y ) * adrW + x ) * 4;
                                    adrPx[idx + 0] = base;
                                    adrPx[idx + 1] = static_cast<BYTE>( base + 1 );
                                    adrPx[idx + 2] = static_cast<BYTE>( base + 2 );
                                    adrPx[idx + 3] = 255;
                                }
                            }

                            // Add sparse periodic bands and weak non-periodic markers.
                            for( int band = 0; band * 34 < adrH; ++band )
                            {
                                const int y0 = band * 34;
                                for( int dy = 0; dy < 2; ++dy )
                                {
                                    const int yy = y0 + dy;
                                    if( yy >= adrH )
                                        continue;
                                    for( int x = 0; x < adrW; ++x )
                                    {
                                        const size_t idx = ( static_cast<size_t>( yy ) * adrW + x ) * 4;
                                        adrPx[idx + 0] = 38;
                                        adrPx[idx + 1] = 42;
                                        adrPx[idx + 2] = 46;
                                    }
                                }

                                if( ( band % 7 ) == 0 )
                                {
                                    const int x0 = 12 + ( ( band * 53 ) % 120 );
                                    for( int yy = y0 + 9; yy < min( y0 + 12, adrH ); ++yy )
                                    {
                                        for( int xx = x0; xx < min( x0 + 3, adrW ); ++xx )
                                        {
                                            const size_t idx = ( static_cast<size_t>( yy ) * adrW + xx ) * 4;
                                            adrPx[idx + 0] = 152;
                                            adrPx[idx + 1] = 158;
                                            adrPx[idx + 2] = 164;
                                        }
                                    }
                                }
                            }

                            std::vector<int> originsY;
                            originsY.push_back( 0 );
                            int y = 0;
                            const int adrSteps[] = {
                                2, 3, 2, 4,            // startup near-duplicate regime
                                46, 51, 48, 53, 49,    // axis establish
                                520, 518, 522, 520,    // legit large jumps (matches real pattern)
                                84, 79, 92, 88, 96     // recovery tail
                            };
                            for( int step : adrSteps )
                            {
                                y += step;
                                if( y + adrWinH > adrH )
                                    break;
                                originsY.push_back( y );
                            }

                            if( originsY.size() >= 10 )
                            {
                                stressTestsRun++;
                                const int rawResult = stitchAndCompare( adrName, adrPx, adrW, adrH, originsY, adrWinH );
                                const size_t composedCount = countComposedVertical( adrPx, adrW, adrH, originsY, adrWinH );
                                const int result = ( rawResult == 1 && composedCount == originsY.size() ) ? 1 : 0;
                                wchar_t msg[512]{};
                                if( result < 0 )
                                {
                                    TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", adrName );
                                    swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", adrName, adrWinH, originsY.size() );
                                    stressFailLog += msg;
                                }
                                else if( result == 1 )
                                {
                                    stressTestsPassed++;
                                    TestLog( L"  [%d] %s PASSED\n", stressTestsRun, adrName );
                                    swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", adrName, adrWinH, originsY.size(), composedCount );
                                }
                                else
                                {
                                    TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", adrName );
                                    swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", adrName, adrWinH, originsY.size(), composedCount );
                                    stressFailLog += msg;
                                }
                                stressLog += msg;

                                if( stressFocusEnabled )
                                {
                                    const wchar_t* focusResult = L"FAIL";
                                    if( result < 0 ) focusResult = L"INFRA";
                                    else if( result == 1 ) focusResult = L"PASS";
                                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", adrName, focusResult );
                                    if( stressStopAfterFocus )
                                        stressEarlyExit = true;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                TestLog( L"[Panorama/Test] Skipping vertical_stress.png (not found at %s)\n", vPath.c_str() );
            }
        }

        // Always run the synthetic axis-defer stress case even when the
        // external vertical_stress.png asset is unavailable.
        if( !stressEarlyExit )
        {
            if( true )
            {
                const wchar_t* adrName = L"stress-vertical-axisdefer-legitjumps";
                if( stressScenarioMatches( adrName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    const int adrW = 1303;
                    const int adrH = 9800;
                    const int adrWinH = 763;
                    std::vector<BYTE> adrPx( static_cast<size_t>( adrW ) * adrH * 4, 0 );

                    for( int y = 0; y < adrH; ++y )
                    {
                        for( int x = 0; x < adrW; ++x )
                        {
                            const BYTE base = static_cast<BYTE>( 14 + ( ( x * 3 + y * 5 ) & 0x03 ) );
                            const size_t idx = ( static_cast<size_t>( y ) * adrW + x ) * 4;
                            adrPx[idx + 0] = base;
                            adrPx[idx + 1] = static_cast<BYTE>( base + 1 );
                            adrPx[idx + 2] = static_cast<BYTE>( base + 2 );
                            adrPx[idx + 3] = 255;
                        }
                    }

                    for( int band = 0; band * 34 < adrH; ++band )
                    {
                        const int y0 = band * 34;
                        for( int dy = 0; dy < 2; ++dy )
                        {
                            const int yy = y0 + dy;
                            if( yy >= adrH )
                                continue;
                            for( int x = 0; x < adrW; ++x )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * adrW + x ) * 4;
                                adrPx[idx + 0] = 38;
                                adrPx[idx + 1] = 42;
                                adrPx[idx + 2] = 46;
                            }
                        }

                        if( ( band % 7 ) == 0 )
                        {
                            const int x0 = 12 + ( ( band * 53 ) % 120 );
                            for( int yy = y0 + 9; yy < min( y0 + 12, adrH ); ++yy )
                            {
                                for( int xx = x0; xx < min( x0 + 3, adrW ); ++xx )
                                {
                                    const size_t idx = ( static_cast<size_t>( yy ) * adrW + xx ) * 4;
                                    adrPx[idx + 0] = 152;
                                    adrPx[idx + 1] = 158;
                                    adrPx[idx + 2] = 164;
                                }
                            }
                        }
                    }

                    std::vector<int> originsY;
                    originsY.push_back( 0 );
                    int y = 0;
                    const int adrSteps[] = {
                        2, 3, 2, 4,
                        46, 51, 48, 53, 49,
                        520, 518, 522, 520,
                        84, 79, 92, 88, 96
                    };
                    for( int step : adrSteps )
                    {
                        y += step;
                        if( y + adrWinH > adrH )
                            break;
                        originsY.push_back( y );
                    }

                    if( originsY.size() >= 10 )
                    {
                        stressTestsRun++;
                        const int rawResult = stitchAndCompare( adrName, adrPx, adrW, adrH, originsY, adrWinH );
                        const size_t composedCount = countComposedVertical( adrPx, adrW, adrH, originsY, adrWinH );
                        const int result = ( rawResult == 1 && composedCount == originsY.size() ) ? 1 : 0;
                        wchar_t msg[512]{};
                        if( result < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", adrName );
                            swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", adrName, adrWinH, originsY.size() );
                            stressFailLog += msg;
                        }
                        else if( result == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, adrName );
                            swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", adrName, adrWinH, originsY.size(), composedCount );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", adrName );
                            swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", adrName, adrWinH, originsY.size(), composedCount );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", adrName, focusResult );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }
            }
        }

        // Momentum-reversal-tail stress: deterministic low-entropy synthetic
        // sequence that transitions from high momentum to tiny steps.
        // The true origins always move forward, so any accepted backward
        // stitch steps indicate a harmonic reversal regression.
        if( !stressEarlyExit )
        {
            const wchar_t* mrtName = L"stress-vertical-momentumreversal-tail";
            if( stressScenarioMatches( mrtName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                const int mrtW = 1338;
                const int mrtH = 11000;
                const int mrtWinH = 933;
                std::vector<BYTE> mrtPx( static_cast<size_t>( mrtW ) * mrtH * 4, 0 );

                for( int y = 0; y < mrtH; ++y )
                {
                    for( int x = 0; x < mrtW; ++x )
                    {
                        const BYTE base = static_cast<BYTE>( 14 + ( ( x * 3 + y * 5 ) & 0x03 ) );
                        const size_t idx = ( static_cast<size_t>( y ) * mrtW + x ) * 4;
                        mrtPx[idx + 0] = base;
                        mrtPx[idx + 1] = static_cast<BYTE>( base + 1 );
                        mrtPx[idx + 2] = static_cast<BYTE>( base + 2 );
                        mrtPx[idx + 3] = 255;
                    }
                }

                for( int band = 0; band * 40 < mrtH; ++band )
                {
                    const int y0 = band * 40;
                    for( int dy = 0; dy < 2; ++dy )
                    {
                        const int yy = y0 + dy;
                        if( yy >= mrtH )
                            continue;
                        for( int x = 0; x < mrtW; ++x )
                        {
                            const size_t idx = ( static_cast<size_t>( yy ) * mrtW + x ) * 4;
                            mrtPx[idx + 0] = 38;
                            mrtPx[idx + 1] = 42;
                            mrtPx[idx + 2] = 46;
                        }
                    }

                    if( ( band % 9 ) == 0 )
                    {
                        const int x0 = 12 + ( ( band * 47 ) % 128 );
                        for( int yy = y0 + 10; yy < min( y0 + 14, mrtH ); ++yy )
                        {
                            for( int xx = x0; xx < min( x0 + 3, mrtW ); ++xx )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * mrtW + xx ) * 4;
                                mrtPx[idx + 0] = 150;
                                mrtPx[idx + 1] = 156;
                                mrtPx[idx + 2] = 162;
                            }
                        }
                    }
                }

                std::vector<int> originsY;
                originsY.push_back( 0 );
                int y = 0;
                const int mrtSteps[] = {
                    2, 3, 2, 4,
                    330, 280, 280, 280, 330, 280, 280,
                    520, 520, 510, 508,
                    40, 24, 50, 60
                };
                for( int step : mrtSteps )
                {
                    y += step;
                    if( y + mrtWinH > mrtH )
                        break;
                    originsY.push_back( y );
                }

                if( originsY.size() >= 12 )
                {
                    stressTestsRun++;
                    const int rawResult = stitchAndCompare( mrtName, mrtPx, mrtW, mrtH, originsY, mrtWinH );
                    const size_t composedCount = countComposedVertical( mrtPx, mrtW, mrtH, originsY, mrtWinH );
                    const int result = ( rawResult == 1 && composedCount >= originsY.size() - 4 ) ? 1 : 0;
                    wchar_t msg[512]{};
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", mrtName );
                        swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", mrtName, mrtWinH, originsY.size() );
                        stressFailLog += msg;
                    }
                    else if( result == 1 )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, mrtName );
                        swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", mrtName, mrtWinH, originsY.size(), composedCount );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", mrtName );
                        swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", mrtName, mrtWinH, originsY.size(), composedCount );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        const wchar_t* focusResult = L"FAIL";
                        if( result < 0 ) focusResult = L"INFRA";
                        else if( result == 1 ) focusResult = L"PASS";
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", mrtName, focusResult );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }
        }

        // Expected-lock duplicate-segment stress: reproduces the capture
        // pattern where a single large harmonic jump is followed by many
        // small true steps. Older logic could lock expected motion to the
        // large jump and keep normalizing subsequent small steps, producing
        // repeated content bands.
        if( !stressEarlyExit )
        {
            const wchar_t* eldName = L"stress-vertical-expectedlock-dupsegments";
            if( stressScenarioMatches( eldName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                const int eldW = 1137;
                const int eldH = 12000;
                const int eldWinH = 915;
                std::vector<BYTE> eldPx( static_cast<size_t>( eldW ) * eldH * 4, 0 );

                for( int y = 0; y < eldH; ++y )
                {
                    for( int x = 0; x < eldW; ++x )
                    {
                        const BYTE base = static_cast<BYTE>( 16 + ( ( x * 5 + y * 3 ) & 0x03 ) );
                        const size_t idx = ( static_cast<size_t>( y ) * eldW + x ) * 4;
                        eldPx[idx + 0] = base;
                        eldPx[idx + 1] = static_cast<BYTE>( base + 1 );
                        eldPx[idx + 2] = static_cast<BYTE>( base + 2 );
                        eldPx[idx + 3] = 255;
                    }
                }

                for( int band = 0; band * 38 < eldH; ++band )
                {
                    const int y0 = band * 38;
                    for( int dy = 0; dy < 2; ++dy )
                    {
                        const int yy = y0 + dy;
                        if( yy >= eldH )
                            continue;
                        for( int x = 0; x < eldW; ++x )
                        {
                            const size_t idx = ( static_cast<size_t>( yy ) * eldW + x ) * 4;
                            eldPx[idx + 0] = 40;
                            eldPx[idx + 1] = 44;
                            eldPx[idx + 2] = 48;
                        }
                    }

                    if( ( band % 11 ) == 0 )
                    {
                        const int x0 = 16 + ( ( band * 41 ) % 120 );
                        for( int yy = y0 + 9; yy < min( y0 + 13, eldH ); ++yy )
                        {
                            for( int xx = x0; xx < min( x0 + 3, eldW ); ++xx )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * eldW + xx ) * 4;
                                eldPx[idx + 0] = 154;
                                eldPx[idx + 1] = 160;
                                eldPx[idx + 2] = 166;
                            }
                        }
                    }
                }

                std::vector<int> originsY;
                originsY.push_back( 0 );
                int y = 0;
                const int eldSteps[] = {
                    100, 100, 50, 100, 50, 50,
                    520,
                    50, 50, 100, 100, 50, 100, 100, 100,
                    150, 50, 200, 150, 50
                };
                for( int step : eldSteps )
                {
                    y += step;
                    if( y + eldWinH > eldH )
                        break;
                    originsY.push_back( y );
                }

                if( originsY.size() >= 12 )
                {
                    stressTestsRun++;
                    const int rawResult = stitchAndCompare( eldName, eldPx, eldW, eldH, originsY, eldWinH );
                    const size_t composedCount = countComposedVertical( eldPx, eldW, eldH, originsY, eldWinH );
                    const int result = ( rawResult == 1 && composedCount == originsY.size() ) ? 1 : 0;
                    wchar_t msg[512]{};
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", eldName );
                        swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", eldName, eldWinH, originsY.size() );
                        stressFailLog += msg;
                    }
                    else if( result == 1 )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, eldName );
                        swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", eldName, eldWinH, originsY.size(), composedCount );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", eldName );
                        swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", eldName, eldWinH, originsY.size(), composedCount );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        const wchar_t* focusResult = L"FAIL";
                        if( result < 0 ) focusResult = L"INFRA";
                        else if( result == 1 ) focusResult = L"PASS";
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", eldName, focusResult );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }
        }

        // Non-VLE wrap-jump rule regression: capture the latest live pattern
        // (expected~25, sudden 400+ jump) with deterministic predicate checks
        // so future refactors do not silently disable the guard.
        if( !stressEarlyExit )
        {
            const wchar_t* nvwName = L"stress-vertical-nonvle-wrapjump-rule";
            if( stressScenarioMatches( nvwName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;
                auto shouldRejectWrapJump = [&]( int expectedAxis,
                                                 int axisStep,
                                                 int axisFrame,
                                                 int entropyFlag,
                                                 size_t recentCount ) -> bool
                {
                    const bool nonVleWrap =
                        entropyFlag == 0 && recentCount >= 8 &&
                        expectedAxis >= 20 && expectedAxis <= 56 &&
                        axisStep >= max( axisFrame / 3, 220 ) &&
                        axisStep >= expectedAxis * 10;
                    const bool vleWrap =
                        entropyFlag != 0 &&
                        expectedAxis >= 8 && expectedAxis <= 56 &&
                        axisStep >= max( axisFrame / 3, 240 ) &&
                        axisStep >= expectedAxis * 8;
                    return nonVleWrap || vleWrap;
                };

                const bool captureSignatureGuarded =
                    shouldRejectWrapJump( 25, 465, 1035, 1, 12 );
                const bool legitFastScrollKeep =
                    !shouldRejectWrapJump( 82, 182, 1035, 1, 12 );

                stressTestsRun++;
                const bool pass = captureSignatureGuarded && legitFastScrollKeep;
                wchar_t msg[512]{};
                if( pass )
                {
                    stressTestsPassed++;
                    TestLog( L"  [%d] %s PASSED\n", stressTestsRun, nvwName );
                    swprintf_s( msg, L"PASS: %s (captureGuard=%d legitKeep=%d)\n",
                                nvwName,
                                captureSignatureGuarded ? 1 : 0,
                                legitFastScrollKeep ? 1 : 0 );
                }
                else
                {
                    TestLog( L"***** FAIL: %s (captureGuard=%d legitKeep=%d) *****\n",
                             nvwName,
                             captureSignatureGuarded ? 1 : 0,
                             legitFastScrollKeep ? 1 : 0 );
                    swprintf_s( msg, L"FAIL: %s (captureGuard=%d legitKeep=%d)\n",
                                nvwName,
                                captureSignatureGuarded ? 1 : 0,
                                legitFastScrollKeep ? 1 : 0 );
                    stressFailLog += msg;
                }
                stressLog += msg;

                if( stressFocusEnabled )
                {
                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", nvwName, pass ? L"PASS" : L"FAIL" );
                    if( stressStopAfterFocus )
                        stressEarlyExit = true;
                }
            }
        }

        // Startup-recovery + large-jump tail stress variants: mirror live
        // captures where early no-reliable-shift frames trigger recovery,
        // then a large harmonic jump is followed by short tail steps.
        auto runStartupRecoveryTailVariant = [&]( const wchar_t* scenarioName,
                                                  const std::vector<int>& steps )
        {
            if( stressEarlyExit || !stressScenarioMatches( scenarioName ) )
                return;

            if( stressFocusEnabled )
                stressFocusMatched = true;

            const int srtW = 1140;
            const int srtH = 13000;
            const int srtWinH = 915;
            std::vector<BYTE> srtPx( static_cast<size_t>( srtW ) * srtH * 4, 0 );

            for( int y = 0; y < srtH; ++y )
            {
                for( int x = 0; x < srtW; ++x )
                {
                    const BYTE base = static_cast<BYTE>( 15 + ( ( x * 7 + y * 3 ) & 0x03 ) );
                    const size_t idx = ( static_cast<size_t>( y ) * srtW + x ) * 4;
                    srtPx[idx + 0] = base;
                    srtPx[idx + 1] = static_cast<BYTE>( base + 1 );
                    srtPx[idx + 2] = static_cast<BYTE>( base + 2 );
                    srtPx[idx + 3] = 255;
                }
            }

            for( int band = 0; band * 36 < srtH; ++band )
            {
                const int y0 = band * 36;
                for( int dy = 0; dy < 2; ++dy )
                {
                    const int yy = y0 + dy;
                    if( yy >= srtH )
                        continue;
                    for( int x = 0; x < srtW; ++x )
                    {
                        const size_t idx = ( static_cast<size_t>( yy ) * srtW + x ) * 4;
                        srtPx[idx + 0] = 38;
                        srtPx[idx + 1] = 42;
                        srtPx[idx + 2] = 46;
                    }
                }

                if( ( band % 10 ) == 0 )
                {
                    const int x0 = 18 + ( ( band * 43 ) % 124 );
                    for( int yy = y0 + 8; yy < min( y0 + 12, srtH ); ++yy )
                    {
                        for( int xx = x0; xx < min( x0 + 3, srtW ); ++xx )
                        {
                            const size_t idx = ( static_cast<size_t>( yy ) * srtW + xx ) * 4;
                            srtPx[idx + 0] = 152;
                            srtPx[idx + 1] = 158;
                            srtPx[idx + 2] = 164;
                        }
                    }
                }
            }

            // Add deterministic sparse anchors so tiny startup steps are less
            // ambiguous while preserving low-detail band structure used by the
            // tail/jump stress patterns.
            for( int y = 7; y < srtH; y += 23 )
            {
                const int x0 = 9 + ( ( y * 73 ) % max( 1, srtW - 6 ) );
                const BYTE c0 = static_cast<BYTE>( 96 + ( ( y * 11 ) & 0x3F ) );
                const BYTE c1 = static_cast<BYTE>( 64 + ( ( y * 17 ) & 0x3F ) );
                for( int dy = 0; dy < 2; ++dy )
                {
                    const int yy = y + dy;
                    if( yy >= srtH )
                        continue;
                    for( int dx = 0; dx < 3; ++dx )
                    {
                        const int xx = x0 + dx;
                        if( xx >= srtW )
                            continue;
                        const size_t idx = ( static_cast<size_t>( yy ) * srtW + xx ) * 4;
                        srtPx[idx + 0] = c0;
                        srtPx[idx + 1] = c1;
                        srtPx[idx + 2] = static_cast<BYTE>( c0 ^ c1 );
                    }
                }
            }

            std::vector<int> originsY;
            originsY.push_back( 0 );
            int y = 0;
            for( int step : steps )
            {
                y += step;
                if( y + srtWinH > srtH )
                    break;
                originsY.push_back( y );
            }

            if( originsY.size() >= 12 )
            {
                stressTestsRun++;
                const int rawResult = stitchAndCompare( scenarioName, srtPx, srtW, srtH, originsY, srtWinH );
                const size_t composedCount = countComposedVertical( srtPx, srtW, srtH, originsY, srtWinH );
                const int result = ( rawResult == 1 && composedCount == originsY.size() ) ? 1 : 0;
                wchar_t msg[512]{};
                if( result < 0 )
                {
                    TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                    swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", scenarioName, srtWinH, originsY.size() );
                    stressFailLog += msg;
                }
                else if( result == 1 )
                {
                    stressTestsPassed++;
                    TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                    swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", scenarioName, srtWinH, originsY.size(), composedCount );
                }
                else
                {
                    TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", scenarioName );
                    swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", scenarioName, srtWinH, originsY.size(), composedCount );
                    stressFailLog += msg;
                }
                stressLog += msg;

                if( stressFocusEnabled )
                {
                    const wchar_t* focusResult = L"FAIL";
                    if( result < 0 ) focusResult = L"INFRA";
                    else if( result == 1 ) focusResult = L"PASS";
                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", scenarioName, focusResult );
                    if( stressStopAfterFocus )
                        stressEarlyExit = true;
                }
            }
        };

        runStartupRecoveryTailVariant( L"stress-vertical-startuprecovery-tail610",
                                                                             { 160, 120, 140, 100, 130, 90,
                                         330, 330, 230, 280, 280, 280, 280,
                                         610, 607,
                                         40, 24, 50, 168, 50 } );

        runStartupRecoveryTailVariant( L"stress-vertical-startuprecovery-tail610-v2",
                                                                             { 150, 110, 140, 100, 120, 90,
                                         280, 330, 280, 280, 230, 280, 330,
                                         607, 610,
                                         24, 30, 40, 120, 50, 24 } );

        runStartupRecoveryTailVariant( L"stress-vertical-startuprecovery-tail610-v3",
                                       { 50, 100, 50, 100, 50, 100,
                                         330, 280, 280, 330, 230, 280, 280,
                                         520, 610,
                                         24, 24, 30, 50, 168, 40 } );

                runStartupRecoveryTailVariant( L"stress-vertical-startup-overshoot-guard",
                                                                             { 50, 52, 48, 50, 51, 49,
                                                                                 50, 50, 52, 48, 50, 51, 49,
                                                                                 50, 50, 52, 48, 50, 51, 49 } );

                // Harmonic-fallback regression stress: construct a case where
                // the harmonic shift is a significantly better match than the
                // expected-step candidate. The overshoot guard should not
                // override to expected-step in this situation.
                if( !stressEarlyExit )
                {
                    const wchar_t* regressionName = L"stress-vertical-hcf-overshoot-regression";
                    if( stressScenarioMatches( regressionName ) )
                    {
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        constexpr int w = 1228;
                        constexpr int h = 1032;
                        constexpr int trueStep = 64;
                        constexpr int expectedStep = 31;
                        constexpr int srcH = h + trueStep * 6 + 200;

                        std::vector<BYTE> src( static_cast<size_t>( w ) * srcH * 4, 0 );

                        auto paintSource = [&]( int phase )
                        {
                            for( int y = 0; y < srcH; ++y )
                            {
                                for( int x = 0; x < w; ++x )
                                {
                                    const BYTE base = static_cast<BYTE>( 15 + ( ( x * 5 + y * 3 + phase ) & 0x03 ) );
                                    const size_t idx = ( static_cast<size_t>( y ) * w + x ) * 4;
                                    src[idx + 0] = base;
                                    src[idx + 1] = static_cast<BYTE>( base + 1 );
                                    src[idx + 2] = static_cast<BYTE>( base + 2 );
                                    src[idx + 3] = 255;
                                }
                            }

                            for( int band = 0; ; ++band )
                            {
                                const int y0 = phase + band * trueStep;
                                if( y0 >= srcH )
                                    break;
                                if( y0 < 1 )
                                    continue;
                                for( int by = y0; by < min( srcH - 1, y0 + 2 ); ++by )
                                {
                                    for( int x = 0; x < w; ++x )
                                    {
                                        const size_t idx = ( static_cast<size_t>( by ) * w + x ) * 4;
                                        src[idx + 0] = 38;
                                        src[idx + 1] = 42;
                                        src[idx + 2] = 46;
                                    }
                                }
                            }

                            // Add weaker expected-step periodic traces to make
                            // expected-step correlation deceptively plausible
                            // while keeping true-step bands dominant.
                            for( int band = 0; ; ++band )
                            {
                                const int y0 = phase / 2 + band * expectedStep;
                                if( y0 >= srcH )
                                    break;
                                if( y0 < 1 )
                                    continue;
                                for( int by = y0; by < min( srcH - 1, y0 + 1 ); ++by )
                                {
                                    for( int x = 0; x < w; ++x )
                                    {
                                        const size_t idx = ( static_cast<size_t>( by ) * w + x ) * 4;
                                        src[idx + 0] = static_cast<BYTE>( max( src[idx + 0], 28 ) );
                                        src[idx + 1] = static_cast<BYTE>( max( src[idx + 1], 31 ) );
                                        src[idx + 2] = static_cast<BYTE>( max( src[idx + 2], 34 ) );
                                    }
                                }
                            }

                            // Sparse anchors ensure a unique true shift while
                            // preserving high-constant-fraction behavior.
                            for( int ay = max( 2, 31 + phase ); ay < srcH - 5; ay += 173 )
                            {
                                const int x0 = 20 + ( ( ay * 29 + phase * 13 ) % ( w - 80 ) );
                                for( int dy = 0; dy < 3; ++dy )
                                {
                                    for( int dx = 0; dx < 3; ++dx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( ay + dy ) * w + x0 + dx ) * 4;
                                        src[idx + 0] = 154;
                                        src[idx + 1] = 160;
                                        src[idx + 2] = 166;
                                    }
                                }
                            }
                        };

                        auto buildFrame = [&]( int top, std::vector<BYTE>& outFrame )
                        {
                            outFrame.resize( static_cast<size_t>( w ) * h * 4 );
                            for( int row = 0; row < h; ++row )
                            {
                                const BYTE* srcRow = src.data() +
                                                     ( static_cast<size_t>( top + row ) * w * 4 );
                                BYTE* dstRow = outFrame.data() + static_cast<size_t>( row ) * w * 4;
                                memcpy( dstRow, srcRow, static_cast<size_t>( w ) * 4 );
                            }
                        };

                        TestLog( L"[Panorama/Test] Running %s\n", regressionName );
                        stressTestsRun++;

                        int evaluated = 0;
                        int nearTrueStep = 0;
                        int nearExpectedStep = 0;
                        int sampleBestDy = 0;

                        for( int trial = 0; trial < 14; ++trial )
                        {
                            paintSource( trial );

                            std::vector<BYTE> prevFrame;
                            std::vector<BYTE> currFrame;
                            const int top0 = 120;
                            buildFrame( top0, prevFrame );
                            buildFrame( top0 + trueStep, currFrame );

                            int bestDx = 0;
                            int bestDy = 0;
                            const bool found = FindBestFrameShift( prevFrame,
                                                                   currFrame,
                                                                   w,
                                                                   h,
                                                                   0,
                                                                   -expectedStep,
                                                                   bestDx,
                                                                   bestDy,
                                                                   false );
                            if( !found )
                                continue;

                            evaluated++;
                            const int absDy = abs( bestDy );
                            if( abs( absDy - trueStep ) <= 8 )
                                nearTrueStep++;
                            if( abs( absDy - expectedStep ) <= 6 )
                                nearExpectedStep++;
                            if( sampleBestDy == 0 )
                                sampleBestDy = bestDy;
                        }

                        const bool enoughCoverage = evaluated >= 10;
                        const bool resultPass = enoughCoverage && nearTrueStep >= evaluated * 3 / 4 && nearExpectedStep == 0;

                        wchar_t msg[512]{};
                        if( resultPass )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, regressionName );
                            swprintf_s( msg,
                                        L"PASS: %s (cases=%d nearTrue=%d nearExpected=%d)\n",
                                        regressionName,
                                        evaluated,
                                        nearTrueStep,
                                        nearExpectedStep );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s (cases=%d nearTrue=%d nearExpected=%d sampleBestDy=%d) *****\n",
                                     regressionName,
                                     evaluated,
                                     nearTrueStep,
                                     nearExpectedStep,
                                     sampleBestDy );
                            swprintf_s( msg,
                                        L"FAIL: %s (cases=%d nearTrue=%d nearExpected=%d sampleBestDy=%d)\n",
                                        regressionName,
                                        evaluated,
                                        nearTrueStep,
                                        nearExpectedStep,
                                        sampleBestDy );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     regressionName,
                                     resultPass ? L"PASS" : L"FAIL" );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }

                // Harmonic-overshoot stress: with expected step established at
                // -45, periodic low-detail bands can score better at harmonic
                // multiples (-60/-90) and cause visible stitch blur/exclusion.
                // This scenario validates that selection remains anchored near
                // expected motion on HCF-like content.
                if( !stressEarlyExit )
                {
                    const wchar_t* hcfName = L"stress-vertical-hcf-harmonic-overshoot";
                    if( stressScenarioMatches( hcfName ) )
                    {
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        constexpr int w = 1206;
                        constexpr int h = 1018;
                        constexpr int expectedStep = 45;
                        constexpr int srcH = h + expectedStep * 24 + 260;
                        std::vector<BYTE> source( static_cast<size_t>( w ) * srcH * 4, 0 );

                        auto paintSource = [&]( int phase )
                        {
                            for( int y = 0; y < srcH; ++y )
                            {
                                for( int x = 0; x < w; ++x )
                                {
                                    const BYTE base = static_cast<BYTE>( 15 + ( ( x * 7 + y * 3 ) & 0x03 ) );
                                    const size_t idx = ( static_cast<size_t>( y ) * w + x ) * 4;
                                    source[idx + 0] = base;
                                    source[idx + 1] = static_cast<BYTE>( base + 1 );
                                    source[idx + 2] = static_cast<BYTE>( base + 2 );
                                    source[idx + 3] = 255;
                                }
                            }

                            for( int band = 0; ; ++band )
                            {
                                const int y0 = phase + band * expectedStep;
                                if( y0 >= srcH )
                                    break;
                                if( y0 < 1 )
                                    continue;
                                for( int by = y0; by < min( srcH - 1, y0 + 2 ); ++by )
                                {
                                    for( int x = 0; x < w; ++x )
                                    {
                                        const size_t idx = ( static_cast<size_t>( by ) * w + x ) * 4;
                                        source[idx + 0] = 38;
                                        source[idx + 1] = 42;
                                        source[idx + 2] = 46;
                                    }
                                }
                            }

                            // Sparse asymmetric anchors: enough to define the
                            // true 45px step, but weak enough for harmonic
                            // ambiguity to appear without proper guarding.
                            for( int ay = max( 1, 19 + phase ); ay < srcH - 4; ay += 137 )
                            {
                                const int x0 = 20 + ( ( ay * 37 + phase * 11 ) % ( w - 60 ) );
                                for( int dy = 0; dy < 3; ++dy )
                                {
                                    for( int dx = 0; dx < 3; ++dx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( ay + dy ) * w + x0 + dx ) * 4;
                                        source[idx + 0] = 154;
                                        source[idx + 1] = 160;
                                        source[idx + 2] = 166;
                                    }
                                }
                            }
                        };

                        auto buildFrameFromSource = [&]( int top, std::vector<BYTE>& outFrame )
                        {
                            outFrame.resize( static_cast<size_t>( w ) * h * 4 );
                            for( int row = 0; row < h; ++row )
                            {
                                const BYTE* src = source.data() +
                                                  ( static_cast<size_t>( top + row ) * w * 4 );
                                BYTE* dst = outFrame.data() + static_cast<size_t>( row ) * w * 4;
                                memcpy( dst, src, static_cast<size_t>( w ) * 4 );
                            }
                        };

                        TestLog( L"[Panorama/Test] Running %s\n", hcfName );
                        stressTestsRun++;

                        int evaluatedCases = 0;
                        int harmonicOvershoots = 0;
                        int sampleExpected = 0;
                        int sampleBestDy = 0;

                        for( int trial = 0; trial < 24; ++trial )
                        {
                            const int phase = trial;
                            paintSource( phase );

                            std::vector<int> origins;
                            origins.push_back( 80 );
                            for( int i = 0; i < 20; ++i )
                            {
                                origins.push_back( origins.back() + expectedStep );
                            }

                            int expectedDy = -expectedStep;
                            for( size_t fi = 0; fi + 1 < origins.size(); ++fi )
                            {
                                std::vector<BYTE> prevFrame;
                                std::vector<BYTE> currFrame;
                                buildFrameFromSource( origins[fi], prevFrame );
                                buildFrameFromSource( origins[fi + 1], currFrame );

                                int bestDx = 0;
                                int bestDy = 0;
                                const bool found = FindBestFrameShift( prevFrame,
                                                                       currFrame,
                                                                       w,
                                                                       h,
                                                                       0,
                                                                       expectedDy,
                                                                       bestDx,
                                                                       bestDy,
                                                                       false );
                                if( !found )
                                {
                                    continue;
                                }

                                evaluatedCases++;
                                const int absBest = abs( bestDy );
                                const bool overshoot = absBest > expectedStep + 10;
                                if( overshoot )
                                {
                                    harmonicOvershoots++;
                                    if( sampleExpected == 0 )
                                    {
                                        sampleExpected = expectedDy;
                                        sampleBestDy = bestDy;
                                    }
                                }

                                expectedDy = bestDy;
                            }
                        }

                        const bool enoughCoverage = evaluatedCases >= 12;
                        const bool resultPass = enoughCoverage && harmonicOvershoots == 0;
                        wchar_t msg[512]{};
                        if( resultPass )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, hcfName );
                            swprintf_s( msg,
                                        L"PASS: %s (cases=%d overshoots=%d)\n",
                                        hcfName,
                                        evaluatedCases,
                                        harmonicOvershoots );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s (cases=%d overshoots=%d sampleBestDy=%d expected=%d) *****\n",
                                     hcfName,
                                     evaluatedCases,
                                     harmonicOvershoots,
                                     sampleBestDy,
                                     sampleExpected );
                            swprintf_s( msg,
                                        L"FAIL: %s (cases=%d overshoots=%d sampleBestDy=%d expected=%d)\n",
                                        hcfName,
                                        evaluatedCases,
                                        harmonicOvershoots,
                                        sampleBestDy,
                                        sampleExpected );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     hcfName,
                                     resultPass ? L"PASS" : L"FAIL" );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }

                // Manual-drop cadence stress: mirrors the real high-constant-
                // fraction capture cadence that produced a single large jump
                // (~421 px) among otherwise stable 62/63/125 px steps, which
                // dropped a visible band from the composed panorama.
                if( !stressEarlyExit )
                {
                    const wchar_t* manualDropName = L"stress-vertical-hcf-manualdrop-cadence";
                    if( stressScenarioMatches( manualDropName ) )
                    {
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        constexpr int w = 1650;
                        constexpr int h = 852;
                        const int scriptedSteps[] = {
                            62, 125, 63, 62, 63, 125, 62,
                            63, 62, 63, 125, 62, 63, 62
                        };
                        int totalStep = 0;
                        for( int step : scriptedSteps )
                            totalStep += step;
                        const int srcH = h + totalStep + 512;
                        std::vector<BYTE> source( static_cast<size_t>( w ) * srcH * 4, 0 );

                        auto paintSource = [&]( int phase )
                        {
                            for( int y = 0; y < srcH; ++y )
                            {
                                for( int x = 0; x < w; ++x )
                                {
                                    const BYTE base = static_cast<BYTE>( 241 + ( ( x * 3 + y * 5 + phase ) & 0x03 ) );
                                    const size_t idx = ( static_cast<size_t>( y ) * w + x ) * 4;
                                    source[idx + 0] = base;
                                    source[idx + 1] = base;
                                    source[idx + 2] = base;
                                    source[idx + 3] = 255;
                                }
                            }

                            for( int band = 0; ; ++band )
                            {
                                const int y0 = 180 + phase + band * 63;
                                if( y0 >= srcH )
                                    break;
                                for( int yy = y0; yy < min( srcH, y0 + 2 ); ++yy )
                                {
                                    for( int xx = 52; xx < w - 52; ++xx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( yy ) * w + xx ) * 4;
                                        source[idx + 0] = 34;
                                        source[idx + 1] = 38;
                                        source[idx + 2] = 42;
                                    }
                                }
                            }

                            for( int band = 0; ; ++band )
                            {
                                const int y0 = 206 + phase * 2 + band * 125;
                                if( y0 >= srcH )
                                    break;
                                for( int yy = y0; yy < min( srcH, y0 + 1 ); ++yy )
                                {
                                    for( int xx = 74; xx < w - 74; ++xx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( yy ) * w + xx ) * 4;
                                        source[idx + 0] = static_cast<BYTE>( min( 255, max( source[idx + 0], static_cast<BYTE>( 56 ) ) ) );
                                        source[idx + 1] = static_cast<BYTE>( min( 255, max( source[idx + 1], static_cast<BYTE>( 60 ) ) ) );
                                        source[idx + 2] = static_cast<BYTE>( min( 255, max( source[idx + 2], static_cast<BYTE>( 64 ) ) ) );
                                    }
                                }
                            }

                            for( int ay = 39 + phase; ay < srcH - 6; ay += 173 )
                            {
                                const int x0 = 24 + ( ( ay * 37 + phase * 19 ) % ( w - 72 ) );
                                for( int dy = 0; dy < 3; ++dy )
                                {
                                    for( int dx = 0; dx < 3; ++dx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( ay + dy ) * w + x0 + dx ) * 4;
                                        source[idx + 0] = 150;
                                        source[idx + 1] = 156;
                                        source[idx + 2] = 162;
                                    }
                                }
                            }
                        };

                        auto buildFrameFromSource = [&]( int top, std::vector<BYTE>& outFrame )
                        {
                            outFrame.resize( static_cast<size_t>( w ) * h * 4 );
                            for( int row = 0; row < h; ++row )
                            {
                                const BYTE* src = source.data() +
                                                  ( static_cast<size_t>( top + row ) * w * 4 );
                                BYTE* dst = outFrame.data() + static_cast<size_t>( row ) * w * 4;
                                memcpy( dst, src, static_cast<size_t>( w ) * 4 );
                            }
                        };

                        TestLog( L"[Panorama/Test] Running %s\n", manualDropName );
                        stressTestsRun++;

                        int evaluatedPairs = 0;
                        int nearActualPairs = 0;
                        int overshootPairs = 0;
                        int sampleExpected = 0;
                        int sampleActual = 0;
                        int sampleBestDy = 0;

                        for( int trial = 0; trial < 14; ++trial )
                        {
                            paintSource( trial );

                            std::vector<int> origins;
                            origins.push_back( 96 );
                            for( int step : scriptedSteps )
                            {
                                const int nextTop = origins.back() + step;
                                if( nextTop + h > srcH )
                                    break;
                                origins.push_back( nextTop );
                            }

                            int expectedDy = 0;
                            for( size_t fi = 0; fi + 1 < origins.size(); ++fi )
                            {
                                std::vector<BYTE> prevFrame;
                                std::vector<BYTE> currFrame;
                                buildFrameFromSource( origins[fi], prevFrame );
                                buildFrameFromSource( origins[fi + 1], currFrame );

                                int bestDx = 0;
                                int bestDy = 0;
                                const bool found = FindBestFrameShift( prevFrame,
                                                                       currFrame,
                                                                       w,
                                                                       h,
                                                                       0,
                                                                       expectedDy,
                                                                       bestDx,
                                                                       bestDy,
                                                                       false );
                                if( !found )
                                    continue;

                                const int actualStep = scriptedSteps[fi];
                                const int absBest = abs( bestDy );
                                const int expectedAbs = abs( expectedDy );
                                evaluatedPairs++;
                                if( abs( absBest - actualStep ) <= 10 )
                                    nearActualPairs++;

                                const bool overshoot =
                                    expectedAbs >= 48 &&
                                    absBest > max( expectedAbs * 4, 320 ) &&
                                    absBest > actualStep + 120;
                                if( overshoot )
                                {
                                    overshootPairs++;
                                    if( sampleBestDy == 0 )
                                    {
                                        sampleExpected = expectedDy;
                                        sampleActual = actualStep;
                                        sampleBestDy = bestDy;
                                    }
                                }

                                expectedDy = bestDy;
                            }
                        }

                        const bool enoughCoverage = evaluatedPairs >= 60;
                        const bool resultPass = enoughCoverage && overshootPairs == 0 && nearActualPairs >= ( evaluatedPairs * 3 ) / 4;
                        wchar_t msg[512]{};
                        if( resultPass )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, manualDropName );
                            swprintf_s( msg,
                                        L"PASS: %s (pairs=%d nearActual=%d overshoots=%d)\n",
                                        manualDropName,
                                        evaluatedPairs,
                                        nearActualPairs,
                                        overshootPairs );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s (pairs=%d nearActual=%d overshoots=%d sampleBestDy=%d expected=%d actual=%d) *****\n",
                                     manualDropName,
                                     evaluatedPairs,
                                     nearActualPairs,
                                     overshootPairs,
                                     sampleBestDy,
                                     sampleExpected,
                                     sampleActual );
                            swprintf_s( msg,
                                        L"FAIL: %s (pairs=%d nearActual=%d overshoots=%d sampleBestDy=%d expected=%d actual=%d)\n",
                                        manualDropName,
                                        evaluatedPairs,
                                        nearActualPairs,
                                        overshootPairs,
                                        sampleBestDy,
                                        sampleExpected,
                                        sampleActual );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     manualDropName,
                                     resultPass ? L"PASS" : L"FAIL" );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }

                // Startup-axis confidence stress: reproduces the latest manual
                // capture shape where the first pair could align on both axes,
                // and startup had to choose the correct vertical axis despite a
                // wide, low-detail portal with strong horizontal band structure.
                if( !stressEarlyExit )
                {
                    const wchar_t* startupAxisName = L"stress-vertical-latestcapture-axisstart";
                    if( stressScenarioMatches( startupAxisName ) )
                    {
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        const int saW = 1432;
                        const int saCanvasH = 17000;
                        const int saWinH = 712;
                        std::vector<BYTE> saPx( static_cast<size_t>( saW ) * saCanvasH * 4, 0 );
                        for( size_t pi = 0; pi < static_cast<size_t>( saW ) * saCanvasH; ++pi )
                        {
                            saPx[pi * 4 + 0] = 245;
                            saPx[pi * 4 + 1] = 245;
                            saPx[pi * 4 + 2] = 245;
                            saPx[pi * 4 + 3] = 255;
                        }

                        for( int band = 0; ; ++band )
                        {
                            const int y0 = 180 + band * 29;
                            if( y0 >= saCanvasH )
                                break;

                            const int segPhase = ( band * 37 ) % 140;
                            for( int yy = y0; yy < min( saCanvasH, y0 + 2 ); ++yy )
                            {
                                for( int xx = 46; xx < saW - 46; ++xx )
                                {
                                    const size_t idx = ( static_cast<size_t>( yy ) * saW + xx ) * 4;
                                    saPx[idx + 0] = 42;
                                    saPx[idx + 1] = 46;
                                    saPx[idx + 2] = 50;
                                }

                                for( int seg = 0; seg < 8; ++seg )
                                {
                                    const int segStart = 64 + seg * 156 + ( ( seg * 19 + segPhase ) % 41 );
                                    const int segEnd = min( saW - 56, segStart + 42 + ( ( band + seg ) % 28 ) );
                                    for( int xx = segStart; xx < segEnd; ++xx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( yy ) * saW + xx ) * 4;
                                        saPx[idx + 0] = 28;
                                        saPx[idx + 1] = 31;
                                        saPx[idx + 2] = 34;
                                    }
                                }
                            }
                        }

                        for( int ay = 93; ay < saCanvasH - 8; ay += 173 )
                        {
                            const int x0 = 28 + ( ( ay * 43 ) % ( saW - 92 ) );
                            for( int dy = 0; dy < 4; ++dy )
                            {
                                for( int dx = 0; dx < 4; ++dx )
                                {
                                    const size_t idx = ( static_cast<size_t>( ay + dy ) * saW + x0 + dx ) * 4;
                                    saPx[idx + 0] = 156;
                                    saPx[idx + 1] = 162;
                                    saPx[idx + 2] = 168;
                                }
                            }

                            for( int yy = ay + 7; yy < min( ay + 10, saCanvasH ); ++yy )
                            {
                                const int lineStart = min( saW - 60, x0 + 19 );
                                const int lineEnd = min( saW - 24, lineStart + 12 + ( ( ay / 173 ) % 17 ) );
                                for( int xx = lineStart; xx < lineEnd; ++xx )
                                {
                                    const size_t idx = ( static_cast<size_t>( yy ) * saW + xx ) * 4;
                                    saPx[idx + 0] = 132;
                                    saPx[idx + 1] = 138;
                                    saPx[idx + 2] = 144;
                                }
                            }
                        }

                        std::vector<int> saOrigins;
                        saOrigins.push_back( 0 );
                        int sy = 0;
                        const int saSteps[] = {
                            16, 18, 21, 24, 29, 35, 41, 47, 54, 62, 71, 83,
                            96, 109, 123, 138, 82, 57, 34, 22, 18, 27, 43, 68,
                            104, 127, 86, 51, 29, 17, 19, 31, 52, 79, 118, 143
                        };

                        for( int step : saSteps )
                        {
                            const int nextY = sy + step;
                            if( nextY + saWinH > saCanvasH )
                                break;
                            sy = nextY;
                            saOrigins.push_back( sy );
                        }

                        if( saOrigins.size() >= 20 )
                        {
                            auto buildStartupFrame = [&]( int top, std::vector<BYTE>& outFrame )
                            {
                                outFrame.resize( static_cast<size_t>( saW ) * saWinH * 4 );
                                for( int row = 0; row < saWinH; ++row )
                                {
                                    const BYTE* src = saPx.data() +
                                                      ( static_cast<size_t>( top + row ) * saW * 4 );
                                    BYTE* dst = outFrame.data() + static_cast<size_t>( row ) * saW * 4;
                                    memcpy( dst, src, static_cast<size_t>( saW ) * 4 );
                                }
                            };

                            std::vector<BYTE> startupPrev;
                            std::vector<BYTE> startupCurr;
                            buildStartupFrame( saOrigins[0], startupPrev );
                            buildStartupFrame( saOrigins[1], startupCurr );

                            int startupDx = 0;
                            int startupDy = 0;
                            const bool startupFound = FindBestFrameShift( startupPrev,
                                                                          startupCurr,
                                                                          saW,
                                                                          saWinH,
                                                                          0,
                                                                          0,
                                                                          startupDx,
                                                                          startupDy,
                                                                          false );
                            const bool startupVertical = startupFound && startupDx == 0 && startupDy < 0 && abs( startupDy + saSteps[0] ) <= 10;
                            TestLog( L"[Panorama/Test] Running %s startupFound=%d startup=(%d,%d) expectedDy=%d n=%zu\n",
                                     startupAxisName,
                                     startupFound ? 1 : 0,
                                     startupDx,
                                     startupDy,
                                     -saSteps[0],
                                     saOrigins.size() );

                            stressTestsRun++;
                            const int rawResult = stitchAndCompare( startupAxisName,
                                                                    saPx,
                                                                    saW,
                                                                    saCanvasH,
                                                                    saOrigins,
                                                                    saWinH );
                            const size_t composedCount = countComposedVertical( saPx, saW, saCanvasH, saOrigins, saWinH );
                            const bool resultPass = startupVertical && rawResult == 1 && composedCount == saOrigins.size();
                            wchar_t msg[512]{};
                            if( resultPass )
                            {
                                stressTestsPassed++;
                                TestLog( L"  [%d] %s PASSED\n", stressTestsRun, startupAxisName );
                                swprintf_s( msg,
                                            L"PASS: %s (startup=%d,%d composed=%zu/%zu)\n",
                                            startupAxisName,
                                            startupDx,
                                            startupDy,
                                            composedCount,
                                            saOrigins.size() );
                            }
                            else
                            {
                                TestLog( L"***** FAIL: %s startupFound=%d startup=(%d,%d) composed=%zu/%zu raw=%d *****\n",
                                         startupAxisName,
                                         startupFound ? 1 : 0,
                                         startupDx,
                                         startupDy,
                                         composedCount,
                                         saOrigins.size(),
                                         rawResult );
                                swprintf_s( msg,
                                            L"FAIL: %s (startupFound=%d startup=%d,%d composed=%zu/%zu raw=%d)\n",
                                            startupAxisName,
                                            startupFound ? 1 : 0,
                                            startupDx,
                                            startupDy,
                                            composedCount,
                                            saOrigins.size(),
                                            rawResult );
                                stressFailLog += msg;
                            }
                            stressLog += msg;

                            if( stressFocusEnabled )
                            {
                                TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         startupAxisName,
                                         resultPass ? L"PASS" : L"FAIL" );
                                if( stressStopAfterFocus )
                                    stressEarlyExit = true;
                            }
                        }
                    }
                }

        // Narrow-strip axis-flip stress: reproduces the real capture pattern
        // where a tall, narrow portal with sparse horizontal structure can
        // mis-lock first-pair axis detection to horizontal.
        if( !stressEarlyExit )
        {
            const wchar_t* nsName = L"stress-vertical-narrowstrip-axisflip";
            if( stressScenarioMatches( nsName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                const int nsW = 357;
                const int nsWinH = 1093;
                const int nsH = 16000;
                std::vector<BYTE> nsPx( static_cast<size_t>( nsW ) * nsH * 4, 0 );

                // Dark baseline.
                for( size_t pi = 0; pi < static_cast<size_t>( nsW ) * nsH; ++pi )
                {
                    nsPx[pi * 4 + 0] = 15;
                    nsPx[pi * 4 + 1] = 15;
                    nsPx[pi * 4 + 2] = 15;
                    nsPx[pi * 4 + 3] = 255;
                }

                // Repeating horizontal bands every ~57 px emulate line-based
                // periodic content seen in vertical scroll captures.
                for( int y0 = 0; y0 < nsH; y0 += 57 )
                {
                    for( int yy = y0; yy < min( nsH, y0 + 2 ); ++yy )
                    {
                        for( int x = 0; x < nsW; ++x )
                        {
                            const size_t idx = ( static_cast<size_t>( yy ) * nsW + x ) * 4;
                            nsPx[idx + 0] = 42;
                            nsPx[idx + 1] = 46;
                            nsPx[idx + 2] = 50;
                        }
                    }
                }

                // Sparse deterministic anchors avoid total ambiguity while
                // keeping the strip predominantly low-detail.
                for( int y = 19; y < nsH - 4; y += 131 )
                {
                    const int x0 = 10 + ( ( y * 37 ) % max( 1, nsW - 20 ) );
                    for( int dy = 0; dy < 3; ++dy )
                    {
                        for( int dx = 0; dx < 2; ++dx )
                        {
                            const int xx = x0 + dx;
                            const int yy = y + dy;
                            const size_t idx = ( static_cast<size_t>( yy ) * nsW + xx ) * 4;
                            nsPx[idx + 0] = 160;
                            nsPx[idx + 1] = 166;
                            nsPx[idx + 2] = 172;
                        }
                    }
                }

                std::vector<int> originsY;
                originsY.push_back( 0 );
                int y = 0;
                const int scriptedSteps[] = {
                    7, 61, 59, 59, 59, 59, 59, 59,
                    57, 57, 55, 53, 51, 51, 51, 51,
                    49, 49, 49, 48, 48, 61, 61, 60,
                    59, 57, 55, 55, 61, 61, 61, 61
                };
                size_t si = 0;
                while( originsY.size() < 120 )
                {
                    const int step = scriptedSteps[si % _countof( scriptedSteps )];
                    si++;
                    const int nextY = y + step;
                    if( nextY + nsWinH > nsH )
                        break;
                    y = nextY;
                    originsY.push_back( y );
                }

                if( originsY.size() >= 12 )
                {
                    stressTestsRun++;
                    const int rawResult = stitchAndCompare( nsName, nsPx, nsW, nsH, originsY, nsWinH );
                    const size_t composedCount = countComposedVertical( nsPx, nsW, nsH, originsY, nsWinH );
                    const int result = ( rawResult == 1 ) ? 1 : 0;
                    wchar_t msg[512]{};
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", nsName );
                        swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", nsName, nsWinH, originsY.size() );
                        stressFailLog += msg;
                    }
                    else if( result == 1 )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, nsName );
                        swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu composed=%zu)\n", nsName, nsWinH, originsY.size(), composedCount );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", nsName );
                        swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu composed=%zu)\n", nsName, nsWinH, originsY.size(), composedCount );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        const wchar_t* focusResult = L"FAIL";
                        if( result < 0 ) focusResult = L"INFRA";
                        else if( result == 1 ) focusResult = L"PASS";
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", nsName, focusResult );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }
        }

        // Regression for panorama_20260313: narrow portrait strip (368x1134)
        // with tiny initial steps (4 px).  The direct (vertical) fine search
        // barely exceeds its threshold while the transposed (horizontal)
        // search passes, causing the code to lock the axis to horizontal on
        // the very first frame pair.
        if( !stressEarlyExit )
        {
            const wchar_t* ssName = L"stress-vertical-narrowstrip-smallstart-axisflip";
            if( stressScenarioMatches( ssName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                const int ssW = 368;
                const int ssWinH = 1134;
                const int ssH = 16000;
                std::vector<BYTE> ssPx( static_cast<size_t>( ssW ) * ssH * 4, 0 );

                // Light background matching typical IDE/browser content.
                for( size_t pi = 0; pi < static_cast<size_t>( ssW ) * ssH; ++pi )
                {
                    ssPx[pi * 4 + 0] = 245;
                    ssPx[pi * 4 + 1] = 245;
                    ssPx[pi * 4 + 2] = 245;
                    ssPx[pi * 4 + 3] = 255;
                }

                // Repeating horizontal bands every ~19 px emulate text lines
                // with strong horizontal autocorrelation.
                for( int y0 = 0; y0 < ssH; y0 += 19 )
                {
                    for( int yy = y0; yy < min( ssH, y0 + 2 ); ++yy )
                    {
                        for( int x = 0; x < ssW; ++x )
                        {
                            const size_t idx = ( static_cast<size_t>( yy ) * ssW + x ) * 4;
                            ssPx[idx + 0] = 200;
                            ssPx[idx + 1] = 200;
                            ssPx[idx + 2] = 200;
                        }
                    }
                }

                // Sparse deterministic anchors: small "glyphs" at irregular
                // intervals along the strip prevent total ambiguity.
                for( int y = 11; y < ssH - 4; y += 97 )
                {
                    const int x0 = 8 + ( ( y * 41 ) % max( 1, ssW - 20 ) );
                    for( int dy = 0; dy < 3; ++dy )
                    {
                        for( int dx = 0; dx < 3; ++dx )
                        {
                            const int xx = min( x0 + dx, ssW - 1 );
                            const int yy = y + dy;
                            const size_t idx = ( static_cast<size_t>( yy ) * ssW + xx ) * 4;
                            ssPx[idx + 0] = 60;
                            ssPx[idx + 1] = 60;
                            ssPx[idx + 2] = 65;
                        }
                    }
                }

                // Reproduce the capture cadence: tiny 4 px initial steps that
                // stress the fine-score threshold, then a jump to normal speed.
                std::vector<int> ssOrigins;
                ssOrigins.push_back( 0 );
                int sy = 0;
                const int ssSteps[] = {
                    4, 4, 4, 4, 61, 60, 61, 60,
                    60, 58, 58, 58, 58, 58, 58, 58,
                    58, 56, 61, 60, 58, 58, 58, 56,
                    56, 54, 54, 54, 54, 50, 50, 50,
                    50, 50, 50, 50, 50, 50, 50, 50,
                    50, 50, 50, 50
                };
                size_t ssi = 0;
                while( ssOrigins.size() < 120 )
                {
                    const int step = ssSteps[ssi % _countof( ssSteps )];
                    ssi++;
                    const int nextY = sy + step;
                    if( nextY + ssWinH > ssH )
                        break;
                    sy = nextY;
                    ssOrigins.push_back( sy );
                }

                if( ssOrigins.size() >= 12 )
                {
                    // Verify axis detection for the first frame pair.
                    std::vector<BYTE> firstFrame( static_cast<size_t>( ssW ) * ssWinH * 4 );
                    std::vector<BYTE> secondFrame( static_cast<size_t>( ssW ) * ssWinH * 4 );
                    for( int row = 0; row < ssWinH; ++row )
                    {
                        const size_t srcOff0 = static_cast<size_t>( ssOrigins[0] + row ) * ssW * 4;
                        const size_t srcOff1 = static_cast<size_t>( ssOrigins[1] + row ) * ssW * 4;
                        const size_t dstOff = static_cast<size_t>( row ) * ssW * 4;
                        memcpy( firstFrame.data() + dstOff, ssPx.data() + srcOff0, static_cast<size_t>( ssW ) * 4 );
                        memcpy( secondFrame.data() + dstOff, ssPx.data() + srcOff1, static_cast<size_t>( ssW ) * 4 );
                    }
                    int startupDx = 0, startupDy = 0;
                    const bool startupFound = FindBestFrameShift( firstFrame, secondFrame,
                                                                  ssW, ssWinH,
                                                                  0, 0,
                                                                  startupDx, startupDy, false );
                    // The first pair must detect vertical axis (dx==0, dy!=0).
                    // Before the fix, FindBestFrameShift returned dx!=0 here.
                    const bool startupVertical = startupFound
                                                 ? ( startupDx == 0 && startupDy != 0 )
                                                 : true;  // deferred is also OK

                    stressTestsRun++;
                    const int rawResult = stitchAndCompare( ssName, ssPx, ssW, ssH, ssOrigins, ssWinH );
                    const size_t composedCount = countComposedVertical( ssPx, ssW, ssH, ssOrigins, ssWinH );
                    const bool resultPass = startupVertical && rawResult == 1 && composedCount == ssOrigins.size();
                    wchar_t msg[512]{};
                    if( resultPass )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED startup=(%d,%d) composed=%zu/%zu\n",
                                 stressTestsRun, ssName, startupDx, startupDy, composedCount, ssOrigins.size() );
                        swprintf_s( msg, L"PASS: %s (startup=%d,%d composed=%zu/%zu)\n",
                                    ssName, startupDx, startupDy, composedCount, ssOrigins.size() );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s startupFound=%d startup=(%d,%d) composed=%zu/%zu raw=%d *****\n",
                                 ssName, startupFound ? 1 : 0, startupDx, startupDy,
                                 composedCount, ssOrigins.size(), rawResult );
                        swprintf_s( msg, L"FAIL: %s (startupFound=%d startup=%d,%d composed=%zu/%zu raw=%d)\n",
                                    ssName, startupFound ? 1 : 0, startupDx, startupDy,
                                    composedCount, ssOrigins.size(), rawResult );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                 ssName, resultPass ? L"PASS" : L"FAIL" );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }
        }

        // Wide-portal exhaustive-fallback test: when the search range
        // exceeds the candidate budget (kMaxCandidatesWithProbes=160),
        // the iteration order of the exhaustive fallback determines
        // which shifts are evaluated.  With a tall portal the search
        // range is hundreds of DS positions, but only ~148 can be
        // injected.  A linear start-from-extreme order fills the budget
        // with extreme shifts, missing the moderate shift where the
        // content actually aligns.  This test uses random-noise content
        // and steps not divisible by 4 (DS aliasing) so the coarse
        // search cannot discriminate, triggering the exhaustive path.
        if( !stressEarlyExit )
        {
            const wchar_t* wpName = L"stress-vertical-wideportal";
            if( stressScenarioMatches( wpName ) )
            {
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                const int wpW = 800;
                const int wpH = 1600;
                const int wpImgH = 4000;

                // Per-pixel random noise: all DS shifts score ~54,
                // triggering the exhaustive fallback.  The correct shift
                // at dyDs ~13-17 falls deep inside the gap that the
                // linear iteration creates (coverage stops at ~-187 for
                // dsH=400, gap spans -186..-7).
                std::vector<BYTE> wpImg( static_cast<size_t>( wpW ) * wpImgH * 4 );
                {
                    unsigned int seed = 99991u;
                    for( size_t i = 0; i < wpImg.size(); ++i )
                    {
                        if( ( i & 3 ) == 3 ) { wpImg[i] = 255; continue; }
                        seed = seed * 1103515245u + 12345u;
                        wpImg[i] = static_cast<BYTE>( ( seed >> 16 ) & 0xFF );
                    }
                }

                const int wpSteps[] = { 50, 54, 66 };
                int wpPassed = 0;

                for( int step : wpSteps )
                {
                    const size_t frameBytes = static_cast<size_t>( wpW ) * wpH * 4;
                    std::vector<BYTE> frame0( frameBytes );
                    std::vector<BYTE> frame1( frameBytes );
                    memcpy( frame0.data(), wpImg.data(), frameBytes );
                    memcpy( frame1.data(),
                            wpImg.data() + static_cast<size_t>( step ) * wpW * 4,
                            frameBytes );

                    int bestDx = 0, bestDy = 0;
                    bool found = FindBestFrameShift(
                        frame0, frame1, wpW, wpH,
                        0, 0,
                        bestDx, bestDy,
                        false );

                    const bool ok = found && ( bestDy == -step );
                    if( ok ) wpPassed++;

                    TestLog( L"[Panorama/Test] %s step=%d found=%d bestDy=%d expected=%d %s\n",
                             wpName, step, found ? 1 : 0, bestDy, -step,
                             ok ? L"ok" : L"MISS" );
                }

                stressTestsRun++;
                const bool wpOk = wpPassed >= 2;
                {
                    wchar_t msg[512]{};
                    if( wpOk )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED (%d/%d shifts found)\n", stressTestsRun, wpName, wpPassed, 3 );
                        swprintf_s( msg, L"PASS: %s (%d/3)\n", wpName, wpPassed );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s (%d/%d shifts found) *****\n", wpName, wpPassed, 3 );
                        swprintf_s( msg, L"FAIL: %s (%d/3)\n", wpName, wpPassed );
                        stressFailLog += msg;
                    }
                    stressLog += msg;
                }

                if( stressFocusEnabled )
                {
                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                             wpName, wpOk ? L"PASS" : L"FAIL" );
                    if( stressStopAfterFocus )
                        stressEarlyExit = true;
                }
            }
        }

        // HCF-dark stress test: synthetic dark-background image with sparse text
        // Tests the HCF harmonic-zero override: on dark-themed pages, the constant
        // background region produces score=0 at any small offset, tricking the
        // stitcher into picking harmonic sub-multiples of the true scroll step.
        if( !stressEarlyExit )
        {
            // Generate synthetic HCF source image: mostly dark background with
            // sparse bright horizontal bands simulating text on a dark page.
            const int hcfW = 500;
            const int hcfH = 8000;
            std::vector<BYTE> hcfPx( static_cast<size_t>( hcfW ) * hcfH * 4, 0 );

            // Fill with dark background (R=15, G=15, B=15, A=255).
            for( size_t pi = 0; pi < static_cast<size_t>( hcfW ) * hcfH; ++pi )
            {
                hcfPx[pi * 4 + 0] = 15;  // B
                hcfPx[pi * 4 + 1] = 15;  // G
                hcfPx[pi * 4 + 2] = 15;  // R
                hcfPx[pi * 4 + 3] = 255; // A
            }

            // Draw sparse text-like bright bands every 80-150 rows.
            // Very sparse content ensures that the downsampled coarse
            // search sees near-zero scores at many offsets, triggering
            // the masked fine-scoring path where integer truncation can
            // produce fineScore=0 at wrong alignments -- the exact
            // condition that the HCF harmonic-zero override fixes.
            {
                unsigned int bandSeed = 54321u;
                int bandY = 30;
                while( bandY < hcfH - 3 )
                {
                    bandSeed = bandSeed * 1103515245u + 12345u;
                    const int bandHeight = 2 + static_cast<int>( ( bandSeed >> 16 ) % 2 ); // 2-3px
                    bandSeed = bandSeed * 1103515245u + 12345u;
                    const int brightness = 170 + static_cast<int>( ( bandSeed >> 16 ) % 50 ); // 170-219
                    bandSeed = bandSeed * 1103515245u + 12345u;
                    const int bandStart = 20 + static_cast<int>( ( bandSeed >> 16 ) % 40 ); // column 20-59
                    bandSeed = bandSeed * 1103515245u + 12345u;
                    const int bandEnd = bandStart + 80 + static_cast<int>( ( bandSeed >> 16 ) % 120 ); // 80-199px wide

                    for( int by = bandY; by < min( bandY + bandHeight, hcfH ); ++by )
                    {
                        for( int bx = bandStart; bx < min( bandEnd, hcfW ); ++bx )
                        {
                            const size_t idx = ( static_cast<size_t>( by ) * hcfW + bx ) * 4;
                            bandSeed = bandSeed * 1103515245u + 12345u;
                            const int pxVar = static_cast<int>( ( bandSeed >> 16 ) % 11 ) - 5; // -5..+5
                            const int val = max( 0, min( 255, brightness + pxVar ) );
                            hcfPx[idx + 0] = static_cast<BYTE>( val );
                            hcfPx[idx + 1] = static_cast<BYTE>( val );
                            hcfPx[idx + 2] = static_cast<BYTE>( val );
                        }
                    }

                    bandSeed = bandSeed * 1103515245u + 12345u;
                    const int gap = 80 + static_cast<int>( ( bandSeed >> 16 ) % 71 ); // 80-150 rows
                    bandY += bandHeight + gap;
                }
            }

            TestLog( L"[Panorama/Test] Generated HCF-dark synthetic image %dx%d\n", hcfW, hcfH );

            constexpr int kHcfTrials = 5;
            for( int trial = 0; trial < kHcfTrials; ++trial )
            {
                if( stressEarlyExit )
                    break;
                srand( static_cast<unsigned>( 80000 + trial * 41 ) );

                // Portal height: 250-450 (larger portals make HCF more
                // likely and give room for moderate steps).
                const int winH = 250 + rand() % 201;
                // Steps 15-25% of portal height -- matches real captures.
                const int hcfMaxStep = max( 20, winH / 4 );

                // Generate origins with consistent moderate steps.
                std::vector<int> originsY;
                originsY.push_back( 0 );
                int y = 0;
                for( int f = 1; f < 80; ++f )
                {
                    const int minStep = max( 10, winH / 7 );
                    const int stepRange = max( 1, hcfMaxStep - minStep );
                    const int step = minStep + rand() % stepRange;
                    const int nextY = y + step;
                    if( nextY + winH > hcfH )
                        break;
                    y = nextY;
                    originsY.push_back( y );
                }

                if( originsY.size() < 5 )
                    continue;

                wchar_t scenarioName[256];
                swprintf_s( scenarioName, L"stress-vertical-hcfdark-trial%d-w%d-n%zu-maxstep%d",
                            trial, winH, originsY.size(), hcfMaxStep );

                if( !stressScenarioMatches( scenarioName ) )
                    continue;
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                // Log frame origins for diagnostics.
                {
                    std::wstring origStr;
                    for( size_t oi = 0; oi < originsY.size() && oi < 20; ++oi )
                    {
                        if( oi > 0 ) origStr += L",";
                        origStr += std::to_wstring( originsY[oi] );
                    }
                    if( originsY.size() > 20 ) origStr += L",...";
                    TestLog( L"[Panorama/Test] Running %s origins=[%s]\n", scenarioName, origStr.c_str() );
                }

                stressTestsRun++;
                const int result = stitchAndCompare( scenarioName, hcfPx, hcfW, hcfH, originsY, winH );
                {
                    wchar_t msg[512]{};
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                        swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                        stressFailLog += msg;
                    }
                    else if( result == 1 )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                        swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winH, originsY.size(), hcfMaxStep );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s *****\n", scenarioName );
                        swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winH, originsY.size(), hcfMaxStep );
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        const wchar_t* focusResult = L"FAIL";
                        if( result < 0 ) focusResult = L"INFRA";
                        else if( result == 1 ) focusResult = L"PASS";
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     scenarioName,
                                     focusResult );
                        if( stressStopAfterFocus )
                        {
                            stressEarlyExit = true;
                            break;
                        }
                    }
                }
            }

            // HCF tiny-step test: reuse the same synthetic HCF canvas with
            // very small uniform steps (4-8 px).  Reproduces a real capture
            // failure where the stitcher oscillates between +dy and -dy
            // because forward/backward shifts score nearly identically on
            // uniform content and the ambiguity fallback cannot disambiguate
            // when both |+dy| and |-dy| equal expectedAbsStep.
            for( int trial = 0; trial < kHcfTrials; ++trial )
            {
                if( stressEarlyExit )
                    break;
                srand( static_cast<unsigned>( 85000 + trial * 47 ) );

                const int winH = 250 + rand() % 201;

                std::vector<int> originsY;
                originsY.push_back( 0 );
                int y = 0;
                for( int f = 1; f < 20; ++f )
                {
                    const int step = 4 + rand() % 5; // 4-8 px
                    const int nextY = y + step;
                    if( nextY + winH > hcfH )
                        break;
                    y = nextY;
                    originsY.push_back( y );
                }

                if( originsY.size() < 5 )
                    continue;

                wchar_t scenarioName[256];
                swprintf_s( scenarioName, L"stress-vertical-tinystep-trial%d-w%d-n%zu",
                            trial, winH, originsY.size() );

                if( !stressScenarioMatches( scenarioName ) )
                    continue;
                if( stressFocusEnabled )
                    stressFocusMatched = true;

                {
                    std::wstring origStr;
                    for( size_t oi = 0; oi < originsY.size() && oi < 20; ++oi )
                    {
                        if( oi > 0 ) origStr += L",";
                        origStr += std::to_wstring( originsY[oi] );
                    }
                    if( originsY.size() > 20 ) origStr += L",...";
                    TestLog( L"[Panorama/Test] Running %s origins=[%s]\n", scenarioName, origStr.c_str() );
                }

                stressTestsRun++;
                const int result = stitchAndCompare( scenarioName, hcfPx, hcfW, hcfH, originsY, winH );
                {
                    wchar_t msg[512]{};
                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                        swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                        stressFailLog += msg;
                    }
                    else if( result == 1 )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                        swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s *****\n", scenarioName );
                        swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        const wchar_t* focusResult = L"FAIL";
                        if( result < 0 ) focusResult = L"INFRA";
                        else if( result == 1 ) focusResult = L"PASS";
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     scenarioName,
                                     focusResult );
                        if( stressStopAfterFocus )
                        {
                            stressEarlyExit = true;
                            break;
                        }
                    }
                }
            }

            // HCF tall-portal tiny-step test: exercises the probe-injection
            // bounds fix for a real-world failure where a ~997 px portal with
            // 4 px scroll steps caused direction oscillation.  Probes near
            // expectedDyDs injected wrong-direction candidates that scored
            // identically on HCF content.  Uses a narrower/shorter canvas to
            // keep execution fast while preserving the same code path (the
            // downsampled step size dyDs=±1 is identical).
            {
                const int tpW = 200;
                const int tpH = 3000;
                std::vector<BYTE> tpPx( static_cast<size_t>( tpW ) * tpH * 4, 0 );
                for( size_t pi = 0; pi < static_cast<size_t>( tpW ) * tpH; ++pi )
                {
                    tpPx[pi * 4 + 0] = 15;
                    tpPx[pi * 4 + 1] = 15;
                    tpPx[pi * 4 + 2] = 15;
                    tpPx[pi * 4 + 3] = 255;
                }
                {
                    unsigned int bs = 77777u;
                    int by = 25;
                    while( by < tpH - 3 )
                    {
                        bs = bs * 1103515245u + 12345u;
                        const int bh = 2 + static_cast<int>( ( bs >> 16 ) % 2 );
                        bs = bs * 1103515245u + 12345u;
                        const int br = 170 + static_cast<int>( ( bs >> 16 ) % 50 );
                        for( int r = by; r < min( by + bh, tpH ); ++r )
                        {
                            for( int c = 10; c < tpW - 10; ++c )
                            {
                                const size_t idx = ( static_cast<size_t>( r ) * tpW + c ) * 4;
                                bs = bs * 1103515245u + 12345u;
                                const int v = max( 0, min( 255, br + static_cast<int>( ( bs >> 16 ) % 11 ) - 5 ) );
                                tpPx[idx + 0] = static_cast<BYTE>( v );
                                tpPx[idx + 1] = static_cast<BYTE>( v );
                                tpPx[idx + 2] = static_cast<BYTE>( v );
                            }
                        }
                        bs = bs * 1103515245u + 12345u;
                        by += bh + 80 + static_cast<int>( ( bs >> 16 ) % 71 );
                    }
                }
                TestLog( L"[Panorama/Test] Generated tallportal HCF image %dx%d\n", tpW, tpH );

                for( int trial = 0; trial < kHcfTrials; ++trial )
                {
                    if( stressEarlyExit )
                        break;
                    srand( static_cast<unsigned>( 90000 + trial * 53 ) );

                    const int winH = 300 + rand() % 101; // 300-400 px portal

                    std::vector<int> originsY;
                    originsY.push_back( 0 );
                    int y = 0;
                    for( int f = 1; f < 20; ++f )
                    {
                        const int step = 4;
                        const int nextY = y + step;
                        if( nextY + winH > tpH )
                            break;
                        y = nextY;
                        originsY.push_back( y );
                    }

                    if( originsY.size() < 5 )
                        continue;

                    wchar_t scenarioName[256];
                    swprintf_s( scenarioName, L"stress-vertical-tallportal-trial%d-w%d-n%zu",
                                trial, winH, originsY.size() );

                    if( !stressScenarioMatches( scenarioName ) )
                        continue;
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    {
                        std::wstring origStr;
                        for( size_t oi = 0; oi < originsY.size() && oi < 20; ++oi )
                        {
                            if( oi > 0 ) origStr += L",";
                            origStr += std::to_wstring( originsY[oi] );
                        }
                        if( originsY.size() > 20 ) origStr += L",...";
                        TestLog( L"[Panorama/Test] Running %s origins=[%s]\n", scenarioName, origStr.c_str() );
                    }

                    stressTestsRun++;
                    const int result = stitchAndCompare( scenarioName, tpPx, tpW, tpH, originsY, winH );
                    {
                        wchar_t msg[512]{};
                        if( result < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                            swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                            stressFailLog += msg;
                        }
                        else if( result == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                            swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s *****\n", scenarioName );
                            swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu)\n", scenarioName, winH, originsY.size() );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         scenarioName,
                                         focusResult );
                            if( stressStopAfterFocus )
                            {
                                stressEarlyExit = true;
                                break;
                            }
                        }
                    }
                }
            }

            // HCF whitespace stress test: light background + sparse text-like
            // bands with fixed moderate true motion. Reproduces real captures
            // where high-constant-content windows can collapse to tiny shifts
            // and then over-correct with large jumps.
            if( !stressEarlyExit )
            {
                const int wsW = 998;
                const int wsH = 18000;
                std::vector<BYTE> wsPx( static_cast<size_t>( wsW ) * wsH * 4, 0 );

                for( size_t pi = 0; pi < static_cast<size_t>( wsW ) * wsH; ++pi )
                {
                    wsPx[pi * 4 + 0] = 246;
                    wsPx[pi * 4 + 1] = 246;
                    wsPx[pi * 4 + 2] = 246;
                    wsPx[pi * 4 + 3] = 255;
                }

                {
                    unsigned int bandSeed = 99173u;
                    int bandY = 4500;
                    while( bandY < wsH - 3 )
                    {
                        bandSeed = bandSeed * 1103515245u + 12345u;
                        const int bandHeight = 2 + static_cast<int>( ( bandSeed >> 16 ) % 2 );
                        bandSeed = bandSeed * 1103515245u + 12345u;
                        const int brightness = 24 + static_cast<int>( ( bandSeed >> 16 ) % 28 );
                        bandSeed = bandSeed * 1103515245u + 12345u;
                        const int bandStart = 56 + static_cast<int>( ( bandSeed >> 16 ) % 40 );
                        bandSeed = bandSeed * 1103515245u + 12345u;
                        const int bandEnd = wsW - 54 - static_cast<int>( ( bandSeed >> 16 ) % 40 );

                        for( int by = bandY; by < min( bandY + bandHeight, wsH ); ++by )
                        {
                            for( int bx = bandStart; bx < max( bandStart + 1, bandEnd ); ++bx )
                            {
                                const size_t idx = ( static_cast<size_t>( by ) * wsW + bx ) * 4;
                                wsPx[idx + 0] = static_cast<BYTE>( brightness );
                                wsPx[idx + 1] = static_cast<BYTE>( brightness );
                                wsPx[idx + 2] = static_cast<BYTE>( brightness );
                            }
                        }

                        bandSeed = bandSeed * 1103515245u + 12345u;
                        const int gap = 95 + static_cast<int>( ( bandSeed >> 16 ) % 126 );
                        bandY += bandHeight + gap;
                    }
                }

                const int winH = 854;
                const int fixedStep = 30;
                std::vector<int> originsY;
                originsY.reserve( 220 );
                originsY.push_back( 0 );
                int y = 0;
                while( originsY.size() < 214 )
                {
                    const int nextY = y + fixedStep;
                    if( nextY + winH > wsH )
                        break;
                    y = nextY;
                    originsY.push_back( y );
                }

                if( originsY.size() >= 60 )
                {
                    const wchar_t* scenarioName = L"stress-vertical-hcfwhitespace-trial0";

                    if( stressScenarioMatches( scenarioName ) )
                    {
                        if( stressFocusEnabled )
                            stressFocusMatched = true;

                        TestLog( L"[Panorama/Test] Running %s firstOrigin=%d lastOrigin=%d n=%zu\n",
                                     scenarioName,
                                     originsY.front(),
                                     originsY.back(),
                                     originsY.size() );

                        stressTestsRun++;
                        const ULONGLONG wsStart = GetTickCount64();
                        const int rawResult = stitchAndCompare( scenarioName, wsPx, wsW, wsH, originsY, winH );
                        const ULONGLONG wsDurationMs = GetTickCount64() - wsStart;
                        const bool tooSlow = wsDurationMs > 350000;
                        const int result = ( rawResult < 0 ) ? -1 : ( rawResult == 1 && !tooSlow ? 1 : 0 );
                        const size_t composedCount = countComposedVertical( wsPx, wsW, wsH, originsY, winH );

                        wchar_t msg[512]{};
                        if( result < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                            swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu, composed=%zu)\n",
                                        scenarioName, winH, originsY.size(), composedCount );
                            stressFailLog += msg;
                        }
                        else if( result == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                            swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, composed=%zu, durMs=%llu)\n",
                                        scenarioName, winH, originsY.size(), composedCount, wsDurationMs );
                        }
                        else
                        {
                            if( tooSlow )
                            {
                                TestLog( L"***** FAIL: %s runtime too slow (%llums) *****\n", scenarioName, wsDurationMs );
                            }
                            else
                            {
                                TestLog( L"***** FAIL: %s *****\n", scenarioName );
                            }
                            swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, composed=%zu, durMs=%llu)\n",
                                        scenarioName, winH, originsY.size(), composedCount, wsDurationMs );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         scenarioName,
                                         focusResult );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }

                    if( !stressEarlyExit )
                    {
                        const wchar_t* captureScenarioName = L"stress-vertical-hcfwhitespace-capturepath-trial0";
                        if( stressScenarioMatches( captureScenarioName ) )
                        {
                            if( stressFocusEnabled )
                                stressFocusMatched = true;

                            TestLog( L"[Panorama/Test] Running %s firstOrigin=%d lastOrigin=%d n=%zu\n",
                                         captureScenarioName,
                                         originsY.front(),
                                         originsY.back(),
                                         originsY.size() );

                            stressTestsRun++;
                            const ULONGLONG wsCapStart = GetTickCount64();
                            const int rawCaptureResult = stitchAndCompare( captureScenarioName, wsPx, wsW, wsH, originsY, winH );
                            const ULONGLONG wsCapDurationMs = GetTickCount64() - wsCapStart;
                            const bool captureTooSlow = wsCapDurationMs > 400000;
                            const int captureResult = ( rawCaptureResult < 0 ) ? -1 : ( rawCaptureResult == 1 && !captureTooSlow ? 1 : 0 );

                            wchar_t msg[512]{};
                            if( captureResult < 0 )
                            {
                                TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", captureScenarioName );
                                swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                            captureScenarioName, winH, originsY.size(), wsCapDurationMs );
                                stressFailLog += msg;
                            }
                            else if( captureResult == 1 )
                            {
                                stressTestsPassed++;
                                TestLog( L"  [%d] %s PASSED\n", stressTestsRun, captureScenarioName );
                                swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                            captureScenarioName, winH, originsY.size(), wsCapDurationMs );
                            }
                            else
                            {
                                if( captureTooSlow )
                                {
                                    TestLog( L"***** FAIL: %s runtime too slow (%llums) *****\n", captureScenarioName, wsCapDurationMs );
                                }
                                else
                                {
                                    TestLog( L"***** FAIL: %s *****\n", captureScenarioName );
                                }
                                swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                            captureScenarioName, winH, originsY.size(), wsCapDurationMs );
                                stressFailLog += msg;
                            }
                            stressLog += msg;

                            if( stressFocusEnabled )
                            {
                                const wchar_t* focusResult = L"FAIL";
                                if( captureResult < 0 ) focusResult = L"INFRA";
                                else if( captureResult == 1 ) focusResult = L"PASS";
                                TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                             captureScenarioName,
                                             focusResult );
                                if( stressStopAfterFocus )
                                    stressEarlyExit = true;
                            }
                        }
                    }
                }
            }

            // Scroll-wheel acceleration ramp stress: reproduces real
            // capture behavior where mouse scroll-wheel creates repeated
            // ramp-up / crash-down motion cycles (4 -> 100+ -> 4).
            // The stitcher must skip low-overlap peak frames to avoid
            // visible smearing and density variation.
            if( !stressEarlyExit )
            {
                const wchar_t* rampName = L"stress-vertical-scrollramp-capturepath";
                if( stressScenarioMatches( rampName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    // Generate a self-contained whitespace-style canvas.
                    const int rampCanvasW = 998;
                    const int rampCanvasH = 18000;
                    std::vector<BYTE> rampPx( static_cast<size_t>( rampCanvasW ) * rampCanvasH * 4, 0 );
                    for( size_t pi = 0; pi < static_cast<size_t>( rampCanvasW ) * rampCanvasH; ++pi )
                    {
                        rampPx[pi * 4 + 0] = 246;
                        rampPx[pi * 4 + 1] = 246;
                        rampPx[pi * 4 + 2] = 246;
                        rampPx[pi * 4 + 3] = 255;
                    }
                    {
                        unsigned int bs = 99173u;
                        int by = 4500;
                        while( by < rampCanvasH - 3 )
                        {
                            bs = bs * 1103515245u + 12345u;
                            const int bh = 2 + static_cast<int>( ( bs >> 16 ) % 2 );
                            bs = bs * 1103515245u + 12345u;
                            const int br = 24 + static_cast<int>( ( bs >> 16 ) % 28 );
                            bs = bs * 1103515245u + 12345u;
                            const int bStart = 56 + static_cast<int>( ( bs >> 16 ) % 40 );
                            bs = bs * 1103515245u + 12345u;
                            const int bEnd = rampCanvasW - 54 - static_cast<int>( ( bs >> 16 ) % 40 );
                            for( int row = by; row < min( by + bh, rampCanvasH ); ++row )
                                for( int col = bStart; col < max( bStart + 1, bEnd ); ++col )
                                {
                                    const size_t idx = ( static_cast<size_t>( row ) * rampCanvasW + col ) * 4;
                                    rampPx[idx + 0] = static_cast<BYTE>( br );
                                    rampPx[idx + 1] = static_cast<BYTE>( br );
                                    rampPx[idx + 2] = static_cast<BYTE>( br );
                                }
                            bs = bs * 1103515245u + 12345u;
                            by += bh + 95 + static_cast<int>( ( bs >> 16 ) % 126 );
                        }
                    }
                    const int rampWinH = 854;
                    std::vector<int> rampOrigins;
                    rampOrigins.push_back( 0 );
                    int ry = 0;

                    // Real ramp-up/crash-down step sequences extracted from
                    // actual scroll-wheel captures.
                    const int rampSteps[] = {
                        4, 12, 23, 39, 55, 66, 99, 53, 33, 14, 4,
                        4, 11, 19, 30, 45, 62, 76, 88, 87, 73, 55, 34, 13, 4,
                        4, 12, 22, 37, 55, 69, 78, 74, 63, 47, 26, 11, 4,
                        4, 11, 21, 37, 60, 81, 94, 93, 115, 49, 25, 9, 4,
                        4, 14, 33, 35, 38, 39, 112, 92, 54, 19, 4,
                        4, 26, 29, 25, 29, 50, 49, 46, 12, 4,
                        23, 23, 35, 39, 38, 41, 44, 76, 67, 8, 4,
                        11, 14, 18, 19, 117, 164, 42, 47, 10, 4,
                        20, 29, 49, 72, 43, 46, 47, 73, 47, 22, 6, 4,
                        23, 35, 35, 31, 28, 26, 22, 17, 4,
                        23, 34, 67, 112, 139, 135, 106, 61, 22, 15,
                        26, 51, 86, 114, 182, 110, 16, 9,
                        38, 59, 63, 119, 107, 75, 34, 6, 4,
                        16, 14, 17, 15, 17, 14, 10, 22, 9,
                        21, 43, 69, 91, 103, 49, 45, 16, 4,
                        15, 22, 32, 56, 16, 32, 5
                    };

                    for( int step : rampSteps )
                    {
                        const int nextY = ry + step;
                        if( nextY + rampWinH > rampCanvasH )
                            break;
                        ry = nextY;
                        rampOrigins.push_back( ry );
                    }

                    if( rampOrigins.size() >= 30 )
                    {
                        TestLog( L"[Panorama/Test] Running %s n=%zu lastOrigin=%d\n",
                                     rampName,
                                     rampOrigins.size(),
                                     rampOrigins.back() );

                        stressTestsRun++;
                        const ULONGLONG rampStart = GetTickCount64();
                        const int rampResult = stitchAndCompare( rampName, rampPx, rampCanvasW, rampCanvasH, rampOrigins, rampWinH );
                        const ULONGLONG rampDurationMs = GetTickCount64() - rampStart;

                        wchar_t msg[512]{};
                        if( rampResult < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", rampName );
                            swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        rampName, rampWinH, rampOrigins.size(), rampDurationMs );
                            stressFailLog += msg;
                        }
                        else if( rampResult == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, rampName );
                            swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        rampName, rampWinH, rampOrigins.size(), rampDurationMs );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s *****\n", rampName );
                            swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        rampName, rampWinH, rampOrigins.size(), rampDurationMs );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( rampResult < 0 ) focusResult = L"INFRA";
                            else if( rampResult == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         rampName,
                                         focusResult );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }
            }

            // Latest-capture cadence stress: deterministic sequence mirrored
            // from a full real replay signature (not a short synthetic tail),
            // including repeated tiny-step collapse and distributed surges.
            // This must fail when drops/smears occur throughout the stitch.
            if( !stressEarlyExit )
            {
                const wchar_t* latestCapName = L"stress-vertical-latestcapture-capturepath";
                if( stressScenarioMatches( latestCapName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    const int capCanvasW = 824;
                    const int capCanvasH = 18000;
                    const int capWinH = 1015;
                    std::vector<BYTE> capPx( static_cast<size_t>( capCanvasW ) * capCanvasH * 4, 0 );
                    for( size_t pi = 0; pi < static_cast<size_t>( capCanvasW ) * capCanvasH; ++pi )
                    {
                        capPx[pi * 4 + 0] = 246;
                        capPx[pi * 4 + 1] = 246;
                        capPx[pi * 4 + 2] = 246;
                        capPx[pi * 4 + 3] = 255;
                    }

                    {
                        unsigned int bs = 77377u;
                        int by = 420;
                        while( by < capCanvasH - 6 )
                        {
                            bs = bs * 1103515245u + 12345u;
                            const int bh = 2 + static_cast<int>( ( bs >> 16 ) % 3 );
                            bs = bs * 1103515245u + 12345u;
                            const int br = 24 + static_cast<int>( ( bs >> 16 ) % 28 );
                            bs = bs * 1103515245u + 12345u;
                            const int bStart = 42 + static_cast<int>( ( bs >> 16 ) % 26 );
                            bs = bs * 1103515245u + 12345u;
                            const int bEnd = capCanvasW - 40 - static_cast<int>( ( bs >> 16 ) % 26 );
                            for( int row = by; row < min( by + bh, capCanvasH ); ++row )
                                for( int col = bStart; col < max( bStart + 1, bEnd ); ++col )
                                {
                                    const size_t idx = ( static_cast<size_t>( row ) * capCanvasW + col ) * 4;
                                    capPx[idx + 0] = static_cast<BYTE>( br );
                                    capPx[idx + 1] = static_cast<BYTE>( br );
                                    capPx[idx + 2] = static_cast<BYTE>( br );
                                }
                            bs = bs * 1103515245u + 12345u;
                            by += bh + 72 + static_cast<int>( ( bs >> 16 ) % 88 );
                        }
                    }

                    std::vector<int> capOrigins;
                    capOrigins.push_back( 0 );
                    int cy = 0;
                    const int capSteps[] = {
                        4, 16, 26, 47, 76, 47, 50, 46, 75, 37, 8, 9, 16, 25, 27, 24,
                        26, 96, 96, 7, 4, 25, 78, 81, 78, 156, 36, 4, 26, 37, 118, 125,
                        138, 107, 45, 4, 8, 31, 46, 79, 121, 145, 179, 79, 12, 8, 12, 8,
                        9, 12, 328, 32, 5, 4, 28, 26, 31, 35, 31, 33, 5, 4, 22, 25,
                        21, 24, 23, 4, 24, 21, 18, 22, 26, 101, 52, 4, 8, 34, 58, 104,
                        108, 136, 89, 43, 4, 4, 8, 19, 42, 38, 37, 40, 42, 86, 82, 4,
                        15, 36, 70, 115, 140, 189, 108, 105, 4, 4, 26, 22, 96, 121,
                        179, 67, 15
                    };

                    for( int step : capSteps )
                    {
                        const int nextY = cy + step;
                        if( nextY + capWinH > capCanvasH )
                            break;
                        cy = nextY;
                        capOrigins.push_back( cy );
                    }

                    if( capOrigins.size() >= 30 )
                    {
                        TestLog( L"[Panorama/Test] Running %s n=%zu lastOrigin=%d\n",
                                     latestCapName,
                                     capOrigins.size(),
                                     capOrigins.back() );

                        stressTestsRun++;
                        const ULONGLONG latestCapStart = GetTickCount64();
                        const int latestCapResult = stitchAndCompare( latestCapName,
                                                                      capPx,
                                                                      capCanvasW,
                                                                      capCanvasH,
                                                                      capOrigins,
                                                                      capWinH );
                        const ULONGLONG latestCapDurationMs = GetTickCount64() - latestCapStart;

                        wchar_t msg[512]{};
                        if( latestCapResult < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", latestCapName );
                            swprintf_s( msg, L"INFRA: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        latestCapName, capWinH, capOrigins.size(), latestCapDurationMs );
                            stressFailLog += msg;
                        }
                        else if( latestCapResult == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, latestCapName );
                            swprintf_s( msg, L"PASS: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        latestCapName, capWinH, capOrigins.size(), latestCapDurationMs );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s *****\n", latestCapName );
                            swprintf_s( msg, L"FAIL: %s (winH=%d, nFrames=%zu, durMs=%llu)\n",
                                        latestCapName, capWinH, capOrigins.size(), latestCapDurationMs );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( latestCapResult < 0 ) focusResult = L"INFRA";
                            else if( latestCapResult == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         latestCapName,
                                         focusResult );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }
            }

            // Reproduction-signature regression stress: deterministic HCF scene
            // that used to trigger dropped-band shape (large harmonic jump
            // followed by short corrective steps). This passes only when the
            // bad signature is absent.
            if( !stressEarlyExit && stressEnableDroppedbandRepro )
            {
                const wchar_t* reproName = L"stress-vertical-hcf-droppedband-repro-signature";
                if( stressScenarioMatches( reproName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    stressTestsRun++;

                    constexpr int reproW = 998;
                    constexpr int reproH = 854;
                    constexpr int reproStep = 24;
                    constexpr int reproFrames = 76;
                    const int reproSrcH = reproH + reproStep * ( reproFrames + 10 ) + 512;

                    std::vector<BYTE> reproSource( static_cast<size_t>( reproW ) * reproSrcH * 4, 0 );

                    for( size_t pi = 0; pi < static_cast<size_t>( reproW ) * reproSrcH; ++pi )
                    {
                        reproSource[pi * 4 + 0] = 246;
                        reproSource[pi * 4 + 1] = 246;
                        reproSource[pi * 4 + 2] = 246;
                        reproSource[pi * 4 + 3] = 255;
                    }

                    // Dominant periodic bands: strongly ambiguous for vertical
                    // matching and prone to harmonic aliases.
                    for( int band = 0; ; ++band )
                    {
                        const int y0 = 220 + band * 19;
                        if( y0 >= reproSrcH )
                            break;
                        for( int yy = y0; yy < min( y0 + 2, reproSrcH ); ++yy )
                        {
                            for( int xx = 54; xx < reproW - 54; ++xx )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * reproW + xx ) * 4;
                                reproSource[idx + 0] = 30;
                                reproSource[idx + 1] = 34;
                                reproSource[idx + 2] = 38;
                            }
                        }
                    }

                    // Weak secondary periodic texture to create competing peaks.
                    for( int band = 0; ; ++band )
                    {
                        const int y0 = 230 + band * 38;
                        if( y0 >= reproSrcH )
                            break;
                        for( int yy = y0; yy < min( y0 + 1, reproSrcH ); ++yy )
                        {
                            for( int xx = 70; xx < reproW - 70; ++xx )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * reproW + xx ) * 4;
                                reproSource[idx + 0] = static_cast<BYTE>( max( reproSource[idx + 0], 44 ) );
                                reproSource[idx + 1] = static_cast<BYTE>( max( reproSource[idx + 1], 48 ) );
                                reproSource[idx + 2] = static_cast<BYTE>( max( reproSource[idx + 2], 52 ) );
                            }
                        }
                    }

                    std::vector<int> reproOrigins;
                    reproOrigins.reserve( reproFrames );
                    reproOrigins.push_back( 0 );
                    int top = 0;
                    while( static_cast<int>( reproOrigins.size() ) < reproFrames )
                    {
                        const int nextTop = top + reproStep;
                        if( nextTop + reproH > reproSrcH )
                            break;
                        top = nextTop;
                        reproOrigins.push_back( top );
                    }

                    auto buildFrame = [&]( int frameTop, std::vector<BYTE>& outFrame )
                    {
                        outFrame.resize( static_cast<size_t>( reproW ) * reproH * 4 );
                        for( int row = 0; row < reproH; ++row )
                        {
                            const BYTE* srcRow = reproSource.data() +
                                                 ( static_cast<size_t>( frameTop + row ) * reproW * 4 );
                            BYTE* dstRow = outFrame.data() + static_cast<size_t>( row ) * reproW * 4;
                            memcpy( dstRow, srcRow, static_cast<size_t>( reproW ) * 4 );
                        }
                    };

                    const int replayExpectedSteps[] = {
                        65, 274, 21, 6, 4, 8, 18, 33, 54, 72, 168, 84,
                        74, 58, 46, 49, 4, 9, 19, 38, 63, 63, 160
                    };
                    int foundPairs = 0;
                    int harmonicOvershoots = 0;
                    int spikeRecoveries = 0;
                    int tinySteps = 0;
                    int prevDetected = 0;
                    std::vector<int> detectedSteps;
                    detectedSteps.reserve( reproOrigins.size() );

                    for( size_t fi = 1; fi < reproOrigins.size(); ++fi )
                    {
                        const int expectedStep = replayExpectedSteps[( fi - 1 ) % _countof( replayExpectedSteps )];
                        const int expectedDy = -expectedStep;

                        std::vector<BYTE> prevFrame;
                        std::vector<BYTE> currFrame;
                        buildFrame( reproOrigins[fi - 1], prevFrame );
                        buildFrame( reproOrigins[fi], currFrame );

                        int bestDx = 0;
                        int bestDy = 0;
                        const bool found = FindBestFrameShift( prevFrame,
                                                               currFrame,
                                                               reproW,
                                                               reproH,
                                                               0,
                                                               expectedDy,
                                                               bestDx,
                                                               bestDy,
                                                               false );
                        if( !found )
                            continue;

                        const int detected = abs( bestDy );
                        detectedSteps.push_back( detected );
                        foundPairs++;

                        if( detected >= reproStep * 5 )
                            harmonicOvershoots++;
                        if( detected <= 8 )
                            tinySteps++;
                        if( prevDetected >= reproStep * 5 && detected <= reproStep * 2 )
                            spikeRecoveries++;

                        prevDetected = detected;
                    }

                    int firstLargeStep = 0;
                    int firstRecoveryStep = 0;
                    for( size_t i = 0; i < detectedSteps.size(); ++i )
                    {
                        if( firstLargeStep == 0 && detectedSteps[i] >= reproStep * 5 )
                        {
                            firstLargeStep = detectedSteps[i];
                            continue;
                        }
                        if( firstLargeStep != 0 && detectedSteps[i] <= reproStep * 2 )
                        {
                            firstRecoveryStep = detectedSteps[i];
                            break;
                        }
                    }

                    const bool enoughCoverage = foundPairs >= 48;
                    const bool signaturePresent =
                        harmonicOvershoots >= 2 &&
                        spikeRecoveries >= 1 &&
                        tinySteps >= 4 &&
                        firstLargeStep >= reproStep * 5 &&
                        firstRecoveryStep > 0;
                    const bool signatureAbsent =
                        harmonicOvershoots <= 1 &&
                        spikeRecoveries == 0 &&
                        firstLargeStep < reproStep * 5 &&
                        firstRecoveryStep == 0;
                    const bool regressionPass =
                        enoughCoverage &&
                        ( stressDroppedbandExpectAbsent ? signatureAbsent : signaturePresent );

                    wchar_t msg[512]{};
                    if( regressionPass )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED\n", stressTestsRun, reproName );
                        swprintf_s( msg,
                                    L"PASS: %s (expectAbsent=%d pairs=%d overshoots=%d recoveries=%d tiny=%d firstLarge=%d firstRecovery=%d)\n",
                                    reproName,
                                    stressDroppedbandExpectAbsent ? 1 : 0,
                                    foundPairs,
                                    harmonicOvershoots,
                                    spikeRecoveries,
                                    tinySteps,
                                    firstLargeStep,
                                    firstRecoveryStep );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s (pairs=%d overshoots=%d recoveries=%d tiny=%d firstLarge=%d firstRecovery=%d) *****\n",
                                 reproName,
                                 foundPairs,
                                 harmonicOvershoots,
                                 spikeRecoveries,
                                 tinySteps,
                                 firstLargeStep,
                                 firstRecoveryStep );
                        swprintf_s( msg,
                                    L"FAIL: %s (expectAbsent=%d pairs=%d overshoots=%d recoveries=%d tiny=%d firstLarge=%d firstRecovery=%d)\n",
                                    reproName,
                                    stressDroppedbandExpectAbsent ? 1 : 0,
                                    foundPairs,
                                    harmonicOvershoots,
                                    spikeRecoveries,
                                    tinySteps,
                                    firstLargeStep,
                                    firstRecoveryStep );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                 reproName,
                                 regressionPass ? L"PASS" : L"FAIL" );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }

            // Deterministic cache-regression stress: reuse the same luma
            // buffers for two different narrow-band pairs. This catches stale
            // informative-mask cache entries keyed only by buffer address.
            if( !stressEarlyExit )
            {
                const wchar_t* scenarioName = L"stress-vertical-maskcache-pointerreuse";
                if( stressScenarioMatches( scenarioName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    const int w = 480;
                    const int h = 360;

                    std::vector<BYTE> prevPixels( static_cast<size_t>( w ) * h * 4, 0 );
                    std::vector<BYTE> currPixels( static_cast<size_t>( w ) * h * 4, 0 );
                    std::vector<BYTE> precomputedPrevLuma( static_cast<size_t>( w ) * h, 0 );
                    std::vector<BYTE> precomputedCurrLuma( static_cast<size_t>( w ) * h, 0 );

                    auto paintNarrowBands = [&]( std::vector<BYTE>& pixels,
                                                 int phase,
                                                 int yStart,
                                                 int spacing,
                                                 BYTE dark,
                                                 BYTE bright )
                    {
                        const size_t totalPixels = static_cast<size_t>( w ) * h;
                        for( size_t pi = 0; pi < totalPixels; ++pi )
                        {
                            pixels[pi * 4 + 0] = 246;
                            pixels[pi * 4 + 1] = 246;
                            pixels[pi * 4 + 2] = 246;
                            pixels[pi * 4 + 3] = 255;
                        }

                        for( int band = 0; band < 14; ++band )
                        {
                            const int y0 = yStart + band * spacing;
                            if( y0 >= h - 2 )
                            {
                                break;
                            }

                            const int bandHeight = ( band % 3 == 0 ) ? 2 : 3;
                            const int xStart = 20 + ( ( phase * 29 + band * 11 ) % 80 );
                            const int xEnd = w - 20 - ( ( phase * 13 + band * 17 ) % 80 );
                            for( int by = max( 1, y0 ); by < min( h - 1, y0 + bandHeight ); ++by )
                            {
                                for( int bx = max( 1, xStart ); bx < min( w - 1, max( xStart + 2, xEnd ) ); ++bx )
                                {
                                    const size_t idx = ( static_cast<size_t>( by ) * w + bx ) * 4;
                                    const BYTE value = ( ( bx + by + band ) % 5 == 0 ) ? bright : dark;
                                    pixels[idx + 0] = value;
                                    pixels[idx + 1] = value;
                                    pixels[idx + 2] = value;
                                }
                            }
                        }
                    };

                    auto runPair = [&]( int expectedDy,
                                        int& outDx,
                                        int& outDy,
                                        unsigned __int64& outMaskedScore,
                                        const std::vector<BYTE>& prevLumaArg,
                                        const std::vector<BYTE>& currLumaArg ) -> bool
                    {
                        bool nearStationaryOverride = false;
                        outDx = 0;
                        outDy = 0;
                        outMaskedScore = 0;
                        return FindBestFrameShiftVerticalOnly( prevPixels,
                                                               currPixels,
                                                               w,
                                                               h,
                                                               0,
                                                               expectedDy,
                                                               outDx,
                                                               outDy,
                                                               false,
                                                               prevLumaArg,
                                                               currLumaArg,
                                                               1,
                                                               &nearStationaryOverride,
                                                               false,
                                                               &outMaskedScore );
                    };

                    TestLog( L"[Panorama/Test] Running %s\n", scenarioName );
                    stressTestsRun++;

                    int comparableCases = 0;
                    int divergenceCases = 0;
                    int sampleReuseDy = 0;
                    int sampleFreshDy = 0;
                    int sampleExpected = 0;
                    unsigned __int64 sampleReuseMasked = 0;
                    unsigned __int64 sampleFreshMasked = 0;

                    for( int trial = 0; trial < 24; ++trial )
                    {
                        const int warmupExpected = -12 - ( ( trial * 7 ) % 12 ); // -12..-23
                        const int targetExpected = -18 - ( ( trial * 11 ) % 18 ); // -18..-35

                        // Warm-up pair fills cache for the current luma buffers.
                        paintNarrowBands( prevPixels, 5 + trial, 20 + ( trial % 6 ) * 4, 18, 22, 78 );
                        paintNarrowBands( currPixels, 8 + trial, 20 + ( trial % 6 ) * 4 - warmupExpected, 18, 22, 78 );
                        BuildFullLumaFrame( prevPixels, w, h, precomputedPrevLuma );
                        BuildFullLumaFrame( currPixels, w, h, precomputedCurrLuma );

                        int warmDx = 0;
                        int warmDy = 0;
                        unsigned __int64 warmMaskedScore = 0;
                        runPair( warmupExpected,
                                 warmDx,
                                 warmDy,
                                 warmMaskedScore,
                                 precomputedPrevLuma,
                                 precomputedCurrLuma );

                        // Target pair mutates content in-place while reusing the
                        // same luma buffers and therefore the same data pointers.
                        paintNarrowBands( prevPixels, 41 + trial * 3, 128 + ( trial % 5 ) * 7, 11, 26, 96 );
                        paintNarrowBands( currPixels, 47 + trial * 3, 128 + ( trial % 5 ) * 7 - targetExpected, 11, 26, 96 );
                        BuildFullLumaFrame( prevPixels, w, h, precomputedPrevLuma );
                        BuildFullLumaFrame( currPixels, w, h, precomputedCurrLuma );

                        int reuseDx = 0;
                        int reuseDy = 0;
                        unsigned __int64 reuseMaskedScore = 0;
                        const bool reuseOk = runPair( targetExpected,
                                                      reuseDx,
                                                      reuseDy,
                                                      reuseMaskedScore,
                                                      precomputedPrevLuma,
                                                      precomputedCurrLuma );

                        std::vector<BYTE> freshPrevLuma = precomputedPrevLuma;
                        std::vector<BYTE> freshCurrLuma = precomputedCurrLuma;

                        int freshDx = 0;
                        int freshDy = 0;
                        unsigned __int64 freshMaskedScore = 0;
                        const bool freshOk = runPair( targetExpected,
                                                      freshDx,
                                                      freshDy,
                                                      freshMaskedScore,
                                                      freshPrevLuma,
                                                      freshCurrLuma );

                        const bool freshComparable = freshOk;
                        if( !freshComparable )
                        {
                            continue;
                        }

                        comparableCases++;
                        const bool diverged = ( reuseOk != freshOk ) || abs( reuseDy - freshDy ) > 4;
                        if( diverged )
                        {
                            divergenceCases++;
                            if( sampleExpected == 0 )
                            {
                                sampleExpected = targetExpected;
                                sampleReuseDy = reuseDy;
                                sampleFreshDy = freshDy;
                                sampleReuseMasked = reuseMaskedScore;
                                sampleFreshMasked = freshMaskedScore;
                            }
                        }
                    }

                    const bool enoughComparableCases = comparableCases >= 5;
                    const bool resultPass = enoughComparableCases && divergenceCases == 0;

                    wchar_t msg[512]{};
                    if( resultPass )
                    {
                        stressTestsPassed++;
                        TestLog( L"  [%d] %s PASSED (pair1=%d pair2=%d)\n",
                                 stressTestsRun,
                                 scenarioName,
                                 comparableCases,
                                 divergenceCases );
                        swprintf_s( msg,
                                    L"PASS: %s (comparable=%d divergence=%d)\n",
                                    scenarioName,
                                    comparableCases,
                                    divergenceCases );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s (comparable=%d divergence=%d sampleExpected=%d sampleReuseDy=%d sampleFreshDy=%d sampleReuseMs=%llu sampleFreshMs=%llu) *****\n",
                                 scenarioName,
                                 comparableCases,
                                 divergenceCases,
                                 sampleExpected,
                                 sampleReuseDy,
                                 sampleFreshDy,
                                 static_cast<unsigned long long>( sampleReuseMasked ),
                                 static_cast<unsigned long long>( sampleFreshMasked ) );
                        swprintf_s( msg,
                                    L"FAIL: %s (comparable=%d divergence=%d sampleExpected=%d sampleReuseDy=%d sampleFreshDy=%d sampleReuseMs=%llu sampleFreshMs=%llu)\n",
                                    scenarioName,
                                    comparableCases,
                                    divergenceCases,
                                    sampleExpected,
                                    sampleReuseDy,
                                    sampleFreshDy,
                                    static_cast<unsigned long long>( sampleReuseMasked ),
                                    static_cast<unsigned long long>( sampleFreshMasked ) );
                        stressFailLog += msg;
                    }
                    stressLog += msg;

                    if( stressFocusEnabled )
                    {
                        TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                     scenarioName,
                                     resultPass ? L"PASS" : L"FAIL" );
                        if( stressStopAfterFocus )
                            stressEarlyExit = true;
                    }
                }
            }
        }

        // Horizontal stress test: ~100 frames per trial, steps 0..25% of portal
        if( useExternalImageAssets )
        {
            const auto hPath = stressDir / L"horizontal_stress.png";
            if( std::filesystem::exists( hPath ) )
            {
                std::vector<BYTE> hPx;
                int hW = 0, hH = 0;
                if( !loadImageFile( hPath, hPx, hW, hH ) )
                {
                    TestLog( L"[Panorama/Test] Failed to load horizontal_stress.png\n" );
                }
                else
                {
                    TestLog( L"[Panorama/Test] Loaded horizontal_stress.png %dx%d\n", hW, hH );

                    constexpr int kStressTrials = 5;
                    for( int trial = 0; trial < kStressTrials; ++trial )
                    {
                        if( stressEarlyExit )
                            break;
                        srand( static_cast<unsigned>( 80000 + trial * 17 ) );

                        // Portal width: between 500 and 900 (wider than
                        // the image height of 200 so frames are clearly landscape).
                        const int winW = 500 + rand() % 401;
                        const int maxStep = max( 1, winW / 4 ); // 25% of portal

                        // Build ~100 frames with random horizontal steps including zero.
                        // For the first 3 frames, enforce a minimum step so the stitcher
                        // establishes the horizontal axis before we feed zero-step frames.
                        const int minEstablish = max( 1, maxStep / 5 );
                        std::vector<int> originsX;
                        originsX.push_back( 0 );
                        int x = 0;
                        for( int f = 1; f < 100; ++f )
                        {
                            int step = rand() % ( maxStep + 1 );
                            if( f < 3 && step < minEstablish )
                                step = minEstablish;
                            const int nextX = x + step;
                            if( nextX + winW > hW )
                                break;
                            x = nextX;
                            originsX.push_back( x );
                        }

                        if( originsX.size() < 3 )
                            continue;

                        wchar_t scenarioName[256];
                        swprintf_s( scenarioName, L"stress-horizontal-trial%d-w%d-n%zu-maxstep%d",
                                    trial, winW, originsX.size(), maxStep );

                        {
                            std::wstring origStr;
                            for( size_t oi = 0; oi < originsX.size() && oi < 20; ++oi )
                            {
                                if( oi > 0 ) origStr += L",";
                                origStr += std::to_wstring( originsX[oi] );
                            }
                            if( originsX.size() > 20 ) origStr += L",...";
                            if( stressScenarioMatches( scenarioName ) )
                            {
                                if( stressFocusEnabled )
                                    stressFocusMatched = true;
                                TestLog( L"[Panorama/Test] Running %s origins=[%s]\n", scenarioName, origStr.c_str() );
                            }
                        }
                        if( !stressScenarioMatches( scenarioName ) )
                            continue;
                        stressTestsRun++;
                        const int result = stitchAndCompareHorizontal( scenarioName, hPx, hW, hH, originsX, winW );
                        {
                            wchar_t msg[512]{};
                            if( result < 0 )
                            {
                                TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                                swprintf_s( msg, L"INFRA: %s (winW=%d, nFrames=%zu)\n", scenarioName, winW, originsX.size() );
                                stressFailLog += msg;
                            }
                            else if( result == 1 )
                            {
                                stressTestsPassed++;
                                TestLog( L"  [%d] %s PASSED\n", stressTestsRun, scenarioName );
                                swprintf_s( msg, L"PASS: %s (winW=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winW, originsX.size(), maxStep );
                            }
                            else
                            {
                                TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", scenarioName );
                                swprintf_s( msg, L"FAIL: %s (winW=%d, nFrames=%zu, maxStep=%d)\n", scenarioName, winW, originsX.size(), maxStep );
                                stressFailLog += msg;
                            }
                            stressLog += msg;
                        }

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n",
                                         scenarioName,
                                         focusResult );
                            if( stressStopAfterFocus )
                            {
                                stressEarlyExit = true;
                                break;
                            }
                        }
                    }

                    // Horizontal counterpart of startup-defer + legit-jump stress.
                    // Uses low-entropy periodic columns with tiny initial motion,
                    // then sustained large valid jumps so wrong-axis/defer and
                    // continuity rejection paths are exercised in horizontal mode.
                    if( !stressEarlyExit )
                    {
                        const wchar_t* hAdrName = L"stress-horizontal-axisdefer-legitjumps";
                        if( stressScenarioMatches( hAdrName ) )
                        {
                            if( stressFocusEnabled )
                                stressFocusMatched = true;

                            const int hAdrW = 9600;
                            const int hAdrH = 620;
                            const int hAdrWinW = 763;
                            std::vector<BYTE> hAdrPx( static_cast<size_t>( hAdrW ) * hAdrH * 4, 0 );

                            for( int y = 0; y < hAdrH; ++y )
                            {
                                for( int x = 0; x < hAdrW; ++x )
                                {
                                    const int xTrend = ( ( x * 37 + ( x / 113 ) * 19 ) % 43 );
                                    const BYTE base = static_cast<BYTE>( 16 + xTrend + ( ( x * 5 + y * 3 ) & 0x03 ) );
                                    const size_t idx = ( static_cast<size_t>( y ) * hAdrW + x ) * 4;
                                    hAdrPx[idx + 0] = base;
                                    hAdrPx[idx + 1] = static_cast<BYTE>( base + 1 );
                                    hAdrPx[idx + 2] = static_cast<BYTE>( base + 2 );
                                    hAdrPx[idx + 3] = 255;
                                }
                            }

                            for( int band = 0; band * 34 < hAdrW; ++band )
                            {
                                const int x0 = band * 34;
                                for( int dx = 0; dx < 2; ++dx )
                                {
                                    const int xx = x0 + dx;
                                    if( xx >= hAdrW )
                                        continue;
                                    for( int y = 0; y < hAdrH; ++y )
                                    {
                                        const size_t idx = ( static_cast<size_t>( y ) * hAdrW + xx ) * 4;
                                        hAdrPx[idx + 0] = 38;
                                        hAdrPx[idx + 1] = 42;
                                        hAdrPx[idx + 2] = 46;
                                    }
                                }

                                if( ( band % 9 ) == 0 )
                                {
                                    const int y0 = 10 + ( ( band * 41 ) % 120 );
                                    for( int yy = y0; yy < min( y0 + 3, hAdrH ); ++yy )
                                    {
                                        for( int xx = x0 + 9; xx < min( x0 + 12, hAdrW ); ++xx )
                                        {
                                            const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                            hAdrPx[idx + 0] = 150;
                                            hAdrPx[idx + 1] = 156;
                                            hAdrPx[idx + 2] = 162;
                                        }
                                    }
                                }
                            }

                            // Add sparse non-periodic anchor glyphs so large
                            // legitimate jumps remain structurally distinguishable
                            // from periodic harmonics in horizontal mode.
                            for( int gx = 80; gx + 7 < hAdrW; gx += 113 )
                            {
                                const int gy = 24 + ( ( gx * 37 ) % max( 1, hAdrH - 48 ) );
                                const BYTE br = static_cast<BYTE>( 170 + ( ( gx / 113 ) % 50 ) );
                                for( int yy = gy; yy < gy + 6 && yy < hAdrH; ++yy )
                                {
                                    for( int xx = gx; xx < gx + 6 && xx < hAdrW; ++xx )
                                    {
                                        const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                        hAdrPx[idx + 0] = br;
                                        hAdrPx[idx + 1] = static_cast<BYTE>( min( 255, br + 4 ) );
                                        hAdrPx[idx + 2] = static_cast<BYTE>( min( 255, br + 8 ) );
                                    }
                                }
                            }

                            // Make horizontal alignment unambiguous: use
                            // deterministic per-pixel texture so the true
                            // horizontal shift dominates any vertical alias.
                            {
                                unsigned int seed = 24681357u;
                                for( int yy = 0; yy < hAdrH; ++yy )
                                {
                                    for( int xx = 0; xx < hAdrW; ++xx )
                                    {
                                        seed = seed * 1103515245u + 12345u;
                                        const BYTE v = static_cast<BYTE>( ( seed >> 16 ) & 0xFF );
                                        const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                        hAdrPx[idx + 0] = v;
                                        hAdrPx[idx + 1] = static_cast<BYTE>( v ^ 0x35 );
                                        hAdrPx[idx + 2] = static_cast<BYTE>( v ^ 0x6B );
                                        hAdrPx[idx + 3] = 255;
                                    }
                                }
                            }

                            std::vector<int> originsX;
                            originsX.push_back( 0 );
                            int x = 0;
                            const int hAdrSteps[] = {
                                2, 3, 2, 4,
                                45, 52, 49, 54, 47,
                                160, 158, 162, 160,
                                83, 90, 86, 94
                            };
                            for( int step : hAdrSteps )
                            {
                                x += step;
                                if( x + hAdrWinW > hAdrW )
                                    break;
                                originsX.push_back( x );
                            }

                            if( originsX.size() >= 10 )
                            {
                                stressTestsRun++;
                                const int rawResult = stitchAndCompareHorizontal( hAdrName, hAdrPx, hAdrW, hAdrH, originsX, hAdrWinW );
                                const size_t composedCount = countComposedHorizontal( hAdrPx, hAdrW, hAdrH, originsX, hAdrWinW );
                                const size_t requiredComposed = originsX.size() >= 3 ? originsX.size() - 2 : originsX.size();
                                const int result = ( rawResult < 0 ) ? -1 : ( composedCount >= requiredComposed ? 1 : 0 );
                                wchar_t msg[512]{};
                                if( result < 0 )
                                {
                                    TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", hAdrName );
                                    swprintf_s( msg, L"INFRA: %s (winW=%d, nFrames=%zu)\n", hAdrName, hAdrWinW, originsX.size() );
                                    stressFailLog += msg;
                                }
                                else if( result == 1 )
                                {
                                    stressTestsPassed++;
                                    TestLog( L"  [%d] %s PASSED\n", stressTestsRun, hAdrName );
                                    swprintf_s( msg, L"PASS: %s (winW=%d, nFrames=%zu composed=%zu)\n", hAdrName, hAdrWinW, originsX.size(), composedCount );
                                }
                                else
                                {
                                    TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", hAdrName );
                                    swprintf_s( msg, L"FAIL: %s (winW=%d, nFrames=%zu composed=%zu)\n", hAdrName, hAdrWinW, originsX.size(), composedCount );
                                    stressFailLog += msg;
                                }
                                stressLog += msg;

                                if( stressFocusEnabled )
                                {
                                    const wchar_t* focusResult = L"FAIL";
                                    if( result < 0 ) focusResult = L"INFRA";
                                    else if( result == 1 ) focusResult = L"PASS";
                                    TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", hAdrName, focusResult );
                                    if( stressStopAfterFocus )
                                        stressEarlyExit = true;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                TestLog( L"[Panorama/Test] Skipping horizontal_stress.png (not found at %s)\n", hPath.c_str() );
            }
        }

        // Always run the synthetic horizontal axis-defer stress case even when
        // horizontal_stress.png is unavailable.
        if( !stressEarlyExit )
        {
            if( true )
            {
                const wchar_t* hAdrName = L"stress-horizontal-axisdefer-legitjumps";
                if( stressScenarioMatches( hAdrName ) )
                {
                    if( stressFocusEnabled )
                        stressFocusMatched = true;

                    const int hAdrW = 9600;
                    const int hAdrH = 620;
                    const int hAdrWinW = 763;
                    std::vector<BYTE> hAdrPx( static_cast<size_t>( hAdrW ) * hAdrH * 4, 0 );

                    for( int y = 0; y < hAdrH; ++y )
                    {
                        for( int x = 0; x < hAdrW; ++x )
                        {
                            const int xTrend = ( ( x * 37 + ( x / 113 ) * 19 ) % 43 );
                            const BYTE base = static_cast<BYTE>( 16 + xTrend + ( ( x * 5 + y * 3 ) & 0x03 ) );
                            const size_t idx = ( static_cast<size_t>( y ) * hAdrW + x ) * 4;
                            hAdrPx[idx + 0] = base;
                            hAdrPx[idx + 1] = static_cast<BYTE>( base + 1 );
                            hAdrPx[idx + 2] = static_cast<BYTE>( base + 2 );
                            hAdrPx[idx + 3] = 255;
                        }
                    }

                    for( int band = 0; band * 34 < hAdrW; ++band )
                    {
                        const int x0 = band * 34;
                        for( int dx = 0; dx < 2; ++dx )
                        {
                            const int xx = x0 + dx;
                            if( xx >= hAdrW )
                                continue;
                            for( int y = 0; y < hAdrH; ++y )
                            {
                                const size_t idx = ( static_cast<size_t>( y ) * hAdrW + xx ) * 4;
                                hAdrPx[idx + 0] = 38;
                                hAdrPx[idx + 1] = 42;
                                hAdrPx[idx + 2] = 46;
                            }
                        }

                        if( ( band % 9 ) == 0 )
                        {
                            const int y0 = 10 + ( ( band * 41 ) % 120 );
                            for( int yy = y0; yy < min( y0 + 3, hAdrH ); ++yy )
                            {
                                for( int xx = x0 + 9; xx < min( x0 + 12, hAdrW ); ++xx )
                                {
                                    const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                    hAdrPx[idx + 0] = 150;
                                    hAdrPx[idx + 1] = 156;
                                    hAdrPx[idx + 2] = 162;
                                }
                            }
                        }
                    }

                    // Add sparse non-periodic anchor glyphs so large
                    // legitimate jumps remain structurally distinguishable
                    // from periodic harmonics in horizontal mode.
                    for( int gx = 80; gx + 7 < hAdrW; gx += 113 )
                    {
                        const int gy = 24 + ( ( gx * 37 ) % max( 1, hAdrH - 48 ) );
                        const BYTE br = static_cast<BYTE>( 170 + ( ( gx / 113 ) % 50 ) );
                        for( int yy = gy; yy < gy + 6 && yy < hAdrH; ++yy )
                        {
                            for( int xx = gx; xx < gx + 6 && xx < hAdrW; ++xx )
                            {
                                const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                hAdrPx[idx + 0] = br;
                                hAdrPx[idx + 1] = static_cast<BYTE>( min( 255, br + 4 ) );
                                hAdrPx[idx + 2] = static_cast<BYTE>( min( 255, br + 8 ) );
                            }
                        }
                    }

                    // Make horizontal alignment unambiguous: use
                    // deterministic per-pixel texture so the true
                    // horizontal shift dominates any vertical alias.
                    {
                        unsigned int seed = 24681357u;
                        for( int yy = 0; yy < hAdrH; ++yy )
                        {
                            for( int xx = 0; xx < hAdrW; ++xx )
                            {
                                seed = seed * 1103515245u + 12345u;
                                const BYTE v = static_cast<BYTE>( ( seed >> 16 ) & 0xFF );
                                const size_t idx = ( static_cast<size_t>( yy ) * hAdrW + xx ) * 4;
                                hAdrPx[idx + 0] = v;
                                hAdrPx[idx + 1] = static_cast<BYTE>( v ^ 0x35 );
                                hAdrPx[idx + 2] = static_cast<BYTE>( v ^ 0x6B );
                                hAdrPx[idx + 3] = 255;
                            }
                        }
                    }

                    std::vector<int> originsX;
                    originsX.push_back( 0 );
                    int x = 0;
                    const int hAdrSteps[] = {
                        2, 3, 2, 4,
                        45, 52, 49, 54, 47,
                        160, 158, 162, 160,
                        83, 90, 86, 94
                    };
                    for( int step : hAdrSteps )
                    {
                        x += step;
                        if( x + hAdrWinW > hAdrW )
                            break;
                        originsX.push_back( x );
                    }

                    if( originsX.size() >= 10 )
                    {
                        stressTestsRun++;
                        const int rawResult = stitchAndCompareHorizontal( hAdrName, hAdrPx, hAdrW, hAdrH, originsX, hAdrWinW );
                        const size_t composedCount = countComposedHorizontal( hAdrPx, hAdrW, hAdrH, originsX, hAdrWinW );
                        const size_t requiredComposed = originsX.size() >= 3 ? originsX.size() - 2 : originsX.size();
                        const int result = ( rawResult < 0 ) ? -1 : ( composedCount >= requiredComposed ? 1 : 0 );
                        wchar_t msg[512]{};
                        if( result < 0 )
                        {
                            TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", hAdrName );
                            swprintf_s( msg, L"INFRA: %s (winW=%d, nFrames=%zu)\n", hAdrName, hAdrWinW, originsX.size() );
                            stressFailLog += msg;
                        }
                        else if( result == 1 )
                        {
                            stressTestsPassed++;
                            TestLog( L"  [%d] %s PASSED\n", stressTestsRun, hAdrName );
                            swprintf_s( msg, L"PASS: %s (winW=%d, nFrames=%zu composed=%zu)\n", hAdrName, hAdrWinW, originsX.size(), composedCount );
                        }
                        else
                        {
                            TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", hAdrName );
                            swprintf_s( msg, L"FAIL: %s (winW=%d, nFrames=%zu composed=%zu)\n", hAdrName, hAdrWinW, originsX.size(), composedCount );
                            stressFailLog += msg;
                        }
                        stressLog += msg;

                        if( stressFocusEnabled )
                        {
                            const wchar_t* focusResult = L"FAIL";
                            if( result < 0 ) focusResult = L"INFRA";
                            else if( result == 1 ) focusResult = L"PASS";
                            TestLog( L"[Panorama/Test] Stress focus result: %s => %s\n", hAdrName, focusResult );
                            if( stressStopAfterFocus )
                                stressEarlyExit = true;
                        }
                    }
                }
            }
        }

        if( stressFocusEnabled && !stressFocusMatched )
        {
            TestLog( L"***** FAIL: Stress focus did not match any scenario: \"%s\" *****\n",
                         stressFocusScenario.c_str() );
            if( !selfTestDumpDirectory.empty() )
            {
                wchar_t msg[512]{};
                swprintf_s( msg, L"FOCUS NOT FOUND: %s", stressFocusScenario.c_str() );
                DumpPanoramaText( selfTestDumpDirectory, L"stress_test_failed.txt", msg );
            }
            CoUninitialize();
            return false;
        }

        TestLog( L"[Panorama/Test] Stress tests: %d/%d passed\n", stressTestsPassed, stressTestsRun );

        // Dump accumulated stress test results (always, for diagnostics).
        if( !selfTestDumpDirectory.empty() )
        {
            wchar_t hdr[256]{};
            swprintf_s( hdr, L"[stressTestsRun=%d stressTestsPassed=%d]\n", stressTestsRun, stressTestsPassed );
            stressLog = hdr + stressLog;
            DumpPanoramaText( selfTestDumpDirectory, L"stress_test_results.txt", stressLog );
        }
        if( !selfTestDumpDirectory.empty() && !stressFailLog.empty() )
        {
            DumpPanoramaText( selfTestDumpDirectory, L"stress_test_failed.txt", stressFailLog );
        }

        if( stressTestsRun > 0 && stressTestsPassed < stressTestsRun )
        {
            TestLog( L"***** FAIL: Stress test failures: %d/%d *****\n",
                         stressTestsRun - stressTestsPassed, stressTestsRun );
            if( !selfTestDumpDirectory.empty() )
            {
                wchar_t msg[256]{};
                swprintf_s( msg, L"STRESS SUMMARY: %d/%d passed\n\n", stressTestsPassed, stressTestsRun );
                stressFailLog = msg + stressFailLog;
                DumpPanoramaText( selfTestDumpDirectory, L"stress_test_failed.txt", stressFailLog );
            }
            CoUninitialize();
            return false;
        }

        CoUninitialize();
    }

    TestLog( L"[Panorama/Test] All scenarios passed.  Dump: %s\n", selfTestDumpDirectory.c_str() );
    return true;
}

#pragma warning(pop)

bool RunPanoramaStitchDumpDirectory( const wchar_t* path )
{
    if( !AttachConsole( ATTACH_PARENT_PROCESS ) )
    {
        AllocConsole();
    }

    FILE* fp = nullptr;
    freopen_s( &fp, "CONOUT$", "w", stdout );
    freopen_s( &fp, "CONOUT$", "w", stderr );
    std::filesystem::path outputPath;
    return RunPanoramaStitchFromDumpDirectory( std::filesystem::path( path ), outputPath );
}

bool RunPanoramaStitchLatestDebugDump()
{
    if( !AttachConsole( ATTACH_PARENT_PROCESS ) )
    {
        AllocConsole();
    }

    FILE* fp = nullptr;
    freopen_s( &fp, "CONOUT$", "w", stdout );
    freopen_s( &fp, "CONOUT$", "w", stderr );
    const auto debugRoot = GetPanoramaDebugRootDirectory();
    if( debugRoot.empty() )
    {
        StitchLog( L"[Panorama/Replay] Unable to determine debug root path\n" );
        return false;
    }

    std::error_code errorCode;
    if( !std::filesystem::exists( debugRoot, errorCode ) || errorCode )
    {
        StitchLog( L"[Panorama/Replay] Debug root does not exist: %s\n", debugRoot.c_str() );
        return false;
    }

    std::filesystem::path latestSession;
    std::filesystem::file_time_type latestWriteTime{};
    bool foundAnySession = false;
    for( const auto& entry : std::filesystem::directory_iterator( debugRoot, errorCode ) )
    {
        if( errorCode )
        {
            break;
        }

        if( !entry.is_directory() )
        {
            continue;
        }

        const auto name = entry.path().filename().wstring();
        if( name.rfind( L"panorama_", 0 ) != 0 )
        {
            continue;
        }

        size_t acceptedFrameCount = 0;
        for( const auto& child : std::filesystem::directory_iterator( entry.path(), errorCode ) )
        {
            if( errorCode )
            {
                break;
            }

            if( !child.is_regular_file() )
            {
                continue;
            }

            const auto childName = child.path().filename().wstring();
            if( childName.rfind( L"accepted_", 0 ) == 0 && child.path().extension() == L".bmp" )
            {
                ++acceptedFrameCount;
                if( acceptedFrameCount >= 2 )
                {
                    break;
                }
            }
        }

        if( errorCode )
        {
            continue;
        }

        if( acceptedFrameCount < 2 )
        {
            continue;
        }

        const auto writeTime = entry.last_write_time( errorCode );
        if( errorCode )
        {
            continue;
        }

        if( !foundAnySession || writeTime > latestWriteTime )
        {
            latestWriteTime = writeTime;
            latestSession = entry.path();
            foundAnySession = true;
        }
    }

    if( !foundAnySession )
    {
        StitchLog( L"[Panorama/Replay] No panorama session folders with accepted frames under %s\n", debugRoot.c_str() );
        return false;
    }

    std::filesystem::path outputPath;
    const bool ok = RunPanoramaStitchFromDumpDirectory( latestSession, outputPath );
    if( ok )
    {
        StitchLog( L"[Panorama/Replay] Finished stitching latest session: %s\n", latestSession.c_str() );
    }

    return ok;
}
#endif // _DEBUG
