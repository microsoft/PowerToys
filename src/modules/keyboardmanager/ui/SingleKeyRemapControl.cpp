#include "pch.h"
#include "SingleKeyRemapControl.h"

//Both static members are initialized to null
HWND SingleKeyRemapControl::EditKeyboardWindowHandle = nullptr;
KeyboardManagerState* SingleKeyRemapControl::keyboardManagerState = nullptr;

// Function to add a new row to the remap keys table. If the originalKey and newKey args are provided, then the displayed remap keys are set to those values.
void SingleKeyRemapControl::AddNewControlKeyRemapRow(StackPanel& parent, const DWORD& originalKey, const WORD& newKey)
{
    // Parent element for the row
    Windows::UI::Xaml::Controls::StackPanel tableRow;
    tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableRow.Spacing(100);
    tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // SingleKeyRemapControl for the original key.
    SingleKeyRemapControl originalRemapKeyControl;
    tableRow.Children().Append(originalRemapKeyControl.getSingleKeyRemapControl());

    // SingleKeyRemapControl for the new remap key.
    SingleKeyRemapControl newRemapKeyControl;
    tableRow.Children().Append(newRemapKeyControl.getSingleKeyRemapControl());

    // Set the key text if the two keys are not null (i.e. default args)
    if (originalKey != NULL && newKey != NULL)
    {
        originalRemapKeyControl.singleKeyRemapText.Text(winrt::to_hstring((unsigned int)originalKey));
        newRemapKeyControl.singleKeyRemapText.Text(winrt::to_hstring((unsigned int)newKey));
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
void SingleKeyRemapControl::createDetectKeyWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState)
{
    // ContentDialog for detecting remap key. This is the parent UI element.
    ContentDialog detectRemapKeyBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectRemapKeyBox.XamlRoot(xamlRoot);
    detectRemapKeyBox.Title(box_value(L"Press a key on selected keyboard:"));
    detectRemapKeyBox.PrimaryButtonText(to_hstring(L"OK"));
    detectRemapKeyBox.IsSecondaryButtonEnabled(false);
    detectRemapKeyBox.CloseButtonText(to_hstring(L"Cancel"));
    detectRemapKeyBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    detectRemapKeyBox.Foreground(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::Black() });

    // Get the linked text block for the "Type Key" button that was clicked
    TextBlock linkedRemapText = getSiblingElement(sender).as<TextBlock>();

    // OK button
    detectRemapKeyBox.PrimaryButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        // Save the detected key in the linked text block
        DWORD detectedKey = keyboardManagerState.GetDetectedSingleRemapKey();
        if (detectedKey != NULL)
        {
            linkedRemapText.Text(winrt::to_hstring((unsigned int)detectedKey));
        }

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
    });

    // Cancel button
    detectRemapKeyBox.CloseButtonClick([&keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
    });

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    // Header textblock
    TextBlock text;
    text.Text(winrt::to_hstring("Key Pressed:"));
    text.Margin({ 0, 0, 0, 10 });

    // Textblock to display the detected key
    TextBlock remapKey;

    stackPanel.Children().Append(text);
    stackPanel.Children().Append(remapKey);
    stackPanel.UpdateLayout();
    detectRemapKeyBox.Content(stackPanel);

    // Configure the keyboardManagerState to store the UI information.
    keyboardManagerState.ConfigureDetectSingleKeyRemapUI(remapKey);

    // Show the dialog
    detectRemapKeyBox.ShowAsync();
}