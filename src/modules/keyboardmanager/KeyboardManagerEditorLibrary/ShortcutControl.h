#pragma once

#include <keyboardmanager/common/Shortcut.h>

namespace KBMEditor
{
    class KeyboardManagerState;
}

class KeyDropDownControl;
namespace winrt::Windows::UI::Xaml
{
    struct XamlRoot;
    namespace Controls
    {
        struct StackPanel;
        struct TextBox;
        struct Button;
    }
}

class ShortcutControl
{
private:
    // Wrap grid for the drop downs to display the selected shortcut
    winrt::Windows::Foundation::IInspectable shortcutDropDownVariableSizedWrapGrid;

    // Button to type the shortcut
    winrt::Windows::Foundation::IInspectable typeShortcut;

    // StackPanel to parent the above controls
    winrt::Windows::Foundation::IInspectable shortcutControlLayout;

    // Function to set the accessible name of the target app text box
    static void SetAccessibleNameForTextBox(TextBox targetAppTextBox, int rowIndex);

    // Function to set the accessible names for all the controls in a row
    static void UpdateAccessibleNames(StackPanel sourceColumn, StackPanel mappedToColumn, TextBox targetAppTextBox, Button deleteButton, int rowIndex);

public:
    // Handle to the current Edit Shortcuts Window
    static HWND editShortcutsWindowHandle;

    // Pointer to the keyboard manager state
    static KBMEditor::KeyboardManagerState* keyboardManagerState;

    // Stores the current list of remappings
    static RemapBuffer shortcutRemapBuffer;

    // Vector to store dynamically allocated KeyDropDownControl objects to avoid early destruction
    std::vector<std::unique_ptr<KeyDropDownControl>> keyDropDownControlObjects;

    // constructor
    ShortcutControl(StackPanel table, StackPanel row, const int colIndex, TextBox targetApp);

    // Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
    static void AddNewShortcutControlRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, const Shortcut& originalKeys = Shortcut(), const KeyShortcutUnion& newKeys = Shortcut(), const std::wstring& targetAppName = L"");

    // Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
    StackPanel GetShortcutControl();

    // Function to create the detect shortcut UI window
    static void CreateDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KBMEditor::KeyboardManagerState& keyboardManagerState, const int colIndex, StackPanel table, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, StackPanel controlLayout, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, HWND parentWindow, RemapBuffer& remapBuffer);
};
