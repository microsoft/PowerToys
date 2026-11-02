#pragma once
#include <atomic>
#include <chrono>
#include <memory>
#include <mutex>
#include <string>
#include <unordered_map>

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/utils/EventWaiter.h>
#include <keyboardmanager/common/Input.h>
#include "State.h"
#include "ProfileSwapGate.h"
#include "RawInputKeyboardTracker.h"
#include "ProfileCycleHotkey.h"

class KeyboardManager
{
public:
    // Posted to the hook thread to run ReconcileHooks.
    static const inline DWORD StartHookMessageID = WM_APP + 1;

    // Constructor
    KeyboardManager();

    ~KeyboardManager()
    {
        // Stop the settings-changed listener first and wait for any in-flight LoadSettings callback
        // to finish (it has a 500 ms retry). It is declared before the profile members below, so
        // without this join it would still be listening while those members — the maps, mutexes,
        // active/requested profile strings and hotkey/tracker pointers it reads — are destroyed in
        // reverse declaration order, letting a resuming callback touch freed state.
        settingsEventWaiter.stop();

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

    // Hook thread (StartHookMessageID): apply a staged profile change if no keyboard hook is installed,
    // then install or remove the keyboard/mouse hooks to match the live remap tables. A stage that
    // arrives while the hook is being removed is applied right after the removal.
    void ReconcileHooks();

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

    // Low level hook handles. Installed on the hook thread; read (and removed) from the settings
    // watcher thread too, hence atomic.
    static std::atomic<HHOOK> hookHandle;

    // Required for Unhook in old versions of Windows
    static HHOOK hookHandleCopy;

    // Companion low-level mouse hook handles (see StartLowlevelMouseHook).
    static std::atomic<HHOOK> mouseHookHandle;
    static HHOOK mouseHookHandleCopy;

    // Static pointer to the current KeyboardManager object required for accessing the HandleKeyboardHookEvent function in the hook procedure
    // Only global or static variables can be accessed in a hook procedure CALLBACK
    static KeyboardManager* keyboardManagerObjectPtr;

    // Variable which stores all the state information to be shared between the UI and back-end
    State state;

    // Decides when a reload that changes the active profile may replace `state`'s tables: on the hook
    // thread, at a fresh physical key-down while no physical key is held (see ProfileSwapGate.h).
    ProfileSwapGate profileSwapGate{
        [] { return std::chrono::steady_clock::now(); },
        [](DWORD vkCode) { return (GetAsyncKeyState(static_cast<int>(vkCode)) & 0x8000) != 0; }
    };

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

    // The profile whose tables are live in `state`. Set by the initial load, by a same-profile reload,
    // and on the hook thread right after a staged profile is adopted. Read on the tracker thread.
    std::wstring activeProfileName;
    std::mutex activeProfileMutex;

    // Tracker-thread-only auto-switch hysteresis state.
    std::wstring pendingTarget;
    int pendingCount = 0;

    // The profile the engine is heading to but has not adopted yet: set when the tracker requests a
    // switch and when a reload stages a profile change; cleared once that profile is adopted and when a
    // same-profile reload is applied. When a reload fails it is reset to the pending stage's profile,
    // or cleared if nothing is staged (so the tracker can retry). The tracker
    // compares a keyboard's profile with this (or with activeProfileName when it is empty), which both
    // dedupes requests while a switch is in flight and lets typing on the current profile's keyboard
    // cancel a pending switch away from it. Guarded by activeProfileMutex.
    std::wstring requestedProfile;

    // Last keyboard seen, logged on change to help discover device paths for the profile map.
    std::wstring lastSeenDevice;

    // Hook procedure definition
    static LRESULT CALLBACK HookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Mouse hook procedure definition (companion to HookProc for the "Alone" dual-key feature)
    static LRESULT CALLBACK MouseHookProc(int nCode, WPARAM wParam, LPARAM lParam);

    // Initial load of the settings, in place (before any hook or reload callback exists).
    void LoadSettings();

    // Settings-changed callback (settings watcher thread). Parses the configuration into a separate
    // State, then lets profileSwapGate apply it in place (same profile) or stage it (profile change).
    void OnSettingsChanged();

    // Load the active profile into a fresh State, retrying once after 500 ms if the load was not clean.
    static std::unique_ptr<State> LoadConfiguration(MappingConfiguration::LoadOutcome& outcome);

    // Hook thread, under the gate lock: make a staged configuration live and record the new profile.
    void AdoptStagedConfiguration(std::unique_ptr<State> staged);

    // Free a configuration's tables on a thread-pool thread instead of the hook thread.
    static void RetireConfiguration(std::unique_ptr<State> configuration);

    // Keys GetAsyncKeyState reports down right now (left/right modifier variants only).
    static std::vector<ProfileSwapGate::KeySnapshot> SnapshotKeysDown();

    // Function called by the hook procedure to handle the events. This is the starting point function for remapping
    intptr_t HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept;
};
