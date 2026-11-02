#include "pch.h"
#include "KeyboardManager.h"
#include <interface/powertoy_module_interface.h>
#include <common/SettingsAPI/settings_objects.h>
#include <common/SettingsAPI/settings_helpers.h>
#include <common/interop/shared_constants.h>
#include <common/debug_control.h>
#include <common/utils/winapi_error.h>
#include <common/utils/json.h>
#include <common/logger/logger_settings.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Helpers.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include <ctime>
#include <filesystem>

#include "KeyboardEventHandlers.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
HHOOK KeyboardManager::hookHandle;
HHOOK KeyboardManager::mouseHookHandle;
HHOOK KeyboardManager::mouseHookHandleCopy;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    // A RIDI_DEVICENAME path looks like "\\?\HID#<deviceId>#<instanceId>#<interfaceGuid>". Some
    // virtual keyboards (e.g. Target_KIP) are assigned a fresh <instanceId> over time, which would
    // break an exact-path match. Match on the stable prefix instead: everything up to the instance
    // (the 2nd '#'). This keeps distinct keyboards apart while tolerating instance-id churn.
    //
    // Known, intentional limitation (SPEC §7): the dropped <instanceId> is also what tells two
    // keyboards of the SAME model apart, so an identical pair collapses to one identity and shares
    // one profile. Accepted for the MVP — the churn this fixes is common, a second identical
    // keyboard is rare. The editor's NormalizeDevicePath applies the exact same rule so both sides
    // agree. Per-instance identity for identical models is deferred, not overlooked.
    std::wstring NormalizeDevicePath(const std::wstring& path)
    {
        const size_t first = path.find(L'#');
        if (first == std::wstring::npos)
        {
            return path;
        }

        const size_t second = path.find(L'#', first + 1);
        if (second == std::wstring::npos)
        {
            return path;
        }

        return path.substr(0, second);
    }
}

namespace
{
    DWORD mainThreadId = {};

    // Thread-pool callback that frees the tables a profile swap replaced (see RetireConfiguration).
    void CALLBACK DeleteRetiredConfiguration(PTP_CALLBACK_INSTANCE, PVOID context)
    {
        delete static_cast<State*>(context);
    }
}

KeyboardManager::KeyboardManager()
{
    mainThreadId = GetCurrentThreadId();

    // Load the initial settings.
    LoadSettings();

    // Set the static pointer to the newest object of the class
    keyboardManagerObjectPtr = this;

    std::filesystem::path modulePath(PTSettingsHelper::get_module_save_folder_location(moduleName));
    auto changeSettingsCallback = [this](DWORD err) {
        Logger::trace(L"{} event was signaled", KeyboardManagerConstants::SettingsEventName);
        if (err != ERROR_SUCCESS)
        {
            Logger::error(L"Failed to watch settings changes. {}", get_last_error_or_default(err));
        }

        try
        {
            OnSettingsChanged();
        }
        catch (...)
        {
            Logger::error("Failed to load settings");
        }
    };

    editorIsRunningEvent = CreateEvent(nullptr, true, false, KeyboardManagerConstants::EditorWindowEventName.c_str());
    settingsEventWaiter.start(KeyboardManagerConstants::SettingsEventName, changeSettingsCallback);

    // Start detecting which physical keyboard is being typed on (for per-keyboard profile switching).
    rawInputTracker = std::make_unique<RawInputKeyboardTracker>(
        [this](const RawInputKeyboardTracker::KeyEvent& keyEvent) { OnRawKeyEvent(keyEvent); });
    rawInputTracker->Start();

    // Global profile-cycle hotkey. Re-read the config so the definition parsed before this object
    // existed is applied (LoadDeviceProfiles guards on the pointer).
    profileCycleHotkey = std::make_unique<ProfileCycleHotkey>([this] { CycleActiveProfile(); });
    profileCycleHotkey->Start();
    LoadDeviceProfiles();
}

void KeyboardManager::OnRawKeyEvent(const RawInputKeyboardTracker::KeyEvent& keyEvent)
{
    // Injected input (hDevice == NULL, incl. KBM's own remap output) is not physical and never
    // triggers a switch. Hysteresis is driven by key-downs only.
    //
    // The switch is requested right away: the reload it triggers is staged, and the low-level hook
    // applies it only at a fresh key-down while no physical key is held (ProfileSwapGate), so a key
    // held on any keyboard is still released under the profile it was pressed under.
    if (keyEvent.injected || !keyEvent.keyDown)
    {
        return;
    }

    // Ignore modifier keys: they lead every chord (including the profile-cycle hotkey, whose own
    // Shift/Alt key-downs would otherwise feed the hysteresis and fight the cycle), and a lone
    // modifier is weak evidence that the user moved to this keyboard.
    switch (keyEvent.vkey)
    {
    case VK_SHIFT:
    case VK_CONTROL:
    case VK_MENU:
    case VK_LSHIFT:
    case VK_RSHIFT:
    case VK_LCONTROL:
    case VK_RCONTROL:
    case VK_LMENU:
    case VK_RMENU:
    case VK_LWIN:
    case VK_RWIN:
        return;
    default:
        break;
    }

    // A physical keystroke whose device path can't be resolved (observed on Surface Type Cover
    // right after idle, when its KIP device node re-enumerates). Log it so drops are visible.
    if (keyEvent.devicePath.empty())
    {
        Logger::trace(L"[autosw] keydown vk=0x{:x} with unresolvable device — skipped", keyEvent.vkey);
        return;
    }

    // Log the active keyboard when it changes (helps discover device paths for the profile map).
    if (keyEvent.devicePath != lastSeenDevice)
    {
        lastSeenDevice = keyEvent.devicePath;
        Logger::trace(L"Detected keyboard: {}", keyEvent.devicePath);
    }

    if (!autoSwitchEnabled.load())
    {
        return;
    }

    // Which profile is this keyboard bound to? Unmapped keyboards keep the current profile.
    std::wstring target;
    {
        std::lock_guard<std::mutex> lock(deviceMapMutex);
        auto it = deviceProfileMap.find(NormalizeDevicePath(keyEvent.devicePath));
        if (it == deviceProfileMap.end())
        {
            return;
        }

        target = it->second;
    }

    std::wstring current;
    std::wstring requested;
    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        current = activeProfileName;
        requested = requestedProfile;
    }

    // Where the engine is heading: the requested/staged profile while a switch is in flight, else the
    // live one. Matching it means there is nothing to do (this also dedupes requests during the
    // reload and staging window). Typing on the live profile's keyboard while a switch away from it
    // is pending does not match, so it requests the live profile again, which supersedes the pending
    // stage (with the same hysteresis as any other switch).
    const std::wstring& heading = requested.empty() ? current : requested;
    if (target == heading)
    {
        pendingTarget.clear();
        pendingCount = 0;
        return;
    }

    // Hysteresis: require a few consecutive keystrokes on the new keyboard before switching.
    if (target == pendingTarget)
    {
        ++pendingCount;
    }
    else
    {
        pendingTarget = target;
        pendingCount = 1;
    }

    if (pendingCount < AutoSwitchThreshold)
    {
        return;
    }

    pendingCount = 0;
    pendingTarget.clear();

    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        requestedProfile = target;
    }

    Logger::trace(L"Auto-switch: keyboard {} -> requesting profile '{}'", keyEvent.devicePath, target);
    SwitchActiveProfile(target);
}

void KeyboardManager::LoadDeviceProfiles()
{
    bool enabled = false;
    std::unordered_map<std::wstring, std::wstring> map;
    UINT hotkeyModifiers = 0;
    UINT hotkeyVk = 0;

    try
    {
        const auto path = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\deviceProfiles.json";
        auto parsed = json::from_file(path);
        if (parsed.has_value())
        {
            const json::JsonObject& obj = parsed.value();
            if (obj.HasKey(L"autoSwitchEnabled"))
            {
                enabled = obj.GetNamedBoolean(L"autoSwitchEnabled", false);
            }

            if (obj.HasKey(L"map"))
            {
                const auto arr = obj.GetNamedArray(L"map");
                for (uint32_t i = 0; i < arr.Size(); ++i)
                {
                    const auto entry = arr.GetObjectAt(i);
                    std::wstring device{ entry.GetNamedString(L"device", L"") };
                    std::wstring profile{ entry.GetNamedString(L"profile", L"") };
                    if (!device.empty() && !profile.empty())
                    {
                        map[NormalizeDevicePath(device)] = profile;
                    }
                }
            }

            // The editor writes "cycleHotkey": null when no hotkey is set; GetNamedObject would throw on
            // that and log a load failure on every reload, so treat anything but an object as "none".
            if (obj.HasKey(L"cycleHotkey") && obj.GetNamedValue(L"cycleHotkey").ValueType() == json::JsonValueType::Object)
            {
                const auto hotkey = obj.GetNamedObject(L"cycleHotkey");
                hotkeyVk = static_cast<UINT>(hotkey.GetNamedNumber(L"code", 0));
                if (hotkey.GetNamedBoolean(L"win", false))
                {
                    hotkeyModifiers |= MOD_WIN;
                }
                if (hotkey.GetNamedBoolean(L"ctrl", false))
                {
                    hotkeyModifiers |= MOD_CONTROL;
                }
                if (hotkey.GetNamedBoolean(L"alt", false))
                {
                    hotkeyModifiers |= MOD_ALT;
                }
                if (hotkey.GetNamedBoolean(L"shift", false))
                {
                    hotkeyModifiers |= MOD_SHIFT;
                }
            }
        }
    }
    catch (...)
    {
        Logger::error(L"Failed to load deviceProfiles.json");
    }

    {
        std::lock_guard<std::mutex> lock(deviceMapMutex);
        deviceProfileMap = std::move(map);
    }

    autoSwitchEnabled.store(enabled);

    if (profileCycleHotkey)
    {
        profileCycleHotkey->Update(hotkeyModifiers, hotkeyVk);
    }
}

void KeyboardManager::CycleActiveProfile()
{
    try
    {
        const auto path = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\settings.json";
        auto parsed = json::from_file(path);
        if (!parsed.has_value())
        {
            return;
        }

        const auto properties = parsed.value().GetNamedObject(L"properties");
        const std::wstring current{ properties.GetNamedObject(KeyboardManagerConstants::ActiveConfigurationSettingName).GetNamedString(L"value", KeyboardManagerConstants::DefaultConfiguration) };

        std::vector<std::wstring> profiles;
        if (properties.HasKey(L"keyboardConfigurations"))
        {
            const auto arr = properties.GetNamedObject(L"keyboardConfigurations").GetNamedArray(L"value");
            for (uint32_t i = 0; i < arr.Size(); ++i)
            {
                profiles.emplace_back(arr.GetStringAt(i));
            }
        }

        if (profiles.size() < 2)
        {
            return; // nothing to cycle to
        }

        size_t currentIndex = 0;
        for (size_t i = 0; i < profiles.size(); ++i)
        {
            if (profiles[i] == current)
            {
                currentIndex = i;
                break;
            }
        }

        const std::wstring& next = profiles[(currentIndex + 1) % profiles.size()];
        Logger::trace(L"CycleActiveProfile: '{}' -> '{}'", current, next);
        SwitchActiveProfile(next);

        // Audible feedback that the profile changed (no UI surface in the engine).
        MessageBeep(MB_OK);
    }
    catch (...)
    {
        Logger::error(L"CycleActiveProfile failed");
    }
}

void KeyboardManager::SwitchActiveProfile(const std::wstring& profile)
{
    // Tracker thread and hotkey thread can both land here; serialize the read-modify-write.
    std::lock_guard<std::mutex> lock(switchProfileMutex);

    // Refuse to activate a profile whose config file is gone. A tracker or hotkey callback can hold a
    // stale target (e.g. captured from the device map just before the profile was deleted), and
    // writing that as activeConfiguration would leave a deleted profile active — its file missing, the
    // engine unable to load it, its old mappings stranded. Validate at commit time and drop the stale
    // request instead. "default" is exempt: it is the protected fallback (never deletable) and is
    // created on demand by the editor, so it must always be switchable to.
    if (profile != KeyboardManagerConstants::DefaultConfiguration)
    {
        const auto configPath = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\" + profile + L".json";
        std::error_code ec;
        if (!std::filesystem::exists(configPath, ec))
        {
            Logger::error(L"Switch: refusing to activate profile '{}' — its config file is missing", profile);
            std::lock_guard<std::mutex> activeLock(activeProfileMutex);
            if (requestedProfile == profile)
            {
                requestedProfile.clear();
            }
            return;
        }
    }

    try
    {
        const auto path = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\settings.json";
        auto parsed = json::from_file(path);
        if (!parsed.has_value())
        {
            // A transient read/parse failure must NOT be treated as an empty settings object:
            // writing that back would erase every other Keyboard Manager property (e.g.
            // keyboardConfigurations). Abort and leave the file untouched, matching
            // CycleActiveProfile. The next keystroke retries the switch.
            Logger::error(L"Auto-switch: settings.json unreadable; leaving it untouched");

            // Clear the request so the next keystroke on this keyboard can retry: OnRawKeyEvent
            // sets requestedProfile before calling us, and otherwise its target == requested guard
            // would filter out all further input from that keyboard and wedge the switch.
            {
                std::lock_guard<std::mutex> lock(activeProfileMutex);
                requestedProfile.clear();
            }
            return;
        }

        json::JsonObject root = parsed.value();
        json::JsonObject properties = root.HasKey(L"properties") ? root.GetNamedObject(L"properties") : json::JsonObject{};

        json::JsonObject activeConfiguration;
        activeConfiguration.SetNamedValue(L"value", json::JsonValue::CreateStringValue(profile));
        properties.SetNamedValue(KeyboardManagerConstants::ActiveConfigurationSettingName, activeConfiguration);
        root.SetNamedValue(L"properties", properties);

        json::to_file(path, root);
    }
    catch (...)
    {
        Logger::error(L"Failed to write activeConfiguration for auto-switch");
        {
            std::lock_guard<std::mutex> lock(activeProfileMutex);
            requestedProfile.clear();
        }
        return;
    }

    // Reuse the existing reload path: the engine's own settings watcher will apply the new profile.
    HANDLE hEvent = CreateEvent(nullptr, false, false, KeyboardManagerConstants::SettingsEventName.c_str());
    if (hEvent)
    {
        SetEvent(hEvent);
        CloseHandle(hEvent);
    }
    else
    {
        Logger::error(L"Auto-switch: failed to signal settings event");
    }
}

void KeyboardManager::LoadSettings()
{
    bool loadedSuccessful = state.LoadSettings();
    if (!loadedSuccessful)
    {
        std::this_thread::sleep_for(std::chrono::milliseconds(500));

        // retry once
        state.LoadSettings();
    }

    // The reload above rebuilt the alone remap table; discard any leftover alone runtime state so a key
    // that was physically held across the reload can't leave a stale pending/combination entry (which a
    // later event would promote, injecting an unmatched original key-down). No-op on the initial load.
    state.ClearAllAloneKeyState();

    // Track the active profile (for auto-switch decisions) and refresh the device->profile map.
    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        activeProfileName = state.currentConfig;
        requestedProfile.clear();
    }
    profileSwapGate.SetAppliedProfile(state.currentConfig);
    LoadDeviceProfiles();

    try
    {
        // Send telemetry about configured key/shortcut to key/shortcut mappings, OS an app specific level.
        Trace::SendKeyAndShortcutRemapLoadedConfiguration(state);
    }
    catch (...)
    {
        try
        {
            Logger::error("Failed to send telemetry for the configured remappings.");
            // Try not to crash the app sending telemetry. Everything inside a try.
            Trace::ErrorSendingKeyAndShortcutRemapLoadedConfiguration();
        }
        catch (...)
        {

        }
    }
}

std::unique_ptr<State> KeyboardManager::LoadConfiguration(MappingConfiguration::LoadOutcome& outcome)
{
    auto configuration = std::make_unique<State>();
    if (configuration->LoadSettings(&outcome))
    {
        return configuration;
    }

    // Same single retry as the in-place load always had: a writer may be mid-write.
    std::this_thread::sleep_for(std::chrono::milliseconds(500));

    auto retry = std::make_unique<State>();
    MappingConfiguration::LoadOutcome retryOutcome = MappingConfiguration::LoadOutcome::Failed;
    retry->LoadSettings(&retryOutcome);

    // Keep the first result if the retry could not read anything (e.g. the first load only skipped a
    // malformed entry and the retry hit a write in progress).
    if (retryOutcome != MappingConfiguration::LoadOutcome::Failed || outcome == MappingConfiguration::LoadOutcome::Failed)
    {
        outcome = retryOutcome;
        configuration = std::move(retry);
    }

    return configuration;
}

void KeyboardManager::OnSettingsChanged()
{
    MappingConfiguration::LoadOutcome outcome = MappingConfiguration::LoadOutcome::Failed;
    std::unique_ptr<State> loaded = LoadConfiguration(outcome);

    LoadDeviceProfiles();

    if (outcome == MappingConfiguration::LoadOutcome::Failed)
    {
        // Transient failure: keep the live tables (and any pending stage). Clear an in-flight
        // auto-switch request so the tracker's dedupe does not block a retry from the same keyboard.
        Logger::error(L"Settings reload failed; keeping the current remaps");
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        requestedProfile.clear();
        return;
    }

    try
    {
        // Send telemetry about configured key/shortcut to key/shortcut mappings, OS an app specific level.
        Trace::SendKeyAndShortcutRemapLoadedConfiguration(*loaded);
    }
    catch (...)
    {
        try
        {
            Logger::error("Failed to send telemetry for the configured remappings.");
            Trace::ErrorSendingKeyAndShortcutRemapLoadedConfiguration();
        }
        catch (...)
        {
        }
    }

    const auto applyInPlace = [this](State& configuration) {
        // Same profile (an editor edit): replace the tables right away, as the in-place reload always
        // did. The hook passes input through while loadingSettings is set. Runtime state other than
        // the "Alone" keys is kept, as before.
        loadingSettings = true;
        state.AdoptConfiguration(configuration, false);
        state.ClearAllAloneKeyState();
        loadingSettings = false;
    };

    const auto onStaged = [this](const std::wstring& stagedProfile) {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        requestedProfile = stagedProfile;
    };

    const auto result = profileSwapGate.Offer(outcome, std::move(loaded), applyInPlace, onStaged);
    if (result == ProfileSwapGate::OfferResult::Staged)
    {
        Logger::trace(L"Profile change to '{}' staged; it is applied at the next key-down with no key held", profileSwapGate.StagedProfile());

        // Without a keyboard hook nothing is remapped, so nothing can be held "under" the live profile:
        // let the hook thread apply the stage now (ReconcileHooks).
        if (!hookHandle)
        {
            PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);
        }

        return;
    }

    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        requestedProfile.clear();
    }

    const bool newHasRemappings = HasRegisteredRemappingsUnchecked();
    // We didn't have any bindings before and we have now
    if (newHasRemappings && !hookHandle)
        PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);

    // All bindings were removed
    if (!newHasRemappings && hookHandle)
        StopLowlevelKeyboardHook();

    // The set of "Alone" (dual-key) remaps may have changed without changing whether ANY binding
    // exists, so reconcile the companion mouse hook. Route an install through the same StartHook
    // message (SetWindowsHookEx must run on the hook thread); a removal is safe inline, mirroring
    // the StopLowlevelKeyboardHook call above.
    const bool needMouseHook = !state.aloneSingleKeyReMap.empty();
    if (needMouseHook && hookHandle && !mouseHookHandle)
        PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);
    else if (!needMouseHook && mouseHookHandle)
        StopLowlevelMouseHook();
}

void KeyboardManager::AdoptStagedConfiguration(std::unique_ptr<State> staged)
{
    state.AdoptConfiguration(*staged, true);

    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        activeProfileName = state.currentConfig;
        if (requestedProfile == activeProfileName)
        {
            requestedProfile.clear();
        }
    }

    // Install/remove the keyboard and mouse hooks for the new tables. Posted, so it runs on this
    // (hook) thread after the current hook callback returns.
    PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);

    Logger::trace(L"Profile '{}' applied", state.currentConfig);

    RetireConfiguration(std::move(staged));
}

void KeyboardManager::RetireConfiguration(std::unique_ptr<State> configuration)
{
    if (!configuration)
    {
        return;
    }

    State* raw = configuration.release();
    if (!TrySubmitThreadpoolCallback(DeleteRetiredConfiguration, raw, nullptr))
    {
        delete raw;
    }
}

std::vector<ProfileSwapGate::KeySnapshot> KeyboardManager::SnapshotKeysDown()
{
    std::vector<ProfileSwapGate::KeySnapshot> keys;

    // From VK_BACK up: 0x01-0x07 are mouse buttons and VK_CANCEL. Skip the generic Shift/Ctrl/Alt:
    // the low-level hook reports the left/right variants, which are checked separately.
    for (int vk = VK_BACK; vk <= 0xFE; ++vk)
    {
        if (vk == VK_SHIFT || vk == VK_CONTROL || vk == VK_MENU)
        {
            continue;
        }

        if (GetAsyncKeyState(vk) & 0x8000)
        {
            const UINT scanCode = MapVirtualKeyW(static_cast<UINT>(vk), MAPVK_VK_TO_VSC_EX);
            keys.push_back({ static_cast<DWORD>(vk), scanCode & 0xFF, (scanCode & 0xFF00) == 0xE000 });
        }
    }

    return keys;
}

void KeyboardManager::ReconcileHooks()
{
    if (!hookHandle && profileSwapGate.HasStage())
    {
        // No keyboard hook means the live profile remaps nothing, so no key is held "under" it: apply
        // the stage now. Keys down right now reached the system unremapped; they are recorded as
        // pass-through orphans so their key-ups also pass through instead of being resolved by the
        // new profile.
        profileSwapGate.ApplyStageWithoutHook(SnapshotKeysDown(), [this](std::unique_ptr<State> staged) {
            AdoptStagedConfiguration(std::move(staged));
        });
    }

    if (HasRegisteredRemappingsUnchecked())
    {
        StartLowlevelKeyboardHook();
    }
    else
    {
        StopLowlevelKeyboardHook();
    }
}

intptr_t KeyboardManager::HandlePhysicalKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    try
    {
        return profileSwapGate.OnPhysicalKeyEvent(
            data,
            [this](LowlevelKeyboardEvent* event) { return HandleKeyboardHookEvent(event); },
            [this](std::unique_ptr<State> staged) { AdoptStagedConfiguration(std::move(staged)); });
    }
    catch (...)
    {
        // Bookkeeping failed (e.g. out of memory). Pass the event on rather than risk handling it twice.
        return 0;
    }
}

LRESULT CALLBACK KeyboardManager::HookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    LowlevelKeyboardEvent event{};
    if (nCode == HC_ACTION)
    {
        event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        event.wParam = wParam;

        // Read before the numpad-origin encoding below changes vkCode.
        const bool physical = !(event.lParam->flags & LLKHF_INJECTED);
        event.lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event.lParam->vkCode, event.lParam->flags & LLKHF_EXTENDED);

        // Physical keys go through the profile-swap gate (held-key tracking, staged profile changes,
        // orphans); injected input, incl. KBM's own remap output, carries LLKHF_INJECTED and is
        // handled by the live tables directly (the gate only notes injected key-ups of held keys).
        intptr_t result = 0;
        if (physical)
        {
            result = keyboardManagerObjectPtr->HandlePhysicalKeyboardHookEvent(&event);
        }
        else
        {
            keyboardManagerObjectPtr->profileSwapGate.OnInjectedKeyEvent(&event);
            result = keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event);
        }

        if (result == 1)
        {
            // Reset Num Lock whenever a NumLock key down event is suppressed since Num Lock key state change occurs before it is intercepted by low level hooks
            if (event.lParam->vkCode == VK_NUMLOCK && (event.wParam == WM_KEYDOWN || event.wParam == WM_SYSKEYDOWN) && event.lParam->dwExtraInfo != KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
            {
                KeyboardEventHandlers::SetNumLockToPreviousState(keyboardManagerObjectPtr->inputHandler);
            }
            return 1;
        }
    }

    return CallNextHookEx(hookHandleCopy, nCode, wParam, lParam);
}

LRESULT CALLBACK KeyboardManager::MouseHookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    if (nCode == HC_ACTION)
    {
        // Only a deliberate button-press or wheel means a held alone key is being used in
        // combination. Mouse MOVE must NOT count, or an incidental move would swallow the tap.
        // Mouse events are never suppressed here — we only promote the held key, then pass through.
        switch (wParam)
        {
        case WM_LBUTTONDOWN:
        case WM_RBUTTONDOWN:
        case WM_MBUTTONDOWN:
        case WM_XBUTTONDOWN:
        case WM_MOUSEWHEEL:
        case WM_MOUSEHWHEEL:
            keyboardManagerObjectPtr->HandleMouseHookEvent();
            break;
        default:
            break;
        }
    }

    return CallNextHookEx(mouseHookHandleCopy, nCode, wParam, lParam);
}

void KeyboardManager::HandleMouseHookEvent() noexcept
{
    if (loadingSettings)
    {
        return;
    }

    // Suspend while the remap key/shortcut editor window is capturing input, mirroring
    // HandleKeyboardHookEvent.
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
    {
        return;
    }

    // Common path: no alone key is held, so a click/scroll is none of our business.
    if (!state.HasPendingAloneKeys())
    {
        return;
    }

    // An alone-mapped key is held and the user clicked/scrolled: promote it to a real modifier so
    // the mouse action is seen in combination (e.g. Ctrl+Click, Ctrl+Wheel). The matching real
    // key-up is injected by the keyboard handler when the alone key is released.
    KeyboardEventHandlers::PromotePendingAloneKeysToCombination(inputHandler, state);
}

void KeyboardManager::StartLowlevelKeyboardHook()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif

    if (!hookHandle)
    {
        hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, HookProc, GetModuleHandle(NULL), NULL);
        hookHandleCopy = hookHandle;
        if (!hookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelKeyboardHook::SetWindowsHookEx");
        }
        else
        {
            // The hook saw none of the events while it was not installed, so its held-key set is stale.
            // Keys down right now were pressed without the hook: their key-ups pass through.
            profileSwapGate.ResetHeldKeys(SnapshotKeysDown());
        }
    }

    // The "Alone" (dual-key) feature also needs a mouse hook so a click/scroll while an alone key is
    // held counts as a combination. Only install it when alone remaps exist; keep it in sync with the
    // keyboard hook (this runs on the hook message-loop thread, where SetWindowsHookEx must be called).
    if (!state.aloneSingleKeyReMap.empty())
    {
        StartLowlevelMouseHook();
    }
    else
    {
        StopLowlevelMouseHook();
    }
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
    }

    StopLowlevelMouseHook();
}

void KeyboardManager::StartLowlevelMouseHook()
{
#if defined(DISABLE_LOWLEVEL_HOOKS_WHEN_DEBUGGED)
    if (IsDebuggerPresent())
    {
        return;
    }
#endif

    if (!mouseHookHandle)
    {
        mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, MouseHookProc, GetModuleHandle(NULL), NULL);
        mouseHookHandleCopy = mouseHookHandle;
        if (!mouseHookHandle)
        {
            DWORD errorCode = GetLastError();
            show_last_error_message(L"SetWindowsHookEx", errorCode, L"PowerToys - Keyboard Manager");
            auto errorMessage = get_last_error_message(errorCode);
            Trace::Error(errorCode, errorMessage.has_value() ? errorMessage.value() : L"", L"StartLowlevelMouseHook::SetWindowsHookEx");
        }
    }
}

void KeyboardManager::StopLowlevelMouseHook()
{
    if (mouseHookHandle)
    {
        UnhookWindowsHookEx(mouseHookHandle);
        mouseHookHandle = nullptr;
    }
}

bool KeyboardManager::HasRegisteredRemappings() const
{
    constexpr int MaxAttempts = 5;

    if (loadingSettings)
    {
        for (int currentAttempt = 0; currentAttempt < MaxAttempts; ++currentAttempt)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(500));
            if (!loadingSettings)
                break;
        }
    }

    // Assume that we have registered remappings to be on the safe side if we couldn't check
    if (loadingSettings)
        return true;

    return HasRegisteredRemappingsUnchecked();
}

bool KeyboardManager::HasRegisteredRemappingsUnchecked() const
{
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.aloneSingleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
}

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data) noexcept
{
    if (loadingSettings)
    {
        return 0;
    }

    // Suspend remapping if remap key/shortcut window is opened
    if (editorIsRunningEvent != nullptr && WaitForSingleObject(editorIsRunningEvent, 0) == WAIT_OBJECT_0)
    {
        return 0;
    }

    // If key has suppress flag, then suppress it
    if (data->lParam->dwExtraInfo == KeyboardManagerConstants::KEYBOARDMANAGER_SUPPRESS_FLAG)
    {
        return 1;
    }

    // Remap a key tapped alone (dual-key). Runs before the regular single-key remap so it can hold
    // the key-down (lazy) and decide tap-vs-combination; if it handles the event, suppress the original.
    intptr_t SingleKeyAloneRemapResult = KeyboardEventHandlers::HandleSingleKeyAloneRemapEvent(inputHandler, data, state);
    if (SingleKeyAloneRemapResult == 1)
    {
        return 1;
    }

    // Remap a key
    intptr_t SingleKeyRemapResult = KeyboardEventHandlers::HandleSingleKeyRemapEvent(inputHandler, data, state);

    // Single key remaps have priority. If a key is remapped, only the remapped version should be visible to the shortcuts and hence the event should be suppressed here.
    if (SingleKeyRemapResult == 1)
    {
        return 1;
    }

    /* This feature has not been enabled (code from proof of concept stage)
        // Remap a key to behave like a modifier instead of a toggle
        intptr_t SingleKeyToggleToModResult = KeyboardEventHandlers::HandleSingleKeyToggleToModEvent(inputHandler, data, keyboardManagerState);
    */

    // Handle an app-specific shortcut remapping
    intptr_t AppSpecificShortcutRemapResult = KeyboardEventHandlers::HandleAppSpecificShortcutRemapEvent(inputHandler, data, state);

    // If an app-specific shortcut is remapped then the os-level shortcut remapping should be suppressed.
    if (AppSpecificShortcutRemapResult == 1)
    {
        return 1;
    }

    intptr_t SingleKeyToTextRemapResult = KeyboardEventHandlers::HandleSingleKeyToTextRemapEvent(inputHandler, data, state);

    if (SingleKeyToTextRemapResult == 1)
    {
        return 1;
    }

    // Handle an os-level shortcut remapping
    return KeyboardEventHandlers::HandleOSLevelShortcutRemapEvent(inputHandler, data, state);
}
