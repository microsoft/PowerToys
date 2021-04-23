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
    // This method sets focus to the first Type button on the last row of the Grid
    void SetFocusOnTypeButtonInLastRow(StackPanel& parent, long colCount);

    RECT GetForegroundWindowDesktopRect();

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable GetSiblingElement(winrt::Windows::Foundation::IInspectable const& element);

    winrt::Windows::Foundation::IInspectable GetWrapped(const winrt::Windows::Foundation::IInspectable& element, double width);

    // Function to return the list of key name in the order for the drop down based on the key codes
    winrt::Windows::Foundation::Collections::IVector<winrt::Windows::Foundation::IInspectable> ToBoxValue(const std::vector<std::pair<DWORD, std::wstring>>& list);
}
