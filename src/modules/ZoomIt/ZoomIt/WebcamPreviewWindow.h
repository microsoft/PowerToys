//==============================================================================
//
// WebcamPreviewWindow.h
//
// Shows a live on-screen preview of the webcam overlay while recording.
// The window is marked WDA_EXCLUDEFROMCAPTURE so it never appears in the
// recorded video.  It reads pre-scaled BGRA pixels from WebcamCapture
// via GetLatestPixels() and blits them with SetDIBitsToDevice.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#pragma once

#include "WebcamCapture.h"
#include <vector>

class WebcamPreviewWindow
{
public:
    WebcamPreviewWindow() = default;
    ~WebcamPreviewWindow();

    // Create and show the preview window.
    //   pCapture     – the active webcam capture (for GetLatestPixels).
    //   screenRect   – the recording region in screen coordinates.
    //                  For full-screen, pass the monitor rect.
    //   outputWidth  – the recording output width (after crop+scale).
    //   outputHeight – the recording output height (after crop+scale).
    bool Create( WebcamCapture* pCapture,
                 RECT screenRect,
                 UINT outputWidth,
                 UINT outputHeight );

    // Destroy the preview window and stop the refresh timer.
    void Destroy();

    // Hide/show the preview window without destroying it,
    // preserving the user's last position and size.
    void Hide();
    void Show();

    // Returns true if the preview window is active.
    bool IsActive() const { return m_hwnd != nullptr; }

    // Return the HWND so the live zoom timer can keep us above it.
    HWND GetHwnd() const { return m_hwnd; }

    // True when the user is actively dragging or resizing the preview.
    // Callers (e.g. the live zoom timer) should skip z-order manipulation
    // while this returns true to avoid disrupting SetCapture.
    bool IsInteracting() const { return m_dragging || m_resizing; }

private:
    // Edge flags for resize hit-testing (combinable for corners).
    enum ResizeEdge : UINT
    {
        EdgeNone   = 0,
        EdgeLeft   = 1,
        EdgeTop    = 2,
        EdgeRight  = 4,
        EdgeBottom = 8,
    };

    static LRESULT CALLBACK WndProc( HWND, UINT, WPARAM, LPARAM );
    void OnPaint();
    void OnTimer();
    RECT ComputeScreenRect() const;
    void OnLButtonDown( int x, int y );
    void OnMouseMove( int x, int y );
    void OnLButtonUp();
    void SyncOverlayPosition();  // Push preview position/size to WebcamCapture

    UINT  HitTestEdge( int x, int y ) const;
    LPCTSTR CursorForEdge( UINT edge ) const;
    static void ForceEdgeAlpha( void* pBits, int width, int height, int grab,
                                WebcamCapture::Shape shape );

    static constexpr UINT_PTR TIMER_ID = 1;
    static constexpr UINT     TIMER_MS = 33;   // ~30 fps refresh
    static constexpr int      EDGE_GRAB = 10;  // pixels from edge for resize grab
    static constexpr int      MIN_SIZE  = 40;  // minimum window dimension in pixels

    HWND             m_hwnd = nullptr;
    WebcamCapture*   m_capture = nullptr;
    RECT             m_screenRect = {};       // recording region on screen
    UINT             m_outputWidth = 0;
    UINT             m_outputHeight = 0;

    // Latest BGRA pixel buffer for painting.
    std::vector<BYTE> m_pixels;
    UINT              m_pixW = 0;
    UINT              m_pixH = 0;

    // Drag state.
    bool              m_dragging = false;
    POINT             m_dragOffset = {};      // cursor offset from window topleft

    // Resize state.
    bool              m_resizing = false;
    UINT              m_resizeEdge = EdgeNone; // which edge(s) are being dragged
    RECT              m_resizeStartRect = {};  // window rect at resize start (screen coords)
    POINT             m_resizeStartPt = {};    // cursor at resize start (screen coords)
    double            m_aspectRatio = 1.0;     // width/height aspect ratio to preserve
};
