#pragma once
#include <keyboardmanager/common/KeyboardManagerState.h>
#include <keyboardManager/common/Helpers.h>
#include <keyboardmanager/common/Shortcut.h>

class ShortcutControl
{
private:
    // Textblock to display the selected shortcut
    TextBlock shortcutText;

    // Stack panel for the drop downs to display the selected shortcut
    StackPanel shortcutDropDownStackPanel;

    // Button to type the shortcut
    Button typeShortcut;

    // StackPanel to parent the above controls
    StackPanel shortcutControlLayout;

public:
    // Handle to the current Edit Shortcuts Window
    static HWND EditShortcutsWindowHandle;
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;
    // Stores the current list of remappings
    static std::vector<std::vector<Shortcut>> shortcutRemapBuffer;

    ShortcutControl(const int& rowIndex, const int& colIndex)
    {
        shortcutDropDownStackPanel.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
        shortcutDropDownStackPanel.Spacing(10);
        shortcutDropDownStackPanel.Orientation(Windows::UI::Xaml::Controls::Orientation::Horizontal);
        AddDropDown(shortcutDropDownStackPanel, rowIndex, colIndex);

        typeShortcut.Content(winrt::box_value(winrt::to_hstring("Type Shortcut")));
        typeShortcut.Click([&, rowIndex, colIndex](IInspectable const& sender, RoutedEventArgs const&) {
            keyboardManagerState->SetUIState(KeyboardManagerUIState::DetectShortcutWindowActivated, EditShortcutsWindowHandle);
            // Using the XamlRoot of the typeShortcut to get the root of the XAML host
            createDetectShortcutWindow(sender, sender.as<Button>().XamlRoot(), shortcutRemapBuffer, *keyboardManagerState, rowIndex, colIndex);
        });

        shortcutControlLayout.Background(Windows::UI::Xaml::Media::SolidColorBrush{ Windows::UI::Colors::LightGray() });
        shortcutControlLayout.Margin({ 0, 0, 0, 10 });
        shortcutControlLayout.Spacing(10);

        shortcutControlLayout.Children().Append(typeShortcut);
        shortcutControlLayout.Children().Append(shortcutDropDownStackPanel);
        shortcutControlLayout.UpdateLayout();
    }

    // Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
    static void AddNewShortcutControlRow(StackPanel& parent, Shortcut originalKeys = Shortcut(), Shortcut newKeys = Shortcut());

    // Function to add a drop down to the shortcut stack panel
    ComboBox AddDropDown(StackPanel parent, const int& rowIndex, const int& colIndex)
    {
        ComboBox shortcutDropDown;
        shortcutDropDown.Width(100);
        shortcutDropDown.MaxDropDownHeight(200);
        shortcutDropDown.ItemsSource(keyboardManagerState->keyboardMap.GetKeyList(true).first);
        // drop down selection handler
        shortcutDropDown.SelectionChanged([&, rowIndex, colIndex, parent](IInspectable const& sender, SelectionChangedEventArgs const&) {
            ComboBox currentDropDown = sender.as<ComboBox>();
            std::vector<DWORD> keyCodes = keyboardManagerState->keyboardMap.GetKeyList(true).second;
            int selectedKeyIndex = currentDropDown.SelectedIndex();
            uint32_t dropDownIndex = -1;
            bool dropDownFound = parent.Children().IndexOf(currentDropDown, dropDownIndex);

            if (selectedKeyIndex != -1 && keyCodes.size() > selectedKeyIndex && dropDownFound)
            {
                // Case: Only 1 drop down and action key is chosen: Warn that a modifier must be chosen
                if (parent.Children().Size() == 1 && !IsModifierKey(keyCodes[selectedKeyIndex]))
                {
                    // Reset the shortcut
                    shortcutRemapBuffer[rowIndex][colIndex].Reset();
                }
                // If it is the last drop down
                else if (dropDownIndex == parent.Children().Size() - 1)
                {
                    // Case: Last drop down and a modifier is selected: add a new drop down (max of 5 drop downs should be enforced)
                    if (IsModifierKey(keyCodes[selectedKeyIndex]) && parent.Children().Size() < 5)
                    {
                        // check if modifier has already been added before
                        shortcutRemapBuffer[rowIndex][colIndex].SetKey(keyCodes[selectedKeyIndex]);
                        AddDropDown(parent, rowIndex, colIndex);
                    }
                    // Case: Last drop down and a modifier is selected but there are already 5 drop downs: warn the user
                    else if (IsModifierKey(keyCodes[selectedKeyIndex]) && parent.Children().Size() >= 5)
                    {
                        // warn
                    }
                    // If None is selected but it's the last index: warn
                    else if (keyCodes[selectedKeyIndex] == 0)
                    {
                        // warn, reset the drop down and set the shortcut buffer to currently selected keys
                        currentDropDown.SelectedIndex(-1);
                        shortcutRemapBuffer[rowIndex][colIndex].SetKeyCodes(GetKeysFromStackPanel(parent));
                    }
                    else
                    {
                        // Set the action key
                        shortcutRemapBuffer[rowIndex][colIndex].SetKey(keyCodes[selectedKeyIndex]);
                    }
                }
                // If it is the not the last drop down
                else
                {
                    if (IsModifierKey(keyCodes[selectedKeyIndex]))
                    {
                        // check if modifier repeated in another drop down
                    }
                    // If None is selected and there are more than 2 drop downs
                    else if (keyCodes[selectedKeyIndex] == 0 && parent.Children().Size() > 2)
                    {
                        // delete drop down
                        parent.Children().RemoveAt(dropDownIndex);
                    }
                    else if (keyCodes[selectedKeyIndex] == 0 && parent.Children().Size() <= 2)
                    {
                        // warn
                    }
                    // If the user tries to set an action key
                    else
                    {
                        // warn
                    }
                }
            }
            else
            {
                // Set to empty shortcut
                shortcutRemapBuffer[rowIndex][colIndex].Reset();
            }

            // Case 2: Last drop down and a modifier is selected: add a new drop down

            // Case 2: Drop down contained a modifier key earlier and None is selected: delete the drop down

            // Multiple of same modifier category
        });

        parent.Children().Append(shortcutDropDown);
        parent.UpdateLayout();

        return shortcutDropDown;
    }

    void AddShortcutToControl(Shortcut& shortcut, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int& rowIndex, const int& colIndex)
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
                if (i < parent.Children().Size())
                {
                    ComboBox currentDropDown = parent.Children().GetAt(i).as<ComboBox>();
                    auto it = std::find(keyCodeList.begin(), keyCodeList.end(), shortcutKeyCodes[i]);
                    if (it != keyCodeList.end())
                    {
                        currentDropDown.SelectedIndex(std::distance(keyCodeList.begin(), it));
                    }
                }
            }
        }
        parent.UpdateLayout();
    }

    std::vector<DWORD>& GetKeysFromStackPanel(StackPanel parent)
    {
        std::vector<DWORD> keys;
        std::vector<DWORD> keyCodeList = keyboardManagerState->keyboardMap.GetKeyList(true).second;
        for (int i = 0; i < parent.Children.Size(); i++)
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

    // Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
    StackPanel getShortcutControl();

    // Function to create the detect shortcut UI window
    void createDetectShortcutWindow(IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::vector<Shortcut>>& shortcutRemapBuffer, KeyboardManagerState& keyboardManagerState, const int& rowIndex, const int& colIndex);
};
