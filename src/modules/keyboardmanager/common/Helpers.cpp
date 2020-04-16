#include "pch.h"
#include "Helpers.h"
#include <sstream>

namespace KeyboardManagerHelper
{
    // Function to split a wstring based on a delimiter and return a vector of split strings
    std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter)
    {
        std::wstringstream ss(input);
        std::wstring item;
        std::vector<std::wstring> splittedStrings;
        while (std::getline(ss, item, delimiter))
        {
            splittedStrings.push_back(item);
        }

        return splittedStrings;
    }

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable getSiblingElement(winrt::Windows::Foundation::IInspectable const& element)
    {
        FrameworkElement frameworkElement = element.as<FrameworkElement>();
        StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
        uint32_t index;

        parentElement.Children().IndexOf(frameworkElement, index);
        return parentElement.Children().GetAt(index + 1);
    }
}
