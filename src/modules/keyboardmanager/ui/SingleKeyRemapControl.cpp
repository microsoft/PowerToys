#include "pch.h"
#include "SingleKeyRemapControl.h"

//Both static members are initialized to null
HWND SingleKeyRemapControl::EditKeyboardWindowHandle = nullptr;
KeyboardManagerState* SingleKeyRemapControl::keyboardManagerState = nullptr;
// Initialized as new vector
std::vector<std::vector<DWORD>> SingleKeyRemapControl::singleKeyRemapBuffer;

// Function to add a new row to the remap keys table. If the originalKey and newKey args are provided, then the displayed remap keys are set to those values.
void SingleKeyRemapControl::AddNewControlKeyRemapRow(StackPanel& parent, const DWORD& originalKey, const DWORD& newKey)
{
    // Parent element for the row
    Windows::UI::Xaml::Controls::StackPanel tableRow;
    tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableRow.Spacing(100);
    tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // SingleKeyRemapControl for the original key.
    SingleKeyRemapControl originalRemapKeyControl(singleKeyRemapBuffer.size(), 0);
    tableRow.Children().Append(originalRemapKeyControl.getSingleKeyRemapControl());

    // SingleKeyRemapControl for the new remap key.
    SingleKeyRemapControl newRemapKeyControl(singleKeyRemapBuffer.size(), 1);
    tableRow.Children().Append(newRemapKeyControl.getSingleKeyRemapControl());

    // Set the key text if the two keys are not null (i.e. default args)
    if (originalKey != NULL && newKey != NULL)
    {
        singleKeyRemapBuffer.push_back(std::vector<DWORD>{ originalKey, newKey });
        originalRemapKeyControl.singleKeyRemapText.Text(winrt::to_hstring(keyboardManagerState->keyboardMap.GetKeyName(originalKey).c_str()));
        newRemapKeyControl.singleKeyRemapText.Text(winrt::to_hstring(keyboardManagerState->keyboardMap.GetKeyName(newKey).c_str()));
    }
    else
    {
        // Initialize both keys to NULL
        singleKeyRemapBuffer.push_back(std::vector<DWORD>{ NULL, NULL });
    }

    // Delete row button
    Windows::UI::Xaml::Controls::Button deleteRemapKeys;
    deleteRemapKeys.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    deleteRemapKeys.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });
    FontIcon deleteSymbol;
    deleteSymbol.FontFamily(Xaml::Media::FontFamily(L"Segoe MDL2 Assets"));
    deleteSymbol.Glyph(L"\xE74D");
    deleteRemapKeys.Content(deleteSymbol);
    deleteRemapKeys.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
        StackPanel currentRow = sender.as<Button>().Parent().as<StackPanel>();
        uint32_t index;
        parent.Children().IndexOf(currentRow, index);
        parent.Children().RemoveAt(index);
        // delete the row from the buffer. Since first child of the stackpanel is the header, the effective index starts from 1
        singleKeyRemapBuffer.erase(singleKeyRemapBuffer.begin() + (index - 1));
    });
    tableRow.Children().Append(deleteRemapKeys);
    parent.Children().Append(tableRow);
}

// Function to return the stack panel element of the SingleKeyRemapControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel SingleKeyRemapControl::getSingleKeyRemapControl()
{
    return singleKeyRemapControlLayout;
}

// Function to create the detect remap key UI window
void SingleKeyRemapControl::createDetectKeyWindow(IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::vector<DWORD>>& singleKeyRemapBuffer, KeyboardManagerState& keyboardManagerState, const int& rowIndex, const int& colIndex)
{
    // ContentDialog for detecting remap key. This is the parent UI element.
    ContentDialog detectRemapKeyBox;

    // TODO: Hardcoded light theme, since the app is not theme aware ATM.
    detectRemapKeyBox.RequestedTheme(ElementTheme::Light);
    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectRemapKeyBox.XamlRoot(xamlRoot);
    detectRemapKeyBox.Title(box_value(L"Press a key on selected keyboard:"));
    detectRemapKeyBox.IsPrimaryButtonEnabled(false);
    detectRemapKeyBox.IsSecondaryButtonEnabled(false);

    // Get the linked text block for the "Type Key" button that was clicked
    TextBlock linkedRemapText = getSiblingElement(sender).as<TextBlock>();

    auto unregisterKeys = [&keyboardManagerState]() {
        std::thread t1(&KeyboardManagerState::UnregisterKeyDelay, &keyboardManagerState, VK_ESCAPE);
        std::thread t2(&KeyboardManagerState::UnregisterKeyDelay, &keyboardManagerState, VK_RETURN);
        t1.detach();
        t2.detach();
    };

    TextBlock primaryButtonText;
    primaryButtonText.Text(to_hstring(L"OK"));

    Button primaryButton;
    primaryButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    primaryButton.Margin({ 2, 2, 2, 2 });
    primaryButton.Content(primaryButtonText);
    primaryButton.Click([=, &singleKeyRemapBuffer, &keyboardManagerState](IInspectable const& sender, RoutedEventArgs const&) {
        // Save the detected key in the linked text block
        DWORD detectedKey = keyboardManagerState.GetDetectedSingleRemapKey();

        if (detectedKey != NULL)
        {
            singleKeyRemapBuffer[rowIndex][colIndex] = detectedKey;
            linkedRemapText.Text(winrt::to_hstring(keyboardManagerState.keyboardMap.GetKeyName(detectedKey).c_str()));
        }

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        unregisterKeys();
        detectRemapKeyBox.Hide();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_RETURN,
        std::bind(&KeyboardManagerState::SelectDetectedRemapKey, &keyboardManagerState, std::placeholders::_1),
        [primaryButton, detectRemapKeyBox](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [primaryButton] {
                    primaryButton.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::DarkGray() });
                });
        },
        [=, &singleKeyRemapBuffer, &keyboardManagerState](DWORD) {
            DWORD detectedKey = keyboardManagerState.GetDetectedSingleRemapKey();

            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [detectRemapKeyBox, linkedRemapText, detectedKey, &keyboardManagerState] {
                    detectRemapKeyBox.Hide();

                    if (detectedKey != NULL)
                    {
                        linkedRemapText.Text(winrt::to_hstring(keyboardManagerState.keyboardMap.GetKeyName(detectedKey).c_str()));
                    }
                });

            if (detectedKey != NULL)
            {
                singleKeyRemapBuffer[rowIndex][colIndex] = detectedKey;
            }

            // Reset the keyboard manager UI state
            keyboardManagerState.ResetUIState();
            unregisterKeys();
        });

    TextBlock cancelButtonText;
    cancelButtonText.Text(to_hstring(L"Cancel"));

    Button cancelButton;
    cancelButton.HorizontalAlignment(HorizontalAlignment::Stretch);
    cancelButton.Margin({ 2, 2, 2, 2 });
    cancelButton.Content(cancelButtonText);
    // Cancel button
    cancelButton.Click([detectRemapKeyBox, unregisterKeys, &keyboardManagerState](IInspectable const& sender, RoutedEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
        unregisterKeys();
        detectRemapKeyBox.Hide();
    });

    keyboardManagerState.RegisterKeyDelay(
        VK_ESCAPE,
        std::bind(&KeyboardManagerState::SelectDetectedRemapKey, &keyboardManagerState, std::placeholders::_1),
        [&keyboardManagerState, detectRemapKeyBox, unregisterKeys](DWORD) {
            detectRemapKeyBox.Dispatcher().RunAsync(
                Windows::UI::Core::CoreDispatcherPriority::Normal,
                [detectRemapKeyBox] {
                    detectRemapKeyBox.Hide();
                });

            keyboardManagerState.ResetUIState();
            unregisterKeys();
        },
        nullptr);

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    detectRemapKeyBox.Content(stackPanel);

    // Header textblock
    TextBlock text;
    text.Text(winrt::to_hstring("Key Pressed:"));
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
    buttonPanel.Margin({0, 20, 0, 0});
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
    keyboardManagerState.ConfigureDetectSingleKeyRemapUI(keyStackPanel);

    // Show the dialog
    detectRemapKeyBox.ShowAsync();
}