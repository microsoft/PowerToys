#pragma once

#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/WorkspacesData.h>

#include <workspaces-common/InvokePoint.h>

#include <LauncherUIHelper.h>
#include <WindowArrangerHelper.h>
#include <WorkspacesLib/PendingLaunchApproval.h>
#include <WorkspacesLib/SignatureVerification.h>
#include <thread>

struct CliLaunchOptions
{
    std::wstring operationId;
    HANDLE cancelEvent = nullptr;
    ULONGLONG deadline = 0;
    DWORD timeoutSeconds = 120;
    std::function<LaunchDecision(const std::wstring&, const std::wstring&, const std::wstring&,
                                const SignatureVerification::Result&)> requestApproval;
};

class Launcher
{
public:
    Launcher(const WorkspacesData::WorkspacesProject& project, std::vector<WorkspacesData::WorkspacesProject>& workspaces, InvokePoint invokePoint, const CliLaunchOptions* cliOptions = nullptr);
    ~Launcher();
    WorkspacesData::LaunchingAppStateMap GetResult();
    std::map<std::wstring, DWORD> GetApplicationErrors();
    std::map<std::wstring, LaunchDecision> GetApprovalDecisions();
    DWORD GetArrangerError() const { return m_arrangerError; }
    bool HasArrangerResult() const { return m_arrangerCompleted; }

private:
    WorkspacesData::WorkspacesProject m_project;
    std::vector<WorkspacesData::WorkspacesProject>& m_workspaces;
    const InvokePoint m_invokePoint;
    const std::chrono::steady_clock::time_point m_start;
    std::atomic<bool> m_launchedSuccessfully{};
    LaunchingStatus m_launchingStatus;
    PendingLaunchApproval m_approval;
    std::atomic<bool> m_launchStarted{};
    std::mutex m_launchThreadMutex;
    std::thread m_launchThread;

    std::unique_ptr<LauncherUIHelper> m_uiHelper;
    std::mutex m_uiHelperMutex;

    std::unique_ptr<WindowArrangerHelper> m_windowArrangerHelper;
    std::mutex m_windowArrangerHelperMutex;
    
    std::vector<std::pair<std::wstring, std::wstring>> m_launchErrors{};
    std::mutex m_launchErrorsMutex;
    std::map<std::wstring, DWORD> m_applicationErrors;
    std::map<std::wstring, LaunchDecision> m_approvalDecisions;
    const CliLaunchOptions* m_cliOptions = nullptr;
    std::atomic<bool> m_stopping{};
    std::atomic<bool> m_arrangerCompleted{};
    DWORD m_arrangerError = ERROR_SUCCESS;

    bool ShouldStop() const;

    void Launch();
    void handleWindowArrangerMessage(const std::wstring& msg);
    void handleUIMessage(const std::wstring& msg);
    LaunchDecision requestApproval(const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result);
};
