#pragma once

#include "WorkArea.h"
#include "on_thread_executor.h"

class VirtualDesktop
{
public:
    VirtualDesktop(const std::function<void()>& vdInitCallback, const std::function<void()>& vdUpdatedCallback);
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

    void Init();
    void UnInit();

    std::optional<GUID> GetCurrentVirtualDesktopIdFromRegistry() const;
    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry() const;

    bool IsWindowOnCurrentDesktop(HWND window) const;
    std::optional<GUID> GetDesktopId(HWND window) const;
    std::optional<GUID> GetDesktopIdByTopLevelWindows() const;

    std::vector<std::pair<HWND, GUID>> GetWindowsRelatedToDesktops() const;

private:
    std::function<void()> m_vdInitCallback;
    std::function<void()> m_vdUpdatedCallback;

    IVirtualDesktopManager* m_vdManager;

    OnThreadExecutor m_virtualDesktopTrackerThread;
    wil::unique_handle m_terminateVirtualDesktopTrackerEvent;

    std::optional<std::vector<GUID>> GetVirtualDesktopIdsFromRegistry(HKEY hKey) const;
    void HandleVirtualDesktopUpdates();
};
