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
    KeyboardManagerHelper::ErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyIndex, const std::vector<DWORD>& keyCodeList, std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer);

    // Function to validate an element of the shortcut remap buffer when the selection has changed
    std::pair<KeyboardManagerHelper::ErrorType, DropDownAction> ValidateShortcutBufferElement(int rowIndex, int colIndex, uint32_t dropDownIndex, bool dropDownFound, int selectedKeyIndex, const std::vector<int32_t>& selectedIndices, std::wstring, const std::vector<DWORD>& keyCodeList, const std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer, bool isHybridControl);
}
