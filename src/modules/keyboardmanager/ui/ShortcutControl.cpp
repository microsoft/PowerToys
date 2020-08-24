#include "pch.h"
#include "ShortcutControl.h"
#include "KeyDropDownControl.h"
#include "keyboardmanager/common/KeyboardManagerState.h"
#include "keyboardmanager/common/Helpers.h"
#include "common/common.h"
#include "keyboardmanager/dll/Generated Files/resource.h"
extern "C" IMAGE_DOS_HEADER __ImageBase;

//Both static members are initialized to null
HWND ShortcutControl::EditShortcutsWindowHandle = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;
// Initialized as new vector
RemapBuffer ShortcutControl::shortcutRemapBuffer;

ShortcutControl::ShortcutControl(Grid table, const int colIndex, TextBox targetApp)
{
    shortcutDropDownStackPanel = StackPanel();
    typeShortcut = Button();
    shortcutControlLayout = StackPanel();
    bool isHybridControl = colIndex == 1 ? true : false;

    shortcutDropDownStackPanel.as<StackPanel>().Spacing(KeyboardManagerConstants::ShortcutTableDropDownSpacing);
    shortcutDropDownStackPanel.as<StackPanel>().Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    typeShortcut.as<Button>().Content(winrt::box_value(GET_RESOURCE_STRING(IDS_TYPE_BUTTON)));
    typeShortcut.as<Button>().Width(KeyboardManagerConstants::ShortcutTableDropDownWidth);
    typeShortcut.as<Button>().Click([&, table, colIndex, isHybridControl, targetApp](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        keyboardManagerState->SetUIState(KeyboardManagerUIState::DetectShortcutWindowActivated, EditShortcutsWindowHandle);
        // Using the XamlRoot of the typeShortcut to get the root of the XAML host
        createDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState, colIndex, table, keyDropDownControlObjects, shortcutControlLayout.as<StackPanel>(), targetApp, isHybridControl, false, EditShortcutsWindowHandle, shortcutRemapBuffer);
    });

    shortcutControlLayout.as<StackPanel>().Margin({ 0, 0, 0, 10 });
    shortcutControlLayout.as<StackPanel>().Spacing(KeyboardManagerConstants::ShortcutTableDropDownSpacing);

    shortcutControlLayout.as<StackPanel>().Children().Append(typeShortcut.as<Button>());
    shortcutControlLayout.as<StackPanel>().Children().Append(shortcutDropDownStackPanel.as<StackPanel>());
    KeyDropDownControl::AddDropDown(table, shortcutControlLayout.as<StackPanel>(), shortcutDropDownStackPanel.as<StackPanel>(), colIndex, shortcutRemapBuffer, keyDropDownControlObjects, targetApp, isHybridControl, false);
    shortcutControlLayout.as<StackPanel>().UpdateLayout();
}

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
void ShortcutControl::AddNewShortcutControlRow(Grid& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, const Shortcut& originalKeys, const std::variant<DWORD, Shortcut>& newKeys, const std::wstring& targetAppName)
{
    // Textbox for target application
    TextBox targetAppTextBox;

    // Create new ShortcutControl objects dynamically so that we does not get destructed
    std::vector<std::unique_ptr<ShortcutControl>> newrow;
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(parent, 0, targetAppTextBox))));
    newrow.push_back(std::move(std::unique_ptr<ShortcutControl>(new ShortcutControl(parent, 1, targetAppTextBox))));
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
    arrowIcon.HorizontalAlignment(HorizontalAlignment::Center);
    parent.SetColumn(arrowIcon, KeyboardManagerConstants::ShortcutTableArrowColIndex);
    parent.SetRow(arrowIcon, parent.RowDefinitions().Size() - 1);
    parent.Children().Append(arrowIcon);

    // ShortcutControl for the new shortcut
    parent.Children().Append(keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->getShortcutControl());

    targetAppTextBox.Width(KeyboardManagerConstants::ShortcutTableDropDownWidth);
    targetAppTextBox.Margin({ 0, 0, 0, KeyboardManagerConstants::ShortcutTableDropDownSpacing });
    targetAppTextBox.VerticalAlignment(VerticalAlignment::Bottom);
    targetAppTextBox.HorizontalAlignment(HorizontalAlignment::Center);
    targetAppTextBox.PlaceholderText(KeyboardManagerConstants::DefaultAppName);
    targetAppTextBox.Text(targetAppName);

    // LostFocus handler will be called whenever text is updated by a user and then they click something else or tab to another control. Does not get called if Text is updated while the TextBox isn't in focus (i.e. from code)
    targetAppTextBox.LostFocus([&keyboardRemapControlObjects, parent, targetAppTextBox](auto const& sender, auto const& e) {
        // Get index of targetAppTextBox button
        UIElementCollection children = parent.Children();
        uint32_t index;
        children.IndexOf(targetAppTextBox, index);
        uint32_t lastIndexInRow = index + ((KeyboardManagerConstants::ShortcutTableColCount - 1) - KeyboardManagerConstants::ShortcutTableTargetAppColIndex);
        // Calculate row index in the buffer from the grid child index (first set of children are header elements and then three children in each row)
        int rowIndex = (lastIndexInRow - KeyboardManagerConstants::ShortcutTableHeaderCount) / KeyboardManagerConstants::ShortcutTableColCount;

        // Validate both set of drop downs
        KeyDropDownControl::ValidateShortcutFromDropDownList(parent, keyboardRemapControlObjects[rowIndex][0]->getShortcutControl(), keyboardRemapControlObjects[rowIndex][0]->shortcutDropDownStackPanel.as<StackPanel>(), 0, ShortcutControl::shortcutRemapBuffer, keyboardRemapControlObjects[rowIndex][0]->keyDropDownControlObjects, targetAppTextBox, false, false);
        KeyDropDownControl::ValidateShortcutFromDropDownList(parent, keyboardRemapControlObjects[rowIndex][1]->getShortcutControl(), keyboardRemapControlObjects[rowIndex][1]->shortcutDropDownStackPanel.as<StackPanel>(), 1, ShortcutControl::shortcutRemapBuffer, keyboardRemapControlObjects[rowIndex][1]->keyDropDownControlObjects, targetAppTextBox, true, false);

        // Reset the buffer based on the selected drop down items
        std::get<Shortcut>(shortcutRemapBuffer[rowIndex].first[0]).SetKeyCodes(KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(KeyDropDownControl::GetSelectedIndicesFromStackPanel(keyboardRemapControlObjects[rowIndex][0]->shortcutDropDownStackPanel.as<StackPanel>()), KeyDropDownControl::keyboardManagerState->keyboardMap.GetKeyCodeList(true)));
        // second column is a hybrid column
        std::vector<DWORD> selectedKeyCodes = KeyboardManagerHelper::GetKeyCodesFromSelectedIndices(KeyDropDownControl::GetSelectedIndicesFromStackPanel(keyboardRemapControlObjects[rowIndex][1]->shortcutDropDownStackPanel.as<StackPanel>()), KeyDropDownControl::keyboardManagerState->keyboardMap.GetKeyCodeList(true));

        // If exactly one key is selected consider it to be a key remap
        if (selectedKeyCodes.size() == 1)
        {
            shortcutRemapBuffer[rowIndex].first[1] = selectedKeyCodes[0];
        }
        else
        {
            Shortcut tempShortcut;
            tempShortcut.SetKeyCodes(selectedKeyCodes);
            // Assign instead of setting the value in the buffer since the previous value may not be a Shortcut
            shortcutRemapBuffer[rowIndex].first[1] = tempShortcut;
        }
        std::wstring newText = targetAppTextBox.Text().c_str();
        std::wstring lowercaseDefAppName = KeyboardManagerConstants::DefaultAppName;
        std::transform(newText.begin(), newText.end(), newText.begin(), towlower);
        std::transform(lowercaseDefAppName.begin(), lowercaseDefAppName.end(), lowercaseDefAppName.begin(), towlower);
        if (newText == lowercaseDefAppName)
        {
            shortcutRemapBuffer[rowIndex].second = L"";
        }
        else
        {
            shortcutRemapBuffer[rowIndex].second = targetAppTextBox.Text().c_str();
        }
    });

    parent.SetColumn(targetAppTextBox, KeyboardManagerConstants::ShortcutTableTargetAppColIndex);
    parent.SetRow(targetAppTextBox, parent.RowDefinitions().Size() - 1);
    parent.Children().Append(targetAppTextBox);

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteShortcut;
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteShortcut.Content(deleteSymbol);
    deleteShortcut.Background(Media::SolidColorBrush(Colors::Transparent()));
    deleteShortcut.HorizontalAlignment(HorizontalAlignment::Center);
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

        // Calculate row index in the buffer from the grid child index (first set of children are header elements and then three children in each row)
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
    parent.UpdateLayout();

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (originalKeys.IsValidShortcut() && !(newKeys.index() == 0 && std::get<DWORD>(newKeys) == NULL) && !(newKeys.index() == 1 && !std::get<Shortcut>(newKeys).IsValidShortcut()))
    {
        // change to load app name
        shortcutRemapBuffer.push_back(std::make_pair<RemapBufferItem, std::wstring>(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring(targetAppName)));
        KeyDropDownControl::AddShortcutToControl(originalKeys, parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutDropDownStackPanel.as<StackPanel>(), *keyboardManagerState, 0, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->keyDropDownControlObjects, shortcutRemapBuffer, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][0]->shortcutControlLayout.as<StackPanel>(), targetAppTextBox, false, false);

        if (newKeys.index() == 0)
        {
            std::vector<DWORD> shortcutListKeyCodes = keyboardManagerState->keyboardMap.GetKeyCodeList(true);
            auto it = std::find(shortcutListKeyCodes.begin(), shortcutListKeyCodes.end(), std::get<DWORD>(newKeys));
            if (it != shortcutListKeyCodes.end())
            {
                keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects[0]->SetSelectedIndex((int32_t)std::distance(shortcutListKeyCodes.begin(), it));
            }
        }
        else
        {
            KeyDropDownControl::AddShortcutToControl(std::get<Shortcut>(newKeys), parent, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->shortcutDropDownStackPanel.as<StackPanel>(), *keyboardManagerState, 1, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->keyDropDownControlObjects, shortcutRemapBuffer, keyboardRemapControlObjects[keyboardRemapControlObjects.size() - 1][1]->shortcutControlLayout.as<StackPanel>(), targetAppTextBox, true, false);
        }
    }
    else
    {
        // Initialize both shortcuts as empty shortcuts
        shortcutRemapBuffer.push_back(std::make_pair<RemapBufferItem, std::wstring>(RemapBufferItem{ Shortcut(), Shortcut() }, std::wstring(targetAppName)));
    }
}

// Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel ShortcutControl::getShortcutControl()
{
    return shortcutControlLayout.as<StackPanel>();
}

// Function to create the detect shortcut UI window
void ShortcutControl::createDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState, const int colIndex, Grid table, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, StackPanel controlLayout, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, HWND parentWindow, RemapBuffer& remapBuffer)
{
    // ContentDialog for detecting shortcuts. This is the parent UI element.
    ContentDialog detectShortcutBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(GET_RESOURCE_STRING(IDS_TYPESHORTCUT_TITLE)));
    detectShortcutBox.IsPrimaryButtonEnabled(false);
    detectShortcutBox.IsSecondaryButtonEnabled(false);

    // Get the linked stack panel for the "Type shortcut" button that was clicked
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

    auto onPressEnter = [linkedShortcutStackPanel,
                         detectShortcutBox,
                         &keyboardManagerState,
                         unregisterKeys,
                         colIndex,
                         table,
                         targetApp,
                         &keyDropDownControlObjects,
                         controlLayout,
                         isHybridControl,
                         isSingleKeyWindow,
                         &remapBuffer] {
        // Save the detected shortcut in the linked text block
        Shortcut detectedShortcutKeys = keyboardManagerState.GetDetectedShortcut();

        if (!detectedShortcutKeys.IsEmpty())
        {
            // The shortcut buffer gets set in this function
            KeyDropDownControl::AddShortcutToControl(detectedShortcutKeys, table, linkedShortcutStackPanel, keyboardManagerState, colIndex, keyDropDownControlObjects, remapBuffer, controlLayout, targetApp, isHybridControl, isSingleKeyWindow);
        }
        // Hide the type shortcut UI
        detectShortcutBox.Hide();
    };

    auto onReleaseEnter = [&keyboardManagerState,
                           unregisterKeys,
                           isSingleKeyWindow,
                           parentWindow] {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        if (isSingleKeyWindow)
        {
            // Revert UI state back to Edit Keyboard window
            keyboardManagerState.SetUIState(KeyboardManagerUIState::EditKeyboardWindowActivated, parentWindow);
        }
        else
        {
            // Revert UI state back to Edit Shortcut window
            keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, parentWindow);
        }

        unregisterKeys();
    };

    auto onAccept = [onPressEnter,
                     onReleaseEnter] {
        onPressEnter();
        onReleaseEnter();
    };

    TextBlock primaryButtonText;
    primaryButtonText.Text(GET_RESOURCE_STRING(IDS_OK_BUTTON));

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
        [primaryButton, onPressEnter, detectShortcutBox](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [primaryButton, onPressEnter] {
                    // Use the base medium low brush to be consistent with the theme
                    primaryButton.Background(Windows::UI::Xaml::Application::Current().Resources().Lookup(box_value(L"SystemControlBackgroundBaseMediumLowBrush")).as<Windows::UI::Xaml::Media::SolidColorBrush>());
                    onPressEnter();
                });
        },
        [onReleaseEnter](DWORD) {
            onReleaseEnter();
        });

    TextBlock cancelButtonText;
    cancelButtonText.Text(GET_RESOURCE_STRING(IDS_CANCEL_BUTTON));

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);
    // Cancel button
    cancelButton.Click([detectShortcutBox, unregisterKeys, &keyboardManagerState, isSingleKeyWindow, parentWindow](winrt::Windows::Foundation::IInspectable const& sender, RoutedEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        if (isSingleKeyWindow)
        {
            // Revert UI state back to Edit Keyboard window
            keyboardManagerState.SetUIState(KeyboardManagerUIState::EditKeyboardWindowActivated, parentWindow);
        }
        else
        {
            // Revert UI state back to Edit Shortcut window
            keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, parentWindow);
        }
        unregisterKeys();
        detectShortcutBox.Hide();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_ESCAPE,
        selectDetectedShortcutAndResetKeys,
        [&keyboardManagerState, detectShortcutBox, unregisterKeys, isSingleKeyWindow, parentWindow](DWORD) {
            detectShortcutBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [detectShortcutBox] {
                    detectShortcutBox.Hide();
                });

            keyboardManagerState.ResetUIState();
            if (isSingleKeyWindow)
            {
                // Revert UI state back to Edit Keyboard window
                keyboardManagerState.SetUIState(KeyboardManagerUIState::EditKeyboardWindowActivated, parentWindow);
            }
            else
            {
                // Revert UI state back to Edit Shortcut window
                keyboardManagerState.SetUIState(KeyboardManagerUIState::EditShortcutsWindowActivated, parentWindow);
            }
            unregisterKeys();
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectShortcutBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(GET_RESOURCE_STRING(IDS_TYPESHORTCUT_HEADER));
    text.Margin({ 0, 0, 0, 10 });
    stackPanel.Children().Append(text);

    // Target StackPanel to place the selected key - first row (for 1-3 keys)
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel1;
    keyStackPanel1.Orientation(Orientation::Horizontal);
    stackPanel.Children().Append(keyStackPanel1);

    // Target StackPanel to place the selected key - second row (for 4-5 keys)
    Windows::UI::Xaml::Controls::StackPanel keyStackPanel2;
    keyStackPanel2.Orientation(Orientation::Horizontal);
    keyStackPanel2.Margin({ 0, 20, 0, 0 });
    keyStackPanel2.Visibility(Visibility::Collapsed);
    stackPanel.Children().Append(keyStackPanel2);

    TextBlock holdEscInfo;
    holdEscInfo.Text(GET_RESOURCE_STRING(IDS_TYPE_HOLDESC));
    holdEscInfo.FontSize(12);
    holdEscInfo.Margin({ 0, 20, 0, 0 });
    stackPanel.Children().Append(holdEscInfo);

    TextBlock holdEnterInfo;
    holdEnterInfo.Text(GET_RESOURCE_STRING(IDS_TYPE_HOLDENTER));
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
    keyboardManagerState.ConfigureDetectShortcutUI(keyStackPanel1, keyStackPanel2);

    // Show the dialog
    detectShortcutBox.ShowAsync();
}
