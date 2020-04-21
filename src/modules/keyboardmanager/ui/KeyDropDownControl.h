#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>

// Wrapper class for the key drop down menu
class KeyDropDownControl
{
private:
    // Stores the drop down combo box
    ComboBox dropDown;
    // Stores the previous layout
    HKL previousLayout = 0;
    // Stores the key code list
    std::vector<DWORD> keyCodeList;

    // Function to set properties apart from the SelectionChanged event handler
    void SetDefaultProperties(bool isShortcut);

    // Function to check if the layout has changed and accordingly update the drop down list
    void CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut);

public:
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;

    // Constructor for single key drop down
    KeyDropDownControl(bool isShortcut)
    {
        SetDefaultProperties(isShortcut);
    }

    // Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
    void SetSelectionHandler(Grid& table, StackPanel& singleKeyControl, size_t colIndex, std::vector<std::vector<DWORD>>& singleKeyRemapBuffer);

    // Function to set selection handler for shortcut drop down. Needs to be called after the constructor since the shortcutControl StackPanel is null if called in the constructor
    void SetSelectionHandler(Grid& table, StackPanel& shortcutControl, StackPanel parent, size_t colIndex, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects);

    // Function to set the selected index of the drop down
    void SetSelectedIndex(int32_t index);

    // Function to return the combo box element of the drop down
    ComboBox GetComboBox();

    // Function to add a drop down to the shortcut stack panel
    static void AddDropDown(Grid table, StackPanel shortcutControl, StackPanel parent, const size_t colIndex, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects);

    // Function to get the list of key codes from the shortcut combo box stack panel
    std::vector<DWORD> GetKeysFromStackPanel(StackPanel parent);

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(StackPanel parent, uint32_t dropDownIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList);

    // Function to set the flyout warning message
    void SetDropDownError(ComboBox dropDown, TextBlock messageBlock, hstring message);
};
