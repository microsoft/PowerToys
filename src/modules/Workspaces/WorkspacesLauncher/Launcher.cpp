#include "pch.h"
#include "Launcher.h"

#include <common/utils/json.h>

#include <workspaces-common/MonitorUtils.h>
#include <workspaces-common/GuidUtils.h>

#include <WorkspacesLib/trace.h>

#include <AppLauncher.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/LauncherUiMessage.h>

Launcher::Launcher(const WorkspacesData::WorkspacesProject& project, 
    std::vector<WorkspacesData::WorkspacesProject>& workspaces,
    InvokePoint invokePoint) :
    m_project(project),
    m_workspaces(workspaces),
    m_invokePoint(invokePoint),
    m_start(std::chrono::high_resolution_clock::now()),
    m_uiHelper(std::make_unique<LauncherUIHelper>(std::bind(&Launcher::handleUIMessage, this, std::placeholders::_1),
                                               std::bind(&Launcher::handleUIFailure, this, std::placeholders::_1))),
    m_windowArrangerHelper(std::make_unique<WindowArrangerHelper>(std::bind(&Launcher::handleWindowArrangerMessage, this, std::placeholders::_1))),
    m_launchingStatus(m_project)
{
    // main thread
    Logger::info(L"Launch Workspace {} : {}", m_project.name, m_project.id);

    if (m_uiHelper->LaunchUI() && m_uiHelper->WaitForReady())
    {
        m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());
    }
    else
    {
        Logger::error(L"Workspaces UI is unavailable; unverified elevated applications will not launch");
    }

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
    m_approval.Cancel();
    {
        std::lock_guard lock(m_launchThreadMutex);
        if (m_launchThread.joinable())
        {
            m_launchThread.join();
        }
    }
    // Stop callbacks while all launch state and synchronization objects are still alive.
    m_windowArrangerHelper.reset();
    m_uiHelper->Shutdown();
    m_uiHelper.reset();
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
    const HRESULT initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
    if (FAILED(initialized))
    {
        Logger::error(L"Unable to initialize the Workspaces launch thread: {:#010x}", static_cast<uint32_t>(initialized));
        m_approval.Cancel();
        m_launchingStatus.Cancel();
        return;
    }
    auto uninitialize = wil::scope_exit([] { CoUninitialize(); });

    const long maxWaitTimeMs = 3000;
    const long ms = 100;

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

        if (additionalWait)
        {
            // Resolves an issue when Outlook does not launch when launching one after another.
            // Launching Outlook instances right one after another causes error message.
            // Launching Outlook instances with less than 1-second delay causes the second window not to appear
            // even though there wasn't a launch error.
            std::this_thread::sleep_for(std::chrono::milliseconds(1000));
        }

        if (waitingTime >= maxWaitTimeMs)
        {
            Logger::info(L"Waiting time for launching next {} instance expired", app.name);
        }

        AppLauncher::LaunchResult result;
        {
            std::mutex heartbeatMutex;
            std::condition_variable_any heartbeatChanged;
            std::jthread heartbeat;
            if (app.isElevated)
            {
                heartbeat = std::jthread([&](std::stop_token stop) {
                    std::unique_lock heartbeatLock(heartbeatMutex);
                    while (!stop.stop_requested())
                    {
                        {
                            std::lock_guard lock(m_windowArrangerHelperMutex);
                            m_windowArrangerHelper->KeepLaunchingAlive();
                        }
                        heartbeatChanged.wait_for(heartbeatLock, stop, std::chrono::seconds(1), [] { return false; });
                    }
                });
            }
            std::lock_guard lock(m_launchErrorsMutex);
            result = AppLauncher::Launch(app, m_launchErrors, [&](const auto& path, const auto& arguments, const auto& verification) { return requestApproval(app.name, path, arguments, verification); }, [&] { return m_approval.IsCanceled(); });
        }

        if (result == AppLauncher::LaunchResult::Launched)
        {
            m_launchingStatus.Update(app, LaunchingState::Launched);
        }
        else if (result == AppLauncher::LaunchResult::Canceled)
        {
            m_launchingStatus.Update(app, LaunchingState::Canceled);
        }
        else if (result == AppLauncher::LaunchResult::Skipped)
        {
            m_launchingStatus.Update(app, LaunchingState::Skipped);
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
}

void Launcher::handleWindowArrangerMessage(const std::wstring& msg) // WorkspacesArranger IPC thread
{
    if (msg == L"ready")
    {
        std::lock_guard lock(m_launchThreadMutex);
        if (!m_approval.IsCanceled() && !m_launchStarted.exchange(true))
        {
            m_launchThread = std::thread([&]() { Launch(); });
        }
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
    if (m_uiFailed)
    {
        return;
    }
    const auto message = LauncherUiMessage::Parse(msg);
    if (!message)
    {
        handleUIFailure(LauncherIpcFailure::InvalidMessage);
        return;
    }
    if (message->type == LauncherUiMessage::Type::Ready)
    {
        if (!m_uiHelper->MarkReady())
        {
            handleUIFailure(LauncherIpcFailure::InvalidMessage);
        }
        return;
    }
    if (!m_uiHelper->IsReady())
    {
        handleUIFailure(LauncherIpcFailure::InvalidMessage);
        return;
    }
    if (message->type == LauncherUiMessage::Type::Cancel)
    {
        m_approval.Cancel();
        m_launchingStatus.Cancel();
        return;
    }
    if (message->type == LauncherUiMessage::Type::Heartbeat)
    {
        // A queued heartbeat can arrive after the decision; never revive that request.
        m_approval.Heartbeat(message->requestId);
        return;
    }
    const bool accepted = message->type == LauncherUiMessage::Type::WarningShown ?
                              m_approval.Acknowledge(message->requestId) :
                              m_approval.Complete(message->requestId, message->decision);
    if (!accepted)
    {
        Logger::warn(L"Ignoring a stale, early, or duplicate elevation confirmation message");
    }
}

void Launcher::handleUIFailure(LauncherIpcFailure failure)
{
    if (!m_uiFailed.exchange(true))
    {
        Logger::error(L"Workspaces UI channel failed: {}", static_cast<int>(failure));
    }
    m_approval.Fail(failure == LauncherIpcFailure::InvalidMessage ? LaunchDecision::InvalidResponse : LaunchDecision::UiUnavailable);
    m_uiHelper->Disconnect();
}

LaunchDecision Launcher::requestApproval(const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result)
{
    if (m_approval.IsCanceled())
    {
        return LaunchDecision::Canceled;
    }
    if (m_uiFailed || !m_uiHelper->IsReady())
    {
        Logger::error(L"Cannot request elevation confirmation: Workspaces UI is unavailable");
        return LaunchDecision::UiUnavailable;
    }
    const std::wstring requestId = CreateGuidString();
    if (!m_approval.Begin(requestId))
    {
        Logger::error(L"Unable to create an elevation confirmation request");
        return m_approval.IsCanceled() ? LaunchDecision::Canceled : LaunchDecision::InvalidResponse;
    }
    auto finish = wil::scope_exit([&] {
        m_approval.End();
        std::lock_guard lock(m_uiHelperMutex);
        m_uiHelper->DismissApproval(requestId);
    });
    {
        std::lock_guard lock(m_uiHelperMutex);
        if (m_uiFailed || !m_uiHelper->RequestApproval(requestId, name, path, arguments, result))
        {
            m_approval.Fail(LaunchDecision::UiUnavailable);
        }
    }
    for (;;)
    {
        if (const auto decision = m_approval.WaitFor(std::chrono::milliseconds(250)); decision.has_value())
        {
            return decision.value();
        }
        if (m_uiFailed || !m_uiHelper->IsReady())
        {
            m_approval.Fail(LaunchDecision::UiUnavailable);
        }
    }
}
