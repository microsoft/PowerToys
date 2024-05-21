#include "pch.h"
#include "VirtualDesktop.h"

#include <iostream>

VirtualDesktop::VirtualDesktop()
{
    auto res = CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&m_vdManager));
    if (FAILED(res))
    {
        // Logger::error("Failed to create VirtualDesktopManager instance");
        std::cout << "Failed to create VirtualDesktopManager instance\n";
    }
}

VirtualDesktop::~VirtualDesktop()
{
    if (m_vdManager)
    {
        m_vdManager->Release();
    }
}

VirtualDesktop& VirtualDesktop::instance()
{
    static VirtualDesktop self;
    return self;
}

bool VirtualDesktop::IsWindowOnCurrentDesktop(HWND window) const
{
    BOOL isWindowOnCurrentDesktop = false;
    if (m_vdManager)
    {
        m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop);
    }

    return isWindowOnCurrentDesktop;
}