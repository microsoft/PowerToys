#pragma once
#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"

class KeyboardManager
{
public:
    static const inline DWORD StartHookMessageID = WM_APP + 1;

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

    bool HasRegisteredRemappings() const;

private:
    // Returns whether there are any remappings available without waiting for settings to load
    bool HasRegisteredRemappingsUnchecked() const;

    // Contains the non localized module name
    std::wstring moduleName = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;

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

    HANDLE editorIsRunningEvent = nullptr;

    // Watchdog state used to detect a low level hook that Windows dropped, see
    // KeyboardManager.cpp. Only touched from the engine's message loop thread, which is also
    // the thread the hook procedure runs on. lastHookEventTick is in GetTickCount units so it
    // can be compared against LASTINPUTINFO::dwTime.
    UINT_PTR hookWatchdogTimerId = 0;
    DWORD lastHookEventTick = 0;
    bool hookProbePending = false;
    int missedHookProbes = 0;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Timer procedure which verifies that the low level hook is still installed
    static void CALLBACK HookWatchdogTimerProc(HWND window, UINT message, UINT_PTR timerId, DWORD elapsed);

    void StartHookWatchdog();
    void StopHookWatchdog();
    void VerifyHookIsStillInstalled();

    // Load settings from the file.
    void LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
