#include "pch.h"
#include "KeyboardManagerState.h"

// Constructor
KeyboardManagerState::KeyboardManagerState() :
    uiState(KeyboardManagerUIState::Deactivated), currentUIWindow(nullptr), currentShortcutTextBlock(nullptr), currentSingleKeyRemapTextBlock(nullptr), detectedRemapKey(NULL)
{
}

// Function to check the if the UI state matches the argument state. For states with activated windows it also checks if the window is in focus.
bool KeyboardManagerState::CheckUIState(KeyboardManagerUIState state)
{
    if (uiState == state)
    {
        if (uiState == KeyboardManagerUIState::Deactivated)
        {
            return true;
        }
        // If the UI state is activated then we also have to ensure that the UI window is in focus.
        // GetForegroundWindow can be used here since we only need to check the main parent window and not the sub windows within the content dialog. Using GUIThreadInfo will give more specific sub-windows within the XAML window which is not needed.
        else if (currentUIWindow == GetForegroundWindow())
        {
            return true;
        }
    }

    return false;
}

// Function to set the window handle of the current UI window that is activated
void KeyboardManagerState::SetCurrentUIWindow(HWND windowHandle)
{
    currentUIWindow = windowHandle;
}

// Function to set the UI state. When a window is activated, the handle to the window can be passed in the windowHandle argument.
void KeyboardManagerState::SetUIState(KeyboardManagerUIState state, HWND windowHandle)
{
    uiState = state;
    SetCurrentUIWindow(windowHandle);
}

// Function to reset the UI state members
void KeyboardManagerState::ResetUIState()
{
    SetUIState(KeyboardManagerUIState::Deactivated);

    // Reset the shortcut UI stored variables
    currentShortcutTextBlock = nullptr;

    detectedShortcut.Reset();

    // Reset all the single key remap UI stored variables.
    currentSingleKeyRemapTextBlock = nullptr;

    detectedRemapKey = NULL;
}

// Function to clear the OS Level shortcut remapping table
void KeyboardManagerState::ClearOSLevelShortcuts()
{
    osLevelShortcutReMap.clear();
}

// Function to clear the Keys remapping table.
void KeyboardManagerState::ClearSingleKeyRemaps()
{
    singleKeyReMap.clear();
}

// Function to add a new OS level shortcut remapping
bool KeyboardManagerState::AddOSLevelShortcut(const Shortcut& originalSC, const Shortcut& newSC)
{
    // Check if the shortcut is already remapped
    auto it = osLevelShortcutReMap.find(originalSC);
    if (it != osLevelShortcutReMap.end())
    {
        return false;
    }

    osLevelShortcutReMap[originalSC] = std::make_pair(newSC, false);
    return true;
}

// Function to add a new OS level shortcut remapping
bool KeyboardManagerState::AddSingleKeyRemap(const DWORD& originalKey, const DWORD& newRemapKey)
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

// Function to set the textblock of the detect shortcut UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectShortcutUI(const TextBlock& textBlock)
{
    currentShortcutTextBlock = textBlock;
}

// Function to set the textblock of the detect remap key UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectSingleKeyRemapUI(const TextBlock& textBlock)
{
    currentSingleKeyRemapTextBlock = textBlock;
}

// Function to update the detect shortcut UI based on the entered keys
void KeyboardManagerState::UpdateDetectShortcutUI()
{
    if (currentShortcutTextBlock == nullptr)
    {
        return;
    }

    hstring shortcutString = detectedShortcut.ToHstring();

    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentShortcutTextBlock.Text(shortcutString);
    });
}

// Function to update the detect remap key UI based on the entered key.
void KeyboardManagerState::UpdateDetectSingleKeyRemapUI()
{
    if (currentSingleKeyRemapTextBlock == nullptr)
    {
        return;
    }

    hstring remapKeyString = winrt::to_hstring((unsigned int)detectedRemapKey);

    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentSingleKeyRemapTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentSingleKeyRemapTextBlock.Text(remapKeyString);
    });
}

// Function to return the currently detected shortcut which is displayed on the UI
Shortcut KeyboardManagerState::GetDetectedShortcut()
{
    hstring detectedShortcutString = currentShortcutTextBlock.Text();
    return Shortcut::CreateShortcut(detectedShortcutString);
}

// Function to return the currently detected remap key which is displayed on the UI
DWORD KeyboardManagerState::GetDetectedSingleRemapKey()
{
    hstring remapKeyString = currentSingleKeyRemapTextBlock.Text();

    std::wstring remapKeyWString = remapKeyString.c_str();
    DWORD remapKey = NULL;
    if (!remapKeyString.empty())
    {
        remapKey = std::stoul(remapKeyWString);
    }

    return remapKey;
}

// Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
bool KeyboardManagerState::DetectSingleRemapKeyUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect key UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectSingleKeyRemapWindowActivated))
    {
        // detect the key if it is pressed down
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            detectedRemapKey = data->lParam->vkCode;

            UpdateDetectSingleKeyRemapUI();
        }

        // Suppress the keyboard event
        return true;
    }

    return false;
}

// Function which can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
bool KeyboardManagerState::DetectShortcutUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect shortcut UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectShortcutWindowActivated))
    {
        // Add the key if it is pressed down
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            // Set the new key and store if a change occured
            bool updateUI = detectedShortcut.SetKey(data->lParam->vkCode);
            if (updateUI)
            {
                // Update the UI. This function is called here because it should store the set of keys pressed till the last key which was pressed down.
                UpdateDetectShortcutUI();
            }
        }
        // Remove the key if it has been released
        else if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
        {
            detectedShortcut.ResetKey(data->lParam->vkCode);
        }

        // Suppress the keyboard event
        return true;
    }

    // If the detect shortcut UI window is not activated, then clear the shortcut buffer if it isn't empty
    else
    {
        if (!detectedShortcut.IsEmpty())
        {
            detectedShortcut.Reset();
        }
    }

    return false;
}