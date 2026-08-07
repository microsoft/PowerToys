#pragma once
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include <thread>
#include "State.h"

class KeyboardManager
{
public:
    static const inline DWORD ReloadSettingsMessageID = WM_APP + 1;

    // Constructor
    KeyboardManager();
    ~KeyboardManager();

    void StartLowlevelKeyboardHook();
    void StopLowlevelKeyboardHook();
    void ReloadSettings();

    bool HasRegisteredRemappings() const;

private:
    // Contains the non localized module name
    std::wstring moduleName = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;

    // Low-level mouse hook used to invalidate caret-sensitive text replacement state.
    static HHOOK mouseHookHandle;

    // Static pointer to the current KeyboardManager object required for accessing the HandleKeyboardHookEvent function in the hook procedure
    // Only global or static variables can be accessed in a hook procedure CALLBACK
    static KeyboardManager* keyboardManagerObjectPtr;

    // Variable which stores all the state information to be shared between the UI and back-end
    State state;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;

    // Auto reset event for waiting for settings changes. The event is signaled when settings are changed
    EventWaiter settingsEventWaiter;

    HANDLE editorIsRunningEvent = nullptr;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static void CALLBACK TextReplacementWinEventProc(HWINEVENTHOOK hook, DWORD event, HWND window, LONG objectId, LONG childId, DWORD eventThread, DWORD eventTime);

    void StartTextReplacementContextTracking();
    void StopTextReplacementContextTracking() noexcept;
    void TextReplacementContextThreadProc();
    bool IsEditorRunning();

    HWINEVENTHOOK textReplacementForegroundHook = nullptr;
    HWINEVENTHOOK textReplacementFocusHook = nullptr;
    HWINEVENTHOOK textReplacementDesktopHook = nullptr;
    HANDLE textReplacementContextStopEvent = nullptr;
    HANDLE textReplacementContextRefreshEvent = nullptr;
    std::thread textReplacementContextThread;
    std::atomic<DWORD> textReplacementContextThreadId = 0;

    // Load settings from the file.
    void LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
