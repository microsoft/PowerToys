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
    std::lock_guard<std::mutex> lock(uiState_mutex);
    if (uiState == state)
    {
        std::unique_lock<std::mutex> lock(currentUIWindow_mutex);
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
    std::unique_lock<std::mutex> currentShortcutTextBlock_lock(currentShortcutTextBlock_mutex);
    currentShortcutTextBlock = nullptr;
    currentShortcutTextBlock_lock.unlock();

    std::unique_lock<std::mutex> detectedShortcut_lock(detectedShortcut_mutex);
    detectedShortcut.clear();
    detectedShortcut_lock.unlock();

    // Reset all the single key remap UI stored variables.
    std::unique_lock<std::mutex> currentSingleKeyRemapTextBlock_lock(currentSingleKeyRemapTextBlock_mutex);
    currentSingleKeyRemapTextBlock = nullptr;
    currentSingleKeyRemapTextBlock_lock.unlock();

    std::unique_lock<std::mutex> detectedRemapKey_lock(detectedRemapKey_mutex);
    detectedRemapKey = NULL;
    detectedRemapKey_lock.unlock();
}

// Function to clear the OS Level shortcut remapping table
void KeyboardManagerState::ClearOSLevelShortcuts()
{
    std::lock_guard<std::mutex> lock(osLevelShortcutReMap_mutex);
    osLevelShortcutReMap.clear();
}

// Function to clear the Keys remapping table.
void KeyboardManagerState::ClearSingleKeyRemaps()
{
    std::lock_guard<std::mutex> lock(singleKeyReMap_mutex);
    singleKeyReMap.clear();
}

// Function to add a new OS level shortcut remapping
bool KeyboardManagerState::AddOSLevelShortcut(const std::vector<DWORD>& originalSC, const std::vector<WORD>& newSC)
{
    std::lock_guard<std::mutex> lock(osLevelShortcutReMap_mutex);

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
bool KeyboardManagerState::AddSingleKeyRemap(const DWORD& originalKey, const WORD& newRemapKey)
{
    std::lock_guard<std::mutex> lock(singleKeyReMap_mutex);

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
    std::lock_guard<std::mutex> lock(currentShortcutTextBlock_mutex);
    currentShortcutTextBlock = textBlock;
}

// Function to set the textblock of the detect remap key UI so that it can be accessed by the hook
void KeyboardManagerState::ConfigureDetectSingleKeyRemapUI(const TextBlock& textBlock)
{
    std::lock_guard<std::mutex> lock(currentSingleKeyRemapTextBlock_mutex);
    currentSingleKeyRemapTextBlock = textBlock;
}

// Function to update the detect shortcut UI based on the entered keys
void KeyboardManagerState::UpdateDetectShortcutUI()
{
    std::lock_guard<std::mutex> currentShortcutTextBlock_lock(currentShortcutTextBlock_mutex);
    if (currentShortcutTextBlock == nullptr)
    {
        return;
    }

    std::unique_lock<std::mutex> detectedShortcut_lock(detectedShortcut_mutex);
    hstring shortcutString = convertVectorToHstring<DWORD>(detectedShortcut);
    detectedShortcut_lock.unlock();

    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentShortcutTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentShortcutTextBlock.Text(shortcutString);
    });
}

// Function to update the detect remap key UI based on the entered key.
void KeyboardManagerState::UpdateDetectSingleKeyRemapUI()
{
    std::lock_guard<std::mutex> currentSingleKeyRemapTextBlock_lock(currentSingleKeyRemapTextBlock_mutex);
    if (currentSingleKeyRemapTextBlock == nullptr)
    {
        return;
    }

    std::unique_lock<std::mutex> detectedRemapKey_lock(detectedRemapKey_mutex);
    hstring remapKeyString = winrt::to_hstring((unsigned int)detectedRemapKey);
    detectedRemapKey_lock.unlock();

    // Since this function is invoked from the back-end thread, in order to update the UI the dispatcher must be used.
    currentSingleKeyRemapTextBlock.Dispatcher().RunAsync(Windows::UI::Core::CoreDispatcherPriority::Normal, [=]() {
        currentSingleKeyRemapTextBlock.Text(remapKeyString);
    });
}

// Function to return the currently detected shortcut which is displayed on the UI
std::vector<DWORD> KeyboardManagerState::GetDetectedShortcut()
{
    std::unique_lock<std::mutex> lock(currentShortcutTextBlock_mutex);
    hstring detectedShortcutString = currentShortcutTextBlock.Text();
    lock.unlock();

    std::wstring detectedShortcutWstring = detectedShortcutString.c_str();
    std::vector<std::wstring> detectedShortcutVector = splitwstring(detectedShortcutWstring, L' ');
    return convertWStringVectorToIntegerVector<DWORD>(detectedShortcutVector);
}

// Function to return the currently detected remap key which is displayed on the UI
DWORD KeyboardManagerState::GetDetectedSingleRemapKey()
{
    std::unique_lock<std::mutex> lock(currentSingleKeyRemapTextBlock_mutex);
    hstring remapKeyString = currentSingleKeyRemapTextBlock.Text();
    lock.unlock();

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
            std::unique_lock<std::mutex> detectedRemapKey_lock(detectedRemapKey_mutex);
            detectedRemapKey = data->lParam->vkCode;
            detectedRemapKey_lock.unlock();

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
            std::unique_lock<std::mutex> lock(detectedShortcut_mutex);
            if (std::find(detectedShortcut.begin(), detectedShortcut.end(), data->lParam->vkCode) == detectedShortcut.end())
            {
                detectedShortcut.push_back(data->lParam->vkCode);
                lock.unlock();

                // Update the UI. This function is called here because it should store the set of keys pressed till the last key which was pressed down.
                UpdateDetectShortcutUI();
            }
        }
        // Remove the key if it has been released
        else if (data->wParam == WM_KEYUP || data->wParam == WM_SYSKEYUP)
        {
            std::lock_guard<std::mutex> lock(detectedShortcut_mutex);
            detectedShortcut.erase(std::remove(detectedShortcut.begin(), detectedShortcut.end(), data->lParam->vkCode), detectedShortcut.end());
        }

        // Suppress the keyboard event
        return true;
    }

    // If the detect shortcut UI window is not activated, then clear the shortcut buffer if it isn't empty
    else
    {
        std::lock_guard<std::mutex> lock(detectedShortcut_mutex);
        if (!detectedShortcut.empty())
        {
            detectedShortcut.clear();
        }
    }

    return false;
}