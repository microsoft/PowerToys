#pragma once
#include "Helpers.h"
#include "Shortcut.h"
#include <interface/lowlevel_keyboard_event_data.h>
#include <mutex>
#include <winrt/Windows.UI.Xaml.Controls.h>
using namespace winrt::Windows::UI::Xaml::Controls;

// Enum type to store different states of the UI
enum class KeyboardManagerUIState
{
    // If set to this value then there is no keyboard manager window currently active that requires a hook
    Deactivated,
    // If set to this value then the detect key window is currently active and it requires a hook
    DetectSingleKeyRemapWindowActivated,
    // If set to this value then the detect shortcut window is currently active and it requires a hook
    DetectShortcutWindowActivated
};

// Class to store the shared state of the keyboard manager between the UI and the hook
class KeyboardManagerState
{
private:
    // State variable used to store which UI window is currently active that requires interaction with the hook
    KeyboardManagerUIState uiState;
    std::mutex uiState_mutex;

    // Window handle for the current UI window which is active. Should be set to nullptr if UI is deactivated
    HWND currentUIWindow;
    std::mutex currentUIWindow_mutex;

    // Object to store the shortcut detected in the detect shortcut UI window. This is used in both the backend and the UI.
    Shortcut detectedShortcut;
    std::mutex detectedShortcut_mutex;

    // Store detected remap key in the remap UI window. This is used in both the backend and the UI.
    DWORD detectedRemapKey;
    std::mutex detectedRemapKey_mutex;

    // Stores the UI element which is to be updated based on the remap key entered.
    TextBlock currentSingleKeyRemapTextBlock;
    std::mutex currentSingleKeyRemapTextBlock_mutex;

    // Stores the UI element which is to be updated based on the shortcut entered
    TextBlock currentShortcutTextBlock;
    std::mutex currentShortcutTextBlock_mutex;

public:
    // The map members and their mutexes are left as public since the maps are used extensively in dllmain.cpp
    // Maps which store the remappings for each of the features. The bool fields should be initalised to false. They are used to check the current state of the shortcut (i.e is that particular shortcut currently pressed down or not).
    // Stores single key remappings
    std::unordered_map<DWORD, WORD> singleKeyReMap;
    std::mutex singleKeyReMap_mutex;

    // Stores keys which need to be changed from toggle behaviour to modifier behaviour. Eg. Caps Lock
    std::unordered_map<DWORD, bool> singleKeyToggleToMod;
    std::mutex singleKeyToggleToMod_mutex;

    // Stores the os level shortcut remappings
    std::map<std::vector<DWORD>, std::pair<std::vector<DWORD>, bool>> osLevelShortcutReMap;
    std::mutex osLevelShortcutReMap_mutex;

    // Stores the app-specific shortcut remappings. Maps application name to the shortcut map
    std::map<std::wstring, std::map<std::vector<DWORD>, std::pair<std::vector<DWORD>, bool>>> appSpecificShortcutReMap;
    std::mutex appSpecificShortcutReMap_mutex;

    // Constructor
    KeyboardManagerState();

    // Function to reset the UI state members
    void ResetUIState();

    // Function to check the if the UI state matches the argument state. For states with activated windows it also checks if the window is in focus.
    bool CheckUIState(KeyboardManagerUIState state);

    // Function to set the window handle of the current UI window that is activated
    void SetCurrentUIWindow(HWND windowHandle);

    // Function to set the UI state. When a window is activated, the handle to the window can be passed in the windowHandle argument.
    void SetUIState(KeyboardManagerUIState state, HWND windowHandle = nullptr);

    // Function to clear the OS Level shortcut remapping table
    void ClearOSLevelShortcuts();

    // Function to clear the Keys remapping table
    void ClearSingleKeyRemaps();

    // Function to add a new single key remapping
    bool AddSingleKeyRemap(const DWORD& originalKey, const DWORD& newRemapKey);

    // Function to add a new OS level shortcut remapping
    bool AddOSLevelShortcut(const std::vector<DWORD>& originalSC, const std::vector<DWORD>& newSC);

    // Function to set the textblock of the detect shortcut UI so that it can be accessed by the hook
    void ConfigureDetectShortcutUI(const TextBlock& textBlock);

    // Function to set the textblock of the detect remap key UI so that it can be accessed by the hook
    void ConfigureDetectSingleKeyRemapUI(const TextBlock& textBlock);

    // Function to update the detect shortcut UI based on the entered keys
    void UpdateDetectShortcutUI();

    // Function to update the detect remap key UI based on the entered key.
    void UpdateDetectSingleKeyRemapUI();

    // Function to return the currently detected shortcut which is displayed on the UI
    Shortcut GetDetectedShortcut();

    // Function to return the currently detected remap key which is displayed on the UI
    DWORD GetDetectedSingleRemapKey();

    // Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
    bool DetectSingleRemapKeyUIBackend(LowlevelKeyboardEvent* data);

    // Function which can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
    bool DetectShortcutUIBackend(LowlevelKeyboardEvent* data);
};