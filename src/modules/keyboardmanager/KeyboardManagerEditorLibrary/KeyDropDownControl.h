#pragma once

#include <keyboardmanager/common/Shortcut.h>

// Enables the WinUI teaching tip to show as the new warning flyout
#define USE_NEW_DROPDOWN_WARNING_TIP

namespace KBMEditor
{
    class KeyboardManagerState;
}

class MappingConfiguration;

namespace winrt::Windows
{
    namespace Foundation
    {
        struct hstring;
    }

    namespace UI::Xaml::Controls
    {
        struct RelativePanel;
        struct StackPanel;
        struct ComboBox;
        struct Flyout;
        struct TextBlock;
    }
}

enum class ShortcutErrorType;

// Wrapper class for the key drop down menu
class KeyDropDownControl
{
private:
    // Stores the drop down combo box
    winrt::Windows::Foundation::IInspectable dropDown;

    // Stores the previous layout
    HKL previousLayout = 0;

#ifdef USE_NEW_DROPDOWN_WARNING_TIP
    // Stores the teaching tip attached to the current drop down
    muxc::TeachingTip warningTip;
#else
    // Stores the flyout warning message
    winrt::Windows::Foundation::IInspectable warningMessage;

    // Stores the flyout attached to the current drop down
    winrt::Windows::Foundation::IInspectable warningFlyout;
#endif

    // Stores whether a key to shortcut warning has to be ignored
    bool ignoreKeyToShortcutWarning;

    // Function to set properties apart from the SelectionChanged event handler
    void SetDefaultProperties(bool isShortcut, bool renderDisable);

    // Function to check if the layout has changed and accordingly update the drop down list
    void CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut, bool renderDisable);

    // Get selected value of dropdown or -1 if nothing is selected
    static DWORD GetSelectedValue(ComboBox comboBox);
    static DWORD GetSelectedValue(TextBlock text);

    // Function to set accessible name for combobox
    static void SetAccessibleNameForComboBox(ComboBox dropDown, int index);

public:
    // Pointer to the keyboard manager state
    static KBMEditor::KeyboardManagerState* keyboardManagerState;
    static MappingConfiguration* mappingConfiguration;

    // Constructor - the last default parameter should be passed as false only if it originates from Type shortcut or when an old shortcut is reloaded
    KeyDropDownControl(bool isShortcut, bool fromAddShortcutToControl = false, bool renderDisable = false) :
        ignoreKeyToShortcutWarning(fromAddShortcutToControl)
    {
        SetDefaultProperties(isShortcut, renderDisable);
    }

    // Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
    void SetSelectionHandler(StackPanel& table, StackPanel row, int colIndex, RemapBuffer& singleKeyRemapBuffer);

    // Function for validating the selection of shortcuts for the drop down
    std::pair<ShortcutErrorType, int> ValidateShortcutSelection(StackPanel table, StackPanel row, VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to set selection handler for shortcut drop down.
    void SetSelectionHandler(StackPanel& table, StackPanel row, winrt::Windows::UI::Xaml::Controls::VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox& targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to return the combo box element of the drop down
    ComboBox GetComboBox();

    // Function to add a drop down to the shortcut stack panel
    static void AddDropDown(StackPanel& table, StackPanel row, winrt::Windows::UI::Xaml::Controls::VariableSizedWrapGrid parent, const int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, bool ignoreWarning = false);

    // Function to get the list of key codes from the shortcut combo box stack panel
    static std::vector<int32_t> GetSelectedCodesFromStackPanel(VariableSizedWrapGrid parent);

    // Function for validating the selection of shortcuts for all the associated drop downs
    static void ValidateShortcutFromDropDownList(StackPanel table, StackPanel row, VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Function to set the warning message
    void SetDropDownError(winrt::Windows::UI::Xaml::Controls::ComboBox currentDropDown, winrt::hstring message);

    // Set selected Value
    void SetSelectedValue(std::wstring value);

    // Function to add a shortcut to the UI control as combo boxes
    static void AddShortcutToControl(Shortcut shortcut, StackPanel table, VariableSizedWrapGrid parent, KBMEditor::KeyboardManagerState& keyboardManagerState, const int colIndex, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, RemapBuffer& remapBuffer, StackPanel row, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow);

    // Get keys name list depending if Disable is in dropdown
    static std::vector<std::pair<DWORD, std::wstring>> GetKeyList(bool isShortcut, bool renderDisable);

    // Get number of selected keys. Do not count -1 and 0 values as they stand for Not selected and None
    static int GetNumberOfSelectedKeys(std::vector<int32_t> keys);
};
