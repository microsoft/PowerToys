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

// Each injection is tracked so that the hook
// procedure can identify injections and allow them
// to pass through while blocking accidental keystrokes.
//
// Each injection is identified by either a virtual
// key code or a unicode character passed as a scan code
// which is wrapped into a VK_PACKET by SendInput when
// the KEYEVENTF_UNICODE flag is specified.
//
// VK_PACKET allows us to synthesize keystrokes which are
// not mapped to virtual-key codes (e.g. foreign characters).
struct Injection
{
    DWORD vkCode;
    DWORD scanCode;

    Injection( DWORD vkCode, DWORD scanCode )
        : vkCode(vkCode), scanCode(scanCode) {}
};

bool                    g_UserDriven = false;
bool                    g_Notepad = false;
bool                    g_Clipboard = false;
TCHAR                   g_LastFilePath[MAX_PATH] = {0};
HHOOK                   g_hHook = nullptr;
size_t                  g_TextLen = 0;
wchar_t*                g_ClipboardCache = nullptr;
std::wstring            g_Text = L"";
std::vector<size_t>     g_TextSegments;
std::wstring            g_BaselineIndentation = L"";
std::atomic<size_t>     g_Index = 0;
std::condition_variable g_EpochReady;
std::mutex              g_EpochMutex;
std::deque<Injection>   g_Injections;
std::mutex              g_InjectionsMutex;
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
// IsInjected
//
//----------------------------------------------------------------------------
bool IsInjected( const DWORD vkCode, const DWORD scanCode )
{
    bool injected = false;
    bool locked = false;
    if( g_EmitterState == ACTIVE_STATE )
    {
        g_InjectionsMutex.lock();
        locked = true;
    }

    if( !g_Injections.empty() )
    {
        if( (g_Injections.front().vkCode != NULL && g_Injections.front().vkCode == vkCode)
         || (g_Injections.front().vkCode == NULL && g_Injections.front().scanCode == scanCode) )
        {
            injected = true;
        }
    }

    if( locked )
    {
        g_InjectionsMutex.unlock();
    }
    return injected;
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
// PopInjection
// 
// See comments above `Injection` struct definition
//
//----------------------------------------------------------------------------
void PopInjection()
{
    bool locked = false;
    if( g_EmitterState == ACTIVE_STATE )
    {
        g_InjectionsMutex.lock();
        locked = true;
    }

    g_Injections.pop_front();

    if( locked )
    {
        g_InjectionsMutex.unlock();
    }
}

//----------------------------------------------------------------------------
//
// PushInjection
// 
// See comments above `Injection` struct definition
//
//----------------------------------------------------------------------------
void PushInjection( const WORD vK, const wchar_t ch )
{
    bool locked = false;
    if( g_EmitterState == ACTIVE_STATE )
    {
        g_InjectionsMutex.lock();
        locked = true;
    }

    g_Injections.push_back( Injection( static_cast<DWORD>(vK), static_cast<DWORD>(ch) ) );
    
    if( locked )
    {
        g_InjectionsMutex.unlock();
    }
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
// SendKeyInput
//
//----------------------------------------------------------------------------
void SendKeyInput( const WORD vK, const wchar_t ch, const bool keyup = false )
{
    INPUT input = {0};
    input.type = INPUT_KEYBOARD;

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

    SendInput( 1, &input, sizeof( INPUT ) );

    // Add latency between keydown/up to accomodate notepad input handling
    if( !keyup && g_Notepad )
    {
        std::this_thread::sleep_for( std::chrono::milliseconds( NOTEPAD_REFRESH ) );
    }
}

//----------------------------------------------------------------------------
//
// SendUnicodeKeyDown
//
//----------------------------------------------------------------------------
void SendUnicodeKeyDown( const wchar_t ch )
{
    PushInjection( NULL, ch );
    SendKeyInput ( NULL, ch );
}

//----------------------------------------------------------------------------
//
// SendUnicodeKeyUp
//
//----------------------------------------------------------------------------
void SendUnicodeKeyUp( const wchar_t ch )
{
    PushInjection( NULL, ch );
    SendKeyInput ( NULL, ch, true );
}

//----------------------------------------------------------------------------
//
// SendVirtualKeyDown
//
//----------------------------------------------------------------------------
void SendVirtualKeyDown( const WORD vK )
{
    PushInjection( vK, NULL );
    SendKeyInput ( vK, NULL );
}

//----------------------------------------------------------------------------
//
// SendVirtualKeyUp
//
//----------------------------------------------------------------------------
void SendVirtualKeyUp( const WORD vK )
{
    PushInjection( vK, NULL );
    SendKeyInput ( vK, NULL, true );
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
//----------------------------------------------------------------------------
int BlockModifierKeys()
{
    int blockDepth = 0;
    const std::vector<WORD> MODIFIERS = { VK_LSHIFT, VK_RSHIFT,
        VK_LCONTROL, VK_RCONTROL, VK_LMENU, VK_RMENU };

    if( (GetKeyState( VK_CAPITAL ) & 0x0001) != 0 )
    {
        SendVirtualKeyDown( VK_CAPITAL );
        SendVirtualKeyUp  ( VK_CAPITAL );
    }
    for( auto modifier : MODIFIERS )
    {
        if( (GetKeyState( modifier ) & 0x8000) != 0 )
        {
            blockDepth++;
            SendVirtualKeyUp( modifier );
        }
    }

    return blockDepth;
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
        CloseClipboard();
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
wchar_t* GetClipboard()
{
    // Confirm clipboard accessibility and data format
    if( !OpenClipboard( nullptr ) && !IsClipboardFormatAvailable( CF_UNICODETEXT ) )
    {
        CloseClipboard();
        return nullptr;
    }
    HANDLE hData = GetClipboardData( CF_UNICODETEXT );
    if( hData == nullptr )
    {
        CloseClipboard();
        return nullptr;
    }

    // Confirm clipboard size doesn't exceed MAX_INPUT_SIZE
    size_t size = GlobalSize( hData );
    if( size <= 0 || size > MAX_INPUT_SIZE )
    {
        GlobalUnlock( hData );
        CloseClipboard();
        return nullptr;
    }

    const wchar_t* pData = static_cast<wchar_t*>(GlobalLock( hData ));
    if( pData == nullptr )
    {
        GlobalUnlock( hData );
        CloseClipboard();
        return nullptr;
    }

    wchar_t* data = new wchar_t[size / sizeof(wchar_t)];
    wcscpy( data, pData );
    GlobalUnlock( hData );
    CloseClipboard();
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
    wchar_t* seekBuffer = nullptr;
    static const WORD VK_C = static_cast<WORD>(LOBYTE( VkKeyScan( L'c' ) ));

    // VS fakes newline indentation until the user adds input
    SendVirtualKeyDown( VK_SPACE );
    SendVirtualKeyUp  ( VK_SPACE );
    SendVirtualKeyDown( VK_BACK  );
    SendVirtualKeyUp  ( VK_BACK  );

    for( int i = 0; i < MAX_INDENT_DEPTH; i++ )
    {
        SendVirtualKeyDown( VK_LSHIFT );
        SendVirtualKeyDown( VK_LEFT   );
        SendVirtualKeyUp  ( VK_LEFT   );
        SendVirtualKeyUp  ( VK_LSHIFT );

        SendVirtualKeyDown( VK_LCONTROL );
        SendVirtualKeyDown( VK_C        );
        SendVirtualKeyUp  ( VK_C        );
        SendVirtualKeyUp  ( VK_LCONTROL );

        std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );

        len = 0;
        delete[] seekBuffer;
        seekBuffer = GetClipboard();        
        if( seekBuffer == nullptr )
        {
            resetCursor = false;
            break;
        }
        len = wcslen( seekBuffer );

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
        SendVirtualKeyDown( VK_RIGHT );
        SendVirtualKeyUp  ( VK_RIGHT );
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

    delete[] seekBuffer;
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

    SendVirtualKeyDown( VK_LCONTROL );
    SendVirtualKeyDown( VK_V        );
    SendVirtualKeyUp  ( VK_V        );
    SendVirtualKeyUp  ( VK_LCONTROL );

    std::this_thread::sleep_for( std::chrono::milliseconds( CLIPBOARD_REFRESH ) );

    // Trim the last character from our chunk if it was [
    // Because it might be the start of a control keyword
    if( trim )
    {
        SendVirtualKeyDown( VK_BACK );
        SendVirtualKeyUp  ( VK_BACK );

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
                SendVirtualKeyDown( VK_RETURN );
                SendVirtualKeyUp  ( VK_RETURN );
            }
            else if( control == L"[up]" )
            {
                SendVirtualKeyDown( VK_UP );
                SendVirtualKeyUp  ( VK_UP );
            }
            else if( control == L"[down]" )
            {
                SendVirtualKeyDown( VK_DOWN );
                SendVirtualKeyUp  ( VK_DOWN );
            }
            else if( control == L"[left]" )
            {
                SendVirtualKeyDown( VK_LEFT );
                SendVirtualKeyUp  ( VK_LEFT );
            }
            else if( control == L"[right]" )
            {
                SendVirtualKeyDown( VK_RIGHT );
                SendVirtualKeyUp  ( VK_RIGHT );
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
        SendUnicodeKeyDown( ch );
        SendUnicodeKeyUp  ( ch );
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
    g_Kill = true;
    {
        std::lock_guard<std::mutex> epochLock(g_EpochMutex);
    }
    g_EpochReady.notify_one();
}

//----------------------------------------------------------------------------
//
// DemoTypeHookProc
//
//----------------------------------------------------------------------------
LRESULT CALLBACK DemoTypeHookProc( int nCode, WPARAM wParam, LPARAM lParam )
{
    static HWND hWndFocus = nullptr;
    static int injectionRatio = 1;
    static int blockDepth = 0;

    if( g_HookState == KILL_STATE )
    {
        PostQuitMessage( 0 );
        return 1;
    }
    else if( g_HookState == START_STATE )
    {
        g_HookState = INIT_STATE;
        if( g_UserDriven )
        {
            injectionRatio = min( 3, max( 1, static_cast<int>(g_SpeedSlider / THIRD_TYPING_SPEED) + 1 ) );
        }

        hWndFocus = GetForegroundWindow();
        g_Notepad = IsWindowNotepad( hWndFocus );
        blockDepth = BlockModifierKeys();
    }

    if( nCode == HC_ACTION )
    {
        KBDLLHOOKSTRUCT* pKbdStruct = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);

        bool injected = IsInjected( pKbdStruct->vkCode, pKbdStruct->scanCode );

        // Block non-injected input until we've negated all modifiers
        if( g_HookState == INIT_STATE )
        {
            if( g_Injections.empty() )
            {
                g_HookState = ACTIVE_STATE;
                {
                    std::lock_guard<std::mutex> epochLock(g_EpochMutex);
                }
                g_EpochReady.notify_one();

                if( g_UserDriven )
                {
                    // Set baseline indentation to a blocking flag
                    // Otherwise indentation seeking will trigger user-driven injection events
                    g_BaselineIndentation = INDENT_SEEK_FLAG;

                    // Initialize the injection handler
                    HandleInjection( true );
                }
                return 1;
            }
        }
        else if( g_HookState == BLOCK_STATE )
        {
            return 1;
        }

        // Handle two possible kill signals: user inputted escape or focus change
        if( (pKbdStruct->vkCode == VK_ESCAPE && !injected) || hWndFocus != GetForegroundWindow() )
        {
            // Notify the controller that the hook is going to BLOCK_STATE and requesting kill
            g_HookState = BLOCK_STATE;
            g_Kill = true;
            {
                std::lock_guard<std::mutex> epochLock(g_EpochMutex);
            }
            g_EpochReady.notify_one();

            // In user-driven mode, we can kill the hook without controller approval
            // In standard mode, we need to stay alive until the emitter is terminated
            if( g_UserDriven )
            {
                PostQuitMessage( 0 );
            }

            // Only pass through if the kill signal was user input after a focus change
            if( hWndFocus != GetForegroundWindow() && injected )
            {
                return 1;
            }
        }
        else if( injected )
        {
            PopInjection();
        }
        else
        {
            switch( wParam )
            {
            case WM_KEYUP:
                // In user-driven mode, [end] needs to be acknowledged by the user with a space before proceeding to kill
                if( pKbdStruct->vkCode == VK_SPACE && g_UserDriven && g_End )
                {
                    // Notify the controller that the hook is going to BLOCK_STATE and requesting kill
                    g_HookState = BLOCK_STATE;
                    g_Kill = true;
                    {
                        std::lock_guard<std::mutex> epochLock(g_EpochMutex);
                    }
                    g_EpochReady.notify_one();

                    PostQuitMessage( 0 );
                }
                else if( g_UserDriven )
                {
                    // Block up to the number of DEMOTYPE_HOTKEY keys
                    if( blockDepth > 0 )
                    {
                        blockDepth--;
                        return 1;
                    }
                    else if( g_BaselineIndentation == INDENT_SEEK_FLAG )
                    {
                        return 1;
                    }
                    
                    // Inject n keys per 1 input keys where n is injectionRatio
                    for( int i = 0; i < injectionRatio && g_Index < g_TextLen && !g_End; i++ )
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
                [[fallthrough]];
            case WM_KEYDOWN:
            case WM_SYSKEYUP:
            case WM_SYSKEYDOWN:
                return 1;
            }
        }
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
    g_hHook = SetWindowsHookEx( WH_KEYBOARD_LL, DemoTypeHookProc, GetModuleHandle( nullptr ), 0 );
    if( g_hHook == nullptr )
    {
        g_HookState = INACTIVE_STATE;
        return;
    }

    // Jump start the hook with an inert message to prevent a stall
    KBDLLHOOKSTRUCT KbdStruct{};
    DemoTypeHookProc( HC_ACTION, 0, reinterpret_cast<LPARAM>(&KbdStruct) );

    MSG msg;
    while( GetMessage( &msg, nullptr, 0, 0 ) )
    {
        TranslateMessage( &msg );
        DispatchMessage( &msg );
    }

    UnhookWindowsHookEx( g_hHook );
    g_hHook = nullptr;

    // Clean up any trailing shift modifier from our injections
    if( (GetKeyState( VK_LSHIFT ) & 0x8000) != 0 )
    {
        SendVirtualKeyUp( VK_LSHIFT );
    }

    g_HookState = INACTIVE_STATE;
}

//----------------------------------------------------------------------------
//
// KillDemoTypeHook
//
//----------------------------------------------------------------------------
void KillDemoTypeHook()
{
    if( g_HookState != INACTIVE_STATE )
    {
        g_HookState = KILL_STATE;
        SendVirtualKeyUp( VK_ESCAPE );
    }
}

//----------------------------------------------------------------------------
//
// DemoTypeController
//
//----------------------------------------------------------------------------
void DemoTypeController()
{
    std::chrono::milliseconds timeout(DEMOTYPE_TIMEOUT);

    g_Injections.clear();
    g_End = false;
    g_Kill = false;
    g_Active = true;
    g_HookState = START_STATE;

    std::thread( StartDemoTypeHook ).detach();

    // Spool up the emitter
    if( !g_UserDriven )
    {
        std::unique_lock<std::mutex> epochLock(g_EpochMutex);
        if( g_EpochReady.wait_for( epochLock, timeout,
            [] { return g_HookState == ACTIVE_STATE; } ) )
        { 
            g_EmitterState = ACTIVE_STATE;
            std::thread( DemoTypeEmitter ).detach();
        }
        else
        {
            KillDemoTypeHook();
            g_Active = false;
            return;
        }
    }

    // Wait for kill request
    {
        std::unique_lock<std::mutex> epochLock(g_EpochMutex);
        g_EpochReady.wait( epochLock, [] { return g_Kill == true; } );
    }

    // Send kill messages
    if( !g_UserDriven )
    {
        if( g_EmitterState != INACTIVE_STATE )
        {
            g_EmitterState = KILL_STATE;
            g_HookState = BLOCK_STATE;

            std::unique_lock<std::mutex> epochLock(g_EpochMutex);
            g_EpochReady.wait_for( epochLock, timeout, [] { return g_EmitterState == INACTIVE_STATE; } );
        }
    }
    KillDemoTypeHook();

    if( g_ClipboardCache != nullptr )
    {
        SetClipboard( g_ClipboardCache );
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
    const int safetyPrefixLen = 7;
    const wchar_t safetyPrefix[] = L"[start]";

    // Check if we can reuse the clipboard cache
    DWORD sequenceNum = GetClipboardSequence();
    if( g_LastClipboardSeq == sequenceNum && g_Clipboard )
    {
        return true;
    }
    g_LastClipboardSeq = sequenceNum;

    delete[] g_ClipboardCache;
    g_ClipboardCache = GetClipboard();

    // Confirm clipboard data begins with the safety prefix
    if( g_ClipboardCache == nullptr || g_ClipboardCache[0] != g_ClipboardCache[0] || g_ClipboardCache[safetyPrefixLen] == '\0' )
    {
        ResetDemoTypeClipboard();
        return false;
    }
    for( int i = 1; i < safetyPrefixLen; i++ )
    {
        if( g_ClipboardCache[i] != safetyPrefix[i] || g_ClipboardCache[i] == '\0' )
        {
            ResetDemoTypeClipboard();
            return false;
        }
    }
    
    g_Text.assign( g_ClipboardCache + safetyPrefixLen );
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

        // Determine the size our wide string will need to be to accomodate the conversion
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
            HANDLE hFile = CreateFile( filePath, GENERIC_READ, FILE_SHARE_READ,
                nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr );
            if( hFile == INVALID_HANDLE_VALUE )
            {
                CloseHandle( hFile );
                return ERROR_LOADING_FILE;
            }

            FILETIME latestFileWrite;
            if( GetFileTime( hFile, nullptr, nullptr, &latestFileWrite ) )
            {
                if( CompareFileTime( &latestFileWrite, &lastFileWrite ) == 1 )
                {
                    g_Text.clear();
                    lastFileWrite = latestFileWrite;
                }
            }
            CloseHandle( hFile );
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