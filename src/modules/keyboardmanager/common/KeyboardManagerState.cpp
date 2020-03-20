#include "KeyboardManagerState.h"

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

void KeyboardManagerState::SetCurrentUIWindow(HWND windowHandle)
{
    currentUIWindow = windowHandle;
}

void KeyboardManagerState::SetUIState(KeyboardManagerUIState state, HWND windowHandle)
{
    uiState = state;
    currentUIWindow = windowHandle;
}

void KeyboardManagerState::ResetUIState()
{
    SetUIState(KeyboardManagerUIState::Deactivated);
    currentShortcutTextBlock = nullptr;
    detectedShortcut.clear();
}

void KeyboardManagerState::ClearOSLevelShortcuts()
{
    osLevelShortcutReMap.clear();
}

void KeyboardManagerState::AddOSLevelShortcut(std::vector<DWORD> originalSC, std::vector<WORD> newSC)
{
    osLevelShortcutReMap[originalSC] = std::make_pair(newSC, false);
}

void KeyboardManagerState::ConfigureDetectShortcutUI(TextBlock& textBlock)
{
    currentShortcutTextBlock = textBlock;
}

void KeyboardManagerState::UpdateDetectShortcutUI()
{
    if (currentShortcutTextBlock == nullptr)
    {
        return;
    }

    hstring shortcutString = convertVectorToHstring<DWORD>(detectedShortcut);    

    currentShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentShortcutTextBlock.Text(shortcutString);
    });
}

std::vector<DWORD> KeyboardManagerState::GetDetectedShortcut()
{
    hstring detectedShortcutString = currentShortcutTextBlock.Text();
    return convertWStringVectorToNumberType<DWORD>(splitwstring(detectedShortcutString.c_str(), L' '));
}

// This function can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
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

// This function can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
bool KeyboardManagerState::DetectShortcutUIBackend(LowlevelKeyboardEvent* data)
{
    // Check if the detect shortcut UI window has been activated
    if (CheckUIState(KeyboardManagerUIState::DetectShortcutWindowActivated))
    {
        if (data->wParam == WM_KEYDOWN || data->wParam == WM_SYSKEYDOWN)
        {
            if (std::find(detectedShortcut.begin(), detectedShortcut.end(), data->lParam->vkCode) == detectedShortcut.end())
            {
                detectedShortcut.push_back(data->lParam->vkCode);
                UpdateDetectShortcutUI();
            }
        }
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