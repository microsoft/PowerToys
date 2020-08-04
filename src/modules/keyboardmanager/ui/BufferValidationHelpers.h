#pragma once
#include "keyboardmanager/common/Helpers.h"
#include <variant>
#include <vector>
#include "keyboardmanager/common/Shortcut.h"

namespace BufferValidationHelpers
{
    enum class DropDownAction
    {
        NoAction,
        AddDropDown,
        DeleteDropDown,
        ClearUnusedDropDowns
    };

    // Function to validate and update an element of the key remap buffer when the selection has changed
    KeyboardManagerHelper::ErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyIndex, std::vector<DWORD>& keyCodeList, std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer);

    // Function to validate an element of the shortcut remap buffer when the selection has changed
    std::pair<KeyboardManagerHelper::ErrorType, DropDownAction> ValidateShortcutBufferElement(int rowIndex, int colIndex, uint32_t dropDownIndex, bool dropDownFound, int selectedKeyIndex, uint32_t dropDownCount, std::vector<DWORD>& selectedKeyCodes, std::vector<int32_t>& selectedIndices, std::wstring& appName, std::vector<DWORD>& keyCodeList, std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer, bool isHybridControl);
}
