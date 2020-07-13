#pragma once
class KeyboardManagerState;
class Shortcut;

namespace winrt::Windows
{
    namespace Foundation
    {
        struct hstring;
    }

    namespace UI::Xaml::Controls
    {
        struct StackPanel;
        struct Grid;
        struct ComboBox;
        struct Flyout;
        struct TextBlock;
    }
}

namespace KeyboardManagerHelper
{
    enum class ErrorType;
}

// Wrapper class for the key drop down menu
class KeyDropDownControl
{
private:
    // Stores the drop down combo box
    winrt::Windows::Foundation::IInspectable dropDown;
    // Stores the previous layout
    HKL previousLayout = 0;
    // Stores the key code list
    std::vector<DWORD> keyCodeList;
    // Stores the flyout warning message
    winrt::Windows::Foundation::IInspectable warningMessage;
    // Stores the flyout attached to the current drop down
    winrt::Windows::Foundation::IInspectable warningFlyout;

    // Function to set properties apart from the SelectionChanged event handler
    void SetDefaultProperties(bool isShortcut);

    // Function to check if the layout has changed and accordingly update the drop down list
    void CheckAndUpdateKeyboardLayout(ComboBox currentDropDown, bool isShortcut);

public:
    // Pointer to the keyboard manager state
    static KeyboardManagerState* keyboardManagerState;

    // Constructor - the last default parameter should be passed as false only if it originates from Type shortcut or when an old shortcut is reloaded
    KeyDropDownControl(bool isShortcut)
    {
        SetDefaultProperties(isShortcut);
    }

    // Function to set selection handler for single key remap drop down. Needs to be called after the constructor since the singleKeyControl StackPanel is null if called in the constructor
    void SetSelectionHandler(winrt::Windows::UI::Xaml::Controls::Grid& table, winrt::Windows::UI::Xaml::Controls::StackPanel singleKeyControl, int colIndex, std::vector<std::vector<DWORD>>& singleKeyRemapBuffer);

    // Function for validating the selection of shortcuts for the drop down
    std::pair<KeyboardManagerHelper::ErrorType, int> ValidateShortcutSelection(winrt::Windows::UI::Xaml::Controls::Grid table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, int colIndex, std::vector<std::pair<std::vector<Shortcut>, std::wstring>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox targetApp);

    // Function to set selection handler for shortcut drop down. Needs to be called after the constructor since the shortcutControl StackPanel is null if called in the constructor
    void SetSelectionHandler(winrt::Windows::UI::Xaml::Controls::Grid& table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, int colIndex, std::vector<std::pair<std::vector<Shortcut>, std::wstring>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox& targetApp);

    // Function to set the selected index of the drop down
    void SetSelectedIndex(int32_t index);

    // Function to return the combo box element of the drop down
    ComboBox GetComboBox();

    // Function to add a drop down to the shortcut stack panel
    static void AddDropDown(winrt::Windows::UI::Xaml::Controls::Grid table, winrt::Windows::UI::Xaml::Controls::StackPanel shortcutControl, winrt::Windows::UI::Xaml::Controls::StackPanel parent, const int colIndex, std::vector<std::pair<std::vector<Shortcut>, std::wstring>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, winrt::Windows::UI::Xaml::Controls::TextBox& targetApp);

    // Function to get the list of key codes from the shortcut combo box stack panel
    static std::vector<DWORD> GetKeysFromStackPanel(StackPanel parent);

    // Function to check if a modifier has been repeated in the previous drop downs
    static bool CheckRepeatedModifier(winrt::Windows::UI::Xaml::Controls::StackPanel parent, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList);

    // Function for validating the selection of shortcuts for all the associated drop downs
    static void ValidateShortcutFromDropDownList(Grid table, StackPanel shortcutControl, StackPanel parent, int colIndex, std::vector<std::pair<std::vector<Shortcut>, std::wstring>>& shortcutRemapBuffer, std::vector<std::unique_ptr<KeyDropDownControl>>& keyDropDownControlObjects, TextBox targetApp);

    // Function to set the warning message
    void SetDropDownError(winrt::Windows::UI::Xaml::Controls::ComboBox currentDropDown, winrt::hstring message);
};
