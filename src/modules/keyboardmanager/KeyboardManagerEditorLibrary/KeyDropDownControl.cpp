#include "pch.h"
#include "KeyDropDownControl.h"

#include <common/interop/shared_constants.h>

#include <keyboardmanager/common/MappingConfiguration.h>

#include "KeyboardManagerState.h"
#include "BufferValidationHelpers.h"
#include "KeyboardManagerEditorStrings.h"
#include "UIHelpers.h"
#include "EditorHelpers.h"
#include "ShortcutErrorType.h"
#include "EditorConstants.h"

// Initialized to null
KBMEditor::KeyboardManagerState* KeyDropDownControl::keyboardManagerState = nullptr;
MappingConfiguration* KeyDropDownControl::mappingConfiguration = nullptr;

// Get selected value of dropdown or -1 if nothing is selected
DWORD KeyDropDownControl::GetSelectedValue(ComboBox comboBox)
{
    auto dataContext = comboBox.SelectedValue();
    if (!dataContext)
    {
        return -1;
    }

    auto value = winrt::unbox_value<hstring>(dataContext);
    return stoi(std::wstring(value));
}

void KeyDropDownControl::SetSelectedValue(std::wstring value)
{
    this->dropDown.as<ComboBox>().SelectedValue(winrt::box_value(value));
}

// Get keys name list depending if Disable is in dropdown
std::vector<std::pair<DWORD, std::wstring>> KeyDropDownControl::GetKeyList(bool isShortcut, bool renderDisable)
{
    auto list = keyboardManagerState->keyboardMap.GetKeyNameList(isShortcut);
    if (renderDisable)
    {
        list.insert(list.begin(), { CommonSharedConstants::VK_DISABLED, keyboardManagerState->keyboardMap.GetKeyName(CommonSharedConstants::VK_DISABLED) });
    }

    return list;
}

// Function to set properties apart from the SelectionChanged event handler
void KeyDropDownControl::SetDefaultProperties(bool isShortcut, bool renderDisable)
{
    dropDown = ComboBox();
    warningFlyout = Flyout();
    warningMessage = TextBlock();

    if (!isShortcut)
    {
        dropDown.as<ComboBox>().Width(EditorConstants::RemapTableDropDownWidth);
    }
    else
    {
        dropDown.as<ComboBox>().Width(EditorConstants::ShortcutTableDropDownWidth);
    }

    dropDown.as<ComboBox>().MaxDropDownHeight(EditorConstants::TableDropDownHeight);
    
    // Initialise layout attribute
    previousLayout = GetKeyboardLayout(0);
    dropDown.as<ComboBox>().SelectedValuePath(L"DataContext");
    dropDown.as<ComboBox>().ItemsSource(UIHelpers::ToBoxValue(GetKeyList(isShortcut, renderDisable)));
    dropDown.as<ComboBox>().Margin(ThicknessHelper::FromLengths(0, 0, EditorConstants::ShortcutTableDropDownSpacing, EditorConstants::ShortcutTableDropDownSpacing));

    // drop down open handler - to reload the items with the latest layout
    dropDown.as<ComboBox>().DropDownOpened([&, isShortcut](winrt::Windows::Foundation::IInspectable const& sender, auto args) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        CheckAndUpdateKeyboardLayout(currentDropDown, isShortcut, renderDisable);
    });

    // Attach flyout to the drop down
    warningFlyout.as<Flyout>().Content(warningMessage.as<TextBlock>());

    // Enable narrator for Content of FlyoutPresenter. For details https://learn.microsoft.com/uwp/api/windows.ui.xaml.controls.flyout?view=winrt-19041#accessibility
    Style style = Style(winrt::xaml_typename<FlyoutPresenter>());
    style.Setters().Append(Setter(Windows::UI::Xaml::Controls::Control::IsTabStopProperty(), winrt::box_value(true)));
    style.Setters().Append(Setter(Windows::UI::Xaml::Controls::Control::TabNavigationProperty(), winrt::box_value(Windows::UI::Xaml::Input::KeyboardNavigationMode::Cycle)));
    warningFlyout.as<Flyout>().FlyoutPresenterStyle(style);
    dropDown.as<ComboBox>().ContextFlyout().SetAttachedFlyout((FrameworkElement)dropDown.as<ComboBox>(), warningFlyout.as<Flyout>());
    
    // To set the accessible name of the combo-box (by default index 1)
    SetAccessibleNameForComboBox(dropDown.as<ComboBox>(), 1);
}

// Function to set accessible name for combobox
void KeyDropDownControl::SetAccessibleNameForComboBox(ComboBox dropDown, int index)
{
    // Display name with drop down index (where this indexing will start from 1) - Used by narrator
    dropDown.SetValue(Automation::AutomationProperties::NameProperty(), box_value(GET_RESOURCE_STRING(IDS_KEY_DROPDOWN_COMBOBOX) + L" " + std::to_wstring(index)));
}

// Function to check if the layout has changed and accordingly update the drop down list
void KeyDropDownControl::CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut, bool renderDisable)
{
    // Get keyboard layout for current thread
    HKL layout = GetKeyboardLayout(0);

    // Check if the layout has changed
    if (previousLayout != layout)
    {
        currentDropDown.ItemsSource(UIHelpers::ToBoxValue(GetKeyList(isShortcut, renderDisable)));
        previousLayout = layout;
    }
}

// Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
void KeyDropDownControl::SetSelectionHandler(StackPanel& table, StackPanel row, int colIndex, RemapBuffer& singleKeyRemapBuffer)
{
    // drop down selection handler
    auto onSelectionChange = [&, table, row, colIndex](winrt::Windows::Foundation::IInspectable const& sender) {
        uint32_t rowIndex = -1;
        if (!table.Children().IndexOf(row, rowIndex))
        {
            return;
        }

        ComboBox currentDropDown = sender.as<ComboBox>();
        int selectedKeyCode = GetSelectedValue(currentDropDown);
        
        // Validate current remap selection
        ShortcutErrorType errorType = BufferValidationHelpers::ValidateAndUpdateKeyBufferElement(rowIndex, colIndex, selectedKeyCode, singleKeyRemapBuffer);

        // If there is an error set the warning flyout
        if (errorType != ShortcutErrorType::NoError)
        {
            SetDropDownError(currentDropDown, KeyboardManagerEditorStrings::GetErrorMessage(errorType));
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

std::pair<ShortcutErrorType, int> KeyDropDownControl::ValidateShortcutSelection(StackPanel table, StackPanel row, VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    ComboBox currentDropDown = dropDown.as<ComboBox>();
    uint32_t dropDownIndex = -1;
    bool dropDownFound = parent.Children().IndexOf(currentDropDown, dropDownIndex);
    std::pair<ShortcutErrorType, BufferValidationHelpers::DropDownAction> validationResult = std::make_pair(ShortcutErrorType::NoError, BufferValidationHelpers::DropDownAction::NoAction);

    uint32_t rowIndex;
    bool controlIindexFound = table.Children().IndexOf(row, rowIndex);
    if (controlIindexFound)
    {
        std::vector<int32_t> selectedCodes = GetSelectedCodesFromStackPanel(parent);

        std::wstring appName;
        if (targetApp != nullptr)
        {
            appName = targetApp.Text().c_str();
        }

        // Validate shortcut element
        validationResult = BufferValidationHelpers::ValidateShortcutBufferElement(rowIndex, colIndex, dropDownIndex, selectedCodes, appName, isHybridControl, shortcutRemapBuffer, dropDownFound);

        // Add or clear unused drop downs
        if (validationResult.second == BufferValidationHelpers::DropDownAction::AddDropDown)
        {
            AddDropDown(table, row, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
        }
        else if (validationResult.second == BufferValidationHelpers::DropDownAction::ClearUnusedDropDowns)
        {
            // remove all the drop downs after the current index (accessible names do not have to be updated since drop downs at the end of the list are getting removed)
            int elementsToBeRemoved = parent.Children().Size() - dropDownIndex - 1;
            for (int i = 0; i < elementsToBeRemoved; i++)
            {
                parent.Children().RemoveAtEnd();
                keyDropDownControlObjects.erase(keyDropDownControlObjects.end() - 1);
            }
        }

        // If ignore key to shortcut warning flag is true and it is a hybrid control in SingleKeyRemapControl, then skip MapToSameKey error
        if (isHybridControl && isSingleKeyWindow && ignoreKeyToShortcutWarning && (validationResult.first == ShortcutErrorType::MapToSameKey))
        {
            validationResult.first = ShortcutErrorType::NoError;
        }

        // If the remapping is invalid display an error message
        if (validationResult.first != ShortcutErrorType::NoError)
        {
            SetDropDownError(currentDropDown, KeyboardManagerEditorStrings::GetErrorMessage(validationResult.first));
        }

        // Handle None case if there are no other errors
        else if (validationResult.second == BufferValidationHelpers::DropDownAction::DeleteDropDown)
        {
            // Update accessible names for drop downs appearing after the deleted one
            for (uint32_t i = dropDownIndex + 1; i < keyDropDownControlObjects.size(); i++)
            {
                // Update accessible name (row index will become i-1 for this element, so the display name would be i (display name indexing from 1)
                SetAccessibleNameForComboBox(keyDropDownControlObjects[i]->GetComboBox(), i);
            }

            parent.Children().RemoveAt(dropDownIndex);
            
            // delete drop down control object from the vector so that it can be destructed
            keyDropDownControlObjects.erase(keyDropDownControlObjects.begin() + dropDownIndex);
        }
    }

    // Reset ignoreKeyToShortcutWarning
    if (ignoreKeyToShortcutWarning)
    {
        ignoreKeyToShortcutWarning = false;
    }

    return std::make_pair(validationResult.first, rowIndex);
}

// Function to set selection handler for shortcut drop down. Needs to be called after the constructor since the shortcutControl StackPanel is null if called in the constructor
void KeyDropDownControl::SetSelectionHandler(StackPanel& table, StackPanel row, VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox& targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    auto onSelectionChange = [&, table, row, colIndex, parent, targetApp, isHybridControl, isSingleKeyWindow](winrt::Windows::Foundation::IInspectable const& sender) {
        std::pair<ShortcutErrorType, int> validationResult = ValidateShortcutSelection(table, row, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);

        // Check if the drop down row index was identified from the return value of validateSelection
        if (validationResult.second != -1)
        {
            // If an error occurred
            if (validationResult.first != ShortcutErrorType::NoError)
            {
                // Validate all the drop downs
                ValidateShortcutFromDropDownList(table, row, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
            }

            // Reset the buffer based on the new selected drop down items. Use static key code list since the KeyDropDownControl object might be deleted
            std::vector<int32_t> selectedKeyCodes = GetSelectedCodesFromStackPanel(parent);
            if (!isHybridControl)
            {
                std::get<Shortcut>(shortcutRemapBuffer[validationResult.second].first[colIndex]).SetKeyCodes(selectedKeyCodes);
            }
            else
            {
                // If exactly one key is selected consider it to be a key remap
                if (GetNumberOfSelectedKeys(selectedKeyCodes) == 1)
                {
                    shortcutRemapBuffer[validationResult.second].first[colIndex] = (DWORD)selectedKeyCodes[0];
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
                std::wstring lowercaseDefAppName = KeyboardManagerEditorStrings::DefaultAppName();
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

// Function to return the combo box element of the drop down
ComboBox KeyDropDownControl::GetComboBox()
{
    return dropDown.as<ComboBox>();
}

// Function to add a drop down to the shortcut stack panel
void KeyDropDownControl::AddDropDown(StackPanel& table, StackPanel row, VariableSizedWrapGrid parent, const int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, bool ignoreWarning)
{
    keyDropDownControlObjects.emplace_back(std::make_unique<KeyDropDownControl>(true, ignoreWarning, colIndex == 1));
    parent.Children().Append(keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->GetComboBox());
    uint32_t index;
    bool found = table.Children().IndexOf(row, index);
    keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->SetSelectionHandler(table, row, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);

    // Update accessible name
    SetAccessibleNameForComboBox(keyDropDownControlObjects[keyDropDownControlObjects.size() - 1]->GetComboBox(), static_cast<int>(keyDropDownControlObjects.size()));
}

// Function to get the list of key codes from the shortcut combo box stack panel
std::vector<int32_t> KeyDropDownControl::GetSelectedCodesFromStackPanel(VariableSizedWrapGrid parent)
{
    std::vector<int32_t> selectedKeyCodes;

    // Get selected indices for each drop down
    for (int i = 0; i < (int)parent.Children().Size(); i++)
    {
        ComboBox ItDropDown = parent.Children().GetAt(i).as<ComboBox>();
        selectedKeyCodes.push_back(GetSelectedValue(ItDropDown));
    }

    return selectedKeyCodes;
}

// Function for validating the selection of shortcuts for all the associated drop downs
void KeyDropDownControl::ValidateShortcutFromDropDownList(StackPanel table, StackPanel row, VariableSizedWrapGrid parent, int colIndex, RemapBuffer& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    // Iterate over all drop downs from left to right in that row/col and validate if there is an error in any of the drop downs. After this the state should be error-free (if it is a valid shortcut)
    for (int i = 0; i < keyDropDownControlObjects.size(); i++)
    {
        // Check for errors only if the current selection is a valid shortcut
        std::vector<int32_t> selectedKeyCodes = GetSelectedCodesFromStackPanel(parent);
        KeyShortcutUnion currentShortcut;
        if (GetNumberOfSelectedKeys(selectedKeyCodes) == 1 && isHybridControl)
        {
            currentShortcut = (DWORD)selectedKeyCodes[0];
        }
        else
        {
            Shortcut temp;
            temp.SetKeyCodes(selectedKeyCodes);
            currentShortcut = temp;
        }

        // If the key/shortcut is valid and that drop down is not empty
        if (((currentShortcut.index() == 0 && std::get<DWORD>(currentShortcut) != NULL) || (currentShortcut.index() == 1 && EditorHelpers::IsValidShortcut(std::get<Shortcut>(currentShortcut)))) && GetSelectedValue(keyDropDownControlObjects[i]->GetComboBox()) != -1)
        {
            keyDropDownControlObjects[i]->ValidateShortcutSelection(table, row, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow);
        }
    }
}

// Function to set the warning message
void KeyDropDownControl::SetDropDownError(ComboBox currentDropDown, hstring message)
{
    currentDropDown.SelectedIndex(-1);
    warningMessage.as<TextBlock>().Text(message);
    try
    {
        currentDropDown.ContextFlyout().ShowAttachedFlyout((FrameworkElement)dropDown.as<ComboBox>());
    }
    catch (winrt::hresult_error const&)
    {
        // If it's loading and some remaps are invalid from previous configs, avoid crashing when flyouts can't be showed yet.
        Logger::error(L"Failed to show dropdown error flyout: {}", message);
    }
}

// Function to add a shortcut to the UI control as combo boxes
void KeyDropDownControl::AddShortcutToControl(Shortcut shortcut, StackPanel table, VariableSizedWrapGrid parent, KBMEditor::KeyboardManagerState& keyboardManagerState, const int colIndex, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, RemapBuffer& remapBuffer, StackPanel row, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow)
{
    // Delete the existing drop down menus
    parent.Children().Clear();
    
    // Remove references to the old drop down objects to destroy them
    keyDropDownControlObjects.clear();
    std::vector<DWORD> shortcutKeyCodes = shortcut.GetKeyCodes();
    if (shortcutKeyCodes.size() != 0)
    {
        bool ignoreWarning = false;

        // If more than one key is to be added, ignore a shortcut to key warning on partially entering the remapping
        if (shortcutKeyCodes.size() > 1)
        {
            ignoreWarning = true;
        }

        KeyDropDownControl::AddDropDown(table, row, parent, colIndex, remapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, isSingleKeyWindow, ignoreWarning);

        for (int i = 0; i < shortcutKeyCodes.size(); i++)
        {
            // New drop down gets added automatically when the SelectedValue(key code) is set
            if (i < (int)parent.Children().Size())
            {
                ComboBox currentDropDown = parent.Children().GetAt(i).as<ComboBox>();
                currentDropDown.SelectedValue(winrt::box_value(std::to_wstring(shortcutKeyCodes[i])));
            }
        }
    }
}

// Disable 26497 this function should be evaluated at compile time
#pragma warning(push)
#pragma warning(disable : 26497)
// Get number of selected keys. Do not count -1 and 0 values as they stand for Not selected and None
int KeyDropDownControl::GetNumberOfSelectedKeys(std::vector<int32_t> keyCodes)
{
    return (int)std::count_if(keyCodes.begin(), keyCodes.end(), [](int32_t a) { return a != -1 && a != 0; });
}
#pragma warning(pop)