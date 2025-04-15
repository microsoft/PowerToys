#pragma once

#include <windows.h>
#include <ShObjIdl.h>
#include <iostream>

class VirtualDesktop
{
public:
    static VirtualDesktop& instance()
    {
        static VirtualDesktop self;
        return self;
    }

    bool IsWindowOnCurrentDesktop(HWND window) const
    {
        BOOL isWindowOnCurrentDesktop = false;
        if (m_vdManager)
        {
            m_vdManager->IsWindowOnCurrentVirtualDesktop(window, &isWindowOnCurrentDesktop);
        }

        return isWindowOnCurrentDesktop;
    }

private:
    VirtualDesktop()
    {
        auto res = CoCreateInstance(CLSID_VirtualDesktopManager, nullptr, CLSCTX_ALL, IID_PPV_ARGS(&m_vdManager));
        if (FAILED(res))
        {
            // Logger::error("Failed to create VirtualDesktopManager instance");
            std::cout << "Failed to create VirtualDesktopManager instance\n";
        }
    }

    ~VirtualDesktop()
    {
        if (m_vdManager)
        {
            m_vdManager->Release();
        }
    }

    IVirtualDesktopManager* m_vdManager{ nullptr };
};


