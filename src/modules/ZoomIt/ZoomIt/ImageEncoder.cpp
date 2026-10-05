//============================================================================
//
// ImageEncoder.cpp
//
// Implements SaveImage(), dispatching to a GDI+ PNG/JPEG encoder or to a
// libwebp-based lossless WebP encoder. All image encoding is confined to
// this file.
//
//============================================================================
#include "ImageEncoder.h"

#include <cstdlib>
#include <vector>
#include <gdiplus.h>
#include <webp/encode.h>

//----------------------------------------------------------------------------
//
// GetEncoderClsid
//
// Looks up the GDI+ encoder CLSID for the given MIME type.
//
//----------------------------------------------------------------------------
static int GetEncoderClsid( const WCHAR* format, CLSID* pClsid )
{
    using namespace Gdiplus;

    UINT num = 0;          // number of image encoders
    UINT size = 0;         // size of the image encoder array in bytes

    ImageCodecInfo* pImageCodecInfo = NULL;

    GetImageEncodersSize( &num, &size );
    if( size == 0 )
        return -1;  // Failure

    pImageCodecInfo = static_cast<ImageCodecInfo*>( malloc( size ) );
    if( pImageCodecInfo == NULL )
        return -1;  // Failure

    GetImageEncoders( num, size, pImageCodecInfo );

    for( UINT j = 0; j < num; ++j )
    {
        if( wcscmp( pImageCodecInfo[j].MimeType, format ) == 0 )
        {
            *pClsid = pImageCodecInfo[j].Clsid;
            free( pImageCodecInfo );
            return j;  // Success
        }
    }

    free( pImageCodecInfo );
    return -1;  // Failure
}

//----------------------------------------------------------------------------
//
// SavePng
//
// Use gdi+ to save a PNG.
//
//----------------------------------------------------------------------------
static DWORD SavePng( LPCTSTR Filename, HBITMAP hBitmap )
{
    Gdiplus::Bitmap     bitmap( hBitmap, NULL );
    CLSID pngClsid;
    GetEncoderClsid( L"image/png", &pngClsid );
    if( bitmap.Save( Filename, &pngClsid, NULL ) ) {

        return GetLastError();
    }
    return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
//
// SaveJpeg
//
// Use gdi+ to save a JPEG. JPEG is lossy and has no alpha channel, so the
// (opaque) screenshot is encoded as RGB at a high quality setting to keep
// text and edges crisp.
//
//----------------------------------------------------------------------------
static DWORD SaveJpeg( LPCTSTR Filename, HBITMAP hBitmap )
{
    Gdiplus::Bitmap     bitmap( hBitmap, NULL );
    CLSID jpegClsid;
    GetEncoderClsid( L"image/jpeg", &jpegClsid );

    ULONG quality = 90;
    Gdiplus::EncoderParameters encoderParams;
    encoderParams.Count = 1;
    encoderParams.Parameter[0].Guid = Gdiplus::EncoderQuality;
    encoderParams.Parameter[0].Type = Gdiplus::EncoderParameterValueTypeLong;
    encoderParams.Parameter[0].NumberOfValues = 1;
    encoderParams.Parameter[0].Value = &quality;

    if( bitmap.Save( Filename, &jpegClsid, &encoderParams ) ) {

        return GetLastError();
    }
    return ERROR_SUCCESS;
}

//----------------------------------------------------------------------------
//
// SaveWebp
//
// Encodes an HBITMAP to a lossless WebP file. The bitmap is read as a
// top-down 32bpp BGRA DIB and handed to libwebp's lossless encoder.
//
//----------------------------------------------------------------------------
static DWORD SaveWebp( LPCTSTR Filename, HBITMAP hBitmap )
{
    BITMAP bm{};
    if( GetObject( hBitmap, sizeof( bm ), &bm ) == 0 )
    {
        return ERROR_INVALID_HANDLE;
    }

    const int width = bm.bmWidth;
    const int height = bm.bmHeight;
    if( width <= 0 || height <= 0 )
    {
        return ERROR_INVALID_DATA;
    }

    // Ask GDI for a top-down (negative height) 32bpp BGRA copy of the bitmap.
    BITMAPINFO bmi{};
    bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
    bmi.bmiHeader.biWidth = width;
    bmi.bmiHeader.biHeight = -height;
    bmi.bmiHeader.biPlanes = 1;
    bmi.bmiHeader.biBitCount = 32;
    bmi.bmiHeader.biCompression = BI_RGB;

    const int stride = width * 4;
    std::vector<BYTE> pixels( static_cast<size_t>( stride ) * static_cast<size_t>( height ) );

    HDC hdc = GetDC( nullptr );
    if( hdc == nullptr )
    {
        return ERROR_INVALID_HANDLE;
    }
    const int scanLines = GetDIBits( hdc, hBitmap, 0, static_cast<UINT>( height ),
                                     pixels.data(), &bmi, DIB_RGB_COLORS );
    ReleaseDC( nullptr, hdc );
    if( scanLines == 0 )
    {
        return ERROR_INVALID_DATA;
    }

    // GDI bitmaps generally carry an undefined/zero alpha channel. Force fully
    // opaque so the lossless WebP matches the visible (opaque) screenshot
    // instead of becoming transparent.
    for( size_t i = 3; i < pixels.size(); i += 4 )
    {
        pixels[i] = 0xFF;
    }

    uint8_t* output = nullptr;
    const size_t outputSize = WebPEncodeLosslessBGRA( pixels.data(), width, height,
                                                      stride, &output );
    if( outputSize == 0 || output == nullptr )
    {
        return ERROR_GEN_FAILURE;
    }

    DWORD status = ERROR_SUCCESS;
    HANDLE hFile = CreateFile( Filename, GENERIC_WRITE, 0, nullptr,
                               CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr );
    if( hFile == INVALID_HANDLE_VALUE )
    {
        status = GetLastError();
    }
    else
    {
        DWORD bytesWritten = 0;
        if( !WriteFile( hFile, output, static_cast<DWORD>( outputSize ),
                        &bytesWritten, nullptr ) ||
            bytesWritten != outputSize )
        {
            status = GetLastError();
            if( status == ERROR_SUCCESS )
            {
                status = ERROR_WRITE_FAULT;
            }
        }
        CloseHandle( hFile );
    }

    WebPFree( output );
    return status;
}

//----------------------------------------------------------------------------
//
// SaveImage
//
//----------------------------------------------------------------------------
DWORD SaveImage( LPCTSTR Filename, HBITMAP hBitmap, ImageFormat format )
{
    switch( format )
    {
    case ImageFormat::Webp:
        return SaveWebp( Filename, hBitmap );

    case ImageFormat::Jpeg:
        return SaveJpeg( Filename, hBitmap );

    case ImageFormat::Png:
    default:
        return SavePng( Filename, hBitmap );
    }
}
