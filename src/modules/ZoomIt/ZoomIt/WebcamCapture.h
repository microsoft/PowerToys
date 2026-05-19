//==============================================================================
//
// WebcamCapture.h
//
// Captures frames from a webcam using Media Foundation's IMFSourceReader.
// The capture thread stores raw BGRA pixel buffers; compositing onto the
// recording frame happens on the caller's thread using D3D11.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#pragma once

#include <d3d11_4.h>
#include <mfapi.h>
#include <mfidl.h>
#include <mfreadwrite.h>
#include <atomic>
#include <condition_variable>
#include <mutex>
#include <thread>
#include <vector>
#include <winrt/base.h>

class WebcamCapture
{
public:
    // Position constants matching g_WebcamPosition values.
    enum Position { TopLeft = 0, TopRight = 1, BottomLeft = 2, BottomRight = 3 };

    // Size constants matching g_WebcamSize values.
    enum Size { Small = 0, Medium = 1, Large = 2, XLarge = 3, FullScreen = 4 };

    // Shape constants matching g_WebcamShape values.
    enum Shape { Square = 0, RoundedRect = 1, RoundedSquare = 2, Circle = 3 };

    WebcamCapture(
        winrt::com_ptr<ID3D11Device> const& device,
        winrt::com_ptr<ID3D11DeviceContext> const& context,
        const wchar_t* deviceSymLink,
        UINT outputWidth,
        UINT outputHeight,
        Position position,
        Size size,
        Shape shape,
        bool fullScreenRecording = false );
    ~WebcamCapture();

    // Start/stop the capture thread.
    bool Start();
    void Stop();

    // Block until the first webcam frame has been captured, or timeoutMs
    // elapses.  Returns true if a frame is ready.
    bool WaitForFirstFrame( int timeoutMs );

    // Composite the latest camera frame onto the given texture.
    // Must be called from the thread that owns m_d3dContext.
    // Returns true if a frame was composited.
    bool CompositeOnto( ID3D11Texture2D* target );

    // Copy the latest pre-scaled BGRA pixels for on-screen preview.
    // Thread-safe (acquires m_frameLock).  Returns true if pixels were copied.
    bool GetLatestPixels( std::vector<BYTE>& outPixels, UINT& outW, UINT& outH );

    // Return the destination rect (in recording-output coordinates) where the
    // webcam overlay is composited.  Valid after first frame is captured.
    RECT GetDestRect() const { return m_destRect; }

    // Update the destination rect (in recording-output coordinates).
    // Called from the preview window when the user drags the webcam overlay.
    // Thread-safe: the encoder reads m_destRect from the encoder thread.
    void SetDestRect( RECT rect ) { m_destRect = rect; }

    // Update destination rect AND overlay pixel dimensions for resize.
    // Must acquire m_frameLock because the capture thread and
    // GetLatestPixels both read m_overlayW / m_overlayH under that lock.
    void SetDestRectAndSize( RECT rect )
    {
        std::lock_guard<std::mutex> lock( m_frameLock );
        m_destRect = rect;
        m_overlayW = static_cast<UINT>( max( 1L, rect.right - rect.left ) );
        m_overlayH = static_cast<UINT>( max( 1L, rect.bottom - rect.top ) );
    }

    // Return the recording output dimensions (for screen-coordinate mapping).
    UINT GetOutputWidth()  const { return m_outputWidth; }
    UINT GetOutputHeight() const { return m_outputHeight; }

    // Return the overlay shape.
    Shape GetShape() const { return m_shape; }

    // Enable or disable compositing.  When disabled, CompositeOnto and
    // GetLatestPixels return false without doing any work.  The capture
    // thread continues running so re-enabling is instant.
    void SetEnabled( bool enabled ) { m_enabled.store( enabled, std::memory_order_relaxed ); }
    bool IsEnabled() const { return m_enabled.load( std::memory_order_relaxed ); }

    // Returns true if the capture thread failed to initialize the camera
    // (e.g. device in use by another application).
    bool HasInitFailed() const { return m_initFailed.load( std::memory_order_acquire ); }

private:
    void CaptureThread();
    bool InitSourceReader();
    RECT ComputeDestRect() const;
    void ComputeOverlayDimensions();

    winrt::com_ptr<ID3D11Device>        m_d3dDevice;
    winrt::com_ptr<ID3D11DeviceContext> m_d3dContext;
    winrt::com_ptr<IMFSourceReader>     m_sourceReader;
    std::wstring                        m_deviceSymLink;

    // Pre-scaled overlay pixels produced by the capture thread.
    // CompositeOnto consumes them and uploads to a cached GPU texture.
    std::mutex                          m_frameLock;
    std::vector<BYTE>                   m_pendingPixels;   // new frame waiting
    UINT                                m_pendingW = 0;    // dims of m_pendingPixels
    UINT                                m_pendingH = 0;
    bool                                m_newFrameReady = false;

    // Cached GPU texture (owned by CompositeOnto's thread only).
    winrt::com_ptr<ID3D11Texture2D>     m_overlayTex;
    bool                                m_hasOverlay = false;
    UINT                                m_texW = 0;
    UINT                                m_texH = 0;

    // Staging texture for alpha-blended compositing (shaped overlays).
    winrt::com_ptr<ID3D11Texture2D>     m_stagingTex;
    UINT                                m_stagingW = 0;
    UINT                                m_stagingH = 0;
    std::vector<BYTE>                   m_lastUploadedPixels;

    // Reusable frame buffer for the capture thread (avoids per-frame alloc).
    std::vector<BYTE>                   m_framePixels;
    std::vector<BYTE>                   m_scaledPixels;

    UINT                                m_overlayW = 0;
    UINT                                m_overlayH = 0;
    UINT                                m_camWidth = 0;
    UINT                                m_camHeight = 0;
    RECT                                m_destRect = {};

    // Output dimensions (recording output after crop+scale).
    UINT                                m_outputWidth = 0;
    UINT                                m_outputHeight = 0;
    Position                            m_position = BottomRight;
    Size                                m_size = Medium;
    Shape                               m_shape = Square;
    bool                                m_fullScreenRecording = false;

    // Capture thread.
    std::thread                         m_thread;
    std::atomic<bool>                   m_running{ false };
    std::atomic<bool>                   m_enabled{ true };
    std::atomic<bool>                   m_initFailed{ false };
    bool                                m_mfStarted = false;

    // Signalled once the first webcam frame has been captured so
    // Start() can block until the overlay is ready.
    std::mutex                          m_readyMutex;
    std::condition_variable             m_readyCV;
    bool                                m_firstFrameCaptured = false;

    // Debug counters for CompositeOnto logging.
    int                                 m_compositeCount = 0;
    int                                 m_lockFailCount = 0;
    int                                 m_uploadCount = 0;
};
