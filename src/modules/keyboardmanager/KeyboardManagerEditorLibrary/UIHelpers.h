#pragma once

namespace winrt
{
    struct hstring;
    namespace Windows::Foundation
    {
        struct IInspectable;
        namespace Collections
        {
            template<typename T>
            struct IVector;
        }
    }
}

// This namespace contains UI methods that are to be used for both KBM windows
namespace UIHelpers
{
    // This method sets focus to the first "Select" button on the last row of the Grid of EditKeyboardWindow
    void SetFocusOnFirstSelectButtonInLastRowOfEditKeyboardWindow(StackPanel& parent, long colCount);

    // This method sets focus to the first "Select" button on the last row of the Grid of EditShortcutsWindow
    void SetFocusOnFirstSelectButtonInLastRowOfEditShortcutsWindow(StackPanel& parent, long colCount);

    RECT GetForegroundWindowDesktopRect();

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable GetSiblingElement(winrt::Windows::Foundation::IInspectable const& element);

    winrt::Windows::Foundation::IInspectable GetWrapped(const winrt::Windows::Foundation::IInspectable& element, double width);

    // Function to return a StackPanel with an element and a TextBlock label. 
    winrt::Windows::Foundation::IInspectable GetLabelWrapped(const winrt::Windows::Foundation::IInspectable& element, std::wstring label, double width, HorizontalAlignment horizontalAlignment = HorizontalAlignment::Left);

    // Function to return the list of key name in the order for the drop down based on the key codes
    winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> ToBoxValue(const std::vector<std::pair<DWORD, std::wstring>>& list);

#ifndef NDEBUG
    // Useful For debugging issues
    std::vector<std::wstring> GetChildrenNames(StackPanel& s);
#endif
}
