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

    // Function to return true if the shortcut is valid. A valid shortcut has at least one modifier, as well as an action key
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
            // Different chord endings can share a prefix. A shortcut without a chord still
            // conflicts with that prefix, since it is invoked before a second key is pressed.
            else if (first.actionKey == second.actionKey &&
                     (!first.HasChord() || !second.HasChord() || first.secondKey == second.secondKey))
            {
                const auto modifiersOverlap = [](ModifierKey firstModifier, ModifierKey secondModifier) {
                    return firstModifier == secondModifier ||
                           (firstModifier != ModifierKey::Disabled && secondModifier != ModifierKey::Disabled &&
                            (firstModifier == ModifierKey::Both || secondModifier == ModifierKey::Both));
                };

                // Every modifier must overlap. A common Ctrl does not make left Shift and
                // right Shift conflict, and different sets of modifier types remain valid.
                if (modifiersOverlap(first.winKey, second.winKey) &&
                    modifiersOverlap(first.ctrlKey, second.ctrlKey) &&
                    modifiersOverlap(first.altKey, second.altKey) &&
                    modifiersOverlap(first.shiftKey, second.shiftKey))
                {
                    return ShortcutErrorType::ConflictingModifierShortcut;
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
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetCtrlKey(ModifierKey::Both)).c_str()));
        }
        if (shortcut.altKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetAltKey(ModifierKey::Both)).c_str()));
        }
        if (shortcut.shiftKey != ModifierKey::Disabled)
        {
            keys.push_back(winrt::to_hstring(keyboardMap.GetKeyName(shortcut.GetShiftKey(ModifierKey::Both)).c_str()));
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
            Logger::info(L"Illegal shortcut detected: Win+L");
            return ShortcutErrorType::WinL;
        }

        // Ctrl+Alt+Del
        if (shortcut.winKey == ModifierKey::Disabled && shortcut.ctrlKey != ModifierKey::Disabled && shortcut.altKey != ModifierKey::Disabled && shortcut.shiftKey == ModifierKey::Disabled && shortcut.actionKey == VK_DELETE)
        {
            Logger::info(L"Illegal shortcut detected: Ctrl+Alt+Del");
            return ShortcutErrorType::CtrlAltDel;
        }

        return ShortcutErrorType::NoError;
    }
}
