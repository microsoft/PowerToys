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
        NotCurrentVirtualDesktop,
        LaunchedByWorkspaces
    };
    
    ProcessabilityType DefineWindowType(HWND window) noexcept;
    bool IsProcessable(HWND window) noexcept;
}