#pragma once
#include "Shortcut.h"
#include <variant>

// This class stores all the variables associated with each shortcut remapping
class RemapShortcut
{
public:
    std::variant<DWORD, Shortcut> targetShortcut;
    bool isShortcutInvoked;
    ModifierKey winKeyInvoked;

    RemapShortcut(const std::variant<DWORD, Shortcut>& sc) :
        targetShortcut(sc), isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled)
    {
    }

    RemapShortcut() :
        targetShortcut(Shortcut()), isShortcutInvoked(false), winKeyInvoked(ModifierKey::Disabled)
    {
    }

    inline bool operator==(const RemapShortcut& sc) const
    {
        return targetShortcut == sc.targetShortcut && isShortcutInvoked == sc.isShortcutInvoked && winKeyInvoked == sc.winKeyInvoked;
    }
};
