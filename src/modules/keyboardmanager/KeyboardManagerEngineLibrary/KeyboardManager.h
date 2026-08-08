#pragma once
#include <atomic>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"
#include "RawInputKeyboardTracker.h"
#include "ProfileCycleHotkey.h"

class KeyboardManager
{
public:
    static const inline DWORD StartHookMessageID = WM_APP + 1;

    // Constructor
    KeyboardManager();

    ~KeyboardManager()
    {
        // Stop the worker threads first so they can't call back into a half-destroyed object.
        if (rawInputTracker)
        {
            rawInputTracker->Stop();
        }

        if (profileCycleHotkey)
        {
            profileCycleHotkey->Stop();
        }

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

    // Detects which physical keyboard is being typed on, for per-keyboard profile auto-switching.
    std::unique_ptr<RawInputKeyboardTracker> rawInputTracker;

    // Global hotkey that cycles through the profiles (definition in deviceProfiles.json).
    std::unique_ptr<ProfileCycleHotkey> profileCycleHotkey;

    // Serializes profile switches (tracker thread and hotkey thread both call SwitchActiveProfile).
    std::mutex switchProfileMutex;

    // Called (on the tracker thread) for each raw keyboard event.
    void OnRawKeyEvent(const RawInputKeyboardTracker::KeyEvent& keyEvent);

    // Reload the device->profile map, the auto-switch enable flag, and the cycle-hotkey
    // definition from deviceProfiles.json.
    void LoadDeviceProfiles();

    // Make the given profile active by writing settings.json + signaling the settings-changed
    // event, so the existing reload path applies it (avoids a second thread mutating `state`).
    void SwitchActiveProfile(const std::wstring& profile);

    // Advance to the next profile in settings.json's keyboardConfigurations list (hotkey action).
    void CycleActiveProfile();

    // Number of consecutive clean keystrokes on a keyboard before its profile is auto-selected
    // (hysteresis to avoid thrashing when two keyboards are used in quick alternation).
    static constexpr int AutoSwitchThreshold = 2;

    // Auto-switch config (shared with the tracker thread; guarded).
    std::atomic_bool autoSwitchEnabled{ false };
    std::unordered_map<std::wstring, std::wstring> deviceProfileMap;
    std::mutex deviceMapMutex;

    // The currently active profile name, updated on every settings load. Read on the tracker thread.
    std::wstring activeProfileName;
    std::mutex activeProfileMutex;

    // Tracker-thread-only auto-switch policy state.
    std::wstring pendingTarget;
    int pendingCount = 0;
    std::wstring requestedProfile;

    // Last keyboard seen, logged on change to help discover device paths for the profile map.
    std::wstring lastSeenDevice;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Load settings from the file.
    void LoadSettings();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
