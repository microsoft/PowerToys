#pragma once

#include <atomic>

#include <WindowCreationHandler.h>

#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/IPCHelper.h>
#include <WorkspacesLib/LaunchingStatus.h>
#include <WorkspacesLib/PwaHelper.h>
#include <WorkspacesLib/WorkspacesData.h>

struct WindowWithDistance
{
    int distance;
    HWND window;
};

class WindowArranger
{
public:
    WindowArranger(WorkspacesData::WorkspacesProject project, std::unique_ptr<IPCHelper> cliPipe = nullptr, std::wstring operationId = L"");
    ~WindowArranger() { m_ipcHelper.reset(); }

private:
    const WorkspacesData::WorkspacesProject m_project;
    const std::wstring m_operationId;
    const std::vector<HWND> m_windowsBefore;
    const std::vector<WorkspacesData::WorkspacesProject::Monitor> m_monitors;
    const Utils::Apps::AppList m_installedApps;
    //const WindowCreationHandler m_windowCreationHandler;
    std::atomic<ULONGLONG> m_launchHeartbeat{};
    std::unique_ptr<IPCHelper> m_ipcHelper;
    LaunchingStatus m_launchingStatus;
    std::atomic<bool> m_resultAcknowledged{};
    std::optional<WindowWithDistance> GetNearestWindow(const WorkspacesData::WorkspacesProject::Application& app, const std::vector<HWND>& movedWindows, Utils::PwaHelper& pwaHelper);
    bool TryMoveWindow(const WorkspacesData::WorkspacesProject::Application& app, HWND windowToMove);

    //void onWindowCreated(HWND window);
    bool processWindows(bool processAll);
    bool processWindow(HWND window);
    bool moveWindow(HWND window, const WorkspacesData::WorkspacesProject::Application& app);

    void receiveIpcMessage(const std::wstring& message);
    void sendUpdatedState(const WorkspacesData::LaunchingAppState& data) const;
};
