#pragma once
#include <keyboardmanager/common/ErrorTypes.h>

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    Helpers::ErrorType DoKeysOverlap(DWORD first, DWORD second);

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCodes);
}