#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardManager/common/Helpers.h>
#include <keyboardmanager/common/Shortcut.h>

class ShortcutControl
{
private:
    // Textblock to display the selected shortcut
    TextBlock shortcutText;

    // Button to type the shortcut
    Button typeShortcut;

    // StackPanel to parent the above controls
    StackPanel shortcutControlLayout;

public:
    // Handle to the current Edit Shortcuts Window
    static HWND EditShortcutsWindowHandle;
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;

    ShortcutControl()
    {
        typeShortcut.Content(winrt::box_value(winrt::to_hstring("Type Shortcut")));
        typeShortcut.Click([&](IInspectable const& sender, RoutedEventArgs const&) {
            keyboardManagerState->SetUIState(KeyboardManagerUIState::DetectShortcutWindowActivated, EditShortcutsWindowHandle);
            // Using the XamlRoot of the typeShortcut to get the root of the XAML host
            createDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), *keyboardManagerState);
        });

        shortcutControlLayout.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
        shortcutControlLayout.Margin({ 0, 0, 0, 10 });
        shortcutControlLayout.Spacing(10);

        shortcutControlLayout.Children().Append(typeShortcut);
        shortcutControlLayout.Children().Append(shortcutText);
    }

    // Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
    static void AddNewShortcutControlRow(StackPanel& parent, const Shortcut& originalKeys = Shortcut(), const Shortcut& newKeys = Shortcut());

    // Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
    StackPanel getShortcutControl();

    // Function to create the detect shortcut UI window
    void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, KeyboardManagerState& keyboardManagerState);
};
