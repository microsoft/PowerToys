#pragma once
#include <vector>
#include <winrt/Windows.System.h>

// Function to split a wstring based on a delimiter and return a vector of split strings
std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter);

// Function to return the next sibling element for an element under a stack panel
winrt::Windows::Foundation::IInspectable getSiblingElement(winrt::Windows::Foundation::IInspectable const& element);

// Function to convert an unsigned int vector to hstring by concatenating them
template<typename T>
winrt::hstring convertVectorToHstring(const std::vector<T>& input)
{
    winrt::hstring output;
    for (int i = 0; i < input.size(); i++)
    {
        output = output + winrt::to_hstring((unsigned int)input[i]) + winrt::to_hstring(L" ");
    }
    return output;
}

// Function to convert a wstring vector to a integer vector
template<typename T>
std::vector<T> convertWStringVectorToIntegerVector(const std::vector<std::wstring>& input)
{
    std::vector<T> typeVector;
    for (int i = 0; i < input.size(); i++)
    {
        typeVector.push_back((T)std::stoi(input[i]));
    }

    return typeVector;
}

// Function to return if the key is an extended key which requires the use of the extended key flag
bool isExtendedKey(DWORD key);
