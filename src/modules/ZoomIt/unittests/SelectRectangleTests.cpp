#include "pch.h"
#include <CppUnitTest.h>
#include <dwmapi.h>

#include "SelectRectangle.h"
#include "Utility.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace SelectRectangleTests
{
    struct SelectionObservation
    {
        HWND window{};
        BYTE initialAlpha{};
        unsigned int shown{};
        unsigned int hidden{};
    };

    thread_local SelectionObservation* observation{};

    void CALLBACK ObserveSelection(
        HWINEVENTHOOK, DWORD event, HWND window, LONG object, LONG child, DWORD, DWORD )
    {
        if( !observation || object != OBJID_WINDOW || child != CHILDID_SELF )
        {
            return;
        }

        wchar_t className[64]{};
        GetClassNameW( window, className, ARRAYSIZE( className ) );
        if( wcscmp( className, L"ZoomitSelectRectangle" ) != 0 )
        {
            return;
        }

        if( event == EVENT_OBJECT_SHOW )
        {
            ++observation->shown;
            if( !observation->window )
            {
                observation->window = window;
                DWORD flags{};
                GetLayeredWindowAttributes( window, nullptr, &observation->initialAlpha, &flags );
                PostMessageW( window, WM_LBUTTONDOWN, MK_LBUTTON, MAKELPARAM( 100, 100 ) );
                PostMessageW( window, WM_MOUSEMOVE, MK_LBUTTON, MAKELPARAM( 400, 300 ) );
                PostMessageW( window, WM_LBUTTONUP, 0, MAKELPARAM( 400, 300 ) );
            }
        }
        else if( event == EVENT_OBJECT_HIDE )
        {
            ++observation->hidden;
        }
    }

    void CALLBACK CancelTimedOutSelection( HWND, UINT, UINT_PTR, DWORD )
    {
        if( observation && observation->window )
        {
            PostMessageW( observation->window, WM_KEYDOWN, VK_ESCAPE, 0 );
        }
        else
        {
            PostQuitMessage( 0 );
        }
    }

    std::vector<DWORD> CaptureDesktop( const RECT& rect )
    {
        Assert::AreEqual( S_OK, DwmFlush() );
        auto screen = wil::GetDC( nullptr );
        wil::unique_hdc memory{ CreateCompatibleDC( screen.get() ) };
        const LONG width = rect.right - rect.left;
        const LONG height = rect.bottom - rect.top;
        BITMAPINFO info{};
        info.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        void* bits{};
        wil::unique_hbitmap bitmap{ CreateDIBSection( memory.get(), &info, DIB_RGB_COLORS, &bits, nullptr, 0 ) };
        Assert::IsNotNull( bits );
        auto previous = wil::SelectObject( memory.get(), bitmap.get() );
        Assert::IsTrue( StretchBlt( memory.get(), 0, 0, width, height, screen.get(),
                                   rect.left, rect.top, width, height, SRCCOPY | CAPTUREBLT ) != FALSE );
        GdiFlush();
        const auto pixels = static_cast<const DWORD*>( bits );
        std::vector<DWORD> result( pixels, pixels + static_cast<size_t>( width ) * height );
        for( auto& pixel : result )
        {
            pixel &= 0x00ffffff;
        }
        return result;
    }

    TEST_CLASS(SelectionWindowTests)
    {
    public:
        TEST_METHOD(RetainedSelectionKeepsOpacityAndCanBeCapturedWithoutHiding)
        {
            const auto previousDpi = SetThreadDpiAwarenessContext( DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 );
            Assert::IsNotNull( previousDpi );
            auto restoreDpi = wil::scope_exit( [&] { SetThreadDpiAwarenessContext( previousDpi ); } );
            const RECT monitor = GetMonitorRectFromCursor();
            wil::unique_hbrush brush{ CreateSolidBrush( RGB( 220, 100, 140 ) ) };
            WNDCLASSW windowClass{};
            windowClass.lpfnWndProc = DefWindowProcW;
            windowClass.hInstance = GetModuleHandleW( nullptr );
            windowClass.hbrBackground = brush.get();
            windowClass.lpszClassName = L"ZoomitSelectionTestBackground";
            Assert::IsTrue( RegisterClassW( &windowClass ) != 0 );
            auto unregister = wil::scope_exit( [&] {
                UnregisterClassW( windowClass.lpszClassName, windowClass.hInstance );
            } );
            wil::unique_hwnd background{ CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOACTIVATE,
                windowClass.lpszClassName, L"", WS_POPUP | WS_VISIBLE,
                monitor.left + 40, monitor.top + 40, 520, 320,
                nullptr, nullptr, windowClass.hInstance, nullptr ) };
            Assert::IsNotNull( background.get() );
            UpdateWindow( background.get() );
            const RECT capture{ monitor.left + 50, monitor.top + 50, monitor.left + 550, monitor.top + 350 };
            const auto cleanPixels = CaptureDesktop( capture );

            SelectionObservation state;
            observation = &state;
            auto clearObservation = wil::scope_exit( [] { observation = nullptr; } );
            const auto hook = SetWinEventHook( EVENT_OBJECT_SHOW, EVENT_OBJECT_HIDE, nullptr,
                                              ObserveSelection, GetCurrentProcessId(), GetCurrentThreadId(),
                                              WINEVENT_OUTOFCONTEXT );
            Assert::IsNotNull( hook );
            auto unhook = wil::scope_exit( [&] { UnhookWinEvent( hook ); } );
            const UINT_PTR timer = SetTimer( nullptr, 0, 10000, CancelTimedOutSelection );
            Assert::IsTrue( timer != 0 );
            auto killTimer = wil::scope_exit( [&] { KillTimer( nullptr, timer ); } );

            SelectRectangle selection;
            selection.RetainDimmedExteriorAfterSelection( true );
            Assert::IsTrue( selection.Start( background.get() ) );
            Assert::IsNotNull( state.window );
            Assert::IsTrue( IsWindowVisible( state.window ) != FALSE );
            BYTE selectedAlpha{};
            DWORD flags{};
            Assert::IsTrue( GetLayeredWindowAttributes( state.window, nullptr, &selectedAlpha, &flags ) != FALSE );
            Assert::AreEqual( selection.Alpha(), state.initialAlpha );
            Assert::AreEqual( state.initialAlpha, selectedAlpha );

            DWORD affinity{};
            Assert::IsTrue( GetWindowDisplayAffinity( state.window, &affinity ) != FALSE );
            Assert::AreEqual( static_cast<DWORD>( WDA_EXCLUDEFROMCAPTURE ), affinity );
            Assert::IsTrue( cleanPixels == CaptureDesktop( capture ),
                            L"The visible selection must not appear in the captured desktop." );

            selection.SetExcludeFromCapture( false );
            Assert::IsTrue( cleanPixels != CaptureDesktop( capture ),
                            L"The selection must actually be visible, not merely absent from both captures." );
            selection.SetExcludeFromCapture( true );
            Assert::IsTrue( cleanPixels == CaptureDesktop( capture ) );
            Assert::IsTrue( IsWindowVisible( state.window ) != FALSE );
            Assert::AreEqual( 1u, state.shown );
            Assert::AreEqual( 0u, state.hidden );
        }
    };
}
