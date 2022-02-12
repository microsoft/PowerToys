#pragma once
#include <keyboardmanager/common/Shortcut.h>
#include "ShortcutErrorType.h"

using RemapBufferItem = std::array<KeyShortcutUnion, 2>;
using ShortcutErrorHandler = std::function<void(ShortcutErrorType, PCWSTR)>;
struct RemapBufferRow
{
    RemapBufferItem mapping;
    std::wstring appName;
    RemapCondition condition{ RemapCondition::Always };
    ShortcutErrorHandler errorHandler;
};

using RemapBuffer = std::vector<RemapBufferRow>;
