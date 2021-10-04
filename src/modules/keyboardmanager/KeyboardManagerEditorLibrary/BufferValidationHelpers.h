#pragma once

#include <keyboardmanager/common/Helpers.h>

#include "ShortcutErrorType.h"

namespace BufferValidationHelpers
{
    enum class DropDownAction
    {
        NoAction,
        AddDropDown,
        DeleteDropDown,
        ClearUnusedDropDowns
    };

    // Helper function to verify if a key is being remapped to/from its combined key
    bool IsKeyRemappingToItsCombinedKey(DWORD keyCode1, DWORD keyCode2);

    // Function to validate and update an element of the key remap buffer when the selection has changed
    ShortcutErrorType ValidateAndUpdateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyCode, RemapBuffer& remapBuffer);

    // Function to validate an element of the shortcut remap buffer when the selection has changed
    std::pair<ShortcutErrorType, DropDownAction> ValidateShortcutBufferElement(int rowIndex, int colIndex, uint32_t dropDownIndex, const std::vector<int32_t>& selectedCodes, std::wstring appName, bool isHybridControl, const RemapBuffer& remapBuffer, bool dropDownFound);
}
