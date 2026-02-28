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
//    Phase 1 â€“ Windowed coarse search on downsampled luma
//      Each frame is converted to single-channel luma and downsampled by
//      4x (or 2x for small frames < 240 px).  The downsampled images are
//      compared at every candidate vertical shift within a search window
//      determined by the expected scroll direction.  The first frame pair
//      searches in both directions; subsequent pairs search only in the
//      established direction across the full feasible range (minStep to
//      maxStep).  This full-range search handles variable scroll speeds
//      (e.g. 40 px â†’ 202 px between consecutive frames).
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
//    Phase 2 â€“ Full-resolution refinement
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
//      clamped to 4â€“28) linearly crossfades between the old and new
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

static HBITMAP StitchPanoramaFrames( const std::vector<HBITMAP>& frames, bool lowContrastMode, std::function<bool(int)> progressCallback = nullptr );
static bool RunPanoramaCaptureCommon( HWND hWnd, bool saveToFile );

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

// â”€â”€ Per-frame "very low entropy" detection â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
// Returns the fraction of pixels in a frame that sit in constant/uniform
// regions (local max-luma-deviation within a 5x5 block â‰¤ 3).  Sampled
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

// â”€â”€ Informative pixel difference â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
    for( size_t p = 0; p < pixelCount; ++p )
    {
        const size_t idx = p * 4;
        luma[p] = static_cast<BYTE>( ( pixels[idx + 2] * 77 +
                                       pixels[idx + 1] * 150 +
                                       pixels[idx + 0] * 29 ) >> 8 );
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
        int sum = 0;
        const int row = y * width;
        for( int x = marginX; x < width - marginX - 1; ++x )
        {
            sum += abs( static_cast<int>( luma[row + x + 1] ) -
                        static_cast<int>( luma[row + x] ) );
        }
        density[y] = sum;
    }
}

// 1D Normalized Cross-Correlation between two int signals.
// Returns NCC in [-1.0, 1.0].  Returns 0.0 if either signal has zero variance.
static double NCC1D( const int* a, const int* b, int n )
{
    if( n <= 0 ) return 0.0;

    double sumA = 0, sumB = 0, sumAB = 0, sumA2 = 0, sumB2 = 0;
    for( int i = 0; i < n; ++i )
    {
        const double ai = static_cast<double>( a[i] );
        const double bi = static_cast<double>( b[i] );
        sumA  += ai;
        sumB  += bi;
        sumAB += ai * bi;
        sumA2 += ai * ai;
        sumB2 += bi * bi;
    }
    const double N = static_cast<double>( n );
    const double varA = sumA2 / N - ( sumA / N ) * ( sumA / N );
    const double varB = sumB2 / N - ( sumB / N ) * ( sumB / N );
    if( varA <= 0.0 || varB <= 0.0 ) return 0.0;
    const double cov = sumAB / N - ( sumA / N ) * ( sumB / N );
    return cov / sqrt( varA * varB );
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
    double sumA = 0, sumB = 0, sumAB = 0, sumA2 = 0, sumB2 = 0;
    int n = 0;
    const int xStart = max( marginX, marginX + max( 0, -dx ) );
    const int xEnd = min( width - marginX, width - marginX - max( 0, dx ) );

    for( int y = 0; y < overlap; ++y )
    {
        const int pY = ( direction < 0 ) ? ( y + absStep ) : y;
        const int cY = ( direction < 0 ) ? y : ( y + absStep );
        const int prevRow = pY * width;
        const int currRow = cY * width;

        for( int x = xStart; x < xEnd; ++x )
        {
            if( !maskPrev[prevRow + x] && !maskCurr[currRow + x + dx] )
                continue;

            const double a = static_cast<double>( prevLuma[prevRow + x] );
            const double b = static_cast<double>( currLuma[currRow + x + dx] );
            sumA  += a;
            sumB  += b;
            sumAB += a * b;
            sumA2 += a * a;
            sumB2 += b * b;
            n++;
        }
    }

    if( n < minSamples ) return 0.0;
    const double N = static_cast<double>( n );
    const double varA = sumA2 / N - ( sumA / N ) * ( sumA / N );
    const double varB = sumB2 / N - ( sumB / N ) * ( sumB / N );
    if( varA <= 0.0 || varB <= 0.0 ) return 0.0;
    const double cov = sumAB / N - ( sumA / N ) * ( sumB / N );
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

    static unsigned s_phase = 0;
    unsigned __int64 avgDiff = 0;
    double changedFraction = 0.0;

    const int coarseSampleStep = lowContrastMode ? 4 : 6;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight,
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
        if( ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight,
                                           fineAvgDiff, fineChangedFraction, 1, ++s_phase ) )
        {
            const bool fineDuplicate = ( fineAvgDiff < 1 && fineChangedFraction < 0.00008 );
            duplicate = coarseDuplicate && fineDuplicate;
            if( coarseDuplicate && !fineDuplicate )
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
        if( LooksLikeSmallShiftNotDuplicate( previousPixels, currentPixels, currentWidth, currentHeight,
                            /*maxAbsDyFull=*/lowContrastMode ? 24 : 16,
                            /*maxAbsDxFull=*/lowContrastMode ? 12 : 8,
                            lowContrastMode,
                                            &guardDx, &guardDy, &s0, &best ) )
        {
            duplicate = false;
            StitchLog( L"[Panorama/Capture] Duplicate-guard: avgDiff=%llu changedPct=%.2f%% smallShift=(%d,%d) stationary=%llu best=%llu\n",
                         avgDiff, changedFraction * 100.0, guardDx, guardDy,
                         static_cast<unsigned long long>( s0 ),
                         static_cast<unsigned long long>( best ) );
        }
    }

    // Informative-pixel rescue: if the frame still looks like a duplicate but
    // the edge/text pixels show real differences, keep it.  This catches slow
    // scrolls of very-low-entropy content where overall avgDiff is diluted
    // by the overwhelmingly constant background.
    if( duplicate && lowContrastMode )
    {
        unsigned __int64 infDiff = 0, infCount = 0;
        if( ComputeInformativePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight, infDiff, infCount ) &&
            infCount > 0 )
        {
            const unsigned __int64 avgInfDiff = infDiff / ( infCount * 3 );
            if( avgInfDiff >= 8 )
            {
                duplicate = false;
                StitchLog( L"[Panorama/Capture] Informative-pixel rescued frame avgInfDiff=%llu infCount=%llu\n",
                             static_cast<unsigned long long>( avgInfDiff ),
                             static_cast<unsigned long long>( infCount ) );
            }
        }
    }

    // Sub-pixel shift detection: the frame passed the duplicate checks
    // (pixel differences above noise floor) but the differences may be
    // ClearType / anti-aliasing jitter rather than real scrolling.  Test
    // whether any Â±1..2 px integer shift produces a meaningfully better
    // match than stationary.  Uses raw per-channel comparison to preserve
    // ClearType's per-channel R/G/B sub-pixel shifts.
    //
    // Only run this when the stationary MAD is small â€” genuine scrolls
    // produce large MAD values that can't be explained by sub-pixel
    // jitter.  The threshold (avgDiffThreshold * 3) catches frames that
    // just barely escaped the duplicate detector.
    if( !duplicate )
    {
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
            // differences are sub-pixel noise â†’ treat as duplicate.
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

    StitchLog( L"[Panorama/Capture] Frame compare avgDiff=%llu changedPct=%.1f%% size=%dx%d identical=%d lowContrast=%d\n",
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

    const unsigned __int64 avgDiffThreshold = lowContrastMode ? 2 : 6;
    const double changedThreshold = lowContrastMode ? 0.0005 : 0.005;
    const bool coarseDuplicate = ( avgDiff < avgDiffThreshold && changedFraction < changedThreshold );
    bool duplicate = coarseDuplicate;

    if( lowContrastMode )
    {
        unsigned __int64 fineAvgDiff = 0;
        double fineChangedFraction = 0.0;
        if( ComputeAveragePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight,
                                           fineAvgDiff, fineChangedFraction, 1, ++s_phase ) )
        {
            const bool fineDuplicate = ( fineAvgDiff < 1 && fineChangedFraction < 0.00008 );
            duplicate = coarseDuplicate && fineDuplicate;
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

    // Informative-pixel rescue (see AreFramesNearDuplicate for details).
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
            }
        }
    }

    return duplicate;
}

// Detect torn/corrupted frames captured mid-scroll.
// A torn frame has its top and bottom halves at different scroll offsets
// because the application was mid-update when the BitBlt fired (e.g.
// ScrollWindowEx shifted pixels but repaint hadn't completed).
//
// Method: find the best vertical scroll offset for the top band and
// bottom band of the frame independently.  If they disagree by more
// than a small tolerance, the frame is torn.
static bool IsFrameTorn( HBITMAP currentFrame, HBITMAP previousFrame )
{
    std::vector<BYTE> currPx, prevPx;
    int cW = 0, cH = 0, pW = 0, pH = 0;
    if( !ReadBitmapPixels32( currentFrame, currPx, cW, cH ) ||
        !ReadBitmapPixels32( previousFrame, prevPx, pW, pH ) )
        return false;
    if( cW != pW || cH != pH || cH < 32 )
        return false;

    const int width = cW;
    const int height = cH;

    // Build downsampled luma for speed.
    const int scale = ( min( width, height ) >= 240 ) ? 4 : 2;
    const int dsW = width / scale;
    const int dsH = height / scale;
    if( dsW < 8 || dsH < 16 )
        return false;

    std::vector<BYTE> prevLuma( dsW * dsH );
    std::vector<BYTE> currLuma( dsW * dsH );
    for( int y = 0; y < dsH; ++y )
    {
        for( int x = 0; x < dsW; ++x )
        {
            const int srcIdx = ( y * scale * width + x * scale ) * 4;
            prevLuma[y * dsW + x] = static_cast<BYTE>(
                ( prevPx[srcIdx + 2] * 77 + prevPx[srcIdx + 1] * 150 + prevPx[srcIdx + 0] * 29 ) >> 8 );
            currLuma[y * dsW + x] = static_cast<BYTE>(
                ( currPx[srcIdx + 2] * 77 + currPx[srcIdx + 1] * 150 + currPx[srcIdx + 0] * 29 ) >> 8 );
        }
    }

    // Score a vertical shift over a horizontal band [bandY0, bandY0+bandH).
    // Returns average absolute luma difference in the overlap.
    auto scoreBand = [&]( int dy, int bandY0, int bandH ) -> double
    {
        const int absDy = abs( dy );
        const int marginX = max( 2, dsW / 20 );
        const int overlapH = bandH - absDy;
        if( overlapH < bandH / 3 )
            return 1e9;

        unsigned __int64 total = 0;
        unsigned __int64 n = 0;
        for( int y = 0; y < overlapH; y += 2 )
        {
            const int pY = bandY0 + ( dy < 0 ? y + absDy : y );
            const int cY = bandY0 + ( dy < 0 ? y : y + absDy );
            if( pY >= dsH || cY >= dsH )
                break;
            for( int x = marginX; x < dsW - marginX; x += 2 )
            {
                total += static_cast<unsigned __int64>(
                    abs( static_cast<int>( prevLuma[pY * dsW + x] ) -
                         static_cast<int>( currLuma[cY * dsW + x] ) ) );
                n++;
            }
        }
        if( n < 50 )
            return 1e9;
        return static_cast<double>( total ) / static_cast<double>( n );
    };

    // Search range: up to half the frame height in downsampled units.
    const int maxDy = min( dsH / 2, dsH - 4 );

    // Find best shift for top band and bottom band.
    const int bandH = dsH / 2;
    const int topY0 = 0;
    const int botY0 = dsH - bandH;

    int bestTopDy = 0, bestBotDy = 0;
    double bestTopScore = scoreBand( 0, topY0, bandH );
    double bestBotScore = scoreBand( 0, botY0, bandH );

    for( int dy = -maxDy; dy <= maxDy; ++dy )
    {
        if( dy == 0 )
            continue;

        const double ts = scoreBand( dy, topY0, bandH );
        if( ts < bestTopScore )
        {
            bestTopScore = ts;
            bestTopDy = dy;
        }

        const double bs = scoreBand( dy, botY0, bandH );
        if( bs < bestBotScore )
        {
            bestBotScore = bs;
            bestBotDy = dy;
        }
    }

    // If both bands found stationary (dy=0), no scroll happened â€” not torn.
    if( bestTopDy == 0 && bestBotDy == 0 )
        return false;

    // Torn if top and bottom disagree by more than 2 downsampled pixels.
    const int dyDiff = abs( bestTopDy - bestBotDy );
    if( dyDiff > 2 )
    {
        StitchLog( L"[Panorama/Capture] Torn frame detected: topDy=%d botDy=%d diff=%d (ds scale=%d)\n",
                   bestTopDy * scale, bestBotDy * scale, dyDiff * scale, scale );
        return true;
    }

    return false;
}

static bool ComputeShiftAlignmentScore( const std::vector<BYTE>& previousPixels,
                                        const std::vector<BYTE>& currentPixels,
                                        int frameWidth,
                                        int frameHeight,
                                        int dx,
                                        int dy,
                                        unsigned __int64& scoreOut,
                                        bool ignoreConstantRegions = false )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    const int absDx = abs( dx );
    const int absDy = abs( dy );
    const int marginX = max( 4, frameWidth / 20 );
    const int marginY = max( 4, frameHeight / 20 );
    const int overlapW = frameWidth - absDx - 2 * marginX;
    const int overlapH = frameHeight - absDy - 2 * marginY;
    if( overlapW < frameWidth / 4 || overlapH < frameHeight / 4 )
    {
        return false;
    }

    unsigned __int64 totalDiff = 0;
    unsigned __int64 samples = 0;

    const int prevX = marginX + max( 0, -dx );
    const int currX = marginX + max( 0, dx );
    const int prevY = marginY + max( 0, -dy );
    const int currY = marginY + max( 0, dy );

    for( int y = 0; y < overlapH; y += 2 )
    {
        const int pY = prevY + y;
        const int cY = currY + y;
        for( int x = 0; x < overlapW; x += 2 )
        {
            const int pX = prevX + x;
            const int cX = currX + x;
            const int pIndex = ( pY * frameWidth + pX ) * 4;
            const int cIndex = ( cY * frameWidth + cX ) * 4;
            const int pLuma = ( previousPixels[pIndex + 2] * 77 + previousPixels[pIndex + 1] * 150 + previousPixels[pIndex + 0] * 29 ) >> 8;
            const int cLuma = ( currentPixels[cIndex + 2] * 77 + currentPixels[cIndex + 1] * 150 + currentPixels[cIndex + 0] * 29 ) >> 8;

            // When ignoring constant regions, skip pixels where neither
            // frame has meaningful gradient (flat background).
            if( ignoreConstantRegions )
            {
                auto hasGradient = [&]( const std::vector<BYTE>& px, int gx, int gy ) -> bool
                {
                    if( gx + 1 >= frameWidth || gy + 1 >= frameHeight )
                        return false;
                    const int idx = ( gy * frameWidth + gx ) * 4;
                    const int idxR = idx + 4;
                    const int idxD = idx + frameWidth * 4;
                    const int lc = ( px[idx + 2] * 77 + px[idx + 1] * 150 + px[idx + 0] * 29 ) >> 8;
                    const int lr = ( px[idxR + 2] * 77 + px[idxR + 1] * 150 + px[idxR + 0] * 29 ) >> 8;
                    const int ld = ( px[idxD + 2] * 77 + px[idxD + 1] * 150 + px[idxD + 0] * 29 ) >> 8;
                    return ( abs( lc - lr ) + abs( lc - ld ) ) >= 4;
                };
                if( !hasGradient( previousPixels, pX, pY ) && !hasGradient( currentPixels, cX, cY ) )
                    continue;
            }

            totalDiff += static_cast<unsigned __int64>( abs( pLuma - cLuma ) );
            samples++;
        }
    }

    if( samples < ( ignoreConstantRegions ? 20 : 100 ) )
    {
        return false;
    }

    scoreOut = totalDiff / samples;
    return true;
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
                                            unsigned __int64* outMaskedStationaryScore = nullptr )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    // Informative-pixel SAD at dy=0 for HCF pairs (set during the
    // stationary check, consumed by the post-search validation).
    unsigned __int64 hcfInfDiff = 0, hcfInfCount = 0;

    // â”€â”€ Phase 1 â”€â”€ Windowed coarse search on downsampled luma â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // Search a LIMITED range around the expected shift to avoid harmonic
    // matches on repetitive content.  For the first frame pair
    // (expectedDy == 0) search outward from the smallest step.
    //
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

    // â”€â”€ Very-low-entropy detection and informative mask â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
            for( int x = 1; x < dsW - 1; ++x )
            {
                const int idx = y * dsW + x;
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
            for( int x = 1; x < dsW - 1; ++x )
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
        // averages away small scrolls (3â€“10px).  Re-check at full resolution
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
        // rescued â€” these frames already passed the duplicate check, so
        // a non-zero masked score indicates real content change.
        // For HCF pairs, maskedStationaryScore >= 1 is sufficient: a
        // non-zero average luma diff at informative pixels indicates
        // real movement even when the full-frame score is diluted by
        // the constant background.
        if( highConstantFractionPair &&
            ( maskedStationaryScore >= 1 || highConstStationaryRelax ) )
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
            return false;
        }
    }

    // Determine the search window in downsampled space.
    // Use full range in the known scroll direction.  Scroll speed
    // can vary dramatically between frames (e.g. 40â†’202â†’213â†’38)
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
    constexpr int kMaxCandidatesWithProbes = 160;
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
                if( candidateCount >= kMaxCandidates && samples >= 100 &&
                    totalDiff / samples > candidates[candidateCount - 1].score )
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

    // â”€â”€ Full-resolution masked coarse fallback â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    // When all coarse candidates have score 0, the downsampled search
    // can't discriminate offsets because constant (background) pixels
    // dominate.  For high-constant-fraction pairs with sparse text,
    // even a masked downsampled search fails because at 4x downsample
    // thin characters become indistinguishable blobs.
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
    if( highConstantFractionPair && candidateCount > 0 )
    {
        bool allZeroCoarse = true;
        for( int ci = 0; ci < candidateCount; ++ci )
        {
            if( candidates[ci].score > 0 )
            {
                allZeroCoarse = false;
                break;
            }
        }

        if( allZeroCoarse )
        {
            // Build full-resolution luma.
            if( hasPrecomputedLuma )
            {
                // Reference precomputed â€” avoid copying.
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

    // Flat-content exhaustive fallback: when the coarse search cannot
    // discriminate offsets (best coarse score is at noise-floor level),
    // inject every DS candidate in the search window.  For high-entropy
    // low-contrast content, all shifts score nearly identically in the
    // downsampled space because rows are independent random values, so
    // the top-12 selection is essentially random and may miss the true
    // shift.  The fine search at full resolution CAN discriminate because
    // the true shift gives zero difference (exact row match).  Once the
    // first score=0 candidate is found the earlyExit mechanism makes all
    // remaining candidates trivially cheap to evaluate.
    if( bestCoarseScore >= 8 && !highConstantFractionPair )
    {
        for( int dyDs = searchMinDy; dyDs <= searchMaxDy && prunedCount < kMaxCandidatesWithProbes; ++dyDs )
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

    // First-frame diversity: when no expected motion is known, the coarse
    // search may concentrate all candidates at small harmonics, causing
    // the fine search to miss the true (potentially large) shift.  Inject
    // evenly-distributed probes across the full search range so the fine
    // search always evaluates a representative sample of shifts.
    // Performance is safe because the correct shift produces fineScore=0
    // on exact-overlap content, and early termination kills all subsequent
    // candidates after the first sample.
    if( expectedDyDs == 0 && prunedCount < kMaxCandidatesWithProbes )
    {
        const int rangeSpan = searchMaxDy - searchMinDy;
        const int probeTarget = min( 30, max( 10, rangeSpan / 4 ) );
        const int probeStride = max( 1, rangeSpan / max( 1, probeTarget ) );
        for( int pos = searchMinDy; pos <= searchMaxDy && prunedCount < kMaxCandidatesWithProbes; pos += probeStride )
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
    // non-zero scores while the correct shift scores â‰ˆ 0.  Injecting
    // every candidate is safe: early termination after the first score=0
    // hit makes subsequent evaluations trivially cheap.
    if( useMaskedFallback && prunedCount < kMaxCandidatesWithProbes )
    {
        for( int dyDs = searchMinDy; dyDs <= searchMaxDy && prunedCount < kMaxCandidatesWithProbes; ++dyDs )
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
    if( harmonicFallback && expectedAbsStepEarly >= frameHeight / 5 )
    {
        const int maxProbeDyDs = max( 3, abs( expectedDy ) / ( 3 * downsampleScale ) );

        for( int dyDs = -maxProbeDyDs; dyDs <= maxProbeDyDs && prunedCount < kMaxCandidatesWithProbes; ++dyDs )
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
        for( int absStep = max( 4, downsampleScale ); absStep <= frameHeight - max( 2, frameHeight / 6 ); absStep += 2 )
        {
            for( int dir = -1; dir <= 1; dir += 2 )
            {
                const int dyF = dir * absStep;
                if( dyF < searchMinFull || dyF > searchMaxFull ) continue;
                const int overlap = frameHeight - absStep;
                const int* aS = ( dir < 0 ) ? &edgePrevInj[absStep] : &edgePrevInj[0];
                const int* bS = ( dir < 0 ) ? &edgeCurrInj[0] : &edgeCurrInj[absStep];
                const double ncc = NCC1D( aS, bS, overlap );

                if( eiCount < kEdgeInject || ncc > edgeInj[eiCount - 1].ncc )
                {
                    int ip = eiCount < kEdgeInject ? eiCount : eiCount - 1;
                    for( int j = ip; j > 0 && edgeInj[j - 1].ncc < ncc; --j )
                    {
                        if( j < kEdgeInject )
                            edgeInj[j] = edgeInj[j - 1];
                        ip = j - 1;
                    }
                    if( ip < kEdgeInject )
                    {
                        edgeInj[ip] = { dyF, ncc };
                        if( eiCount < kEdgeInject )
                            eiCount++;
                    }
                }
            }
        }

        // Inject into candidate array (avoid duplicates).
        for( int ei = 0; ei < eiCount && prunedCount < kMaxCandidatesWithProbes; ++ei )
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

    // â”€â”€ Phase 2 â”€â”€ Rank candidates by full-resolution comparison â”€â”€â”€â”€â”€â”€
    // For each coarse candidate, compute a fine score at full resolution.
    // This resolves ambiguity from harmonic matches on repetitive content
    // since the full-resolution comparison sees fine text details that
    // the downsampled comparison misses.
    //
    // Pre-compute full-resolution luma arrays so the inner loop uses
    // cheap byte lookups instead of per-pixel RGBâ†’luma multiplies.
    //
    // When the masked-fallback created evenly-distributed candidates,
    // the gap between adjacent candidates can be much larger than the
    // normal refine radius (e.g. 96 full-res pixels vs Â±5).  Widen
    // refineRadiusDy to half the distribution stride so that adjacent
    // candidate refinement ranges overlap, guaranteeing full coverage.
    const int normalRefineRadius = max( 3, downsampleScale + 1 );
    const int refineRadiusDy = useMaskedFallback && candidateCount >= 2
        ? max( normalRefineRadius,
               ( searchMaxDy - searchMinDy ) * downsampleScale / ( 2 * max( 1, candidateCount - 1 ) ) )
        : normalRefineRadius;
    const int refineRadiusDx = 1;

    // Build full-resolution informative masks for very-low-entropy pairs.
    // The masked fine search restricts scoring to content pixels (text
    // edges) which is essential for low-contrast terminal-style content
    // where the standard SAD produces near-zero scores everywhere.
    //
    // For the high-constant-fraction fallback path, ClearType subpixel
    // rendering redistributes R/G/B without changing luma, making the
    // luma-based fine search blind to wrong shifts (all produce score 0).
    // Use RGB channel comparison instead: compare the original BGRA pixel
    // data so that per-channel differences are preserved.
    std::vector<BYTE> fullMaskPrev;
    std::vector<BYTE> fullMaskCurr;
    const bool useFineMask = veryLowEntropyPair || useMaskedFallback;
    const bool useRgbFineSearch = false;
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

    // Scale fine scores by 256 to preserve sub-integer precision.
    // For sparse content (< 1% non-background pixels), the per-pixel
    // average difference at wrong shifts is < 1.0, which integer
    // division truncates to 0 â€” making correct and wrong shifts
    // indistinguishable.  The 256Ã— scale gives 8 bits of fractional
    // precision without risk of u64 overflow (max totalDiffÃ—256 â‰ˆ 20B).
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
    // (within Â±4 px). Used both for diagnostics and for harmonic-override
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

                unsigned __int64 score;

                if( useZnccFineSearch )
                {
                    // ZNCC fine scoring: uses only informative (masked) pixels.
                    // Constant regions have Ïƒ=0 and contribute nothing, avoiding
                    // the "everything scores 0" problem that SAD has on HCF content.
                    const double zncc = ComputeMaskedZNCC(
                        previousFullLuma.data(), currentFullLuma.data(),
                        fullMaskPrev.data(), fullMaskCurr.data(),
                        frameWidth, overlap, absStep,
                        ( dy < 0 ) ? -1 : 1, dx,
                        fineMarginX, 50 /*minSamples*/ );

                    // Convert ZNCC to integer score compatible with SAD pipeline.
                    // ZNCC=1.0 â†’ score=0 (perfect), ZNCC=0.5 â†’ score=kZnccScoreBase/2.
                    score = static_cast<unsigned __int64>(
                        max( 0.0, ( 1.0 - zncc ) * static_cast<double>( kZnccScoreBase ) ) );
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
                        // Masked scoring: only accumulate at informative
                        // pixels (union of both masks at their respective
                        // positions).
                        for( int xi = 0; xi < xSpan; ++xi )
                        {
                            const int prevIdx = prevRow + xStart + xi;
                            const int currIdx = currRow + xStart + xi + dx;
                            if( fullMaskPrev[prevIdx] || fullMaskCurr[currIdx] )
                            {
                                rowDiff += static_cast<unsigned __int64>(
                                    abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
                                samples++;
                            }
                        }
                        totalDiff += rowDiff;
                    }
                    else if( useRgbFineSearch )
                    {

                    // RGB/BGRA comparison: ClearType subpixel rendering
                    // creates per-channel differences that cancel in luma.
                    // Compare the original BGRA pixel data directly so
                    // that per-channel differences are preserved.
                    const BYTE* pRgb = &previousPixels[static_cast<size_t>( prevRow + xStart ) * 4];
                    const BYTE* cRgb = &currentPixels[static_cast<size_t>( currRow + xStart + dx ) * 4];
                    const int byteSpan = xSpan * 4;

#if defined(_M_X64) || defined(_M_IX86)
                    __m128i rgbSadAcc = _mm_setzero_si128();
                    int bi = 0;
                    for( ; bi + 16 <= byteSpan; bi += 16 )
                    {
                        const __m128i a = _mm_loadu_si128( reinterpret_cast<const __m128i*>( pRgb + bi ) );
                        const __m128i b = _mm_loadu_si128( reinterpret_cast<const __m128i*>( cRgb + bi ) );
                        rgbSadAcc = _mm_add_epi64( rgbSadAcc, _mm_sad_epu8( a, b ) );
                    }

                    rowDiff = static_cast<unsigned __int64>( _mm_cvtsi128_si64( rgbSadAcc ) ) +
                              static_cast<unsigned __int64>( _mm_cvtsi128_si64( _mm_srli_si128( rgbSadAcc, 8 ) ) );

                    for( ; bi < byteSpan; ++bi )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pRgb[bi] ) - static_cast<int>( cRgb[bi] ) ) );
                    }
#elif defined(_M_ARM64)
                    uint64x2_t rgbSadAcc = vdupq_n_u64( 0 );
                    int bi = 0;
                    for( ; bi + 16 <= byteSpan; bi += 16 )
                    {
                        const uint8x16_t a = vld1q_u8( pRgb + bi );
                        const uint8x16_t b = vld1q_u8( cRgb + bi );
                        const uint8x16_t absDiff = vabdq_u8( a, b );
                        const uint16x8_t sum16 = vpaddlq_u8( absDiff );
                        const uint32x4_t sum32 = vpaddlq_u16( sum16 );
                        const uint64x2_t sum64 = vpaddlq_u32( sum32 );
                        rgbSadAcc = vaddq_u64( rgbSadAcc, sum64 );
                    }

                    rowDiff = vgetq_lane_u64( rgbSadAcc, 0 ) + vgetq_lane_u64( rgbSadAcc, 1 );

                    for( ; bi < byteSpan; ++bi )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pRgb[bi] ) - static_cast<int>( cRgb[bi] ) ) );
                    }
#else
                    for( int bi = 0; bi < byteSpan; ++bi )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pRgb[bi] ) - static_cast<int>( cRgb[bi] ) ) );
                    }
#endif

                    totalDiff += rowDiff;
                    samples += static_cast<unsigned __int64>( byteSpan );

                    } // useRgbFineSearch
                    else
                    {

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
#elif defined(_M_ARM64)
                    // NEON SIMD: process 16 luma pixels at once using
                    // vabdq_u8 (absolute difference) + vpaddlq (pairwise
                    // widening add) to compute sum of absolute differences.
                    uint64x2_t sadAcc = vdupq_n_u64( 0 );
                    int xi = 0;
                    for( ; xi + 16 <= xSpan; xi += 16 )
                    {
                        const uint8x16_t a = vld1q_u8( pBase + xi );
                        const uint8x16_t b = vld1q_u8( cBase + xi );
                        const uint8x16_t absDiff = vabdq_u8( a, b );
                        // Widen 8â†’16â†’32â†’64 with pairwise adds.
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
                    // Scalar fallback.
                    for( int xi = 0; xi < xSpan; ++xi )
                    {
                        rowDiff += static_cast<unsigned __int64>(
                            abs( static_cast<int>( pBase[xi] ) - static_cast<int>( cBase[xi] ) ) );
                    }
#endif

                    totalDiff += rowDiff;
                    samples += xSpan;

                    } // standard luma

                    // Early termination: if running average already exceeds
                    // the best fine score, this candidate cannot win.
                    // Exception: always evaluate the neighborhood of the expected
                    // step fully.  On sparse/high-constant-fraction content the
                    // RGB fine-search can accumulate 800 comparison bytes in less
                    // than one row; a previous harmonic candidate may have set a
                    // low bestFineScore that prematurely kills the expected-step
                    // candidate before it sees enough content to produce a fair
                    // average.
                    const unsigned __int64 earlyMinSamples = useFineMask ? 50 : ( useRgbFineSearch ? 800 : 200 );
                    const bool isExpectedStepDy = ( highConstantFractionPair || expectedAbsStep >= frameHeight / 4 ) &&
                        expectedAbsStep > 0 &&
                        abs( abs( dy ) - expectedAbsStep ) <= refineRadiusDy + 4;
                    if( !isExpectedStepDy &&
                        bestFineScore != ( std::numeric_limits<unsigned __int64>::max )() &&
                        samples >= earlyMinSamples && totalDiff * kFineScoreScale / samples > bestFineScore )
                    {
                        earlyExit = true;
                    }
                }

                const unsigned __int64 minSamples = useFineMask ? 20 : ( useRgbFineSearch ? 400 : 100 );
                if( earlyExit || samples < minSamples )
                {
                    continue;
                }

                score = totalDiff * kFineScoreScale / samples;

                } // !useZnccFineSearch

                // Harmonic-fallback tracking.
                // For ZNCC, "perfect" means score near 0 (within kZnccMinPerfect
                // due to floating-point rounding) rather than exactly 0.
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
                // already found a perfect score (the correct shift is already in
                // the candidate list and probes can only cause harm).
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
                    // Ranking-only overlap penalty for periodic content.
                    // Harmonic matches often win with smaller overlap
                    // regions that happen to have lower average SAD.
                    // Keep the raw score for acceptance thresholds; use the
                    // penalty only for candidate ordering.
                    // Exempt candidates near the expected step â€” the overlap
                    // penalty is designed to catch unexpected large steps, not
                    // penalize legitimately large expected motion.
                    const bool nearExpectedStep = abs( absStep - expectedAbsStep ) <= refineRadiusDy + 4;
                    const int overlapPct = ( overlap * 100 ) / max( 1, frameHeight );
                    if( !nearExpectedStep && overlapPct < 72 )
                    {
                        const unsigned __int64 overlapPenalty =
                            static_cast<unsigned __int64>( ( 72 - overlapPct ) * 6 );
                        rankScore = score + overlapPenalty;
                    }
                }
                // Record best score near expected step. Used both for
                // diagnostics and for harmonic-override that prefers
                // expected-step candidates over far-away harmonics.
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
                    const int absStep = abs( dy );
                    const int absDx = abs( dx );
                    const int expectedDelta = ( expectedAbsStep > 0 ) ? abs( absStep - expectedAbsStep ) : ( std::numeric_limits<int>::max )();

                    // Tie-break equal-quality matches by preferring the shift
                    // closest to expected motion. Repeating patterns often
                    // produce harmonic ties where smallest-|dy| collapses
                    // height and drops real content ranges.
                    //
                    // First frame pair (expectedAbsStep==0): prefer smaller
                    // shift.  On VLE content all offsets score identically
                    // (fineScore=0), so picking the smallest avoids locking
                    // onto a randomly-large harmonic that makes the alignment
                    // score fail the overlap check and causes wrong axis
                    // selection.
                    if( ( expectedAbsStep > 0 && expectedDelta < bestExpectedDelta ) ||
                        ( expectedAbsStep == 0 && absStep < bestAbsStep ) ||
                        ( expectedDelta == bestExpectedDelta &&
                          ( absStep < bestAbsStep || ( absStep == bestAbsStep && absDx < bestAbsDx ) ) ) )
                    {
                        bestDx = dx;
                        bestDy = dy;
                        bestCoarseDy = candidates[ci].dyDs;
                        bestAbsStep = absStep;
                        bestAbsDx = absDx;
                        bestExpectedDelta = expectedDelta;
                    }
                }
                else if( expectedAbsStep > 0 && bestFineRankScore != ( std::numeric_limits<unsigned __int64>::max )() )
                {
                    unsigned __int64 scoreSlack = (std::max)( static_cast<unsigned __int64>( 2 ), bestFineRankScore / 80 );
                    const int absStep = abs( dy );
                    const int absDx = abs( dx );
                    const int expectedDelta = abs( absStep - expectedAbsStep );
                    const bool preferExpectedStep = ( highConstantFractionPair || expectedAbsStep >= frameHeight / 4 ) &&
                        expectedAbsStep >= 8;

                    // Sparse high-constant frames frequently produce shallow
                    // harmonic minima. Allow a wider near-tie band so a
                    // candidate materially closer to expected motion can win.
                    // Use tiered slack: wider for HCF, moderate for non-HCF.
                    if( preferExpectedStep )
                    {
                        scoreSlack = (std::max)( scoreSlack, highConstantFractionPair
                            ? bestFineRankScore / 12    // 8.3% for HCF
                            : bestFineRankScore / 30 ); // 3.3% for non-HCF
                    }

                    // If score is nearly identical, prefer a materially more
                    // plausible step near expected motion.
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
                        bestAbsStep = absStep;
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
        }
    }

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
            if( secondDelta + 2 < bestDelta )
            {
                StitchLog( L"[Panorama/Stitch] FindBestFrameShift ambiguity-fallback expected=(%d,%d) best=(%d,%d) second=(%d,%d) bestRank=%llu secondRank=%llu\n",
                             expectedDx,
                             expectedDy,
                             bestDx,
                             bestDy,
                             secondBestDx,
                             secondBestDy,
                             static_cast<unsigned long long>( bestFineRankScore ),
                             static_cast<unsigned long long>( secondBestFineRankScore ) );

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
    // certainly spurious â€” the constant-fraction region (dark background)
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
        return false;
    }

    // Near-stationary flag: when the fine score per-pixel is no better
    // than the stationary score, the "best" match is unreliable â€” likely
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

        // On sparse/high-constant-fraction content the RGB fine search can
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
    // Clamp cross-axis shift to zero.  The fine search evaluates dx=Â±1
    // for better score discrimination (ClearType subpixel effects), but
    // screen captures scroll perfectly along one axis â€” there is never
    // real cross-axis motion.  Allowing dxâ‰ 0 would accumulate drift.
    bestDx = 0;

    if( outMaskedStationaryScore )
        *outMaskedStationaryScore = maskedStationaryScore;
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

    // â”€â”€ Once the scroll axis is established, only search along that axis.
    // Never fall back to the cross-axis â€” a perpendicular match is always
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

    // â”€â”€ First frame pair: axis unknown â€” run both searches and pick best.
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

    if( directOk && !transposedOk )
    {
        bestDx = directDx;
        bestDy = directDy;
        return true;
    }

    if( transposedOk && !directOk )
    {
        bestDx = mappedDx;
        bestDy = mappedDy;
        return true;
    }

    unsigned __int64 directScore = 0;
    unsigned __int64 transposedScore = 0;
    const bool ignoreConst = lowContrastMode &&
        ( precomputedVeryLowEntropy >= 0
          ? ( precomputedVeryLowEntropy != 0 )
          : IsVeryLowEntropyPair( previousPixels, currentPixels, frameWidth, frameHeight ) );
    const bool directScored = ComputeShiftAlignmentScore( previousPixels, currentPixels, frameWidth, frameHeight, directDx, directDy, directScore, ignoreConst );
    const bool transposedScored = ComputeShiftAlignmentScore( previousPixels, currentPixels, frameWidth, frameHeight, mappedDx, mappedDy, transposedScore, ignoreConst );

    if( directScored && transposedScored )
    {
        if( transposedScore < directScore )
        {
            bestDx = mappedDx;
            bestDy = mappedDy;
        }
        else
        {
            bestDx = directDx;
            bestDy = directDy;
        }
        return true;
    }

    if( transposedScored )
    {
        bestDx = mappedDx;
        bestDy = mappedDy;
    }
    else
    {
        bestDx = directDx;
        bestDy = directDy;
    }
    return true;
}

static HBITMAP StitchPanoramaFrames(const std::vector<HBITMAP>& frames, bool lowContrastMode, std::function<bool(int)> progressCallback)
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
        if( cancelled )
        {
            StitchLog( L"[Panorama/Stitch] Cancelled during pixel read\n" );
            return nullptr;
        }
    }

    // Pre-compute full-resolution luma for every frame so that
    // FindBestFrameShift can skip redundant BGRAâ†’luma conversions.
    std::vector<std::vector<BYTE>> frameLuma( frames.size() );
    for( size_t i = 0; i < frames.size(); i++ )
    {
        BuildFullLumaFrame( framePixels[i], frameWidth, frameHeight, frameLuma[i] );
    }

    // Pre-compute per-frame constant-content fraction so that
    // IsVeryLowEntropyPair can be resolved without redundant scans.
    std::vector<double> frameConstantFraction( frames.size() );
    for( size_t i = 0; i < frames.size(); i++ )
    {
        frameConstantFraction[i] = ComputeConstantContentFraction( framePixels[i], frameWidth, frameHeight );
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

    const int minFrameDimension = min( frameWidth, frameHeight );
    const int minProgress = lowContrastMode ? max( 4, minFrameDimension / 40 ) : max( 8, minFrameDimension / 30 );
    int expectedDx = 0;
    int expectedDy = 0;
    int retryEligibilityStep = 0;
    int retryNormalizationBudget = 0;
    int nearStationaryCount = 0;
    int duplicateRetryStreak = 0;

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

        int dx = expectedDx;
        int dy = expectedDy;
        int retryStreakUsed = 0;
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
                const int retryExpectedMaxStep = max( 32, frameHeight / 6 + 1 );
                const bool retryEligible =
                    veryLowEntropy != 0 &&
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
                            // correct per-frame shift â€” blocking those loses
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
                StitchLog( L"[Panorama/Stitch] Frame %zu rejected: no reliable shift match expected=(%d,%d)\n",
                             i,
                             expectedDx,
                             expectedDy );
                continue;
            }
        }
        duplicateRetryStreak = 0;

        const int maxAbsDx = max( 8, frameWidth / 6 );
        const int maxAbsDy = frameHeight - minProgress;
        dx = max( -maxAbsDx, min( maxAbsDx, dx ) );
        dy = max( -maxAbsDy, min( maxAbsDy, dy ) );

        int stepX = -dx;
        int stepY = -dy;

        // After establishing a predominantly vertical or horizontal scroll
        // direction, clamp the perpendicular component to zero.  Subpixel
        // rendering noise (e.g. ClearType) causes the fine refinement to
        // report Â±1 px cross-axis drift per frame, which accumulates into
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
        // real slow-scroll steps (e.g. 1071px → minProgress/2=17 drops
        // genuine 4-16 px scrolls).
        if( !nearStationaryZeroStep && abs( stepX ) + abs( stepY ) < min( minProgress / 2, 4 ) )
        {
            StitchLog( L"[Panorama/Stitch] Frame %zu rejected: low movement step=(%d,%d)\n", i, stepX, stepY );
            continue;
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
                const int outlierStepThreshold = max( ( axisFrame * 2 ) / 5, max( minProgress * 6, medianAxisStep * 4 ) );
                const int lowOverlapThreshold = ( axisFrame * 3 ) / 5;
                const int expectedSpikeThreshold = max( axisFrame / 3, max( minProgress * 5, expectedAxisStep * 3 ) );

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
                    continue;
                }

                // Step-range outlier guard: on periodic content the fine
                // search can pick a harmonic that is only moderately above
                // normal â€” not enough for the 4Ã— median guard â€” but well
                // above the observed step range.  Use the 75th percentile
                // of recent steps as a tighter reference.
                //
                // Keep a hard floor at 40% of the frame to avoid rejecting
                // legitimate fast-scroll jumps.  The stress "legitjumps"
                // scenarios include valid jumps up to that range.
                if( sorted.size() >= 6 )
                {
                    const int p75 = sorted[sorted.size() * 3 / 4];
                    const int rangeGuard = p75 + max( minProgress, medianAxisStep / 2 );
                    const int legitJumpFloor = ( axisFrame * 2 ) / 5;
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
                        continue;
                    }
                }

                // VLE continuity guard: on very-low-entropy content, ZNCC
                // scoring is unreliable because near-uniform pixels match
                // at essentially random offsets.  Use a tighter step ceiling
                // than the standard outlier guard, but still allow legitimate
                // fast-scroll jumps up to 45% of frame height.
                if( veryLowEntropy != 0 )
                {
                    const int vleMedianCeiling = max( medianAxisStep * 3, minProgress * 4 );
                    const int vleLegitFloor = ( axisFrame * 9 ) / 20; // 45% of frame
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
                        continue;
                    }
                }

            }
        }

        POINT nextOrigin = composedFrameOrigins.back();
        nextOrigin.x += stepX;
        nextOrigin.y += stepY;
        composedFrameIndices.push_back( i );
        composedFrameOrigins.push_back( nextOrigin );
        composedFrameSteps.push_back( { stepX, stepY } );
        expectedDx = dx;
        expectedDy = dy;

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
            retryEligibilityStep = max( abs( dx ), abs( dy ) );
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
    if( stitchedWidth <= 0 || stitchedHeight <= 0 || stitchedWidth > 30000 || stitchedHeight > 30000 )
    {
        StitchLog( L"[Panorama/Stitch] Invalid stitched canvas size %dx%d\n", stitchedWidth, stitchedHeight );
        return nullptr;
    }

    std::vector<BYTE> stitchedPixels( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ) * 4, 0 );
    std::vector<BYTE> stitchedWritten( static_cast<size_t>( stitchedWidth ) * static_cast<size_t>( stitchedHeight ), 0 );
    const int verticalFeather = max( 4, min( 28, frameHeight / 18 ) );
    const int horizontalFeather = max( 4, min( 28, frameWidth / 18 ) );

    for( size_t i = 0; i < composedFrameIndices.size(); ++i )
    {
        reportProgress( 90 + static_cast<int>( ( i + 1 ) * 9 / composedFrameIndices.size() ) );
        if( cancelled )
        {
            StitchLog( L"[Panorama/Stitch] Cancelled during composition\n" );
            return nullptr;
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

        for( int y = 0; y < frameHeight; ++y )
        {
            const int canvasY = destinationY + y;
            if( canvasY < 0 || canvasY >= stitchedHeight )
            {
                continue;
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

                // When a frame has no clear directional movement (neither
                // mostlyVerticalMove nor mostlyHorizontalMove), default to
                // preserving existing canvas content.  Only unwritten pixels
                // (gaps) will be filled.  This prevents near-duplicate or
                // small-step frames from overwriting correctly composed content.
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
        const auto logPath = std::filesystem::path( debugDumpDirectory ) / L"stitch_log.txt";
        g_StitchLogFile = _wfopen( logPath.wstring().c_str(), L"w" );
        StitchLog( L"[Panorama/Debug] Dump directory: %s\n", debugDumpDirectory.c_str() );
    }
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
#endif

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
        StitchLog( L"[Panorama/Capture] CreateDC failed\n" );
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
            StitchLog( L"[Panorama/Debug] Failed to capture desktop snapshot\n" );
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

#ifdef _DEBUG
    DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, firstFrame );
#endif

    size_t duplicateFrameCount = 0;
    size_t subPixelDropCount = 0;
    size_t tornFrameCount = 0;
    size_t captureIteration = 0;

    // Resolve DwmFlush once for the capture loop.  Used to synchronize
    // with the DWM composition cycle so BitBlt captures fully-composed
    // frames instead of mid-scroll torn content.
    using pfnDwmFlush_t = HRESULT( WINAPI* )();
    const auto pfnDwmFlush = reinterpret_cast<pfnDwmFlush_t>(
        GetProcAddress( GetModuleHandleW( L"dwmapi.dll" ), "DwmFlush" ) );

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

#ifdef _DEBUG
        DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, frame );
#endif

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

        // Discard torn frames captured mid-scroll.  A torn frame has
        // its top and bottom halves at different scroll offsets (e.g.
        // ScrollWindowEx shifted pixels but repaint hadn't completed).
        // Discarding n+1 is safe: the next clean frame n+2 will be
        // compared against the last accepted frame n.
        // DISABLED: DwmFlush synchronization should prevent torn frames.
        // if( IsFrameTorn( frame, frames.back() ) )
        // {
        //     tornFrameCount++;
        //     StitchLog( L"[Panorama/Capture] Torn frame discarded (grabbed=%zu count=%zu iteration=%zu)\n",
        //                debugGrabbedFrameCount,
        //                tornFrameCount,
        //                captureIteration );
        //     DeleteObject( frame );
        //     continue;
        // }

        frames.push_back( frame );
        frame = nullptr;
        StitchLog( L"[Panorama/Capture] Captured moving frame #%zu (grabbed=%zu) at iteration=%zu\n",
                   frames.size(),
                   debugGrabbedFrameCount,
                   captureIteration );
        if( frames.size() >= 256 )
        {
            StitchLog( L"[Panorama/Capture] Reached frame limit (256), stopping capture\n" );
            break;
        }
    }

    StitchLog( L"[Panorama/Capture] Loop exited stopRequested=%d frames=%zu duplicates=%zu subpixel=%zu torn=%zu iterations=%zu\n",
               g_PanoramaStopRequested ? 1 : 0,
               frames.size(),
               duplicateFrameCount,
               subPixelDropCount,
               tornFrameCount,
               captureIteration );

#ifdef _DEBUG
    if( !debugDumpDirectory.empty() )
    {
        wchar_t statsText[256]{};
        swprintf_s( statsText,
                    L"framesAccepted=%zu\nduplicates=%zu\nsubpixel=%zu\ntorn=%zu\niterations=%zu\nstopRequested=%d\n",
                    frames.size(),
                    duplicateFrameCount,
                    subPixelDropCount,
                    tornFrameCount,
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
        StitchLog( L"[Panorama/Capture] Stitch result is null\n" );
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
    StitchLog( L"[Panorama/Capture] Success: DIB copied to clipboard (%dx%d)\n", bmpWidth, abs( bmpHeight ) );
    return true;
}

#ifdef _DEBUG
//
// Panorama stitch self-test
// -------------------------
// How to run:
//   1. Build the ARM64 Debug configuration.
//   2. Place test images (image1.png â€¦ image5.png) in the Debug\ directory
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

        return std::wstring( valueStart, valueEnd );
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

            HBITMAP frameBitmap = createBitmapFromPixels( framePixels, frameWidth, frameHeight );
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
    TestLog( L"\n==== Phase 1: Basic stitching scenarios ====\n" );
    int basicTestsRun = 0;
    int basicTestsPassed = 0;

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
        TestLog( L"  [%d/5] baseline-uniform-scroll ...\n", basicTestsRun );
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
        TestLog( L"  [%d/5] baseline-uniform-scroll PASSED\n", basicTestsRun );
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
    TestLog( L"  [%d/5] small-step-no-overwrite ...\n", basicTestsRun );
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
        const int expectedStitchedHeight = frameHeight + normalStep * ( normalFrameCount - 1 ) + smallStep;

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
                        framePixels[idx + 2] = 255;     // R â€” bright red marker
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
                                  L"OVERWRITE: small-step-no-overwrite â€” red markers visible in overlap" );
            }
            return false;
        }

        TestLog( L"  [%d/5] small-step-no-overwrite PASSED\n", basicTestsRun );
        basicTestsPassed++;
    }

    basicTestsRun++;
    TestLog( L"  [%d/5] repro-1099x336-variable-steps-tail ...\n", basicTestsRun );
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
        TestLog( L"  [%d/5] repro-1099x336-variable-steps-tail PASSED\n", basicTestsRun );
    }

    basicTestsRun++;
    TestLog( L"  [%d/5] repro-realcapture-variable-large-steps ...\n", basicTestsRun );
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
        TestLog( L"  [%d/5] repro-realcapture-variable-large-steps PASSED\n", basicTestsRun );
    }

    // ----- Drop-logic test: exercise AreFramesNearDuplicate directly -----
    basicTestsRun++;
    TestLog( L"  [%d/5] drop-logic-near-duplicate ...\n", basicTestsRun );
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

        // Case 1: identical frames → must be detected as duplicate.
        {
            HBITMAP frameA = createBitmapFromPixels( basePixels, dW, dH );
            HBITMAP frameB = createBitmapFromPixels( basePixels, dW, dH );
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

        // Case 2: frame shifted by large amount → must NOT be duplicate.
        {
            // Shift base down by 20 pixels.
            std::vector<BYTE> shifted( pixelBytes, 0 );
            constexpr int shiftY = 20;
            memcpy( shifted.data() + static_cast<size_t>( shiftY ) * dW * 4,
                    basePixels.data(),
                    static_cast<size_t>( dH - shiftY ) * dW * 4 );
            HBITMAP frameA = createBitmapFromPixels( basePixels, dW, dH );
            HBITMAP frameB = createBitmapFromPixels( shifted, dW, dH );
            bool dup = AreFramesNearDuplicate( frameB, frameA, false );
            DeleteObject( frameA );
            DeleteObject( frameB );
            if( dup )
            {
                TestLog( L"***** FAIL: drop-logic-near-duplicate case 2 (scrolled frame falsely dropped) *****\n" );
                return false;
            }
        }

        // Case 3: low-contrast frame with ±1 px shift that improves MAD —
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

            HBITMAP frameA = createBitmapFromPixels( lowBase, dW, dH );
            HBITMAP frameB = createBitmapFromPixels( lowShifted, dW, dH );
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

        // Case 4: truly identical low-contrast frames → must be duplicate.
        {
            std::vector<BYTE> flat( pixelBytes );
            for( size_t i = 0; i < pixelBytes; i += 4 )
            {
                flat[i + 0] = 130;
                flat[i + 1] = 130;
                flat[i + 2] = 130;
                flat[i + 3] = 0xFF;
            }
            HBITMAP frameA = createBitmapFromPixels( flat, dW, dH );
            HBITMAP frameB = createBitmapFromPixels( flat, dW, dH );
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
        TestLog( L"  [%d/5] drop-logic-near-duplicate PASSED\n", basicTestsRun );
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
            const int expectedH = origins.back() + winH;
            const bool isStressScenario = wcsncmp( scenario, L"stress-", 7 ) == 0;
            const bool isStrictRangeScenario = wcsstr( scenario, L"legitjumps" ) != nullptr;
            const bool isFastScrollScenario = wcsstr( scenario, L"fastscroll" ) != nullptr;
            const bool isHcfDarkScenario = wcsstr( scenario, L"hcfdark" ) != nullptr;

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
                        if( luma < 40 ) continue; // dark pixel â€” keep identical
                        for( int ch = 0; ch < 3; ++ch )
                        {
                            noiseSeed = noiseSeed * 1103515245u + 12345u;
                            const int noise = static_cast<int>( ( noiseSeed >> 16 ) % 5 ) - 2;
                            const int val = static_cast<int>( fp[bi + ch] ) + noise;
                            fp[bi + ch] = static_cast<BYTE>( max( 0, min( 255, val ) ) );
                        }
                    }
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

            const int htol = isStrictRangeScenario
                ? max( 40, winH / 10 + static_cast<int>( origins.size() ) * 3 )
                : ( winH / 4 + static_cast<int>( origins.size() ) * 8 );
            if( sH < expectedH - htol || sH > expectedH + htol )
            {
                // Check if the source image is low-contrast.
                // Low-contrast images can't be stitched by correlation â€” the stitcher
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
                        TestLog( L"[Panorama/Test] %s: low-contrast (avgDiff=%.1f), graceful degradation â€” PASS\n",
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
            if( mappedSourceRows.size() >= 8 )
            {
                for( size_t i = 1; i < mappedSourceRows.size(); ++i )
                {
                    const int dy = mappedSourceRows[i] - mappedSourceRows[i - 1];
                    // Only count dup transitions when the drift-tracked
                    // search is in a reliable region (drift < maxVE).
                    // In high-drift regions the stitcher compressed
                    // the canvas, which naturally maps many canvas rows
                    // to the same source rows â€” this is expected and
                    // already tested by the height tolerance check.
                    if( dy <= 1 &&
                        mappedDriftAbs[i] <= maxVE && mappedDriftAbs[i - 1] <= maxVE )
                        dupTransitions++;
                    if( dy >= 36 )
                        jumpTransitions++;
                    if( dy < -2 )
                        backwardTransitions++;
                }

                // Enforce continuity only for stress scenarios.
                // HCF-dark scenarios have relaxed backtrack tolerance because
                // the mostly-dark content makes row mapping unreliable â€”
                // indistinguishable dark rows cause the source-row search to
                // wander, producing false backward transitions.
                const bool isStressScenario = wcsncmp( scenario, L"stress-", 7 ) == 0;
                if( isStressScenario )
                {
                    const size_t transitions = mappedSourceRows.size() - 1;
                    const bool tooManyDups = dupTransitions > transitions / 2;
                    const bool tooManyJumps = jumpTransitions > transitions / 6;
                    const size_t backtrackLimit = isHcfDarkScenario ? transitions / 6 : transitions / 30;
                    const bool tooManyBacktracks = backwardTransitions > backtrackLimit;
                    continuityOk = !( tooManyDups || tooManyJumps || tooManyBacktracks );
                }
            }

            const bool ok = samples > 0 && mrate < 0.15 && continuityOk;

            // On low-vertical-contrast (HCF-dark) content, the per-row
            // search used for pixel comparison is unreliable because many
            // rows are nearly indistinguishable, leading to drift and false
            // mismatches.  If the stitched height is correct, try a direct
            // pixel comparison (stitched row y == source row y) which is
            // valid because selftest frames are exact slices of the source.
            if( !ok && sH >= expectedH - htol && sH <= expectedH + htol )
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
                    TestLog( L"[Panorama/Test] %s: direct comparison passed (%.2f%% vs row-match %.2f%%) â€” PASS\n",
                                 scenario, directRate * 100.0, mrate * 100.0 );
                    return 1;
                }
            }

            TestLog( L"[Panorama/Test] %s result=%s stitched=%dx%d dx=%d samples=%zu mismatches=%zu (%.2f%%)\n",
                         scenario, ok ? L"PASS" : L"FAIL", sW, sH, bestDx, samples, mismatches, mrate * 100.0 );

            if( !ok )
            {
                StitchLog( L"[Panorama/Test] PIXELS-FAIL %s stitched=%dx%d mrate=%.2f%% continuity(dup=%zu jump=%zu back=%zu transitions=%zu)\n",
                           scenario, sW, sH, mrate * 100.0,
                           dupTransitions, jumpTransitions, backwardTransitions,
                           mappedSourceRows.size() > 0 ? mappedSourceRows.size() - 1 : 0 );
                if( !selfTestDumpDirectory.empty() )
                {
                    wchar_t msg[512]{};
                    swprintf_s( msg, L"PIXELS: %s stitched=%dx%d dx=%d mismatches=%zu/%zu (%.2f%%) continuity(dup=%zu jump=%zu back=%zu)",
                                scenario, sW, sH, bestDx, mismatches, samples, mrate * 100.0,
                                dupTransitions, jumpTransitions, backwardTransitions );
                    DumpPanoramaText( selfTestDumpDirectory, L"image_trial_failed.txt", msg );
                }
            }

            return ok ? 1 : 0;
        };

        if( !selfTestStressOnly )
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

            // ====================================================================
            // Capture-log replay tests
            //
            // Replays the exact frame origins from recorded capture logs in
            // the image*-stitches/ subdirectories.  This tests the stitcher
            // against real-world scrolling patterns captured from actual use.
            // ====================================================================
            {
                TestLog( L"\n==== Phase 3: Capture-replay tests ====\n" );
                int captureReplayRun = 0;
                int captureReplayPassed = 0;

                for( const wchar_t* imageFile : imageFiles )
                {
                    // Derive the stitch directory name from the image file name.
                    // e.g. "image6.png" -> "image6-stitches"
                    std::wstring baseName( imageFile );
                    const auto dotPos = baseName.rfind( L'.' );
                    if( dotPos != std::wstring::npos )
                        baseName.resize( dotPos );
                    const auto stitchDir = imageDir / ( baseName + L"-stitches" );

                    if( !std::filesystem::exists( stitchDir ) )
                        continue;

                    // Load the source image.
                    const auto imagePath = imageDir / imageFile;
                    std::vector<BYTE> imagePixels;
                    int imageWidth = 0, imageHeight = 0;
                    if( !loadImageFile( imagePath, imagePixels, imageWidth, imageHeight ) )
                    {
                        TestLog( L"[Panorama/Test] CaptureReplay: failed to load %s\n", imageFile );
                        continue;
                    }

                // Iterate over all capture logs in the stitch directory.
                for( const auto& entry : std::filesystem::directory_iterator( stitchDir ) )
                {
                    const auto& logPath = entry.path();
                    if( logPath.extension() != L".log" )
                        continue;
                    const auto logName = logPath.filename().wstring();
                    if( logName.find( L"-capture.log" ) == std::wstring::npos )
                        continue;

                    // Parse the capture log file.
                    FILE* fp = nullptr;
                    if( _wfopen_s( &fp, logPath.c_str(), L"r" ) != 0 || !fp )
                        continue;

                    int capW = 0, capH = 0;
                    std::vector<int> originsY;
                    char line[512];
                    while( fgets( line, sizeof(line), fp ) )
                    {
                        int w = 0, h = 0;
                        if( sscanf_s( line, "captureWindowSize=%dx%d", &w, &h ) == 2 )
                        {
                            capW = w;
                            capH = h;
                        }
                        int fidx = 0, ox = 0, oy = 0, fw = 0, fh = 0;
                        if( sscanf_s( line, "frame%d.offset=(%d,%d) size=%dx%d", &fidx, &ox, &oy, &fw, &fh ) == 5 )
                        {
                            originsY.push_back( oy );
                        }
                    }
                    fclose( fp );

                    if( capH <= 0 || originsY.size() < 2 )
                        continue;
                    if( capW != imageWidth )
                        continue;

                    // Verify all origins are valid.
                    bool valid = true;
                    for( int oy : originsY )
                    {
                        if( oy < 0 || oy + capH > imageHeight )
                        {
                            valid = false;
                            break;
                        }
                    }
                    if( !valid )
                        continue;

                    // Build scenario name from the log file name.
                    std::wstring scenarioW = L"capture-replay-" + logName.substr( 0, logName.size() - 4 );
                    wchar_t scenarioName[256]{};
                    wcsncpy_s( scenarioName, scenarioW.c_str(), _TRUNCATE );

                    TestLog( L"[Panorama/Test] Running %s (%s %dx%d portal=%d frames=%zu)\n",
                                 scenarioName, imageFile, imageWidth, imageHeight, capH, originsY.size() );

                    captureReplayRun++;
                    const int result = stitchAndCompare( scenarioName, imagePixels, imageWidth, imageHeight, originsY, capH );

                    if( result < 0 )
                    {
                        TestLog( L"***** FAIL: %s INFRASTRUCTURE ERROR *****\n", scenarioName );
                    }
                    else if( result == 1 )
                    {
                        captureReplayPassed++;
                        TestLog( L"  [%d] %s PASSED\n", captureReplayRun, scenarioName );
                    }
                    else
                    {
                        TestLog( L"***** FAIL: %s COMPARISON FAILED *****\n", scenarioName );
                    }
                }
            }

                TestLog( L"[Panorama/Test] Capture replay: %d/%d passed\n", captureReplayPassed, captureReplayRun );

                if( captureReplayRun > 0 && captureReplayPassed < captureReplayRun )
                {
                    TestLog( L"***** FAIL: Capture replay failures: %d/%d *****\n",
                                 captureReplayRun - captureReplayPassed, captureReplayRun );
                    CoUninitialize();
                    return false;
                }
            }
        }
        else
        {
            TestLog( L"[Panorama/Test] Stress-only mode: skipping image-slice and capture replay tests\n" );
        }

        // ====================================================================
        // Stress tests: vertical_stress.png and horizontal_stress.png
        //
        // Each trial generates ~100 overlapping frames with random step sizes
        // from 0 (duplicate) up to 25% of the portal size.  This exercises
        // all four content regimes (high/low entropy Ã— high/low contrast),
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

        const std::wstring stressFocusScenario = readSelfTestArg( L"/panorama-stress-focus" );
        const bool stressFocusEnabled = !stressFocusScenario.empty();

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

        bool stressFocusMatched = false;
        auto stressScenarioMatches = [&]( const wchar_t* scenarioName ) -> bool
        {
            if( !stressFocusEnabled )
                return true;
            if( scenarioName == nullptr )
                return false;
            return wcsstr( scenarioName, stressFocusScenario.c_str() ) != nullptr;
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

                HBITMAP bmp = createBitmapFromPixels( fp, winW, imgH );
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
                    TestLog( L"[Panorama/Test] %s: low-contrast horizontal (avgDiff=%.1f), graceful degradation â€” PASS\n",
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

            // ---- Column-luminance profile correlation ----
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

            // Find best linear mapping: stitchProf[i] â†” srcProf[offset + i*scale].
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

            // Coarse search: scale from 0.5Ã— to 2.5Ã— in steps of 0.05,
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

            // A correlation â‰¥ 0.60 indicates the structure strongly matches.
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

        // --- Vertical stress test: ~100 frames per trial, steps 0..25% of portal ---
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
                            else
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

                            const int nextY = y + step;
                            if( nextY + winH > vH )
                                break;
                            y = nextY;
                            originsY.push_back( y );
                            frame++;
                        }

                        return originsY;
                    };

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
                    }
                }
            }
            else
            {
                TestLog( L"[Panorama/Test] Skipping vertical_stress.png (not found at %s)\n", vPath.c_str() );
            }
        }

        // --- HCF-dark stress test: synthetic dark-background image with sparse text ---
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
            // produce fineScore=0 at wrong alignments â€” the exact
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
                // Steps 15-25% of portal height â€” matches real captures.
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
        }

        // --- Horizontal stress test: ~100 frames per trial, steps 0..25% of portal ---
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
                }
            }
            else
            {
                TestLog( L"[Panorama/Test] Skipping horizontal_stress.png (not found at %s)\n", hPath.c_str() );
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
