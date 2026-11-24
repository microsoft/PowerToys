#include "pch.h"
#include <CppUnitTest.h>
#include "ClipboardPayload.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

void TestOutputDebug( const TCHAR*, ... ) {}

namespace
{
    constexpr UINT WM_TEST_PASTE = WM_APP + 100;
    std::atomic<DWORD> clipboardSequence{ 1 };
    std::atomic<DWORD> clipboardFormats{ 0 };
    std::atomic<int> imagePastes{ 0 };
    std::atomic<int> textPastes{ 0 };
    std::atomic<int> physicalPastes{ 0 };
    thread_local bool physicalControlDown = false;

    LRESULT CALLBACK TestMessageHook( int code, WPARAM wordParam, LPARAM longParam );

    DWORD TestClipboardSequence()
    {
        return clipboardSequence.load();
    }

    HWND TestForegroundWindow()
    {
        return GetDesktopWindow();
    }

    SHORT TestKeyState( int key )
    {
        return key == VK_CONTROL && physicalControlDown ? static_cast<SHORT>( 0x8000 ) : 0;
    }

    HHOOK TestInstallHook( int, HOOKPROC, HINSTANCE, DWORD )
    {
        return SetWindowsHookExW( WH_GETMESSAGE, TestMessageHook, nullptr, GetCurrentThreadId() );
    }

    LRESULT TestNextHook( HHOOK, int, WPARAM, LPARAM )
    {
        return 0;
    }

    bool TestPublishClipboard( HWND, HBITMAP, const std::wstring&, DWORD formats )
    {
        clipboardFormats.store( formats );
        ++clipboardSequence;
        return true;
    }

    void ConsumeClipboard()
    {
        // Model a chat paste handler that chooses the image over the text.
        if( ( clipboardFormats.load() & SNIP_CLIPBOARD_IMAGE ) != 0 )
        {
            ++imagePastes;
        }
        else if( ( clipboardFormats.load() & SNIP_CLIPBOARD_TEXT ) != 0 )
        {
            ++textPastes;
        }
    }

    UINT TestSendInput( UINT count, INPUT*, int )
    {
        ConsumeClipboard();
        return count;
    }
}

// Exercise the real worker, hook callback and timers without changing the
// desktop's clipboard or injecting keyboard input into another application.
#undef SetWindowsHookEx
#define SetWindowsHookEx TestInstallHook
#define GetClipboardSequenceNumber TestClipboardSequence
#define GetForegroundWindow TestForegroundWindow
#define GetAsyncKeyState TestKeyState
#define CallNextHookEx TestNextHook
#define SendInput TestSendInput
#define PublishSnipClipboard TestPublishClipboard
#define OutputDebug TestOutputDebug
#include "..\ZoomIt\PasteChaperone.cpp"
#undef SetWindowsHookEx
#undef GetClipboardSequenceNumber
#undef GetForegroundWindow
#undef GetAsyncKeyState
#undef CallNextHookEx
#undef SendInput
#undef PublishSnipClipboard
#undef OutputDebug

namespace
{
    LRESULT CALLBACK TestMessageHook( int code, WPARAM wordParam, LPARAM longParam )
    {
        if( code >= 0 && wordParam == PM_REMOVE )
        {
            auto* message = reinterpret_cast<MSG*>( longParam );
            if( message->message == WM_TEST_PASTE )
            {
                KBDLLHOOKSTRUCT key{};
                key.vkCode = 'V';
                key.flags = static_cast<DWORD>( message->wParam );
                physicalControlDown = true;
                const auto swallowed = KeyboardHookProc(
                    HC_ACTION, WM_KEYDOWN, reinterpret_cast<LPARAM>( &key ) );
                physicalControlDown = false;
                if( swallowed == 0 )
                {
                    ConsumeClipboard();
                }
                ++physicalPastes;
                message->message = WM_NULL;
            }
        }
        return CallNextHookEx( nullptr, code, wordParam, longParam );
    }

    template<typename Predicate>
    void WaitUntil( Predicate predicate )
    {
        const ULONGLONG deadline = GetTickCount64() + 6000;
        while( !predicate() && GetTickCount64() < deadline )
        {
            Sleep( 10 );
        }
        Assert::IsTrue( predicate(), L"Paste worker did not reach the expected state." );
    }
}

namespace PasteChaperoneTests
{
    TEST_CLASS(RepeatPasteTests)
    {
    public:
        TEST_METHOD_INITIALIZE(Initialize)
        {
            clipboardSequence = 1;
            clipboardFormats = 0;
            imagePastes = 0;
            textPastes = 0;
            physicalPastes = 0;
            HBITMAP bitmap = CreateBitmap( 1, 1, 1, 32, nullptr );
            Assert::IsNotNull( bitmap );
            const bool armed = PasteChaperone::Arm( bitmap, L"Dictated text", SNIP_CLIPBOARD_ALL );
            DeleteObject( bitmap );
            Assert::IsTrue( armed );
        }

        TEST_METHOD_CLEANUP(Cleanup)
        {
            PasteChaperone::Cancel();
        }

        TEST_METHOD(RepeatedPastesDeliverImageAndTextAndRestoreCombinedPayload)
        {
            for( int paste = 1; paste <= 3; ++paste )
            {
                Assert::IsTrue( PostThreadMessageW( g_ThreadId, WM_TEST_PASTE, 0, 0 ) != FALSE );
                WaitUntil( [paste] {
                    return physicalPastes == paste && textPastes == paste &&
                           clipboardFormats == SNIP_CLIPBOARD_ALL;
                } );
                Assert::AreEqual( paste, imagePastes.load(), L"Each gesture must paste exactly one image." );
                Assert::IsTrue( PasteChaperone::IsArmed(), L"Repeat paste must stay armed." );
            }
        }

        TEST_METHOD(NewClipboardContentDisarmsWithoutOverwritingIt)
        {
            Assert::IsTrue( PostThreadMessageW( g_ThreadId, WM_TEST_PASTE, 0, 0 ) != FALSE );
            WaitUntil( [] { return textPastes == 1 && clipboardFormats == SNIP_CLIPBOARD_ALL; } );
            clipboardFormats = SNIP_CLIPBOARD_TEXT;
            ++clipboardSequence;
            Assert::IsTrue( PostThreadMessageW( g_ThreadId, WM_TEST_PASTE, 0, 0 ) != FALSE );
            WaitUntil( [] { return !PasteChaperone::IsArmed(); } );
            Assert::AreEqual( static_cast<DWORD>( SNIP_CLIPBOARD_TEXT ), clipboardFormats.load(),
                             L"External clipboard content must be preserved." );
            Assert::AreEqual( 1, imagePastes.load(), L"External paste must not replay the image." );
            Assert::AreEqual( 2, textPastes.load(), L"External paste must pass through." );
        }

        TEST_METHOD(InjectedPasteDoesNotTriggerTheSequence)
        {
            Assert::IsTrue( PostThreadMessageW(
                g_ThreadId, WM_TEST_PASTE, LLKHF_INJECTED, 0 ) != FALSE );
            WaitUntil( [] { return physicalPastes == 1; } );
            Assert::AreEqual( 1, imagePastes.load() );
            Assert::AreEqual( 0, textPastes.load() );
            Assert::AreEqual( static_cast<DWORD>( SNIP_CLIPBOARD_IMAGE ), clipboardFormats.load() );
        }

        TEST_METHOD(NewClipboardContentDisarmsWhileIdle)
        {
            Assert::IsTrue( PostThreadMessageW( g_ThreadId, WM_TEST_PASTE, 0, 0 ) != FALSE );
            WaitUntil( [] { return textPastes == 1 && clipboardFormats == SNIP_CLIPBOARD_ALL; } );
            clipboardFormats = SNIP_CLIPBOARD_TEXT;
            ++clipboardSequence;
            WaitUntil( [] { return !PasteChaperone::IsArmed(); } );
            Assert::AreEqual( static_cast<DWORD>( SNIP_CLIPBOARD_TEXT ), clipboardFormats.load() );
            Assert::AreEqual( 1, imagePastes.load() );
            Assert::AreEqual( 1, textPastes.load() );
        }

        TEST_METHOD(CancelStopsRepeatMonitoringWithoutChangingCombinedPayload)
        {
            Assert::IsTrue( PostThreadMessageW( g_ThreadId, WM_TEST_PASTE, 0, 0 ) != FALSE );
            WaitUntil( [] { return textPastes == 1 && clipboardFormats == SNIP_CLIPBOARD_ALL; } );
            PasteChaperone::Cancel();
            Assert::IsFalse( PasteChaperone::IsArmed() );
            Assert::AreEqual( static_cast<DWORD>( SNIP_CLIPBOARD_ALL ), clipboardFormats.load() );
        }
    };
}
