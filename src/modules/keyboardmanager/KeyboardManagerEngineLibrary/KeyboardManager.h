#pragma once
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"

class KeyboardManager
{
public:
    static const inline DWORD StartHookMessageID = WM_APP + 1;

    // Posted to the hook thread when the settings have no remapping left (see StopLowlevelKeyboardHookWhenIdle).
    static const inline DWORD StopHookMessageID = WM_APP + 2;

    // Constructor
    KeyboardManager();

    ~KeyboardManager()
    {
        if (editorIsRunningEvent)
        {
            CloseHandle(editorIsRunningEvent);
        }
    }

    void StartLowlevelKeyboardHook();
    void StopLowlevelKeyboardHook();

    // Removes the hook because the settings have no remapping left, unless the remappings are back
    // or a press that a key sequence replaced is still held: the rest of that press must still be
    // suppressed, so the hook stays until its key-up and is removed then. Hook thread only.
    void StopLowlevelKeyboardHookWhenIdle();

    bool HasRegisteredRemappings() const;

private:
    // Returns whether there are any remappings available without waiting for settings to load
    bool HasRegisteredRemappingsUnchecked() const;

    // Companion low-level mouse hook, installed alongside the keyboard hook only while "Alone"
    // (dual-key) remaps exist. It promotes a held alone key to a real modifier when a click/scroll
    // arrives, so combinations like Ctrl+Click / Ctrl+Wheel work (the keyboard hook handles the
    // key-combination case). Idle otherwise.
    void StartLowlevelMouseHook();
    void StopLowlevelMouseHook();
    void HandleMouseHookEvent() noexcept;

    // Contains the non localized module name
    std::wstring moduleName = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;

    // Companion low-level mouse hook handles (see StartLowlevelMouseHook).
    static HHOOK mouseHookHandle;
    static HHOOK mouseHookHandleCopy;

    // Static pointer to the current KeyboardManager object required for accessing the HandleKeyboardHookEvent function in the hook procedure
    // Only global or static variables can be accessed in a hook procedure CALLBACK
    static KeyboardManager* keyboardManagerObjectPtr;

    // Variable which stores all the state information to be shared between the UI and back-end
    State state;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;

    // Auto reset event for waiting for settings changes. The event is signaled when settings are changed
    EventWaiter settingsEventWaiter;

    std::atomic_bool loadingSettings = false;

    // Set by the thread that loads the settings: true when the settings loaded last have no remapping.
    std::atomic_bool noRemappingsLoaded = false;

    // True while the hook waits for the held presses that key sequences replaced to end before it
    // is removed (see StopLowlevelKeyboardHookWhenIdle). Hook thread only.
    bool hookStopPending = false;

    HANDLE editorIsRunningEvent = nullptr;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Mouse hook procedure definition (companion to HookProc for the "Alone" dual-key feature)
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Load settings from the file.
    void LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
