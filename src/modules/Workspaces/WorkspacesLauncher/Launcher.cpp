#include "pch.h"
#include "Launcher.h"

#include <common/utils/json.h>

#include <workspaces-common/MonitorUtils.h>

#include <WorkspacesLib/trace.h>

#include <AppLauncher.h>
#include <WorkspacesLib/AppUtils.h>

Launcher::Launcher(const WorkspacesData::WorkspacesProject& project, 
    std::vector<WorkspacesData::WorkspacesProject>& workspaces,
    InvokePoint invokePoint) :
    m_project(project),
    m_workspaces(workspaces),
    m_invokePoint(invokePoint),
    m_start(std::chrono::high_resolution_clock::now()),
    m_uiHelper(std::make_unique<LauncherUIHelper>(std::bind(&Launcher::handleUIMessage, this, std::placeholders::_1))),
    m_windowArrangerHelper(std::make_unique<WindowArrangerHelper>(std::bind(&Launcher::handleWindowArrangerMessage, this, std::placeholders::_1))),
    m_launchingStatus(m_project)
{
    // main thread
    Logger::info(L"Launch Workspace {} : {}", m_project.name, m_project.id);

    m_uiHelper->LaunchUI();
    m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());

    bool launchElevated = std::find_if(m_project.apps.begin(), m_project.apps.end(), [](const WorkspacesData::WorkspacesProject::Application& app) { return app.isElevated; }) != m_project.apps.end();
    m_windowArrangerHelper->Launch(m_project.id, launchElevated, [&]() -> bool
        {
            // Check if we received the completion signal
            if (m_arrangerCompleted)
            {
                Logger::info(L"WindowArranger completed signal received, stopping wait");
                return false;
            }
            
            if (m_launchingStatus.AllLaunchedAndMoved())
            {
                return false;
            }

            if (m_launchingStatus.AllLaunched())
            {
                static auto arrangerTimeDelay = std::chrono::high_resolution_clock::now();
                auto currentTime = std::chrono::high_resolution_clock::now();
                std::chrono::duration<double> timeDiff = currentTime - arrangerTimeDelay;
                // Use 2 second timeout - balance between performance and ensuring window movement completion
                if (timeDiff.count() >= 2.0)
                {
                    Logger::info(L"WindowArranger timeout reached (2s), stopping wait");
                    return false;
                }
            }
            
            return true;
        });
}

Launcher::~Launcher()
{
    // main thread, will wait until arranger is finished
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

    std::lock_guard lock(m_launchErrorsMutex);
    Trace::Workspaces::Launch(m_launchedSuccessfully, m_project, m_invokePoint, duration.count(), differentSetup, m_launchErrors);
}

void Launcher::Launch() // Launching thread
{
    const long maxWaitTimeMs = 2000; // Reduced from 3000 to 2000
    const long ms = 50; // Reduced from 100 to 50 for faster polling

    // Launch apps
    for (auto appState = m_launchingStatus.GetNext(LaunchingState::Waiting);appState.has_value();appState = m_launchingStatus.GetNext(LaunchingState::Waiting))
    {
        auto app = appState.value().application;
        
        long waitingTime = 0;
        bool additionalWait = false;
        while (!m_launchingStatus.AllInstancesOfTheAppLaunchedAndMoved(app) && waitingTime < maxWaitTimeMs)
        {
            std::this_thread::sleep_for(std::chrono::milliseconds(ms));
            waitingTime += ms;
            additionalWait = true;
        }

        // Reduced Outlook delay from 1000ms to 500ms for better performance
        if (additionalWait)
        {
            // Resolves an issue when Outlook does not launch when launching one after another.
            // Launching Outlook instances right one after another causes error message.
            // Launching Outlook instances with less than 1-second delay causes the second window not to appear
            // even though there wasn't a launch error.
            std::this_thread::sleep_for(std::chrono::milliseconds(500)); // Reduced from 1000ms
        }

        if (waitingTime >= maxWaitTimeMs)
        {
            Logger::info(L"Waiting time for launching next {} instance expired", app.name);
        }

        bool launched{ false };
        {
            std::lock_guard lock(m_launchErrorsMutex);
            launched = AppLauncher::Launch(app, m_launchErrors);
        }

        if (launched)
        {
            m_launchingStatus.Update(app, LaunchingState::Launched);
            Logger::info(L"Successfully launched {}", app.name);
        }
        else
        {
            Logger::error(L"Failed to launch {}", app.name);
            m_launchingStatus.Update(app, LaunchingState::Failed);
            m_launchedSuccessfully = false;
        }

        auto status = m_launchingStatus.Get(app); // updated after launch status 
        if (status.has_value())
        {
            {
                std::lock_guard lock(m_windowArrangerHelperMutex);
                m_windowArrangerHelper->UpdateLaunchStatus(status.value());
            }
        }

        {
            std::lock_guard lock(m_uiHelperMutex);
            m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());
        };
    }

    Logger::info(L"All apps launched, total apps processed: {}", m_project.apps.size());
}

void Launcher::handleWindowArrangerMessage(const std::wstring& msg) // WorkspacesArranger IPC thread
{
    if (msg == L"ready")
    {
        std::thread([&]() { Launch(); }).detach();
    }
    else if (msg == L"completed")
    {
        Logger::info(L"Received completion signal from WindowArranger");
        // Force completion by setting a flag or similar mechanism
        m_arrangerCompleted = true;
    }
    else
    {
        try
        {
            auto data = WorkspacesData::AppLaunchInfoJSON::FromJson(json::JsonValue::Parse(msg).GetObjectW());
            if (data.has_value())
            {
                m_launchingStatus.Update(data.value().application, data.value().state);
                
                {
                    std::lock_guard lock(m_uiHelperMutex);
                    m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());
                }
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

void Launcher::handleUIMessage(const std::wstring& msg) // UI IPC thread
{
    if (msg == L"cancel")
    {
        m_launchingStatus.Cancel();
    }
}
