#pragma once
#include <bitset>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"
#include "TextExpansionController.h"

class KeyboardManager
{
public:
    static const inline DWORD ReloadSettingsMessageID = WM_APP + 1;
    static const inline DWORD TextExpansionCommitMessageID = WM_APP + 2;

    // Constructor
    KeyboardManager();

    ~KeyboardManager();

    void StartLowlevelKeyboardHook();
    void StopLowlevelKeyboardHook();
    void Shutdown() noexcept;

    bool HasRegisteredRemappings() const;

    // Applies a settings notification on the hook-owning thread. Reload is deferred
    // until active remap and Text Expansion transactions finish.
    void ReloadSettings();
    void CompletePendingTextExpansion() noexcept;

private:
    // Returns whether there are any remappings available without waiting for settings to load
    bool HasRegisteredRemappingsUnchecked() const;

    // Companion low-level mouse hook shared by buffered Text Expansion and "Alone" remaps.
    // Button presses invalidate the text buffer; button/wheel input promotes a held alone key
    // to a real modifier for combinations such as Ctrl+Click and Ctrl+Wheel.
    void StartLowlevelMouseHook();
    void StopLowlevelMouseHook();
    void HandleMouseHookEvent() noexcept;

    // Contains the non localized module name
    std::wstring moduleName = KeyboardManagerConstants::ModuleName;

    // Low level hook handles
    static HHOOK hookHandle;
    static HHOOK mouseHookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;
    static HHOOK mouseHookHandleCopy;

    // Static pointer to the current KeyboardManager object required for accessing the HandleKeyboardHookEvent function in the hook procedure
    // Only global or static variables can be accessed in a hook procedure CALLBACK
    static KeyboardManager* keyboardManagerObjectPtr;

    // Variable which stores all the state information to be shared between the UI and back-end
    State state;

    // Object of class which implements InputInterface. Required for calling library functions while enabling testing
    KeyboardManagerInput::Input inputHandler;

    std::unique_ptr<TextExpansionController> textExpansionController;

    // Auto reset event for waiting for settings changes. The event is signaled when settings are changed
    EventWaiter settingsEventWaiter;

    std::atomic_bool loadingSettings = false;
    std::atomic_bool settingsReloadDeferred = false;
    std::atomic_bool deferredReloadPosted = false;
    std::atomic_bool shutdownStarted = false;

    HANDLE editorIsRunningEvent = nullptr;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);
    static void CALLBACK DeferredReloadTimerProc(HWND, UINT, UINT_PTR timerId, DWORD);

    // Load settings from the file.
    void LoadSettings();
    void ArmDeferredReloadTimer() noexcept;
    void QueueDeferredSettingsReloadIfReady() noexcept;
    bool HasPendingInputWork() const noexcept;

    UINT_PTR deferredReloadTimer = 0;
    uint64_t textExpansionInstanceId = 0;
    std::bitset<512> activeRemapPresses;

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
