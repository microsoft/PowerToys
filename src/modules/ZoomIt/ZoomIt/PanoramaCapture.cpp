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
//============================================================================
#include "pch.h"

#include "PanoramaCapture.h"
#include "Utility.h"

#include <filesystem>
#include <fstream>
#include <limits>
#include <vector>

// Externs from Zoomit.cpp
extern BOOL             g_RecordCropping;
extern SelectRectangle  g_SelectRectangle;
void OutputDebug(const TCHAR* format, ...);
const wchar_t* HotkeyIdToString( WPARAM hotkeyId );

static HBITMAP StitchPanoramaFrames( const std::vector<HBITMAP>& frames );

// Temporary file-based trace for stitch debugging
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

    HBITMAP hBitmap = CreateCompatibleBitmap( hdcSource, captureWidth, captureHeight );
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

    HBITMAP stitched = StitchPanoramaFrames( frames );

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

static bool ComputeAveragePixelDifference( const std::vector<BYTE>& currentPixels,
                                           const std::vector<BYTE>& previousPixels,
                                           int frameWidth,
                                           int frameHeight,
                                           unsigned __int64& avgDiff )
{
    if( currentPixels.size() != previousPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    const int stride = frameWidth * 4;
    const int marginX = max( 4, frameWidth / 40 );
    const int marginY = max( 4, frameHeight / 40 );
    const int startX = marginX;
    const int endX = frameWidth - marginX;
    const int startY = marginY;
    const int endY = frameHeight - marginY;

    if( endX <= startX || endY <= startY )
    {
        return false;
    }

    unsigned __int64 totalDiff = 0;
    unsigned __int64 samples = 0;
    for( int y = startY; y < endY; y += 6 )
    {
        const int rowOffset = y * stride;
        for( int x = startX; x < endX; x += 6 )
        {
            const int index = rowOffset + x * 4;
            totalDiff += static_cast<unsigned __int64>( abs( currentPixels[index + 0] - previousPixels[index + 0] ) );
            totalDiff += static_cast<unsigned __int64>( abs( currentPixels[index + 1] - previousPixels[index + 1] ) );
            totalDiff += static_cast<unsigned __int64>( abs( currentPixels[index + 2] - previousPixels[index + 2] ) );
            samples += 3;
        }
    }

    if( samples == 0 )
    {
        return false;
    }

    avgDiff = totalDiff / samples;
    return true;
}

static bool AreFramesNearDuplicate(HBITMAP currentFrame, HBITMAP previousFrame)
{
    std::vector<BYTE> currentPixels;
    std::vector<BYTE> previousPixels;
    int currentWidth = 0, currentHeight = 0;
    int previousWidth = 0, previousHeight = 0;
    if( !ReadBitmapPixels32( currentFrame, currentPixels, currentWidth, currentHeight ) ||
        !ReadBitmapPixels32( previousFrame, previousPixels, previousWidth, previousHeight ) )
    {
        return false;
    }

    if( currentWidth != previousWidth || currentHeight != previousHeight )
    {
        return false;
    }

    unsigned __int64 avgDiff = 0;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight, avgDiff ) )
    {
        return false;
    }
    return avgDiff < 6;
}

static bool AreFramesVisuallyStable( HBITMAP currentFrame, HBITMAP previousFrame )
{
    std::vector<BYTE> currentPixels;
    std::vector<BYTE> previousPixels;
    int currentWidth = 0, currentHeight = 0;
    int previousWidth = 0, previousHeight = 0;
    if( !ReadBitmapPixels32( currentFrame, currentPixels, currentWidth, currentHeight ) ||
        !ReadBitmapPixels32( previousFrame, previousPixels, previousWidth, previousHeight ) )
    {
        return false;
    }

    if( currentWidth != previousWidth || currentHeight != previousHeight )
    {
        return false;
    }

    unsigned __int64 avgDiff = 0;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, currentWidth, currentHeight, avgDiff ) )
    {
        return false;
    }

    // Looser than duplicate detection: good enough to treat motion as settled.
    return avgDiff < 12;
}

static bool ArePixelFramesNearDuplicate( const std::vector<BYTE>& currentPixels,
                                         const std::vector<BYTE>& previousPixels,
                                         int frameWidth,
                                         int frameHeight )
{
    unsigned __int64 avgDiff = 0;
    if( !ComputeAveragePixelDifference( currentPixels, previousPixels, frameWidth, frameHeight, avgDiff ) )
    {
        return false;
    }

    return avgDiff < 6;
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

static bool EvaluateLumaShiftScore( const std::vector<BYTE>& previousLuma,
                                    const std::vector<BYTE>& currentLuma,
                                    int width,
                                    int height,
                                    int dx,
                                    int dy,
                                    int sampleStep,
                                    unsigned __int64& score )
{
    const int marginX = max( 2, width / 12 );
    const int marginY = max( 2, height / 12 );
    const int xStart = max( marginX, marginX - dx );
    const int yStart = max( marginY, marginY - dy );
    const int xEnd = min( width - marginX, width - marginX - dx );
    const int yEnd = min( height - marginY, height - marginY - dy );
    const int overlapWidth = xEnd - xStart;
    const int overlapHeight = yEnd - yStart;
    if( overlapWidth < max( 8, width / 3 ) || overlapHeight < max( 8, height / 3 ) )
    {
        return false;
    }

    unsigned __int64 totalDiff = 0;
    unsigned __int64 sampleCount = 0;
    for( int y = yStart; y < yEnd; y += sampleStep )
    {
        const int previousRow = y * width;
        const int currentRow = ( y + dy ) * width;
        for( int x = xStart; x < xEnd; x += sampleStep )
        {
            const int previousValue = previousLuma[previousRow + x];
            const int currentValue = currentLuma[currentRow + ( x + dx )];
            totalDiff += static_cast<unsigned __int64>( abs( previousValue - currentValue ) );
            sampleCount++;
        }
    }

    if( sampleCount < 200 )
    {
        return false;
    }

    score = totalDiff / sampleCount;
    return true;
}

static bool EvaluateBgraShiftScore( const std::vector<BYTE>& previousPixels,
                                    const std::vector<BYTE>& currentPixels,
                                    int frameWidth,
                                    int frameHeight,
                                    int dx,
                                    int dy,
                                    int sampleStep,
                                    unsigned __int64& score )
{
    const int marginX = max( 4, frameWidth / 16 );
    const int marginY = max( 4, frameHeight / 16 );
    const int xStart = max( marginX, marginX - dx );
    const int yStart = max( marginY, marginY - dy );
    const int xEnd = min( frameWidth - marginX, frameWidth - marginX - dx );
    const int yEnd = min( frameHeight - marginY, frameHeight - marginY - dy );
    const int overlapWidth = xEnd - xStart;
    const int overlapHeight = yEnd - yStart;
    if( overlapWidth < max( 24, frameWidth / 3 ) || overlapHeight < max( 24, frameHeight / 3 ) )
    {
        return false;
    }

    const int stride = frameWidth * 4;
    unsigned __int64 totalDiff = 0;
    unsigned __int64 sampleCount = 0;
    for( int y = yStart; y < yEnd; y += sampleStep )
    {
        const int previousRow = y * stride;
        const int currentRow = ( y + dy ) * stride;
        for( int x = xStart; x < xEnd; x += sampleStep )
        {
            const int previousIndex = previousRow + x * 4;
            const int currentIndex = currentRow + ( x + dx ) * 4;
            const int previousLuma = ( previousPixels[previousIndex + 2] * 77 +
                                       previousPixels[previousIndex + 1] * 150 +
                                       previousPixels[previousIndex + 0] * 29 ) >> 8;
            const int currentLuma = ( currentPixels[currentIndex + 2] * 77 +
                                      currentPixels[currentIndex + 1] * 150 +
                                      currentPixels[currentIndex + 0] * 29 ) >> 8;
            totalDiff += static_cast<unsigned __int64>( abs( previousLuma - currentLuma ) );
            sampleCount++;
        }
    }

    if( sampleCount < 250 )
    {
        return false;
    }

    score = totalDiff / sampleCount;
    return true;
}

static int EstimateVerticalStepFromOverlap( const std::vector<BYTE>& previousPixels,
                                            const std::vector<BYTE>& currentPixels,
                                            int frameWidth,
                                            int frameHeight,
                                            int predictedStepY )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 || predictedStepY == 0 )
    {
        return predictedStepY;
    }

    const int sign = ( predictedStepY > 0 ) ? 1 : -1;
    const int predictedAbs = abs( predictedStepY );
    const int minStep = max( 6, predictedAbs - max( 12, predictedAbs / 3 ) );
    const int maxStep = min( frameHeight - 6, predictedAbs + max( 12, predictedAbs / 3 ) );
    if( minStep >= maxStep )
    {
        return predictedStepY;
    }

    const int rowStride = frameWidth * 4;
    unsigned __int64 bestScore = (std::numeric_limits<unsigned __int64>::max)();
    int bestAbsStep = predictedAbs;

    for( int stepAbs = minStep; stepAbs <= maxStep; ++stepAbs )
    {
        const int overlap = frameHeight - stepAbs;
        if( overlap < frameHeight / 3 )
        {
            continue;
        }

        unsigned __int64 totalDiff = 0;
        unsigned __int64 sampleCount = 0;
        for( int y = 0; y < overlap; y += 2 )
        {
            const int previousY = ( sign > 0 ) ? ( y + stepAbs ) : y;
            const int currentY = ( sign > 0 ) ? y : ( y + stepAbs );
            const int previousRow = previousY * rowStride;
            const int currentRow = currentY * rowStride;
            for( int x = 0; x < frameWidth; x += 8 )
            {
                const int previousIndex = previousRow + x * 4;
                const int currentIndex = currentRow + x * 4;
                const int previousLuma = ( previousPixels[previousIndex + 2] * 77 +
                                           previousPixels[previousIndex + 1] * 150 +
                                           previousPixels[previousIndex + 0] * 29 ) >> 8;
                const int currentLuma = ( currentPixels[currentIndex + 2] * 77 +
                                          currentPixels[currentIndex + 1] * 150 +
                                          currentPixels[currentIndex + 0] * 29 ) >> 8;
                totalDiff += static_cast<unsigned __int64>( abs( previousLuma - currentLuma ) );
                sampleCount++;
            }
        }

        if( sampleCount == 0 )
        {
            continue;
        }

        const unsigned __int64 score = totalDiff / sampleCount;
        if( score < bestScore )
        {
            bestScore = score;
            bestAbsStep = stepAbs;
        }
    }

    return sign * bestAbsStep;
}

static bool FindBestFrameShift( const std::vector<BYTE>& previousPixels,
                                const std::vector<BYTE>& currentPixels,
                                int frameWidth,
                                int frameHeight,
                                int expectedDx,
                                int expectedDy,
                                int& bestDx,
                                int& bestDy )
{
    if( previousPixels.size() != currentPixels.size() || frameWidth <= 0 || frameHeight <= 0 )
    {
        return false;
    }

    // ── Phase 1 ── Windowed coarse search on downsampled luma ─────────
    // Search a LIMITED range around the expected shift to avoid harmonic
    // matches on repetitive content.  For the first frame pair
    // (expectedDy == 0) search outward from the smallest step.
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

    const int minStepDs = max( 2, dsH / 30 );
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
    if( stationaryScore <= 2 )
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
    CoarseCandidate candidates[kMaxCandidates];
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

            for( int y = 0; y < overlap; ++y )
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
            }

            if( samples < 100 )
            {
                continue;
            }

            unsigned __int64 score = totalDiff / samples;

            if( expectedDyDs == 0 )
            {
                // First frame: add a mild step-size penalty so among
                // ties the smallest step wins.
                score = score * 4 + static_cast<unsigned __int64>( absStep );
            }

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

    // ── Phase 2 ── Rank candidates by full-resolution comparison ──────
    // For each coarse candidate, compute a fine score at full resolution.
    // This resolves ambiguity from harmonic matches on repetitive content
    // since the full-resolution comparison sees fine text details that
    // the downsampled comparison misses.
    const int refineRadiusDy = max( 3, downsampleScale + 1 );
    const int refineRadiusDx = 1;

    unsigned __int64 bestFineScore = ( std::numeric_limits<unsigned __int64>::max )();
    bestDx = 0;
    bestDy = candidates[0].dyDs * downsampleScale;
    int bestCoarseDy = candidates[0].dyDs;

    const int stride = frameWidth * 4;
    const int fineMarginX = max( 4, frameWidth / 20 );

    for( int ci = 0; ci < candidateCount; ++ci )
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

                for( int y = 0; y < overlap; y += 2 )
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

                    const int prevRow = pY * stride;
                    const int currRow = cY * stride;

                    for( int x = xStart; x < xEnd; x += 2 )
                    {
                        const int prevIdx = prevRow + x * 4;
                        const int currIdx = currRow + ( x + dx ) * 4;
                        const int pLuma = ( previousPixels[prevIdx + 2] * 77 +
                                            previousPixels[prevIdx + 1] * 150 +
                                            previousPixels[prevIdx + 0] * 29 ) >> 8;
                        const int cLuma = ( currentPixels[currIdx + 2] * 77 +
                                            currentPixels[currIdx + 1] * 150 +
                                            currentPixels[currIdx + 0] * 29 ) >> 8;
                        totalDiff += static_cast<unsigned __int64>( abs( pLuma - cLuma ) );
                        samples++;
                    }
                }

                if( samples < 100 )
                {
                    continue;
                }

                const unsigned __int64 score = totalDiff / samples;
                if( score < bestFineScore )
                {
                    bestFineScore = score;
                    bestDx = dx;
                    bestDy = dy;
                    bestCoarseDy = candidates[ci].dyDs;
                }
            }
        }
    }

    if( bestFineScore == ( std::numeric_limits<unsigned __int64>::max )() || bestFineScore > 10 )
    {
        StitchLog( L"[Panorama/Stitch] FindBestFrameShift poor-fine expected=(%d,%d) best=(%d,%d) fineScore=%llu\n",
                     expectedDx, expectedDy, bestDx, bestDy,
                     static_cast<unsigned long long>( bestFineScore ) );
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

static HBITMAP StitchPanoramaFrames(const std::vector<HBITMAP>& frames)
{
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

    const int minProgress = max( 8, frameHeight / 30 );
    int expectedDx = 0;
    int expectedDy = 0;

    int minX = 0;
    int minY = 0;
    int maxX = frameWidth;
    int maxY = frameHeight;

    for( size_t i = 1; i < frames.size(); i++ )
    {
        int dx = expectedDx;
        int dy = expectedDy;
        bool foundShift = FindBestFrameShift( framePixels[composedFrameIndices.back()], framePixels[i], frameWidth, frameHeight, expectedDx, expectedDy, dx, dy );
        if( !foundShift )
        {
            if( ArePixelFramesNearDuplicate( framePixels[composedFrameIndices.back()], framePixels[i], frameWidth, frameHeight ) )
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
    return stitchedBitmap;
}

bool RunPanoramaCaptureToClipboard( HWND hWnd )
{
    OutputDebug( L"[Panorama/Capture] Start\n" );
    const std::wstring debugDumpDirectory = CreatePanoramaDebugDumpDirectory();
    size_t debugGrabbedFrameCount = 0;
    if( !debugDumpDirectory.empty() )
    {
        OutputDebug( L"[Panorama/Debug] Dump directory: %s\n", debugDumpDirectory.c_str() );
    }

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
    OutputDebug( L"[Panorama/Capture] Capture rect absolute=(%ld,%ld)-(%ld,%ld)\n",
                 absoluteRect.left,
                 absoluteRect.top,
                 absoluteRect.right,
                 absoluteRect.bottom );

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

    wil::unique_hdc hdcSource( CreateDC( L"DISPLAY", static_cast<PTCHAR>(nullptr), static_cast<PTCHAR>(nullptr), static_cast<CONST DEVMODE*>(nullptr) ) );
    if( hdcSource == nullptr )
    {
        OutputDebug( L"[Panorama/Capture] CreateDC failed\n" );
        g_SelectRectangle.Stop();
        return false;
    }

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

    std::vector<HBITMAP> frames;
    HBITMAP firstFrame = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), absoluteRect );
    if( firstFrame == nullptr )
    {
        OutputDebug( L"[Panorama/Capture] Failed to capture first frame\n" );
        g_SelectRectangle.Stop();
        return false;
    }
    frames.push_back( firstFrame );
    OutputDebug( L"[Panorama/Capture] Captured frame #1\n" );
    DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, firstFrame );

    size_t duplicateFrameCount = 0;
    size_t unstableFrameCount = 0;
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

        Sleep( 60 );
        HBITMAP frame = CaptureAbsoluteScreenRectToBitmap( hdcSource.get(), absoluteRect );
        if( frame == nullptr )
        {
            OutputDebug( L"[Panorama/Capture] Capture failed at iteration=%zu\n", captureIteration );
            continue;
        }

        DumpPanoramaBitmap( debugDumpDirectory, L"grabbed", ++debugGrabbedFrameCount, frame );

        if( AreFramesNearDuplicate( frame, frames.back() ) )
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
        if( frames.size() >= 128 )
        {
            OutputDebug( L"[Panorama/Capture] Reached frame limit (128), stopping capture\n" );
            break;
        }
    }

    OutputDebug( L"[Panorama/Capture] Loop exited stopRequested=%d frames=%zu duplicates=%zu unstable=%zu iterations=%zu\n",
                 g_PanoramaStopRequested ? 1 : 0,
                 frames.size(),
                 duplicateFrameCount,
                 unstableFrameCount,
                 captureIteration );

    if( !debugDumpDirectory.empty() )
    {
        wchar_t statsText[256]{};
        swprintf_s( statsText,
                    L"framesAccepted=%zu\nduplicates=%zu\nunstable=%zu\niterations=%zu\nstopRequested=%d\n",
                    frames.size(),
                    duplicateFrameCount,
                    unstableFrameCount,
                    captureIteration,
                    g_PanoramaStopRequested ? 1 : 0 );
        DumpPanoramaText( debugDumpDirectory, L"capture_stats.txt", statsText );

        for( size_t frameIndex = 0; frameIndex < frames.size(); ++frameIndex )
        {
            DumpPanoramaBitmap( debugDumpDirectory, L"accepted", frameIndex + 1, frames[frameIndex] );
        }
    }

    g_SelectRectangle.Stop();

    HBITMAP panoramaBitmap = nullptr;
    if( frames.size() == 1 )
    {
        panoramaBitmap = frames.front();
        frames.front() = nullptr;
    }
    else
    {
        panoramaBitmap = StitchPanoramaFrames( frames );
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

    DumpPanoramaBitmap( debugDumpDirectory, L"stitched", 0, panoramaBitmap );

    const bool opened = OpenClipboard( hWnd ) != FALSE;
    if( !opened )
    {
        OutputDebug( L"[Panorama/Capture] OpenClipboard failed err=%lu\n", GetLastError() );
        DeleteObject( panoramaBitmap );
        return false;
    }

    if( !EmptyClipboard() )
    {
        OutputDebug( L"[Panorama/Capture] EmptyClipboard failed err=%lu\n", GetLastError() );
        CloseClipboard();
        DeleteObject( panoramaBitmap );
        return false;
    }

    if( SetClipboardData( CF_BITMAP, panoramaBitmap ) == nullptr )
    {
        OutputDebug( L"[Panorama/Capture] SetClipboardData(CF_BITMAP) failed err=%lu\n", GetLastError() );
        CloseClipboard();
        DeleteObject( panoramaBitmap );
        return false;
    }

    CloseClipboard();
    OutputDebug( L"[Panorama/Capture] Success: bitmap copied to clipboard\n" );
    return true;
}

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

        HBITMAP stitchedBitmap = StitchPanoramaFrames( frames );

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
        return passed;
    };

    {
        constexpr int frameWidth = 420;
        constexpr int frameHeight = 320;
        constexpr int stepY = 90;
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

    OutputDebug( L"[Panorama/Test] All scenarios passed\n" );
    return true;
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
