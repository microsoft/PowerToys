#include "pch.h"
#include "ShortcutControl.h"

//Both static members are initialized to null
HWND ShortcutControl::EditShortcutsWindowHandle = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;
// Initialized as new vector
std::vector<std::vector<Shortcut>> ShortcutControl::shortcutRemapBuffer;

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
void ShortcutControl::AddNewShortcutControlRow(StackPanel& parent, Shortcut originalKeys, Shortcut newKeys)
{
    // Parent element for the row
    Windows::UI::Xaml::Controls::StackPanel tableRow;
    tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableRow.Spacing(100);
    tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // ShortcutControl for the original shortcut
    ShortcutControl originalSC((int)shortcutRemapBuffer.size(), 0);
    tableRow.Children().Append(originalSC.getShortcutControl());

    // ShortcutControl for the new shortcut
    ShortcutControl newSC((int)shortcutRemapBuffer.size(), 1);
    tableRow.Children().Append(newSC.getShortcutControl());

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (originalKeys.IsValidShortcut() && newKeys.IsValidShortcut())
    {
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
        originalSC.AddShortcutToControl(originalKeys, originalSC.shortcutDropDownStackPanel, *keyboardManagerState, (int)shortcutRemapBuffer.size() - 1, 0);
        newSC.AddShortcutToControl(newKeys, newSC.shortcutDropDownStackPanel, *keyboardManagerState, (int)shortcutRemapBuffer.size() - 1, 1);
    }
    else
    {
        // Initialize both shortcuts as empty shortcuts
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
    }

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteShortcut;
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteShortcut.Content(deleteSymbol);
    deleteShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        StackPanel currentRow = sender.as<Button>().Parent().as<StackPanel>();
        uint32_t index;
        parent.Children().IndexOf(currentRow, index);
        parent.Children().RemoveAt(index);
        // delete the row from the buffer. Since first child of the stackpanel is the header, the effective index starts from 1
        shortcutRemapBuffer.erase(shortcutRemapBuffer.begin() + (index - 1));
    });
    tableRow.Children().Append(deleteShortcut);
    parent.Children().Append(tableRow);
}

// Function to add a drop down to the shortcut stack panel
ComboBox ShortcutControl::AddDropDown(StackPanel parent, const int& rowIndex, const int& colIndex)
{
    ComboBox shortcutDropDown;
    shortcutDropDown.Width(100);
    shortcutDropDown.MaxDropDownHeight(200);
    shortcutDropDown.ItemsSource(keyboardManagerState->keyboardMap.GetKeyList(true).first);
    // Flyout to display the warning on the drop down element
    Flyout warningFlyout;
    TextBlock warningMessage;
    warningFlyout.Content(warningMessage);
    shortcutDropDown.ContextFlyout().SetAttachedFlyout((FrameworkElement)shortcutDropDown, warningFlyout);

    // drop down selection handler
    shortcutDropDown.SelectionChanged([&, rowIndex, colIndex, parent, warningMessage](IInspectable const& sender, SelectionChangedEventArgs const&) {
        ComboBox currentDropDown = sender.as<ComboBox>();
        std::vector<DWORD> keyCodeList = keyboardManagerState->keyboardMap.GetKeyList(true).second;
        int selectedKeyIndex = currentDropDown.SelectedIndex();
        uint32_t dropDownIndex = -1;
        bool dropDownFound = parent.Children().IndexOf(currentDropDown, dropDownIndex);

        if (selectedKeyIndex != -1 && keyCodeList.size() > selectedKeyIndex && dropDownFound)
        {
            // If only 1 drop down and action key is chosen: Warn that a modifier must be chosen
            if (parent.Children().Size() == 1 && !IsModifierKey(keyCodeList[selectedKeyIndex]))
            {
                // warn and reset the drop down
                SetDropDownError(currentDropDown, warningMessage, L"Shortcut must start with a modifier key");
            }
            // If it is the last drop down
            else if (dropDownIndex == parent.Children().Size() - 1)
            {
                // If last drop down and a modifier is selected: add a new drop down (max of 5 drop downs should be enforced)
                if (IsModifierKey(keyCodeList[selectedKeyIndex]) && parent.Children().Size() < 5)
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
                        AddDropDown(parent, rowIndex, colIndex);
                    }
                }
                // If last drop down and a modifier is selected but there are already 5 drop downs: warn the user
                else if (IsModifierKey(keyCodeList[selectedKeyIndex]) && parent.Children().Size() >= 5)
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
                if (IsModifierKey(keyCodeList[selectedKeyIndex]))
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

    parent.Children().Append(shortcutDropDown);
    parent.UpdateLayout();

    return shortcutDropDown;
}

// Function to add a shortcut to the shortcut control as combo boxes
void ShortcutControl::AddShortcutToControl(Shortcut& shortcut, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int& rowIndex, const int& colIndex)
{
    parent.Children().Clear();
    std::vector<DWORD> shortcutKeyCodes = shortcut.GetKeyCodes();
    std::vector<DWORD> keyCodeList = keyboardManagerState.keyboardMap.GetKeyList(true).second;
    if (shortcutKeyCodes.size() != 0)
    {
        ComboBox firstDropDown = AddDropDown(parent, rowIndex, colIndex);
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

// Function to get the list of key codes from the shortcut combo box stack panel
std::vector<DWORD> ShortcutControl::GetKeysFromStackPanel(StackPanel parent)
{
    std::vector<DWORD> keys;
    std::vector<DWORD> keyCodeList = keyboardManagerState->keyboardMap.GetKeyList(true).second;
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
bool ShortcutControl::CheckRepeatedModifier(StackPanel parent, uint32_t dropDownIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList)
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
void ShortcutControl::SetDropDownError(ComboBox dropDown, TextBlock messageBlock, hstring message)
{
    messageBlock.Text(message);
    dropDown.ContextFlyout().ShowAttachedFlyout((FrameworkElement)dropDown);
    dropDown.SelectedIndex(-1);
}

// Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel ShortcutControl::getShortcutControl()
{
    return shortcutControlLayout;
}

// Function to create the detect shortcut UI window
void ShortcutControl::createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, KeyboardManagerState& keyboardManagerState, const int& rowIndex, const int& colIndex)
{
    // ContentDialog for detecting shortcuts. This is the parent UI element.
    ContentDialog detectShortcutBox;

    // TODO: Hardcoded light theme, since the app is not theme aware ATM.
    detectShortcutBox.RequestedTheme(ElementTheme::Light);
    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
    detectShortcutBox.IsPrimaryButtonEnabled(false);
    detectShortcutBox.IsSecondaryButtonEnabled(false);

    // Get the linked text block for the "Type shortcut" button that was clicked
    StackPanel linkedShortcutStackPanel = getSiblingElement(sender).as<StackPanel>();

    auto unregisterKeys = [&keyboardManagerState]() {
        std::thread t1(&KeyboardManagerState::UnregisterKeyDelay, &keyboardManagerState, VK_ESCAPE);
        std::thread t2(&KeyboardManagerState::UnregisterKeyDelay, &keyboardManagerState, VK_RETURN);
        t1.detach();
        t2.detach();
    };

    auto selectDetectedShortcutAndResetKeys = [&keyboardManagerState](DWORD key) {
        keyboardManagerState.SelectDetectedShortcut(key);
        keyboardManagerState.ResetDetectedShortcutKey(key);
    };

    auto onAccept = [this,
                     linkedShortcutStackPanel,
                     detectShortcutBox,
                     &keyboardManagerState,
                     &shortcutRemapBuffer,
                     unregisterKeys,
                     rowIndex,
                     colIndex] {
        // Save the detected shortcut in the linked text block
        Shortcut detectedShortcutKeys = keyboardManagerState.GetDetectedShortcut();

        if (!detectedShortcutKeys.IsEmpty())
        {
            // The shortcut buffer gets set in this function
            AddShortcutToControl(detectedShortcutKeys, linkedShortcutStackPanel, keyboardManagerState, rowIndex, colIndex);
        }

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        unregisterKeys();
        detectShortcutBox.Hide();
    };

    TextBlock primaryButtonText;
    primaryButtonText.Text(to_hstring(L"OK"));

    Button primaryButton;
    primaryButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    primaryButton.Margin({ 2, 2, 2, 2 });
    primaryButton.Content(primaryButtonText);

    // OK button
    primaryButton.Click([onAccept](IInspectable const& sender, RoutedEventArgs const&) {
        onAccept();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        selectDetectedShortcutAndResetKeys,
        [primaryButton, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [primaryButton] {
                    primaryButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::DarkGray() });
                });
        },
        [onAccept, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [onAccept] {
                    onAccept();
                });
        });

    TextBlock cancelButtonText;
    cancelButtonText.Text(to_hstring(L"Cancel"));

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);
    // Cancel button
    cancelButton.Click([detectShortcutBox, unregisterKeys, &keyboardManagerState](IInspectable const& sender, RoutedEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        unregisterKeys();
        detectShortcutBox.Hide();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_ESCAPE,
        selectDetectedShortcutAndResetKeys,
        [&keyboardManagerState, detectShortcutBox, unregisterKeys](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [detectShortcutBox] {
                    detectShortcutBox.Hide();
                });

            keyboardManagerState.ResetUIState();
            unregisterKeys();
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectShortcutBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(winrt::to_hstring("Keys Pressed:"));
    text.Margin({ 0, 0, 0, 10 });
    stackPanel.Children().Append(text);

    // Target StackPanel to place the selected key
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel;
    keyStackPanel.Orientation(Orientation::Horizontal);
    stackPanel.Children().Append(keyStackPanel);

    TextBlock holdEscInfo;
    holdEscInfo.Text(winrt::to_hstring("Hold Esc to discard"));
    holdEscInfo.FontSize(12);
    holdEscInfo.Margin({ 0, 20, 0, 0 });
    stackPanel.Children().Append(holdEscInfo);

    TextBlock holdEnterInfo;
    holdEnterInfo.Text(winrt::to_hstring("Hold Enter to apply"));
    holdEnterInfo.FontSize(12);
    holdEnterInfo.Margin({ 0, 0, 0, 0 });
    stackPanel.Children().Append(holdEnterInfo);

    ColumnDefinition primaryButtonColumn;
    ColumnDefinition cancelButtonColumn;

    Grid buttonPanel;
    buttonPanel.Margin({ 0, 20, 0, 0 });
    buttonPanel.HorizontalAlignment(HorizontalAlignment::Stretch);
    buttonPanel.ColumnDefinitions().Append(primaryButtonColumn);
    buttonPanel.ColumnDefinitions().Append(cancelButtonColumn);
    buttonPanel.SetColumn(primaryButton, 0);
    buttonPanel.SetColumn(cancelButton, 1);

    buttonPanel.Children().Append(primaryButton);
    buttonPanel.Children().Append(cancelButton);

    stackPanel.Children().Append(buttonPanel);
    stackPanel.UpdateLayout();

    // Configure the keyboardManagerState to store the UI information.
    keyboardManagerState.ConfigureDetectShortcutUI(keyStackPanel);

    // Show the dialog
    detectShortcutBox.ShowAsync();
}