#pragma once
#include <keyboardmanager/common/ErrorTypes.h>

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    Helpers::ErrorType DoKeysOverlap(DWORD first, DWORD second);
}