#include "pch.h"
#include "Launcher.h"

#include <common/utils/json.h>

#include <workspaces-common/MonitorUtils.h>
#include <workspaces-common/GuidUtils.h>

#include <WorkspacesLib/trace.h>

#include <AppLauncher.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/LauncherUiMessage.h>
#include <WorkspacesLib/WorkspaceStore.h>

Launcher::Launcher(const WorkspacesData::WorkspacesProject& project,
                   std::vector<WorkspacesData::WorkspacesProject>& workspaces,
                   InvokePoint invokePoint,
                   const CliLaunchOptions* cliOptions) :
    m_project(project),
    m_workspaces(workspaces),
    m_invokePoint(invokePoint),
    m_start(std::chrono::high_resolution_clock::now()),
    m_uiHelper(cliOptions ? nullptr : std::make_unique<LauncherUIHelper>(std::bind(&Launcher::handleUIMessage, this, std::placeholders::_1))),
    m_windowArrangerHelper(std::make_unique<WindowArrangerHelper>(
        std::bind(&Launcher::handleWindowArrangerMessage, this, std::placeholders::_1),
        cliOptions ? cliOptions->operationId : L"")),
    m_launchingStatus(m_project),
    m_cliOptions(cliOptions)
{
    // main thread
    Logger::info(L"Launch Workspace {} : {}", m_project.name, m_project.id);

    if (m_uiHelper && m_uiHelper->LaunchUI())
    {
        m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());
    }
    else if (m_uiHelper)
    {
        Logger::error(L"Workspaces UI is unavailable; unverified elevated applications will not launch");
    }

    bool launchElevated = std::find_if(m_project.apps.begin(), m_project.apps.end(), [](const WorkspacesData::WorkspacesProject::Application& app) { return app.isElevated; }) != m_project.apps.end();
    m_arrangerError = m_windowArrangerHelper->Launch(m_project.id, launchElevated, [&]() -> bool {
            if (ShouldStop())
            {
                m_stopping = true;
                m_launchingStatus.Cancel();
                return false;
            }
            if (m_cliOptions)
                return !m_arrangerCompleted;
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
            
            return true; }, cliOptions ? cliOptions->timeoutSeconds : 0);
    if (m_cliOptions)
    {
        m_stopping = true;
        std::lock_guard lock(m_launchThreadMutex);
        if (m_launchThread.joinable())
            m_launchThread.join();
    }
}

Launcher::~Launcher()
{
    m_stopping = true;
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
    m_uiHelper.reset();
    if (m_cliOptions)
        return;
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
        if (WorkspaceStore::UpdateLastLaunched(WorkspacesData::WorkspacesFile(), m_project.id, launchedTime) != WorkspaceStore::UpdateResult::Updated)
            Logger::warn("Workspace launch history could not be saved");
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
        if (ShouldStop())
        {
            m_launchingStatus.Cancel();
            break;
        }
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

        if (ShouldStop())
        {
            m_launchingStatus.Cancel();
            break;
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
            DWORD nativeError = ERROR_SUCCESS;
            result = AppLauncher::Launch(app, m_launchErrors,
                [&](const auto& path, const auto& arguments, const auto& verification) {
                    const auto decision = requestApproval(app.name, path, arguments, verification);
                    m_approvalDecisions[app.id] = decision;
                    return decision;
                },
                [&] { return m_approval.IsCanceled() || ShouldStop(); }, &nativeError);
            if (result != AppLauncher::LaunchResult::Launched)
                m_applicationErrors[app.id] = nativeError;
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
            if (m_uiHelper)
                m_uiHelper->UpdateLaunchStatus(m_launchingStatus.Get());
        };
    }
}

void Launcher::handleWindowArrangerMessage(const std::wstring& msg) // WorkspacesArranger IPC thread
{
    if (m_cliOptions && msg.size() > 1024 * 1024)
    {
        m_stopping = true;
        return;
    }
    if (m_cliOptions && msg == L"snapshot-request")
    {
        m_windowArrangerHelper->SendSnapshot(m_project);
        return;
    }
    if (msg == L"ready")
    {
        std::lock_guard lock(m_launchThreadMutex);
        if (!m_approval.IsCanceled() && !ShouldStop() && !m_launchStarted.exchange(true))
        {
            m_launchThread = std::thread([&]() { Launch(); });
        }
    }
    else
    {
        try
        {
            const auto object = json::JsonValue::Parse(msg).GetObjectW();
            if (m_cliOptions && object.GetNamedString(L"kind", L"") == L"arranger-result")
            {
                if (object.GetNamedNumber(L"protocolVersion") != 1 || object.GetNamedString(L"operationId") != m_cliOptions->operationId)
                    throw std::runtime_error("Invalid arranger protocol");
                auto expected = m_launchingStatus.Get();
                if (object.GetNamedArray(L"applications").Size() != expected.size())
                    throw std::runtime_error("Incomplete arranger result");
                for (const auto& value : object.GetNamedArray(L"applications"))
                {
                    const auto item = WorkspacesData::AppLaunchInfoJSON::FromJson(value.GetObjectW());
                    if (!item || !expected.erase(item->application) ||
                        item->state < LaunchingState::Waiting || item->state > LaunchingState::Skipped)
                        throw std::runtime_error("Invalid arranger result");
                    m_launchingStatus.Update(item->application, item->state);
                }
                m_windowArrangerHelper->AcknowledgeResult();
                m_arrangerCompleted = true;
                return;
            }
            auto data = WorkspacesData::AppLaunchInfoJSON::FromJson(object);
            if (data.has_value())
            {
                m_launchingStatus.Update(data.value().application, data.value().state);
                
                {
                    std::lock_guard lock(m_uiHelperMutex);
                    if (m_uiHelper)
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
            if (m_cliOptions)
                m_stopping = true;
        }
        catch (const std::exception&)
        {
            Logger::error(L"Invalid WorkspacesWindowArranger result");
            if (m_cliOptions)
                m_stopping = true;
        }
    }
}

void Launcher::handleUIMessage(const std::wstring& msg) // UI IPC thread
{
    const auto message = LauncherUiMessage::Parse(msg);
    if (!message)
    {
        Logger::error(L"Invalid Workspaces UI message");
        m_approval.Fail(LaunchDecision::InvalidResponse);
        return;
    }
    if (message->type == LauncherUiMessage::Type::Cancel)
    {
        m_approval.Cancel();
        m_launchingStatus.Cancel();
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

LaunchDecision Launcher::requestApproval(const std::wstring& name, const std::wstring& path, const std::wstring& arguments, const SignatureVerification::Result& result)
{
    if (m_approval.IsCanceled() || ShouldStop())
    {
        return LaunchDecision::Canceled;
    }
    if (m_cliOptions)
    {
        return m_cliOptions->requestApproval ?
            m_cliOptions->requestApproval(name, path, arguments, result) : LaunchDecision::UiUnavailable;
    }
    if (!m_uiHelper || !m_uiHelper->IsRunning())
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
        if (!m_uiHelper->RequestApproval(requestId, name, path, arguments, result))
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
        if (!m_uiHelper->IsRunning())
        {
            m_approval.Fail(LaunchDecision::UiUnavailable);
        }
    }
}

bool Launcher::ShouldStop() const
{
    return m_cliOptions && (m_stopping ||
                            WaitForSingleObject(m_cliOptions->cancelEvent, 0) == WAIT_OBJECT_0 ||
                            GetTickCount64() >= m_cliOptions->deadline);
}

WorkspacesData::LaunchingAppStateMap Launcher::GetResult()
{
    return m_launchingStatus.Get();
}

std::map<std::wstring, DWORD> Launcher::GetApplicationErrors()
{
    std::lock_guard lock(m_launchErrorsMutex);
    return m_applicationErrors;
}

std::map<std::wstring, LaunchDecision> Launcher::GetApprovalDecisions()
{
    std::lock_guard lock(m_launchErrorsMutex);
    return m_approvalDecisions;
}
