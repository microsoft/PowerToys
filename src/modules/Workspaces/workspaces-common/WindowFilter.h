#pragma once

#include "VirtualDesktop.h"
#include "WindowUtils.h"

namespace WindowFilter
{
    inline bool FilterPopup(HWND window)
    {
        auto style = GetWindowLong(window, GWL_STYLE);
        bool isPopup = WindowUtils::HasStyle(style, WS_POPUP);
        bool hasThickFrame = WindowUtils::HasStyle(style, WS_THICKFRAME);
        bool hasCaption = WindowUtils::HasStyle(style, WS_CAPTION);
        bool hasMinimizeMaximizeButtons = WindowUtils::HasStyle(style, WS_MINIMIZEBOX) || WindowUtils::HasStyle(style, WS_MAXIMIZEBOX);
        if (isPopup && !(hasThickFrame && (hasCaption || hasMinimizeMaximizeButtons)))
        {
            // popup windows we want to snap: e.g. Calculator, Telegram
            // popup windows we don't want to snap: start menu, notification popup, tray window, etc.
            // WS_CAPTION, WS_MINIMIZEBOX, WS_MAXIMIZEBOX are used for filtering out menus,
            // e.g., in Edge "Running as admin" menu when creating a new PowerToys issue.
            return true;
        }

        return false;
    }

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
