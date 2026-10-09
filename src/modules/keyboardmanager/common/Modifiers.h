#pragma once
#include "ModifierKey.h"

#include <vector>

class Modifiers
{
public:
    ModifierKey winKey = ModifierKey::Disabled;
    ModifierKey ctrlKey = ModifierKey::Disabled;
    ModifierKey altKey = ModifierKey::Disabled;
    ModifierKey shiftKey = ModifierKey::Disabled;

    void Reset()
    {
        winKey = ModifierKey::Disabled;
        ctrlKey = ModifierKey::Disabled;
        altKey = ModifierKey::Disabled;
        shiftKey = ModifierKey::Disabled;
    }

    inline bool operator==(const Modifiers& other) const
    {
        return winKey == other.winKey && ctrlKey == other.ctrlKey && altKey == other.altKey && shiftKey == other.shiftKey;
    }
};
