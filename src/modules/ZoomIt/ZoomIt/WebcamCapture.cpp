//==============================================================================
//
// WebcamCapture.cpp
//
// Captures frames from a webcam using Media Foundation's IMFSourceReader.
// Frames are delivered as D3D11 textures for GPU-side compositing.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#include "pch.h"
#include "WebcamCapture.h"

// Defined in Zoomit.cpp; compiles to nothing in Release builds.
void OutputDebug(const TCHAR* format, ...);

#pragma comment(lib, "mf.lib")
#pragma comment(lib, "mfplat.lib")
#pragma comment(lib, "mfreadwrite.lib")
#pragma comment(lib, "mfuuid.lib")

//----------------------------------------------------------------------------
// WebcamCapture::WebcamCapture
//----------------------------------------------------------------------------
WebcamCapture::WebcamCapture(
    winrt::com_ptr<ID3D11Device> const& device,
    winrt::com_ptr<ID3D11DeviceContext> const& context,
    const wchar_t* deviceSymLink,
    UINT outputWidth,
    UINT outputHeight,
    Position position,
    Size size,
    Shape shape,
    bool fullScreenRecording )
    : m_d3dDevice( device )
    , m_d3dContext( context )
    , m_deviceSymLink( deviceSymLink ? deviceSymLink : L"" )
    , m_outputWidth( outputWidth )
    , m_outputHeight( outputHeight )
    , m_position( position )
    , m_size( size )
    , m_shape( shape )
    , m_fullScreenRecording( fullScreenRecording )
{
}

//----------------------------------------------------------------------------
// WebcamCapture::~WebcamCapture
//----------------------------------------------------------------------------
WebcamCapture::~WebcamCapture()
{
    Stop();
}

//----------------------------------------------------------------------------
// WebcamCapture::InitSourceReader
//
// Opens the camera via IMFSourceReader.  Negotiates BGRA output so frames
// can be composited directly.  Uses the D3D11 device manager for
// GPU-accelerated decoding when the driver supports it.
//----------------------------------------------------------------------------
bool WebcamCapture::InitSourceReader()
{
    HRESULT hr = MFStartup( MF_VERSION );
    if( FAILED( hr ) )
    {
        OutputDebug( L"[WebcamCapture] MFStartup failed\n" );
        return false;
    }
    m_mfStarted = true;

    // Create a media source for the camera.
    winrt::com_ptr<IMFAttributes> sourceAttrs;
    hr = MFCreateAttributes( sourceAttrs.put(), 1 );
    if( FAILED( hr ) ) { OutputDebug( L"[WebcamCapture] MFCreateAttributes failed\n" ); return false; }

    sourceAttrs->SetGUID( MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE,
                          MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_GUID );

    // Enumerate video capture devices.
    IMFActivate** ppDevices = nullptr;
    UINT32 count = 0;
    hr = MFEnumDeviceSources( sourceAttrs.get(), &ppDevices, &count );
    if( FAILED( hr ) || count == 0 )
    {
        if( ppDevices ) CoTaskMemFree( ppDevices );
        OutputDebug( L"[WebcamCapture] No cameras found\n" );
        return false;
    }

    // Find matching device by symlink, or use the first one.
    UINT32 deviceIndex = 0;
    if( !m_deviceSymLink.empty() )
    {
        for( UINT32 i = 0; i < count; i++ )
        {
            WCHAR* symLink = nullptr;
            UINT32 symLinkLen = 0;
            if( SUCCEEDED( ppDevices[i]->GetAllocatedString(
                    MF_DEVSOURCE_ATTRIBUTE_SOURCE_TYPE_VIDCAP_SYMBOLIC_LINK,
                    &symLink, &symLinkLen ) ) )
            {
                if( _wcsicmp( symLink, m_deviceSymLink.c_str() ) == 0 )
                    deviceIndex = i;
                CoTaskMemFree( symLink );
            }
        }
    }

    winrt::com_ptr<IMFMediaSource> mediaSource;
    hr = ppDevices[deviceIndex]->ActivateObject( IID_PPV_ARGS( mediaSource.put() ) );
    for( UINT32 i = 0; i < count; i++ )
        ppDevices[i]->Release();
    CoTaskMemFree( ppDevices );
    if( FAILED( hr ) ) { OutputDebug( L"[WebcamCapture] ActivateObject failed\n" ); return false; }

    // Create source reader attributes (request BGRA output).
    winrt::com_ptr<IMFAttributes> readerAttrs;
    hr = MFCreateAttributes( readerAttrs.put(), 2 );
    if( FAILED( hr ) ) return false;

    // Enable the software video processor so the source reader converts
    // NV12/YUY2/MJPEG → BGRA.  Do NOT enable MF_READWRITE_ENABLE_HARDWARE_TRANSFORMS:
    // on Intel iGPU systems the hardware MFT probing loads/unloads the entire
    // GPU driver stack repeatedly (libmfxhw64 → igc64 → msg_end exception ×N),
    // blocking InitSourceReader for tens of seconds and starving the webcam
    // overlay of frames for the duration of short recordings.
    readerAttrs->SetUINT32( MF_SOURCE_READER_ENABLE_VIDEO_PROCESSING, TRUE );

    hr = MFCreateSourceReaderFromMediaSource( mediaSource.get(), readerAttrs.get(),
                                              m_sourceReader.put() );
    if( FAILED( hr ) ) { OutputDebug( L"[WebcamCapture] MFCreateSourceReaderFromMediaSource failed\n" ); return false; }

    // Set the output media type.  Try RGB32 (BGRX, no alpha) first — this
    // is the format the MF video processor most reliably converts to from
    // NV12/YUY2/MJPEG.  ARGB32 (BGRA with alpha) is not supported by many
    // cameras' video processor chains.
    //
    // Request native-size first (no explicit frame size).  The MF software
    // video processor converts color space reliably but does NOT always
    // resize: on some cameras SetCurrentMediaType(640x480) succeeds yet
    // ReadSample still delivers buffers at the camera's native resolution
    // (e.g. 1920x1080).  Since CaptureThread pre-scales every frame to
    // overlay dimensions anyway, native-size avoids the mismatch and is
    // equally fast.
    const GUID formatsToTry[] = { MFVideoFormat_RGB32, MFVideoFormat_ARGB32 };
    bool formatSet = false;

    for( const auto& subtype : formatsToTry )
    {
        // Try without specifying resolution (native-size — most reliable).
        winrt::com_ptr<IMFMediaType> outputType;
        hr = MFCreateMediaType( outputType.put() );
        if( FAILED( hr ) ) continue;
        outputType->SetGUID( MF_MT_MAJOR_TYPE, MFMediaType_Video );
        outputType->SetGUID( MF_MT_SUBTYPE, subtype );

        hr = m_sourceReader->SetCurrentMediaType(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM),
            nullptr,
            outputType.get() );
        if( SUCCEEDED( hr ) ) { formatSet = true; break; }

        // Fallback: try explicit 640x480 in case the driver requires
        // a specific frame size.
        outputType = nullptr;
        hr = MFCreateMediaType( outputType.put() );
        if( FAILED( hr ) ) continue;
        outputType->SetGUID( MF_MT_MAJOR_TYPE, MFMediaType_Video );
        outputType->SetGUID( MF_MT_SUBTYPE, subtype );
        MFSetAttributeSize( outputType.get(), MF_MT_FRAME_SIZE, 640, 480 );

        hr = m_sourceReader->SetCurrentMediaType(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM),
            nullptr,
            outputType.get() );
        if( SUCCEEDED( hr ) ) { formatSet = true; break; }
    }

    if( !formatSet )
    {
        OutputDebug( L"[WebcamCapture] SetCurrentMediaType failed for all formats\n" );
        return false;
    }

    // Read the actual negotiated frame size.
    winrt::com_ptr<IMFMediaType> actualType;
    hr = m_sourceReader->GetCurrentMediaType(
        static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM),
        actualType.put() );
    if( SUCCEEDED( hr ) )
    {
        MFGetAttributeSize( actualType.get(), MF_MT_FRAME_SIZE, &m_camWidth, &m_camHeight );
    }

    if( m_camWidth == 0 || m_camHeight == 0 )
    {
        m_camWidth = 640;
        m_camHeight = 480;
    }

    {
        OutputDebug( L"[WebcamCapture] Camera opened: %ux%u\n", m_camWidth, m_camHeight );
    }

    return true;
}

//----------------------------------------------------------------------------
// WebcamCapture::Start
//----------------------------------------------------------------------------
bool WebcamCapture::Start()
{
    m_running.store( true );
    m_thread = std::thread( &WebcamCapture::CaptureThread, this );

    // Don't block — let the capture thread warm up the camera asynchronously.
    // CompositeOnto will return false until the first frame arrives, so the
    // first few video frames won't have the webcam overlay, but recording
    // starts immediately instead of stalling ~1 s for camera warmup.
    return true;
}

//----------------------------------------------------------------------------
// WebcamCapture::WaitForFirstFrame
//----------------------------------------------------------------------------
bool WebcamCapture::WaitForFirstFrame( int timeoutMs )
{
    std::unique_lock<std::mutex> lock( m_readyMutex );
    m_readyCV.wait_for( lock, std::chrono::milliseconds( timeoutMs ),
                        [this]{ return m_firstFrameCaptured || m_initFailed.load( std::memory_order_acquire ); } );
    return m_firstFrameCaptured;
}

//----------------------------------------------------------------------------
// WebcamCapture::Stop
//----------------------------------------------------------------------------
void WebcamCapture::Stop()
{
    m_running.store( false );
    if( m_thread.joinable() )
        m_thread.join();

    {
        std::lock_guard<std::mutex> lock( m_frameLock );
        m_pendingPixels.clear();
        m_pendingW = 0;
        m_pendingH = 0;
        m_newFrameReady = false;
    }
    m_overlayTex = nullptr;
    m_hasOverlay = false;
    m_texW = 0;
    m_texH = 0;
}

//----------------------------------------------------------------------------
// WebcamCapture::CaptureThread
//
// Reads frames from the source reader.  Each frame is converted to a
// D3D11 texture and stored as the latest frame for compositing.
//----------------------------------------------------------------------------
void WebcamCapture::CaptureThread()
{
    // ALL Media Foundation work must happen on the same thread:
    // COM init, MFStartup, source reader creation, and ReadSample.
    // IMFSourceReader is not thread-safe across different apartment threads.
    CoInitializeEx( nullptr, COINIT_MULTITHREADED );

    if( !InitSourceReader() )
    {
        OutputDebug( L"[WebcamCapture] InitSourceReader failed on capture thread\n" );
        m_initFailed.store( true, std::memory_order_release );
        // Wake up WaitForFirstFrame so it doesn't block for the full timeout.
        {
            std::lock_guard<std::mutex> lock( m_readyMutex );
            m_firstFrameCaptured = false;
        }
        m_readyCV.notify_all();
        CoUninitialize();
        return;
    }

    OutputDebug( L"[WebcamCapture] Capture thread started, reading frames...\n" );
    int frameCount = 0;
#if _DEBUG
    LARGE_INTEGER perfFreq = {}, lastFrameTime = {}, loopStart = {};
    QueryPerformanceFrequency( &perfFreq );
    QueryPerformanceCounter( &lastFrameTime );
    double totalReadMs = 0, totalCopyMs = 0, totalScaleMs = 0, totalLockMs = 0;
#endif

    while( m_running.load() )
    {
        DWORD streamIndex = 0, flags = 0;
        LONGLONG timestamp = 0;
        winrt::com_ptr<IMFSample> sample;

#if _DEBUG
        QueryPerformanceCounter( &loopStart );
#endif

        HRESULT hr = m_sourceReader->ReadSample(
            static_cast<DWORD>(MF_SOURCE_READER_FIRST_VIDEO_STREAM),
            0,
            &streamIndex,
            &flags,
            &timestamp,
            sample.put() );

        if( FAILED( hr ) || ( flags & MF_SOURCE_READERF_ENDOFSTREAM ) )
        {
            OutputDebug( L"[WebcamCapture] ReadSample exit: hr=0x%08X flags=0x%X frames=%d\n",
                         static_cast<unsigned>( hr ), flags, frameCount );
            break;
        }

        if( !sample )
            continue;

#if _DEBUG
        LARGE_INTEGER afterRead;
        QueryPerformanceCounter( &afterRead );
        double readMs = static_cast<double>( afterRead.QuadPart - loopStart.QuadPart ) * 1000.0 / perfFreq.QuadPart;
        totalReadMs += readMs;
#endif

        // Get the buffer and flatten it into a known-tight BGRA buffer.
        // Use IMF2DBuffer + MFCopyImage when available so the true source
        // stride is honored. Some camera/MF pipelines expose padded or
        // otherwise non-trivial row pitch, and assuming tight packing
        // produces the striped corruption seen in the overlay.
        winrt::com_ptr<IMFMediaBuffer> buffer;
        DWORD bufferCount = 0;
        hr = sample->GetBufferCount( &bufferCount );
        if( FAILED( hr ) || bufferCount == 0 )
            continue;

        if( bufferCount == 1 )
        {
            hr = sample->GetBufferByIndex( 0, buffer.put() );
        }
        else
        {
            hr = sample->ConvertToContiguousBuffer( buffer.put() );
        }
        if( FAILED( hr ) || !buffer )
        {
            OutputDebug( L"[WebcamCapture] Failed to get sample buffer\n" );
            continue;
        }

        const UINT rowBytes = m_camWidth * 4;
        const DWORD frameBytes = static_cast<DWORD>( rowBytes * m_camHeight );
        m_framePixels.resize( frameBytes );

        bool copiedFrame = false;
        if( auto buffer2D = buffer.try_as<IMF2DBuffer>() )
        {
            BYTE* srcData = nullptr;
            LONG srcStride = 0;
            hr = buffer2D->Lock2D( &srcData, &srcStride );
            if( SUCCEEDED( hr ) && srcData != nullptr )
            {
                hr = MFCopyImage( m_framePixels.data(), rowBytes,
                                  srcData, srcStride,
                                  rowBytes, m_camHeight );
                buffer2D->Unlock2D();
                copiedFrame = SUCCEEDED( hr );
            }
        }

        if( !copiedFrame )
        {
            BYTE* data = nullptr;
            DWORD maxLen = 0, curLen = 0;
            hr = buffer->Lock( &data, &maxLen, &curLen );
            if( FAILED( hr ) || !data )
                continue;

            // Safety net: if the buffer is larger than the negotiated frame
            // size, the video processor didn't actually resize — the real
            // frame is at the camera's native resolution.  Re-derive the
            // dimensions from the buffer so we copy the correct pixels.
            if( curLen > frameBytes && curLen > 0 )
            {
                // Check common heights to find one that divides evenly
                // into 4-byte-per-pixel rows.
                const UINT heights[] = { 1080, 720, 480, 960, 1440, 2160 };
                for( UINT h : heights )
                {
                    if( curLen % ( h * 4 ) == 0 )
                    {
                        UINT w = curLen / ( h * 4 );
                        if( w >= 160 && w <= 7680 )
                        {
                            OutputDebug( L"[WebcamCapture] Buffer mismatch: "
                                        L"negotiated %ux%u (%u bytes) but buffer is "
                                        L"%u bytes — using %ux%u\n",
                                        m_camWidth, m_camHeight, frameBytes,
                                        curLen, w, h );
                            m_camWidth = w;
                            m_camHeight = h;
                            break;
                        }
                    }
                }

                const UINT newRowBytes = m_camWidth * 4;
                const DWORD newFrameBytes = static_cast<DWORD>( newRowBytes * m_camHeight );
                m_framePixels.resize( newFrameBytes );
                memcpy_s( m_framePixels.data(), newFrameBytes, data, min( curLen, newFrameBytes ) );
            }
            else
            {
                memcpy_s( m_framePixels.data(), frameBytes, data, min( curLen, frameBytes ) );
            }
            buffer->Unlock();
            copiedFrame = true;
        }

        if( !copiedFrame )
            continue;

#if _DEBUG
        LARGE_INTEGER afterCopy;
        QueryPerformanceCounter( &afterCopy );
        double copyMs = static_cast<double>( afterCopy.QuadPart - afterRead.QuadPart ) * 1000.0 / perfFreq.QuadPart;
        totalCopyMs += copyMs;
#endif

        // Use the current m_camWidth (which may have been corrected by
        // the buffer-mismatch safety net above).
        const UINT actualRowBytes = m_camWidth * 4;

        // Compute overlay dimensions if not yet done.
        if( m_overlayW == 0 )
            ComputeOverlayDimensions();

        const UINT ovW = m_overlayW;
        const UINT ovH = m_overlayH;
        if( ovW > 0 && ovH > 0 )
        {
            // Pre-scale camera frame to overlay dimensions (CPU only).
            // NO D3D calls here — the capture thread must not touch the
            // recording session's D3D device to avoid GPU contention.
            const UINT scaledStride = ovW * 4;
            const size_t scaledSize = static_cast<size_t>( scaledStride ) * ovH;
            m_scaledPixels.resize( scaledSize );

            const UINT32* srcPixels = reinterpret_cast<const UINT32*>( m_framePixels.data() );
            UINT32* dstPixels = reinterpret_cast<UINT32*>( m_scaledPixels.data() );
            const UINT srcW32 = m_camWidth;  // stride in uint32 units

            // For FullScreen mode, crop the camera feed to match the
            // output aspect ratio (crop-to-fill) so nothing is stretched.
            // For other modes the full camera frame is used.
            UINT srcCropX = 0, srcCropY = 0;
            UINT srcCropW = m_camWidth, srcCropH = m_camHeight;

            if( (m_size == FullScreen || m_shape == Circle || m_shape == RoundedSquare) && !m_fullScreenRecording && m_camWidth > 0 && m_camHeight > 0 && ovW > 0 && ovH > 0 )
            {
                // Compare aspect ratios: camera vs output.
                // Scale camera so it fills the output, then crop excess.
                double camAspect = static_cast<double>( m_camWidth ) / m_camHeight;
                double outAspect = static_cast<double>( ovW ) / ovH;

                if( camAspect > outAspect )
                {
                    // Camera is wider — crop sides.
                    srcCropH = m_camHeight;
                    srcCropW = static_cast<UINT>( m_camHeight * outAspect + 0.5 );
                    srcCropX = ( m_camWidth - srcCropW ) / 2;
                }
                else
                {
                    // Camera is taller — crop top/bottom.
                    srcCropW = m_camWidth;
                    srcCropH = static_cast<UINT>( m_camWidth / outAspect + 0.5 );
                    srcCropY = ( m_camHeight - srcCropH ) / 2;
                }
            }

            // Pre-compute shape mask parameters.
            const float halfW = ovW * 0.5f;
            const float halfH = ovH * 0.5f;
            // Rounded-rect corner radius: 10% of the smaller dimension.
            // Rounded-square corner radius: 40% for exaggerated rounding.
            const float cornerRadius = min( halfW, halfH ) *
                ( m_shape == RoundedSquare ? 0.40f : 0.10f );

            for( UINT y = 0; y < ovH; y++ )
            {
                const UINT srcY = srcCropY + y * srcCropH / ovH;
                const UINT32* srcRow = srcPixels + static_cast<size_t>( srcY ) * srcW32;
                UINT32* dstRow = dstPixels + static_cast<size_t>( y ) * ovW;

                for( UINT x = 0; x < ovW; x++ )
                {
                    const UINT srcX = srcCropX + x * srcCropW / ovW;
                    UINT32 pixel = srcRow[srcX];

                    bool inside = true;
                    if( m_shape == Circle )
                    {
                        // True circle: use min(halfW, halfH) as radius
                        // so the shape is always circular, centered, and
                        // inscribed within the smaller dimension.
                        float radius = min( halfW, halfH );
                        float dx = ( x + 0.5f - halfW ) / radius;
                        float dy = ( y + 0.5f - halfH ) / radius;
                        inside = ( dx * dx + dy * dy ) <= 1.0f;
                    }
                    else if( m_shape == RoundedRect || m_shape == RoundedSquare )
                    {
                        // Check corners only — the interior and edges are always inside.
                        float px = static_cast<float>( x ) + 0.5f;
                        float py = static_cast<float>( y ) + 0.5f;
                        float cx = 0, cy = 0;
                        bool inCorner = false;
                        if( px < cornerRadius && py < cornerRadius )
                        {
                            cx = cornerRadius; cy = cornerRadius; inCorner = true;
                        }
                        else if( px > ovW - cornerRadius && py < cornerRadius )
                        {
                            cx = ovW - cornerRadius; cy = cornerRadius; inCorner = true;
                        }
                        else if( px < cornerRadius && py > ovH - cornerRadius )
                        {
                            cx = cornerRadius; cy = ovH - cornerRadius; inCorner = true;
                        }
                        else if( px > ovW - cornerRadius && py > ovH - cornerRadius )
                        {
                            cx = ovW - cornerRadius; cy = ovH - cornerRadius; inCorner = true;
                        }
                        if( inCorner )
                        {
                            float ddx = px - cx;
                            float ddy = py - cy;
                            inside = ( ddx * ddx + ddy * ddy ) <= ( cornerRadius * cornerRadius );
                        }
                    }
                    // Square: inside is always true (no masking).

                    dstRow[x] = inside ? ( pixel | 0xFF000000u ) : 0x00000000u;
                }
            }

            {
                std::lock_guard<std::mutex> lock( m_frameLock );

#if _DEBUG
                LARGE_INTEGER afterScale;
                QueryPerformanceCounter( &afterScale );
                double scaleMs = static_cast<double>( afterScale.QuadPart - afterCopy.QuadPart ) * 1000.0 / perfFreq.QuadPart;
                totalScaleMs += scaleMs;
#endif

                m_pendingPixels.swap( m_scaledPixels );
                m_pendingW = ovW;
                m_pendingH = ovH;
                m_newFrameReady = true;
                frameCount++;

#if _DEBUG
                LARGE_INTEGER afterLock;
                QueryPerformanceCounter( &afterLock );
                double lockMs = static_cast<double>( afterLock.QuadPart - afterScale.QuadPart ) * 1000.0 / perfFreq.QuadPart;
                totalLockMs += lockMs;

                double frameIntervalMs = static_cast<double>( afterLock.QuadPart - lastFrameTime.QuadPart ) * 1000.0 / perfFreq.QuadPart;
                lastFrameTime = afterLock;

                if( frameCount <= 5 || ( frameCount % 30 ) == 0 )
                {
                    OutputDebug( L"[WebcamCapture] frame %d: cam=%ux%u overlay=%ux%u "
                                L"read=%.1fms copy=%.1fms scale=%.1fms lock=%.1fms "
                                L"interval=%.1fms avgRead=%.1f avgCopy=%.1f avgScale=%.1f\n",
                                 frameCount, m_camWidth, m_camHeight, ovW, ovH,
                                 readMs, copyMs, scaleMs, lockMs, frameIntervalMs,
                                 totalReadMs / frameCount, totalCopyMs / frameCount,
                                 totalScaleMs / frameCount );
                }
#endif
            }

            // Signal that the first frame is ready so Start() can unblock.
            if( frameCount == 1 )
            {
                std::lock_guard<std::mutex> readyLock( m_readyMutex );
                m_firstFrameCaptured = true;
                m_readyCV.notify_one();
            }
        }

    }

    // Release MF objects on the thread that created them.
    m_sourceReader = nullptr;
    if( m_mfStarted )
    {
        MFShutdown();
        m_mfStarted = false;
    }

    CoUninitialize();
}

//----------------------------------------------------------------------------
// WebcamCapture::ComputeDestRect
//
// Computes the destination rectangle for the webcam overlay on the
// recording output surface.
//----------------------------------------------------------------------------
RECT WebcamCapture::ComputeDestRect() const
{
    // Full screen: overlay covers the entire output area (no edges).
    if( m_size == FullScreen )
    {
        // In fullscreen recording mode, letterbox/pillarbox the webcam
        // to preserve its full field of view without magnification.
        if( m_fullScreenRecording && m_camWidth > 0 && m_camHeight > 0 )
        {
            double camAspect = static_cast<double>( m_camWidth ) / m_camHeight;
            double outAspect = static_cast<double>( m_outputWidth ) / m_outputHeight;
            LONG fitW, fitH;
            if( camAspect > outAspect )
            {
                // Camera is wider — fit to width, pillarbox top/bottom.
                fitW = static_cast<LONG>( m_outputWidth );
                fitH = static_cast<LONG>( m_outputWidth / camAspect + 0.5 );
            }
            else
            {
                // Camera is taller — fit to height, letterbox sides.
                fitH = static_cast<LONG>( m_outputHeight );
                fitW = static_cast<LONG>( m_outputHeight * camAspect + 0.5 );
            }
            LONG x = ( static_cast<LONG>( m_outputWidth ) - fitW ) / 2;
            LONG y = ( static_cast<LONG>( m_outputHeight ) - fitH ) / 2;
            return RECT{ x, y, x + fitW, y + fitH };
        }

        return RECT{ 0, 0,
                     static_cast<LONG>( m_outputWidth ),
                     static_cast<LONG>( m_outputHeight ) };
    }

    // Size percentages: Small=15%, Medium=25%, Large=33%, XLarge=50%.
    static const int sizePercent[] = { 15, 25, 33, 50 };
    const int pct = sizePercent[min( static_cast<int>( m_size ), 3 )];
    const int margin = 8;

    // Compute overlay dimensions maintaining camera aspect ratio.
    int overlayW = static_cast<int>( m_outputWidth ) * pct / 100;
    int overlayH = ( m_camHeight > 0 && m_camWidth > 0 )
        ? overlayW * static_cast<int>( m_camHeight ) / static_cast<int>( m_camWidth )
        : overlayW * 3 / 4;

    // Clamp to output size.
    if( overlayW > static_cast<int>( m_outputWidth ) - margin * 2 )
        overlayW = static_cast<int>( m_outputWidth ) - margin * 2;
    if( overlayH > static_cast<int>( m_outputHeight ) - margin * 2 )
        overlayH = static_cast<int>( m_outputHeight ) - margin * 2;

    // For circle and rounded-square shapes, make the bounding box square
    // (use the smaller dimension).  The webcam source is center-cropped
    // to fill this square.
    if( m_shape == Circle || m_shape == RoundedSquare )
    {
        int diameter = min( overlayW, overlayH );
        overlayW = diameter;
        overlayH = diameter;
    }

    RECT dst = {};
    switch( m_position )
    {
    case TopLeft:
        dst.left = margin;
        dst.top = margin;
        break;
    case TopRight:
        dst.left = static_cast<int>( m_outputWidth ) - overlayW - margin;
        dst.top = margin;
        break;
    case BottomLeft:
        dst.left = margin;
        dst.top = static_cast<int>( m_outputHeight ) - overlayH - margin;
        break;
    case BottomRight:
    default:
        dst.left = static_cast<int>( m_outputWidth ) - overlayW - margin;
        dst.top = static_cast<int>( m_outputHeight ) - overlayH - margin;
        break;
    }
    dst.right = dst.left + overlayW;
    dst.bottom = dst.top + overlayH;
    return dst;
}

//----------------------------------------------------------------------------
// WebcamCapture::ComputeOverlayDimensions
//
// Pre-computes the overlay width/height and destination rect once the
// camera resolution is known.
//----------------------------------------------------------------------------
void WebcamCapture::ComputeOverlayDimensions()
{
    m_destRect = ComputeDestRect();
    m_overlayW = static_cast<UINT>( max( 1L, m_destRect.right - m_destRect.left ) );
    m_overlayH = static_cast<UINT>( max( 1L, m_destRect.bottom - m_destRect.top ) );
}

//----------------------------------------------------------------------------
// WebcamCapture::CompositeOnto
//
// Composites the pre-scaled webcam overlay onto the target texture.
// When a new webcam frame arrives, create a fresh GPU texture from the
// CPU pixels using initial subresource data, then copy that texture into
// the target surface. This is a conservative path that avoids both the
// UpdateSubresource and Map/Unmap upload behaviors that appear to vary
// across desktop Intel driver stacks.
//----------------------------------------------------------------------------
bool WebcamCapture::CompositeOnto( ID3D11Texture2D* target )
{
    if( !m_enabled.load( std::memory_order_relaxed ) )
        return false;

#if _DEBUG
    m_compositeCount++;
#endif

    // If the capture thread has new pixels, upload them to the cached
    // GPU texture.  Use try_lock so we NEVER block the encoder callback.
    // If the lock is contended, just reuse the previous cached texture.
    if( m_frameLock.try_lock() )
    {
        if( m_newFrameReady && !m_pendingPixels.empty() && m_pendingW > 0 && m_pendingH > 0 )
        {
#if _DEBUG
            m_uploadCount++;
#endif

            // Use the dimensions the pixels were actually produced at,
            // NOT m_overlayW/m_overlayH which may have been changed by
            // SetDestRectAndSize since the capture thread produced this frame.
            const UINT pixW = m_pendingW;
            const UINT pixH = m_pendingH;

            // Recreate the texture if dimensions changed.
            if( m_texW != pixW || m_texH != pixH )
            {
                m_overlayTex = nullptr;

                D3D11_TEXTURE2D_DESC texDesc = {};
                texDesc.Width = pixW;
                texDesc.Height = pixH;
                texDesc.MipLevels = 1;
                texDesc.ArraySize = 1;
                texDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
                texDesc.SampleDesc.Count = 1;
                texDesc.Usage = D3D11_USAGE_DEFAULT;
                texDesc.BindFlags = 0;

                if( FAILED( m_d3dDevice->CreateTexture2D( &texDesc, nullptr, m_overlayTex.put() ) ) )
                {
                    m_overlayTex = nullptr;
                    m_newFrameReady = false;
                    m_frameLock.unlock();
                    return false;
                }
                m_texW = pixW;
                m_texH = pixH;
            }

            // Upload new pixels.  UpdateSubresource + CopySubresourceRegion
            // on the same immediate context are serialized by the runtime,
            // so there is no read-write hazard.
            m_d3dContext->UpdateSubresource(
                m_overlayTex.get(), 0, nullptr,
                m_pendingPixels.data(), pixW * 4, 0 );

            // Keep a CPU copy for alpha-blended compositing (shaped overlays).
            if( m_shape != Square )
                m_lastUploadedPixels = m_pendingPixels;

            m_hasOverlay = true;
            m_newFrameReady = false;
        }
        m_frameLock.unlock();
    }
    else
    {
#if _DEBUG
        m_lockFailCount++;
#endif
    }

#if _DEBUG
    if( ( m_compositeCount % 30 ) == 0 )
    {
        OutputDebug( L"[WebcamCapture] Composite: calls=%d uploads=%d lockFails=%d hasOverlay=%d\n",
                     m_compositeCount, m_uploadCount, m_lockFailCount, m_hasOverlay ? 1 : 0 );
    }
#endif

    if( !m_hasOverlay )
        return false;

    // For Square shape, use the fast GPU-only blit (no alpha needed).
    // For RoundedRect/Circle, we need alpha blending.
    if( m_shape == Square )
    {
        D3D11_TEXTURE2D_DESC targetDesc = {};
        target->GetDesc( &targetDesc );

        UINT destX = static_cast<UINT>( max( 0L, m_destRect.left ) );
        UINT destY = static_cast<UINT>( max( 0L, m_destRect.top ) );

        D3D11_BOX srcBox = {};
        srcBox.right = min( m_texW, targetDesc.Width - destX );
        srcBox.bottom = min( m_texH, targetDesc.Height - destY );
        srcBox.back = 1;

        m_d3dContext->CopySubresourceRegion(
            target, 0, destX, destY, 0,
            m_overlayTex.get(), 0, &srcBox );
    }
    else
    {
        // Alpha-blended compositing for shaped overlays.
        // Read the target region into a staging texture, blend overlay
        // pixels on CPU, then copy back. The overlay is small (typically
        // 200-400px wide) so this is fast.
        D3D11_TEXTURE2D_DESC targetDesc = {};
        target->GetDesc( &targetDesc );

        UINT destX = static_cast<UINT>( max( 0L, m_destRect.left ) );
        UINT destY = static_cast<UINT>( max( 0L, m_destRect.top ) );
        UINT copyW = min( m_texW, targetDesc.Width - destX );
        UINT copyH = min( m_texH, targetDesc.Height - destY );

        if( copyW == 0 || copyH == 0 )
            return false;

        // Create or reuse staging textures for readback/upload.
        if( !m_stagingTex || m_stagingW != copyW || m_stagingH != copyH )
        {
            m_stagingTex = nullptr;
            D3D11_TEXTURE2D_DESC stagingDesc = {};
            stagingDesc.Width = copyW;
            stagingDesc.Height = copyH;
            stagingDesc.MipLevels = 1;
            stagingDesc.ArraySize = 1;
            stagingDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
            stagingDesc.SampleDesc.Count = 1;
            stagingDesc.Usage = D3D11_USAGE_STAGING;
            stagingDesc.CPUAccessFlags = D3D11_CPU_ACCESS_READ | D3D11_CPU_ACCESS_WRITE;
            if( FAILED( m_d3dDevice->CreateTexture2D( &stagingDesc, nullptr, m_stagingTex.put() ) ) )
                return false;
            m_stagingW = copyW;
            m_stagingH = copyH;
        }

        // Copy the target region to staging for readback.
        D3D11_BOX targetBox = {};
        targetBox.left = destX;
        targetBox.top = destY;
        targetBox.right = destX + copyW;
        targetBox.bottom = destY + copyH;
        targetBox.back = 1;
        m_d3dContext->CopySubresourceRegion( m_stagingTex.get(), 0, 0, 0, 0,
                                             target, 0, &targetBox );

        // Map staging, blend overlay pixels.
        D3D11_MAPPED_SUBRESOURCE mapped = {};
        if( SUCCEEDED( m_d3dContext->Map( m_stagingTex.get(), 0, D3D11_MAP_READ_WRITE, 0, &mapped ) ) )
        {
            // Also need overlay pixels. Map the overlay texture via a
            // separate staging texture, or use the cached CPU pixels.
            // Using the cached CPU pixels (m_pendingPixels) is simplest.
            // But m_pendingPixels may have been swapped — use a lock-free
            // copy stored during upload.
            // Actually, the overlay texture already has the data. We can
            // copy it to another staging texture and map it. But simpler:
            // keep a CPU copy of the last-uploaded pixels.
            const UINT32* overlayPixels = reinterpret_cast<const UINT32*>( m_lastUploadedPixels.data() );
            if( !m_lastUploadedPixels.empty() )
            {
                for( UINT row = 0; row < copyH; row++ )
                {
                    UINT32* dstRow = reinterpret_cast<UINT32*>(
                        static_cast<BYTE*>( mapped.pData ) + row * mapped.RowPitch );
                    const UINT32* srcRow = overlayPixels + row * m_texW;

                    for( UINT col = 0; col < copyW; col++ )
                    {
                        UINT32 ovPixel = srcRow[col];
                        UINT32 ovAlpha = ( ovPixel >> 24 ) & 0xFF;
                        if( ovAlpha == 0xFF )
                        {
                            dstRow[col] = ovPixel;
                        }
                        else if( ovAlpha > 0 )
                        {
                            // Alpha blend: dst = src * alpha + dst * (1-alpha)
                            UINT32 bg = dstRow[col];
                            UINT32 invAlpha = 255 - ovAlpha;
                            UINT32 r = ( ( ( ovPixel >> 16 ) & 0xFF ) * ovAlpha + ( ( bg >> 16 ) & 0xFF ) * invAlpha ) / 255;
                            UINT32 g = ( ( ( ovPixel >> 8 ) & 0xFF ) * ovAlpha + ( ( bg >> 8 ) & 0xFF ) * invAlpha ) / 255;
                            UINT32 b = ( ( ovPixel & 0xFF ) * ovAlpha + ( bg & 0xFF ) * invAlpha ) / 255;
                            dstRow[col] = 0xFF000000u | ( r << 16 ) | ( g << 8 ) | b;
                        }
                        // ovAlpha == 0: keep background pixel unchanged.
                    }
                }
            }
            m_d3dContext->Unmap( m_stagingTex.get(), 0 );

            // Copy blended staging back to the target.
            D3D11_BOX stagingBox = {};
            stagingBox.right = copyW;
            stagingBox.bottom = copyH;
            stagingBox.back = 1;
            m_d3dContext->CopySubresourceRegion( target, 0, destX, destY, 0,
                                                 m_stagingTex.get(), 0, &stagingBox );
        }
    }

    return true;
}

//----------------------------------------------------------------------------
// WebcamCapture::GetLatestPixels
//
// Copies the latest pre-scaled BGRA pixels for on-screen preview.
// Thread-safe — acquires m_frameLock to read m_pendingPixels.
//----------------------------------------------------------------------------
bool WebcamCapture::GetLatestPixels( std::vector<BYTE>& outPixels, UINT& outW, UINT& outH )
{
    if( !m_enabled.load( std::memory_order_relaxed ) )
        return false;

    std::lock_guard<std::mutex> lock( m_frameLock );
    if( m_pendingPixels.empty() || m_pendingW == 0 || m_pendingH == 0 )
        return false;

    outPixels = m_pendingPixels;
    outW = m_pendingW;
    outH = m_pendingH;
    return true;
}
