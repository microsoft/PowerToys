#pragma once

class VirtualDesktop
{
public:
    static VirtualDesktop& instance();

    // saved values
    GUID GetCurrentVirtualDesktopId() const noexcept;
    GUID GetPreviousVirtualDesktopId() const noexcept;
    void UpdateVirtualDesktopId() noexcept;

    // IVirtualDesktopManager
    bool IsWindowOnCurrentDesktop(HWND window) const;
    std::optional<GUID> GetDesktopId(HWND window) const;
    std::optional<GUID> GetDesktopIdByTopLevelWindows() const;
    std::vector<std::pair<HWND, GUID>> GetWindowsRelatedToDesktops() const;
    std::vector<HWND> GetWindowsFromCurrentDesktop() const;

    // registry
    std::optional<GUID> GetCurrentVirtualDesktopIdFromRegistry() const;
    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry() const;
    bool IsVirtualDesktopIdSavedInRegistry(GUID id) const;

private:
    VirtualDesktop();
    ~VirtualDesktop();

    IVirtualDesktopManager* m_vdManager{nullptr};

    GUID m_currentVirtualDesktopId{};
    GUID m_previousDesktopId{};

    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry(HKEY hKey) const;
};
