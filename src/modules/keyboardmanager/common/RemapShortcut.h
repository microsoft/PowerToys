#pragma once
#include "Shortcut.h"
#include <variant>

// This class stores all the variables associated with each shortcut remapping
class RemapShortcut
{
public:
    KeyShortcutTextUnion targetShortcut;
    bool isShortcutInvoked;
    ModifierKey winKeyInvoked;
    // This bool value is only required for remapping shortcuts to Disable
    bool isOriginalActionKeyPressed;

    RemapShortcut(const KeyShortcutTextUnion& sc) :
        targetShortcut(sc), isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled), isOriginalActionKeyPressed(false)
    {
    }

    RemapShortcut() :
        targetShortcut(Shortcut()), isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled), isOriginalActionKeyPressed(false)
    {
    }

    inline bool operator==(const RemapShortcut& sc) const
    {
        return targetShortcut == sc.targetShortcut && isShortcutInvoked == sc.isShortcutInvoked && winKeyInvoked == sc.winKeyInvoked;
    }

    bool RemapToKey()
    {
        return targetShortcut.index() == 0;
    }
};
