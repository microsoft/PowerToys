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
#include <keyboardmanager/common/SettingsFile.h>
#include <keyboardmanager/common/KeyboardEventHandlers.h>
#include <ctime>
#include <filesystem>

#include "KeyboardEventHandlers.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
std::atomic<HHOOK> KeyboardManager::hookHandle{ nullptr };
std::atomic<HHOOK> KeyboardManager::mouseHookHandle{ nullptr };
HHOOK KeyboardManager::mouseHookHandleCopy;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    // A RIDI_DEVICENAME path looks like "\\?\HID#<deviceId>#<instanceId>#<interfaceGuid>". Some
    // virtual keyboards (e.g. Target_KIP) are assigned a fresh <instanceId> over time, which would
    // break an exact-path match. Match on the stable prefix instead: everything up to the instance
    // (the 2nd '#'). This keeps distinct keyboards apart while tolerating instance-id churn.
    //
    // Known, intentional limitation: the dropped <instanceId> is also what tells two
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

    // Create the profile-cycle hotkey object first, not yet started. LoadSettings below hands it the
    // hotkey from deviceProfiles.json, and once the settings listener runs, its thread reads this
    // pointer too, so the pointer must not change after that.
    profileCycleHotkey = std::make_unique<ProfileCycleHotkey>([this] { CycleActiveProfile(); });

    // The same goes for the keyboard tracker (per-keyboard profile switching): LoadSettings tells it
    // whether any keyboard is assigned, and it looks at key events only then.
    rawInputTracker = std::make_unique<RawInputKeyboardTracker>(
        [this](const RawInputKeyboardTracker::KeyEvent& keyEvent) { OnRawKeyEvent(keyEvent); });

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
    rawInputTracker->Start();

    // Start the global profile-cycle hotkey last: its thread registers the hotkey that LoadSettings
    // (or a later reload on the listener thread) handed to the object. The listener is started
    // before the tracker and the hotkey because both ask for a switch by signalling its event.
    profileCycleHotkey->Start();
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

    // Nothing below runs, and nothing is logged, while the feature is off.
    if (!autoSwitchEnabled.load())
    {
        return;
    }

    // A physical keystroke whose keyboard can't be identified (observed on Surface Type Cover right
    // after idle, when its KIP device node re-enumerates). Engine logs go into bug reports, so no key
    // code and no device path is logged anywhere in this function: only how often this happened.
    if (keyEvent.devicePath.empty())
    {
        if (const uint64_t count = unidentifiedKeyboardCount.Add(GetTickCount64()); count != 0)
        {
            Logger::trace(L"Auto-switch: {} key-down(s) came from a keyboard that could not be identified", count);
        }

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
    if (KeyboardManagerSettingsFile::IsSameProfile(target, heading))
    {
        pendingTarget.clear();
        pendingCount = 0;
        return;
    }

    // A switch to this profile failed a moment ago (settings.json could not be saved, or the
    // profile's file is gone). Without a pause, every second keystroke on this keyboard would try
    // again, block this thread for up to half a second and add an error line to the log.
    const uint64_t nowTick = GetTickCount64();
    if (nowTick < failedSwitchRetryTick && KeyboardManagerSettingsFile::IsSameProfile(target, failedSwitchTarget))
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

    Logger::trace(L"Auto-switch: requesting profile '{}'", target);
    if (SwitchActiveProfile(target))
    {
        failedSwitchTarget.clear();
        failedSwitchRetryTick = 0;
    }
    else
    {
        failedSwitchTarget = target;
        failedSwitchRetryTick = nowTick + FailedSwitchRetryDelayMs;
    }
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

        // A file that is not there means the feature was never set up: everything off. A file that
        // is there but cannot be read right now (another program has it open, or it is damaged) says
        // nothing, so the values loaded last stay in effect. Treating it as empty would turn
        // auto-switch off and unregister the cycle hotkey, and neither could ask for a reload again.
        std::optional<json::JsonObject> parsed;
        bool absent = false;
        constexpr int Attempts = 5;
        constexpr DWORD RetryDelayMs = 50;
        for (int attempt = 1;; ++attempt)
        {
            parsed = json::from_file(path);
            if (parsed.has_value())
            {
                break;
            }

            std::error_code ec;
            if (!std::filesystem::exists(path, ec) && !ec)
            {
                absent = true;
                break;
            }

            if (attempt >= Attempts)
            {
                Logger::error(L"deviceProfiles.json could not be read; keeping the keyboard assignments and the cycle hotkey loaded before");
                return;
            }

            Sleep(RetryDelayMs);
        }

        if (!absent)
        {
            const json::JsonObject& obj = parsed.value();
            if (obj.HasKey(L"autoSwitchEnabled"))
            {
                enabled = obj.GetNamedBoolean(L"autoSwitchEnabled", false);
            }

            // "map": null and a null entry assign nothing, as in the editor (DeviceProfileManager), so
            // both sides read a hand-edited file the same way. Any other wrong type is a load failure.
            if (obj.HasKey(L"map") && obj.GetNamedValue(L"map").ValueType() != json::JsonValueType::Null)
            {
                const auto arr = obj.GetNamedArray(L"map");
                for (uint32_t i = 0; i < arr.Size(); ++i)
                {
                    if (arr.GetAt(i).ValueType() == json::JsonValueType::Null)
                    {
                        continue;
                    }

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
        // A member of the wrong type. Applying what was parsed before it would be a half-read file.
        Logger::error(L"Failed to load deviceProfiles.json; keeping the keyboard assignments and the cycle hotkey loaded before");
        return;
    }

    // While auto-switch is off, or no keyboard is assigned, there is nothing to decide per key: the
    // tracker then drops each raw-input message without reading it or resolving its keyboard.
    const bool trackKeys = enabled && !map.empty();

    {
        std::lock_guard<std::mutex> lock(deviceMapMutex);
        deviceProfileMap = std::move(map);
    }

    autoSwitchEnabled.store(enabled);

    if (rawInputTracker)
    {
        rawInputTracker->SetActive(trackKeys);
    }

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

        // A listed profile whose file is gone cannot be activated (SwitchActiveProfile refuses it).
        // Skip it: stopping there would keep the hotkey from ever reaching the profiles after it.
        const std::wstring folder = PTSettingsHelper::get_module_save_folder_location(moduleName);
        const std::wstring next = KeyboardManagerSettingsFile::NextProfileInCycle(profiles, current, [&folder](const std::wstring& profile) {
            if (KeyboardManagerSettingsFile::IsSameProfile(profile, KeyboardManagerConstants::DefaultConfiguration))
            {
                return true;
            }

            std::error_code ec;
            return std::filesystem::exists(folder + L"\\" + profile + L".json", ec);
        });

        if (next.empty())
        {
            return; // nothing to cycle to
        }

        Logger::trace(L"CycleActiveProfile: '{}' -> '{}'", current, next);

        // Audible feedback that the profile changed (no UI surface in the engine): only once the
        // switch is saved. A switch that could not be saved stays silent and is logged.
        if (SwitchActiveProfile(next))
        {
            MessageBeep(MB_OK);
        }
    }
    catch (...)
    {
        Logger::error(L"CycleActiveProfile failed");
    }
}

bool KeyboardManager::SwitchActiveProfile(const std::wstring& profile)
{
    // Tracker thread and hotkey thread can both land here; serialize the read-modify-write.
    std::lock_guard<std::mutex> lock(switchProfileMutex);

    // Refuse to activate a profile whose config file is gone. A tracker or hotkey callback can hold a
    // stale target (e.g. captured from the device map just before the profile was deleted), and
    // writing that as activeConfiguration would leave a deleted profile active — its file missing, the
    // engine unable to load it, its old mappings stranded. Validate at commit time and drop the stale
    // request instead. "default" is exempt: it is the protected fallback (never deletable) and is
    // created on demand by the editor, so it must always be switchable to.
    if (!KeyboardManagerSettingsFile::IsSameProfile(profile, KeyboardManagerConstants::DefaultConfiguration))
    {
        const auto configPath = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\" + profile + L".json";
        std::error_code ec;
        if (!std::filesystem::exists(configPath, ec))
        {
            Logger::error(L"Switch: refusing to activate profile '{}' — its config file is missing", profile);
            ResetFailedProfileRequest(profile);
            return false;
        }
    }

    // Save the switch first: settings.json is replaced as a whole (temporary file + rename), so a
    // failed write leaves the previous file intact. A settings.json that cannot be read is left
    // untouched too: writing it back as an empty object would erase every other Keyboard Manager
    // property (e.g. keyboardConfigurations).
    const auto path = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\settings.json";
    if (!KeyboardManagerSettingsFile::WriteActiveConfiguration(path, profile))
    {
        Logger::error(L"Switch: could not save the active profile to settings.json; it is unchanged");

        // Undo the request so the next keystroke on this keyboard can retry: OnRawKeyEvent sets
        // requestedProfile before calling us, and otherwise its target == requested guard would
        // filter out all further input from that keyboard and wedge the switch. A pending stage is
        // still where the engine is heading, so the request falls back to it.
        ResetFailedProfileRequest(profile);
        return false;
    }

    // Only now that the switch is saved, ask for the reload.
    // Reuse the existing reload path: the engine's own settings watcher will apply the new profile.
    HANDLE hEvent = CreateEvent(nullptr, false, false, KeyboardManagerConstants::SettingsEventName.c_str());
    if (hEvent)
    {
        SetEvent(hEvent);
        CloseHandle(hEvent);
    }
    else
    {
        // The switch is saved but the reload was not asked for, so it has not happened: report it as
        // failed (no tone), and undo the request so that it can be tried again.
        Logger::error(L"Switch: failed to signal the settings event");
        ResetFailedProfileRequest(profile);
        return false;
    }

    return true;
}

void KeyboardManager::ResetFailedProfileRequest(const std::wstring& failedProfile)
{
    // Gate lock first, then activeProfileMutex (the order AdoptStagedConfiguration and OnSettingsChanged
    // use), so the staged profile read here cannot be applied or replaced before requestedProfile is set.
    profileSwapGate.ReportPendingProfile([this, &failedProfile](const std::wstring& pending) {
        std::lock_guard<std::mutex> lock(activeProfileMutex);

        // Only the failed request itself: a request for another profile (e.g. a successful switch whose
        // reload is still in flight, while this failure came from the cycle hotkey) is left alone.
        if (KeyboardManagerSettingsFile::IsSameProfile(requestedProfile, failedProfile))
        {
            requestedProfile = pending;
        }
    });
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

    // Runs under the gate lock (gate lock, then activeProfileMutex: the same order as the hook thread's
    // AdoptStagedConfiguration). The requested profile becomes the staged profile, or empty when
    // nothing is staged.
    const auto onRequested = [this](const std::wstring& requested) {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        requestedProfile = requested;
    };

    if (outcome == MappingConfiguration::LoadOutcome::Failed)
    {
        // Transient failure: keep the live tables (and any pending stage). The gate resets the
        // in-flight auto-switch request to the pending stage, or clears it when nothing is staged, so
        // the tracker's dedupe does not block a retry from the same keyboard.
        Logger::error(L"Settings reload failed; keeping the current remaps");
        profileSwapGate.Offer(outcome, nullptr, [](State&) {}, onRequested);
        return;
    }

    // Telemetry about the configured remappings (one event per remapping) is sent when the profile
    // that is applied was loaded again, i.e. after an edit, as it was before profiles existed. A
    // reload that only switches to another profile sends none: with auto-switch that happens every
    // time the user moves to another keyboard, and would repeat the same events many times a day.
    if (KeyboardManagerSettingsFile::IsSameProfile(loaded->currentConfig, profileSwapGate.AppliedProfile()))
    {
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
    }

    const auto applyInPlace = [this](State& configuration) {
        // Same profile: replace the tables right away, as the in-place reload always did (see
        // State::ApplyReloadInPlace). The hook passes input through while loadingSettings is set.
        state.ApplyReloadInPlace(configuration, loadingSettings);
    };

    const auto result = profileSwapGate.Offer(outcome, std::move(loaded), applyInPlace, onRequested);
    if (result == ProfileSwapGate::OfferResult::Staged)
    {
        Logger::trace(L"Profile change to '{}' staged; it is applied at the next key-down with no key held", profileSwapGate.StagedProfile());

        // Without a keyboard hook nothing is remapped, so nothing can be held "under" the live profile:
        // the hook thread applies the stage now (ReconcileHooks). Always post, without checking
        // hookHandle here: the hook thread may be removing the hook right now, after its own check for a
        // stage, and would then never see a key-down to apply this one. ReconcileHooks does nothing
        // for the stage while the hook is installed, so an extra message is harmless.
        PostReconcileHooks();
        return;
    }

    // Applied in place: let the hook thread install or remove the keyboard and mouse hooks for the new
    // tables (ReconcileHooks). Always post, and never install or remove a hook from this thread: a
    // decision taken here from the current hookHandle could be overtaken by the hook thread's own
    // reconciliation, which may be about to remove the hook for the tables this reload replaced (the
    // first mapping added to an empty profile). An extra message is harmless.
    PostReconcileHooks();
}

void KeyboardManager::PostReconcileHooks() const
{
    if (!PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0))
    {
        Logger::error(L"Could not ask the hook thread to update the hooks (error {}); they follow the remaps again at the next reload", GetLastError());
    }
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
    PostReconcileHooks();

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
    // The hook thread is the only place that installs or removes the hooks: OnSettingsChanged posts
    // StartHookMessageID after every accepted reload, so the last message always reconciles against
    // the newest tables.
    profileSwapGate.ReconcileHooks(
        {
            [] { return hookHandle.load() != nullptr; },
            [this] { return HasRegisteredRemappingsUnchecked(); },
            [this] { return !state.aloneSingleKeyReMap.empty(); },
            [this](const bool mouseHook) { InstallHooks(mouseHook); },
            [this] { StopLowlevelKeyboardHook(); },
            [] { return SnapshotKeysDown(); },
        },
        [this](std::unique_ptr<State> staged) { AdoptStagedConfiguration(std::move(staged)); });
}

LRESULT CALLBACK KeyboardManager::HookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    LowlevelKeyboardEvent event{};
    if (nCode == HC_ACTION)
    {
        event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        event.wParam = wParam;

        // The profile-swap gate classifies the event and encodes the numpad origin into vkCode.
        // Physical input and input injected by other programs go through its held-key tracking
        // (staged profile changes, orphans); KBM's own output is handled by the live tables directly
        // (the gate only notes injected key-ups of held keys). The unit tests drive the same function.
        const intptr_t result = keyboardManagerObjectPtr->profileSwapGate.OnHookEvent(
            &event,
            [](LowlevelKeyboardEvent* data, const bool shortcutsAllowed) { return keyboardManagerObjectPtr->HandleKeyboardHookEvent(data, shortcutsAllowed); },
            [](std::unique_ptr<State> staged) { keyboardManagerObjectPtr->AdoptStagedConfiguration(std::move(staged)); });

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
    // The "Alone" (dual-key) feature also needs a mouse hook so a click/scroll while an alone key is
    // held counts as a combination. Only install it when alone remaps exist.
    InstallHooks(!state.aloneSingleKeyReMap.empty());
}

void KeyboardManager::InstallHooks(const bool mouseHook)
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

    // Keep the mouse hook in sync with the keyboard hook (this runs on the hook message-loop thread,
    // where SetWindowsHookEx must be called).
    if (mouseHook)
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
    // Called from the hook thread (ReconcileHooks) and at shutdown: take the handle exactly once.
    if (const HHOOK hook = hookHandle.exchange(nullptr))
    {
        UnhookWindowsHookEx(hook);
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
    if (const HHOOK hook = mouseHookHandle.exchange(nullptr))
    {
        UnhookWindowsHookEx(hook);
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

intptr_t KeyboardManager::HandleKeyboardHookEvent(LowlevelKeyboardEvent* data, const bool shortcutsAllowed) noexcept
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

    return KeyboardEventHandlers::HandleRemapEvent(inputHandler, data, state, shortcutsAllowed);
}
