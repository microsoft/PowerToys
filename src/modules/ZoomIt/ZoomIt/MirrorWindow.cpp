//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// DemoMirror: mirrors a screen region or window, including the mouse cursor,
// onto a second monitor so an audience can follow a demo app on the
// presentation monitor without the presenter leaving the presentation.
//
// Frames come from Windows.Graphics.Capture (CaptureFrameWait) with cursor
// capture enabled, get cached in a texture, and the visible sub-rectangle is
// copied into a flip-model swapchain whose source size drives the zoom: DXGI
// stretches the sub-rectangle to the window with linear filtering.
//
//==============================================================================
#include "pch.h"
#include "MirrorWindow.h"

namespace util
{
    using namespace robmikh::common::desktop;
}

const float MIRROR_ZOOM_MAX = 8.0f;
const float MIRROR_ZOOM_ANIMATION_STEP = 0.3f;
const float MIRROR_PAN_ANIMATION_STEP = 0.25f;
const DWORD MIRROR_FRAME_TIMEOUT_MS = 33;
const UINT MIRROR_TOPMOST_TIMER_MS = 250;

//----------------------------------------------------------------------------
//
// GetMonitorRect
//
//----------------------------------------------------------------------------
static RECT GetMonitorRect( HMONITOR monitor )
{
    MONITORINFO monitorInfo = { sizeof( monitorInfo ) };
    GetMonitorInfo( monitor, &monitorInfo );
    return monitorInfo.rcMonitor;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::Start
//
// Creates the mirror window on the target monitor and starts capturing and
// presenting the source.
//
//----------------------------------------------------------------------------
bool MirrorWindow::Start( winrt::GraphicsCaptureItem const& item, RECT sourceRect,
                          HWND sourceWindow, HMONITOR sourceMonitor,
                          HMONITOR targetMonitor, HWND notifyWindow,
                          std::function<AnnotationState()> annotationQuery )
{
    if( IsActive() )
    {
        return false;
    }

    m_sourceRect = sourceRect;
    m_sourceWindow = sourceWindow;
    m_notifyWindow = notifyWindow;
    m_annotationQuery = annotationQuery;
    m_annotationState = AnnotationState::None;
    m_monitorOverride = false;
    m_zoom = 1.0f;
    m_zoomTarget = 1.0f;
    m_sourceTexture = nullptr;
    m_contentSize = item.Size();
    m_poolSize = item.Size();
    m_targetRect = GetMonitorRect( targetMonitor );

    // The mirrored region in capture-texture coordinates: monitor captures
    // are relative to the monitor origin, window captures use the full
    // captured content.
    if( m_sourceWindow != nullptr )
    {
        SetRect( &m_textureCrop, 0, 0, m_contentSize.Width, m_contentSize.Height );
    }
    else
    {
        RECT monitorRect = GetMonitorRect( sourceMonitor );
        SetRect( &m_textureCrop,
                 sourceRect.left - monitorRect.left,
                 sourceRect.top - monitorRect.top,
                 sourceRect.right - monitorRect.left,
                 sourceRect.bottom - monitorRect.top );
    }
    m_bufferWidth = m_textureCrop.right - m_textureCrop.left;
    m_bufferHeight = m_textureCrop.bottom - m_textureCrop.top;
    if( m_bufferWidth <= 0 || m_bufferHeight <= 0 )
    {
        return false;
    }

    m_centerX = m_bufferWidth / 2.0f;
    m_centerY = m_bufferHeight / 2.0f;

    RECT windowRect = ComputeWindowRect();

    try
    {
        // A dedicated D3D device keeps mirroring independent of any
        // simultaneous recording session.
        m_device = util::CreateD3D11Device();
        m_device->GetImmediateContext( m_context.put() );
        auto multithread = m_context.try_as<ID3D11Multithread>();
        if( multithread )
        {
            multithread->SetMultithreadProtected( TRUE );
        }

        auto dxgiDevice = m_device.as<IDXGIDevice>();
        m_winrtDevice = CreateDirect3DDevice( dxgiDevice.get() );

        m_frameWait = std::make_unique<CaptureFrameWait>( m_winrtDevice, item, item.Size() );
        m_frameWait->EnableCursorCapture( true );
        m_frameWait->ShowCaptureBorder( false );

        WNDCLASSW windowClass{};
        windowClass.lpfnWndProc = []( HWND window, UINT message, WPARAM wordParam, LPARAM longParam ) -> LRESULT
        {
            if( message == WM_NCCREATE )
            {
                auto createStruct = reinterpret_cast<LPCREATESTRUCT>( longParam );
                SetWindowLongPtrW( window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>( createStruct->lpCreateParams ) );
                return TRUE;
            }

            auto self = reinterpret_cast<MirrorWindow*>( GetWindowLongPtrW( window, GWLP_USERDATA ) );
            if( self == nullptr )
            {
                return DefWindowProcW( window, message, wordParam, longParam );
            }
            return self->WindowProc( window, message, wordParam, longParam );
        };
        windowClass.hInstance = GetModuleHandle( nullptr );
        windowClass.hCursor = LoadCursorW( nullptr, IDC_ARROW );
        windowClass.hbrBackground = static_cast<HBRUSH>( GetStockObject( BLACK_BRUSH ) );
        windowClass.lpszClassName = m_className;
        if( RegisterClassW( &windowClass ) == 0 )
        {
            THROW_LAST_ERROR_IF( GetLastError() != ERROR_CLASS_ALREADY_EXISTS );
        }

        // Full-monitor black backdrop behind the mirror so letterbox areas
        // don't show the presentation. Created first so the mirror window,
        // created after it, starts out above it.
        m_backdropWindow = CreateWindowExW( WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                                            m_className, L"ZoomIt DemoMirror Backdrop", WS_POPUP,
                                            m_targetRect.left, m_targetRect.top,
                                            m_targetRect.right - m_targetRect.left,
                                            m_targetRect.bottom - m_targetRect.top,
                                            nullptr, nullptr, GetModuleHandle( nullptr ), nullptr );
        THROW_LAST_ERROR_IF_NULL( m_backdropWindow );
        SetWindowDisplayAffinity( m_backdropWindow, WDA_EXCLUDEFROMCAPTURE );

        // No activation and click-through: the mirror is display-only and
        // must never steal focus from the demo app.
        m_window = CreateWindowExW( WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT,
                                    m_className, L"ZoomIt DemoMirror", WS_POPUP,
                                    windowRect.left, windowRect.top,
                                    windowRect.right - windowRect.left,
                                    windowRect.bottom - windowRect.top,
                                    nullptr, nullptr, GetModuleHandle( nullptr ), this );
        THROW_LAST_ERROR_IF_NULL( m_window );

        // Never let the mirror feed back into a capture.
        SetWindowDisplayAffinity( m_window, WDA_EXCLUDEFROMCAPTURE );

        DXGI_SWAP_CHAIN_DESC1 swapChainDesc{};
        swapChainDesc.Width = m_bufferWidth;
        swapChainDesc.Height = m_bufferHeight;
        swapChainDesc.Format = DXGI_FORMAT_B8G8R8A8_UNORM;
        swapChainDesc.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT;
        swapChainDesc.SampleDesc.Count = 1;
        swapChainDesc.BufferCount = 2;
        swapChainDesc.Scaling = DXGI_SCALING_STRETCH;
        swapChainDesc.SwapEffect = DXGI_SWAP_EFFECT_FLIP_SEQUENTIAL;
        swapChainDesc.AlphaMode = DXGI_ALPHA_MODE_IGNORE;

        auto dxgiDevice2 = m_device.as<IDXGIDevice2>();
        winrt::com_ptr<IDXGIAdapter> adapter;
        winrt::check_hresult( dxgiDevice2->GetParent( winrt::guid_of<IDXGIAdapter>(), adapter.put_void() ) );
        winrt::com_ptr<IDXGIFactory2> factory;
        winrt::check_hresult( adapter->GetParent( winrt::guid_of<IDXGIFactory2>(), factory.put_void() ) );

        winrt::com_ptr<IDXGISwapChain1> swapChain;
        winrt::check_hresult( factory->CreateSwapChainForHwnd( m_device.get(), m_window, &swapChainDesc,
                                                               nullptr, nullptr, swapChain.put() ) );
        m_swapChain = swapChain.as<IDXGISwapChain2>();
        factory->MakeWindowAssociation( m_window, DXGI_MWA_NO_ALT_ENTER | DXGI_MWA_NO_WINDOW_CHANGES );
    }
    catch( ... )
    {
        Stop();
        return false;
    }

    ShowWindow( m_backdropWindow, SW_SHOWNA );
    ShowWindow( m_window, SW_SHOWNA );

    // Ctrl+Up/Ctrl+Down zoom the mirror, matching LiveZoom. Registration
    // fails benignly when LiveZoom is active and owns the keys.
    RegisterHotKey( m_window, ZOOM_IN_HOTKEY_ID, MOD_CONTROL, VK_UP );
    RegisterHotKey( m_window, ZOOM_OUT_HOTKEY_ID, MOD_CONTROL, VK_DOWN );

    // The presentation reasserts topmost when a slide show starts, so
    // periodically reclaim it like live zoom does.
    SetTimer( m_window, TOPMOST_TIMER_ID, MIRROR_TOPMOST_TIMER_MS, nullptr );

    m_stopEvent.ResetEvent();
    m_renderThread = std::thread( &MirrorWindow::RenderLoop, this );
    return true;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::Stop
//
// Stops the capture and render thread and tears down the window. Also
// handles partially-initialized state when Start fails.
//
//----------------------------------------------------------------------------
void MirrorWindow::Stop()
{
    m_stopEvent.SetEvent();
    if( m_frameWait )
    {
        m_frameWait->StopCapture();
    }
    if( m_renderThread.joinable() )
    {
        m_renderThread.join();
    }

    if( m_window != nullptr )
    {
        KillTimer( m_window, TOPMOST_TIMER_ID );
        UnregisterHotKey( m_window, ZOOM_IN_HOTKEY_ID );
        UnregisterHotKey( m_window, ZOOM_OUT_HOTKEY_ID );
        DestroyWindow( m_window );
        m_window = nullptr;
    }
    if( m_backdropWindow != nullptr )
    {
        DestroyWindow( m_backdropWindow );
        m_backdropWindow = nullptr;
    }

    m_frameWait = nullptr;
    m_sourceTexture = nullptr;
    m_swapChain = nullptr;
    m_context = nullptr;
    m_device = nullptr;
    m_winrtDevice = nullptr;
    m_sourceWindow = nullptr;
    m_notifyWindow = nullptr;
    m_annotationQuery = nullptr;
    m_annotationState = AnnotationState::None;
    m_monitorOverride = false;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::SwitchCapture
//
// Replaces the capture source mid-mirror, resizing the swapchain to the new
// content size and re-fitting the mirror window.
//
//----------------------------------------------------------------------------
bool MirrorWindow::SwitchCapture( winrt::GraphicsCaptureItem const& item, bool enableCursor )
{
    try
    {
        auto frameWait = std::make_unique<CaptureFrameWait>( m_winrtDevice, item, item.Size() );
        frameWait->EnableCursorCapture( enableCursor );
        frameWait->ShowCaptureBorder( false );
        m_frameWait->StopCapture();
        m_frameWait = std::move( frameWait );
    }
    catch( ... )
    {
        return false;
    }

    m_poolSize = item.Size();
    m_bufferWidth = m_poolSize.Width;
    m_bufferHeight = m_poolSize.Height;
    m_contentSize = m_poolSize;
    m_sourceTexture = nullptr;
    if( FAILED( m_swapChain->ResizeBuffers( 2, m_bufferWidth, m_bufferHeight,
                                            DXGI_FORMAT_B8G8R8A8_UNORM, 0 ) ) )
    {
        return false;
    }
    PostMessage( m_window, WM_MIRROR_RELAYOUT, 0, 0 );
    return true;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::UpdateAnnotationState
//
// While mirroring a window, ZoomIt's zoom/draw/live-zoom overlays aren't
// part of the window's surface, so they wouldn't show in the mirror. When
// one of those modes activates, temporarily mirror the monitor under the
// cursor (where the annotation UI appears) instead, and switch back to the
// window when the mode exits. Returns true when the capture was switched so
// the caller can discard the frame from the previous capture.
//
//----------------------------------------------------------------------------
bool MirrorWindow::UpdateAnnotationState()
{
    const AnnotationState state = m_annotationQuery();
    if( state == m_annotationState )
    {
        return false;
    }
    bool switched = false;

    if( state == AnnotationState::None )
    {
        if( IsWindow( m_sourceWindow ) )
        {
            try
            {
                auto item = util::CreateCaptureItemForWindow( m_sourceWindow );
                if( SwitchCapture( item, true ) )
                {
                    m_monitorOverride = false;
                    switched = true;
                }
            }
            catch( ... ) {}
        }
    }
    else if( !m_monitorOverride )
    {
        POINT cursorPos;
        GetCursorPos( &cursorPos );
        HMONITOR monitor = MonitorFromPoint( cursorPos, MONITOR_DEFAULTTONEAREST );
        try
        {
            auto item = util::CreateCaptureItemForMonitor( monitor );

            // Live zoom already renders a magnified cursor into the frame;
            // capturing the cursor too would double it.
            if( SwitchCapture( item, state != AnnotationState::AnnotatingLiveZoom ) )
            {
                m_overrideRect = GetMonitorRect( monitor );
                m_monitorOverride = true;
                switched = true;
            }
        }
        catch( ... ) {}
    }
    else
    {
        // Already mirroring the monitor; just track the cursor-capture state.
        m_frameWait->EnableCursorCapture( state != AnnotationState::AnnotatingLiveZoom );
    }
    m_annotationState = state;
    return switched;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::ComputeWindowRect
//
// The largest rectangle with the source's aspect ratio that fits on the
// target monitor, centered.
//
//----------------------------------------------------------------------------
RECT MirrorWindow::ComputeWindowRect() const
{
    const int monitorWidth = m_targetRect.right - m_targetRect.left;
    const int monitorHeight = m_targetRect.bottom - m_targetRect.top;
    const double scale = min( static_cast<double>( monitorWidth ) / m_bufferWidth,
                              static_cast<double>( monitorHeight ) / m_bufferHeight );
    const int windowWidth = max( 1, static_cast<int>( m_bufferWidth * scale ) );
    const int windowHeight = max( 1, static_cast<int>( m_bufferHeight * scale ) );

    RECT windowRect;
    windowRect.left = m_targetRect.left + ( monitorWidth - windowWidth ) / 2;
    windowRect.top = m_targetRect.top + ( monitorHeight - windowHeight ) / 2;
    windowRect.right = windowRect.left + windowWidth;
    windowRect.bottom = windowRect.top + windowHeight;
    return windowRect;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::WindowProc
//
//----------------------------------------------------------------------------
LRESULT MirrorWindow::WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam )
{
    switch( message )
    {
    case WM_HOTKEY:
        if( wordParam == ZOOM_IN_HOTKEY_ID )
        {
            float zoomTarget = m_zoomTarget;
            if( zoomTarget < MIRROR_ZOOM_MAX )
            {
                m_zoomTarget = zoomTarget * 2;
            }
            return 0;
        }
        else if( wordParam == ZOOM_OUT_HOTKEY_ID )
        {
            float zoomTarget = m_zoomTarget;
            if( zoomTarget > 1.0f )
            {
                m_zoomTarget = max( zoomTarget / 2, 1.0f );
            }
            return 0;
        }
        break;

    case WM_TIMER:
        if( wordParam == TOPMOST_TIMER_ID )
        {
            SetWindowPos( window, HWND_TOPMOST, 0, 0, 0, 0,
                          SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE );
            if( m_backdropWindow != nullptr )
            {
                // Keep the backdrop directly below the mirror.
                SetWindowPos( m_backdropWindow, window, 0, 0, 0, 0,
                              SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE );
            }
            return 0;
        }
        break;

    case WM_MIRROR_RELAYOUT:
    {
        // The mirrored window resized; re-fit to its new aspect ratio.
        RECT windowRect = ComputeWindowRect();
        SetWindowPos( window, HWND_TOPMOST, windowRect.left, windowRect.top,
                      windowRect.right - windowRect.left,
                      windowRect.bottom - windowRect.top, SWP_NOACTIVATE );
        return 0;
    }

    case WM_MOUSEACTIVATE:
        return MA_NOACTIVATE;
    }
    return DefWindowProcW( window, message, wordParam, longParam );
}

//----------------------------------------------------------------------------
//
// MirrorWindow::RenderLoop
//
// Render thread: caches arriving capture frames and presents them. The
// timeout keeps zoom/pan animating when the source is static, since
// Windows.Graphics.Capture only delivers frames on change.
//
//----------------------------------------------------------------------------
void MirrorWindow::RenderLoop()
{
    winrt::init_apartment( winrt::apartment_type::multi_threaded );

    while( !m_stopEvent.is_signaled() )
    {
        auto frame = m_frameWait->TryGetNextFrame( MIRROR_FRAME_TIMEOUT_MS );
        if( m_stopEvent.is_signaled() )
        {
            break;
        }

        // Stop if the mirrored window went away.
        if( m_sourceWindow != nullptr && !IsWindow( m_sourceWindow ) )
        {
            PostMessage( m_notifyWindow, WM_USER_MIRROR_STOP, 0, 0 );
            break;
        }

        // Switch between window and monitor capture as ZoomIt annotation
        // modes come and go. On a switch, discard any frame from the
        // previous capture.
        if( m_sourceWindow != nullptr && m_annotationQuery && UpdateAnnotationState() )
        {
            continue;
        }

        if( frame )
        {
            // A mirrored window can resize; recreate the frame pool, cache
            // texture, and swapchain at the new content size and re-fit the
            // mirror window to the new aspect ratio.
            if( m_sourceWindow != nullptr && !m_monitorOverride &&
                frame->ContentSize.Width > 0 && frame->ContentSize.Height > 0 &&
                ( frame->ContentSize.Width != m_poolSize.Width ||
                  frame->ContentSize.Height != m_poolSize.Height ))
            {
                m_poolSize = frame->ContentSize;
                m_bufferWidth = m_poolSize.Width;
                m_bufferHeight = m_poolSize.Height;
                m_sourceTexture = nullptr;
                m_frameWait->RecreateFramePool( m_poolSize );
                if( FAILED( m_swapChain->ResizeBuffers( 2, m_bufferWidth, m_bufferHeight,
                                                        DXGI_FORMAT_B8G8R8A8_UNORM, 0 ) ) )
                {
                    break;
                }
                PostMessage( m_window, WM_MIRROR_RELAYOUT, 0, 0 );

                // Wait for a frame at the new size.
                continue;
            }

            auto frameTexture = GetDXGIInterfaceFromObject<ID3D11Texture2D>( frame->FrameTexture );
            D3D11_TEXTURE2D_DESC textureDesc;
            frameTexture->GetDesc( &textureDesc );
            if( m_sourceTexture != nullptr )
            {
                // The cached copy must match the frame exactly for CopyResource.
                D3D11_TEXTURE2D_DESC cachedDesc;
                m_sourceTexture->GetDesc( &cachedDesc );
                if( cachedDesc.Width != textureDesc.Width || cachedDesc.Height != textureDesc.Height )
                {
                    m_sourceTexture = nullptr;
                }
            }
            if( m_sourceTexture == nullptr )
            {
                textureDesc.Usage = D3D11_USAGE_DEFAULT;
                textureDesc.BindFlags = 0;
                textureDesc.CPUAccessFlags = 0;
                textureDesc.MiscFlags = 0;
                if( FAILED( m_device->CreateTexture2D( &textureDesc, nullptr, m_sourceTexture.put() ) ) )
                {
                    break;
                }
            }
            m_context->CopyResource( m_sourceTexture.get(), frameTexture.get() );
            m_contentSize = frame->ContentSize;
        }

        if( m_sourceTexture != nullptr )
        {
            RenderFrame();
        }
    }

    winrt::uninit_apartment();
}

//----------------------------------------------------------------------------
//
// MirrorWindow::RenderFrame
//
// Copies the visible sub-rectangle of the cached source frame into the
// swapchain and presents it. The sub-rectangle shrinks with the zoom level
// and pans to follow the mouse.
//
//----------------------------------------------------------------------------
void MirrorWindow::RenderFrame()
{
    // Animate toward the target zoom level.
    const float zoomTarget = m_zoomTarget;
    m_zoom += ( zoomTarget - m_zoom ) * MIRROR_ZOOM_ANIMATION_STEP;
    if( fabsf( zoomTarget - m_zoom ) < 0.01f )
    {
        m_zoom = zoomTarget;
    }

    D3D11_TEXTURE2D_DESC sourceDesc;
    m_sourceTexture->GetDesc( &sourceDesc );

    // The mirrored region in texture coordinates, clamped to the captured
    // content, which changes when a mirrored window resizes. Window and
    // monitor-override captures mirror the full captured content.
    RECT crop = m_textureCrop;
    if( m_monitorOverride || m_sourceWindow != nullptr )
    {
        SetRect( &crop, 0, 0, m_contentSize.Width, m_contentSize.Height );
    }
    crop.left = max( crop.left, 0L );
    crop.top = max( crop.top, 0L );
    crop.right = min( crop.right, static_cast<LONG>( sourceDesc.Width ) );
    crop.bottom = min( crop.bottom, static_cast<LONG>( sourceDesc.Height ) );
    const int cropWidth = crop.right - crop.left;
    const int cropHeight = crop.bottom - crop.top;
    if( cropWidth <= 0 || cropHeight <= 0 )
    {
        return;
    }

    const int subWidth = max( 1, min( static_cast<int>( cropWidth / m_zoom ), m_bufferWidth ) );
    const int subHeight = max( 1, min( static_cast<int>( cropHeight / m_zoom ), m_bufferHeight ) );

    // Pan to follow the mouse when zoomed.
    float desiredX = cropWidth / 2.0f;
    float desiredY = cropHeight / 2.0f;
    if( m_zoom > 1.0f )
    {
        POINT cursorPos;
        if( GetCursorPos( &cursorPos ) )
        {
            if( m_monitorOverride )
            {
                desiredX = static_cast<float>( cursorPos.x - m_overrideRect.left );
                desiredY = static_cast<float>( cursorPos.y - m_overrideRect.top );
            }
            else if( m_sourceWindow != nullptr )
            {
                RECT windowRect;
                if( GetWindowRect( m_sourceWindow, &windowRect ) &&
                    windowRect.right > windowRect.left && windowRect.bottom > windowRect.top )
                {
                    desiredX = ( cursorPos.x - windowRect.left ) * static_cast<float>( cropWidth ) /
                                    ( windowRect.right - windowRect.left );
                    desiredY = ( cursorPos.y - windowRect.top ) * static_cast<float>( cropHeight ) /
                                    ( windowRect.bottom - windowRect.top );
                }
            }
            else
            {
                desiredX = static_cast<float>( cursorPos.x - m_sourceRect.left );
                desiredY = static_cast<float>( cursorPos.y - m_sourceRect.top );
            }
        }
    }
    desiredX = max( subWidth / 2.0f, min( desiredX, cropWidth - subWidth / 2.0f ) );
    desiredY = max( subHeight / 2.0f, min( desiredY, cropHeight - subHeight / 2.0f ) );
    m_centerX += ( desiredX - m_centerX ) * MIRROR_PAN_ANIMATION_STEP;
    m_centerY += ( desiredY - m_centerY ) * MIRROR_PAN_ANIMATION_STEP;

    int boxLeft = crop.left + static_cast<int>( m_centerX - subWidth / 2.0f );
    int boxTop = crop.top + static_cast<int>( m_centerY - subHeight / 2.0f );
    boxLeft = max( static_cast<int>( crop.left ), min( boxLeft, static_cast<int>( crop.right ) - subWidth ) );
    boxTop = max( static_cast<int>( crop.top ), min( boxTop, static_cast<int>( crop.bottom ) - subHeight ) );

    D3D11_BOX box;
    box.left = boxLeft;
    box.top = boxTop;
    box.right = boxLeft + subWidth;
    box.bottom = boxTop + subHeight;
    box.front = 0;
    box.back = 1;

    winrt::com_ptr<ID3D11Texture2D> backBuffer;
    if( FAILED( m_swapChain->GetBuffer( 0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void() ) ) )
    {
        return;
    }
    m_context->CopySubresourceRegion( backBuffer.get(), 0, 0, 0, 0, m_sourceTexture.get(), 0, &box );
    m_swapChain->SetSourceSize( subWidth, subHeight );
    m_swapChain->Present( 1, 0 );
}
