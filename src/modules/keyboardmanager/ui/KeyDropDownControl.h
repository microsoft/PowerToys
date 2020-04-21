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

    // Constructor for shortcut drop down
    KeyDropDownControl(size_t rowIndex, size_t colIndex, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, StackPanel parent)
    {
        SetDefaultProperties(true);
        Flyout warningFlyout;
        TextBlock warningMessage;
        warningFlyout.Content(warningMessage);
        dropDown.ContextFlyout().SetAttachedFlyout((FrameworkElement)dropDown, warningFlyout);

        // drop down selection handler
        dropDown.SelectionChanged([&, rowIndex, colIndex, parent, warningMessage](winrt::Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const&) {
            ComboBox currentDropDown = sender.as<ComboBox>();
            int selectedKeyIndex = currentDropDown.SelectedIndex();
            uint32_t dropDownIndex = -1;
            bool dropDownFound = parent.Children().IndexOf(currentDropDown, dropDownIndex);

            if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex && dropDownFound)
            {
                // If only 1 drop down and action key is chosen: Warn that a modifier must be chosen
                if (parent.Children().Size() == 1 && !KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]))
                {
                    // warn and reset the drop down
                    SetDropDownError(currentDropDown, warningMessage, L"Shortcut must start with a modifier key");
                }
                // If it is the last drop down
                else if (dropDownIndex == parent.Children().Size() - 1)
                {
                    // If last drop down and a modifier is selected: add a new drop down (max of 5 drop downs should be enforced)
                    if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]) && parent.Children().Size() < 5)
                    {
                        // If it matched any of the previous modifiers then reset that drop down
                        if (CheckRepeatedModifier(parent, dropDownIndex, selectedKeyIndex, keyCodeList))
                        {
                            // warn and reset the drop down
                            SetDropDownError(currentDropDown, warningMessage, L"Shortcut cannot contain a repeated modifier");
                        }
                        // If not, add a new drop down
                        else
                        {
                            AddDropDown(parent, rowIndex, colIndex, shortcutRemapBuffer, keyDropDownControlObjects);
                        }
                    }
                    // If last drop down and a modifier is selected but there are already 5 drop downs: warn the user
                    else if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]) && parent.Children().Size() >= 5)
                    {
                        // warn and reset the drop down
                        SetDropDownError(currentDropDown, warningMessage, L"Shortcut must contain an action key");
                    }
                    // If None is selected but it's the last index: warn
                    else if (keyCodeList[selectedKeyIndex] == 0)
                    {
                        // warn and reset the drop down
                        SetDropDownError(currentDropDown, warningMessage, L"Shortcut must contain an action key");
                    }
                    // If none of the above, then the action key will be set
                }
                // If it is the not the last drop down
                else
                {
                    if (KeyboardManagerHelper::IsModifierKey(keyCodeList[selectedKeyIndex]))
                    {
                        // If it matched any of the previous modifiers then reset that drop down
                        if (CheckRepeatedModifier(parent, dropDownIndex, selectedKeyIndex, keyCodeList))
                        {
                            // warn and reset the drop down
                            SetDropDownError(currentDropDown, warningMessage, L"Shortcut cannot contain a repeated modifier");
                        }
                        // If not, the modifier key will be set
                    }
                    // If None is selected and there are more than 2 drop downs
                    else if (keyCodeList[selectedKeyIndex] == 0 && parent.Children().Size() > 2)
                    {
                        // delete drop down
                        parent.Children().RemoveAt(dropDownIndex);
                        // delete drop down control object from the vector so that it can be destructed
                        keyDropDownControlObjects.erase(keyDropDownControlObjects.begin() + dropDownIndex);
                        parent.UpdateLayout();
                    }
                    else if (keyCodeList[selectedKeyIndex] == 0 && parent.Children().Size() <= 2)
                    {
                        // warn and reset the drop down
                        SetDropDownError(currentDropDown, warningMessage, L"Shortcut must have atleast 2 keys");
                    }
                    // If the user tries to set an action key check if all drop down menus after this are empty if it is not the first key
                    else if (dropDownIndex != 0)
                    {
                        bool isClear = true;
                        for (int i = dropDownIndex + 1; i < (int)parent.Children().Size(); i++)
                        {
                            ComboBox currentDropDown = parent.Children().GetAt(i).as<ComboBox>();
                            if (currentDropDown.SelectedIndex() != -1)
                            {
                                isClear = false;
                                break;
                            }
                        }

                        if (isClear)
                        {
                            // remove all the drop down
                            int elementsToBeRemoved = parent.Children().Size() - dropDownIndex - 1;
                            for (int i = 0; i < elementsToBeRemoved; i++)
                            {
                                parent.Children().RemoveAtEnd();
                            }
                            parent.UpdateLayout();
                        }
                        else
                        {
                            // warn and reset the drop down
                            SetDropDownError(currentDropDown, warningMessage, L"Shortcut cannot have more than one action key");
                        }
                    }
                    // If there an action key is chosen on the first drop down and there are more than one drop down menus
                    else
                    {
                        // warn and reset the drop down
                        SetDropDownError(currentDropDown, warningMessage, L"Shortcut must start with a modifier key");
                    }
                }
            }

            // Reset the buffer based on the new selected drop down items
            shortcutRemapBuffer[rowIndex][colIndex].SetKeyCodes(GetKeysFromStackPanel(parent));
        });
    }

    // Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
    void SetSelectionHandler(Grid& table, StackPanel& singleKeyControl, size_t colIndex, std::vector<std::vector<DWORD>>& singleKeyRemapBuffer)
    {
        dropDown.SelectionChanged([&, table, singleKeyControl, colIndex](winrt::Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const& args) {
            ComboBox currentDropDown = sender.as<ComboBox>();
            int selectedKeyIndex = currentDropDown.SelectedIndex();
            // Get row index of the single key control
            uint32_t controlIndex;
            bool indexFound = table.Children().IndexOf(singleKeyControl, controlIndex);
            if (indexFound)
            {
                int rowIndex = (controlIndex - 2) / 3;
                // Check if the element was not found or the index exceeds the known keys
                if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex)
                {
                    singleKeyRemapBuffer[rowIndex][colIndex] = keyCodeList[selectedKeyIndex];
                }
                else
                {
                    // Reset to null if the key is not found
                    singleKeyRemapBuffer[rowIndex][colIndex] = NULL;
                }
            }
        });
    }

    // Function to set the selected index of the drop down
    void SetSelectedIndex(int32_t index);

    // Function to return the combo box element of the drop down
    ComboBox GetComboBox();

    // Function to add a drop down to the shortcut stack panel
    static void AddDropDown(StackPanel parent, const size_t rowIndex, const size_t colIndex, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects);

    // Function to get the list of key codes from the shortcut combo box stack panel
    std::vector<DWORD> GetKeysFromStackPanel(StackPanel parent);

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(StackPanel parent, uint32_t dropDownIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList);

    // Function to set the flyout warning message
    void SetDropDownError(ComboBox dropDown, TextBlock messageBlock, hstring message);
};
