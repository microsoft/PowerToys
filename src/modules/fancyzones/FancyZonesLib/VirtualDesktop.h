#pragma once

#include "WorkArea.h"
#include "on_thread_executor.h"

class VirtualDesktop
{
public:
    VirtualDesktop(const std::function<void()>& vdInitCallback, const std::function<void()>& vdUpdatedCallback);
    ~VirtualDesktop() = default;

    void Init();
    void UnInit();

    std::optional<GUID> GetWindowDesktopId(HWND topLevelWindow) const;
    std::optional<GUID> GetCurrentVirtualDesktopId() const;
    std::optional<std::vector<GUID>> GetVirtualDesktopIds() const;

    bool IsWindowOnCurrentDesktop(HWND window) const;
    std::optional<GUID> GetDesktopId(HWND window) const;

private:
    std::function<void()> m_vdInitCallback;
    std::function<void()> m_vdUpdatedCallback;

    IVirtualDesktopManager* m_vdManager;

    OnThreadExecutor m_virtualDesktopTrackerThread;
    wil::unique_handle m_terminateVirtualDesktopTrackerEvent;

    std::optional<std::vector<GUID>> GetVirtualDesktopIds(HKEY hKey) const;
    std::optional<GUID> GetDesktopIdByTopLevelWindows() const;
    void HandleVirtualDesktopUpdates();
};
