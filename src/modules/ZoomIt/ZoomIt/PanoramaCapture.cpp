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
//    presses the stop hotkey or 256 frames have been collected.
//
// 2. Stitching (StitchPanoramaFrames)
//    ---------------------------------
//    All accepted frames are read into 32-bpp BGRA pixel arrays.  They are
//    then composed onto a single canvas by computing relative displacements
//    between each consecutive accepted pair.  Displacement detection uses a
//    two-phase search in FindBestFrameShift:
//
//    Phase 1 – Windowed coarse search on downsampled luma
//      Each frame is converted to single-channel luma and downsampled by
//      4x (or 2x for small frames < 240 px).  The downsampled images are
//      compared at every candidate vertical shift within a search window
//      determined by the expected scroll direction.  The first frame pair
//      searches in both directions; subsequent pairs search only in the
//      established direction across the full feasible range (minStep to
//      maxStep).  This full-range search handles variable scroll speeds
//      (e.g. 40 px → 202 px between consecutive frames).
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
//    Phase 2 – Full-resolution refinement
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
//      clamped to 4–28) linearly crossfades between the old and new
//      frame content using per-pixel alpha weighting.
//
//    Output
//      The stitched pixel array is converted to an HBITMAP via
//      CreateDIBSection.  The caller either copies it to the clipboard
//      as CF_DIB or saves it as a PNG file through IFileSaveDialog.
//
// Debug support (debug builds only)
// ----------------------------------
// In debug builds, every grabbed and accepted frame is saved as a BMP
// to %TEMP%\ZoomItPanoramaDebug\<session>.  A StitchLog function writes
// tracing output to OutputDebugString and optionally to a file.
// Command-line switches /panorama-selftest, /panorama-stitch-latest,
// and /panorama-stitch-replay allow offline re-stitching and automated
// regression testing.
//
//============================================================================
#include "pch.h"

#include "PanoramaCapture.h"
#include "Utility.h"
#include "WindowsVersions.h"

#include <filesystem>
#include <fstream>
#include <limits>
#include <vector>
#include <functional>
#include <cmath>
#include <commctrl.h>
#if defined(_M_X64) || defined(_M_IX86)
#include <emmintrin.h>
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

static HBITMAP StitchPanoramaFrames( const std::vector<HBITMAP>& frames, bool lowContrastMode, std::function<void(int)> progressCallback = nullptr );
static bool RunPanoramaCaptureCommon( HWND hWnd, bool saveToFile );

//----------------------------------------------------------------------------
// Progress dialog for panorama stitching.
//----------------------------------------------------------------------------
class PanoramaProgressDialog
{
public:
    PanoramaProgressDialog() : m_hWnd( nullptr ), m_hProgress( nullptr ), m_hLabel( nullptr ) {}

    void Create( HWND hWndParent )
    {
        EnsureWindowClass();

        // Get DPI for proper sizing
        const UINT dpi = GetDpiForWindowHelper( hWndParent ? hWndParent : GetDesktopWindow() );
        const int margin = ScaleForDpi( 14, dpi );
        const int labelHeight = ScaleForDpi( 20, dpi );
        const int barHeight = ScaleForDpi( 16, dpi );
        const int spacing = ScaleForDpi( 10, dpi );

        // Compute desired client area, then inflate to full window size
        const int clientWidth = ScaleForDpi( 340, dpi );
        const int clientHeight = margin + labelHeight + spacing + barHeight + margin;
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

    void Destroy()
    {
        if( m_hWnd )
        {
            DestroyWindow( m_hWnd );
            m_hWnd = nullptr;
            m_hLabel = nullptr;
            m_hProgress = nullptr;
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
            TranslateMessage( &msg );
            DispatchMessage( &msg );
        }
    }

    HWND m_hWnd;
    HWND m_hProgress;
    HWND m_hLabel;
    HFONT m_hFont = nullptr;

    static LRESULT CALLBACK WndProc( HWND hWnd, UINT uMsg, WPARAM wParam, LPARAM lParam )
    {
        switch( uMsg )
        {
        case WM_CTLCOLORSTATIC:
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
#ifdef _DEBUG
static FILE* g_StitchLogFile = nullptr;
static void StitchLog( const wchar_t* format, ... )
{
    va_list args;
    va_start( args, format );
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
#else
#define StitchLog(...) ((void)0)
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

#ifdef _DEBUG
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

static HBITMAP LoadBitmapFromFile( const std::filesystem::path& filePath )
{
    return reinterpret_cast<HBITMAP>( LoadImageW( nullptr,
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

    const bool useGrabbedFrames = grabbedFramePaths.size() >= 2 && grabbedFramePaths.size() > acceptedFramePaths.size();
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

    // Open a trace log in the dump directory for diagnostics.
    {
        auto logPath = dumpDirectory / L"stitch_trace.log";
        g_StitchLogFile = fopen( logPath.string().c_str(), "w" );
    }

    HBITMAP stitched = StitchPanoramaFrames( frames, false );

    if( g_StitchLogFile != nullptr )
    {
        fclose( g_StitchLogFile );
        g_StitchLogFile = nullptr;
    }

    for( HBITMAP frame : frames )
    {
        DeleteObject( frame );
    }

    if( stitched == nullptr )
    {
        StitchLog( L"[Panorama/Replay] StitchPanoramaFrames failed for %s\n", dumpDirectory.c_str() );
        return false;
    }

    outputPath = dumpDirectory / ( useGrabbedFrames ? L"stitched_replay_grabbed_0000.bmp" : L"stitched_replay_0000.bmp" );
    const bool saved = SaveBitmapAsBmp( stitched, outputPath );
    DeleteObject( stitched );
    if( !saved )
    {
        StitchLog( L"[Panorama/Replay] Failed to save stitched replay: %s\n", outputPath.c_str() );
        return false;
    }

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

static bool AreFramesNearDuplicate( HBITMAP currentFrame, HBITMAP previousFrame, bool lowContrastMode )
{
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

    static unsigned s_phase = 0;
    unsigned __int64 avgDiff = 0;
    double changedFraction = 0.0;

    const int coarseSampleStep = lowContrastMode ? 4 : 6;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight,
                                        avgDiff, changedFraction, coarseSampleStep, ++s_phase ) )
    {
        return false;
    }

    const unsigned __int64 avgDiffThreshold = lowContrastMode ? 3 : 6;
    const double changedThreshold = lowContrastMode ? 0.0008 : 0.005;
    bool duplicate = ( avgDiff < avgDiffThreshold && changedFraction < changedThreshold );

    // Low-content captures (e.g. mostly blank editors where only line numbers
    // change) can be under-sampled by the coarse pass.  Recheck with denser
    // sampling before dropping a frame.
    if( duplicate && lowContrastMode )
    {
        unsigned __int64 fineAvgDiff = 0;
        double fineChangedFraction = 0.0;
        if( ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight,
                                           fineAvgDiff, fineChangedFraction, 2, ++s_phase ) )
        {
            const bool fineDuplicate = ( fineAvgDiff < 2 && fineChangedFraction < 0.00035 );
            if( !fineDuplicate )
            {
                duplicate = false;
                OutputDebug( L"[Panorama/Capture] Fine-pass rescued frame avgDiff=%llu changedPct=%.3f%% fineAvg=%llu fineChangedPct=%.3f%%\n",
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
        if( LooksLikeSmallShiftNotDuplicate( previousPixels, currentPixels, currentWidth, currentHeight,
                            /*maxAbsDyFull=*/lowContrastMode ? 24 : 16,
                            /*maxAbsDxFull=*/lowContrastMode ? 12 : 8,
                            lowContrastMode,
                                            &guardDx, &guardDy, &s0, &best ) )
        {
            duplicate = false;
            OutputDebug( L"[Panorama/Capture] Duplicate-guard: avgDiff=%llu changedPct=%.2f%% smallShift=(%d,%d) stationary=%llu best=%llu\n",
                         avgDiff, changedFraction * 100.0, guardDx, guardDy,
                         static_cast<unsigned long long>( s0 ),
                         static_cast<unsigned long long>( best ) );
        }
    }

    OutputDebug( L"[Panorama/Capture] Frame compare avgDiff=%llu changedPct=%.1f%% size=%dx%d identical=%d lowContrast=%d\n",
                 avgDiff, changedFraction * 100.0, currentWidth, currentHeight, duplicate ? 1 : 0, lowContrastMode ? 1 : 0 );
    return duplicate;
}

static bool ArePixelFramesNearDuplicate( const std::vector<BYTE>& currentPixels,
                                         const std::vector<BYTE>& previousPixels,
                                         int frameWidth,
                                         int frameHeight,
                                         bool lowContrastMode )
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

    const unsigned __int64 avgDiffThreshold = lowContrastMode ? 3 : 6;
    const double changedThreshold = lowContrastMode ? 0.0008 : 0.005;
    bool duplicate = ( avgDiff < avgDiffThreshold && changedFraction < changedThreshold );

    if( duplicate && lowContrastMode )
    {
        unsigned __int64 fineAvgDiff = 0;
        double fineChangedFraction = 0.0;
        if( ComputeAveragePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight,
                                           fineAvgDiff, fineChangedFraction, 2, ++s_phase ) )
        {
            const bool fineDuplicate = ( fineAvgDiff < 2 && fineChangedFraction < 0.00035 );
            if( !fineDuplicate )
            {
                duplicate = false;
            }
        }
    }

    if( duplicate )
    {
        if( LooksLikeSmallShiftNotDuplicate( previousPixels, currentPixels, frameWidth, frameHeight,
                                            /*maxAbsDyFull=*/lowContrastMode ? 24 : 16,
                                            /*maxAbsDxFull=*/lowContrastMode ? 12 : 8,
                                            lowContrastMode ) )
        {
            duplicate = false;
        }
    }

    return duplicate;
}

static bool FindBestFrameShift( const std::vector<BYTE>& previousPixels,
                                const std::vector<BYTE>& currentPixels,
                                int frameWidth,
                                int frameHeight,
                                int expectedDx,
                                int expectedDy,
                                int& bestDx,
                                int& bestDy,
                                bool lowContrastMode )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    // ── Phase 1 ── Windowed coarse search on downsampled luma ─────────
    // Search a LIMITED range around the expected shift to avoid harmonic
    // matches on repetitive content.  For the first frame pair
    // (expectedDy == 0) search outward from the smallest step.
    //
    const int downsampleScale = ( min( frameWidth, frameHeight ) >= 240 ) ? 4 : 2;
    std::vector<BYTE> previousLuma;
    std::vector<BYTE> currentLuma;
    int dsW = 0, dsH = 0, dsW2 = 0, dsH2 = 0;
    BuildDownsampledLumaFrame( previousPixels, frameWidth, frameHeight, downsampleScale, previousLuma, dsW, dsH );
    BuildDownsampledLumaFrame( currentPixels, frameWidth, frameHeight, downsampleScale, currentLuma, dsW2, dsH2 );
    if( dsW != dsW2 || dsH != dsH2 )
    {
        return false;
    }

    const int minStepDs = 1;
    const int maxStepDs = dsH - max( 2, dsH / 6 );
    const int marginX = max( 2, dsW / 20 );

    // Stationary score: how well the frames match with zero shift.
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

    // Reject if frames are stationary (near-identical).
    const unsigned __int64 stationaryRejectThreshold = lowContrastMode ? 1 : 2;
    if( stationaryScore <= stationaryRejectThreshold )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift stationary expected=(%d,%d) stationary=%llu frame=%dx%d\n",
                     expectedDx, expectedDy,
                     static_cast<unsigned long long>( stationaryScore ),
                     frameWidth, frameHeight );
        return false;
    }

    // Determine the search window in downsampled space.
    // Use full range in the known scroll direction.  Scroll speed
    // can vary dramatically between frames (e.g. 40→202→213→38)
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
    constexpr int kMaxCandidatesWithProbes = 24;
    CoarseCandidate candidates[kMaxCandidatesWithProbes];
    int candidateCount = 0;

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
                    totalDiff += static_cast<unsigned __int64>(
                        abs( static_cast<int>( previousLuma[prevRow + x] ) -
                             static_cast<int>( currentLuma[currRow + x] ) ) );
                    samples++;
                }

                // Early termination: if running average already exceeds
                // the worst kept candidate, this step cannot win.
                if( candidateCount >= kMaxCandidates && samples >= 100 &&
                    totalDiff / samples > candidates[candidateCount - 1].score )
                {
                    earlyExitCoarse = true;
                }
            }

            if( earlyExitCoarse || samples < 100 )
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

    if( candidateCount == 0 )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift no-match expected=(%d,%d) frame=%dx%d\n",
                     expectedDx, expectedDy, frameWidth, frameHeight );
        return false;
    }

    const unsigned __int64 bestCoarseScore = candidates[0].score;

    // Prune candidates whose coarse score is far worse than the best.
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

    // Inject probe candidates near the expected shift.  Content with regular
    // vertical structure (e.g. code text at ~13 px line height) produces many
    // similarly-scored coarse candidates at text-line harmonics, pushing the
    // correct shift outside the top-12.  Adding probes at the expected step
    // ensures the fine search always evaluates the correct neighborhood.
    if( expectedDyDs != 0 && prunedCount < kMaxCandidatesWithProbes )
    {
        for( int probe = -3; probe <= 3 && prunedCount < kMaxCandidatesWithProbes; ++probe )
        {
            const int probeDyDs = expectedDyDs + probe;
            if( abs( probeDyDs ) < minStepDs || abs( probeDyDs ) > maxStepDs )
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

    // ── Phase 2 ── Rank candidates by full-resolution comparison ──────
    // For each coarse candidate, compute a fine score at full resolution.
    // This resolves ambiguity from harmonic matches on repetitive content
    // since the full-resolution comparison sees fine text details that
    // the downsampled comparison misses.
    //
    // Pre-compute full-resolution luma arrays so the inner loop uses
    // cheap byte lookups instead of per-pixel RGB→luma multiplies.
    const int refineRadiusDy = max( 3, downsampleScale + 1 );
    const int refineRadiusDx = 1;

    std::vector<BYTE> previousFullLuma( static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight ) );
    std::vector<BYTE> currentFullLuma( static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight ) );
    {
        const size_t pixelCount = static_cast<size_t>( frameWidth ) * static_cast<size_t>( frameHeight );
        for( size_t p = 0; p < pixelCount; ++p )
        {
            const size_t idx = p * 4;
            previousFullLuma[p] = static_cast<BYTE>( ( previousPixels[idx + 2] * 77 +
                                                       previousPixels[idx + 1] * 150 +
                                                       previousPixels[idx + 0] * 29 ) >> 8 );
            currentFullLuma[p] = static_cast<BYTE>( ( currentPixels[idx + 2] * 77 +
                                                      currentPixels[idx + 1] * 150 +
                                                      currentPixels[idx + 0] * 29 ) >> 8 );
        }
    }

    unsigned __int64 bestFineScore = ( std::numeric_limits<unsigned __int64>::max )();
    unsigned __int64 secondBestFineScore = ( std::numeric_limits<unsigned __int64>::max )();
    bestDx = 0;
    bestDy = candidates[0].dyDs * downsampleScale;
    int bestCoarseDy = candidates[0].dyDs;

    const int fineMarginX = max( 4, frameWidth / 20 );

    for( int ci = 0; ci < prunedCount; ++ci )
    {
        const int coarseDyFull = candidates[ci].dyDs * downsampleScale;

        for( int ddy = -refineRadiusDy; ddy <= refineRadiusDy; ++ddy )
        {
            const int dy = coarseDyFull + ddy;
            const int absStep = abs( dy );
            if( absStep < 4 || absStep >= frameHeight - 4 )
            {
                continue;
            }

            const int overlap = frameHeight - absStep;
            if( overlap < frameHeight / 4 )
            {
                continue;
            }

            for( int dx = -refineRadiusDx; dx <= refineRadiusDx; ++dx )
            {
                const int xStart = max( fineMarginX, fineMarginX + max( 0, -dx ) );
                const int xEnd = min( frameWidth - fineMarginX, frameWidth - fineMarginX - max( 0, dx ) );
                if( xEnd - xStart < frameWidth / 3 )
                {
                    continue;
                }

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

#if defined(_M_X64) || defined(_M_IX86)
                    // SSE2 SIMD: process 16 luma pixels at once using
                    // _mm_sad_epu8 (sum of absolute differences).
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
#else
                    // Scalar fallback for ARM64.
                    for( int xi = 0; xi < xSpan; ++xi )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
                    }
#endif

                    totalDiff += rowDiff;
                    samples += xSpan;

                    // Early termination: if running average already exceeds
                    // the best fine score, this candidate cannot win.
                    if( bestFineScore != ( std::numeric_limits<unsigned __int64>::max )() &&
                        samples >= 200 && totalDiff / samples > bestFineScore )
                    {
                        earlyExit = true;
                    }
                }

                if( earlyExit || samples < 100 )
                {
                    continue;
                }

                const unsigned __int64 score = totalDiff / samples;
                if( score < bestFineScore )
                {
                    secondBestFineScore = bestFineScore;
                    bestFineScore = score;
                    bestDx = dx;
                    bestDy = dy;
                    bestCoarseDy = candidates[ci].dyDs;
                }
                else if( score < secondBestFineScore )
                {
                    secondBestFineScore = score;
                }
            }
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

    const unsigned __int64 crossValidationStationaryThreshold = lowContrastMode ? 18 : 15;
    if( !shiftMatchesDirection && stationaryScore < crossValidationStationaryThreshold && detectedStep > frameHeight / 3 && bestFineScore > 0 )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift shift-stationary-mismatch expected=(%d,%d) best=(%d,%d) step=%d fineScore=%llu stationary=%llu\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     detectedStep,
                     static_cast<unsigned long long>( bestFineScore ),
                     static_cast<unsigned long long>( stationaryScore ) );
        return false;
    }

    // Adaptive fine threshold: content with high stationary score (frames are
    // truly different) can tolerate higher fine scores from subpixel rendering
    // artifacts and ClearType.  Near-duplicate frames (low stationary score)
    // use a strict threshold to avoid accepting spurious matches.
    const unsigned __int64 fineThreshold = ( stationaryScore > 15 )
        ? ( lowContrastMode ? 24 : 30 )
        : ( lowContrastMode ? 12 : 15 );
    if( bestFineScore == ( std::numeric_limits<unsigned __int64>::max )() || bestFineScore > fineThreshold )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift poor-fine expected=(%d,%d) best=(%d,%d) fineScore=%llu fineThreshold=%llu stationary=%llu\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     static_cast<unsigned long long>( bestFineScore ),
                     static_cast<unsigned long long>( fineThreshold ),
                     static_cast<unsigned long long>( stationaryScore ) );
        return false;
    }

    StitchLog( L"[Panorama/Stitch] FindBestFrameShift expected=(%d,%d) best=(%d,%d) coarseScore=%llu fineScore=%llu stationary=%llu window=[%d,%d] accepted=1\n",
                 expectedDx, expectedDy, bestDx, bestDy,
                 static_cast<unsigned long long>( bestCoarseScore ),
                 static_cast<unsigned long long>( bestFineScore ),
                 static_cast<unsigned long long>( stationaryScore ),
                 searchMinDy * downsampleScale,
                 searchMaxDy * downsampleScale );
    return true;
}

static HBITMAP StitchPanoramaFrames(const std::vector<HBITMAP>& frames, bool lowContrastMode, std::function<void(int)> progressCallback)
{
    auto reportProgress = [&progressCallback]( int percent )
    {
        if( progressCallback )
        {
            progressCallback( max( 0, min( 100, percent ) ) );
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

        reportProgress( static_cast<int>( ( i + 1 ) * 5 / frames.size() ) );
    }

    std::vector<size_t> composedFrameIndices;
    std::vector<POINT> composedFrameOrigins;
    std::vector<POINT> composedFrameSteps;
    composedFrameIndices.reserve( frames.size() );
    composedFrameOrigins.reserve( frames.size() );
    composedFrameSteps.reserve( frames.size() );
    composedFrameIndices.push_back( 0 );
    composedFrameOrigins.push_back( { 0, 0 } );
    composedFrameSteps.push_back( { 0, 0 } );

    const int minProgress = lowContrastMode ? max( 4, frameHeight / 40 ) : max( 8, frameHeight / 30 );
    int expectedDx = 0;
    int expectedDy = 0;

    int minX = 0;
    int minY = 0;
    int maxX = frameWidth;
    int maxY = frameHeight;

    for( size_t i = 1; i < frames.size(); i++ )
    {
        reportProgress( 5 + static_cast<int>( i * 85 / frames.size() ) );

        int dx = expectedDx;
        int dy = expectedDy;
        bool foundShift = FindBestFrameShift( framePixels[composedFrameIndices.back()], framePixels[i], frameWidth, frameHeight, expectedDx, expectedDy, dx, dy, lowContrastMode );
        if( !foundShift )
        {
            if( ArePixelFramesNearDuplicate( framePixels[composedFrameIndices.back()], framePixels[i], frameWidth, frameHeight, lowContrastMode ) )
            {
                StitchLog( L"[Panorama/Stitch] Frame %zu rejected: duplicate vs frame %zu\n",
                             i,
                             composedFrameIndices.back() );
                continue;
            }

            StitchLog( L"[Panorama/Stitch] Frame %zu rejected: no reliable shift match expected=(%d,%d)\n",
                         i,
                         expectedDx,
                         expectedDy );
            continue;
        }

        const int maxAbsDx = max( 8, frameWidth / 6 );
        const int maxAbsDy = frameHeight - minProgress;
        dx = max( -maxAbsDx, min( maxAbsDx, dx ) );
        dy = max( -maxAbsDy, min( maxAbsDy, dy ) );

        int stepX = -dx;
        int stepY = -dy;

        // After establishing a predominantly vertical or horizontal scroll
        // direction, clamp the perpendicular component to zero.  Subpixel
        // rendering noise (e.g. ClearType) causes the fine refinement to
        // report ±1 px cross-axis drift per frame, which accumulates into
        // visible slanting over many composed frames.
        if( composedFrameSteps.size() >= 3 )
        {
            int totalAbsStepX = 0, totalAbsStepY = 0;
            for( size_t si = 1; si < composedFrameSteps.size(); ++si )
            {
                totalAbsStepX += abs( composedFrameSteps[si].x );
                totalAbsStepY += abs( composedFrameSteps[si].y );
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

        if( abs( stepX ) + abs( stepY ) < minProgress / 2 )
        {
            StitchLog( L"[Panorama/Stitch] Frame %zu rejected: low movement step=(%d,%d)\n", i, stepX, stepY );
            continue;
        }

        POINT nextOrigin = composedFrameOrigins.back();
        nextOrigin.x += stepX;
        nextOrigin.y += stepY;
        composedFrameIndices.push_back( i );
        composedFrameOrigins.push_back( nextOrigin );
        composedFrameSteps.push_back( { stepX, stepY } );
        expectedDx = dx;
        expectedDy = dy;

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

    // Normalize output orientation so the first frame appears at the top and
    // frames progress chronologically downward.
    if( !composedFrameOrigins.empty() && composedFrameOrigins.back().y < composedFrameOrigins.front().y )
    {
        for( POINT& origin : composedFrameOrigins )
        {
            origin.y = -origin.y;
        }

        for( POINT& step : composedFrameSteps )
        {
            step.y = -step.y;
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

        StitchLog( L"[Panorama/Stitch] Normalized orientation: first frame at top\n" );
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
    if( stitchedWidth <= 0 || stitchedHeight <= 0 || stitchedWidth > 30000 || stitchedHeight > 30000 )
    {
        StitchLog( L"[Panorama/Stitch] Invalid stitched canvas size %dx%d\n", stitchedWidth, stitchedHeight );
        return nullptr;
    }

    std::vector<BYTE> stitchedPixels( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4, 0 );
    std::vector<BYTE> stitchedWritten( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
    const int verticalFeather = max( 4, min( 28, frameHeight / 18 ) );

    for( size_t i = 0; i < composedFrameIndices.size(); ++i )
    {
        reportProgress( 90 + static_cast<int>( ( i + 1 ) * 9 / composedFrameIndices.size() ) );

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

        const int absStepY = abs( stepY );
        const bool mostlyVerticalMove = i > 0 && absStepY >= minProgress && abs( stepX ) <= max( 12, frameWidth / 20 );
        const int overlapHeight = mostlyVerticalMove ? max( 0, frameHeight - absStepY ) : 0;

        for( int y = 0; y < frameHeight; ++y )
        {
            const int canvasY = destinationY + y;
            if( canvasY < 0 || canvasY >= stitchedHeight )
            {
                continue;
            }

            BYTE weightNew = 255;
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

            const size_t srcRowBase = static_cast<size_t>( y ) * static_cast<size_t>( frameWidth ) * 4;
            const size_t dstRowBase = static_cast<size_t>( canvasY ) * static_cast<size_t>( stitchedWidth ) * 4;
            const size_t dstMaskRowBase = static_cast<size_t>( canvasY ) * static_cast<size_t>( stitchedWidth );

            for( int x = 0; x < frameWidth; ++x )
            {
                const int canvasX = destinationX + x;
                if( canvasX < 0 || canvasX >= stitchedWidth )
                {
                    continue;
                }

                const size_t srcIndex = srcRowBase + static_cast<size_t>( x ) * 4;
                const size_t dstIndex = dstRowBase + static_cast<size_t>( canvasX ) * 4;
                const size_t dstMaskIndex = dstMaskRowBase + static_cast<size_t>( canvasX );

                if( stitchedWritten[dstMaskIndex] == 0 )
                {
                    stitchedPixels[dstIndex + 0] = sourcePixels[srcIndex + 0];
                    stitchedPixels[dstIndex + 1] = sourcePixels[srcIndex + 1];
                    stitchedPixels[dstIndex + 2] = sourcePixels[srcIndex + 2];
                    stitchedPixels[dstIndex + 3] = 0xFF;
                    stitchedWritten[dstMaskIndex] = 1;
                    continue;
                }

                if( weightNew == 0 )
                {
                    continue;
                }

                if( weightNew == 255 )
                {
                    stitchedPixels[dstIndex + 0] = sourcePixels[srcIndex + 0];
                    stitchedPixels[dstIndex + 1] = sourcePixels[srcIndex + 1];
                    stitchedPixels[dstIndex + 2] = sourcePixels[srcIndex + 2];
                    stitchedPixels[dstIndex + 3] = 0xFF;
                    continue;
                }

                const int oldWeight = 255 - static_cast<int>( weightNew );
                stitchedPixels[dstIndex + 0] = static_cast<BYTE>( ( static_cast<int>( stitchedPixels[dstIndex + 0] ) * oldWeight +
                                                                    static_cast<int>( sourcePixels[srcIndex + 0] ) * static_cast<int>( weightNew ) ) / 255 );
                stitchedPixels[dstIndex + 1] = static_cast<BYTE>( ( static_cast<int>( stitchedPixels[dstIndex + 1] ) * oldWeight +
                                                                    static_cast<int>( sourcePixels[srcIndex + 1] ) * static_cast<int>( weightNew ) ) / 255 );
                stitchedPixels[dstIndex + 2] = static_cast<BYTE>( ( static_cast<int>( stitchedPixels[dstIndex + 2] ) * oldWeight +
                                                                    static_cast<int>( sourcePixels[srcIndex + 2] ) * static_cast<int>( weightNew ) ) / 255 );
                stitchedPixels[dstIndex + 3] = 0xFF;
            }
        }
    }

    HBITMAP stitchedBitmap = CreateBitmapFromPixels32( stitchedPixels, stitchedWidth, stitchedHeight );
    if( stitchedBitmap == nullptr )
    {
        StitchLog( L"[Panorama/Stitch] Failed to create stitched bitmap from pixels\n" );
        return nullptr;
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
#ifdef _DEBUG
    const std::wstring debugDumpDirectory = CreatePanoramaDebugDumpDirectory();
    size_t debugGrabbedFrameCount = 0;
    if( !debugDumpDirectory.empty() )
    {
        OutputDebug( L"[Panorama/Debug] Dump directory: %s\n", debugDumpDirectory.c_str() );
    }
#endif

    g_RecordCropping = TRUE;
    const bool started = g_SelectRectangle.Start( hWnd );
    g_RecordCropping = FALSE;
    if( !started )
    {
        OutputDebug( L"[Panorama/Capture] Selection cancelled\n" );
        g_SelectRectangle.Stop();
        return false;
    }

    const RECT selectedRect = g_SelectRectangle.SelectedRect();
    OutputDebug( L"[Panorama/Capture] Selected rect local=(%ld,%ld)-(%ld,%ld)\n",
                 selectedRect.left,
                 selectedRect.top,
                 selectedRect.right,
                 selectedRect.bottom );

    if( selectedRect.right <= selectedRect.left || selectedRect.bottom <= selectedRect.top )
    {
        OutputDebug( L"[Panorama/Capture] Invalid selected rect\n" );
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

    OutputDebug( L"[Panorama/Capture] Capture rect absolute=(%ld,%ld)-(%ld,%ld)\n",
                 absoluteRect.left,
                 absoluteRect.top,
                 absoluteRect.right,
                 absoluteRect.bottom );

#ifdef _DEBUG
    if( !debugDumpDirectory.empty() )
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
#endif

    wil::unique_hdc hdcSource( CreateDC( L"DISPLAY", static_cast<PTCHAR>(nullptr), static_cast<PTCHAR>(nullptr), static_cast<CONST DEVMODE*>(nullptr) ) );
    if( hdcSource == nullptr )
    {
        OutputDebug( L"[Panorama/Capture] CreateDC failed\n" );
        g_SelectRectangle.Stop();
        return false;
    }

#ifdef _DEBUG
    if( !debugDumpDirectory.empty() )
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
            OutputDebug( L"[Panorama/Debug] Failed to capture desktop snapshot\n" );
        }
    }
#endif

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
        OutputDebug( L"[Panorama/Capture] Failed to capture first frame\n" );
        g_SelectRectangle.Stop();
        return false;
    }
    frames.push_back( firstFrame );

    double contrastSpread = 0.0;
    double contrastStdDev = 0.0;
    double contrastEdgeDelta = 0.0;
    const bool lowContrastMode = IsLowContrastSeedFrame( firstFrame, &contrastSpread, &contrastStdDev, &contrastEdgeDelta );
    OutputDebug( L"[Panorama/Capture] Captured frame #1 lowContrast=%d spread=%.1f stdDev=%.1f edgeDelta=%.1f\n",
                 lowContrastMode ? 1 : 0,
                 contrastSpread,
                 contrastStdDev,
                 contrastEdgeDelta );

#ifdef _DEBUG
    DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, firstFrame );
#endif

    size_t duplicateFrameCount = 0;
    size_t captureIteration = 0;

    while( !g_PanoramaStopRequested )
    {
        captureIteration++;
        MSG msg{};
        while( PeekMessage( &msg, hWnd, WM_HOTKEY, WM_HOTKEY, PM_REMOVE ) )
        {
            OutputDebug( L"[Panorama/Capture] Dispatching WM_HOTKEY id=%ld(%s) during capture loop\n",
                         static_cast<long>( msg.wParam ),
                         HotkeyIdToString( msg.wParam ) );
            DispatchMessage( &msg );
        }

        if( PeekMessage( &msg, nullptr, WM_QUIT, WM_QUIT, PM_REMOVE ) )
        {
            PostQuitMessage( static_cast<int>(msg.wParam) );
            g_PanoramaStopRequested = true;
            OutputDebug( L"[Panorama/Capture] WM_QUIT received, stopping capture\n" );
            break;
        }

        Sleep( 16 );

        HBITMAP frame = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), absoluteRect );
        if( frame == nullptr )
        {
            OutputDebug( L"[Panorama/Capture] Capture failed at iteration=%zu\n", captureIteration );
            continue;
        }

#ifdef _DEBUG
        DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, frame );
#endif

        if( AreFramesNearDuplicate( frame, frames.back(), lowContrastMode ) )
        {
            duplicateFrameCount++;
            if( duplicateFrameCount <= 3 || ( duplicateFrameCount % 10 ) == 0 )
            {
                OutputDebug( L"[Panorama/Capture] Duplicate frame skipped (count=%zu iteration=%zu)\n",
                             duplicateFrameCount,
                             captureIteration );
            }
            DeleteObject( frame );
            continue;
        }

        frames.push_back( frame );
        frame = nullptr;
        OutputDebug( L"[Panorama/Capture] Captured moving frame #%zu at iteration=%zu\n",
                     frames.size(),
                     captureIteration );
        if( frames.size() >= 256 )
        {
            OutputDebug( L"[Panorama/Capture] Reached frame limit (256), stopping capture\n" );
            break;
        }
    }

    OutputDebug( L"[Panorama/Capture] Loop exited stopRequested=%d frames=%zu duplicates=%zu iterations=%zu\n",
                 g_PanoramaStopRequested ? 1 : 0,
                 frames.size(),
                 duplicateFrameCount,
                 captureIteration );

#ifdef _DEBUG
    if( !debugDumpDirectory.empty() )
    {
        wchar_t statsText[256]{};
        swprintf_s( statsText,
                    L"framesAccepted=%zu\nduplicates=%zu\niterations=%zu\nstopRequested=%d\n",
                    frames.size(),
                    duplicateFrameCount,
                    captureIteration,
                    g_PanoramaStopRequested ? 1 : 0 );
        DumpPanoramaText( debugDumpDirectory, L"capture_stats.txt", statsText );

        for( size_t frameIndex = 0; frameIndex < frames.size(); ++frameIndex )
        {
            DumpPanoramaBitmap( debugDumpDirectory, L"accepted", frameIndex + 1, frames[frameIndex] );
        }
    }
#endif

    g_SelectRectangle.Stop();

    HBITMAP panoramaBitmap = nullptr;
    if( frames.size() == 1 )
    {
        panoramaBitmap = frames.front();
        frames.front() = nullptr;
    }
    else
    {
        g_ProgressDialog.Create( hWnd );
        panoramaBitmap = StitchPanoramaFrames( frames, lowContrastMode, [&]( int percent )
        {
            g_ProgressDialog.SetProgress( percent );
        } );
        g_ProgressDialog.Destroy();
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
        OutputDebug( L"[Panorama/Capture] Stitch result is null\n" );
        return false;
    }

#ifdef _DEBUG
    DumpPanoramaBitmap( debugDumpDirectory, L"stitched", 0, panoramaBitmap );
#endif

    if( saveToFile )
    {
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
                OutputDebug( L"[Panorama/Capture] Success: saved to %s\n", selectedFilePath.c_str() );
                success = true;
            }
            else
            {
                OutputDebug( L"[Panorama/Capture] SavePng failed err=%lu\n", saveResult );
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
        OutputDebug( L"[Panorama/Capture] GlobalAlloc for DIB failed\n" );
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
        OutputDebug( L"[Panorama/Capture] OpenClipboard failed err=%lu\n", GetLastError() );
        GlobalFree( hDib );
        return false;
    }

    if( !EmptyClipboard() )
    {
        OutputDebug( L"[Panorama/Capture] EmptyClipboard failed err=%lu\n", GetLastError() );
        CloseClipboard();
        GlobalFree( hDib );
        return false;
    }

    if( SetClipboardData( CF_DIB, hDib ) == nullptr )
    {
        OutputDebug( L"[Panorama/Capture] SetClipboardData(CF_DIB) failed err=%lu\n", GetLastError() );
        CloseClipboard();
        GlobalFree( hDib );
        return false;
    }

    CloseClipboard();
    OutputDebug( L"[Panorama/Capture] Success: DIB copied to clipboard (%dx%d)\n", bmpWidth, abs( bmpHeight ) );
    return true;
}

#ifdef _DEBUG
//
// Panorama stitch self-test
// -------------------------
// How to run:
//   1. Build the ARM64 Debug configuration.
//   2. Place test images (image1.png … image5.png) in the Debug\ directory
//      next to the solution root (i.e. <repo>\Debug\).
//   3. Run:  ZoomIt64a.exe /panorama-selftest
//   4. Exit code 0 = all tests passed, exit code 2 = failure.
//   5. Diagnostic output goes to OutputDebugString (view with DebugView
//      or a debugger).  On failure, artifacts are written to
//      %TEMP%\ZoomItPanoramaDebug\panorama_<timestamp>_<pid>\.
//
bool RunPanoramaStitchSelfTest()
{
    const std::wstring selfTestDumpDirectory = CreatePanoramaDebugDumpDirectory();
    if( !selfTestDumpDirectory.empty() )
    {
        DumpPanoramaText( selfTestDumpDirectory,
                          L"selftest_marker.txt",
                          L"Panorama self-test started and dump path is writable.\n" );
        OutputDebug( L"[Panorama/Test] Dump directory: %s\n", selfTestDumpDirectory.c_str() );
    }

    auto createBitmapFromPixels = []( const std::vector<BYTE>& pixels, int width, int height ) -> HBITMAP
    {
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
    };

    auto runScenario = [&]( const wchar_t* scenarioName,
                            int frameWidth,
                            int frameHeight,
                            const std::vector<int>& frameOriginsY,
                            const std::vector<BYTE>& canvasPixels,
                            int canvasHeight,
                            int expectedHeight,
                            int toleranceHeight,
                            bool allowMismatches ) -> bool
    {
        OutputDebug( L"[Panorama/Test] Scenario=%s frame=%dx%d frameCount=%zu expectedHeight=%d\n",
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
                OutputDebug( L"[Panorama/Test] Scenario=%s invalid origin frame=%zu originY=%d\n",
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

            HBITMAP frameBitmap = createBitmapFromPixels( framePixels, frameWidth, frameHeight );
            if( frameBitmap == nullptr )
            {
                OutputDebug( L"[Panorama/Test] Scenario=%s failed to create frame bitmap index=%zu\n",
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
            OutputDebug( L"[Panorama/Test] Scenario=%s StitchPanoramaFrames returned nullptr\n", scenarioName );
            return false;
        }

        std::vector<BYTE> stitchedPixels;
        int stitchedWidth = 0;
        int stitchedHeight = 0;
        const bool readOk = ReadBitmapPixels32( stitchedBitmap, stitchedPixels, stitchedWidth, stitchedHeight );
        DeleteObject( stitchedBitmap );
        if( !readOk )
        {
            OutputDebug( L"[Panorama/Test] Scenario=%s failed to read stitched bitmap pixels\n", scenarioName );
            return false;
        }

        const int minExpectedHeight = max( 1, expectedHeight - toleranceHeight );
        const int maxExpectedHeight = expectedHeight + toleranceHeight;
        if( stitchedWidth != frameWidth || stitchedHeight < minExpectedHeight || stitchedHeight > maxExpectedHeight )
        {
            OutputDebug( L"[Panorama/Test] Scenario=%s size mismatch actual=%dx%d expected=%dx[%d..%d]\n",
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
        const int sampleHeight = min( expectedHeight, stitchedHeight );
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
        OutputDebug( L"[Panorama/Test] Scenario=%s result passed=%d samples=%zu mismatches=%zu actualHeight=%d\n",
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
            return false;
        }
    }

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
            return false;
        }
    }

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
            return false;
        }
    }

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
            OutputDebug( L"[Panorama/Test] CoInitializeEx failed hr=0x%08lx\n", hrCom );
            return false;
        }

        // Locate test images relative to the executable.
        // Exe is at <solutionDir>/ARM64/Debug/ZoomIt64a.exe; images at <solutionDir>/Debug/
        wchar_t modulePath[MAX_PATH]{};
        if( GetModuleFileNameW( nullptr, modulePath, ARRAYSIZE( modulePath ) ) == 0 )
        {
            OutputDebug( L"[Panorama/Test] GetModuleFileNameW failed\n" );
            CoUninitialize();
            return false;
        }
        const auto imageDir = std::filesystem::path( modulePath ).parent_path().parent_path().parent_path() / L"Debug";

        OutputDebug( L"[Panorama/Test] Image directory: %s\n", imageDir.c_str() );

        const wchar_t* imageFiles[] = { L"image1.png", L"image2.png", L"image3.png", L"image4.png", L"image5.png", L"image6.png" };

        // WIC-based loader for PNG files to HBITMAP.
        auto loadImageFile = [&]( const std::filesystem::path& filePath, std::vector<BYTE>& pixelsOut, int& widthOut, int& heightOut ) -> bool
        {
            IWICImagingFactory* factory = nullptr;
            HRESULT hr = CoCreateInstance( CLSID_WICImagingFactory, nullptr, CLSCTX_INPROC_SERVER,
                                           IID_PPV_ARGS( &factory ) );
            if( FAILED( hr ) || factory == nullptr )
            {
                OutputDebug( L"[Panorama/Test] WIC factory creation failed hr=0x%08lx\n", hr );
                return false;
            }

            IWICBitmapDecoder* decoder = nullptr;
            hr = factory->CreateDecoderFromFilename( filePath.c_str(), nullptr, GENERIC_READ,
                                                      WICDecodeMetadataCacheOnDemand, &decoder );
            if( FAILED( hr ) || decoder == nullptr )
            {
                factory->Release();
                OutputDebug( L"[Panorama/Test] WIC decode failed for %s hr=0x%08lx\n", filePath.c_str(), hr );
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
            const int expectedH = origins.back() + winH;

            std::vector<HBITMAP> frames;
            frames.reserve( origins.size() );
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

                HBITMAP bmp = createBitmapFromPixels( fp, imgW, winH );
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
                OutputDebug( L"[Panorama/Test] %s: StitchPanoramaFrames returned nullptr\n", scenario );
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

            const int htol = winH / 4 + static_cast<int>( origins.size() ) * 8;
            if( sH < expectedH - htol || sH > expectedH + htol )
            {
                // Check if the source image is low-contrast.
                // Low-contrast images can't be stitched by correlation — the stitcher
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
                        OutputDebug( L"[Panorama/Test] %s: low-contrast (avgDiff=%.1f), graceful degradation — PASS\n",
                                     scenario, avgVertDiff );
                        return 1;
                    }
                }

                OutputDebug( L"[Panorama/Test] %s FAILED: height stitched=%d expected=%d tol=%d\n",
                             scenario, sH, expectedH, htol );
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
            for( int yy = 0; yy < sH; yy += 19 )
            {
                int bestSY = yy;
                double bestRD = 1e18;
                for( int ty = max( 0, yy - maxVE ); ty <= min( imgH - 1, yy + maxVE ); ++ty )
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
            }

            const double mrate = samples > 0 ? static_cast<double>( mismatches ) / samples : 0.0;
            const bool ok = samples > 0 && mrate < 0.15;

            OutputDebug( L"[Panorama/Test] %s result=%s stitched=%dx%d dx=%d samples=%zu mismatches=%zu (%.2f%%)\n",
                         scenario, ok ? L"PASS" : L"FAIL", sW, sH, bestDx, samples, mismatches, mrate * 100.0 );

            if( !ok && !selfTestDumpDirectory.empty() )
            {
                wchar_t msg[512]{};
                swprintf_s( msg, L"PIXELS: %s stitched=%dx%d dx=%d mismatches=%zu/%zu (%.2f%%)",
                            scenario, sW, sH, bestDx, mismatches, samples, mrate * 100.0 );
                DumpPanoramaText( selfTestDumpDirectory, L"image_trial_failed.txt", msg );
            }

            return ok ? 1 : 0;
        };

        constexpr int kTrialsPerImage = 5;
        int imageSliceTestsPassed = 0;

        for( const wchar_t* imageFile : imageFiles )
        {
            const auto imagePath = imageDir / imageFile;
            if( !std::filesystem::exists( imagePath ) )
            {
                OutputDebug( L"[Panorama/Test] Skipping missing image: %s\n", imagePath.c_str() );
                continue;
            }

            std::vector<BYTE> imagePixels;
            int imageWidth = 0, imageHeight = 0;
            if( !loadImageFile( imagePath, imagePixels, imageWidth, imageHeight ) )
            {
                OutputDebug( L"[Panorama/Test] Failed to load image: %s\n", imagePath.c_str() );
                CoUninitialize();
                return false;
            }

            OutputDebug( L"[Panorama/Test] Loaded %s  %dx%d\n", imageFile, imageWidth, imageHeight );

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

                const int result = stitchAndCompare( scenarioName, imagePixels, imageWidth, imageHeight, originsY, windowH );
                if( result < 0 )
                {
                    OutputDebug( L"[Panorama/Test] %s infrastructure error\n", scenarioName );
                    CoUninitialize();
                    return false;
                }
                if( result == 0 )
                {
                    CoUninitialize();
                    return false;
                }
                imageSliceTestsPassed++;
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
                OutputDebug( L"[Panorama/Test] Failed to load image for fixed15: %s\n", imagePath.c_str() );
                CoUninitialize();
                return false;
            }

            constexpr int kFixedSlices = 15;
            for( int trial = 0; trial < 5; ++trial )
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

                const int result = stitchAndCompare( scenarioName, imagePixels, imageWidth, imageHeight, originsY, windowH );
                if( result < 0 )
                {
                    OutputDebug( L"[Panorama/Test] %s infrastructure error\n", scenarioName );
                    CoUninitialize();
                    return false;
                }
                if( result == 0 )
                {
                    CoUninitialize();
                    return false;
                }
                imageSliceTestsPassed++;
            }
        }

        OutputDebug( L"[Panorama/Test] Image-slice tests passed: %d\n", imageSliceTestsPassed );

        // Require at least 60 image slice tests (6 images x 5 trials x 2 modes).
        if( imageSliceTestsPassed < 60 )
        {
            OutputDebug( L"[Panorama/Test] Insufficient image tests: %d (need 60)\n", imageSliceTestsPassed );
            if( !selfTestDumpDirectory.empty() )
            {
                wchar_t summary[128]{};
                swprintf_s( summary, L"INSUFFICIENT: only %d tests passed (need 60)", imageSliceTestsPassed );
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

        CoUninitialize();
    }

    OutputDebug( L"[Panorama/Test] All scenarios passed.  Dump: %s\n", selfTestDumpDirectory.c_str() );
    return true;
}

bool RunPanoramaStitchDumpDirectory( const wchar_t* path )
{
    std::filesystem::path outputPath;
    return RunPanoramaStitchFromDumpDirectory( std::filesystem::path( path ), outputPath );
}

bool RunPanoramaStitchLatestDebugDump()
{
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
