#pragma once

#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <workspaces-common/InvokePoint.h>

#include <LauncherUIHelper.h>
#include <WindowArrangerHelper.h>

class Launcher
{
public:
    Launcher(const WorkspacesData::WorkspacesProject& project, std::vector<WorkspacesData::WorkspacesProject>& workspaces, InvokePoint invokePoint);
    ~Launcher();

private:
    WorkspacesData::WorkspacesProject m_project;
    std::vector<WorkspacesData::WorkspacesProject>& m_workspaces;
    const InvokePoint m_invokePoint;
    const std::chrono::steady_clock::time_point m_start;
    std::atomic<bool> m_launchedSuccessfully{};
    LaunchingStatus m_launchingStatus;

    std::unique_ptr<LauncherUIHelper> m_uiHelper;
    std::mutex m_uiHelperMutex;

    std::unique_ptr<WindowArrangerHelper> m_windowArrangerHelper;
    std::mutex m_windowArrangerHelperMutex;
    
    std::vector<std::pair<std::wstring, std::wstring>> m_launchErrors{};
    std::mutex m_launchErrorsMutex;

    void Launch();
    void handleWindowArrangerMessage(const std::wstring& msg);
    void handleUIMessage(const std::wstring& msg);
};
