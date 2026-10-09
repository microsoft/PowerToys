#pragma once

#include <ShObjIdl.h>

class VirtualDesktopUtils
{
public:
    VirtualDesktopUtils();
    ~VirtualDesktopUtils();

    bool IsWindowOnCurrentDesktop(HWND window) const;

    std::optional<GUID> GetDesktopId(HWND window) const;

private:
    IVirtualDesktopManager* m_vdManager;
};
