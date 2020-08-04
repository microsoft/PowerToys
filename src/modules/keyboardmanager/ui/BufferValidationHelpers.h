#pragma once
#include "keyboardmanager/common/Helpers.h"
#include <variant>
#include <vector>
#include "keyboardmanager/common/Shortcut.h"

namespace BufferValidationHelpers
{
    // Function to validate an element of the key remap buffer when the selection has changed
    KeyboardManagerHelper::ErrorType ValidateKeyBufferElement(int rowIndex, int colIndex, int selectedKeyIndex, std::vector<DWORD>& keyCodeList, std::vector<std::pair<std::vector<std::variant<DWORD, Shortcut>>, std::wstring>>& remapBuffer);
}
