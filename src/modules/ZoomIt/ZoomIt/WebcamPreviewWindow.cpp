//==============================================================================
//
// WebcamPreviewWindow.cpp
//
// On-screen webcam preview during recording.
//
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
//==============================================================================
#include "pch.h"
#include "WebcamPreviewWindow.h"
#include <windowsx.h>  // GET_X_LPARAM, GET_Y_LPARAM
#include <cmath>       // sqrtf, fabsf, atan2f

// Defined in Zoomit.cpp; compiles to nothing in Release builds.
void OutputDebug( const TCHAR* format, ... );

static const wchar_t* const kClassName = L"ZoomItWebcamPreview";
static bool s_classRegistered = false;

//----------------------------------------------------------------------------
// WebcamPreviewWindow::~WebcamPreviewWindow
//----------------------------------------------------------------------------
WebcamPreviewWindow::~WebcamPreviewWindow()
{
    Destroy();
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::ComputeScreenRect
//
// Maps the webcam overlay's position from recording-output coordinates
// to screen coordinates within the recording region.
//----------------------------------------------------------------------------
RECT WebcamPreviewWindow::ComputeScreenRect() const
{
    RECT dest = m_capture->GetDestRect();

    int screenW = m_screenRect.right - m_screenRect.left;
    int screenH = m_screenRect.bottom - m_screenRect.top;
    int outW = static_cast<int>( m_outputWidth );
    int outH = static_cast<int>( m_outputHeight );

    if( outW <= 0 || outH <= 0 )
        return {};

    // Map from output coordinates to screen coordinates.
    RECT r;
    r.left   = m_screenRect.left + MulDiv( dest.left,   screenW, outW );
    r.top    = m_screenRect.top  + MulDiv( dest.top,    screenH, outH );
    r.right  = m_screenRect.left + MulDiv( dest.right,  screenW, outW );
    r.bottom = m_screenRect.top  + MulDiv( dest.bottom, screenH, outH );
    return r;
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::Create
//----------------------------------------------------------------------------
bool WebcamPreviewWindow::Create(
    WebcamCapture* pCapture,
    RECT screenRect,
    UINT outputWidth,
    UINT outputHeight )
{
    if( !pCapture )
        return false;

    m_capture = pCapture;
    m_screenRect = screenRect;
    m_outputWidth = outputWidth;
    m_outputHeight = outputHeight;

    // Register the window class once.
    if( !s_classRegistered )
    {
        WNDCLASSEXW wc = { sizeof( wc ) };
        wc.lpfnWndProc = WndProc;
        wc.hInstance = GetModuleHandleW( nullptr );
        wc.lpszClassName = kClassName;
        wc.hCursor = LoadCursor( nullptr, IDC_ARROW );
        wc.hbrBackground = static_cast<HBRUSH>( GetStockObject( BLACK_BRUSH ) );
        if( !RegisterClassExW( &wc ) )
            return false;
        s_classRegistered = true;
    }

    RECT r = ComputeScreenRect();
    int w = r.right - r.left;
    int h = r.bottom - r.top;
    if( w <= 0 || h <= 0 )
        return false;

    m_hwnd = CreateWindowExW(
        WS_EX_TOPMOST | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED,
        kClassName,
        L"",
        WS_POPUP,
        r.left, r.top, w, h,
        nullptr,     // no owner — avoids hidden-owner issues
        nullptr,
        GetModuleHandleW( nullptr ),
        this );

    if( !m_hwnd )
        return false;

    // Exclude from screen capture so it doesn't appear in the recording.
    typedef BOOL( WINAPI* PSWA )( HWND, DWORD );
    auto pSetWindowDisplayAffinity = reinterpret_cast<PSWA>(
        GetProcAddress( GetModuleHandleW( L"user32.dll" ), "SetWindowDisplayAffinity" ) );
    if( pSetWindowDisplayAffinity )
        pSetWindowDisplayAffinity( m_hwnd, WDA_EXCLUDEFROMCAPTURE );

    ShowWindow( m_hwnd, SW_SHOWNA );
    SetTimer( m_hwnd, TIMER_ID, TIMER_MS, nullptr );

    OutputDebug( L"[WebcamPreview] Created: screen=(%d,%d)-(%d,%d) size=%dx%d\n",
                 r.left, r.top, r.right, r.bottom, w, h );
    return true;
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::Destroy
//----------------------------------------------------------------------------
void WebcamPreviewWindow::Destroy()
{
    if( m_hwnd )
    {
        KillTimer( m_hwnd, TIMER_ID );
        DestroyWindow( m_hwnd );
        m_hwnd = nullptr;
    }
    m_capture = nullptr;
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::Hide / Show
//
// Hide or show the preview window without destroying it, preserving the
// user's last position and size for when it is re-shown.
//----------------------------------------------------------------------------
void WebcamPreviewWindow::Hide()
{
    if( m_hwnd )
    {
        KillTimer( m_hwnd, TIMER_ID );
        ShowWindow( m_hwnd, SW_HIDE );
    }
}

void WebcamPreviewWindow::Show()
{
    if( m_hwnd )
    {
        ShowWindow( m_hwnd, SW_SHOWNA );
        SetTimer( m_hwnd, TIMER_ID, TIMER_MS, nullptr );
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::OnTimer
//
// Fetches the latest webcam pixels and triggers a repaint.
//----------------------------------------------------------------------------
void WebcamPreviewWindow::OnTimer()
{
    if( !m_capture )
        return;

    // Re-assert topmost Z-order so the preview stays above the live zoom
    // magnification window.  SWP_NOACTIVATE does not affect SetCapture,
    // so this is safe even during active drag/resize.
    SetWindowPos( m_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                  SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE );

    UINT w = 0, h = 0;
    if( !m_capture->GetLatestPixels( m_pixels, w, h ) )
    {
        // No frame available yet — keep waiting.
        return;
    }

    {
        if( w != m_pixW || h != m_pixH )
        {
            OutputDebug( L"[WebcamPreview] Pixel dims changed: %ux%u -> %ux%u\n",
                         m_pixW, m_pixH, w, h );
            m_pixW = w;
            m_pixH = h;
        }

        // Use UpdateLayeredWindow with per-pixel alpha so transparent
        // pixels (from circle/rounded-rect masking) let the desktop
        // show through instead of painting black.
        if( m_pixW > 0 && m_pixH > 0 && !m_pixels.empty() )
        {
            RECT wndRect;
            GetWindowRect( m_hwnd, &wndRect );
            int clientW = wndRect.right - wndRect.left;
            int clientH = wndRect.bottom - wndRect.top;

            HDC hdcScreen = GetDC( nullptr );
            HDC hdcMem = CreateCompatibleDC( hdcScreen );

            BITMAPINFO bmi = {};
            bmi.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
            bmi.bmiHeader.biWidth = static_cast<LONG>( m_pixW );
            bmi.bmiHeader.biHeight = -static_cast<LONG>( m_pixH ); // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 32;
            bmi.bmiHeader.biCompression = BI_RGB;

            void* pvBits = nullptr;
            HBITMAP hBmp = CreateDIBSection( hdcMem, &bmi, DIB_RGB_COLORS, &pvBits, nullptr, 0 );
            if( hBmp && pvBits )
            {
                // Copy webcam pixels, converting to pre-multiplied alpha.
                // UpdateLayeredWindow + AC_SRC_ALPHA requires premultiplied BGRA.
                const UINT32* src = reinterpret_cast<const UINT32*>( m_pixels.data() );
                UINT32* dst = static_cast<UINT32*>( pvBits );
                const UINT totalPixels = m_pixW * m_pixH;
                for( UINT i = 0; i < totalPixels; i++ )
                {
                    UINT32 px = src[i];
                    UINT32 a = ( px >> 24 ) & 0xFF;
                    if( a == 0xFF )
                    {
                        dst[i] = px;
                    }
                    else if( a == 0 )
                    {
                        dst[i] = 0;
                    }
                    else
                    {
                        UINT32 r = ( ( px >> 16 ) & 0xFF ) * a / 255;
                        UINT32 g = ( ( px >> 8 ) & 0xFF ) * a / 255;
                        UINT32 b = ( px & 0xFF ) * a / 255;
                        dst[i] = ( a << 24 ) | ( r << 16 ) | ( g << 8 ) | b;
                    }
                }

                HBITMAP hOld = static_cast<HBITMAP>( SelectObject( hdcMem, hBmp ) );

                POINT ptSrc = { 0, 0 };
                POINT ptDst = { wndRect.left, wndRect.top };
                SIZE sizeWnd = { clientW, clientH };
                BLENDFUNCTION blend = {};
                blend.BlendOp = AC_SRC_OVER;
                blend.SourceConstantAlpha = 255;
                blend.AlphaFormat = AC_SRC_ALPHA;

                // If webcam pixels differ from window size, stretch via
                // StretchBlt into a same-size DIB first.
                if( static_cast<UINT>( clientW ) != m_pixW ||
                    static_cast<UINT>( clientH ) != m_pixH )
                {
                    OutputDebug( L"[WebcamPreview] StretchBlt path: pix=%ux%u wnd=%dx%d\n",
                                 m_pixW, m_pixH, clientW, clientH );

                    HDC hdcStretch = CreateCompatibleDC( hdcScreen );
                    BITMAPINFO bmiStretch = bmi;
                    bmiStretch.bmiHeader.biWidth = clientW;
                    bmiStretch.bmiHeader.biHeight = -clientH;
                    void* pvStretch = nullptr;
                    HBITMAP hBmpStretch = CreateDIBSection( hdcStretch, &bmiStretch,
                                                            DIB_RGB_COLORS, &pvStretch, nullptr, 0 );
                    if( hBmpStretch )
                    {
                        HBITMAP hOldStretch = static_cast<HBITMAP>( SelectObject( hdcStretch, hBmpStretch ) );
                        SetStretchBltMode( hdcStretch, COLORONCOLOR );
                        StretchBlt( hdcStretch, 0, 0, clientW, clientH,
                                    hdcMem, 0, 0, m_pixW, m_pixH, SRCCOPY );
                        ForceEdgeAlpha( pvStretch, clientW, clientH, EDGE_GRAB,
                                        m_capture ? m_capture->GetShape() : WebcamCapture::Square );
                        if( !UpdateLayeredWindow( m_hwnd, hdcScreen, &ptDst, &sizeWnd,
                                             hdcStretch, &ptSrc, 0, &blend, ULW_ALPHA ) )
                        {
                            OutputDebug( L"[WebcamPreview] UpdateLayeredWindow FAILED (stretch) err=%u\n",
                                         GetLastError() );
                        }
                        SelectObject( hdcStretch, hOldStretch );
                        DeleteObject( hBmpStretch );
                    }
                    DeleteDC( hdcStretch );
                }
                else
                {
                    ForceEdgeAlpha( pvBits, clientW, clientH, EDGE_GRAB,
                                    m_capture ? m_capture->GetShape() : WebcamCapture::Square );
                    if( !UpdateLayeredWindow( m_hwnd, hdcScreen, &ptDst, &sizeWnd,
                                         hdcMem, &ptSrc, 0, &blend, ULW_ALPHA ) )
                    {
                        OutputDebug( L"[WebcamPreview] UpdateLayeredWindow FAILED err=%u\n",
                                     GetLastError() );
                    }
                }

                SelectObject( hdcMem, hOld );
                DeleteObject( hBmp );
            }
            DeleteDC( hdcMem );
            ReleaseDC( nullptr, hdcScreen );
        }
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::OnPaint
//----------------------------------------------------------------------------
void WebcamPreviewWindow::OnPaint()
{
    // Layered windows don't receive WM_PAINT in the normal sense.
    // Just validate the region so Windows doesn't keep sending it.
    PAINTSTRUCT ps;
    BeginPaint( m_hwnd, &ps );
    EndPaint( m_hwnd, &ps );
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::ForceEdgeAlpha
//
// For WS_EX_LAYERED windows rendered via UpdateLayeredWindow(ULW_ALPHA),
// the system uses the alpha channel for hit-testing — pixels with
// alpha == 0 let mouse messages pass through to the window below.
// To ensure the resize grab zones work even where the webcam image is
// transparent (circle / rounded-rect masking), we force a minimum
// alpha of 1 on all pixels within the grab margin.  Alpha 1/255 is
// imperceptible but sufficient for hit-testing.
//
// NOTE: SetWindowRgn cannot be used with UpdateLayeredWindow — MSDN
// documents the combination as undefined behaviour.
//----------------------------------------------------------------------------
void WebcamPreviewWindow::ForceEdgeAlpha( void* pBits, int width, int height, int grab,
                                          WebcamCapture::Shape shape )
{
    UINT32* pixels = static_cast<UINT32*>( pBits );

    if( shape == WebcamCapture::Circle )
    {
        // Force alpha=1 in an annular ring around the inscribed circle.
        const float cx = width  * 0.5f;
        const float cy = height * 0.5f;
        const float radius = min( cx, cy );
        const float rOuter = radius + grab;
        const float rInner = max( 0.0f, radius - grab );
        const float rOuter2 = rOuter * rOuter;
        const float rInner2 = rInner * rInner;

        for( int y = 0; y < height; y++ )
        {
            const float dy = y + 0.5f - cy;
            for( int x = 0; x < width; x++ )
            {
                const float dx = x + 0.5f - cx;
                const float d2 = dx * dx + dy * dy;
                if( d2 >= rInner2 && d2 <= rOuter2 )
                {
                    UINT32& px = pixels[y * width + x];
                    if( ( px >> 24 ) == 0 )
                        px = 0x01000000;  // alpha=1, premultiplied black
                }
            }
        }
    }
    else
    {
        // Rectangular / rounded-rect: force alpha on the rectangular border.
        int grabX = min( grab, width );
        int grabY = min( grab, height );

        // Top edge rows.
        for( int y = 0; y < grabY; y++ )
            for( int x = 0; x < width; x++ )
            {
                UINT32& px = pixels[y * width + x];
                if( ( px >> 24 ) == 0 )
                    px = 0x01000000;
            }

        // Bottom edge rows.
        for( int y = max( grabY, height - grab ); y < height; y++ )
            for( int x = 0; x < width; x++ )
            {
                UINT32& px = pixels[y * width + x];
                if( ( px >> 24 ) == 0 )
                    px = 0x01000000;
            }

        // Left and right columns in the middle rows.
        for( int y = grabY; y < height - grab; y++ )
        {
            for( int x = 0; x < grabX; x++ )
            {
                UINT32& px = pixels[y * width + x];
                if( ( px >> 24 ) == 0 )
                    px = 0x01000000;
            }
            for( int x = max( grabX, width - grab ); x < width; x++ )
            {
                UINT32& px = pixels[y * width + x];
                if( ( px >> 24 ) == 0 )
                    px = 0x01000000;
            }
        }
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::HitTestEdge
//
// Returns a combination of ResizeEdge flags indicating which edge(s)
// the given client-coordinate point is near, or EdgeNone if the cursor
// is in the interior (drag zone).
//----------------------------------------------------------------------------
UINT WebcamPreviewWindow::HitTestEdge( int x, int y ) const
{
    RECT rc;
    GetClientRect( m_hwnd, &rc );
    const int w = rc.right;
    const int h = rc.bottom;

    if( m_capture && m_capture->GetShape() == WebcamCapture::Circle )
    {
        // Circle mode: hit-test against the inscribed circle's circumference.
        const float cx = w * 0.5f;
        const float cy = h * 0.5f;
        const float radius = min( cx, cy );
        const float dx = x - cx;
        const float dy = y - cy;
        const float dist = sqrtf( dx * dx + dy * dy );

        // Only treat as edge if within EDGE_GRAB of the circumference.
        if( fabsf( dist - radius ) > EDGE_GRAB )
            return EdgeNone;  // interior (drag) or outside the circle

        // Determine which edge(s) based on 45° octants.
        // atan2 returns angle from center; map to edge flags.
        // Note: screen Y is inverted (down = positive), so negate dy for angle.
        float angle = atan2f( -dy, dx );  // radians, 0=right, PI/2=up
        if( angle < 0 ) angle += 6.2831853f;  // normalize to [0, 2*PI)

        // 8 octants of 45° each, centered on cardinal/diagonal directions:
        //   Right:        337.5° – 22.5°   (octant 0)
        //   Top+Right:     22.5° – 67.5°   (octant 1)
        //   Top:           67.5° – 112.5°  (octant 2)
        //   Top+Left:     112.5° – 157.5°  (octant 3)
        //   Left:         157.5° – 202.5°  (octant 4)
        //   Bottom+Left:  202.5° – 247.5°  (octant 5)
        //   Bottom:       247.5° – 292.5°  (octant 6)
        //   Bottom+Right: 292.5° – 337.5°  (octant 7)
        const float PI_8 = 0.3926991f;  // PI/8
        int octant = static_cast<int>( ( angle + PI_8 ) / ( 2 * PI_8 ) ) % 8;

        static const UINT octantEdge[8] = {
            EdgeRight,                 // 0: right
            EdgeRight | EdgeTop,       // 1: top-right
            EdgeTop,                   // 2: top
            EdgeLeft  | EdgeTop,       // 3: top-left
            EdgeLeft,                  // 4: left
            EdgeLeft  | EdgeBottom,    // 5: bottom-left
            EdgeBottom,                // 6: bottom
            EdgeRight | EdgeBottom,    // 7: bottom-right
        };
        return octantEdge[octant];
    }

    // Rectangular / rounded-rect: use rectangular edge zones.
    UINT edge = EdgeNone;
    if( x <= EDGE_GRAB )                    edge |= EdgeLeft;
    if( x >= w - EDGE_GRAB )                edge |= EdgeRight;
    if( y <= EDGE_GRAB )                    edge |= EdgeTop;
    if( y >= h - EDGE_GRAB )                edge |= EdgeBottom;
    return edge;
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::CursorForEdge
//
// Returns the system cursor ID appropriate for the given resize edge(s).
//----------------------------------------------------------------------------
LPCTSTR WebcamPreviewWindow::CursorForEdge( UINT edge ) const
{
    switch( edge )
    {
    case EdgeLeft:
    case EdgeRight:
        return IDC_SIZEWE;
    case EdgeTop:
    case EdgeBottom:
        return IDC_SIZENS;
    case EdgeLeft  | EdgeTop:
    case EdgeRight | EdgeBottom:
        return IDC_SIZENWSE;
    case EdgeRight | EdgeTop:
    case EdgeLeft  | EdgeBottom:
        return IDC_SIZENESW;
    default:
        return IDC_SIZEALL;   // interior = move
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::OnLButtonDown
//----------------------------------------------------------------------------
void WebcamPreviewWindow::OnLButtonDown( int x, int y )
{
    // Bring the window to the very top of the topmost band on click so
    // it can't get stuck behind another topmost window (e.g. the live zoom).
    SetWindowPos( m_hwnd, HWND_TOPMOST, 0, 0, 0, 0,
                  SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE );

    UINT edge = HitTestEdge( x, y );
    if( edge != EdgeNone )
    {
        // Start resizing.
        m_resizing = true;
        m_resizeEdge = edge;

        GetWindowRect( m_hwnd, &m_resizeStartRect );

        POINT screenPt = { x, y };
        ClientToScreen( m_hwnd, &screenPt );
        m_resizeStartPt = screenPt;

        // Capture the current aspect ratio.
        int w = m_resizeStartRect.right - m_resizeStartRect.left;
        int h = m_resizeStartRect.bottom - m_resizeStartRect.top;
        m_aspectRatio = ( h > 0 ) ? static_cast<double>( w ) / h : 1.0;

        SetCapture( m_hwnd );
    }
    else
    {
        // Start dragging.
        m_dragging = true;
        m_dragOffset = { x, y };
        SetCapture( m_hwnd );
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::OnMouseMove
//----------------------------------------------------------------------------
void WebcamPreviewWindow::OnMouseMove( int x, int y )
{
    if( m_resizing )
    {
        POINT cursor = { x, y };
        ClientToScreen( m_hwnd, &cursor );

        int dx = cursor.x - m_resizeStartPt.x;
        int dy = cursor.y - m_resizeStartPt.y;

        RECT r = m_resizeStartRect;

        // Apply the delta to the grabbed edge(s).
        if( m_resizeEdge & EdgeLeft )   r.left   += dx;
        if( m_resizeEdge & EdgeRight )  r.right  += dx;
        if( m_resizeEdge & EdgeTop )    r.top    += dy;
        if( m_resizeEdge & EdgeBottom ) r.bottom += dy;

        // Enforce minimum size before aspect-ratio correction.
        int newW = r.right - r.left;
        int newH = r.bottom - r.top;
        if( newW < MIN_SIZE ) newW = MIN_SIZE;
        if( newH < MIN_SIZE ) newH = MIN_SIZE;

        // Preserve aspect ratio.  The "dominant" axis is whichever the
        // user is dragging; adjust the other axis to match.
        bool horzEdge = ( m_resizeEdge & ( EdgeLeft | EdgeRight ) ) != 0;
        bool vertEdge = ( m_resizeEdge & ( EdgeTop | EdgeBottom ) ) != 0;

        if( horzEdge && !vertEdge )
        {
            // Horizontal edge only — height follows width.
            newH = static_cast<int>( newW / m_aspectRatio + 0.5 );
            if( newH < MIN_SIZE ) { newH = MIN_SIZE; newW = static_cast<int>( newH * m_aspectRatio + 0.5 ); }
        }
        else if( vertEdge && !horzEdge )
        {
            // Vertical edge only — width follows height.
            newW = static_cast<int>( newH * m_aspectRatio + 0.5 );
            if( newW < MIN_SIZE ) { newW = MIN_SIZE; newH = static_cast<int>( newW / m_aspectRatio + 0.5 ); }
        }
        else
        {
            // Corner drag — pick the axis with the larger delta.
            if( abs( dx ) >= abs( dy ) )
            {
                newH = static_cast<int>( newW / m_aspectRatio + 0.5 );
                if( newH < MIN_SIZE ) { newH = MIN_SIZE; newW = static_cast<int>( newH * m_aspectRatio + 0.5 ); }
            }
            else
            {
                newW = static_cast<int>( newH * m_aspectRatio + 0.5 );
                if( newW < MIN_SIZE ) { newW = MIN_SIZE; newH = static_cast<int>( newW / m_aspectRatio + 0.5 ); }
            }
        }

        // Anchor the non-moving edges.
        if( m_resizeEdge & EdgeLeft )
            r.left = r.right - newW;
        else
            r.right = r.left + newW;

        if( m_resizeEdge & EdgeTop )
            r.top = r.bottom - newH;
        else
            r.bottom = r.top + newH;

        // During resize, keep the full bounding rect within the
        // recording region so the webcam image never extends beyond it.
        if( r.left < m_screenRect.left )
        {
            r.left = m_screenRect.left;
            newW = r.right - r.left;
            newH = static_cast<int>( newW / m_aspectRatio + 0.5 );
            if( m_resizeEdge & EdgeTop )
                r.top = r.bottom - newH;
            else
                r.bottom = r.top + newH;
        }
        if( r.top < m_screenRect.top )
        {
            r.top = m_screenRect.top;
            newH = r.bottom - r.top;
            newW = static_cast<int>( newH * m_aspectRatio + 0.5 );
            if( m_resizeEdge & EdgeLeft )
                r.left = r.right - newW;
            else
                r.right = r.left + newW;
        }
        if( r.right > m_screenRect.right )
        {
            r.right = m_screenRect.right;
            newW = r.right - r.left;
            newH = static_cast<int>( newW / m_aspectRatio + 0.5 );
            if( m_resizeEdge & EdgeTop )
                r.top = r.bottom - newH;
            else
                r.bottom = r.top + newH;
        }
        if( r.bottom > m_screenRect.bottom )
        {
            r.bottom = m_screenRect.bottom;
            newH = r.bottom - r.top;
            newW = static_cast<int>( newH * m_aspectRatio + 0.5 );
            if( m_resizeEdge & EdgeLeft )
                r.left = r.right - newW;
            else
                r.right = r.left + newW;
        }

        // Reassert HWND_TOPMOST during resize so the live zoom window
        // (which also uses HWND_TOPMOST every 20 ms) can't push us behind.
        SetWindowPos( m_hwnd, HWND_TOPMOST, r.left, r.top,
                      r.right - r.left, r.bottom - r.top,
                      SWP_NOACTIVATE );

        SyncOverlayPosition();
        return;
    }

    if( !m_dragging )
        return;

    // Compute new window position from cursor.
    POINT cursor = { x, y };
    ClientToScreen( m_hwnd, &cursor );

    RECT wndRect;
    GetWindowRect( m_hwnd, &wndRect );
    int wndW = wndRect.right - wndRect.left;
    int wndH = wndRect.bottom - wndRect.top;

    int newX = cursor.x - m_dragOffset.x;
    int newY = cursor.y - m_dragOffset.y;

    // Constrain to recording region.
    if( newX < m_screenRect.left )
        newX = m_screenRect.left;
    if( newY < m_screenRect.top )
        newY = m_screenRect.top;
    if( newX + wndW > m_screenRect.right )
        newX = m_screenRect.right - wndW;
    if( newY + wndH > m_screenRect.bottom )
        newY = m_screenRect.bottom - wndH;

    // Reassert HWND_TOPMOST during drag so the live zoom window can't
    // push us behind it mid-drag.
    SetWindowPos( m_hwnd, HWND_TOPMOST, newX, newY, 0, 0,
                  SWP_NOSIZE | SWP_NOACTIVATE );

    // Update the recording overlay position to match.
    SyncOverlayPosition();
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::OnLButtonUp
//----------------------------------------------------------------------------
void WebcamPreviewWindow::OnLButtonUp()
{
    if( m_dragging )
    {
        m_dragging = false;
        ReleaseCapture();
        SyncOverlayPosition();
    }
    if( m_resizing )
    {
        m_resizing = false;
        m_resizeEdge = EdgeNone;
        ReleaseCapture();
        SyncOverlayPosition();
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::SyncOverlayPosition
//
// Maps the preview window's current screen position and size back to
// recording-output coordinates and updates WebcamCapture's destRect.
//----------------------------------------------------------------------------
void WebcamPreviewWindow::SyncOverlayPosition()
{
    if( !m_capture || !m_hwnd )
        return;

    RECT wndRect;
    GetWindowRect( m_hwnd, &wndRect );

    int screenW = m_screenRect.right - m_screenRect.left;
    int screenH = m_screenRect.bottom - m_screenRect.top;
    int outW = static_cast<int>( m_outputWidth );
    int outH = static_cast<int>( m_outputHeight );

    if( screenW <= 0 || screenH <= 0 )
        return;

    // Map from screen coordinates back to output coordinates.
    RECT dest;
    dest.left   = MulDiv( wndRect.left   - m_screenRect.left, outW, screenW );
    dest.top    = MulDiv( wndRect.top    - m_screenRect.top,  outH, screenH );
    dest.right  = MulDiv( wndRect.right  - m_screenRect.left, outW, screenW );
    dest.bottom = MulDiv( wndRect.bottom - m_screenRect.top,  outH, screenH );

    // Clamp to output bounds.
    if( dest.left < 0 ) { dest.right -= dest.left; dest.left = 0; }
    if( dest.top < 0 )  { dest.bottom -= dest.top; dest.top = 0; }
    if( dest.right > outW )  { dest.left -= (dest.right - outW); dest.right = outW; }
    if( dest.bottom > outH ) { dest.top -= (dest.bottom - outH); dest.bottom = outH; }

    if( m_resizing )
    {
        // Resize: update both position and overlay pixel dimensions so
        // the capture thread pre-scales to the new size.
        m_capture->SetDestRectAndSize( dest );
    }
    else
    {
        // Drag (position-only): do NOT touch m_overlayW / m_overlayH.
        // Changing them racily can cause GetLatestPixels to return
        // dimensions that don't match the pixel buffer, which pushes
        // us into the StretchBlt path where GDI may zero the alpha
        // channel and make the window invisible.
        m_capture->SetDestRect( dest );
    }
}

//----------------------------------------------------------------------------
// WebcamPreviewWindow::WndProc
//----------------------------------------------------------------------------
LRESULT CALLBACK WebcamPreviewWindow::WndProc( HWND hwnd, UINT msg, WPARAM wParam, LPARAM lParam )
{
    WebcamPreviewWindow* self = nullptr;

    if( msg == WM_NCCREATE )
    {
        auto cs = reinterpret_cast<CREATESTRUCTW*>( lParam );
        self = static_cast<WebcamPreviewWindow*>( cs->lpCreateParams );
        SetWindowLongPtrW( hwnd, GWLP_USERDATA, reinterpret_cast<LONG_PTR>( self ) );
    }
    else
    {
        self = reinterpret_cast<WebcamPreviewWindow*>( GetWindowLongPtrW( hwnd, GWLP_USERDATA ) );
    }

    if( self )
    {
        switch( msg )
        {
        case WM_TIMER:
            if( wParam == TIMER_ID )
            {
                self->OnTimer();
                return 0;
            }
            break;

        case WM_PAINT:
            self->OnPaint();
            return 0;

        case WM_ERASEBKGND:
            return 1;  // Handled — avoid flicker.

        case WM_LBUTTONDOWN:
            self->OnLButtonDown( GET_X_LPARAM( lParam ), GET_Y_LPARAM( lParam ) );
            return 0;

        case WM_MOUSEMOVE:
            self->OnMouseMove( GET_X_LPARAM( lParam ), GET_Y_LPARAM( lParam ) );
            return 0;

        case WM_LBUTTONUP:
            self->OnLButtonUp();
            return 0;

        case WM_CAPTURECHANGED:
            // Another window stole capture.  Cancel the current
            // drag/resize cleanly so the user can start again.
            if( self->m_dragging || self->m_resizing )
            {
                OutputDebug( L"[WebcamPreview] Capture lost during %s\n",
                             self->m_resizing ? L"resize" : L"drag" );
                self->m_dragging = false;
                self->m_resizing = false;
                self->m_resizeEdge = EdgeNone;
                self->SyncOverlayPosition();
            }
            return 0;

        case WM_SETCURSOR:
            if( LOWORD( lParam ) == HTCLIENT )
            {
                POINT pt;
                GetCursorPos( &pt );
                ScreenToClient( hwnd, &pt );
                UINT edge = self->HitTestEdge( pt.x, pt.y );
                SetCursor( LoadCursor( nullptr, self->CursorForEdge( edge ) ) );
                return TRUE;
            }
            break;

        case WM_DESTROY:
            self->m_hwnd = nullptr;
            return 0;
        }
    }

    return DefWindowProcW( hwnd, msg, wParam, lParam );
}
