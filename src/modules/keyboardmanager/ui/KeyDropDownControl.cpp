#include "pch.h"
#include "KeyDropDownControl.h"

// Initialized to null
KeyboardManagerState* KeyDropDownControl::keyboardManagerState = nullptr;

// Function to set properties apart from the SelectionChanged event handler
void KeyDropDownControl::SetDefaultProperties(bool isShortcut)
{
    dropDown.Width(100);
    dropDown.MaxDropDownHeight(200);
    // Initialise layout attribute
    previousLayout = GetKeyboardLayout(0);
    keyCodeList = keyboardManagerState->keyboardMap.GetKeyCodeList(isShortcut);
    dropDown.ItemsSource(keyboardManagerState->keyboardMap.GetKeyNameList(isShortcut));
    // drop down open handler - to reload the items with the latest layout
    dropDown.DropDownOpened([&, isShortcut](IInspectable const& sender, auto args) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        CheckAndUpdateKeyboardLayout(currentDropDown, isShortcut);
    });
}

// Function to check if the layout has changed and accordingly update the drop down list
void KeyDropDownControl::CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut)
{
    // Get keyboard layout for current thread
    HKL layout = GetKeyboardLayout(0);

    // Check if the layout has changed
    if (previousLayout != layout)
    {
        keyCodeList = keyboardManagerState->keyboardMap.GetKeyCodeList(isShortcut);
        currentDropDown.ItemsSource(keyboardManagerState->keyboardMap.GetKeyNameList(isShortcut));
        previousLayout = layout;
    }
}

// Function to set the selected index of the drop down
void KeyDropDownControl::SetSelectedIndex(int32_t index)
{
    dropDown.SelectedIndex(index);
}

// Function to return the combo box element of the drop down
ComboBox KeyDropDownControl::GetComboBox()
{
    return dropDown;
}

// Function to add a drop down to the shortcut stack panel
void KeyDropDownControl::AddDropDown(StackPanel parent, const int rowIndex, const int colIndex, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects)
{
    keyDropDownControlObjects.push_back(std::move(std::unique_ptr<KeyDropDownControl>(new KeyDropDownControl(rowIndex, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, parent))));
    // Flyout to display the warning on the drop down element
    Flyout warningFlyout;
    TextBlock warningMessage;
    warningFlyout.Content(warningMessage);
    parent.Children().Append(keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->GetComboBox());
    parent.UpdateLayout();
}

// Function to get the list of key codes from the shortcut combo box stack panel
std::vector<DWORD> KeyDropDownControl::GetKeysFromStackPanel(StackPanel parent)
{
    std::vector<DWORD> keys;
    std::vector<DWORD> keyCodeList = keyboardManagerState->keyboardMap.GetKeyCodeList(true);
    for (int i = 0; i < (int)parent.Children().Size(); i++)
    {
        ComboBox currentDropDown = parent.Children().GetAt(i).as<ComboBox>();
        int selectedKeyIndex = currentDropDown.SelectedIndex();
        if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex)
        {
            // If None is not the selected key
            if (keyCodeList[selectedKeyIndex] != 0)
            {
                keys.push_back(keyCodeList[selectedKeyIndex]);
            }
        }
    }

    return keys;
}

// Function to check if a modifier has been repeated in the previous drop downs
bool KeyDropDownControl::CheckRepeatedModifier(StackPanel parent, uint32_t dropDownIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList)
{
    // check if modifier has already been added before in a previous drop down
    std::vector<DWORD> currentKeys = GetKeysFromStackPanel(parent);
    bool matchPreviousModifier = false;
    for (int i = 0; i < currentKeys.size(); i++)
    {
        // Skip the current drop down
        if (i != dropDownIndex)
        {
            // If the key type for the newly added key matches any of the existing keys in the shortcut
            if (GetKeyType(keyCodeList[selectedKeyIndex]) == GetKeyType(currentKeys[i]))
            {
                matchPreviousModifier = true;
                break;
            }
        }
    }

    return matchPreviousModifier;
}

// Function to set the flyout warning message
void KeyDropDownControl::SetDropDownError(ComboBox dropDown, TextBlock messageBlock, hstring message)
{
    messageBlock.Text(message);
    dropDown.ContextFlyout().ShowAttachedFlyout((FrameworkElement)dropDown);
    dropDown.SelectedIndex(-1);
}
