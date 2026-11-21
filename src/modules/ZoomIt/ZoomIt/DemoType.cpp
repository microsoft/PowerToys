//============================================================================
//
// Zoomit
// Copyright (C) Mark Russinovich
// Sysinternals - www.sysinternals.com
//
// DemoType allows the presenter to synthesize keystrokes from a script
//
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//============================================================================

#include "pch.h"
#include "DemoType.h"

#define MAX_INDENT_DEPTH    100

#define INDENT_SEEK_FLAG    L"x"

#define END_CONTROL_LEN     5
// Longest accepted control: [pause:000]
#define MAX_CONTROL_LEN     11

#define THIRD_TYPING_SPEED  static_cast<int>((MIN_TYPING_SPEED - MAX_TYPING_SPEED) / 3)
#define TYPING_VARIANCE     ((float) 1.0)

#define NOTEPAD_REFRESH     1    // ms
#define DEMOTYPE_REFRESH    50   // ms
#define CLIPBOARD_REFRESH   100  // ms
#define DEMOTYPE_TIMEOUT    1000 // ms

#define INACTIVE_STATE      0
#define START_STATE         1
#define INIT_STATE          2
#define ACTIVE_STATE        3
#define BLOCK_STATE         4
#define KILL_STATE          5

// Every keystroke DemoType synthesizes carries this tag in dwExtraInfo so the
// hook procedure can let our injections through while blocking accidental
// user keystrokes, without sharing a queue (and a lock) with the injector.
//
// Unicode characters are sent with KEYEVENTF_UNICODE, which SendInput wraps
// into a VK_PACKET. VK_PACKET allows us to synthesize keystrokes which are
// not mapped to virtual-key codes (e.g. foreign characters).
#define DEMOTYPE_INJECTION_TAG static_cast<ULONG_PTR>(0x5A4D4454) // 'ZMDT'

// The low-level keyboard hook must return quickly: Windows stalls all system
// input while it runs and silently removes hooks that exceed
// LowLevelHooksTimeout. The hook therefore only decides whether to swallow
// each event and signals the controller or the user-driven worker, which do
// all typing, clipboard work and waiting on their own threads.

bool                    g_UserDriven = false;
bool                    g_Notepad = false;
bool                    g_Clipboard = false;
TCHAR                   g_LastFilePath[MAX_PATH] = {0};
HHOOK                   g_hHook = nullptr;
std::atomic<DWORD>      g_HookThreadId = 0;
HWND                    g_hWndFocus = nullptr;
int                     g_InjectionRatio = 1;
bool                    g_SwallowedKeys[256] = {};
size_t                  g_TextLen = 0;
std::unique_ptr<wchar_t[]> g_ClipboardCache;
std::wstring            g_Text = L"";
std::vector<size_t>     g_TextSegments;
std::wstring            g_BaselineIndentation = L"";
std::atomic<size_t>     g_Index = 0;
std::atomic<int>        g_PendingInjections = 0;
std::atomic<int>        g_PendingAdvances = 0;
std::atomic<bool>       g_SeekingBaseline = false;
std::atomic<bool>       g_Active = false;
std::atomic<bool>       g_End = false;
std::atomic<bool>       g_Kill = false;
std::atomic<int>        g_HookState = INACTIVE_STATE;
std::atomic<int>        g_EmitterState = INACTIVE_STATE;
DWORD                   g_LastClipboardSeq = 0;
DWORD                   g_SpeedSlider = static_cast<int>((
    (MIN_TYPING_SPEED - MAX_TYPING_SPEED) / 2) + MAX_TYPING_SPEED);

//----------------------------------------------------------------------------
//
// GetEpochEvent
//
// Signaled whenever hook, emitter or worker state changes so the controller
// can re-check its wait condition. SetEvent never blocks, unlike taking the
// mutex a condition variable requires, so the hook can signal it safely.
//
//----------------------------------------------------------------------------
HANDLE GetEpochEvent()
{
    static wil::unique_event epochEvent( wil::EventOptions::None );
    return epochEvent.get();
}

//----------------------------------------------------------------------------
//
// GetAdvanceEvent
//
// Wakes the user-driven worker when the hook queues work or a kill request.
//
//----------------------------------------------------------------------------
HANDLE GetAdvanceEvent()
{
    static wil::unique_event advanceEvent( wil::EventOptions::None );
    return advanceEvent.get();
}

//----------------------------------------------------------------------------
//
// SignalEpoch
//
//----------------------------------------------------------------------------
void SignalEpoch()
{
    SetEvent( GetEpochEvent() );
}

//----------------------------------------------------------------------------
//
// WaitForEpoch
//
// Waits until `predicate` holds or `timeoutMs` elapses. Returns the final
// value of `predicate`.
//
//----------------------------------------------------------------------------
template <typename Predicate>
bool WaitForEpoch( Predicate predicate, const DWORD timeoutMs = INFINITE )
{
    const ULONGLONG start = GetTickCount64();
    while( !predicate() )
    {
        DWORD wait = INFINITE;
        if( timeoutMs != INFINITE )
        {
            const ULONGLONG elapsed = GetTickCount64() - start;
            if( elapsed >= timeoutMs )
            {
                return predicate();
            }
            wait = static_cast<DWORD>(timeoutMs - elapsed);
        }
        WaitForSingleObject( GetEpochEvent(), wait );
    }
    return true;
}

//----------------------------------------------------------------------------
//
// RequestKill
//
//----------------------------------------------------------------------------
void RequestKill()
{
    g_Kill = true;
    SignalEpoch();
    SetEvent( GetAdvanceEvent() );
}

//----------------------------------------------------------------------------
//
// IsWindowNotepad
//
//----------------------------------------------------------------------------
bool IsWindowNotepad( const HWND hwnd )
{
    const int CLASS_NAME_LEN = 256;
    WCHAR className[CLASS_NAME_LEN];
    if( GetClassName( hwnd, className, CLASS_NAME_LEN ) > 0 )
    {
        if( wcscmp( className, L"Notepad" ) == 0 )
        {
            return true;
        }
    }
    return false;
}

//----------------------------------------------------------------------------
//
// IsDemoTypeInjection
//
//----------------------------------------------------------------------------
bool IsDemoTypeInjection( const KBDLLHOOKSTRUCT& kbd )
{
    return (kbd.flags & LLKHF_INJECTED) != 0 && kbd.dwExtraInfo == DEMOTYPE_INJECTION_TAG;
}

//----------------------------------------------------------------------------
//
// IsAutoFormatTrigger
//
//----------------------------------------------------------------------------
bool IsAutoFormatTrigger( wchar_t lastCh, wchar_t ch )
{
    // Will trigger auto-indentation in smart editors
    //     '\t' check also handles possible auto-completion
    if( ch == L'\n' || ch == L'\t' || (ch == L' ' && lastCh == L'\n') )
    {
        return true;
    }

    // Will trigger auto-close character(s) in smart editors
    if( ch == L'{' || ch == L'[' || ch == L'(' || (ch == L'*' && lastCh == L'/') )
    {
        return true;
    }

    return false;
}

//----------------------------------------------------------------------------
//
// IsNotPrintable
//
//----------------------------------------------------------------------------
bool IsNotPrintable( wchar_t ch )
{
    return ch != L'\n' && ch != L'\t' && !iswprint( ch );
}

//----------------------------------------------------------------------------
//
// MakeKeyInput
//
//----------------------------------------------------------------------------
constexpr INPUT MakeKeyInput( const WORD vK, const wchar_t ch, const bool keyup )
{
    INPUT input = {0};
    input.type = INPUT_KEYBOARD;
    input.ki.dwExtraInfo = DEMOTYPE_INJECTION_TAG;

    // Send unicode character via VK_PACKET
    if( vK == NULL )
    {
        input.ki.wScan = ch;
        input.ki.dwFlags = KEYEVENTF_UNICODE;
    }
    // Send virtual-key code
    else
    {
        input.ki.wVk = vK;

        if( vK == VK_RCONTROL || vK == VK_RMENU || vK == VK_LEFT
         || vK == VK_RIGHT || vK == VK_UP || vK == VK_DOWN )
        {
            input.ki.dwFlags |= KEYEVENTF_EXTENDEDKEY;
        }
    }

    if( keyup )
    {
        input.ki.dwFlags |= KEYEVENTF_KEYUP;
    }
    return input;
}

//----------------------------------------------------------------------------
//
// SendDemoTypeInputs
//
// Counts injections before sending them because the hook thread may observe
// them before SendInput returns.
//
//----------------------------------------------------------------------------
UINT SendDemoTypeInputs( INPUT* inputs, const UINT count )
{
    if( count == 0 )
    {
        return 0;
    }

    g_PendingInjections += static_cast<int>(count);
    const UINT sent = SendInput( count, inputs, sizeof( INPUT ) );
    if( sent < count )
    {
        g_PendingInjections -= static_cast<int>(count - sent);
    }
    return sent;
}

//----------------------------------------------------------------------------
//
// SendKeyUps
//
// Retries once so a partially blocked send cannot strand a key-down.
//
//----------------------------------------------------------------------------
void SendKeyUps( std::vector<INPUT>& keyUps )
{
    const UINT count = static_cast<UINT>(keyUps.size());
    const UINT sent = SendDemoTypeInputs( keyUps.data(), count );
    if( sent < count )
    {
        SendDemoTypeInputs( keyUps.data() + sent, count - sent );
    }
}

struct DemoTypeKey
{
    WORD    vK;
    wchar_t ch;
};

//----------------------------------------------------------------------------
//
// SendKeyChord
//
// Presses `keys` in order and releases them in reverse order. Every key-down
// is always followed by its key-up, so no injected key can be left logically
// pressed, even if part of the input could not be injected.
//
//----------------------------------------------------------------------------
void SendKeyChord( const std::initializer_list<DemoTypeKey> keys )
{
    std::vector<INPUT> keyDowns;
    std::vector<INPUT> keyUps;
    for( const auto& key : keys )
    {
        keyDowns.push_back( MakeKeyInput( key.vK, key.ch, false ) );
    }
    for( auto key = std::rbegin( keys ); key != std::rend( keys ); ++key )
    {
        keyUps.push_back( MakeKeyInput( key->vK, key->ch, true ) );
    }

    if( !g_Notepad )
    {
        std::vector<INPUT> chord( keyDowns );
        chord.insert( chord.end(), keyUps.begin(), keyUps.end() );

        const UINT count = static_cast<UINT>(chord.size());
        if( SendDemoTypeInputs( chord.data(), count ) < count )
        {
            // Releasing a key that is already up is harmless
            SendKeyUps( keyUps );
        }
        return;
    }

    // Add latency after each keydown to accommodate notepad input handling
    for( auto& keyDown : keyDowns )
    {
        SendDemoTypeInputs( &keyDown, 1 );
        std::this_thread::sleep_for( std::chrono::milliseconds( NOTEPAD_REFRESH ) );
    }
    SendKeyUps( keyUps );
}

//----------------------------------------------------------------------------
//
// SendUnicodeKeyPress
//
//----------------------------------------------------------------------------
void SendUnicodeKeyPress( const wchar_t ch )
{
    SendKeyChord( { { NULL, ch } } );
}

//----------------------------------------------------------------------------
//
// SendVirtualKeyPress
//
//----------------------------------------------------------------------------
void SendVirtualKeyPress( const WORD vK )
{
    SendKeyChord( { { vK, NULL } } );
}

//----------------------------------------------------------------------------
//
// SendVirtualKeyUp
//
//----------------------------------------------------------------------------
void SendVirtualKeyUp( const WORD vK )
{
    INPUT input = MakeKeyInput( vK, NULL, true );
    SendDemoTypeInputs( &input, 1 );
}

//----------------------------------------------------------------------------
//
// GetRandomNumber
//
//----------------------------------------------------------------------------
unsigned int GetRandomNumber( unsigned int lower, unsigned int upper )
{
    return lower + std::rand() % (upper - lower + 1);
}

//----------------------------------------------------------------------------
//
// BlockModifierKeys
//
// Releases held modifiers so they don't combine with injected keystrokes.
// Modifiers are only ever released here, never pressed again to restore them.
//
//----------------------------------------------------------------------------
void BlockModifierKeys()
{
    const std::vector<WORD> MODIFIERS = { VK_LSHIFT, VK_RSHIFT,
        VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU };

    if( (GetKeyState( VK_CAPITAL ) & 0x0001) != 0 )
    {
        SendVirtualKeyPress( VK_CAPITAL );
    }

    std::vector<INPUT> keyUps;
    for( auto modifier : MODIFIERS )
    {
        if( (GetKeyState( modifier ) & 0x8000) != 0 )
        {
            keyUps.push_back( MakeKeyInput( modifier, NULL, true ) );
        }
    }
    SendKeyUps( keyUps );
}

//----------------------------------------------------------------------------
//
// GetClipboardSequence
//
//----------------------------------------------------------------------------
DWORD GetClipboardSequence()
{
    DWORD sequence;
    if( !OpenClipboard( nullptr ) )
    {
        return 0;
    }
    sequence = GetClipboardSequenceNumber();
    CloseClipboard();
    return sequence;
}

//----------------------------------------------------------------------------
//
// GetClipboard
//
//----------------------------------------------------------------------------
std::unique_ptr<wchar_t[]> GetClipboard()
{
    if( !OpenClipboard( nullptr ) )
    {
        return nullptr;
    }
    auto closeClipboard = wil::scope_exit( [] { CloseClipboard(); } );
    if( !IsClipboardFormatAvailable( CF_UNICODETEXT ) )
    {
        return nullptr;
    }
    HANDLE hData = GetClipboardData( CF_UNICODETEXT );
    if( hData == nullptr )
    {
        return nullptr;
    }

    // Confirm clipboard size doesn't exceed MAX_INPUT_SIZE
    size_t size = GlobalSize( hData );
    if( size < sizeof(wchar_t) || size > MAX_INPUT_SIZE )
    {
        return nullptr;
    }

    const wchar_t* pData = static_cast<wchar_t*>(GlobalLock( hData ));
    if( pData == nullptr )
    {
        return nullptr;
    }
    auto unlockData = wil::scope_exit( [hData] { GlobalUnlock( hData ); } );

    const size_t textLen = wcsnlen_s( pData, size / sizeof(wchar_t) );
    auto data = std::make_unique<wchar_t[]>( textLen + 1 );
    memcpy( data.get(), pData, textLen * sizeof(wchar_t) );
    data[textLen] = L'\0';
    return data;
}

//----------------------------------------------------------------------------
//
// SetClipboard
//
//----------------------------------------------------------------------------
bool SetClipboard( const wchar_t* data )
{
    if( data == nullptr )
    {
        return false;
    }
    if( !OpenClipboard( nullptr ) )
    {
        CloseClipboard();
        return false;
    }
    EmptyClipboard();

    size_t size = (wcslen( data ) + 1) * sizeof( wchar_t );
    HGLOBAL hData = GlobalAlloc( GMEM_MOVEABLE, size );
    if( hData == nullptr )
    {
        CloseClipboard();
        return false;
    }

    wchar_t* pData = static_cast<wchar_t*>(GlobalLock( hData ));
    if( pData == nullptr )
    {
        GlobalUnlock( hData );
        GlobalFree( hData );
        CloseClipboard();
        return false;
    }

    wcscpy( pData, data );
    GlobalUnlock( hData );
    SetClipboardData( CF_UNICODETEXT, hData );    
    CloseClipboard();
    return true;
}

//----------------------------------------------------------------------------
//
// GetBaselineIndentation
//
//----------------------------------------------------------------------------
void GetBaselineIndentation()
{
    size_t len = 0;
    size_t lastLen = 0;
    bool resetCursor = true;
    std::unique_ptr<wchar_t[]> seekBuffer;
    static const WORD VK_C = static_cast<WORD>(LOBYTE( VkKeyScan( L'c' ) ));

    // VS fakes newline indentation until the user adds input. Notepad has no virtual
    // indentation to materialize, so avoid an insert/delete probe in its document.
    if( !g_Notepad )
    {
        SendVirtualKeyPress( VK_SPACE );
        SendVirtualKeyPress( VK_BACK  );
    }

    for( int i = 0; i < MAX_INDENT_DEPTH; i++ )
    {
        // Stop probing as soon as a kill is requested so the session can wind down promptly
        if( g_Kill )
        {
            resetCursor = false;
            break;
        }

        SendKeyChord( { { VK_LSHIFT, NULL }, { VK_LEFT, NULL } } );
        SendKeyChord( { { VK_LCONTROL, NULL }, { VK_C, NULL } } );

        std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );

        len = 0;
        seekBuffer = GetClipboard();        
        if( seekBuffer == nullptr )
        {
            resetCursor = false;
            break;
        }
        len = wcslen( seekBuffer.get() );

        if( g_LastClipboardSeq == GetClipboardSequence() )
        {
            resetCursor = false;
            break;
        }
        else if( seekBuffer[0] == L'\n' || seekBuffer[0] == L'\r' )
        {
            break;
        }
        else if( len == lastLen )
        {
            if( len == 0 )
            {
                resetCursor = false;
            }
            break;
        }
        lastLen = len;
    }

    if( resetCursor )
    {
        SendVirtualKeyPress( VK_RIGHT );
    }

    // Extract line indentation
    g_BaselineIndentation.clear();
    for( size_t i = 0; i < len; i++ )
    {
        if( iswprint( seekBuffer[i] ) && seekBuffer[i] != L' ' )
        {
            break;
        }
        else if( seekBuffer[i] == L'\t' || seekBuffer[i] == L' ' )
        {
            g_BaselineIndentation.push_back( seekBuffer[i] );
        }
    }
}

//----------------------------------------------------------------------------
//
// InjectByClipboard
//
// Editors handle paste operations slowly so we use this method sparingly
// 
//----------------------------------------------------------------------------
wchar_t InjectByClipboard( wchar_t lastCh, wchar_t ch, const std::wstring& override = L"" )
{
    int i = 0;
    bool trim = false;
    bool chunk = false;
    std::wstring injection(1, ch);
    static const WORD VK_V = static_cast<WORD>(LOBYTE( VkKeyScan( L'v' ) ));

    if( override == L"" )
    {
        if( ch == L'\n' && g_BaselineIndentation != L"" && g_BaselineIndentation != L"x" )
        {
            injection.append( g_BaselineIndentation );
        }

        // VS absorbs pasted line indentation so we inject it as a chunk of indents and the first printable ch
        if( lastCh == L'\n' && (ch == L'\t' || ch == L' ') )
        {
            chunk = true;
            for( i = 1; g_Index + i < g_TextLen; i++ )
            {
                injection.push_back( g_Text[g_Index + i] );
                if( g_Text[g_Index + i] != L' ' && iswprint( g_Text[g_Index + i] ) )
                {
                    if( g_Text[g_Index + i] == L'[' )
                    {
                        trim = true;
                    }
                    break;
                }
            }
        }
    }

    std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );
    if( !SetClipboard( override == L"" ? injection.c_str() : override.c_str() ) )
    {
        std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );
        SetClipboard( override == L"" ? injection.c_str() : override.c_str() );
    }
    std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );

    SendKeyChord( { { VK_LCONTROL, NULL }, { VK_V, NULL } } );

    std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );

    // Trim the last character from our chunk if it was [
    // Because it might be the start of a control keyword
    if( trim )
    {
        SendVirtualKeyPress( VK_BACK );

        g_Index += static_cast<unsigned long long>(i) - 1;
        return g_Text[g_Index];
    }
    else if( chunk )
    {
        g_Index += i;
        return g_Text[g_Index];
    }
    return NULL;
}

//----------------------------------------------------------------------------
//
// HandleControlKeyword
//
//----------------------------------------------------------------------------
bool HandleControlKeyword()
{
    size_t controlClose = g_Text.find( L']', g_Index );
    size_t controlLen = controlClose - g_Index + 1;

    if( controlLen <= MAX_CONTROL_LEN )
    {
        std::wstring control = g_Text.substr( g_Index, controlLen );

        if( control == L"[end]" )
        {
            g_End = true;
            g_Index += controlLen;
            g_TextSegments.push_back( g_Index );

            // In standard mode, [end] is interpreted as an immediate kill signal
            if( !g_UserDriven )
            {
                g_EmitterState = KILL_STATE;
            }

            return true;
        }
        else if( control.substr( 0, 7 ) == L"[pause:" )
        {
            g_Index += controlLen;

            if( g_UserDriven )
            {
                return true;
            }

            std::wistringstream iss(control.substr( 7, control.length() - 2 ));
            unsigned int time;

            if( iss >> time )
            {
                if( time > 0 )
                {
                    // Pause but poll for termination
                    for( int i = 0; i < static_cast<int>(1000 / DEMOTYPE_REFRESH * time); i++ )
                    {
                        if( g_EmitterState == KILL_STATE )
                        {
                            break;
                        }
                        std::this_thread::sleep_for( std::chrono::milliseconds( 50 ) );
                    }
                    return true;
                }
            }
        }
        else if( control == L"[paste]" )
        {
            size_t endControlOpen = g_Text.find( L"[/paste]", controlClose );
            if( endControlOpen != std::wstring::npos )
            {
                size_t endControlClose = g_Text.find( L']', endControlOpen );
                size_t endControlLen = endControlClose - g_Index + 1;

                std::wstring pasteData = g_Text.substr( controlClose + 1, endControlOpen - controlClose - 1 );
                InjectByClipboard( NULL, NULL, pasteData );

                g_Index += endControlLen;
                return true;
            }
        }
        else
        {
            if( control == L"[enter]" )
            {
                SendVirtualKeyPress( VK_RETURN );
            }
            else if( control == L"[up]" )
            {
                SendVirtualKeyPress( VK_UP );
            }
            else if( control == L"[down]" )
            {
                SendVirtualKeyPress( VK_DOWN );
            }
            else if( control == L"[left]" )
            {
                SendVirtualKeyPress( VK_LEFT );
            }
            else if( control == L"[right]" )
            {
                SendVirtualKeyPress( VK_RIGHT );
            }
            else
            {
                return false;
            }
            g_Index += controlLen;
            return true;
        }
    }
    return false;
}

//----------------------------------------------------------------------------
//
// HandleInjection
//
//----------------------------------------------------------------------------
void HandleInjection( bool init = false )
{
    static wchar_t lastCh = NULL;

    if( init )
    {
        if( g_Index == 0 )
        {
            g_TextSegments.clear();
        }

        lastCh = NULL;
        GetBaselineIndentation();
        return;
    }

    wchar_t ch = g_Text[g_Index];

    if( ch == L'[' )
    {
        if( HandleControlKeyword() )
        {
            return;
        }
    }

    if( IsAutoFormatTrigger( lastCh, ch ) )
    {
        wchar_t newCh = InjectByClipboard( lastCh, ch );
        if( newCh != NULL )
        {
            ch = newCh;
        }
    }
    else
    {
        SendUnicodeKeyPress( ch );
    }
    lastCh = ch;
    g_Index++;
}

//----------------------------------------------------------------------------
//
// DemoTypeEmitter
//
//----------------------------------------------------------------------------
void DemoTypeEmitter()
{    
    const unsigned int speed = static_cast<unsigned int>((MIN_TYPING_SPEED + MAX_TYPING_SPEED) - g_SpeedSlider);
    const unsigned int variance = static_cast<unsigned int>(speed * TYPING_VARIANCE);

    // Initialize the injection handler
    HandleInjection( true );

    while( g_EmitterState == ACTIVE_STATE && g_Index < g_TextLen )
    {
        HandleInjection();

        std::this_thread::sleep_for( std::chrono::milliseconds(
            GetRandomNumber( max( speed - variance, 1 ), max( speed + variance, 1 ) ) ) );
    }
    if( g_Index >= g_TextLen )
    {
        g_Index = 0;

        // Synthesize [end] at end of script if no [end] is present
        if( !g_End )
        {
            g_End = true;
            g_TextSegments.push_back( g_Index );
        }
    }

    g_EmitterState = INACTIVE_STATE;
    RequestKill();
}

//----------------------------------------------------------------------------
//
// AdvanceUserDriven
//
//----------------------------------------------------------------------------
void AdvanceUserDriven()
{
    // Inject n keys per 1 input keys where n is injectionRatio
    for( int i = 0; i < g_InjectionRatio && g_Index < g_TextLen && !g_End && !g_Kill; i++ )
    {
        HandleInjection();
    }
    if( g_Index >= g_TextLen )
    {
        g_Index = 0;

        // Synthesize [end] at end of script if no [end] is present
        if( !g_End )
        {
            g_End = true;
            g_TextSegments.push_back( g_Index );
        }
    }
}

//----------------------------------------------------------------------------
//
// DemoTypeUserDrivenWorker
//
// Types on behalf of the hook in user-driven mode so the hook itself never
// injects input, sleeps or touches the clipboard.
//
//----------------------------------------------------------------------------
void DemoTypeUserDrivenWorker()
{
    // Wait for the hook to finish negating modifiers
    while( !g_Kill && g_HookState != ACTIVE_STATE )
    {
        WaitForSingleObject( GetAdvanceEvent(), DEMOTYPE_REFRESH );
    }

    if( !g_Kill )
    {
        // Set baseline indentation to a blocking flag
        g_BaselineIndentation = INDENT_SEEK_FLAG;

        // Initialize the injection handler
        HandleInjection( true );
    }
    g_SeekingBaseline = false;

    while( !g_Kill )
    {
        if( g_PendingAdvances > 0 )
        {
            g_PendingAdvances--;
            AdvanceUserDriven();
        }
        else
        {
            WaitForSingleObject( GetAdvanceEvent(), INFINITE );
        }
    }
}

//----------------------------------------------------------------------------
//
// ActivateDemoTypeHook
//
// Moves the hook from INIT_STATE to ACTIVE_STATE once every modifier release
// we injected has passed through the hook.
//
//----------------------------------------------------------------------------
void ActivateDemoTypeHook()
{
    int expected = INIT_STATE;
    if( g_PendingInjections <= 0 && g_HookState.compare_exchange_strong( expected, ACTIVE_STATE ) )
    {
        SignalEpoch();
        SetEvent( GetAdvanceEvent() );
    }
}

//----------------------------------------------------------------------------
//
// BlockDemoTypeHook
//
// Moves the hook to BLOCK_STATE without overriding a KILL_STATE request.
//
//----------------------------------------------------------------------------
void BlockDemoTypeHook()
{
    int state = g_HookState;
    while( (state == START_STATE || state == INIT_STATE || state == ACTIVE_STATE)
        && !g_HookState.compare_exchange_weak( state, BLOCK_STATE ) )
    {
    }
}

//----------------------------------------------------------------------------
//
// DemoTypeHookProc
//
// Runs on the hook thread for every keystroke in the system, so it must only
// decide whether to swallow the keystroke and signal other threads.
//
//----------------------------------------------------------------------------
LRESULT CALLBACK DemoTypeHookProc( int nCode, WPARAM wParam, LPARAM lParam )
{
    if( nCode != HC_ACTION )
    {
        return CallNextHookEx( g_hHook, nCode, wParam, lParam );
    }

    const KBDLLHOOKSTRUCT* pKbdStruct = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
    const DWORD vkCode = pKbdStruct->vkCode & 0xFF;
    const bool injected = IsDemoTypeInjection( *pKbdStruct );
    const bool keyUp = (pKbdStruct->flags & LLKHF_UP) != 0;

    // The asynchronous key state is not yet updated for this keystroke
    const bool keyWasDown = (GetAsyncKeyState( static_cast<int>(vkCode) ) & 0x8000) != 0;
    const bool keyDownSwallowed = g_SwallowedKeys[vkCode];
    if( keyUp )
    {
        g_SwallowedKeys[vkCode] = false;
    }

    if( injected )
    {
        g_PendingInjections--;
    }

    bool swallow = false;
    const int state = g_HookState;
    if( state == KILL_STATE )
    {
        PostQuitMessage( 0 );
        swallow = true;
    }
    else if( state == BLOCK_STATE )
    {
        swallow = true;
    }
    else
    {
        const bool focusChanged = g_hWndFocus != GetForegroundWindow();

        // Handle two possible kill signals: user inputted escape or focus change
        if( (vkCode == VK_ESCAPE && !injected) || focusChanged )
        {
            // Notify the controller that the hook is going to BLOCK_STATE and requesting kill
            BlockDemoTypeHook();
            RequestKill();

            // In user-driven mode, we can kill the hook without controller approval
            // In standard mode, we need to stay alive until the emitter is terminated
            if( g_UserDriven )
            {
                PostQuitMessage( 0 );
            }

            // Only pass through if the kill signal was user input after a focus change
            swallow = focusChanged && injected;
        }
        else if( injected )
        {
            if( state == INIT_STATE )
            {
                ActivateDemoTypeHook();
            }
        }
        else
        {
            if( wParam == WM_KEYUP && g_UserDriven )
            {
                // In user-driven mode, [end] needs to be acknowledged by the user with a space before proceeding to kill
                if( vkCode == VK_SPACE && g_End )
                {
                    BlockDemoTypeHook();
                    RequestKill();
                    PostQuitMessage( 0 );
                }
                // Advance only on releases of keys pressed during the session (not the DemoType hotkey)
                else if( state == ACTIVE_STATE && keyDownSwallowed && !g_SeekingBaseline )
                {
                    g_PendingAdvances++;
                    SetEvent( GetAdvanceEvent() );
                }
            }
            swallow = true;
        }
    }

    // Never swallow the release of a key that the system still considers
    // pressed, otherwise it stays logically stuck until it is pressed again
    if( swallow && keyUp && keyWasDown && !keyDownSwallowed )
    {
        swallow = false;
    }

    if( swallow )
    {
        if( !keyUp && !keyWasDown )
        {
            g_SwallowedKeys[vkCode] = true;
        }
        return 1;
    }
    return CallNextHookEx( g_hHook, nCode, wParam, lParam );
}

//----------------------------------------------------------------------------
//
// StartDemoTypeHook
//
//----------------------------------------------------------------------------
void StartDemoTypeHook()
{
    MSG msg;

    // Create the message queue before publishing the thread id so the controller can post WM_QUIT
    PeekMessage( &msg, nullptr, WM_USER, WM_USER, PM_NOREMOVE );
    g_HookThreadId = GetCurrentThreadId();
    std::fill( std::begin( g_SwallowedKeys ), std::end( g_SwallowedKeys ), false );

    g_hHook = SetWindowsHookEx( WH_KEYBOARD_LL, DemoTypeHookProc, GetModuleHandle( nullptr ), 0 );
    if( g_hHook == nullptr )
    {
        g_HookThreadId = 0;
        g_HookState = INACTIVE_STATE;
        SignalEpoch();
        return;
    }

    std::thread worker;
    int expected = START_STATE;
    if( g_HookState.compare_exchange_strong( expected, INIT_STATE ) )
    {
        g_hWndFocus = GetForegroundWindow();
        g_Notepad = IsWindowNotepad( g_hWndFocus );
        if( g_UserDriven )
        {
            g_InjectionRatio = min( 3, max( 1, static_cast<int>(g_SpeedSlider / THIRD_TYPING_SPEED) + 1 ) );
            worker = std::thread( DemoTypeUserDrivenWorker );
        }

        // Block non-injected input until we've negated all modifiers
        BlockModifierKeys();
        ActivateDemoTypeHook();

        while( GetMessage( &msg, nullptr, 0, 0 ) > 0 )
        {
            TranslateMessage( &msg );
            DispatchMessage( &msg );
        }
    }

    RequestKill();

    // Keep servicing the hook while the worker winds down so its remaining
    // injections are still filtered and system input never waits on us
    if( worker.joinable() )
    {
        HANDLE hWorker = worker.native_handle();
        while( MsgWaitForMultipleObjects( 1, &hWorker, FALSE, INFINITE, QS_ALLINPUT ) == WAIT_OBJECT_0 + 1 )
        {
            while( PeekMessage( &msg, nullptr, 0, 0, PM_REMOVE ) )
            {
                if( msg.message != WM_QUIT )
                {
                    TranslateMessage( &msg );
                    DispatchMessage( &msg );
                }
            }
        }
        worker.join();
    }

    UnhookWindowsHookEx( g_hHook );
    g_hHook = nullptr;
    g_HookThreadId = 0;

    // Clean up any trailing shift modifier from our injections
    if( (GetKeyState( VK_LSHIFT ) & 0x8000) != 0 )
    {
        SendVirtualKeyUp( VK_LSHIFT );
    }

    g_HookState = INACTIVE_STATE;
    SignalEpoch();
}

//----------------------------------------------------------------------------
//
// KillDemoTypeHook
//
//----------------------------------------------------------------------------
void KillDemoTypeHook()
{
    int state = g_HookState;
    do
    {
        if( state == INACTIVE_STATE )
        {
            return;
        }
    } while( !g_HookState.compare_exchange_weak( state, KILL_STATE ) );

    // Wake the hook thread even if no keyboard input arrives
    const DWORD hookThreadId = g_HookThreadId;
    if( hookThreadId != 0 )
    {
        PostThreadMessage( hookThreadId, WM_QUIT, 0, 0 );
    }
}

//----------------------------------------------------------------------------
//
// WaitForDemoTypeHookExit
//
//----------------------------------------------------------------------------
void WaitForDemoTypeHookExit()
{
    WaitForEpoch( [] { return g_HookState == INACTIVE_STATE; }, DEMOTYPE_TIMEOUT );
}

//----------------------------------------------------------------------------
//
// DemoTypeController
//
//----------------------------------------------------------------------------
void DemoTypeController()
{
    g_PendingInjections = 0;
    g_PendingAdvances = 0;
    g_SeekingBaseline = g_UserDriven;
    g_End = false;
    g_Kill = false;
    g_Active = true;
    g_HookState = START_STATE;

    std::thread( StartDemoTypeHook ).detach();

    // Wait for the hook to negate held modifiers and start blocking input
    const bool started = WaitForEpoch( [] {
        return g_HookState == ACTIVE_STATE || g_HookState == INACTIVE_STATE || g_Kill;
    }, DEMOTYPE_TIMEOUT );
    if( !started || (g_HookState == INACTIVE_STATE && !g_Kill) )
    {
        KillDemoTypeHook();
        WaitForDemoTypeHookExit();
        g_Active = false;
        return;
    }

    // Spool up the emitter
    if( !g_UserDriven && !g_Kill )
    {
        g_EmitterState = ACTIVE_STATE;
        std::thread( DemoTypeEmitter ).detach();
    }

    // Wait for kill request
    WaitForEpoch( [] { return g_Kill.load(); } );

    // Send kill messages
    if( !g_UserDriven )
    {
        if( g_EmitterState != INACTIVE_STATE )
        {
            g_EmitterState = KILL_STATE;
            BlockDemoTypeHook();

            WaitForEpoch( [] { return g_EmitterState == INACTIVE_STATE; }, DEMOTYPE_TIMEOUT );
        }
    }
    KillDemoTypeHook();

    // The user-driven worker types on the hook thread's behalf, so wait for it
    // to stop before restoring the clipboard and moving the script index
    WaitForDemoTypeHookExit();

    if( g_ClipboardCache != nullptr )
    {
        SetClipboard( g_ClipboardCache.get() );
        g_LastClipboardSeq = GetClipboardSequence();
    }

    // Upon kill, hop to the next text segment if kill wasn't triggered by an [end]
    if( g_Index != 0 && !g_End )
    {
        size_t nextEnd = g_Text.find( L"[end]", g_Index );
        if( nextEnd == std::wstring::npos )
        {
            g_Index = 0;
        }
        else
        {
            g_Index = nextEnd + END_CONTROL_LEN;
            g_TextSegments.push_back( g_Index );
            if( g_Index >= g_TextLen )
            {
                g_Index = 0;
            }
        }
    }

    g_Active = false;
}

//----------------------------------------------------------------------------
//
// TrimNewlineAroundControl
//
//----------------------------------------------------------------------------
void TrimNewlineAroundControl( const std::wstring control, const bool trimLeft, const bool trimRight )
{
    const size_t controlLen = control.length();

    // Seek first occurrence of `control` in `g_Text`
    size_t nextControl = g_Text.find( control );

    // Loop through each occurrence of `control` in `g_Text`
    while( nextControl != std::wstring::npos )
    {
        // Erase the character to the left of `control` if it is a newline
        if( trimLeft && nextControl > 0 && g_Text[nextControl - 1] == L'\n' )
        {
            g_Text.erase( nextControl - 1, 1 );
            // Decrement `nextControl` to account for `g_Text` shrinking to left of `nextControl`
            nextControl--;
        }

        // Erase the character to the right of `control` if it is a newline
        if( trimRight && (nextControl + controlLen) < g_Text.length() && g_Text[nextControl + controlLen] == L'\n' )
        {
            g_Text.erase( nextControl + controlLen, 1 );
        }

        // Seek next occurrence of `control` in `g_Text`
        nextControl = g_Text.find( control, nextControl + controlLen);

        // Shrink `g_Text` to new size on last pass
        if( nextControl == std::wstring::npos )
        {
            g_Text.shrink_to_fit();
        }
    }
}

//----------------------------------------------------------------------------
//
// CleanDemoTypeText
//
//----------------------------------------------------------------------------
bool CleanDemoTypeText()
{
    // Remove all unsupported characters from our text buffer
    g_Text.erase( std::remove_if( g_Text.begin(), g_Text.end(), IsNotPrintable ), g_Text.end() );
    g_Text.shrink_to_fit();

    // Remove the first character if it is a newline
    if( g_Text.length() > 0 && g_Text[0] == L'\n' )
    {
        g_Text.erase( 0, 1 );
        g_Text.shrink_to_fit();
    }

    // Trim a newline character to the left and right of each [end] control
    TrimNewlineAroundControl( L"[end]", true, true );

    // Trim a newline character to the right of each [paste] control
    TrimNewlineAroundControl( L"[paste]", false, true );

    // Trim a newline character to the left of each [/paste] control
    TrimNewlineAroundControl( L"[/paste]", true, false );

    // Remove any dangling whitespace after the last [end]
    size_t lastEnd = g_Text.rfind( L"[end]" );
    if( lastEnd != std::wstring::npos )
    {
        for( size_t i = lastEnd + END_CONTROL_LEN; i < g_Text.length(); i++ )
        {
            if( iswprint( g_Text[i] ) && g_Text[i] != L' ' )
            {
                break;
            }
            else if( i >= g_Text.length() - 1 )
            {
                g_Text.erase( lastEnd + END_CONTROL_LEN );
                g_Text.shrink_to_fit();
            }
        }
    }

    g_TextLen = g_Text.length();
    if( g_TextLen > 0 )
    {
        return true;
    }
    else
    {
        return false;
    }
}

//----------------------------------------------------------------------------
//
// ResetDemoTypeClipboard
//
//----------------------------------------------------------------------------
void ResetDemoTypeClipboard()
{
    if( g_Clipboard )
    {
        g_Text.clear();
        g_Clipboard = false;
    }
}

//----------------------------------------------------------------------------
//
// GetDemoTypeClipboard
//
//----------------------------------------------------------------------------
bool GetDemoTypeClipboard()
{
    const size_t safetyPrefixLen = 7;
    const wchar_t safetyPrefix[] = L"[start]";

    // Check if we can reuse the clipboard cache
    DWORD sequenceNum = GetClipboardSequence();
    if( g_LastClipboardSeq == sequenceNum && g_Clipboard )
    {
        return true;
    }
    g_LastClipboardSeq = sequenceNum;

    g_ClipboardCache = GetClipboard();

    // Preserve the existing prefix match, which ignores its first character.
    if( g_ClipboardCache == nullptr
     || wcsnlen_s( g_ClipboardCache.get(), safetyPrefixLen + 1 ) <= safetyPrefixLen
     || wcsncmp( g_ClipboardCache.get() + 1, safetyPrefix + 1, safetyPrefixLen - 1 ) != 0 )
    {
        ResetDemoTypeClipboard();
        return false;
    }
    g_Text.assign( g_ClipboardCache.get() + safetyPrefixLen );
    g_Clipboard = true;
    g_Index = 0;
    return CleanDemoTypeText();
}

//----------------------------------------------------------------------------
//
// GetDemoTypeFile
//
// Supported encoding: UTF-8, UTF-8 with BOM, UTF-16LE, UTF-16BE
//
//----------------------------------------------------------------------------
int GetDemoTypeFile( const TCHAR* filePath )
{
    std::ifstream file(filePath, std::ios::binary);
    if( !file.is_open() )
    {
        return ERROR_LOADING_FILE;
    }

    // Confirm file size doesn't exceed MAX_INPUT_SIZE
    file.seekg( 0, std::ios::end );
    std::streampos size = file.tellg();
    file.seekg( 0, std::ios::beg );
    if( size <= 0 || size > MAX_INPUT_SIZE )
    {
        return FILE_SIZE_OVERFLOW;
    }

    // Grab the potential Byte Order Mark
    // Which identifies the encoding pattern
    char byteOrderMark[3];
    file.read( byteOrderMark, 3 );
    file.seekg( 0, std::ios::beg );

    // UTF-16 is a variable-length character encoding pattern
    //   - code points are encoded with one or two 16-bit code units
    //   - 16-bit code units are composed of byte pairs subject to endianness
    //      - Little-endian Byte Order Mark {0xFF, 0xFE, ...}
    //      -    Big-endian Byte Order Mark {0xFE, 0xFF, ...}

    // UTF-8 is a variable-length character encoding pattern
    //   - code points are encoded with one to four 8-bit code units
    //   - optional Byte Order Mark {0xEF, 0xBB, 0xBF, ...}

    // UTF-16LE
    if( byteOrderMark[0] == static_cast<char>(0xFF)
     && byteOrderMark[1] == static_cast<char>(0xFE) )
    {
        // Truncate the Byte Order Mark
        file.seekg( 2 );

        char bytePair[2];
        wchar_t codeUnit;
        while( file.read( bytePair, 2 ) )
        {
            // Squash each little-endian byte pair into a 2-byte code unit
            //   if bytePair[0] = 0xff
            //   if bytePair[1] = 0x00
            //         codeUnit = 0x00ff
            codeUnit = (static_cast<wchar_t>(bytePair[1]) << 8)
                      | static_cast<wchar_t>(bytePair[0]);

            g_Text += codeUnit;
        }
    }
    // UTF-16BE
    else if( byteOrderMark[0] == static_cast<char>(0xFE)
          && byteOrderMark[1] == static_cast<char>(0xFF) )
    {
        // Truncate the Byte Order Mark
        file.seekg( 2 );

        char bytePair[2];
        wchar_t codeUnit;
        while( file.read( bytePair, 2 ) )
        {
            // Squash each big-endian byte pair into a 2-byte code unit
            //   if bytePair[0] = 0xff
            //   if bytePair[1] = 0x00
            //         codeUnit = 0xff00
            codeUnit = (static_cast<wchar_t>(bytePair[0]) << 8)
                      | static_cast<wchar_t>(bytePair[1]);

            g_Text += codeUnit;
        }
    }
    // UTF-8
    else
    {
        // If UTF-8 with BOM, truncate the Byte Order Mark
        if( byteOrderMark[0] == static_cast<char>(0xEF)
         && byteOrderMark[1] == static_cast<char>(0xBB)
         && byteOrderMark[2] == static_cast<char>(0xBF) )
        {
            file.seekg( 3 );
        }

        std::stringstream buffer;
        buffer << file.rdbuf();
        std::string narrowText = buffer.str();

        // Determine the size our wide string will need to be to accommodate the conversion
        int wideSize = MultiByteToWideChar( CP_UTF8, 0, narrowText.c_str(), -1, nullptr, 0 );
        if( wideSize <= 0 )
        {
            return ERROR_LOADING_FILE;
        }

        g_Text.resize( wideSize );

        // Map the multi-byte capable char string onto the wide char string
        if( MultiByteToWideChar( CP_UTF8, 0, narrowText.c_str(), -1, &g_Text[0], wideSize ) <= 0 )
        {
            return ERROR_LOADING_FILE;
        }
    }

    g_Index = 0;
    return CleanDemoTypeText() ? 0 : UNKNOWN_FILE_DATA;
}

//----------------------------------------------------------------------------
//
// ResetDemoTypeIndex
//
//----------------------------------------------------------------------------
void ResetDemoTypeIndex()
{
    size_t newIndex = 0;

    if( !g_TextSegments.empty() && g_Index <= g_TextSegments.back() )
    {
        g_TextSegments.pop_back();
    }
    if( !g_TextSegments.empty() )
    {
        newIndex = g_TextSegments.back();
    }

    g_Index = newIndex;
}

//----------------------------------------------------------------------------
//
// StartDemoType
//
//----------------------------------------------------------------------------
int StartDemoType( const TCHAR* filePath, const DWORD speedSlider, const BOOLEAN userDriven )
{
    static FILETIME lastFileWrite = {0};

    if( g_Active )
    {
        return -1;
    }

    if( !GetDemoTypeClipboard() )
    {
        if( _tcslen( filePath ) == 0 )
        {
            return NO_FILE_SPECIFIED;
        }

        if( _tcscmp( g_LastFilePath, filePath ) != 0 )
        {
            _tcscpy( g_LastFilePath, filePath );
            // Trigger (re)capture of lastFileWrite
            g_Text = L"x";
            g_Index = 0;
            lastFileWrite = {0};
        }

        // Check if the file has been updated since last read
        if( !g_Text.empty() )
        {
            wil::unique_hfile hFile{ CreateFile( filePath, GENERIC_READ, FILE_SHARE_READ,
                nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr ) };
            if( !hFile )
            {
                return ERROR_LOADING_FILE;
            }

            FILETIME latestFileWrite;
            if( GetFileTime( hFile.get(), nullptr, nullptr, &latestFileWrite ) )
            {
                if( CompareFileTime( &latestFileWrite, &lastFileWrite ) == 1 )
                {
                    g_Text.clear();
                    lastFileWrite = latestFileWrite;
                }
            }
        }

        if( g_Text.empty() )
        {
            switch( GetDemoTypeFile( filePath ) )
            {
            case ERROR_LOADING_FILE:
                return ERROR_LOADING_FILE;

            case FILE_SIZE_OVERFLOW:
                return FILE_SIZE_OVERFLOW;

            case UNKNOWN_FILE_DATA:
                return UNKNOWN_FILE_DATA;
            }
        }
    }

    g_UserDriven = userDriven;
    g_SpeedSlider = speedSlider;
    std::thread( DemoTypeController ).detach();
    return 0;
}
