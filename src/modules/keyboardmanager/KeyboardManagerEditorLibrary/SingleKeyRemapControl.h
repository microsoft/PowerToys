#pragma once

#include <keyboardmanager/common/Shortcut.h>

#include <KeyDropDownControl.h>

namespace KBMEditor
{
    class KeyboardManagerState;
}

namespace winrt::Windows::UI::Xaml
{
    struct XamlRoot;
    namespace Controls
    {
        struct StackPanel;
        struct Grid;
        struct Button;
    }
}

class SingleKeyRemapControl
{
private:
    // Button to type the remap key
    winrt::Windows::Foundation::IInspectable typeKey;

    // StackPanel to parent the above controls
    winrt::Windows::Foundation::IInspectable singleKeyRemapControlLayout;

    // Stack panel for the drop downs to display the selected shortcut for the hybrid case
    winrt::Windows::Foundation::IInspectable hybridDropDownVariableSizedWrapGrid;

    // Function to set the accessible names for all the controls in a row
    static void UpdateAccessibleNames(StackPanel sourceColumn, StackPanel mappedToColumn, Button deleteButton, int rowIndex);

    void TextToMapChangedHandler(winrt::Windows::Foundation::IInspectable const& sender, winrt::Windows::UI::Xaml::Controls::TextChangedEventArgs const& e);

public:
    // Vector to store dynamically allocated KeyDropDownControl objects to avoid early destruction
    std::vector<std::unique_ptr<KeyDropDownControl>> keyDropDownControlObjects;

    // Handle to the current Edit Keyboard Window
    static HWND EditKeyboardWindowHandle;

    // Pointer to the keyboard manager state
    static KBMEditor::KeyboardManagerState* keyboardManagerState;

    // Stores the current list of remappings
    static RemapBuffer singleKeyRemapBuffer;

    // constructor
    SingleKeyRemapControl(StackPanel table, StackPanel row, const int colIndex);

    // Function to add a new row to the remap keys table. If the originalKey and newKey args are provided, then the displayed remap keys are set to those values.
    static void AddNewControlKeyRemapRow(StackPanel& parent, std::vector<std::vector<std::unique_ptr<SingleKeyRemapControl>>>& keyboardRemapControlObjects, const DWORD originalKey = 0, const KeyShortcutTextUnion newKey = (DWORD)0);

    // Function to return the stack panel element of the SingleKeyRemapControl. This is the externally visible UI element which can be used to add it to other layouts
    winrt::Windows::UI::Xaml::Controls::StackPanel getSingleKeyRemapControl();

    // Function to create the detect remap keys UI window
    void createDetectKeyWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, KBMEditor::KeyboardManagerState& keyboardManagerState);
};
