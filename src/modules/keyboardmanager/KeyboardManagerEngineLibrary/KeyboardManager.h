#pragma once
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"
#include "EditorSuspensionState.h"

class KeyboardManager
{
public:
    static const inline DWORD UpdateHookMessageID = WM_APP + 1;

    // Constructor
    KeyboardManager();

    ~KeyboardManager()
    {
        if (editorIsRunningEvent)
        {
            CloseHandle(editorIsRunningEvent);
        }

        if (editorLifetimeMutex)
        {
            CloseHandle(editorLifetimeMutex);
        }

        if (editorCaptureReadyEvent)
        {
            CloseHandle(editorCaptureReadyEvent);
        }
    }

    void StartLowlevelKeyboardHook();
    void StopLowlevelKeyboardHook();
    void UpdateLowlevelKeyboardHook();

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

    // Published by the settings loader; hook lifecycle updates must not wait for a
    // configuration reload or read its mutable mapping tables on the hook thread.
    std::atomic_bool hasRegisteredRemappings = false;
    std::atomic_bool hasAloneRemappings = false;

    HANDLE editorIsRunningEvent = nullptr;

    HANDLE editorLifetimeMutex = nullptr;

    HANDLE editorCaptureReadyEvent = nullptr;

    EditorSuspensionState editorSuspensionState;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Mouse hook procedure definition (companion to HookProc for the "Alone" dual-key feature)
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Load settings from the file.
    void LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
