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

    void Launch();

private:
    WorkspacesData::WorkspacesProject m_project;
    std::vector<WorkspacesData::WorkspacesProject>& m_workspaces;
    const InvokePoint m_invokePoint;
    const std::chrono::steady_clock::time_point m_start;
    std::unique_ptr<LauncherUIHelper> m_uiHelper;
    std::unique_ptr<WindowArrangerHelper> m_windowArrangerHelper;
    LaunchingStatus m_launchingStatus;
    bool m_launchedSuccessfully{};
    std::vector<std::pair<std::wstring, std::wstring>> m_launchErrors{};

    void handleWindowArrangerMessage(const std::wstring& msg);
};
