#pragma once
#include "Shortcut.h"
#include "Modifiers.h"
#include <variant>
#include <vector>

// This class stores all the variables associated with each shortcut remapping
class RemapShortcut
{
public:
    KeyShortcutTextUnion targetShortcut;
    bool isShortcutInvoked;

    Modifiers modifierKeysInvoked;
    // This bool value is only required for remapping shortcuts to Disable
    bool isOriginalActionKeyPressed;

    RemapShortcut(const KeyShortcutTextUnion& sc) :
        targetShortcut(sc), isShortcutInvoked(false), isOriginalActionKeyPressed(false)
    {
    }

    RemapShortcut() :
        targetShortcut(Shortcut()), isShortcutInvoked(false), isOriginalActionKeyPressed(false)
    {
    }

    inline bool operator==(const RemapShortcut& sc) const
    {
        return targetShortcut == sc.targetShortcut && isShortcutInvoked == sc.isShortcutInvoked && modifierKeysInvoked == sc.modifierKeysInvoked;
    }

    bool RemapToKey()
    {
        return targetShortcut.index() == 0;
    }
};
