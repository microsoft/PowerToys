//============================================================================
//
// ImageEncoder.h
//
// Image-format dispatch for saving screenshots. Keeps the WebP (libwebp)
// encoder isolated from the rest of ZoomIt so callers only deal with a
// single SaveImage() entry point.
//
//============================================================================
#pragma once

#include <windows.h>

enum class ImageFormat
{
    Png,
    Webp,
    Jpeg
};

// Looks up the GDI+ encoder CLSID for the given MIME type.
int GetEncoderClsid( const WCHAR* format, CLSID* pClsid );

// Saves the given bitmap to Filename in the requested format.
// Returns ERROR_SUCCESS on success, otherwise a Win32 error code.
DWORD SaveImage( LPCTSTR Filename, HBITMAP hBitmap, ImageFormat format );
