#pragma once

#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <common/utils/OnThreadExecutor.h>

class WindowArrangerHelper
{
public:
    WindowArrangerHelper(std::function<void(const std::wstring&)> ipcCallback);
    ~WindowArrangerHelper();

    void Launch(const std::wstring& projectId, bool elevated, std::function<bool()> keepWaitingCallback);
    void UpdateLaunchStatus(const WorkspacesData::LaunchingAppState& appState) const;

private:
    DWORD m_processId;
    IPCHelper m_ipcHelper;
    OnThreadExecutor m_threadExecutor;
};
