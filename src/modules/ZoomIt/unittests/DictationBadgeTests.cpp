#include "pch.h"
#include <CppUnitTest.h>

#include "DictationBadge.h"
#include "Utility.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace DictationBadgeTests
{
    HWND FindBadgeWindow()
    {
        HWND result = nullptr;
        EnumThreadWindows( GetCurrentThreadId(), []( HWND window, LPARAM context ) -> BOOL {
            wchar_t className[64]{};
            GetClassNameW( window, className, ARRAYSIZE( className ) );
            if( wcscmp( className, L"ZoomitDictationBadge" ) == 0 )
            {
                *reinterpret_cast<HWND*>( context ) = window;
                return FALSE;
            }
            return TRUE;
        }, reinterpret_cast<LPARAM>( &result ) );
        return result;
    }

    std::vector<DWORD> CaptureBadge( HWND window )
    {
        RECT rect{};
        Assert::IsTrue( GetWindowRect( window, &rect ) != FALSE );
        const LONG width = rect.right - rect.left;
        const LONG height = rect.bottom - rect.top;
        wil::unique_hdc memory{ CreateCompatibleDC( nullptr ) };
        BITMAPINFO info{};
        info.bmiHeader.biSize = sizeof( BITMAPINFOHEADER );
        info.bmiHeader.biWidth = width;
        info.bmiHeader.biHeight = -height;
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        void* bits = nullptr;
        wil::unique_hbitmap bitmap{ CreateDIBSection( memory.get(), &info, DIB_RGB_COLORS, &bits, nullptr, 0 ) };
        Assert::IsNotNull( bits );
        auto previous = wil::SelectObject( memory.get(), bitmap.get() );
        SendMessageW( window, WM_PRINTCLIENT, reinterpret_cast<WPARAM>( memory.get() ), PRF_CLIENT );
        GdiFlush();
        const auto pixels = static_cast<const DWORD*>( bits );
        std::vector<DWORD> result( pixels, pixels + static_cast<size_t>( width ) * height );
        return result;
    }

    TEST_CLASS(BadgeWindowTests)
    {
        ULONG_PTR m_graphicsToken{};
        DPI_AWARENESS_CONTEXT m_previousDpiContext{};

    public:
        TEST_METHOD_INITIALIZE(InitializeGraphics)
        {
            m_previousDpiContext = SetThreadDpiAwarenessContext( DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 );
            Assert::IsNotNull( m_previousDpiContext );
            Gdiplus::GdiplusStartupInput input;
            Assert::AreEqual( static_cast<int>( Gdiplus::Ok ),
                             static_cast<int>( Gdiplus::GdiplusStartup( &m_graphicsToken, &input, nullptr ) ) );
        }

        TEST_METHOD_CLEANUP(ShutdownGraphics)
        {
            Gdiplus::GdiplusShutdown( m_graphicsToken );
            SetThreadDpiAwarenessContext( m_previousDpiContext );
        }

        TEST_METHOD(InitialPopupIsTextSizedBeforeRecognitionCallbacks)
        {
            DictationBadge badge;
            const RECT anchor{ 100, 100, 900, 700 };
            badge.Show( nullptr, anchor );
            HWND window = FindBadgeWindow();
            Assert::IsNotNull( window );
            RECT initialRect{};
            Assert::IsTrue( GetWindowRect( window, &initialRect ) != FALSE );
            const auto initialPixels = CaptureBadge( window );

            badge.SetStatus( L"Starting dictation..." );
            RECT statusRect{};
            Assert::IsTrue( GetWindowRect( window, &statusRect ) != FALSE );
            Assert::IsTrue( EqualRect( &initialRect, &statusRect ) != FALSE );
            Assert::IsTrue( initialPixels == CaptureBadge( window ) );

            badge.Hide();
            badge.Show( nullptr, anchor );
            window = FindBadgeWindow();
            Assert::IsNotNull( window );
            Assert::IsTrue( GetWindowRect( window, &statusRect ) != FALSE );
            Assert::IsTrue( EqualRect( &initialRect, &statusRect ) != FALSE );
            Assert::IsTrue( initialPixels == CaptureBadge( window ) );
        }

        TEST_METHOD(StartupAndListeningKeepIdenticalBounds)
        {
            DictationBadge badge;
            badge.Show( nullptr, { 100, 100, 900, 700 } );
            HWND window = FindBadgeWindow();
            Assert::IsNotNull( window );
            RECT startingRect{};
            Assert::IsTrue( GetWindowRect( window, &startingRect ) != FALSE );
            const auto startingPixels = CaptureBadge( window );

            std::thread worker( [&badge] {
                badge.SetStatus( L"Listening. Release to snip." );
            } );
            worker.join();
            MSG message{};
            while( PeekMessageW( &message, window, 0, 0, PM_REMOVE ) )
            {
                DispatchMessageW( &message );
            }

            RECT listeningRect{};
            Assert::IsTrue( GetWindowRect( window, &listeningRect ) != FALSE );
            Assert::IsTrue( EqualRect( &startingRect, &listeningRect ) != FALSE,
                            L"The popup must keep its initial bounds when listening starts." );
            Assert::IsTrue( startingPixels != CaptureBadge( window ) );

            badge.SetStatus( L"Starting dictation..." );
            Assert::IsTrue( GetWindowRect( window, &listeningRect ) != FALSE );
            Assert::IsTrue( EqualRect( &startingRect, &listeningRect ) != FALSE );
            Assert::IsTrue( startingPixels == CaptureBadge( window ) );
        }

        TEST_METHOD(ShortStatusUsesMeasuredTextWidth)
        {
            DictationBadge badge;
            badge.SetStatus( L"Listening. Release to snip." );
            badge.Show( nullptr, { 100, 100, 900, 700 } );
            HWND window = FindBadgeWindow();
            Assert::IsNotNull( window );
            RECT rect{};
            GetWindowRect( window, &rect );
            Assert::IsTrue( rect.right - rect.left < ScaleForDpi( 300, GetDpiForWindowHelper( window ) ) );
            const LONG listeningWidth = rect.right - rect.left;
            badge.BeginTranscribing();
            GetWindowRect( window, &rect );
            Assert::IsTrue( rect.right - rect.left > listeningWidth );
        }

        TEST_METHOD(LongContentRemainsBoundedAndWraps)
        {
            DictationBadge badge;
            badge.SetStatus( L"Listening. Release to snip." );
            badge.Show( nullptr, { 100, 100, 900, 700 } );
            HWND window = FindBadgeWindow();
            Assert::IsNotNull( window );
            RECT rect{};
            GetWindowRect( window, &rect );
            const LONG statusHeight = rect.bottom - rect.top;

            std::wstring text;
            for( int index = 0; index < 50; ++index )
            {
                text += L"A long spoken annotation. ";
            }
            badge.SetText( text );
            GetWindowRect( window, &rect );
            const UINT dpi = GetDpiForWindowHelper( window );
            Assert::IsTrue( rect.right - rect.left <= ScaleForDpi( 520, dpi ) );
            Assert::IsTrue( rect.bottom - rect.top > statusHeight );
            Assert::IsTrue( rect.bottom - rect.top <= statusHeight + 3 * ScaleForDpi( 20, dpi ) );
            badge.SetStatus( std::wstring( 1000, L'W' ) );
            GetWindowRect( window, &rect );
            Assert::AreEqual( static_cast<LONG>( ScaleForDpi( 520, dpi ) ), rect.right - rect.left );
        }

        TEST_METHOD(TranscribingSpinnerRepaintsAtFixedSizeWithoutPumpingMessages)
        {
            wil::unique_hwnd owner{ CreateWindowExW(
                WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TOPMOST, L"STATIC", L"", WS_POPUP | WS_VISIBLE,
                100, 100, 800, 600, nullptr, nullptr, GetModuleHandleW( nullptr ), nullptr ) };
            wil::unique_hwnd selection{ CreateWindowExW(
                WS_EX_LAYERED | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE, L"STATIC", L"", WS_POPUP,
                100, 100, 800, 600, owner.get(), nullptr, GetModuleHandleW( nullptr ), nullptr ) };
            Assert::IsNotNull( owner.get() );
            Assert::IsNotNull( selection.get() );
            Assert::IsTrue( SetLayeredWindowAttributes( selection.get(), 0, 176, LWA_ALPHA ) != FALSE );
            ShowWindow( selection.get(), SW_SHOWNA );
            DictationBadge badge;
            badge.SetStatus( L"Listening. Release to snip." );
            badge.Show( owner.get(), { 100, 100, 900, 700 } );
            HWND window = FindBadgeWindow();
            Assert::IsNotNull( window );

            // Simulate the drag's modal loop, followed by a blocking Stop.
            MSG message{};
            while( PeekMessageW( &message, nullptr, 0, 0, PM_REMOVE ) )
            {
                DispatchMessageW( &message );
            }
            const auto listening = CaptureBadge( window );
            ShowWindow( selection.get(), SW_HIDE );
            ShowWindow( selection.get(), SW_SHOWNA );
            InvalidateRect( selection.get(), nullptr, FALSE );
            InvalidateRect( owner.get(), nullptr, FALSE );
            badge.BeginTranscribing();
            const auto transcribing = CaptureBadge( window );
            Assert::IsTrue( listening != transcribing );

            RECT animationRect{};
            GetWindowRect( window, &animationRect );
            const LONG width = animationRect.right - animationRect.left;
            const LONG textLeft = ScaleForDpi( 34, GetDpiForWindowHelper( window ) );
            auto previousFrame = transcribing;
            for( int frame = 0; frame < 24; ++frame )
            {
                // Final worker callbacks must not change the layout mid-spin.
                badge.SetStatus( L"Listening. Release to snip." );
                badge.SetText( L"A late recognition result must not resize the popup." );
                badge.AdvanceTranscribingAnimation();
                RECT rect{};
                GetWindowRect( window, &rect );
                Assert::IsTrue( EqualRect( &animationRect, &rect ) != FALSE );
                const auto pixels = CaptureBadge( window );
                Assert::IsTrue( previousFrame != pixels );
                for( size_t index = 0; index < pixels.size(); ++index )
                {
                    if( index % width >= static_cast<size_t>( textLeft ) )
                    {
                        Assert::AreEqual( transcribing[index], pixels[index] );
                    }
                }
                previousFrame = pixels;
            }
            Assert::IsTrue( previousFrame == transcribing );
        }
    };
}
