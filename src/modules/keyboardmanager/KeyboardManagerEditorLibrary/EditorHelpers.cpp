#include "pch.h"
#include <keyboardmanager/common/ShortcutErrorType.h>
#include <keyboardmanager/common/Helpers.h>

using Helpers::GetKeyType;

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoKeysOverlap(DWORD first, DWORD second)
    {
        // If the keys are same
        if (first == second)
        {
            return ShortcutErrorType::SameKeyPreviouslyMapped;
        }
        else if ((GetKeyType(first) == GetKeyType(second)) && GetKeyType(first) != Helpers::KeyType::Action)
        {
            // If the keys are of the same modifier type and overlapping, i.e. one is L/R and other is common
            if (((first == VK_LWIN && second == VK_RWIN) || (first == VK_RWIN && second == VK_LWIN)) || ((first == VK_LCONTROL && second == VK_RCONTROL) || (first == VK_RCONTROL && second == VK_LCONTROL)) || ((first == VK_LMENU && second == VK_RMENU) || (first == VK_RMENU && second == VK_LMENU)) || ((first == VK_LSHIFT && second == VK_RSHIFT) || (first == VK_RSHIFT && second == VK_LSHIFT)))
            {
                return ShortcutErrorType::NoError;
            }
            else
            {
                return ShortcutErrorType::ConflictingModifierKey;
            }
        }
        // If no overlap
        else
        {
            return ShortcutErrorType::NoError;
        }
    }

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCode)
    {
        // Count the number of keys that are equal to 'selectedKeyCode'
        int numberOfSameType = 0;
        for (int i = 0; i < currentKeys.size(); i++)
        {
            numberOfSameType += Helpers::GetKeyType(selectedKeyCode) == Helpers::GetKeyType(currentKeys[i]);
        }

        // If we have at least two keys equal to 'selectedKeyCode' than modifier was repeated
        return numberOfSameType > 1;
    }
}