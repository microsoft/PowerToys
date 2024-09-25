#include "pch.h"
#include "Launcher.h"

#include <common/utils/json.h>

#include <workspaces-common/MonitorUtils.h>

#include <WorkspacesLib/trace.h>

#include <AppLauncher.h>

Launcher::Launcher(const WorkspacesData::WorkspacesProject& project, 
    std::vector<WorkspacesData::WorkspacesProject>& workspaces,
    InvokePoint invokePoint) :
    m_project(project),
    m_workspaces(workspaces),
    m_invokePoint(invokePoint),
    m_start(std::chrono::high_resolution_clock::now()),
    m_uiHelper(std::make_unique<LauncherUIHelper>()),
    m_windowArrangerHelper(std::make_unique<WindowArrangerHelper>(std::bind(&Launcher::handleWindowArrangerMessage, this, std::placeholders::_1))),
    m_launchingStatus(m_project, std::bind(&LauncherUIHelper::UpdateLaunchStatus, m_uiHelper.get(), std::placeholders::_1))
{
    m_uiHelper->LaunchUI();
    m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());

    bool launchElevated = std::find_if(m_project.apps.begin(), m_project.apps.end(), [](const WorkspacesData::WorkspacesProject::Application& app) { return app.isElevated; }) != m_project.apps.end();
    m_windowArrangerHelper->Launch(m_project.id, launchElevated, [&]() -> bool
        {
            if (m_launchingStatus.AllLaunchedAndMoved())
            {
                return false;
            }

            if (m_launchingStatus.AllLaunched())
            {
                static auto arrangerTimeDelay = std::chrono::high_resolution_clock::now();
                auto currentTime = std::chrono::high_resolution_clock::now();
                std::chrono::duration<double> timeDiff = currentTime - arrangerTimeDelay;
                if (timeDiff.count() >= 5)
                {
                    return false;
                }
            }
            
            return true;
        });
}

Launcher::~Launcher()
{
    Logger::trace(L"Finalizing launch");

    // update last-launched time
    if (m_invokePoint != InvokePoint::LaunchAndEdit)
    {
        time_t launchedTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
        m_project.lastLaunchedTime = launchedTime;
        for (int i = 0; i < m_workspaces.size(); i++)
        {
            if (m_workspaces[i].id == m_project.id)
            {
                m_workspaces[i] = m_project;
                break;
            }
        }
        json::to_file(WorkspacesData::WorkspacesFile(), WorkspacesData::WorkspacesListJSON::ToJson(m_workspaces));
    }

    // telemetry
    auto end = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double> duration = end - m_start;
    Logger::trace(L"Launching time: {} s", duration.count());

    auto monitors = MonitorUtils::IdentifyMonitors();
    bool differentSetup = monitors.size() != m_project.monitors.size();
    if (!differentSetup)
    {
        for (const auto& monitor : m_project.monitors)
        {
            auto setup = std::find_if(monitors.begin(), monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.dpi == monitor.dpi && val.monitorRectDpiAware == monitor.monitorRectDpiAware; });
            if (setup == monitors.end())
            {
                differentSetup = true;
                break;
            }
        }
    }

    Trace::Workspaces::Launch(m_launchedSuccessfully, m_project, m_invokePoint, duration.count(), differentSetup, m_launchErrors);
}

void Launcher::Launch()
{
    Logger::info(L"Launch Workspace {} : {}", m_project.name, m_project.id);
    m_launchedSuccessfully = AppLauncher::Launch(m_project, m_launchingStatus, m_launchErrors);
}

void Launcher::handleWindowArrangerMessage(const std::wstring& msg)
{
    if (msg == L"ready")
    {
        Launch();
    }
    else
    {
        try
        {
            auto data = WorkspacesData::AppLaunchInfoJSON::FromJson(json::JsonValue::Parse(msg).GetObjectW());
            if (data.has_value())
            {
                m_launchingStatus.Update(data.value().application, data.value().state);
            }
            else
            {
                Logger::error(L"Failed to parse message from WorkspacesWindowArranger");
            }
        }
        catch (const winrt::hresult_error&)
        {
            Logger::error(L"Failed to parse message from WorkspacesWindowArranger");
        }
    }
}
