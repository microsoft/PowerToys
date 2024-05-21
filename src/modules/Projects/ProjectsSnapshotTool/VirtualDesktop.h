#pragma once

#include <windows.h>
#include <ShObjIdl.h>

class VirtualDesktop
{
public:
    static VirtualDesktop& instance();

    bool IsWindowOnCurrentDesktop(HWND window) const;

private:
    VirtualDesktop();
    ~VirtualDesktop();

    IVirtualDesktopManager* m_vdManager{ nullptr };
};


