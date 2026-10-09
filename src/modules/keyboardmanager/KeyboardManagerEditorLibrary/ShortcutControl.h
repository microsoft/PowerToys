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
    Button btnPickShortcut;
    
    // StackPanel to hold the shortcut
    StackPanel spBtnPickShortcut;

    // StackPanel to parent the above controls
    winrt::Windows::Foundation::IInspectable shortcutControlLayout;

    // StackPanel to parent the first line of "To" Column
    winrt::Windows::Foundation::IInspectable keyComboStackPanel;

    // Function to set the accessible name of the target app text box
    static void SetAccessibleNameForTextBox(TextBox targetAppTextBox, int rowIndex);

    // Function to set the accessible names for all the controls in a row
    static void UpdateAccessibleNames(StackPanel sourceColumn, StackPanel mappedToColumn, TextBox targetAppTextBox, Button deleteButton, int rowIndex);

    // enum for the type of shortcut, to make it easier to switch on and read
    enum class ShortcutType
    {
        Shortcut,
        Text,
        RunProgram,
        OpenURI
    };

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

    // Function to that will CreateDetectShortcutWindow, created here to it can be done automatically when "new shortcut" is clicked.
    void OpenNewShortcutControlRow(StackPanel table, StackPanel row);

    // Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
    static ShortcutControl& AddNewShortcutControlRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, const Shortcut& originalKeys = Shortcut(), const KeyShortcutTextUnion& newKeys = Shortcut(), const std::wstring& targetAppName = L"");

    // Function to delete the shortcut control
    static void ShortcutControl::DeleteShortcutControl(StackPanel& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, int index);

    // Function to get the shortcut type
    static ShortcutType GetShortcutType(const Controls::ComboBox& typeCombo);

    // Function to remove extra quotes from the start and end of the string (used where we will add them as needed later)
    static std::wstring ShortcutControl::RemoveExtraQuotes(const std::wstring& str);

    // Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
    StackPanel GetShortcutControl();

    // Function to create the detect shortcut UI window
    static void CreateDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KBMEditor::KeyboardManagerState& keyboardManagerState, const int colIndex, StackPanel table, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, StackPanel controlLayout, TextBox targetApp, bool isHybridControl, bool isSingleKeyWindow, HWND parentWindow, RemapBuffer& remapBuffer);
};

StackPanel SetupRunProgramControls(StackPanel& parent, StackPanel& row, Shortcut& shortCut, winrt::Windows::UI::Xaml::Thickness& textInputMargin, ::StackPanel& _controlStackPanel);

void CreateNewTempShortcut(StackPanel& row, Shortcut& tempShortcut, const uint32_t& rowIndex);

StackPanel SetupOpenURIControls(StackPanel& parent, StackPanel& row, Shortcut& shortCut, winrt::Windows::UI::Xaml::Thickness& textInputMargin, ::StackPanel& _controlStackPanel);
