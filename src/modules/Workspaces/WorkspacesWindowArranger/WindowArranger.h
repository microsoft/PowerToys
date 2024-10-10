#pragma once

#include <WindowCreationHandler.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/WorkspacesData.h>

class WindowArranger
{
public:
    WindowArranger(WorkspacesData::WorkspacesProject project);
    ~WindowArranger() = default;

private:
    const WorkspacesData::WorkspacesProject m_project;
    const std::vector<HWND> m_windowsBefore;
    const std::vector<WorkspacesData::WorkspacesProject::Monitor> m_monitors;
    const Utils::Apps::AppList m_installedApps;
    //const WindowCreationHandler m_windowCreationHandler;
    IPCHelper m_ipcHelper;
    LaunchingStatus m_launchingStatus;
    
    //void onWindowCreated(HWND window);
    void processWindows(bool processAll);
    void processWindow(HWND window);
    bool moveWindow(HWND window, const WorkspacesData::WorkspacesProject::Application& app);

    void receiveIpcMessage(const std::wstring& message);
    void sendUpdatedState(const WorkspacesData::LaunchingAppState& data) const;
};
