#include "pch.h"
#include "KeyboardManagerState.h"
#include "Shortcut.h"
#include "RemapShortcut.h"
#include <common/SettingsAPI/settings_helpers.h>
#include "KeyDelay.h"
#include "Helpers.h"

// Constructor
KeyboardManagerState::KeyboardManagerState() :
    uiState(KeyboardManagerUIState::Deactivated), currentUIWindow(nullptr), currentShortcutUI1(nullptr), currentShortcutUI2(nullptr), currentSingleKeyUI(nullptr), detectedRemapKey(NULL), remappingsEnabled(true)
{
    configFile_mutex = CreateMutex(
        NULL, // default security descriptor
        FALSE, // mutex not owned
        KeyboardManagerConstants::ConfigFileMutexName.c_str());
}

// Destructor
KeyboardManagerState::~KeyboardManagerState()
{
    if (configFile_mutex)
    {
        CloseHandle(configFile_mutex);
    }
}

// Function to check the if the UI state matches the argument state. For states with detect windows it also checks if the window is in focus.
bool KeyboardManagerState::CheckUIState(KeyboardManagerUIState state)
{
    std::lock_guard<std::mutex> lock(uiState_mutex);
    if (uiState == state)
    {
        std::unique_lock<std::mutex> lock(currentUIWindow_mutex);
        if (uiState == KeyboardManagerUIState::Deactivated || uiState == KeyboardManagerUIState::EditKeyboardWindowActivated || uiState == KeyboardManagerUIState::EditShortcutsWindowActivated)
        {
            return true;
        }
        // If the UI state is a detect window then we also have to ensure that the UI window is in focus.
        // GetForegroundWindow can be used here since we only need to check the main parent window and not the sub windows within the content dialog. Using GUIThreadInfo will give more specific sub-windows within the XAML window which is not needed.
        else if (currentUIWindow == GetForegroundWindow())
        {
            return true;
        }
    }
    // If we are checking for EditKeyboardWindowActivated then it's possible the state could be DetectSingleKeyRemapWindowActivated but not in focus
    else if (state == KeyboardManagerUIState::EditKeyboardWindowActivated && uiState == KeyboardManagerUIState::DetectSingleKeyRemapWindowActivated)
    {
        return true;
    }
    // If we are checking for EditShortcutsWindowActivated then it's possible the state could be DetectShortcutWindowActivated but not in focus
    else if (state == KeyboardManagerUIState::EditShortcutsWindowActivated && uiState == KeyboardManagerUIState::DetectShortcutWindowActivated)
    {
        return true;
    }

    return false;
}

// Function to set the window handle of the current UI window that is activated
void KeyboardManagerState::SetCurrentUIWindow(HWND windowHandle)
{
    std::lock_guard<std::mutex> lock(currentUIWindow_mutex);
    currentUIWindow = windowHandle;
}

// Function to set the UI state. When a window is activated, the handle to the window can be passed in the windowHandle argument.
void KeyboardManagerState::SetUIState(KeyboardManagerUIState state, HWND windowHandle)
{
    std::lock_guard<std::mutex> lock(uiState_mutex);
    uiState = state;
    SetCurrentUIWindow(windowHandle);
}

// Function to reset the UI state members
void KeyboardManagerState::ResetUIState()
{
    SetUIState(KeyboardManagerUIState::Deactivated);

    // Reset the shortcut UI stored variables
    std::unique_lock<std::mutex> currentShortcutUI_lock(currentShortcutUI_mutex);
    currentShortcutUI1 = nullptr;
    currentShortcutUI2 = nullptr;
    currentShortcutUI_lock.unlock();

    std::unique_lock<std::mutex> detectedShortcut_lock(detectedShortcut_mutex);
    detectedShortcut.Reset();
    detectedShortcut_lock.unlock();

    std::unique_lock<std::mutex> currentShortcut_lock(currentShortcut_mutex);
    currentShortcut.Reset();
    currentShortcut_lock.unlock();

    // Reset all the single key remap UI stored variables.
    std::unique_lock<std::mutex> currentSingleKeyUI_lock(currentSingleKeyUI_mutex);
    currentSingleKeyUI = nullptr;
    currentSingleKeyUI_lock.unlock();

    std::unique_lock<std::mutex> detectedRemapKey_lock(detectedRemapKey_mutex);
    detectedRemapKey = NULL;
    detectedRemapKey_lock.unlock();
}

// Function to clear the OS Level shortcut remapping table
void KeyboardManagerState::ClearOSLevelShortcuts()
{
    osLevelShortcutReMap.clear();
    osLevelShortcutReMapSortedKeys.clear();
}

// Function to clear the Keys remapping table.
void KeyboardManagerState::ClearSingleKeyRemaps()
{
    singleKeyReMap.clear();
}

// Function to clear the App specific shortcut remapping table
void KeyboardManagerState::ClearAppSpecificShortcuts()
{
    appSpecificShortcutReMap.clear();
    appSpecificShortcutReMapSortedKeys.clear();
}

// Function to add a new OS level shortcut remapping
bool KeyboardManagerState::AddOSLevelShortcut(const Shortcut& originalSC, const KeyShortcutUnion& newSC)
{
    // Check if the shortcut is already remapped
    auto it = osLevelShortcutReMap.find(originalSC);
    if (it != osLevelShortcutReMap.end())
    {
        return false;
    }

    osLevelShortcutReMap[originalSC] = RemapShortcut(newSC);
    osLevelShortcutReMapSortedKeys.push_back(originalSC);
    KeyboardManagerHelper::SortShortcutVectorBasedOnSize(osLevelShortcutReMapSortedKeys);

    return true;
}

// Function to add a new single key to key/shortcut remapping
bool KeyboardManagerState::AddSingleKeyRemap(const DWORD& originalKey, const KeyShortcutUnion& newRemapKey)
{
    // Check if the key is already remapped
    auto it = singleKeyReMap.find(originalKey);
    if (it != singleKeyReMap.end())
    {
        return false;
    }

    singleKeyReMap[originalKey] = newRemapKey;
    return true;
}

// Function to add a new App specific shortcut remapping
bool KeyboardManagerState::AddAppSpecificShortcut(const std::wstring& app, const Shortcut& originalSC, const KeyShortcutUnion& newSC)
{
    // Convert app name to lower case
    std::wstring process_name;
    process_name.resize(app.length());
    std::transform(app.begin(), app.end(), process_name.begin(), towlower);

    // Check if there are any app specific shortcuts for this app
    auto appIt = appSpecificShortcutReMap.find(process_name);
    if (appIt != appSpecificShortcutReMap.end())
    {
        // Check if the shortcut is already remapped
        auto shortcutIt = appSpecificShortcutReMap[process_name].find(originalSC);
        if (shortcutIt != appSpecificShortcutReMap[process_name].end())
        {
            return false;
        }
    }
    else
    {
        appSpecificShortcutReMapSortedKeys[process_name] = std::vector<Shortcut>();
    }

    appSpecificShortcutReMap[process_name][originalSC] = RemapShortcut(newSC);
    appSpecificShortcutReMapSortedKeys[process_name].push_back(originalSC);
    KeyboardManagerHelper::SortShortcutVectorBasedOnSize(appSpecificShortcutReMapSortedKeys[process_name]);
    return true;
}

// Function to get the iterator of a single key remap given the source key. Returns nullopt if it isn't remapped
std::optional<SingleKeyRemapTable::iterator> KeyboardManagerState::GetSingleKeyRemap(const DWORD& originalKey)
{
    auto it = singleKeyReMap.find(originalKey);
    if (it != singleKeyReMap.end())
    {
        return it;
    }

    return std::nullopt;
}

bool KeyboardManagerState::CheckShortcutRemapInvoked(const std::optional<std::wstring>& appName)
{
    // Assumes appName exists in the app-specific remap table
    ShortcutRemapTable& currentRemapTable = appName ? appSpecificShortcutReMap[*appName] : osLevelShortcutReMap;
    for (auto& it : currentRemapTable)
    {
        if (it.second.isShortcutInvoked)
        {
            return true;
        }
    }

    return false;
}

std::vector<Shortcut>& KeyboardManagerState::GetSortedShortcutRemapVector(const std::optional<std::wstring>& appName)
{
    // Assumes appName exists in the app-specific remap table
    return appName ? appSpecificShortcutReMapSortedKeys[*appName] : osLevelShortcutReMapSortedKeys;
}

// Function to get the source and target of a shortcut remap given the source shortcut. Returns nullopt if it isn't remapped
ShortcutRemapTable& KeyboardManagerState::GetShortcutRemapTable(const std::optional<std::wstring>& appName)
{
    if (appName)
    {
        auto itTable = appSpecificShortcutReMap.find(*appName);
        if (itTable != appSpecificShortcutReMap.end())
        {
            return itTable->second;
        }
    }

    return osLevelShortcutReMap;
}

// Function to set the textblock of the detect shortcut UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectShortcutUI(const StackPanel& textBlock1, const StackPanel& textBlock2)
{
    std::lock_guard<std::mutex> lock(currentShortcutUI_mutex);
    currentShortcutUI1 = textBlock1.as<winrt::Windows::Foundation::IInspectable>();
    currentShortcutUI2 = textBlock2.as<winrt::Windows::Foundation::IInspectable>();
}

// Function to set the textblock of the detect remap key UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectSingleKeyRemapUI(const StackPanel& textBlock)
{
    std::lock_guard<std::mutex> lock(currentSingleKeyUI_mutex);
    currentSingleKeyUI = textBlock.as<winrt::Windows::Foundation::IInspectable>();
}

void KeyboardManagerState::AddKeyToLayout(const StackPanel& panel, const hstring& key)
{
    // Textblock to display the detected key
    TextBlock remapKey;
    Border border;

    border.Padding({ 20, 10, 20, 10 });
    border.Margin({ 0, 0, 10, 0 });
    // Use the base low brush to be consistent with the theme
    border.Background(Windows::UI::Xaml::Application::Current().Resources().Lookup(box_value(L"SystemControlBackgroundBaseLowBrush")).as<Windows::UI::Xaml::Media::SolidColorBrush>());
    remapKey.FontSize(20);
    border.HorizontalAlignment(HorizontalAlignment::Left);
    border.Child(remapKey);

    remapKey.Text(key);
    panel.Children().Append(border);
}

// Function to update the detect shortcut UI based on the entered keys
void KeyboardManagerState::UpdateDetectShortcutUI()
{
    std::lock_guard<std::mutex> currentShortcutUI_lock(currentShortcutUI_mutex);
    if (currentShortcutUI1 == nullptr)
    {
        return;
    }

    std::unique_lock<std::mutex> detectedShortcut_lock(detectedShortcut_mutex);
    std::unique_lock<std::mutex> currentShortcut_lock(currentShortcut_mutex);
    // Save the latest displayed shortcut
    currentShortcut = detectedShortcut;
    auto detectedShortcutCopy = detectedShortcut;
    currentShortcut_lock.unlock();
    detectedShortcut_lock.unlock();
    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentShortcutUI1.as<StackPanel>().Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this, detectedShortcutCopy]() {
        std::vector<hstring> shortcut = detectedShortcutCopy.GetKeyVector(keyboardMap);
        currentShortcutUI1.as<StackPanel>().Children().Clear();
        currentShortcutUI2.as<StackPanel>().Children().Clear();

        // The second row should be hidden if there are 3 keys or lesser to avoid an extra margin
        if (shortcut.size() > 3)
        {
            currentShortcutUI2.as<StackPanel>().Visibility(Visibility::Visible);
        }
        else
        {
            currentShortcutUI2.as<StackPanel>().Visibility(Visibility::Collapsed);
        }

        for (int i = 0; i < shortcut.size(); i++)
        {
            if (i < 3)
            {
                AddKeyToLayout(currentShortcutUI1.as<StackPanel>(), shortcut[i]);
            }
            else
            {
                AddKeyToLayout(currentShortcutUI2.as<StackPanel>(), shortcut[i]);
            }
        }
        currentShortcutUI1.as<StackPanel>().UpdateLayout();
        currentShortcutUI2.as<StackPanel>().UpdateLayout();
    });
}

// Function to update the detect remap key UI based on the entered key.
void KeyboardManagerState::UpdateDetectSingleKeyRemapUI()
{
    std::lock_guard<std::mutex> currentSingleKeyUI_lock(currentSingleKeyUI_mutex);
    if (currentSingleKeyUI == nullptr)
    {
        return;
    }
    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentSingleKeyUI.as<StackPanel>().Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [this]() {
        currentSingleKeyUI.as<StackPanel>().Children().Clear();
        hstring key = winrt::to_hstring(keyboardMap.GetKeyName(detectedRemapKey).c_str());
        AddKeyToLayout(currentSingleKeyUI.as<StackPanel>(), key);
        currentSingleKeyUI.as<StackPanel>().UpdateLayout();
    });
}

// Function to return the currently detected shortcut which is displayed on the UI
Shortcut KeyboardManagerState::GetDetectedShortcut()
{
    std::lock_guard<std::mutex> lock(currentShortcut_mutex);
    return currentShortcut;
}

// Function to return the currently detected remap key which is displayed on the UI
DWORD KeyboardManagerState::GetDetectedSingleRemapKey()
{
    std::lock_guard<std::mutex> lock(detectedRemapKey_mutex);
    return detectedRemapKey;
}

void KeyboardManagerState::SelectDetectedRemapKey(DWORD key)
{
    std::lock_guard<std::mutex> guard(detectedRemapKey_mutex);
    detectedRemapKey = key;
    UpdateDetectSingleKeyRemapUI();
    return;
}

void KeyboardManagerState::SelectDetectedShortcut(DWORD key)
{
    // Set the new key and store if a change occurred
    std::unique_lock<std::mutex> lock(detectedShortcut_mutex);
    bool updateUI = detectedShortcut.SetKey(key);
    lock.unlock();

    if (updateUI)
    {
        // Update the UI. This function is called here because it should store the set of keys pressed till the last key which was pressed down.
        UpdateDetectShortcutUI();
    }
    return;
}

void KeyboardManagerState::ResetDetectedShortcutKey(DWORD key)
{
    std::lock_guard<std::mutex> lock(detectedShortcut_mutex);
    detectedShortcut.ResetKey(key);
}

// Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
KeyboardManagerHelper::KeyboardHookDecision KeyboardManagerState::DetectSingleRemapKeyUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect key UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectSingleKeyRemapWindowActivated))
    {
        if (HandleKeyDelayEvent(data))
        {
            return KeyboardManagerHelper::KeyboardHookDecision::Suppress;
        }
        // detect the key if it is pressed down
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            SelectDetectedRemapKey(data->lParam->vkCode);
        }

        // Suppress the keyboard event
        return KeyboardManagerHelper::KeyboardHookDecision::Suppress;
    }

    // If the settings window is up, remappings should not be applied, but we should not suppress events in the hook
    else if (CheckUIState(KeyboardManagerUIState::EditKeyboardWindowActivated))
    {
        return KeyboardManagerHelper::KeyboardHookDecision::SkipHook;
    }

    return KeyboardManagerHelper::KeyboardHookDecision::ContinueExec;
}

// Function which can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
KeyboardManagerHelper::KeyboardHookDecision KeyboardManagerState::DetectShortcutUIBackend(LowlevelKeyboardEvent* data, bool isRemapKey)
{
    // Check if the detect shortcut UI window has been activated
    if ((!isRemapKey && CheckUIState(KeyboardManagerUIState::DetectShortcutWindowActivated)) || (isRemapKey && CheckUIState(KeyboardManagerUIState::DetectShortcutWindowInEditKeyboardWindowActivated)))
    {
        if (HandleKeyDelayEvent(data))
        {
            return KeyboardManagerHelper::KeyboardHookDecision::Suppress;
        }

        // Add the key if it is pressed down
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            SelectDetectedShortcut(data->lParam->vkCode);
        }
        // Remove the key if it has been released
        else if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
        {
            ResetDetectedShortcutKey(data->lParam->vkCode);
        }

        // Suppress the keyboard event
        return KeyboardManagerHelper::KeyboardHookDecision::Suppress;
    }

    // If the detect shortcut UI window is not activated, then clear the shortcut buffer if it isn't empty
    else if (!CheckUIState(KeyboardManagerUIState::DetectShortcutWindowActivated) && !CheckUIState(KeyboardManagerUIState::DetectShortcutWindowInEditKeyboardWindowActivated))
    {
        std::lock_guard<std::mutex> lock(detectedShortcut_mutex);
        if (!detectedShortcut.IsEmpty())
        {
            detectedShortcut.Reset();
        }
    }

    // If the settings window is up, shortcut remappings should not be applied, but we should not suppress events in the hook
    if (!isRemapKey && (CheckUIState(KeyboardManagerUIState::EditShortcutsWindowActivated)) || (isRemapKey && uiState == KeyboardManagerUIState::DetectShortcutWindowInEditKeyboardWindowActivated))
    {
        return KeyboardManagerHelper::KeyboardHookDecision::SkipHook;
    }

    return KeyboardManagerHelper::KeyboardHookDecision::ContinueExec;
}

void KeyboardManagerState::RegisterKeyDelay(
    DWORD key,
    std::function<void(DWORD)> onShortPress,
    std::function<void(DWORD)> onLongPressDetected,
    std::function<void(DWORD)> onLongPressReleased)
{
    std::lock_guard l(keyDelays_mutex);

    if (keyDelays.find(key) != keyDelays.end())
    {
        throw std::invalid_argument("This key was already registered.");
    }

    keyDelays[key] = std::make_unique<KeyDelay>(key, onShortPress, onLongPressDetected, onLongPressReleased);
}

void KeyboardManagerState::UnregisterKeyDelay(DWORD key)
{
    std::lock_guard l(keyDelays_mutex);

    auto deleted = keyDelays.erase(key);
    if (deleted == 0)
    {
        throw std::invalid_argument("The key was not previously registered.");
    }
}

// Function to clear all the registered key delays
void KeyboardManagerState::ClearRegisteredKeyDelays()
{
    std::lock_guard l(keyDelays_mutex);
    keyDelays.clear();
}

bool KeyboardManagerState::HandleKeyDelayEvent(LowlevelKeyboardEvent* ev)
{
    if (currentUIWindow != GetForegroundWindow())
    {
        return false;
    }

    std::lock_guard l(keyDelays_mutex);

    if (keyDelays.find(ev->lParam->vkCode) == keyDelays.end())
    {
        return false;
    }

    keyDelays[ev->lParam->vkCode]->KeyEvent(ev);
    return true;
}

// Save the updated configuration.
bool KeyboardManagerState::SaveConfigToFile()
{
    bool result = true;
    json::JsonObject configJson;
    json::JsonObject remapShortcuts;
    json::JsonObject remapKeys;
    json::JsonArray inProcessRemapKeysArray;
    json::JsonArray appSpecificRemapShortcutsArray;
    json::JsonArray globalRemapShortcutsArray;
    for (const auto& it : singleKeyReMap)
    {
        json::JsonObject keys;
        keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(winrt::to_hstring((unsigned int)it.first)));

        // For key to key remapping
        if (it.second.index() == 0)
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(it.second))));
        }

        // For key to shortcut remapping
        else
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::get<Shortcut>(it.second).ToHstringVK()));
        }

        inProcessRemapKeysArray.Append(keys);
    }

    for (const auto& it : osLevelShortcutReMap)
    {
        json::JsonObject keys;
        keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(it.first.ToHstringVK()));

        // For shortcut to key remapping
        if (it.second.targetShortcut.index() == 0)
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(it.second.targetShortcut))));
        }

        // For shortcut to shortcut remapping
        else
        {
            keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::get<Shortcut>(it.second.targetShortcut).ToHstringVK()));
        }

        globalRemapShortcutsArray.Append(keys);
    }

    for (const auto& itApp : appSpecificShortcutReMap)
    {
        // Iterate over apps
        for (const auto& itKeys : itApp.second)
        {
            json::JsonObject keys;
            keys.SetNamedValue(KeyboardManagerConstants::OriginalKeysSettingName, json::value(itKeys.first.ToHstringVK()));

            // For shortcut to key remapping
            if (itKeys.second.targetShortcut.index() == 0)
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(winrt::to_hstring((unsigned int)std::get<DWORD>(itKeys.second.targetShortcut))));
            }

            // For shortcut to shortcut remapping
            else
            {
                keys.SetNamedValue(KeyboardManagerConstants::NewRemapKeysSettingName, json::value(std::get<Shortcut>(itKeys.second.targetShortcut).ToHstringVK()));
            }

            keys.SetNamedValue(KeyboardManagerConstants::TargetAppSettingName, json::value(itApp.first));

            appSpecificRemapShortcutsArray.Append(keys);
        }
    }

    remapShortcuts.SetNamedValue(KeyboardManagerConstants::GlobalRemapShortcutsSettingName, globalRemapShortcutsArray);
    remapShortcuts.SetNamedValue(KeyboardManagerConstants::AppSpecificRemapShortcutsSettingName, appSpecificRemapShortcutsArray);
    remapKeys.SetNamedValue(KeyboardManagerConstants::InProcessRemapKeysSettingName, inProcessRemapKeysArray);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapKeysSettingName, remapKeys);
    configJson.SetNamedValue(KeyboardManagerConstants::RemapShortcutsSettingName, remapShortcuts);

    // Set timeout of 1sec to wait for file to get free.
    DWORD timeout = 1000;
    auto dwWaitResult = WaitForSingleObject(
        configFile_mutex,
        timeout);
    if (dwWaitResult == WAIT_OBJECT_0)
    {
        try
        {
            json::to_file((PTSettingsHelper::get_module_save_folder_location(KeyboardManagerConstants::ModuleName) + L"\\" + GetCurrentConfigName() + L".json"), configJson);
        }
        catch (...)
        {
            result = false;
        }

        // Make sure to release the Mutex.
        ReleaseMutex(configFile_mutex);
    }
    else
    {
        result = false;
    }

    return result;
}

void KeyboardManagerState::SetCurrentConfigName(const std::wstring& configName)
{
    std::lock_guard<std::mutex> lock(currentConfig_mutex);
    currentConfig = configName;
}

std::wstring KeyboardManagerState::GetCurrentConfigName()
{
    std::lock_guard<std::mutex> lock(currentConfig_mutex);
    return currentConfig;
}

// Sets the activated target application in app-specific shortcut
void KeyboardManagerState::SetActivatedApp(const std::wstring& appName)
{
    activatedAppSpecificShortcutTarget = appName;
}

// Gets the activated target application in app-specific shortcut
std::wstring KeyboardManagerState::GetActivatedApp()
{
    return activatedAppSpecificShortcutTarget;
}

bool KeyboardManagerState::AreRemappingsEnabled()
{
    return remappingsEnabled;
}

void KeyboardManagerState::RemappingsDisabledWrapper(std::function<void()> method)
{
    // Disable keyboard remappings
    remappingsEnabled = false;

    // Run the method which requires the remappings to be disabled
    method();

    // Re-enable the keyboard remappings
    remappingsEnabled = true;
}
