#pragma once
#include <vector>
#include <winrt/Windows.System.h>

namespace KeyboardManagerHelper
{
    // Type to distinguish between keys
    enum class KeyType
    {
        Win,
        Ctrl,
        Alt,
        Shift,
        Action
    };

    // Function to split a wstring based on a delimiter and return a vector of split strings
    std::vector<std::wstring> splitwstring(const std::wstring& input, wchar_t delimiter);

    // Function to return the next sibling element for an element under a stack panel
    winrt::Windows::Foundation::IInspectable getSiblingElement(winrt::Windows::Foundation::IInspectable const& element);

    // Function to return if the key is an extended key which requires the use of the extended key flag
    bool isExtendedKey(DWORD key);

    // Function to check if the key is a modifier key
    bool IsModifierKey(DWORD key);

    // Function to get the type of the key
    KeyType GetKeyType(DWORD key);

    // Function to return if the key is an extended key which requires the use of the extended key flag
    bool isExtendedKey(DWORD key);
}
