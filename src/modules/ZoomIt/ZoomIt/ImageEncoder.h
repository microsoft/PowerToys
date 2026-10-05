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

// Saves the given bitmap to Filename in the requested format.
// Returns ERROR_SUCCESS on success, otherwise a Win32 error code.
DWORD SaveImage( LPCTSTR Filename, HBITMAP hBitmap, ImageFormat format );
