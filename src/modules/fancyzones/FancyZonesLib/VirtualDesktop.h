#pragma once

#include <thread>

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

class VirtualDesktopWatcher
{
public:
    ~VirtualDesktopWatcher();

    void Start(HWND window) noexcept;
    void Stop() noexcept;

private:
    wil::unique_event m_stopEvent;
    std::thread m_thread;
};
