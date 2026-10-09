#include "pch.h"
#include "KeyboardManagerState.h"

#include <keyboardmanager/common/Helpers.h>

#include "EditorHelpers.h"
#include "KeyDelay.h"

using namespace KBMEditor;

// Constructor
KeyboardManagerState::KeyboardManagerState() :
    uiState(KeyboardManagerUIState::Deactivated), currentUIWindow(nullptr), currentShortcutUI1(nullptr), currentShortcutUI2(nullptr), currentSingleKeyUI(nullptr), detectedRemapKey(NULL)
{
}

// Destructor
KeyboardManagerState::~KeyboardManagerState()
{
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

TextBlock KeyboardManagerState::AddKeyToLayout(const StackPanel& panel, const hstring& key)
{
    // Textblock to display the detected key
    TextBlock remapKey;
    Border border;

    border.Padding({ 10, 5, 10, 5 });
    border.Margin({ 0, 0, 10, 0 });

    // Based on settings-ui\Settings.UI\SettingsXAML\Controls\KeyVisual\KeyVisual.xaml
    border.Background(Application::Current().Resources().Lookup(box_value(L"ButtonBackground")).as<Media::Brush>());
    border.BorderBrush(Application::Current().Resources().Lookup(box_value(L"ButtonBorderBrush")).as<Media::Brush>());
    border.BorderThickness(unbox_value<Thickness>(Application::Current().Resources().Lookup(box_value(L"ButtonBorderThemeThickness"))));
    border.CornerRadius(unbox_value<CornerRadius>(Application::Current().Resources().Lookup(box_value(L"ControlCornerRadius"))));
    remapKey.Foreground(Application::Current().Resources().Lookup(box_value(L"ButtonForeground")).as<Media::Brush>());
    remapKey.FontWeight(Text::FontWeights::SemiBold());

    remapKey.FontSize(14);

    border.HorizontalAlignment(HorizontalAlignment::Left);
    border.Child(remapKey);

    remapKey.Text(key);
    panel.Children().Append(border);

    return remapKey;
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
        std::vector<hstring> shortcut = EditorHelpers::GetKeyVector(detectedShortcutCopy, keyboardMap);
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

        auto lastStackPanel = currentShortcutUI2.as<StackPanel>();
        for (int i = 0; i < shortcut.size(); i++)
        {
            if (i < 3)
            {
                AddKeyToLayout(currentShortcutUI1.as<StackPanel>(), shortcut[i]);
                lastStackPanel = currentShortcutUI1.as<StackPanel>();
            }
            else
            {
                AddKeyToLayout(currentShortcutUI2.as<StackPanel>(), shortcut[i]);
                lastStackPanel = currentShortcutUI2.as<StackPanel>();
            }
        }

        if (!AllowChord)
        {
            detectedShortcut.secondKey = NULL;
        }

        // add a TextBlock, to show what shortcut in text, e.g.: "CTRL+j, k" OR "CTRL+j, CTRL+k".
        if (detectedShortcut.HasChord())
        {
            TextBlock txtComma;
            txtComma.Text(L",");
            txtComma.FontSize(20);
            txtComma.Padding({ 0, 0, 10, 0 });
            txtComma.VerticalAlignment(VerticalAlignment::Bottom);
            txtComma.TextAlignment(TextAlignment::Left);
            lastStackPanel.Children().Append(txtComma);
            AddKeyToLayout(lastStackPanel, EditorHelpers::GetKeyVector(Shortcut(detectedShortcutCopy.secondKey), keyboardMap)[0]);
        }

        try
        {
            // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
            currentShortcutUI1.as<StackPanel>().UpdateLayout();
        }
        catch (...)
        {
        }
        try
        {
            // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
            currentShortcutUI2.as<StackPanel>().UpdateLayout();
        }
        catch (...)
        {
        }
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
        try
        {
            // If a layout update has been triggered by other methods (e.g.: adapting to zoom level), this may throw an exception.
            currentSingleKeyUI.as<StackPanel>().UpdateLayout();
        }
        catch (...)
        {
        }
    });
}

// Function to return the currently detected shortcut which is displayed on the UI
Shortcut KeyboardManagerState::GetDetectedShortcut()
{
    std::lock_guard<std::mutex> lock(currentShortcut_mutex);
    return currentShortcut;
}

void KeyboardManagerState::SetDetectedShortcut(Shortcut shortcut)
{
    detectedShortcut = shortcut;
    UpdateDetectShortcutUI();
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
    bool updateUI = false;

    if (AllowChord)
    {
        // Code to determine if we're building/updating a chord.
        auto currentFirstKey = detectedShortcut.GetActionKey();
        auto currentSecondKey = detectedShortcut.GetSecondKey();

        Shortcut tempShortcut = Shortcut(key);
        bool isKeyActionTypeKey = (tempShortcut.actionKey != NULL);

        if (isKeyActionTypeKey)
        {
            // we want a chord and already have the first key set
            std::unique_lock<std::mutex> lock(detectedShortcut_mutex);

            if (currentFirstKey == NULL)
            {
                Logger::trace(L"AllowChord AND no first");
                updateUI = detectedShortcut.SetKey(key);
            }
            else if (currentSecondKey == NULL)
            {
                // we don't have the second key, set it now
                Logger::trace(L"AllowChord AND we have first key of {}, will use {}", currentFirstKey, key);
                updateUI = detectedShortcut.SetSecondKey(key);
            }
            else
            {
                // we already have the second key, swap it to first, and use new as second
                Logger::trace(L"DO have secondKey, will make first {} and second {}", currentSecondKey, key);
                detectedShortcut.actionKey = currentSecondKey;
                detectedShortcut.secondKey = key;
                updateUI = true;
            }
            updateUI = true;
            lock.unlock();
        }
        else
        {
            std::unique_lock<std::mutex> lock(detectedShortcut_mutex);
            updateUI = detectedShortcut.SetKey(key);
            lock.unlock();
        }
    }
    else
    {
        std::unique_lock<std::mutex> lock(detectedShortcut_mutex);
        updateUI = detectedShortcut.SetKey(key);
        lock.unlock();
    }

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
    // only clear if mod, not if action, since we need to keek actionKey and secondKey for chord
    if (Shortcut::IsModifier(key))
    {
        detectedShortcut.ResetKey(key);
    }
}
// Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
Helpers::KeyboardHookDecision KeyboardManagerState::DetectSingleRemapKeyUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect key UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectSingleKeyRemapWindowActivated))
    {
        if (HandleKeyDelayEvent(data))
        {
            return Helpers::KeyboardHookDecision::Suppress;
        }
        // detect the key if it is pressed down
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            SelectDetectedRemapKey(data->lParam->vkCode);
        }

        // Suppress the keyboard event
        return Helpers::KeyboardHookDecision::Suppress;
    }

    // If the settings window is up, remappings should not be applied, but we should not suppress events in the hook
    else if (CheckUIState(KeyboardManagerUIState::EditKeyboardWindowActivated))
    {
        return Helpers::KeyboardHookDecision::SkipHook;
    }

    return Helpers::KeyboardHookDecision::ContinueExec;
}

// Function which can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
Helpers::KeyboardHookDecision KeyboardManagerState::DetectShortcutUIBackend(LowlevelKeyboardEvent* data, bool isRemapKey)
{
    // Check if the detect shortcut UI window has been activated
    if ((!isRemapKey && CheckUIState(KeyboardManagerUIState::DetectShortcutWindowActivated)) || (isRemapKey && CheckUIState(KeyboardManagerUIState::DetectShortcutWindowInEditKeyboardWindowActivated)))
    {
        if (HandleKeyDelayEvent(data))
        {
            return Helpers::KeyboardHookDecision::Suppress;
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
        return Helpers::KeyboardHookDecision::Suppress;
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
        return Helpers::KeyboardHookDecision::SkipHook;
    }

    return Helpers::KeyboardHookDecision::ContinueExec;
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

void KBMEditor::KeyboardManagerState::ClearStoredShortcut()
{
    std::scoped_lock<std::mutex> detectedShortcut_lock(detectedShortcut_mutex);
    detectedShortcut.Reset();
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
