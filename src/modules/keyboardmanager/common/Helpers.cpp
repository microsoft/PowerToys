#include "Helpers.h"

std::vector<std::wstring> splitwstring(std::wstring input, wchar_t delimiter)
{
    std::wstringstream ss(input);
    std::wstring item;
    std::vector<std::wstring> splittedStrings;
    while (std::getline(ss, item, L' '))
    {
        splittedStrings.push_back(item);
    }

    return splittedStrings;
}

IInspectable getSiblingElement(IInspectable const& element)
{
    FrameworkElement frameworkElement = element.as<FrameworkElement>();
    StackPanel parentElement = frameworkElement.Parent().as<StackPanel>();
    uint32_t index;

    parentElement.Children().IndexOf(frameworkElement, index);
    return parentElement.Children().GetAt(index + 1);
}
