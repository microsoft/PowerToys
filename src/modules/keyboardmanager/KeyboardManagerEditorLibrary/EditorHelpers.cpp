#include "pch.h"
#include <keyboardmanager/common/ErrorTypes.h>
#include <keyboardmanager/common/Helpers.h>

using Helpers::ErrorType;
using Helpers::GetKeyType;

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ErrorType DoKeysOverlap(DWORD first, DWORD second)
    {
        // If the keys are same
        if (first == second)
        {
            return ErrorType::SameKeyPreviouslyMapped;
        }
        else if ((GetKeyType(first) == GetKeyType(second)) && GetKeyType(first) != Helpers::KeyType::Action)
        {
            // If the keys are of the same modifier type and overlapping, i.e. one is L/R and other is common
            if (((first == VK_LWIN && second == VK_RWIN) || (first == VK_RWIN && second == VK_LWIN)) || ((first == VK_LCONTROL && second == VK_RCONTROL) || (first == VK_RCONTROL && second == VK_LCONTROL)) || ((first == VK_LMENU && second == VK_RMENU) || (first == VK_RMENU && second == VK_LMENU)) || ((first == VK_LSHIFT && second == VK_RSHIFT) || (first == VK_RSHIFT && second == VK_LSHIFT)))
            {
                return ErrorType::NoError;
            }
            else
            {
                return ErrorType::ConflictingModifierKey;
            }
        }
        // If no overlap
        else
        {
            return ErrorType::NoError;
        }
    }
}