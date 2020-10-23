#pragma once
#include <keyboardmanager/common/Shortcut.h>
#include <vector>
class KeyboardManagerState;

namespace winrt::Windows
{
    namespace Foundation
    {
        struct hstring;
    }

    namespace UI::Xaml::Controls
    {
        struct StackPanel;
        struct Grid;
        struct ComboBox;
        struct Flyout;
        struct TextBlock;
    }
}

namespace KeyboardManagerHelper
{
    enum class ErrorType;
}

// Wrapper class for the key drop down menu
class KeyDropDownControl
{
private:
    // Stores the drop down combo box
    winrt::Windows::Foundation::IInspectable dropDown;
    // Stores the previous layout
    HKL previousLayout = 0;
    // Stores the flyout warning message
    winrt::Windows::Foundation::IInspectable warningMessage;
    // Stores the flyout attached to the current drop down
    winrt::Windows::Foundation::IInspectable warningFlyout;
    // Stores whether a key to shortcut warning has to be ignored
    bool ignoreKeyToShortcutWarning;

    // Function to set properties apart from the SelectionChanged event handler
    void SetDefaultProperties(bool isShortcut, bool renderDisable);

    // Function to check if the layout has changed and accordingly update the drop down list
    void CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut, bool renderDisable);

    // Get selected value of dropdown or -1 if nothing is selected
    static DWORD GetSelectedValue(ComboBox comboBox);

    // Function to set accessible name for combobox
    static void SetAccessibleNameForComboBox(ComboBox dropDown, int index);
public:
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;

    // Constructor - the last default parameter should be passed as false only if it originates from Type shortcut or when an old shortcut is reloaded
    KeyDropDownControl(bool isShortcut, bool fromAddShortcutToControl = false, bool renderDisable = false) :
        ignoreKeyToShortcutWarning(fromAddShortcutToControl)
    {
        SetDefaultProperties(isShortcut, renderDisable);
    }

    // Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
    void SetSelectionHandler(winrt::Windows::UI::Xaml::Controls::Grid& table, winrt::Windows::UI::Xaml::Controls::StackPanel singleKeyControl, int colIndex, RemapBuffer& singleKeyRemapBuffer);

    // Function for validating the selection of shortcuts for the drop down
    std::pair<KeyboardManagerHelper::ErrorType, int> ValidateShortcutSelection(winrt::Windows::UI::Xaml::Controls::Grid table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to set selection handler for shortcut drop down. Needs to be called after the constructor since the shortcutControl StackPanel is null if called in the constructor
    void SetSelectionHandler(winrt::Windows::UI::Xaml::Controls::Grid& table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox& targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to return the combo box element of the drop down
    ComboBox GetComboBox();

    // Function to add a drop down to the shortcut stack panel
    static void AddDropDown(winrt::Windows::UI::Xaml::Controls::Grid table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, const int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, bool ignoreWarning = false);

    // Function to get the list of key codes from the shortcut combo box stack panel
    static std::vector<int32_t> GetSelectedCodesFromStackPanel(StackPanel parent);

    // Function for validating the selection of shortcuts for all the associated drop downs
    static void ValidateShortcutFromDropDownList(Grid table, StackPanel shortcutControl, StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to set the warning message
    void SetDropDownError(winrt::Windows::UI::Xaml::Controls::ComboBox currentDropDown, winrt::hstring message);

    // Set selected Value
    void SetSelectedValue(std::wstring value);

    // Function to add a shortcut to the UI control as combo boxes
    static void AddShortcutToControl(Shortcut shortcut, Grid table, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int colIndex, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, RemapBuffer& remapBuffer, StackPanel controlLayout, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Get keys name list depending if Disable is in dropdown
    static std::vector<std::pair<DWORD,std::wstring>> GetKeyList(bool isShortcut, bool renderDisable);

    // Get number of selected keys. Do not count -1 and 0 values as they stand for Not selected and None
    static int GetNumberOfSelectedKeys(std::vector<int32_t> keys);
};
