//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// Class to select a recording rectangle and show it while recording
//
//==============================================================================
#include "pch.h"
#include "SelectRectangle.h"
#include "Utility.h"
#include "WindowsVersions.h"

static void SelectRectangleDebugLog( const wchar_t* format, ... )
{
#if _DEBUG
    wchar_t message[1024]{};
    va_list args;
    va_start( args, format );
    vswprintf_s( message, format, args );
    va_end( args );
    OutputDebugStringW( message );
#else
    UNREFERENCED_PARAMETER( format );
#endif
}

//----------------------------------------------------------------------------
//
// SelectRectangle::Start
//
//----------------------------------------------------------------------------
bool SelectRectangle::Start( HWND ownerWindow, bool fullMonitor )
{
    m_stopping = false;
    SelectRectangleDebugLog( L"[SelectRectangle] Start owner=%p fullMonitor=%d minSize=%d alpha=%u\n",
                             ownerWindow,
                             fullMonitor ? 1 : 0,
                             MinSize(),
                             Alpha() );
    WNDCLASSW windowClass{};
    windowClass.lpfnWndProc = []( HWND window, UINT message, WPARAM wordParam, LPARAM longParam ) -> LRESULT
    {
        if( message == WM_NCCREATE )
        {
            auto createStruct = reinterpret_cast<LPCREATESTRUCT>(longParam);
            SetWindowLongPtrW( window, GWLP_USERDATA, reinterpret_cast<LONG_PTR>(createStruct->lpCreateParams) );
            return TRUE;
        }

        auto self = reinterpret_cast<SelectRectangle*>(GetWindowLongPtrW( window, GWLP_USERDATA ));
        return self->WindowProc( window, message, wordParam, longParam );
    };
    windowClass.hInstance = GetModuleHandle( nullptr );
    windowClass.hCursor = LoadCursorW( nullptr, IDC_CROSS );
    windowClass.hbrBackground = static_cast<HBRUSH>(GetStockObject( BLACK_BRUSH ));
    windowClass.lpszClassName = m_className;
    if( RegisterClassW( &windowClass ) == 0 )
    {
        THROW_LAST_ERROR_IF( GetLastError() != ERROR_CLASS_ALREADY_EXISTS );

        WNDCLASSW existingClass{};
        THROW_IF_WIN32_BOOL_FALSE( GetClassInfoW( GetModuleHandle( nullptr ), m_className, &existingClass ) );
        THROW_LAST_ERROR_IF( existingClass.lpfnWndProc != windowClass.lpfnWndProc );
    }

    m_cancel = false;
    auto rect = GetMonitorRectFromCursor();
    SelectRectangleDebugLog( L"[SelectRectangle] Monitor rect=(%ld,%ld)-(%ld,%ld)\n",
                             rect.left,
                             rect.top,
                             rect.right,
                             rect.bottom );
    m_window = wil::unique_hwnd( CreateWindowExW( WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST, m_className, nullptr, WS_POPUP,
                                                  rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, ownerWindow,
                                                  nullptr, nullptr, this ) );
    THROW_LAST_ERROR_IF_NULL( m_window.get() );
    SelectRectangleDebugLog( L"[SelectRectangle] Window created hwnd=%p\n", m_window.get() );

    if( fullMonitor )
    {
        m_selectedRect = rect;
        ShowSelected();
    }
    else
    {
        const BOOL layered = SetLayeredWindowAttributes( m_window.get(), 0, Alpha(), LWA_ALPHA );
        SelectRectangleDebugLog( L"[SelectRectangle] SetLayeredWindowAttributes(alpha=%u) success=%d err=%lu\n",
                                 Alpha(),
                                 layered ? 1 : 0,
                                 layered ? 0 : GetLastError() );
    }

    ShowWindow( m_window.get(), SW_SHOW );
    SetForegroundWindow( m_window.get() );

    if( !fullMonitor )
    {
        GetClipCursor( &m_oldClipRect );
        ClipCursor( &rect );
        m_setClip = true;
        SelectRectangleDebugLog( L"[SelectRectangle] Cursor clipped to monitor bounds\n" );
    }

    MSG message;
    while( GetMessageW( &message, nullptr, 0, 0 ) != 0 )
    {
        TranslateMessage( &message );
        DispatchMessageW( &message );
        if( m_cancel )
        {
            SelectRectangleDebugLog( L"[SelectRectangle] Start cancelled via Stop()\n" );
            return false;
        }
        if( m_selected )
        {
            SelectRectangleDebugLog( L"[SelectRectangle] Selection finalized rect=(%ld,%ld)-(%ld,%ld)\n",
                                     m_selectedRect.left,
                                     m_selectedRect.top,
                                     m_selectedRect.right,
                                     m_selectedRect.bottom );
            break;
        }
    }
    SelectRectangleDebugLog( L"[SelectRectangle] Start complete selected=%d cancel=%d\n", m_selected ? 1 : 0, m_cancel ? 1 : 0 );
    return true;
}

//----------------------------------------------------------------------------
//
// SelectRectangle::Stop
//
//----------------------------------------------------------------------------
void SelectRectangle::Stop()
{
    if( m_stopping )
    {
        SelectRectangleDebugLog( L"[SelectRectangle] Stop ignored due to reentrancy\n" );
        return;
    }

    m_stopping = true;
    SelectRectangleDebugLog( L"[SelectRectangle] Stop hwnd=%p selected=%d cancel=%d clip=%d rect=(%ld,%ld)-(%ld,%ld)\n",
                             m_window.get(),
                             m_selected ? 1 : 0,
                             m_cancel ? 1 : 0,
                             m_setClip ? 1 : 0,
                             m_selectedRect.left,
                             m_selectedRect.top,
                             m_selectedRect.right,
                             m_selectedRect.bottom );
    if( m_setClip )
    {
        ClipCursor( &m_oldClipRect );
        m_setClip = false;
    }

    HWND window = m_window.release();
    if( window != nullptr && IsWindow( window ) )
    {
        DestroyWindow( window );
    }

    m_selected = false;
    m_selectedRect = {};
    m_cancel = true;
    m_stopping = false;
}

//----------------------------------------------------------------------------
//
// SelectRectangle::ShowSelected
//
//----------------------------------------------------------------------------
void SelectRectangle::ShowSelected()
{
    m_selected = true;
    SelectRectangleDebugLog( L"[SelectRectangle] ShowSelected rect=(%ld,%ld)-(%ld,%ld) dpi=%u\n",
                             m_selectedRect.left,
                             m_selectedRect.top,
                             m_selectedRect.right,
                             m_selectedRect.bottom,
                             m_dpi );

    // Set the alpha to match the Windows graphics capture API yellow border
    // and set the window to be transparent and disabled, so it will be skipped
    // for hit testing and as a candidate for the next foreground window.
    const BOOL layered = SetLayeredWindowAttributes( m_window.get(), 0, 191, LWA_ALPHA );
    SelectRectangleDebugLog( L"[SelectRectangle] ShowSelected SetLayeredWindowAttributes(alpha=191) success=%d err=%lu\n",
                             layered ? 1 : 0,
                             layered ? 0 : GetLastError() );
    SetWindowLong( m_window.get(), GWL_EXSTYLE, GetWindowLong( m_window.get(), GWL_EXSTYLE ) | WS_EX_TRANSPARENT );
    EnableWindow( m_window.get(), FALSE );

    POINT point{ m_selectedRect.left, m_selectedRect.top };
    auto rect = m_selectedRect;
    OffsetRect( &rect, -rect.left, -rect.top );
    int width = ScaleForDpi( 2, m_dpi );

    // Draw the selection border outside the selected rectangle on builds lower
    // than Windows 11 22H2 because the graphics capture API does not skip
    // windows if layered, meaning this yellow border will be captured.
    if( GetWindowsBuild( nullptr ) < BUILD_WINDOWS_11_22H2 )
    {
        InflateRect( &rect, width, width );
        OffsetRect( &rect, -rect.left, -rect.top );
        point.x -= width;
        point.y -= width;
    }

    // Resize the window to the selection rectangle and translate the position.
    RECT windowRect;
    GetWindowRect( m_window.get(), &windowRect );
    point.x += windowRect.left;
    point.y += windowRect.top;
    MoveWindow( m_window.get(), point.x, point.y, rect.right, rect.bottom, true );
    SelectRectangleDebugLog( L"[SelectRectangle] Border window moved to (%ld,%ld) size=%ldx%ld borderWidth=%d\n",
                             point.x,
                             point.y,
                             rect.right,
                             rect.bottom,
                             width );

    // Use a region to keep everything but the border transparent.
    wil::unique_hrgn region{CreateRectRgnIndirect( &rect )};
    InflateRect( &rect, -width, -width );
    wil::unique_hrgn insideRegion{CreateRectRgnIndirect( &rect )};
    CombineRgn( region.get(), region.get(), insideRegion.get(), RGN_XOR );
    SetWindowRgn( m_window.get(), region.release(), true );
    SelectRectangleDebugLog( L"[SelectRectangle] Border window region applied\n" );

    // Force immediate paint so the yellow border is visible instead of a
    // transient black frame from the class background brush.
    RedrawWindow( m_window.get(), nullptr, nullptr, RDW_INVALIDATE | RDW_UPDATENOW | RDW_FRAME );
}

//----------------------------------------------------------------------------
//
// SelectRectangle::UpdateOwner
//
//----------------------------------------------------------------------------
void SelectRectangle::UpdateOwner( HWND window )
{
    if( m_window != nullptr )
    {
        SelectRectangleDebugLog( L"[SelectRectangle] UpdateOwner hwnd=%p newOwner=%p\n", m_window.get(), window );
        SetWindowLongPtr( m_window.get(), GWLP_HWNDPARENT, reinterpret_cast<LONG_PTR>(window) );
        SetWindowPos( m_window.get(), HWND_TOPMOST, 0, 0, 0, 0, SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE );
    }
}

//----------------------------------------------------------------------------
//
// SelectRectangle::WindowProc
//
//----------------------------------------------------------------------------
LRESULT SelectRectangle::WindowProc( HWND window, UINT message, WPARAM wordParam, LPARAM longParam )
{
    switch( message )
    {
    case WM_CREATE:
        m_dpi = GetDpiForWindowHelper( window );
        SetWindowDisplayAffinity( window, WDA_EXCLUDEFROMCAPTURE );
        SelectRectangleDebugLog( L"[SelectRectangle] WM_CREATE hwnd=%p dpi=%u\n", window, m_dpi );
        return 0;

    case WM_DESTROY:
        SelectRectangleDebugLog( L"[SelectRectangle] WM_DESTROY hwnd=%p\n", window );
        if( m_window.get() == window )
        {
            m_window.release();
        }
        if( m_setClip )
        {
            ClipCursor( &m_oldClipRect );
            m_setClip = false;
        }
        m_selected = false;
        m_selectedRect = {};
        m_cancel = true;
        m_stopping = false;
        return 0;

    case WM_LBUTTONDOWN:
    {
        SetCapture( window );

        m_startPoint = { GET_X_LPARAM( longParam ), GET_Y_LPARAM( longParam ) };
        SelectRectangleDebugLog( L"[SelectRectangle] WM_LBUTTONDOWN startPoint=(%ld,%ld)\n", m_startPoint.x, m_startPoint.y );
        [[fallthrough]];
    }
    case WM_MOUSEMOVE:
        if( GetCapture() == window )
        {
            RECT rect;
            GetClientRect( window, &rect );
            POINT point{ GET_X_LPARAM( longParam ), GET_Y_LPARAM( longParam ) };
            m_selectedRect = ForceRectInBounds( RectFromPointsMinSize( m_startPoint, point, MinSize() ), rect );
            SelectRectangleDebugLog( L"[SelectRectangle] Drag rect=(%ld,%ld)-(%ld,%ld)\n",
                                     m_selectedRect.left,
                                     m_selectedRect.top,
                                     m_selectedRect.right,
                                     m_selectedRect.bottom );

            // Use a region to carve out the selected rectangle.
            wil::unique_hrgn region{CreateRectRgnIndirect( &m_selectedRect )};
            wil::unique_hrgn clientRegion{CreateRectRgnIndirect( &rect )};
            CombineRgn( region.get(), region.get(), clientRegion.get(), RGN_XOR );
            SetWindowRgn( window, region.release(), true );
        }
        return 0;

    case WM_KEYDOWN:
        if( wordParam == VK_ESCAPE )
        {
            SelectRectangleDebugLog( L"[SelectRectangle] WM_KEYDOWN Escape pressed\n" );
            Stop();
        }
        return 0;

    case WM_KILLFOCUS:
        if( !m_selected )
        {
            SelectRectangleDebugLog( L"[SelectRectangle] WM_KILLFOCUS before selection complete\n" );
            Stop();
        }
        return 0;

    case WM_LBUTTONUP:
    {
        SelectRectangleDebugLog( L"[SelectRectangle] WM_LBUTTONUP selectedRect=(%ld,%ld)-(%ld,%ld)\n",
                                 m_selectedRect.left,
                                 m_selectedRect.top,
                                 m_selectedRect.right,
                                 m_selectedRect.bottom );
        if( m_setClip )
        {
            ClipCursor( &m_oldClipRect );
            m_setClip = false;
        }
        ReleaseCapture();

        ShowSelected();
        return 0;
    }
    case WM_NCHITTEST:
        if( m_selected )
        {
            return HTTRANSPARENT;
        }
        break;

    case WM_PAINT:
        if( m_selected )
        {
            PAINTSTRUCT paint;
            auto deviceContext = BeginPaint( window, &paint );

            RECT rect;
            GetClientRect( window, &rect );
            SelectRectangleDebugLog( L"[SelectRectangle] WM_PAINT selected border rect=(%ld,%ld)-(%ld,%ld)\n",
                                     rect.left,
                                     rect.top,
                                     rect.right,
                                     rect.bottom );

            // Draw a border matching the Windows graphics capture API border.
            // The outer frame is yellow and two logical pixels wide, while the
            // inner is black and 1 logical pixel wide.
            wil::unique_hbrush brush{CreateSolidBrush( RGB( 255, 222, 0 ) )};
            FillRect( deviceContext, &rect, brush.get() );
            int width = ScaleForDpi( 1, m_dpi );
            InflateRect( &rect, -width, -width );
            FillRect( deviceContext, &rect, static_cast<HBRUSH>(GetStockObject( BLACK_BRUSH )) );

            EndPaint( window, &paint );
            return 0;
        }
        break;
    }

    return DefWindowProcW( window, message, wordParam, longParam );
}
