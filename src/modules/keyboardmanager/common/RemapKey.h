#pragma once
#include "ModifierKey.h"

class RemapKey
{
public:
    DWORD key;
    ModifierKey winKeyInvoked;

    RemapKey() :
        key(0), winKeyInvoked(ModifierKey::Disabled)
    {
    }

    RemapKey(DWORD argKey) :
        key(argKey), winKeyInvoked(ModifierKey::Disabled)
    {
    }
};
