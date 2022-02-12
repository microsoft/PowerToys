#pragma once
#include <keyboardmanager/common/Shortcut.h>

#include "ShortcutErrorType.h"

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoKeysOverlap(DWORD first, DWORD second);

    constexpr bool DoConditionsOverlap(RemapCondition first, RemapCondition second)
    {
        return (first != RemapCondition::Always) && (second != RemapCondition::Always) && (first != second);
    }

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCodes);

    // Function to check if the two shortcuts are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoShortcutsOverlap(const Shortcut& first, const Shortcut& second);

    // Function to return a vector of hstring for each key in the display order
    std::vector<winrt::hstring> GetKeyVector(Shortcut shortcut, LayoutMap& keyboardMap);

    // Function to check if the shortcut is illegal (i.e. Win+L or Ctrl+Alt+Del)
    ShortcutErrorType IsShortcutIllegal(Shortcut shortcut);
}