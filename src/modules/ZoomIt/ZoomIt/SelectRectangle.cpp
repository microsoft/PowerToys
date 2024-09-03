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

//----------------------------------------------------------------------------
//
// SelectRectangle::Start
//
//----------------------------------------------------------------------------
bool SelectRectangle::Start( HWND ownerWindow, bool fullMonitor )
{
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
    m_window = wil::unique_hwnd( CreateWindowExW( WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_TOPMOST, m_className, nullptr, WS_POPUP,
                                                  rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top, ownerWindow,
                                                  nullptr, nullptr, this ) );
    THROW_LAST_ERROR_IF_NULL( m_window.get() );

    if( fullMonitor )
    {
        m_selectedRect = rect;
        ShowSelected();
    }
    else
    {
        SetLayeredWindowAttributes( m_window.get(), 0, Alpha(), LWA_ALPHA );
    }

    ShowWindow( m_window.get(), SW_SHOW );
    SetForegroundWindow( m_window.get() );

    if( !fullMonitor )
    {
        GetClipCursor( &m_oldClipRect );
        ClipCursor( &rect );
        m_setClip = true;
    }

    MSG message;
    while( GetMessageW( &message, nullptr, 0, 0 ) != 0 )
    {
        TranslateMessage( &message );
        DispatchMessageW( &message );
        if( m_cancel )
        {
            return false;
        }
        if( m_selected )
        {
            break;
        }
    }
    return true;
}

//----------------------------------------------------------------------------
//
// SelectRectangle::Stop
//
//----------------------------------------------------------------------------
void SelectRectangle::Stop()
{
    if( m_setClip )
    {
        ClipCursor( &m_oldClipRect );
        m_setClip = false;
    }
    m_window.reset();
    m_selected = false;
    m_selectedRect = {};
    m_cancel = true;
}

//----------------------------------------------------------------------------
//
// SelectRectangle::ShowSelected
//
//----------------------------------------------------------------------------
void SelectRectangle::ShowSelected()
{
    m_selected = true;

    // Set the alpha to match the Windows graphics capture API yellow border
    // and set the window to be transparent and disabled, so it will be skipped
    // for hit testing and as a candidate for the next foreground window.
    SetLayeredWindowAttributes( m_window.get(), 0, 191, LWA_ALPHA );
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

    // Use a region to keep everything but the border transparent.
    wil::unique_hrgn region{CreateRectRgnIndirect( &rect )};
    InflateRect( &rect, -width, -width );
    wil::unique_hrgn insideRegion{CreateRectRgnIndirect( &rect )};
    CombineRgn( region.get(), region.get(), insideRegion.get(), RGN_XOR );
    SetWindowRgn( m_window.get(), region.release(), true );
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
        return 0;

    case WM_DESTROY:
        Stop();
        return 0;

    case WM_LBUTTONDOWN:
    {
        SetCapture( window );

        m_startPoint = { GET_X_LPARAM( longParam ), GET_Y_LPARAM( longParam ) };
        [[fallthrough]];
    }
    case WM_MOUSEMOVE:
        if( GetCapture() == window )
        {
            RECT rect;
            GetClientRect( window, &rect );
            POINT point{ GET_X_LPARAM( longParam ), GET_Y_LPARAM( longParam ) };
            m_selectedRect = ForceRectInBounds( RectFromPointsMinSize( m_startPoint, point, MinSize() ), rect );

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
            Stop();
        }
        return 0;

    case WM_KILLFOCUS:
        if( !m_selected )
        {
            Stop();
        }
        return 0;

    case WM_LBUTTONUP:
    {
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
