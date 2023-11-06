#include "pch.h"
#include "FancyZonesWindowProcessing.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowUtils.h>

FancyZonesWindowProcessing::ProcessabilityType FancyZonesWindowProcessing::DefineWindowType(HWND window) noexcept
{
    const bool isSplashScreen = FancyZonesWindowUtils::IsSplashScreen(window);
    if (isSplashScreen)
    {
        return ProcessabilityType::SplashScreen;
    }

    const bool windowMinimized = IsIconic(window);
    if (windowMinimized)
    {
        return ProcessabilityType::Minimized;
    }

    const bool standard = FancyZonesWindowUtils::IsStandardWindow(window);
    if (!standard)
    {
        return ProcessabilityType::NonStandardWindow;
    }

    bool isPopup = FancyZonesWindowUtils::IsPopupWindow(window);
    bool hasThickFrame = FancyZonesWindowUtils::HasThickFrame(window);
    bool hasMinimizeMaximizeButtons = FancyZonesWindowUtils::HasMinimizeMaximizeButtons(window); 
    if (isPopup && !(hasThickFrame && hasMinimizeMaximizeButtons))
    {
        // popup windows we want to snap: e.g. Calculator, Telegram   
        // popup windows we don't want to snap: start menu, notification popup, tray window, etc.
        // minimize maximize buttons are used for filtering out menus,
        // e.g., in Edge "Running as admin" menu when creating a new PowerToys issue.
        return ProcessabilityType::PopupMenu;
    }

    // allow child windows
    auto hasOwner = FancyZonesWindowUtils::HasVisibleOwner(window);
    if (hasOwner && !FancyZonesSettings::settings().allowSnapChildWindows)
    {
        return ProcessabilityType::ChildWindow;
    }

    if (FancyZonesWindowUtils::IsExcluded(window))
    {
        return ProcessabilityType::Excluded;
    }

    // Switch between virtual desktops results with posting same windows messages that also indicate
    // creation of new window. We need to check if window being processed is on currently active desktop.
    if (!VirtualDesktop::instance().IsWindowOnCurrentDesktop(window))
    {
        return ProcessabilityType::NotCurrentVirtualDesktop;
    }

    return ProcessabilityType::Processable;
}

bool FancyZonesWindowProcessing::IsProcessable(HWND window) noexcept
{
    return DefineWindowType(window) == ProcessabilityType::Processable;
}
