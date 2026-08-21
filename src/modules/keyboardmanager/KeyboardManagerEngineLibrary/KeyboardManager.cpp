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

#include "KeyboardEventHandlers.h"
#include "trace.h"

HHOOK KeyboardManager::hookHandleCopy;
HHOOK KeyboardManager::hookHandle;
KeyboardManager* KeyboardManager::keyboardManagerObjectPtr;

namespace
{
    // A RIDI_DEVICENAME path looks like "\\?\HID#<deviceId>#<instanceId>#<interfaceGuid>". Some
    // virtual keyboards (e.g. Target_KIP) are assigned a fresh <instanceId> over time, which would
    // break an exact-path match. Match on the stable prefix instead: everything up to the instance
    // (the 2nd '#'). This keeps distinct keyboards apart while tolerating instance-id churn.
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

        loadingSettings = true;
        bool loadedSuccessfully = false;
        try
        {
            LoadSettings();
            loadedSuccessfully = true;
        }
        catch (...)
        {
            Logger::error("Failed to load settings");
        }

        loadingSettings = false;

        if (!loadedSuccessfully)
            return;

        const bool newHasRemappings = HasRegisteredRemappingsUnchecked();
        // We didn't have any bindings before and we have now
        if (newHasRemappings && !hookHandle)
            PostThreadMessageW(mainThreadId, StartHookMessageID, 0, 0);

        // All bindings were removed
        if (!newHasRemappings && hookHandle)
            StopLowlevelKeyboardHook();
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
    // Ignore injected input (hDevice == NULL, incl. KBM's own remap output) and key-ups.
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
    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        current = activeProfileName;
    }

    if (target == current)
    {
        // Already on the right profile; reset any pending switch.
        pendingTarget.clear();
        pendingCount = 0;
        requestedProfile.clear();
        return;
    }

    // A switch to this profile was already requested; wait for the reload to take effect.
    if (target == requestedProfile)
    {
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
    requestedProfile = target;
    Logger::trace(L"Auto-switch: keyboard {} -> profile '{}'", keyEvent.devicePath, target);
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

            if (obj.HasKey(L"cycleHotkey"))
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

    try
    {
        const auto path = PTSettingsHelper::get_module_save_folder_location(moduleName) + L"\\settings.json";
        auto parsed = json::from_file(path);
        json::JsonObject root = parsed.has_value() ? parsed.value() : json::JsonObject{};

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

    // Track the active profile (for auto-switch decisions) and refresh the device->profile map.
    {
        std::lock_guard<std::mutex> lock(activeProfileMutex);
        activeProfileName = state.currentConfig;
    }
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

LRESULT CALLBACK KeyboardManager::HookProc(int nCode, const WPARAM wParam, const LPARAM lParam)
{
    LowlevelKeyboardEvent event{};
    if (nCode == HC_ACTION)
    {
        event.lParam = reinterpret_cast<KBDLLHOOKSTRUCT*>(lParam);
        event.wParam = wParam;
        event.lParam->vkCode = Helpers::EncodeKeyNumpadOrigin(event.lParam->vkCode, event.lParam->flags & LLKHF_EXTENDED);

        if (keyboardManagerObjectPtr->HandleKeyboardHookEvent(&event) == 1)
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
    }
}

void KeyboardManager::StopLowlevelKeyboardHook()
{
    if (hookHandle)
    {
        UnhookWindowsHookEx(hookHandle);
        hookHandle = nullptr;
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
    return !(state.appSpecificShortcutReMap.empty() && state.appSpecificShortcutReMapSortedKeys.empty() && state.osLevelShortcutReMap.empty() && state.osLevelShortcutReMapSortedKeys.empty() && state.singleKeyReMap.empty() && state.singleKeyToTextReMap.empty());
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
