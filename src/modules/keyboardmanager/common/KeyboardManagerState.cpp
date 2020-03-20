#include "KeyboardManagerState.h"

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
    currentUIWindow = windowHandle;
}

// Function to reset the UI state members
void KeyboardManagerState::ResetUIState()
{
    SetUIState(KeyboardManagerUIState::Deactivated);
    currentShortcutTextBlock = nullptr;
    detectedShortcut.clear();
}

// Function to clear the OS Level shortcut remapping table
void KeyboardManagerState::ClearOSLevelShortcuts()
{
    osLevelShortcutReMap.clear();
}

// Function to add a new OS level shortcut remapping
void KeyboardManagerState::AddOSLevelShortcut(const std::vector<DWORD>& originalSC,const std::vector<WORD>& newSC)
{
    osLevelShortcutReMap[originalSC] = std::make_pair(newSC, false);
}

// Function to set the textblock of the detect shortcut UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectShortcutUI(const TextBlock& textBlock)
{
    currentShortcutTextBlock = textBlock;
}

// Function to update the detect shortcut UI based on the entered keys
void KeyboardManagerState::UpdateDetectShortcutUI()
{
    if (currentShortcutTextBlock == nullptr)
    {
        return;
    }

    hstring shortcutString = convertVectorToHstring<DWORD>(detectedShortcut);

    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentShortcutTextBlock.Text(shortcutString);
    });
}

// Function to return the currently detected shortcut which is displayed on the UI
std::vector<DWORD> KeyboardManagerState::GetDetectedShortcut()
{
    hstring detectedShortcutString = currentShortcutTextBlock.Text();
    std::wstring detectedShortcutWstring = detectedShortcutString.c_str();
    std::vector<std::wstring> detectedShortcutVector = splitwstring(detectedShortcutWstring, L' ');
    return convertWStringVectorToIntegerVector<DWORD>(detectedShortcutVector);
}

// Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
bool KeyboardManagerState::DetectKeyUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect key UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectKeyWindowActivated))
    {
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
            if (std::find(detectedShortcut.begin(), detectedShortcut.end(), data->lParam->vkCode) == detectedShortcut.end())
            {
                detectedShortcut.push_back(data->lParam->vkCode);
                // Update the UI. This function is called here because it should store the set of keys pressed till the last key which was pressed down.
                UpdateDetectShortcutUI();
            }
        }
        // Remove the key if it has been released
        else if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
        {
            detectedShortcut.erase(std::remove(detectedShortcut.begin(), detectedShortcut.end(), data->lParam->vkCode), detectedShortcut.end());
        }

        // Suppress the keyboard event
        return true;
    }

    // If the detect shortcut UI window is not activated, then clear the shortcut buffer if it isn't empty
    else if (!detectedShortcut.empty())
    {
        detectedShortcut.clear();
    }

    return false;
}