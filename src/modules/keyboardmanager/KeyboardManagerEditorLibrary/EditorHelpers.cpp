#include "pch.h"
#include <common/interop/keyboard_layout.h>
#include <keyboardmanager/common/Helpers.h>

#include "ShortcutErrorType.h"

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

    // Function to return true if the shortcut is valid. A valid shortcut has atleast one modifier, as well as an action key
    bool IsValidShortcut(Shortcut shortcut)
    {
        if (shortcut.operationType == Shortcut::OperationType::RunProgram && shortcut.runProgramFilePath.length() > 0)
        {
            return true;
        }

        if (shortcut.operationType == Shortcut::OperationType::OpenURI && shortcut.uriToOpen.length() > 0)
        {
            return true;
        }

        if (shortcut.actionKey != NULL)
        {
            if (shortcut.winKey != ModifierKey::Disabled || shortcut.ctrlKey != ModifierKey::Disabled || shortcut.altKey != ModifierKey::Disabled || shortcut.shiftKey != ModifierKey::Disabled)
            {
                return true;
            }
        }

        return false;
    }

    // Function to check if the two shortcuts are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoShortcutsOverlap(const Shortcut& first, const Shortcut& second)
    {
        if (IsValidShortcut(first) && IsValidShortcut(second))
        {
            // If the shortcuts are equal
            if (first == second)
            {
                return ShortcutErrorType::SameShortcutPreviouslyMapped;
            }
            // action keys match
            else if (first.actionKey == second.actionKey)
            {
                // corresponding modifiers are either both disabled or both not disabled - this ensures that both match in types of modifiers i.e. Ctrl(l/r/c) Shift (l/r/c) A matches Ctrl(l/r/c) Shift (l/r/c) A
                if (((first.winKey != ModifierKey::Disabled && second.winKey != ModifierKey::Disabled) || (first.winKey == ModifierKey::Disabled && second.winKey == ModifierKey::Disabled)) &&
                    ((first.ctrlKey != ModifierKey::Disabled && second.ctrlKey != ModifierKey::Disabled) || (first.ctrlKey == ModifierKey::Disabled && second.ctrlKey == ModifierKey::Disabled)) &&
                    ((first.altKey != ModifierKey::Disabled && second.altKey != ModifierKey::Disabled) || (first.altKey == ModifierKey::Disabled && second.altKey == ModifierKey::Disabled)) &&
                    ((first.shiftKey != ModifierKey::Disabled && second.shiftKey != ModifierKey::Disabled) || (first.shiftKey == ModifierKey::Disabled && second.shiftKey == ModifierKey::Disabled)))
                {
                    // If one of the modifier is common
                    if ((first.winKey == ModifierKey::Both || second.winKey == ModifierKey::Both) ||
                        (first.ctrlKey == ModifierKey::Both || second.ctrlKey == ModifierKey::Both) ||
                        (first.altKey == ModifierKey::Both || second.altKey == ModifierKey::Both) ||
                        (first.shiftKey == ModifierKey::Both || second.shiftKey == ModifierKey::Both))
                    {
                        return ShortcutErrorType::ConflictingModifierShortcut;
                    }
                }
            }
        }

        return ShortcutErrorType::NoError;
    }

    // Function to return a vector of hstring for each key in the display order
    std::vector<winrt::hstring> GetKeyVector(Shortcut shortcut, LayoutMap& keyboardMap)
    {
        std::vector<winrt::hstring> keys;
        if (shortcut.winKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetWinKey(ModifierKey::Both)).c_str()));
        }
        if (shortcut.ctrlKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetCtrlKey()).c_str()));
        }
        if (shortcut.altKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetAltKey()).c_str()));
        }
        if (shortcut.shiftKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetShiftKey()).c_str()));
        }
        if (shortcut.actionKey != NULL)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.actionKey).c_str()));
        }
        return keys;
    }

    // Function to check if the shortcut is illegal (i.e. Win+L or Ctrl+Alt+Del)
    ShortcutErrorType IsShortcutIllegal(Shortcut shortcut)
    {
        // Win+L
        if (shortcut.winKey != ModifierKey::Disabled && shortcut.ctrlKey == ModifierKey::Disabled && shortcut.altKey == ModifierKey::Disabled && shortcut.shiftKey == ModifierKey::Disabled && shortcut.actionKey == 0x4C)
        {
            return ShortcutErrorType::WinL;
        }

        // Ctrl+Alt+Del
        if (shortcut.winKey == ModifierKey::Disabled && shortcut.ctrlKey != ModifierKey::Disabled && shortcut.altKey != ModifierKey::Disabled && shortcut.shiftKey == ModifierKey::Disabled && shortcut.actionKey == VK_DELETE)
        {
            return ShortcutErrorType::CtrlAltDel;
        }

        return ShortcutErrorType::NoError;
    }
}
