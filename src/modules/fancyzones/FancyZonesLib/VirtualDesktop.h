#pragma once

class VirtualDesktop
{
public:
    static VirtualDesktop& instance();

    // IVirtualDesktopManager
    bool IsWindowOnCurrentDesktop(HWND window) const;
    std::vector<HWND> GetWindowsFromCurrentDesktop() const;

    // registry
    GUID GetCurrentVirtualDesktopIdFromRegistry() const;
    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry() const;
    
private:
    VirtualDesktop();
    ~VirtualDesktop();

    IVirtualDesktopManager* m_vdManager{nullptr};

    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry(HKEY hKey) const;
};
