#pragma once

namespace FancyZonesWindowProcessing
{
    enum class ProcessabilityType
    {
        Processable = 0,
        SplashScreen,
        Minimized,
        ToolWindow,
        NotVisible,
        NonRootWindow,
        NonProcessablePopupWindow,
        ChildWindow,
        Excluded,
        NotCurrentVirtualDesktop
    };
    
    ProcessabilityType DefineWindowType(HWND window) noexcept;
    bool IsProcessableAutomatically(HWND window) noexcept;
    bool IsProcessableManually(HWND window) noexcept;
}