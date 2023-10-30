#include "pch.h"
#include "FancyZonesWindowProcessing.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowUtils.h>

bool FancyZonesWindowProcessing::IsProcessable(HWND window) noexcept
{
    const bool isSplashScreen = FancyZonesWindowUtils::IsSplashScreen(window);
    if (isSplashScreen)
    {
        return false;
    }

    const bool windowMinimized = IsIconic(window);
    if (windowMinimized)
    {
        return false;
    }

    const bool standard = FancyZonesWindowUtils::IsStandardWindow(window);
    if (!standard)
    {
        return false;
    }

    bool isPopup = FancyZonesWindowUtils::IsPopupWindow(window);
    bool hasThickFrame = FancyZonesWindowUtils::HasThickFrame(window);
    bool hasMinimizeMaximizeButtons = FancyZonesWindowUtils::HasMinimizeMaximizeButtons(window); 
    if (isPopup)
    {
        if (hasThickFrame && hasMinimizeMaximizeButtons)
        {
            // popup could be the windows we want to snap disregarding the "allowSnapPopupWindows" setting, e.g. Calculator, Telegram   
        }
        else if (!FancyZonesSettings::settings().allowSnapPopupWindows || !hasThickFrame || !hasMinimizeMaximizeButtons)
        {
            // popup could be the window we don't want to snap: start menu, notification popup, tray window, etc.
            // minimize maximize buttons are used for filtering out menus, 
            // e.g., in Edge "Running as admin" menu when creating a new PowerToys issue.
            return false;
        }
    }

    // allow child windows
    auto hasOwner = FancyZonesWindowUtils::HasVisibleOwner(window);
    if (hasOwner && !FancyZonesSettings::settings().allowSnapChildWindows)
    {
        return false;
    }

    if (FancyZonesWindowUtils::IsExcluded(window))
    {
        return false;
    }

    // Switch between virtual desktops results with posting same windows messages that also indicate
    // creation of new window. We need to check if window being processed is on currently active desktop.
    if (!VirtualDesktop::instance().IsWindowOnCurrentDesktop(window))
    {
        return false;
    }

    return true;
}
