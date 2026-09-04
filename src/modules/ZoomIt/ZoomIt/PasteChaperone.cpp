//==============================================================================
//
// Zoomit
// Sysinternals - www.sysinternals.com
//
// See PasteChaperone.h for what this does and why it is needed.
//
//==============================================================================
#include "pch.h"
#include "PasteChaperone.h"
#include "ClipboardPayload.h"

#include <memory>
#include <new>

namespace
{
    // Posted by the low level keyboard hook when the user presses Ctrl+V.
    constexpr UINT WM_CHAPERONE_PASTED = WM_APP + 1;
    // Posted by Cancel to unwind the sequence early.
    constexpr UINT WM_CHAPERONE_CANCEL = WM_APP + 2;

    // Applications read the clipboard asynchronously after the paste
    // keystroke, so the image has to stay put long enough for the target to
    // finish reading it before the transcription replaces it.
    constexpr UINT CONSUME_DELAY = 400;

    // Time to leave the transcription on the clipboard for the synthesized
    // paste before the combined payload is restored.
    constexpr UINT RESTORE_DELAY = 400;

    // How long to wait for the user's paste before giving up and restoring the
    // combined payload.
    constexpr UINT ARM_TIMEOUT = 60 * 1000;

    // Longest we wait for the user to release Ctrl before synthesizing the
    // second paste, in 10ms steps.
    constexpr int CTRL_RELEASE_POLLS = 100;

    struct ChaperoneState
    {
        ~ChaperoneState()
        {
            if( Bitmap != nullptr )
            {
                DeleteObject( Bitmap );
            }
        }

        DWORD           ThreadId = 0;
        HHOOK           Hook = nullptr;
        HWND            Window = nullptr;
        HBITMAP         Bitmap = nullptr;
        std::wstring    Text;
        DWORD           CombinedFormats = 0;
        DWORD           Sequence = 0;
        HWND            PasteTarget = nullptr;
        bool            SawPaste = false;
        UINT_PTR        TimerConsume = 0;
        UINT_PTR        TimerRestore = 0;
        UINT_PTR        TimerTimeout = 0;
    };

    // Only ever touched by the chaperone thread.
    ChaperoneState* g_State = nullptr;

    // Shared with callers on other threads.
    HANDLE  g_Thread = nullptr;
    DWORD   g_ThreadId = 0;
    LONG    g_Armed = 0;


    //----------------------------------------------------------------------
    //
    // KeyboardHookProc
    //
    // Runs on the chaperone thread while it pumps messages. Only physical
    // keystrokes count: the paste this class synthesizes is flagged as
    // injected and must not retrigger the sequence.
    //
    //----------------------------------------------------------------------
    LRESULT CALLBACK KeyboardHookProc( int code, WPARAM wParam, LPARAM lParam )
    {
        if( code == HC_ACTION && g_State != nullptr && !g_State->SawPaste &&
            ( wParam == WM_KEYDOWN || wParam == WM_SYSKEYDOWN ) )
        {
            const auto* key = reinterpret_cast<const KBDLLHOOKSTRUCT*>( lParam );

            if( key->vkCode == 'V' && ( key->flags & LLKHF_INJECTED ) == 0 &&
                ( GetAsyncKeyState( VK_CONTROL ) & 0x8000 ) != 0 )
            {
                g_State->SawPaste = true;
                g_State->PasteTarget = GetForegroundWindow();
                PostThreadMessage( g_State->ThreadId, WM_CHAPERONE_PASTED, 0, 0 );
            }
        }

        return CallNextHookEx( nullptr, code, wParam, lParam );
    }


    //----------------------------------------------------------------------
    //
    // PublishStage
    //
    // Publishes one stage and remembers the resulting clipboard sequence
    // number so that we can tell our own content apart from anything the user
    // copies while the sequence is armed.
    //
    //----------------------------------------------------------------------
    bool PublishStage( DWORD formats )
    {
        if( !PublishSnipClipboard( g_State->Window, g_State->Bitmap, g_State->Text, formats ) )
        {
            return false;
        }

        g_State->Sequence = GetClipboardSequenceNumber();
        return true;
    }


    //----------------------------------------------------------------------
    //
    // ClipboardStillOurs
    //
    // False once anything else has written to the clipboard, in which case the
    // sequence has to abandon quietly rather than overwrite the user's data.
    //
    //----------------------------------------------------------------------
    bool ClipboardStillOurs()
    {
        return GetClipboardSequenceNumber() == g_State->Sequence;
    }

    bool PasteTargetStillActive()
    {
        return g_State->PasteTarget != nullptr &&
               IsWindow( g_State->PasteTarget ) &&
               GetForegroundWindow() == g_State->PasteTarget;
    }


    //----------------------------------------------------------------------
    //
    // SendPaste
    //
    //----------------------------------------------------------------------
    bool SendPaste()
    {
        // Waiting for the physical Ctrl to come up keeps the synthesized key
        // events from interleaving with the user's own.
        for( int i = 0; i < CTRL_RELEASE_POLLS && ( GetAsyncKeyState( VK_CONTROL ) & 0x8000 ) != 0; i++ )
        {
            Sleep( 10 );
        }

        if( !PasteTargetStillActive() )
        {
            return false;
        }

        INPUT inputs[4] = {};

        inputs[0].type = INPUT_KEYBOARD;
        inputs[0].ki.wVk = VK_CONTROL;
        inputs[1].type = INPUT_KEYBOARD;
        inputs[1].ki.wVk = 'V';
        inputs[2].type = INPUT_KEYBOARD;
        inputs[2].ki.wVk = 'V';
        inputs[2].ki.dwFlags = KEYEVENTF_KEYUP;
        inputs[3].type = INPUT_KEYBOARD;
        inputs[3].ki.wVk = VK_CONTROL;
        inputs[3].ki.dwFlags = KEYEVENTF_KEYUP;

        return SendInput( ARRAYSIZE( inputs ), inputs, sizeof( INPUT ) ) ==
               ARRAYSIZE( inputs );
    }


    //----------------------------------------------------------------------
    //
    // ChaperoneThread
    //
    // Owns the hook, the timers and the state. A dedicated thread is required
    // because a low level keyboard hook is dispatched on the thread that
    // installed it, and that thread has to be pumping messages.
    //
    //----------------------------------------------------------------------
    DWORD WINAPI ChaperoneThread( LPVOID parameter )
    {
        auto* state = static_cast<ChaperoneState*>( parameter );

        g_State = state;
        state->ThreadId = GetCurrentThreadId();

        // Forces the message queue to exist before Cancel can post to it.
        MSG msg;
        PeekMessage( &msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE );

        CoInitializeEx( nullptr, COINIT_APARTMENTTHREADED );

        state->Window = CreateWindowEx( 0, L"STATIC", L"ZoomItPasteChaperone", 0, 0, 0, 0, 0,
                                        HWND_MESSAGE, nullptr, GetModuleHandle( nullptr ), nullptr );

        // Stage one. With only the image on the clipboard an application that
        // takes either an image or text has no text to prefer, so it takes the
        // image.
        if( state->Window != nullptr && PublishStage( SNIP_CLIPBOARD_IMAGE ) )
        {
            state->Hook = SetWindowsHookEx( WH_KEYBOARD_LL, KeyboardHookProc,
                                            GetModuleHandle( nullptr ), 0 );
        }

        if( state->Hook != nullptr )
        {
            state->TimerTimeout = SetTimer( nullptr, 0, ARM_TIMEOUT, nullptr );
            InterlockedExchange( &g_Armed, 1 );

            while( GetMessage( &msg, nullptr, 0, 0 ) > 0 )
            {
                bool done = false;

                if( msg.message == WM_CHAPERONE_CANCEL )
                {
                    done = true;
                }
                else if( msg.message == WM_CHAPERONE_PASTED )
                {
                    // The user's paste is on its way to the target. Stop
                    // hooking immediately so that nothing else is intercepted.
                    UnhookWindowsHookEx( state->Hook );
                    state->Hook = nullptr;

                    KillTimer( nullptr, state->TimerTimeout );
                    state->TimerTimeout = 0;
                    state->TimerConsume = SetTimer( nullptr, 0, CONSUME_DELAY, nullptr );
                }
                else if( msg.message == WM_TIMER && state->TimerConsume != 0 &&
                         msg.wParam == state->TimerConsume )
                {
                    KillTimer( nullptr, state->TimerConsume );
                    state->TimerConsume = 0;

                    // Stage two: the image has been consumed, so hand the
                    // target the transcription and paste it for the user.
                    if( ClipboardStillOurs() && PasteTargetStillActive() &&
                        PublishStage( SNIP_CLIPBOARD_TEXT ) && SendPaste() )
                    {
                        state->TimerRestore = SetTimer( nullptr, 0, RESTORE_DELAY, nullptr );
                    }
                    else
                    {
                        if( ClipboardStillOurs() )
                        {
                            PublishStage( state->CombinedFormats );
                        }
                        done = true;
                    }
                }
                else if( msg.message == WM_TIMER && state->TimerRestore != 0 &&
                         msg.wParam == state->TimerRestore )
                {
                    KillTimer( nullptr, state->TimerRestore );
                    state->TimerRestore = 0;

                    if( ClipboardStillOurs() )
                    {
                        PublishStage( state->CombinedFormats );
                    }
                    done = true;
                }
                else if( msg.message == WM_TIMER && state->TimerTimeout != 0 &&
                         msg.wParam == state->TimerTimeout )
                {
                    KillTimer( nullptr, state->TimerTimeout );
                    state->TimerTimeout = 0;

                    // The user never pasted. Leave them with everything, the
                    // same as if the chaperone had not been armed at all.
                    if( ClipboardStillOurs() )
                    {
                        PublishStage( state->CombinedFormats );
                    }
                    done = true;
                }

                if( done )
                {
                    break;
                }

                TranslateMessage( &msg );
                DispatchMessage( &msg );
            }
        }

        InterlockedExchange( &g_Armed, 0 );

        if( state->TimerConsume != 0 ) KillTimer( nullptr, state->TimerConsume );
        if( state->TimerRestore != 0 ) KillTimer( nullptr, state->TimerRestore );
        if( state->TimerTimeout != 0 ) KillTimer( nullptr, state->TimerTimeout );
        if( state->Hook != nullptr ) UnhookWindowsHookEx( state->Hook );
        if( state->Window != nullptr ) DestroyWindow( state->Window );
        CoUninitialize();

        g_State = nullptr;
        delete state;

        return 0;
    }
}


//----------------------------------------------------------------------
//
// PasteChaperone::Arm
//
//----------------------------------------------------------------------
bool PasteChaperone::Arm( HBITMAP bitmap, const std::wstring& text, DWORD combinedFormats )
{
    // A transcription is what makes the two stages necessary; without one a
    // plain image paste already does the right thing everywhere.
    if( bitmap == nullptr || text.empty() )
    {
        return false;
    }

    Cancel();

    std::unique_ptr<ChaperoneState> state( new ( std::nothrow ) ChaperoneState() );
    if( state == nullptr )
    {
        return false;
    }

    state->Bitmap = static_cast<HBITMAP>( CopyImage( bitmap, IMAGE_BITMAP, 0, 0, LR_CREATEDIBSECTION ) );
    if( state->Bitmap == nullptr )
    {
        return false;
    }

    state->Text = text;
    state->CombinedFormats = combinedFormats;

    g_Thread = CreateThread( nullptr, 0, ChaperoneThread, state.get(), 0, &g_ThreadId );
    if( g_Thread == nullptr )
    {
        g_ThreadId = 0;
        return false;
    }

    state.release();
    return true;
}


//----------------------------------------------------------------------
//
// PasteChaperone::Cancel
//
//----------------------------------------------------------------------
void PasteChaperone::Cancel()
{
    if( g_Thread == nullptr )
    {
        return;
    }

    // The thread creates its queue before doing anything else, but posting can
    // still lose the race on a heavily loaded machine.
    for( int i = 0; i < 50; i++ )
    {
        if( PostThreadMessage( g_ThreadId, WM_CHAPERONE_CANCEL, 0, 0 ) )
        {
            break;
        }

        if( WaitForSingleObject( g_Thread, 10 ) == WAIT_OBJECT_0 )
        {
            break;
        }
    }

    WaitForSingleObject( g_Thread, 5000 );
    CloseHandle( g_Thread );

    g_Thread = nullptr;
    g_ThreadId = 0;
}


//----------------------------------------------------------------------
//
// PasteChaperone::IsArmed
//
//----------------------------------------------------------------------
bool PasteChaperone::IsArmed()
{
    return InterlockedCompareExchange( &g_Armed, 0, 0 ) != 0;
}
