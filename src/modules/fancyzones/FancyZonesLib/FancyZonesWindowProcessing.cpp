#include "pch.h"
#include "FancyZonesWindowProcessing.h"

#include <FancyZonesLib/Settings.h>
#include <FancyZonesLib/VirtualDesktop.h>
#include <FancyZonesLib/WindowUtils.h>

FancyZonesWindowProcessing::ProcessabilityType FancyZonesWindowProcessing::DefineWindowType(HWND window) noexcept
{
    const bool windowMinimized = IsIconic(window);
    if (windowMinimized)
    {
        return ProcessabilityType::Minimized;
    }

    auto style = GetWindowLong(window, GWL_STYLE);
    auto exStyle = GetWindowLong(window, GWL_EXSTYLE);

    if (!FancyZonesWindowUtils::HasStyle(style, WS_VISIBLE))
    {
        return ProcessabilityType::NotVisible;
    }

    if (FancyZonesWindowUtils::HasStyle(exStyle, WS_EX_TOOLWINDOW))
    {
        return ProcessabilityType::ToolWindow;
    }

    if (!FancyZonesWindowUtils::IsRoot(window))
    {
        // child windows such as buttons, combo boxes, etc.
        return ProcessabilityType::NonRootWindow;
    }

    bool isPopup = FancyZonesWindowUtils::HasStyle(style, WS_POPUP);
    bool hasThickFrame = FancyZonesWindowUtils::HasStyle(style, WS_THICKFRAME);
    bool hasCaption = FancyZonesWindowUtils::HasStyle(style, WS_CAPTION);
    bool hasMinimizeMaximizeButtons = FancyZonesWindowUtils::HasStyle(style, WS_MINIMIZEBOX) || FancyZonesWindowUtils::HasStyle(style, WS_MAXIMIZEBOX);
    if (isPopup && !(hasThickFrame && (hasCaption || hasMinimizeMaximizeButtons)))
    {
        // popup windows we want to snap: e.g. Calculator, Telegram   
        // popup windows we don't want to snap: start menu, notification popup, tray window, etc.
        // WS_CAPTION, WS_MINIMIZEBOX, WS_MAXIMIZEBOX are used for filtering out menus,
        // e.g., in Edge "Running as admin" menu when creating a new PowerToys issue.
        return ProcessabilityType::NonProcessablePopupWindow;
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
