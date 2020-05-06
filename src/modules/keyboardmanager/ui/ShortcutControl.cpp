#include "pch.h"
#include "ShortcutControl.h"
#include "KeyDropDownControl.h"

//Both static members are initialized to null
HWND ShortcutControl::EditShortcutsWindowHandle = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;
// Initialized as new vector
std::vector<std::vector<Shortcut>> ShortcutControl::shortcutRemapBuffer;

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
void ShortcutControl::AddNewShortcutControlRow(Grid& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, Shortcut originalKeys, Shortcut newKeys)
{
    // Warning icon for the row
    ToolTip warningMessage;
    FontIcon warningIcon;
    warningIcon.Visibility(Visibility::Collapsed);
    warningMessage.Content(box_value(KeyboardManagerConstants::ToolTipInitialContent));

    // Create new ShortcutControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<ShortcutControl>> newrow;
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(parent, 0, warningIcon, warningMessage))));
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(parent, 1, warningIcon, warningMessage))));
    keyboardRemapControlObjects.push_back(std::move(newrow));

    // Add to grid
    parent.RowDefinitions().Append(RowDefinition());
    parent.SetColumn(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->getShortcutControl(), KeyboardManagerConstants::ShortcutTableOriginalColIndex);
    parent.SetRow(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->getShortcutControl(), parent.RowDefinitions().Size() - 1);
    parent.SetColumn(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->getShortcutControl(), KeyboardManagerConstants::ShortcutTableNewColIndex);
    parent.SetRow(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->getShortcutControl(), parent.RowDefinitions().Size() - 1);
    // ShortcutControl for the original shortcut
    parent.Children().Append(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->getShortcutControl());

    // Arrow icon
    FontIcon arrowIcon;
    arrowIcon.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    arrowIcon.Glyph(L"\xE72A");
    arrowIcon.VerticalAlignment(VerticalAlignment::Center);
    arrowIcon.HorizontalAlignment(HorizontalAlignment::Left);
    parent.SetColumn(arrowIcon, KeyboardManagerConstants::ShortcutTableArrowColIndex);
    parent.SetRow(arrowIcon, parent.RowDefinitions().Size() - 1);
    parent.Children().Append(arrowIcon);

    // ShortcutControl for the new shortcut
    parent.Children().Append(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->getShortcutControl());

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteShortcut;
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteShortcut.Content(deleteSymbol);
    deleteShortcut.Background(Media::SolidColorBrush(Colors::Transparent()));
    deleteShortcut.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        Button currentButton = sender.as<Button>();
        uint32_t index;
        // Get index of delete button
        UIElementCollection children = parent.Children();
        children.IndexOf(currentButton, index);
        uint32_t lastIndexInRow = index + ((KeyboardManagerConstants::ShortcutTableColCount - 1) - KeyboardManagerConstants::ShortcutTableRemoveColIndex);
        // Change the row index of elements appearing after the current row, as we will delete the row definition
        for (uint32_t i = lastIndexInRow + 1; i < children.Size(); i++)
        {
            int32_t elementRowIndex = parent.GetRow(children.GetAt(i).as<FrameworkElement>());
            parent.SetRow(children.GetAt(i).as<FrameworkElement>(), elementRowIndex - 1);
        }

        for (int i = 0; i < KeyboardManagerConstants::ShortcutTableColCount; i++)
        {
            parent.Children().RemoveAt(lastIndexInRow - i);
        }

        // Calculate row index in the buffer from the grid child index (first two children are header elements and then three children in each row)
        int bufferIndex = (lastIndexInRow - KeyboardManagerConstants::ShortcutTableHeaderCount) / KeyboardManagerConstants::ShortcutTableColCount;
        // Delete the row definition
        parent.RowDefinitions().RemoveAt(bufferIndex + 1);
        // delete the row from the buffer
        shortcutRemapBuffer.erase(shortcutRemapBuffer.begin() + bufferIndex);
        // delete the ShortcutControl objects so that they get destructed
        keyboardRemapControlObjects.erase(keyboardRemapControlObjects.begin() + bufferIndex);
    });
    parent.SetColumn(deleteShortcut, KeyboardManagerConstants::ShortcutTableRemoveColIndex);
    parent.SetRow(deleteShortcut, parent.RowDefinitions().Size() - 1);
    parent.Children().Append(deleteShortcut);

    warningIcon.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    warningIcon.Glyph(L"\xE783");
    warningIcon.HorizontalAlignment(HorizontalAlignment::Left);
    ToolTipService::SetToolTip(warningIcon, warningMessage);
    parent.SetColumn(warningIcon, KeyboardManagerConstants::ShortcutTableWarningColIndex);
    parent.SetRow(warningIcon, parent.RowDefinitions().Size() - 1);
    parent.Children().Append(warningIcon);
    parent.UpdateLayout();

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (originalKeys.IsValidShortcut() && newKeys.IsValidShortcut())
    {
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->AddShortcutToControl(originalKeys, parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutDropDownStackPanel, *keyboardManagerState, 0, warningIcon, warningMessage);
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->AddShortcutToControl(newKeys, parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->shortcutDropDownStackPanel, *keyboardManagerState, 1, warningIcon, warningMessage);
    }
    else
    {
        // Initialize both shortcuts as empty shortcuts
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
    }
}

// Function to add a shortcut to the shortcut control as combo boxes
void ShortcutControl::AddShortcutToControl(Shortcut& shortcut, Grid table, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int colIndex, FontIcon warning, ToolTip toolTip)
{
    // Delete the existing drop down menus
    parent.Children().Clear();
    // Remove references to the old drop down objects to destroy them
    keyDropDownControlObjects.clear();

    std::vector<DWORD> shortcutKeyCodes = shortcut.GetKeyCodes();
    std::vector<DWORD> keyCodeList = keyboardManagerState.keyboardMap.GetKeyCodeList(true);
    if (shortcutKeyCodes.size() != 0)
    {
        KeyDropDownControl::AddDropDown(table, shortcutControlLayout, parent, colIndex, shortcutRemapBuffer, keyDropDownControlObjects, warning, toolTip);
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

// Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel ShortcutControl::getShortcutControl()
{
    return shortcutControlLayout;
}

// Function to create the detect shortcut UI window
void ShortcutControl::createDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, KeyboardManagerState& keyboardManagerState, const int colIndex, Grid table, FontIcon warning, ToolTip toolTip)
{
    // ContentDialog for detecting shortcuts. This is the parent UI element.
    ContentDialog detectShortcutBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
    detectShortcutBox.IsPrimaryButtonEnabled(false);
    detectShortcutBox.IsSecondaryButtonEnabled(false);

    // Get the linked text block for the "Type shortcut" button that was clicked
    StackPanel linkedShortcutStackPanel = KeyboardManagerHelper::getSiblingElement(sender).as<StackPanel>();

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
                     colIndex,
                     table,
                     warning,
                     toolTip] {
        // Save the detected shortcut in the linked text block
        Shortcut detectedShortcutKeys = keyboardManagerState.GetDetectedShortcut();

        if (!detectedShortcutKeys.IsEmpty())
        {
            // The shortcut buffer gets set in this function
            AddShortcutToControl(detectedShortcutKeys, table, linkedShortcutStackPanel, keyboardManagerState, colIndex, warning, toolTip);
        }

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        // Revert UI state back to Edit Shortcut window
        keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, EditShortcutsWindowHandle);
        unregisterKeys();
        detectShortcutBox.Hide();
    };

    TextBlock primaryButtonText;
    primaryButtonText.Text(L"OK");

    Button primaryButton;
    primaryButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    primaryButton.Margin({ 2, 2, 2, 2 });
    primaryButton.Content(primaryButtonText);

    // OK button
    primaryButton.Click([onAccept](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        onAccept();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        selectDetectedShortcutAndResetKeys,
        [primaryButton, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [primaryButton] {
                    // Use the base medium low brush to be consistent with the theme
                    primaryButton.Background(Windows::UI::Xaml::Application::Current().Resources().Lookup(box_value(L"SystemControlBackgroundBaseMediumLowBrush")).as<Windows::UI::Xaml::Media::SolidColorBrush>());
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
    cancelButtonText.Text(L"Cancel");

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);
    // Cancel button
    cancelButton.Click([detectShortcutBox, unregisterKeys, &keyboardManagerState](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        // Revert UI state back to Edit Shortcut window
        keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, EditShortcutsWindowHandle);
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
            // Revert UI state back to Edit Shortcut window
            keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, EditShortcutsWindowHandle);
            unregisterKeys();
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectShortcutBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(L"Keys Pressed:");
    text.Margin({ 0, 0, 0, 10 });
    stackPanel.Children().Append(text);

    // Target StackPanel to place the selected key
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel;
    keyStackPanel.Orientation(Orientation::Horizontal);
    stackPanel.Children().Append(keyStackPanel);

    TextBlock holdEscInfo;
    holdEscInfo.Text(L"Hold Esc to discard");
    holdEscInfo.FontSize(12);
    holdEscInfo.Margin({ 0, 20, 0, 0 });
    stackPanel.Children().Append(holdEscInfo);

    TextBlock holdEnterInfo;
    holdEnterInfo.Text(L"Hold Enter to apply");
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