#pragma once
#include "Shortcut.h"

// This class stores all the variables associated with each shortcut remapping
class RemapShortcut
{
public:
    Shortcut targetShortcut;
    bool isShortcutInvoked;
    ModifierKey winKeyInvoked;

    RemapShortcut(const Shortcut& sc) :
        targetShortcut(sc), isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled)
    {
    }

    RemapShortcut() :
        isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled)
    {
    }
};
