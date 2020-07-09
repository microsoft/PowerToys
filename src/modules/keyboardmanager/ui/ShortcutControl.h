#pragma once
#include "keyboardmanager/common/Shortcut.h"

class KeyboardManagerState;
class KeyDropDownControl;
namespace winrt::Windows::UI::Xaml
{
    struct XamlRoot;
    namespace Controls
    {
        struct StackPanel;
        struct Grid;
        struct TextBox;
    }
}

class ShortcutControl
{
private:
    // Stack panel for the drop downs to display the selected shortcut
    winrt::Windows::Foundation::IInspectable shortcutDropDownStackPanel;

    // Button to type the shortcut
    winrt::Windows::Foundation::IInspectable typeShortcut;

    // StackPanel to parent the above controls
    winrt::Windows::Foundation::IInspectable shortcutControlLayout;

public:
    // Handle to the current Edit Shortcuts Window
    static HWND EditShortcutsWindowHandle;
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;
    // Stores the current list of remappings
    static std::vector<std::pair<std::vector<Shortcut>, std::wstring>> shortcutRemapBuffer;
    // Vector to store dynamically allocated KeyDropDownControl objects to avoid early destruction
    std::vector<std::unique_ptr<KeyDropDownControl>> keyDropDownControlObjects;

    // constructor
    ShortcutControl(Grid table, const int colIndex, TextBox targetApp);

    // Function to add a new row to the shortcut table. If the originalKeys and newKeys args are provided, then the displayed shortcuts are set to those values.
    static void AddNewShortcutControlRow(Grid& parent, std::vector<std::vector<std::unique_ptr<ShortcutControl>>>& keyboardRemapControlObjects, Shortcut originalKeys = Shortcut(), Shortcut newKeys = Shortcut(), std::wstring targetAppName = L"");

    // Function to add a shortcut to the shortcut control as combo boxes
    void AddShortcutToControl(Shortcut& shortcut, Grid table, StackPanel parent, KeyboardManagerState& keyboardManagerState, const int colIndex, TextBox targetApp);

    // Function to return the stack panel element of the ShortcutControl. This is the externally visible UI element which can be used to add it to other layouts
    StackPanel getShortcutControl();

    // Function to create the detect shortcut UI window
    void createDetectShortcutWindow(winrt::Windows::Foundation::IInspectable const& sender, XamlRoot xamlRoot, std::vector<std::pair<std::vector<Shortcut>, std::wstring>>& shortcutRemapBuffer, KeyboardManagerState& keyboardManagerState, const int colIndex, Grid table, TextBox targetApp);
};
