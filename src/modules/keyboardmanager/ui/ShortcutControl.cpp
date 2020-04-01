#include "pch.h"
#include "ShortcutControl.h"

//Both static members are initialized to null
HWND ShortcutControl::EditShortcutsWindowHandle = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;

// Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
void ShortcutControl::AddNewShortcutControlRow(StackPanel& parent, const std::vector<DWORD>& originalKeys, const std::vector<DWORD>& newKeys)
{
    // Parent element for the row
    Windows::UI::Xaml::Controls::StackPanel tableRow;
    tableRow.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
    tableRow.Spacing(100);
    tableRow.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);

    // ShortcutControl for the original shortcut
    ShortcutControl originalSC;
    tableRow.Children().Append(originalSC.getShortcutControl());

    // ShortcutControl for the new shortcut
    ShortcutControl newSC;
    tableRow.Children().Append(newSC.getShortcutControl());

    // Set the shortcut text if the two vectors are not empty (i.e. default args)
    if (!originalKeys.empty() && !newKeys.empty())
    {
        originalSC.shortcutText.Text(convertVectorToHstring<DWORD>(originalKeys));
        newSC.shortcutText.Text(convertVectorToHstring<DWORD>(newKeys));
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
    });
    tableRow.Children().Append(deleteShortcut);
    parent.Children().Append(tableRow);
}

// Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
StackPanel ShortcutControl::getShortcutControl()
{
    return shortcutControlLayout;
}

// Function to create the detect shortcut UI window
void ShortcutControl::createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState)
{
    // ContentDialog for detecting shortcuts. This is the parent UI element.
    ContentDialog detectShortcutBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
    detectShortcutBox.PrimaryButtonText(to_hstring(L"OK"));
    detectShortcutBox.IsSecondaryButtonEnabled(false);
    detectShortcutBox.CloseButtonText(to_hstring(L"Cancel"));
    detectShortcutBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    // Get the linked text block for the "Type shortcut" button that was clicked
    TextBlock linkedShortcutText = getSiblingElement(sender).as<TextBlock>();

    // OK button
    detectShortcutBox.PrimaryButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        // Save the detected shortcut in the linked text block
        Shortcut detectedShortcutKeys = keyboardManagerState.GetDetectedShortcut();
        linkedShortcutText.Text(detectedShortcutKeys.toHstring());

        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
    });

    // Cancel button
    detectShortcutBox.CloseButtonClick([&keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        // Reset the keyboard manager UI state
        keyboardManagerState.ResetUIState();
    });

    // StackPanel parent for the displayed text in the dialog
    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    // Header textblock
    TextBlock text;
    text.Text(winrt::to_hstring("Keys Pressed:"));
    text.Margin({ 0, 0, 0, 10 });

    // Textblock to display the detected shortcut
    TextBlock shortcutKeys;

    stackPanel.Children().Append(text);
    stackPanel.Children().Append(shortcutKeys);
    stackPanel.UpdateLayout();
    detectShortcutBox.Content(stackPanel);

    // Configure the keyboardManagerState to store the UI information.
    keyboardManagerState.ConfigureDetectShortcutUI(shortcutKeys);

    // Show the dialog
    detectShortcutBox.ShowAsync();
}