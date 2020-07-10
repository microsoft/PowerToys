#pragma once
#include "ModifierKey.h"

class RemapKey
{
    DWORD key;
    ModifierKey winKeyInvoked;

    RemapKey() :
        key(0), winKeyInvoked(ModifierKey::Disabled)
    {
    }
};
