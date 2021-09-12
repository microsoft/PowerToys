#pragma once
#include <keyboardmanager/common/Shortcut.h>

#include "ShortcutErrorType.h"

namespace EditorHelpers
{
    // Function to check if two keys are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoKeysOverlap(DWORD first, DWORD second);

    // Function to check if a modifier has been repeated in the previous drop downs
    bool CheckRepeatedModifier(const std::vector<int32_t>& currentKeys, int selectedKeyCodes);

    // Function to return true if the shortcut is valid. A valid shortcut has atleast one modifier, as well as an action key
    bool IsValidShortcut(Shortcut shortcut);

    // Function to check if the two shortcuts are equal or cover the same set of keys. Return value depends on type of overlap
    ShortcutErrorType DoShortcutsOverlap(const Shortcut& first, const Shortcut& second);

    // Function to return a vector of hstring for each key in the display order
    std::vector<winrt::hstring> GetKeyVector(Shortcut shortcut, LayoutMap& keyboardMap);

    // Function to check if the shortcut is illegal (i.e. Win+L or Ctrl+Alt+Del)
    ShortcutErrorType IsShortcutIllegal(Shortcut shortcut);
}