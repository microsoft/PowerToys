#pragma once
#include <keyboardmanager/common/Shortcut.h>
#include "ShortcutErrorType.h"

using RemapBufferItem = std::array<KeyShortcutTextUnion, 2>;
struct RemapBufferRow
{
    RemapBufferItem mapping;
    std::wstring appName;
    RemapCondition condition{ RemapCondition::Always };
};

using RemapBuffer = std::vector<RemapBufferRow>;
