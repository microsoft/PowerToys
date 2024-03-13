#pragma once

#include <common/hooks/LowlevelKeyboardEvent.h>
#include <common/interop/keyboard_layout.h>

#include <keyboardmanager/common/KeyboardManagerConstants.h>
#include <keyboardmanager/common/Shortcut.h>

class KeyDelay;

namespace Helpers
{
    // Enum type to store possible decision for input in the low level hook
    enum class KeyboardHookDecision
    {
        ContinueExec,
        Suppress,
        SkipHook
    };
}

namespace winrt::Windows::UI::Xaml::Controls
{
    struct StackPanel;
    struct TextBlock;
}

namespace KBMEditor
{
    // Enum type to store different states of the UI
    enum class KeyboardManagerUIState
    {
        // If set to this value then there is no keyboard manager window currently active that requires a hook
        Deactivated,
        // If set to this value then the detect key window is currently active and it requires a hook
        DetectSingleKeyRemapWindowActivated,
        // If set to this value then the detect shortcut window in edit keyboard window is currently active and it requires a hook
        DetectShortcutWindowInEditKeyboardWindowActivated,
        // If set to this value then the edit keyboard window is currently active and remaps should not be applied
        EditKeyboardWindowActivated,
        // If set to this value then the detect shortcut window is currently active and it requires a hook
        DetectShortcutWindowActivated,
        // If set to this value then the edit shortcuts window is currently active and remaps should not be applied
        EditShortcutsWindowActivated
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

        // Object to store the shortcut detected in the detect shortcut UI window. Gets cleared on releasing keys. This is used in both the backend and the UI.
        Shortcut detectedShortcut;
        std::mutex detectedShortcut_mutex;

        // Object to store the shortcut state displayed in the UI window. Always stores last displayed shortcut irrespective of releasing keys. This is used in both the backend and the UI.
        Shortcut currentShortcut;
        std::mutex currentShortcut_mutex;

        // Store detected remap key in the remap UI window. This is used in both the backend and the UI.
        DWORD detectedRemapKey;
        std::mutex detectedRemapKey_mutex;

        // Stores the UI element which is to be updated based on the remap key entered.
        winrt::Windows::Foundation::IInspectable currentSingleKeyUI;
        std::mutex currentSingleKeyUI_mutex;

        // Stores the UI element which is to be updated based on the shortcut entered (each StackPanel represents a row of keys)
        winrt::Windows::Foundation::IInspectable currentShortcutUI1;
        winrt::Windows::Foundation::IInspectable currentShortcutUI2;
        std::mutex currentShortcutUI_mutex;

        // Registered KeyDelay objects, used to notify delayed key events.
        std::map<DWORD, std::unique_ptr<KeyDelay>> keyDelays;
        std::mutex keyDelays_mutex;

    public:
        // Display a key by appending a border Control as a child of the panel.
        winrt::Windows::UI::Xaml::Controls::TextBlock AddKeyToLayout(const winrt::Windows::UI::Xaml::Controls::StackPanel& panel, const winrt::hstring& key);

        

        // flag to set if we want to allow building a chord
        bool AllowChord = false;

        // Stores the keyboard layout
        LayoutMap keyboardMap;

        // Constructor
        KeyboardManagerState();

        // Destructor
        ~KeyboardManagerState();

        // Function to reset the UI state members
        void ResetUIState();

        // Function to check the if the UI state matches the argument state. For states with detect windows it also checks if the window is in focus.
        bool CheckUIState(KeyboardManagerUIState state);

        // Function to set the window handle of the current UI window that is activated
        void SetCurrentUIWindow(HWND windowHandle);

        // Function to set the UI state. When a window is activated, the handle to the window can be passed in the windowHandle argument.
        void SetUIState(KeyboardManagerUIState state, HWND windowHandle = nullptr);

        // Function to set the textblock of the detect shortcut UI so that it can be accessed by the hook
        void ConfigureDetectShortcutUI(const winrt::Windows::UI::Xaml::Controls::StackPanel& textBlock1, const winrt::Windows::UI::Xaml::Controls::StackPanel& textBlock2);

        // Function to set the textblock of the detect remap key UI so that it can be accessed by the hook
        void ConfigureDetectSingleKeyRemapUI(const winrt::Windows::UI::Xaml::Controls::StackPanel& textBlock);

        // Function to update the detect shortcut UI based on the entered keys
        void UpdateDetectShortcutUI();

        // Function to update the detect remap key UI based on the entered key.
        void UpdateDetectSingleKeyRemapUI();

        // Function to return the currently detected shortcut which is displayed on the UI
        Shortcut GetDetectedShortcut();

        // Function to SetDetectedShortcut and also UpdateDetectShortcutUI
        void KeyboardManagerState::SetDetectedShortcut(Shortcut shortcut);

        // Function to return the currently detected remap key which is displayed on the UI
        DWORD GetDetectedSingleRemapKey();

        // Function which can be used in HandleKeyboardHookEvent before the single key remap event to use the UI and suppress events while the remap window is active.
        Helpers::KeyboardHookDecision DetectSingleRemapKeyUIBackend(LowlevelKeyboardEvent* data);

        // Function which can be used in HandleKeyboardHookEvent before the os level shortcut remap event to use the UI and suppress events while the remap window is active.
        Helpers::KeyboardHookDecision DetectShortcutUIBackend(LowlevelKeyboardEvent* data, bool isRemapKey);

        // Add a KeyDelay object to get delayed key presses events for a given virtual key
        // NOTE: this will throw an exception if a virtual key is registered twice.
        // NOTE*: the virtual key should represent the original, unmapped virtual key.
        void RegisterKeyDelay(
            DWORD key,
            std::function<void(DWORD)> onShortPress,
            std::function<void(DWORD)> onLongPressDetected,
            std::function<void(DWORD)> onLongPressReleased);

        // Remove a KeyDelay.
        // NOTE: this method will throw if the virtual key is not registered beforehand.
        // NOTE*: the virtual key should represent the original, unmapped virtual key.
        void UnregisterKeyDelay(DWORD key);

        // Function to clear all the registered key delays
        void ClearRegisteredKeyDelays();

        void ClearStoredShortcut();

        // Handle a key event, for a delayed key.
        bool HandleKeyDelayEvent(LowlevelKeyboardEvent* ev);

        // Update the currently selected single key remap
        void SelectDetectedRemapKey(DWORD key);

        // Update the currently selected shortcut.
        void SelectDetectedShortcut(DWORD key);

        // Reset the shortcut (backend) state after releasing a key.
        void ResetDetectedShortcutKey(DWORD key);
    };
}