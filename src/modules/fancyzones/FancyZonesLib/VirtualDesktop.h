#pragma once

class VirtualDesktop
{
public:
    VirtualDesktop();
    ~VirtualDesktop();

    inline bool IsVirtualDesktopIdSavedInRegistry(GUID id) const
    {
        auto ids = GetVirtualDesktopIdsFromRegistry();
        if (!ids.has_value())
        {
            return false;
        }

        for (const auto& regId : *ids)
        {
            if (regId == id)
            {
                return true;
            }
        }

        return false;
    }

    std::optional<GUID> GetCurrentVirtualDesktopIdFromRegistry() const;
    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry() const;

    bool IsWindowOnCurrentDesktop(HWND window) const;
    std::optional<GUID> GetDesktopId(HWND window) const;
    std::optional<GUID> GetDesktopIdByTopLevelWindows() const;

    std::vector<std::pair<HWND, GUID>> GetWindowsRelatedToDesktops() const;

private:
    IVirtualDesktopManager* m_vdManager;

    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry(HKEY hKey) const;
};
