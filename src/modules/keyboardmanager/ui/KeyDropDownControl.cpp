#include "pch.h"
#include "KeyDropDownControl.h"
#include "keyboardmanager/common/Helpers.h"
#include <keyboardmanager/common/KeyboardManagerState.h>
#include "BufferValidationHelpers.h"

// Initialized to null
KeyboardManagerState* KeyDropDownControl::keyboardManagerState = nullptr;

// Function to set properties apart from the SelectionChanged event handler
void KeyDropDownControl::SetDefaultProperties(bool isShortcut)
{
    dropDown = ComboBox();
    warningFlyout = Flyout();
    warningMessage = TextBlock();

    if (!isShortcut)
    {
        dropDown.as<ComboBox>().Width(KeyboardManagerConstants::RemapTableDropDownWidth);
    }
    else
    {
        dropDown.as<ComboBox>().Width(KeyboardManagerConstants::ShortcutTableDropDownWidth);
    }
    dropDown.as<ComboBox>().MaxDropDownHeight(KeyboardManagerConstants::TableDropDownHeight);
    // Initialise layout attribute
    previousLayout = GetKeyboardLayout(0);
    keyCodeList = keyboardManagerState->keyboardMap.GetKeyCodeList(isShortcut);
    dropDown.as<ComboBox>().ItemsSource(KeyboardManagerHelper::ToBoxValue(keyboardManagerState->keyboardMap.GetKeyNameList(isShortcut)));
    // drop down open handler - to reload the items with the latest layout
    dropDown.as<ComboBox>().DropDownOpened([&, isShortcut](winrt::Windows::Foundation::IInspectable const& sender, auto args) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        CheckAndUpdateKeyboardLayout(currentDropDown, isShortcut);
    });

    // Attach flyout to the drop down
    warningFlyout.as<Flyout>().Content(warningMessage.as<TextBlock>());
    dropDown.as<ComboBox>().ContextFlyout().SetAttachedFlyout((FrameworkElement)dropDown.as<ComboBox>(), warningFlyout.as<Flyout>());
    // To set the accessible name of the combo-box
    dropDown.as<ComboBox>().SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_KEY_DROPDOWN_COMBOBOX)));
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
        currentDropDown.ItemsSource(KeyboardManagerHelper::ToBoxValue(keyboardManagerState->keyboardMap.GetKeyNameList(isShortcut)));
        previousLayout = layout;
    }
}

// Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
void KeyDropDownControl::SetSelectionHandler(Grid& table, StackPanel singleKeyControl, int colIndex, RemapBuffer& singleKeyRemapBuffer)
{
    // drop down selection handler
    auto onSelectionChange = [&, table, singleKeyControl, colIndex](winrt::Windows::Foundation::IInspectable const& sender) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        int selectedKeyIndex = currentDropDown.SelectedIndex();
        // Get row index of the single key control
        uint32_t controlIndex;
        bool indexFound = table.Children().IndexOf(singleKeyControl, controlIndex);
        if (indexFound)
        {
            // GetRow will give the row index including the table header
            int rowIndex = table.GetRow(singleKeyControl) - 1;

            // Validate current remap selection
            KeyboardManagerHelper::ErrorType errorType = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(rowIndex, colIndex, selectedKeyIndex, keyCodeList, singleKeyRemapBuffer);

            // If there is an error set the warning flyout
            if (errorType != KeyboardManagerHelper::ErrorType::NoError)
            {
                SetDropDownError(currentDropDown, KeyboardManagerHelper::GetErrorMessage(errorType));
            }
        }
    };

    // Rather than on every selection change (which gets triggered on searching as well) we set the handler only when the drop down is closed
    dropDown.as<ComboBox>().DropDownClosed([onSelectionChange](winrt::Windows::Foundation::IInspectable const& sender, auto const& args) {
        onSelectionChange(sender);
    });

    // We check if the selection changed was triggered while the drop down was closed. This is required to handle Type key, initial loading of remaps and if the user just types in the combo box without opening it
    dropDown.as<ComboBox>().SelectionChanged([onSelectionChange](winrt::Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const& args) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        if (!currentDropDown.IsDropDownOpen())
        {
            onSelectionChange(sender);
        }
    });
}

std::pair<KeyboardManagerHelper::ErrorType, int> KeyDropDownControl::ValidateShortcutSelection(Grid table, StackPanel shortcutControl, StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    ComboBox currentDropDown = dropDown.as<ComboBox>();
    int selectedKeyIndex = currentDropDown.SelectedIndex();
    uint32_t dropDownIndex = -1;
    bool dropDownFound = parent.Children().IndexOf(currentDropDown, dropDownIndex);
    // Get row index of the single key control
    uint32_t controlIndex;
    bool controlIindexFound = table.Children().IndexOf(shortcutControl, controlIndex);
    int rowIndex = -1;
    std::pair<KeyboardManagerHelper::ErrorType, BufferValidationHelpers::DropDownAction> validationResult = std::make_pair(KeyboardManagerHelper::ErrorType::NoError, BufferValidationHelpers::DropDownAction::NoAction);

    if (controlIindexFound)
    {
        // GetRow will give the row index including the table header
        rowIndex = table.GetRow(shortcutControl) - 1;

        std::vector<int32_t> selectedIndices = GetSelectedIndicesFromStackPanel(parent);

        std::wstring appName;
        if (targetApp != nullptr)
        {
            appName = targetApp.Text().c_str();
        }

        // Validate shortcut element
        validationResult = BufferValidationHelpers::ValidateShortcutBufferElement(rowIndex, colIndex, dropDownIndex, selectedIndices, appName, isHybridControl, keyCodeList, shortcutRemapBuffer, dropDownFound);

        // Add or clear unused drop downs
        if (validationResult.second == BufferValidationHelpers::DropDownAction::AddDropDown)
        {
            AddDropDown(table, shortcutControl, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
        }
        else if (validationResult.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns)
        {
            // remove all the drop downs after the current index
            int elementsToBeRemoved = parent.Children().Size() - dropDownIndex - 1;
            for (int i = 0; i < elementsToBeRemoved; i++)
            {
                parent.Children().RemoveAtEnd();
                keyDropDownControlObjects.erase(keyDropDownControlObjects.end() - 1);
            }
            parent.UpdateLayout();
        }

        if (validationResult.first != KeyboardManagerHelper::ErrorType::NoError)
        {
            SetDropDownError(currentDropDown, KeyboardManagerHelper::GetErrorMessage(validationResult.first));
        }

        // Handle None case if there are no other errors
        else if (validationResult.second == BufferValidationHelpers::DropDownAction::DeleteDropDown)
        {
            parent.Children().RemoveAt(dropDownIndex);
            // delete drop down control object from the vector so that it can be destructed
            keyDropDownControlObjects.erase(keyDropDownControlObjects.begin() + dropDownIndex);
            parent.UpdateLayout();
        }
    }

    return std::make_pair(validationResult.first, rowIndex);
}

// Function to set selection handler for shortcut drop down. Needs to be called after the constructor since the shortcutControl StackPanel is null if called in the constructor
void KeyDropDownControl::SetSelectionHandler(Grid& table, StackPanel shortcutControl, StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox& targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    auto onSelectionChange = [&, table, shortcutControl, colIndex, parent, targetApp, isHybridControl, isSingleKeyWindow](winrt::Windows::Foundation::IInspectable const& sender) {
        std::pair<KeyboardManagerHelper::ErrorType, int> validationResult = ValidateShortcutSelection(table, shortcutControl, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);

        // Check if the drop down row index was identified from the return value of validateSelection
        if (validationResult.second != -1)
        {
            // If an error occurred
            if (validationResult.first != KeyboardManagerHelper::ErrorType::NoError)
            {
                // Validate all the drop downs
                ValidateShortcutFromDropDownList(table, shortcutControl, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
            }

            // Reset the buffer based on the new selected drop down items. Use static key code list since the KeyDropDownControl object might be deleted
            std::vector selectedKeyCodes = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(GetSelectedIndicesFromStackPanel(parent), KeyDropDownControl::keyboardManagerState->keyboardMap.GetKeyCodeList(true));
            if (!isHybridControl)
            {
                std::get<Shortcut>(shortcutRemapBuffer[validationResult.second].first[colIndex]).SetKeyCodes(selectedKeyCodes);
            }
            else
            {
                // If exactly one key is selected consider it to be a key remap
                if (selectedKeyCodes.size() == 1)
                {
                    shortcutRemapBuffer[validationResult.second].first[colIndex] = selectedKeyCodes[0];
                }
                else
                {
                    Shortcut tempShortcut;
                    tempShortcut.SetKeyCodes(selectedKeyCodes);
                    // Assign instead of setting the value in the buffer since the previous value may not be a Shortcut
                    shortcutRemapBuffer[validationResult.second].first[colIndex] = tempShortcut;
                }
            }
            if (targetApp != nullptr)
            {
                std::wstring newText = targetApp.Text().c_str();
                std::wstring lowercaseDefAppName = KeyboardManagerConstants::DefaultAppName;
                std::transform(newText.begin(), newText.end(), newText.begin(), towlower);
                std::transform(lowercaseDefAppName.begin(), lowercaseDefAppName.end(), lowercaseDefAppName.begin(), towlower);
                if (newText == lowercaseDefAppName)
                {
                    shortcutRemapBuffer[validationResult.second].second = L"";
                }
                else
                {
                    shortcutRemapBuffer[validationResult.second].second = targetApp.Text().c_str();
                }
            }
        }

        // If the user searches for a key the selection handler gets invoked however if they click away it reverts back to the previous state. This can result in dangling references to added drop downs which were then reset.
        // We handle this by removing the drop down if it no longer a child of the parent
        for (long long i = keyDropDownControlObjects.size() - 1; i >= 0; i--)
        {
            uint32_t index;
            bool found = parent.Children().IndexOf(keyDropDownControlObjects[i]->GetComboBox(), index);
            if (!found)
            {
                keyDropDownControlObjects.erase(keyDropDownControlObjects.begin() + i);
            }
        }
    };

    // Rather than on every selection change (which gets triggered on searching as well) we set the handler only when the drop down is closed
    dropDown.as<ComboBox>().DropDownClosed([onSelectionChange](winrt::Windows::Foundation::IInspectable const& sender, auto const& args) {
        onSelectionChange(sender);
    });

    // We check if the selection changed was triggered while the drop down was closed. This is required to handle Type key, initial loading of remaps and if the user just types in the combo box without opening it
    dropDown.as<ComboBox>().SelectionChanged([onSelectionChange](winrt::Windows::Foundation::IInspectable const& sender, SelectionChangedEventArgs const& args) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        if (!currentDropDown.IsDropDownOpen())
        {
            onSelectionChange(sender);
        }
    });
}

// Function to set the selected index of the drop down
void KeyDropDownControl::SetSelectedIndex(int32_t index)
{
    dropDown.as<ComboBox>().SelectedIndex(index);
}

// Function to return the combo box element of the drop down
ComboBox KeyDropDownControl::GetComboBox()
{
    return dropDown.as<ComboBox>();
}

// Function to add a drop down to the shortcut stack panel
void KeyDropDownControl::AddDropDown(Grid table, StackPanel shortcutControl, StackPanel parent, const int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    keyDropDownControlObjects.push_back(std::move(std::unique_ptr<KeyDropDownControl>(new KeyDropDownControl(true))));
    parent.Children().Append(keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->GetComboBox());
    keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->SetSelectionHandler(table, shortcutControl, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
    parent.UpdateLayout();
}

// Function to get the list of key codes from the shortcut combo box stack panel
std::vector<int32_t> KeyDropDownControl::GetSelectedIndicesFromStackPanel(StackPanel parent)
{
    std::vector<int32_t> selectedIndices;

    // Get selected indices for each drop down
    for (int i = 0; i < (int)parent.Children().Size(); i++)
    {
        ComboBox ItDropDown = parent.Children().GetAt(i).as<ComboBox>();
        selectedIndices.push_back(ItDropDown.SelectedIndex());
    }

    return selectedIndices;
}

// Function for validating the selection of shortcuts for all the associated drop downs
void KeyDropDownControl::ValidateShortcutFromDropDownList(Grid table, StackPanel shortcutControl, StackPanel parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    // Iterate over all drop downs from left to right in that row/col and validate if there is an error in any of the drop downs. After this the state should be error-free (if it is a valid shortcut)
    for (int i = 0; i < keyDropDownControlObjects.size(); i++)
    {
        // Check for errors only if the current selection is a valid shortcut
        std::vector<DWORD> selectedKeyCodes = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(keyDropDownControlObjects[i]->GetSelectedIndicesFromStackPanel(parent), KeyDropDownControl::keyboardManagerState->keyboardMap.GetKeyCodeList(true));
        std::variant<DWORD, Shortcut> currentShortcut;
        if (selectedKeyCodes.size() == 1 && isHybridControl)
        {
            currentShortcut = selectedKeyCodes[0];
        }
        else
        {
            Shortcut temp;
            temp.SetKeyCodes(selectedKeyCodes);
            currentShortcut = temp;
        }

        // If the key/shortcut is valid and that drop down is not empty
        if (((currentShortcut.index() == 0 && std::get<DWORD>(currentShortcut) != NULL) || (currentShortcut.index() == 1 && std::get<Shortcut>(currentShortcut).IsValidShortcut())) && keyDropDownControlObjects[i]->GetComboBox().SelectedIndex() != -1)
        {
            keyDropDownControlObjects[i]->ValidateShortcutSelection(table, shortcutControl, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
        }
    }
}

// Function to set the warning message
void KeyDropDownControl::SetDropDownError(ComboBox currentDropDown, hstring message)
{
    currentDropDown.SelectedIndex(-1);
    warningMessage.as<TextBlock>().Text(message);
    currentDropDown.ContextFlyout().ShowAttachedFlyout((FrameworkElement)dropDown.as<ComboBox>());
}

// Function to add a shortcut to the UI control as combo boxes
void KeyDropDownControl::AddShortcutToControl(Shortcut shortcut, Grid table, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int colIndex, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, RemapBuffer& remapBuffer, StackPanel controlLayout, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    // Delete the existing drop down menus
    parent.Children().Clear();
    // Remove references to the old drop down objects to destroy them
    keyDropDownControlObjects.clear();

    std::vector<DWORD> shortcutKeyCodes = shortcut.GetKeyCodes();
    std::vector<DWORD> keyCodeList = keyboardManagerState.keyboardMap.GetKeyCodeList(true);
    if (shortcutKeyCodes.size() != 0)
    {
        KeyDropDownControl::AddDropDown(table, controlLayout, parent, colIndex, remapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
        for (int i = 0; i < shortcutKeyCodes.size(); i++)
        {
            // New drop down gets added automatically when the SelectedIndex is set
            if (i < (int)parent.Children().Size())
            {
                ComboBox currentDropDown = parent.Children().GetAt(i).as<ComboBox>();
                auto it = std::find(keyCodeList.begin(), keyCodeList.end(), shortcutKeyCodes[i]);
                if (it != keyCodeList.end())
                {
                    currentDropDown.SelectedIndex((int32_t)std::distance(keyCodeList.begin(), it));
                }
            }
        }
    }
    parent.UpdateLayout();
}
