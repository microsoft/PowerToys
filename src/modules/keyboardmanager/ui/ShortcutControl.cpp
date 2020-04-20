#include "pch.h"
#include "ShortcutControl.h"
#include "KeyDropDownControl.h"

//Both static members are initialized to null
HWND ShortcutControl::EditShortcutsWindowHandle = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;
// Initialized as new vector
std::vector<std::vector<Shortcut>> ShortcutControl::shortcutRemapBuffer;

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
void ShortcutControl::AddNewShortcutControlRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, Shortcut originalKeys, Shortcut newKeys)
{
    // Parent element for the row
    Windows::UI::Xaml::Controls::StackPanel tableRow;
    tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableRow.Spacing(100);
    tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // Create new ShortcutControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<ShortcutControl>> newrow;
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(shortcutRemapBuffer.size(), 0))));
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(shortcutRemapBuffer.size(), 1))));
    keyboardRemapControlObjects.push_back(std::move(newrow));
    // ShortcutControl for the original shortcut
    tableRow.Children().Append(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->getShortcutControl());
    // ShortcutControl for the new shortcut
    tableRow.Children().Append(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->getShortcutControl());

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteShortcut;
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteShortcut.Content(deleteSymbol);
    deleteShortcut.Click([&](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        StackPanel currentRow = sender.as<Button>().Parent().as<StackPanel>();
        uint32_t index;
        parent.Children().IndexOf(currentRow, index);
        parent.Children().RemoveAt(index);
        // delete the row from the buffer. Since first child of the stackpanel is the header, the effective index starts from 1
        shortcutRemapBuffer.erase(shortcutRemapBuffer.begin() + (index - 1));
        // delete the ShortcutControl objects so that they get destructed
        keyboardRemapControlObjects.erase(keyboardRemapControlObjects.begin() + (index - 1));
    });
    tableRow.Children().Append(deleteShortcut);
    tableRow.UpdateLayout();
    parent.Children().Append(tableRow);
    parent.UpdateLayout();

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (originalKeys.IsValidShortcut() && newKeys.IsValidShortcut())
    {
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->AddShortcutToControl(originalKeys, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutDropDownStackPanel, *keyboardManagerState, shortcutRemapBuffer.size() - 1, 0);
        keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->AddShortcutToControl(newKeys, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->shortcutDropDownStackPanel, *keyboardManagerState, shortcutRemapBuffer.size() - 1, 1);
    }
    else
    {
        // Initialize both shortcuts as empty shortcuts
        shortcutRemapBuffer.push_back(std::vector<Shortcut>{ Shortcut(), Shortcut() });
    }
}

// Function to add a shortcut to the shortcut control as combo boxes
void ShortcutControl::AddShortcutToControl(Shortcut& shortcut, StackPanel parent, KeyboardManagerState& keyboardManagerState, const size_t rowIndex, const size_t colIndex)
{
    // Delete the existing drop down menus
    parent.Children().Clear();
    // Remove references to the old drop down objects to destroy them
    keyDropDownControlObjects.clear();

    std::vector<DWORD> shortcutKeyCodes = shortcut.GetKeyCodes();
    std::vector<DWORD> keyCodeList = keyboardManagerState.keyboardMap.GetKeyCodeList(true);
    if (shortcutKeyCodes.size() != 0)
    {
        KeyDropDownControl::AddDropDown(parent, rowIndex, colIndex, shortcutRemapBuffer, keyDropDownControlObjects);
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
void ShortcutControl::createDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, KeyboardManagerState& keyboardManagerState, const size_t rowIndex, const size_t colIndex)
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
    cancelButtonText.Text(L"Cancel");

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);
    // Cancel button
    cancelButton.Click([detectShortcutBox, unregisterKeys, &keyboardManagerState](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
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