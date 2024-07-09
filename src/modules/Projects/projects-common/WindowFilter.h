#pragma once

#include "VirtualDesktop.h"
#include "WindowUtils.h"

namespace WindowFilter
{
    inline bool Filter(HWND window)
    {
        auto style = GetWindowLong(window, GWL_STYLE);
        auto exStyle = GetWindowLong(window, GWL_EXSTYLE);

        if (!WindowUtils::HasStyle(style, WS_VISIBLE))
        {
            return false;
        }

        if (!IsWindowVisible(window))
        {
            return false;
        }

        if (WindowUtils::HasStyle(exStyle, WS_EX_TOOLWINDOW))
        {
            return false;
        }

        if (!WindowUtils::IsRoot(window))
        {
            // child windows such as buttons, combo boxes, etc.
            return false;
        }

        if (!VirtualDesktop::instance().IsWindowOnCurrentDesktop(window))
        {
            return false;
        }

        return true;
    }
}
