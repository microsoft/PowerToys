//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Dictation status overlay.
//
//==============================================================================
#include "pch.h"
#include "DictationBadge.h"
#include "Utility.h"

namespace
{
    constexpr int c_paddingDips = 10;
    constexpr int c_gapDips = 8;
    constexpr int c_maximumWidthDips = 520;
    constexpr int c_lineHeightDips = 20;
    constexpr int c_spinnerSizeDips = 16;
    constexpr unsigned int c_spinnerFrames = 24;
    constexpr COLORREF c_backgroundColor = RGB( 32, 32, 32 );
    constexpr COLORREF c_textColor = RGB( 240, 240, 240 );
    constexpr COLORREF c_statusColor = RGB( 255, 222, 0 );
    constexpr BYTE c_alpha = 232;

    // Custom message used to marshal text updates from the recognition worker
    // thread onto the thread that owns the badge window.
    constexpr UINT WM_BADGE_REFRESH = WM_USER + 1;

    bool IsWindowThread( HWND window )
    {
        return GetWindowThreadProcessId( window, nullptr ) == GetCurrentThreadId();
    }
}

//----------------------------------------------------------------------------
//
// DictationBadge::WindowProcThunk
//
//----------------------------------------------------------------------------
LRESULT CALLBACK DictationBadge::WindowProcThunk( HWND window, UINT message, WPARAM wordParam, LPARAM longParam )
{
    if( message == WM_NCCREATE )
    {
        auto createStruct = reinterpret_cast<LPCREATESTRUCT>(longParam);
        SetWindowLongPtrW( window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(createStruct->lpCreateParams) );
        return DefWindowProcW( window, message, wordParam, longParam );
    }

    auto self = reinterpret_cast<DictationBadge*>(GetWindowLongPtrW( window, GWLP_USERDATA ));
    if( self == nullptr )
    {
        return DefWindowProcW( window, message, wordParam, longParam );
    }
    return self->WindowProc( window, message, wordParam, longParam );
}

//----------------------------------------------------------------------------
//
// DictationBadge::Show
//
//----------------------------------------------------------------------------
void DictationBadge::Show( HWND owner, const RECT& anchor )
{
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        if( m_status.empty() )
        {
            m_status = PreparingStatus;
        }
    }

    if( m_window )
    {
        Reposition( anchor );
        return;
    }

    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = &DictationBadge::WindowProcThunk;
    windowClass.hInstance = GetModuleHandle( nullptr );
    windowClass.hCursor = LoadCursorW( nullptr, IDC_ARROW );
    windowClass.hbrBackground = nullptr;
    windowClass.lpszClassName = m_className;
    if( RegisterClassW( &windowClass ) == 0 && GetLastError() != ERROR_CLASS_ALREADY_EXISTS )
    {
        return;
    }

    m_anchor = anchor;
    m_window = wil::unique_hwnd( CreateWindowExW(
        WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE,
        m_className, nullptr, WS_POPUP,
        anchor.left, anchor.bottom, 10, 10, owner, nullptr, nullptr, this ) );
    if( !m_window )
    {
        return;
    }

    // Keep the badge out of the captured image and out of recordings.
    SetWindowDisplayAffinity( m_window.get(), WDA_EXCLUDEFROMCAPTURE );

    m_dpi = GetDpiForWindowHelper( m_window.get() );
    m_font.reset( CreateFontW( -ScaleForDpi( 13, m_dpi ), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                               DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                               CLEARTYPE_QUALITY, VARIABLE_PITCH, L"Segoe UI" ) );

    Reposition( anchor );
    ShowWindow( m_window.get(), SW_SHOWNA );
}

//----------------------------------------------------------------------------
//
// DictationBadge::SetText
//
//----------------------------------------------------------------------------
void DictationBadge::SetText( const std::wstring& text )
{
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        if( m_transcribing || m_text == text )
        {
            return;
        }
        m_text = text;
    }

    Refresh();
}

//----------------------------------------------------------------------------
//
// DictationBadge::SetStatus
//
//----------------------------------------------------------------------------
void DictationBadge::SetStatus( const std::wstring& status )
{
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        if( m_transcribing || m_status == status )
        {
            return;
        }
        m_status = status;
    }

    Refresh();
}

void DictationBadge::BeginTranscribing()
{
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        if( m_transcribing )
        {
            return;
        }
        m_transcribing = true;
        m_spinnerFrame = 0;
        m_status = L"Transcribing - Press Esc to cancel.";
        m_text.clear();
    }

    Refresh();
}

void DictationBadge::AdvanceTranscribingAnimation()
{
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        if( !m_transcribing )
        {
            return;
        }
        m_spinnerFrame = ( m_spinnerFrame + 1 ) % c_spinnerFrames;
    }

    Refresh();
}

//----------------------------------------------------------------------------
//
// DictationBadge::Refresh
//
// Repaints immediately when called on the thread that owns the badge, and
// otherwise marshals the update to it. The owning thread spends the drag inside
// a modal message loop, so a posted message is picked up promptly; once the drag
// ends nothing pumps messages any more, which is why the same thread case has to
// paint directly.
//
//----------------------------------------------------------------------------
void DictationBadge::Refresh()
{
    if( !m_window )
    {
        return;
    }

    if( IsWindowThread( m_window.get() ) )
    {
        Reposition( m_anchor );
    }
    else
    {
        PostMessageW( m_window.get(), WM_BADGE_REFRESH, 0, 0 );
    }
}

//----------------------------------------------------------------------------
//
// DictationBadge::MeasureContent
//
//----------------------------------------------------------------------------
SIZE DictationBadge::MeasureContent() const
{
    const int padding = ScaleForDpi( c_paddingDips, m_dpi );
    const int lineHeight = ScaleForDpi( c_lineHeightDips, m_dpi );
    const int maximumWidth = ScaleForDpi( c_maximumWidthDips, m_dpi );

    std::wstring status;
    std::wstring text;
    bool transcribing;
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        status = m_status;
        text = m_text;
        transcribing = m_transcribing;
    }

    wil::unique_hdc deviceContext{ CreateCompatibleDC( nullptr ) };
    THROW_LAST_ERROR_IF_NULL( deviceContext.get() );
    auto previousFont = wil::SelectObject( deviceContext.get(), m_font.get() );

    const int maximumContentWidth = maximumWidth - 2 * padding;
    RECT statusSize{};
    DrawTextW( deviceContext.get(), status.c_str(), -1, &statusSize,
               DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX );
    if( transcribing )
    {
        statusSize.right += ScaleForDpi( c_spinnerSizeDips + c_gapDips, m_dpi );
    }
    else if( status == PreparingStatus || status == ListeningStatus )
    {
        // Reserve both labels from the first frame so microphone startup cannot resize the badge.
        const auto* alternateStatus = status == PreparingStatus ? ListeningStatus : PreparingStatus;
        RECT alternateSize{};
        DrawTextW( deviceContext.get(), alternateStatus, -1, &alternateSize,
                   DT_CALCRECT | DT_SINGLELINE | DT_NOPREFIX );
        statusSize.right = (std::max)( statusSize.right, alternateSize.right );
    }
    RECT textSize{ 0, 0, maximumContentWidth, 0 };
    DrawTextW( deviceContext.get(), text.c_str(), -1, &textSize,
               DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX );
    const LONG contentWidth = std::clamp<LONG>(
        (std::max)( statusSize.right, textSize.right ), 1, maximumContentWidth );

    // Measure again at the fitted width so wrapping and height match Paint.
    int lines = status.empty() ? 0 : 1;
    if( !text.empty() )
    {
        textSize = { 0, 0, contentWidth, 0 };
        DrawTextW( deviceContext.get(), text.c_str(), -1, &textSize,
                   DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX );
        lines += std::clamp<int>( ( textSize.bottom + lineHeight - 1 ) / lineHeight, 1, 3 );
    }

    return { contentWidth + 2 * padding, (std::max)( lines, 1 ) * lineHeight + 2 * padding };
}

//----------------------------------------------------------------------------
//
// DictationBadge::Reposition
//
// Centers the badge along the top edge of the selection, which keeps it near
// where the user is looking without covering the middle of the capture. The
// badge is excluded from capture, so sitting inside the selection does not
// affect the snip. When the selection is too short to hold the badge it moves
// just outside the top edge, and then below the selection if there is no room
// above either.
//
//----------------------------------------------------------------------------
void DictationBadge::Reposition( const RECT& anchor )
{
    if( !m_window )
    {
        return;
    }

    m_anchor = anchor;
    SIZE size = MeasureContent();
    const int gap = ScaleForDpi( c_gapDips, m_dpi );

    MONITORINFO monitorInfo{ sizeof( MONITORINFO ) };
    GetMonitorInfoW( MonitorFromRect( &anchor, MONITOR_DEFAULTTONEAREST ), &monitorInfo );

    int x = anchor.left + ( ( anchor.right - anchor.left ) - size.cx ) / 2;
    int y = anchor.top + gap;

    // A selection shorter than the badge would be completely covered by it.
    if( y + size.cy > anchor.bottom )
    {
        y = anchor.top - gap - size.cy;

        if( y < monitorInfo.rcMonitor.top )
        {
            y = anchor.bottom + gap;
        }
    }

    if( y + size.cy > monitorInfo.rcMonitor.bottom )
    {
        y = monitorInfo.rcMonitor.bottom - size.cy;
    }
    if( y < monitorInfo.rcMonitor.top )
    {
        y = monitorInfo.rcMonitor.top;
    }
    if( x + size.cx > monitorInfo.rcMonitor.right )
    {
        x = monitorInfo.rcMonitor.right - size.cx;
    }
    if( x < monitorInfo.rcMonitor.left )
    {
        x = monitorInfo.rcMonitor.left;
    }

    // WS_EX_TRANSPARENT defers WM_PAINT until windows below us have painted.
    // Publish pixels directly: finalization blocks the UI message loop, so an
    // invalidated selection must not leave the badge displaying "Listening".
    wil::unique_hdc memory{ CreateCompatibleDC( nullptr ) };
    THROW_LAST_ERROR_IF_NULL( memory.get() );
    BITMAPINFO info{};
    info.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
    info.bmiHeader.biWidth = size.cx;
    info.bmiHeader.biHeight = -size.cy;
    info.bmiHeader.biPlanes = 1;
    info.bmiHeader.biBitCount = 32;
    void* bits = nullptr;
    wil::unique_hbitmap bitmap{ CreateDIBSection( memory.get(), &info, DIB_RGB_COLORS, &bits, nullptr, 0 ) };
    THROW_LAST_ERROR_IF_NULL( bitmap.get() );
    auto previousBitmap = wil::SelectObject( memory.get(), bitmap.get() );
    Paint( memory.get(), { 0, 0, size.cx, size.cy } );

    POINT destination{ x, y };
    POINT source{};
    BLENDFUNCTION blend{ AC_SRC_OVER, 0, c_alpha, 0 };
    THROW_IF_WIN32_BOOL_FALSE( UpdateLayeredWindow(
        m_window.get(), nullptr, &destination, &size, memory.get(), &source, 0, &blend, ULW_ALPHA ) );
    SetWindowPos( m_window.get(), HWND_TOPMOST, 0, 0, 0, 0,
                  SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW );
}

//----------------------------------------------------------------------------
//
// DictationBadge::Hide
//
//----------------------------------------------------------------------------
void DictationBadge::Hide()
{
    HWND window = m_window.release();
    if( window != nullptr && IsWindow( window ) )
    {
        SetWindowLongPtrW( window, GWLP_USERDATA, 0 );
        DestroyWindow( window );
    }

    m_font.reset();

    std::lock_guard<std::mutex> guard( m_textLock );
    m_text.clear();
    m_status.clear();
    m_transcribing = false;
    m_spinnerFrame = 0;
}

void DictationBadge::PaintSpinner( HDC deviceContext, const RECT& bounds, unsigned int frame ) const
{
    const float stroke = static_cast<float>( ScaleForDpi( 2, m_dpi ) );
    const Gdiplus::RectF ring{
        bounds.left + stroke / 2,
        bounds.top + stroke / 2,
        bounds.right - bounds.left - stroke,
        bounds.bottom - bounds.top - stroke,
    };
    Gdiplus::Graphics graphics( deviceContext );
    graphics.SetSmoothingMode( Gdiplus::SmoothingModeAntiAlias );
    Gdiplus::Pen track( Gdiplus::Color( 255, 80, 80, 80 ), stroke );
    Gdiplus::Pen arc(
        Gdiplus::Color( 255, GetRValue( c_statusColor ), GetGValue( c_statusColor ), GetBValue( c_statusColor ) ),
        stroke );
    arc.SetStartCap( Gdiplus::LineCapRound );
    arc.SetEndCap( Gdiplus::LineCapRound );
    graphics.DrawEllipse( &track, ring );
    graphics.DrawArc( &arc, ring, frame * ( 360.0f / c_spinnerFrames ) - 90.0f, 100.0f );
}

//----------------------------------------------------------------------------
//
// DictationBadge::Paint
//
//----------------------------------------------------------------------------
void DictationBadge::Paint( HDC deviceContext, const RECT& client )
{
    const int padding = ScaleForDpi( c_paddingDips, m_dpi );
    const int lineHeight = ScaleForDpi( c_lineHeightDips, m_dpi );

    wil::unique_hbrush background{ CreateSolidBrush( c_backgroundColor ) };
    FillRect( deviceContext, &client, background.get() );

    // A thin accent border, matching the selection rectangle's highlight.
    wil::unique_hbrush border{ CreateSolidBrush( c_statusColor ) };
    FrameRect( deviceContext, &client, border.get() );

    std::wstring status;
    std::wstring text;
    bool transcribing;
    unsigned int spinnerFrame;
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        status = m_status;
        text = m_text;
        transcribing = m_transcribing;
        spinnerFrame = m_spinnerFrame;
    }

    HGDIOBJ previousFont = SelectObject( deviceContext, m_font.get() );
    SetBkMode( deviceContext, TRANSPARENT );

    RECT line{ client.left + padding, client.top + padding, client.right - padding, client.bottom - padding };

    if( !status.empty() )
    {
        SetTextColor( deviceContext, c_statusColor );
        RECT statusRect = line;
        statusRect.bottom = statusRect.top + lineHeight;
        if( transcribing )
        {
            const int diameter = ScaleForDpi( c_spinnerSizeDips, m_dpi );
            const int top = statusRect.top + ( lineHeight - diameter ) / 2;
            PaintSpinner( deviceContext,
                          { statusRect.left, top, statusRect.left + diameter, top + diameter },
                          spinnerFrame );
            statusRect.left += diameter + ScaleForDpi( c_gapDips, m_dpi );
        }
        DrawTextW( deviceContext, status.c_str(), -1, &statusRect,
                   DT_SINGLELINE | DT_END_ELLIPSIS | DT_NOPREFIX | DT_VCENTER );
        line.top += lineHeight;
    }

    if( !text.empty() )
    {
        SetTextColor( deviceContext, c_textColor );
        DrawTextW( deviceContext, text.c_str(), -1, &line,
                   DT_WORDBREAK | DT_END_ELLIPSIS | DT_NOPREFIX );
    }

    SelectObject( deviceContext, previousFont );
}

//----------------------------------------------------------------------------
//
// DictationBadge::WindowProc
//
//----------------------------------------------------------------------------
LRESULT DictationBadge::WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam )
{
    switch( message )
    {
    case WM_BADGE_REFRESH:
        Reposition( m_anchor );
        return 0;

    case WM_DPICHANGED:
        m_dpi = HIWORD( wordParam );
        m_font.reset( CreateFontW( -ScaleForDpi( 13, m_dpi ), 0, 0, 0, FW_NORMAL, FALSE, FALSE, FALSE,
                                   DEFAULT_CHARSET, OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS,
                                   CLEARTYPE_QUALITY, VARIABLE_PITCH, L"Segoe UI" ) );
        Reposition( m_anchor );
        return 0;

    case WM_PAINT:
    {
        PAINTSTRUCT paint{};
        BeginPaint( window, &paint );
        EndPaint( window, &paint );
        return 0;
    }

    case WM_PRINTCLIENT:
    {
        RECT client{};
        GetClientRect( window, &client );
        Paint( reinterpret_cast<HDC>( wordParam ), client );
        return 0;
    }

    case WM_NCHITTEST:
        return HTTRANSPARENT;

    case WM_DESTROY:
        if( m_window.get() == window )
        {
            m_window.release();
        }
        return 0;
    }

    return DefWindowProcW( window, message, wordParam, longParam );
}
