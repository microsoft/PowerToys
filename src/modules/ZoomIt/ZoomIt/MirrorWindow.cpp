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
#include "Utility.h"
#include <dwmapi.h>

namespace util
{
    using namespace robmikh::common::desktop;
}

const DWORD MIRROR_FRAME_TIMEOUT_MS = 33;
const UINT MIRROR_TOPMOST_TIMER_MS = 250;

//----------------------------------------------------------------------------
//
// RequestBorderlessCapture
//
// Windows 11 draws a yellow system border around captured windows and
// monitors unless the process requests borderless capture access, which is
// granted without a prompt for desktop apps. Until the request completes,
// IsBorderRequired(false) has no effect. Must be called from an MTA thread
// because it blocks on the request.
//
//----------------------------------------------------------------------------
static void RequestBorderlessCapture()
{
    static bool requested = false;
    if( requested )
    {
        return;
    }
    requested = true;

    try
    {
        if( winrt::ApiInformation::IsTypePresent( L"Windows.Graphics.Capture.GraphicsCaptureAccess" ) )
        {
            auto status = winrt::GraphicsCaptureAccess::RequestAccessAsync( winrt::GraphicsCaptureAccessKind::Borderless ).get();
            wchar_t message[128];
            swprintf_s( message, L"[Mirror] Borderless capture access status=%d\n", static_cast<int>( status ) );
            OutputDebugStringW( message );
        }
        else
        {
            OutputDebugStringW( L"[Mirror] GraphicsCaptureAccess type not present\n" );
        }
    }
    catch( const winrt::hresult_error& error )
    {
        wchar_t message[256];
        swprintf_s( message, L"[Mirror] Borderless capture access request failed: 0x%08X\n",
                    static_cast<unsigned>( error.code().value ) );
        OutputDebugStringW( message );
    }
    catch( ... )
    {
        OutputDebugStringW( L"[Mirror] Borderless capture access request failed\n" );
    }
}

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
// MirrorWindow::GetWindowFrameRect
//
// GetWindowRect includes the invisible resize borders around modern
// windows, which would mirror as a margin of desktop; the DWM extended
// frame bounds are the visible edges.
//
//----------------------------------------------------------------------------
RECT MirrorWindow::GetWindowFrameRect( HWND window )
{
    typedef HRESULT ( WINAPI *type_pDwmGetWindowAttribute )( HWND, DWORD, PVOID, DWORD );
    static type_pDwmGetWindowAttribute pDwmGetWindowAttributeDynamic =
        reinterpret_cast<type_pDwmGetWindowAttribute>(
            GetProcAddress( LoadLibraryW( L"dwmapi.dll" ), "DwmGetWindowAttribute" ) );

    RECT rect{};
    if( pDwmGetWindowAttributeDynamic == nullptr ||
        FAILED( pDwmGetWindowAttributeDynamic( window, DWMWA_EXTENDED_FRAME_BOUNDS, &rect, sizeof( rect ) ) ) )
    {
        GetWindowRect( window, &rect );
    }
    return rect;
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
                          std::function<AnnotationState()> annotationQuery,
                          bool trackWindow )
{
    if( IsActive() )
    {
        return false;
    }

    m_sourceRect = sourceRect;
    m_sourceWindow = sourceWindow;
    m_trackWindow = trackWindow;
    m_notifyWindow = notifyWindow;
    m_annotationQuery = annotationQuery;
    m_annotationState = AnnotationState::None;
    m_monitorOverride = false;
    m_sourceTexture = nullptr;
    m_contentSize = item.Size();
    m_poolSize = item.Size();
    m_targetRect = GetMonitorRect( targetMonitor );
    m_sourceMonitorRect = GetMonitorRect( sourceMonitor );

    // The mirrored region in capture-texture coordinates: monitor captures
    // are relative to the monitor origin, window captures use the full
    // captured content. Window tracking captures the monitor and recomputes
    // the crop from the window rect every frame.
    if( m_trackWindow || m_sourceWindow == nullptr )
    {
        SetRect( &m_textureCrop,
                 sourceRect.left - m_sourceMonitorRect.left,
                 sourceRect.top - m_sourceMonitorRect.top,
                 sourceRect.right - m_sourceMonitorRect.left,
                 sourceRect.bottom - m_sourceMonitorRect.top );
    }
    else
    {
        SetRect( &m_textureCrop, 0, 0, m_contentSize.Width, m_contentSize.Height );
    }

    // The swapchain covers the full capture; the displayed image is the
    // crop, which drives the mirror window's aspect ratio.
    m_bufferWidth = m_trackWindow ? m_contentSize.Width : ( m_textureCrop.right - m_textureCrop.left );
    m_bufferHeight = m_trackWindow ? m_contentSize.Height : ( m_textureCrop.bottom - m_textureCrop.top );
    m_imageWidth = m_textureCrop.right - m_textureCrop.left;
    m_imageHeight = m_textureCrop.bottom - m_textureCrop.top;
    if( m_bufferWidth <= 0 || m_bufferHeight <= 0 || m_imageWidth <= 0 || m_imageHeight <= 0 )
    {
        return false;
    }

    RECT windowRect = ComputeWindowRect();

    try
    {
        // Acquire the borderless-capture grant before creating the session
        // so the yellow system capture border never appears. The request
        // blocks, and needs an MTA thread.
        std::thread( []() {
            winrt::init_apartment( winrt::apartment_type::multi_threaded );
            RequestBorderlessCapture();
            winrt::uninit_apartment();
        } ).join();

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

        m_frameWait = std::make_unique<CaptureFrameWait>( m_winrtDevice, item, item.Size(), false );
        m_frameWait->EnableCursorCapture( true );

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

        if( m_sourceWindow != nullptr )
        {
            // Bright green border around the mirrored window so the
            // presenter can see what's being mirrored, matching the record
            // border's width but fully opaque and distinct in color. It
            // follows the window as it moves and resizes.
            m_borderWindow = CreateWindowExW( WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_LAYERED,
                                              m_className, L"ZoomIt DemoMirror Border", WS_POPUP,
                                              0, 0, 0, 0,
                                              nullptr, nullptr, GetModuleHandle( nullptr ), this );
            THROW_LAST_ERROR_IF_NULL( m_borderWindow );
            SetLayeredWindowAttributes( m_borderWindow, 0, 255, LWA_ALPHA );
            EnableWindow( m_borderWindow, FALSE );
            SetWindowDisplayAffinity( m_borderWindow, WDA_EXCLUDEFROMCAPTURE );
            m_borderTarget = m_sourceRect;
            UpdateBorderWindow();
        }
    }
    catch( ... )
    {
        Stop();
        return false;
    }

    ShowWindow( m_backdropWindow, SW_SHOWNA );
    ShowWindow( m_window, SW_SHOWNA );
    if( m_borderWindow != nullptr )
    {
        ShowWindow( m_borderWindow, SW_SHOWNA );
    }

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
        DestroyWindow( m_window );
        m_window = nullptr;
    }
    if( m_backdropWindow != nullptr )
    {
        DestroyWindow( m_backdropWindow );
        m_backdropWindow = nullptr;
    }
    if( m_borderWindow != nullptr )
    {
        DestroyWindow( m_borderWindow );
        m_borderWindow = nullptr;
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
    m_trackWindow = false;
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
        auto frameWait = std::make_unique<CaptureFrameWait>( m_winrtDevice, item, item.Size(), false );
        frameWait->EnableCursorCapture( enableCursor );
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
    m_imageWidth = m_bufferWidth;
    m_imageHeight = m_bufferHeight;
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

    if( m_trackWindow || m_sourceWindow == nullptr )
    {
        // Monitor captures (screen, region, and window tracking) show
        // annotations in place; just avoid a doubled cursor while live zoom
        // renders a magnified one.
        m_frameWait->EnableCursorCapture( state != AnnotationState::AnnotatingLiveZoom );
        m_annotationState = state;
        return false;
    }

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
    double scale = min( static_cast<double>( monitorWidth ) / m_imageWidth,
                        static_cast<double>( monitorHeight ) / m_imageHeight );

    // Mirror windows at native size, scaling only when they don't fit on
    // the target monitor, so the image stays stable as the window resizes.
    // Regions always fill the monitor.
    if( m_sourceWindow != nullptr )
    {
        scale = min( scale, 1.0 );
    }

    const int windowWidth = max( 1, static_cast<int>( m_imageWidth * scale ) );
    const int windowHeight = max( 1, static_cast<int>( m_imageHeight * scale ) );

    RECT windowRect;
    windowRect.left = m_targetRect.left + ( monitorWidth - windowWidth ) / 2;
    windowRect.top = m_targetRect.top + ( monitorHeight - windowHeight ) / 2;
    windowRect.right = windowRect.left + windowWidth;
    windowRect.bottom = windowRect.top + windowHeight;
    return windowRect;
}

//----------------------------------------------------------------------------
//
// MirrorWindow::UpdateBorderWindow
//
// Positions the border frame just outside the source window rectangle,
// using a window region so only the frame is visible.
//
//----------------------------------------------------------------------------
void MirrorWindow::UpdateBorderWindow()
{
    if( m_borderWindow == nullptr )
    {
        return;
    }

    const RECT target = m_borderTarget;
    const int width = ScaleForDpi( 2, GetDpiForWindowHelper( m_borderWindow ) );
    RECT outer = target;
    InflateRect( &outer, width, width );

    wil::unique_hrgn region{ CreateRectRgn( 0, 0, outer.right - outer.left, outer.bottom - outer.top ) };
    wil::unique_hrgn inside{ CreateRectRgn( width, width,
                                            width + ( target.right - target.left ),
                                            width + ( target.bottom - target.top ) ) };
    CombineRgn( region.get(), region.get(), inside.get(), RGN_XOR );

    SetWindowPos( m_borderWindow, HWND_TOPMOST, outer.left, outer.top,
                  outer.right - outer.left, outer.bottom - outer.top, SWP_NOACTIVATE );
    SetWindowRgn( m_borderWindow, region.release(), TRUE );
    RedrawWindow( m_borderWindow, nullptr, nullptr, RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME );
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
    case WM_ERASEBKGND:
        if( window == m_borderWindow )
        {
            RECT clientRect;
            GetClientRect( window, &clientRect );
            HBRUSH brush = CreateSolidBrush( MIRROR_BORDER_COLOR );
            FillRect( reinterpret_cast<HDC>( wordParam ), &clientRect, brush );
            DeleteObject( brush );
            return 1;
        }
        break;

    case WM_MIRROR_BORDER:
        UpdateBorderWindow();
        return 0;

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

        // Track the source window with the border.
        if( m_sourceWindow != nullptr && m_borderWindow != nullptr )
        {
            RECT windowRect = GetWindowFrameRect( m_sourceWindow );
            if( !IsRectEmpty( &windowRect ) && !EqualRect( &windowRect, &m_borderTarget ))
            {
                m_borderTarget = windowRect;
                PostMessage( m_window, WM_MIRROR_BORDER, 0, 0 );
            }
        }

        // Switch between window and monitor capture as ZoomIt annotation
        // modes come and go. On a switch, discard any frame from the
        // previous capture.
        if( m_annotationQuery && UpdateAnnotationState() )
        {
            continue;
        }

        if( frame )
        {
            // A mirrored window can resize; recreate the frame pool, cache
            // texture, and swapchain at the new content size and re-fit the
            // mirror window to the new aspect ratio.
            if( m_sourceWindow != nullptr && !m_monitorOverride && !m_trackWindow &&
                frame->ContentSize.Width > 0 && frame->ContentSize.Height > 0 &&
                ( frame->ContentSize.Width != m_poolSize.Width ||
                  frame->ContentSize.Height != m_poolSize.Height ))
            {
                m_poolSize = frame->ContentSize;
                m_bufferWidth = m_poolSize.Width;
                m_bufferHeight = m_poolSize.Height;
                m_imageWidth = m_bufferWidth;
                m_imageHeight = m_bufferHeight;
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
// Copies the mirrored region of the cached source frame into the swapchain
// and presents it.
//
//----------------------------------------------------------------------------
void MirrorWindow::RenderFrame()
{
    D3D11_TEXTURE2D_DESC sourceDesc;
    m_sourceTexture->GetDesc( &sourceDesc );

    // The mirrored region in texture coordinates, clamped to the captured
    // content, which changes when a mirrored window resizes. Window and
    // monitor-override captures mirror the full captured content; window
    // tracking follows the window rect within the monitor capture.
    RECT crop = m_textureCrop;
    if( m_trackWindow )
    {
        RECT windowRect = GetWindowFrameRect( m_sourceWindow );
        if( IsRectEmpty( &windowRect ) )
        {
            return;
        }
        SetRect( &crop,
                 windowRect.left - m_sourceMonitorRect.left,
                 windowRect.top - m_sourceMonitorRect.top,
                 windowRect.right - m_sourceMonitorRect.left,
                 windowRect.bottom - m_sourceMonitorRect.top );
    }
    else if( m_monitorOverride || m_sourceWindow != nullptr )
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

    // A tracked window's crop changes as it resizes; re-fit the mirror.
    if( m_trackWindow && ( cropWidth != m_imageWidth || cropHeight != m_imageHeight ))
    {
        m_imageWidth = cropWidth;
        m_imageHeight = cropHeight;
        PostMessage( m_window, WM_MIRROR_RELAYOUT, 0, 0 );
    }

    const int width = min( cropWidth, m_bufferWidth );
    const int height = min( cropHeight, m_bufferHeight );

    D3D11_BOX box;
    box.left = crop.left;
    box.top = crop.top;
    box.right = crop.left + width;
    box.bottom = crop.top + height;
    box.front = 0;
    box.back = 1;

    winrt::com_ptr<ID3D11Texture2D> backBuffer;
    if( FAILED( m_swapChain->GetBuffer( 0, winrt::guid_of<ID3D11Texture2D>(), backBuffer.put_void() ) ) )
    {
        return;
    }
    m_context->CopySubresourceRegion( backBuffer.get(), 0, 0, 0, 0, m_sourceTexture.get(), 0, &box );
    m_swapChain->SetSourceSize( width, height );
    m_swapChain->Present( 1, 0 );
}
