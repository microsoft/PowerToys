#include "ShortcutControl.h"

HWND ShortcutControl::_hWndEditShortcutsWindow = nullptr;
KeyboardManagerState* ShortcutControl::keyboardManagerState = nullptr;

StackPanel ShortcutControl::getShortcutControl()
{
    return shortcutControlLayout;
}

void ShortcutControl::createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState)
{
    ContentDialog detectShortcutBox;

    // ContentDialog requires manually setting the XamlRoot (https://docs.microsoft.com/en-us/uwp/api/windows.ui.xaml.controls.contentdialog#contentdialog-in-appwindow-or-xaml-islands)
    detectShortcutBox.XamlRoot(xamlRoot);
    detectShortcutBox.Title(box_value(L"Press the keys in shortcut:"));
    detectShortcutBox.PrimaryButtonText(to_hstring(L"OK"));
    detectShortcutBox.IsSecondaryButtonEnabled(false);
    detectShortcutBox.CloseButtonText(to_hstring(L"Cancel"));
    detectShortcutBox.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    TextBlock linkedShortcutText = getSiblingElement(sender).as<TextBlock>();

    detectShortcutBox.PrimaryButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        hstring shortcutString;
        std::vector<DWORD> shortcutKeys = keyboardManagerState.GetDetectedShortcut();
        for (int i = 0; i < shortcutKeys.size(); i++)
        {
            shortcutString = shortcutString + to_hstring((unsigned int)shortcutKeys[i]) + to_hstring(L" ");
        }
        linkedShortcutText.Text(shortcutString);
        keyboardManagerState.ResetUIState();
    });
    detectShortcutBox.CloseButtonClick([=, &keyboardManagerState](Windows::UI::Xaml::Controls::ContentDialog const& sender, ContentDialogButtonClickEventArgs const&) {
        keyboardManagerState.ResetUIState();
    });

    Windows::UI::Xaml::Controls::StackPanel stackPanel;
    stackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });

    TextBlock text;
    text.Text(winrt::to_hstring("Keys Pressed:"));
    text.Margin({ 0, 0, 0, 10 });
    TextBlock shortcutKeys;

    stackPanel.Children().Append(text);
    stackPanel.Children().Append(shortcutKeys);
    stackPanel.UpdateLayout();
    detectShortcutBox.Content(stackPanel);

    keyboardManagerState.ConfigureDetectShortcutUI(shortcutKeys);
    detectShortcutBox.ShowAsync();
}