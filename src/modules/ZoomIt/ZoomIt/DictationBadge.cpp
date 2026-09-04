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
    SetLayeredWindowAttributes( m_window.get(), 0, c_alpha, LWA_ALPHA );

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
        if( m_text == text )
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
        if( m_status == status )
        {
            return;
        }
        m_status = status;
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
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        status = m_status;
        text = m_text;
    }

    int lines = status.empty() ? 0 : 1;

    // Wrap the transcription at the maximum width, capped at three lines so the
    // badge never dominates the screen.
    if( !text.empty() )
    {
        HDC deviceContext = GetDC( m_window.get() );
        if( deviceContext != nullptr )
        {
            HGDIOBJ previousFont = SelectObject( deviceContext, m_font.get() );
            RECT measure{ 0, 0, maximumWidth - 2 * padding, 0 };
            DrawTextW( deviceContext, text.c_str(), -1, &measure, DT_CALCRECT | DT_WORDBREAK | DT_NOPREFIX );
            SelectObject( deviceContext, previousFont );
            ReleaseDC( m_window.get(), deviceContext );

            int textLines = ( measure.bottom + lineHeight - 1 ) / lineHeight;
            if( textLines < 1 )
            {
                textLines = 1;
            }
            else if( textLines > 3 )
            {
                textLines = 3;
            }
            lines += textLines;
        }
        else
        {
            lines += 1;
        }
    }

    if( lines == 0 )
    {
        lines = 1;
    }

    SIZE size{};
    size.cx = maximumWidth;
    size.cy = lines * lineHeight + 2 * padding;
    return size;
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
    const SIZE size = MeasureContent();
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

    SetWindowPos( m_window.get(), HWND_TOPMOST, x, y, size.cx, size.cy,
                  SWP_NOACTIVATE | SWP_SHOWWINDOW );
    RedrawWindow( m_window.get(), nullptr, nullptr, RDW_INVALIDATE | RDW_UPDATENOW );
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
    {
        std::lock_guard<std::mutex> guard( m_textLock );
        status = m_status;
        text = m_text;
    }

    HGDIOBJ previousFont = SelectObject( deviceContext, m_font.get() );
    SetBkMode( deviceContext, TRANSPARENT );

    RECT line{ client.left + padding, client.top + padding, client.right - padding, client.bottom - padding };

    if( !status.empty() )
    {
        SetTextColor( deviceContext, c_statusColor );
        RECT statusRect = line;
        statusRect.bottom = statusRect.top + lineHeight;
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
        PAINTSTRUCT paint;
        HDC deviceContext = BeginPaint( window, &paint );
        RECT client;
        GetClientRect( window, &client );
        Paint( deviceContext, client );
        EndPaint( window, &paint );
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
