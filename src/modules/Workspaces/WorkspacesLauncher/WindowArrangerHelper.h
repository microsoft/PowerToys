#pragma once

#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <common/utils/OnThreadExecutor.h>

class WindowArrangerHelper
{
public:
    WindowArrangerHelper(std::function<void(const std::wstring&)> ipcCallback);
    ~WindowArrangerHelper();

    void Launch(const std::wstring& projectId, bool elevated, std::function<bool()> allWindowsFoundCallback);

private:
    DWORD processId;
    IPCHelper ipcHelper;
    OnThreadExecutor m_threadExecutor;
};
