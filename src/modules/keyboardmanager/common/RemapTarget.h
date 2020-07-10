#pragma once
#include "RemapKey.h"
#include "RemapShortcut.h"

enum class RemapType
{
    Key,
    Shortcut
};

class RemapTarget
{
    RemapType type;
    union
    {
        RemapKey key;
        RemapShortcut shortcut;
    };
};
