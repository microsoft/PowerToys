//============================================================================
//
// PanoramaCapture.h
//
// Panorama (scrolling) screen capture and stitching.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================
#pragma once

#include <windows.h>
#include <vector>

// Globals shared with the main ZoomIt module.
extern bool    g_PanoramaCaptureActive;
extern bool    g_PanoramaStopRequested;

// Run the panorama capture flow: select a region, capture frames while
// scrolling, stitch them together, and copy the result to the clipboard.
bool RunPanoramaCaptureToClipboard( HWND hWnd );

// Run a synthetic, non-interactive self-test for panorama frame stitching.
// Returns true when stitching output matches expected dimensions/content.
bool RunPanoramaStitchSelfTest();

// Re-stitch frames from a specific debug dump directory.
bool RunPanoramaStitchDumpDirectory( const wchar_t* path );

// Re-stitch accepted panorama frames from the latest debug dump session and
// save output into that same session directory.
bool RunPanoramaStitchLatestDebugDump();
