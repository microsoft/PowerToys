#include "pch.h"
#include "LauncherUIHelper.h"

#include <filesystem>
#include <common/utils/process_path.h>

namespace
{
    json::JsonObject Message(const wchar_t* type)
    {
        json::JsonObject message;
        message.SetNamedValue(L"protocolVersion", json::value(1));
        message.SetNamedValue(L"type", json::value(type));
        return message;
    }
}

LauncherUIHelper::LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback,
                                 std::function<void(LauncherIpcFailure)> failureCallback) :
    m_processId{},
    m_readyEvent(CreateEventW(nullptr, TRUE, FALSE, nullptr)),
    m_channel(std::move(ipcCallback), std::move(failureCallback))
{
}

LauncherUIHelper::~LauncherUIHelper()
{
    Shutdown();
}

void LauncherUIHelper::Shutdown()
{
    if (IsReady())
    {
        m_channel.Send(Message(L"shutdown").Stringify().c_str());
    }
    m_channel.Stop();
    if (m_process && WaitForSingleObject(m_process.get(), 1500) == WAIT_TIMEOUT)
    {
        Logger::warn(L"Stopping unresponsive Workspaces UI process {}", m_processId);
        if (!TerminateProcess(m_process.get(), 1))
        {
            Logger::error(L"Unable to stop the Workspaces UI process: {}", GetLastError());
        }
    }
    m_process.reset();
}

bool LauncherUIHelper::LaunchUI()
{
    const auto directory = get_module_folderpath();
    const auto version = interop_auth::GetOwnModuleVersion();
    if (!m_readyEvent || directory.empty() || !version || !m_channel.Initialize())
    {
        Logger::error(L"Unable to initialize the Workspaces UI identity or channel");
        return false;
    }

    const auto executable = (std::filesystem::path(directory) / L"PowerToys.WorkspacesLauncherUI.exe").wstring();
    auto command = L"\"" + executable + L"\" --launcher-pid " + std::to_wstring(GetCurrentProcessId()) +
                   L" --ipc-name \"" + m_channel.Name() + L"\"";
    STARTUPINFOW startup{ sizeof(startup) };
    PROCESS_INFORMATION process{};
    if (!CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE, 0, nullptr, directory.c_str(), &startup, &process))
    {
        Logger::error(L"Unable to start the Workspaces UI: {}", GetLastError());
        return false;
    }
    wil::unique_handle thread(process.hThread);
    m_process.reset(process.hProcess);
    m_processId = process.dwProcessId;

    interop_auth::CallerPolicy policy;
    policy.enabled = true;
    policy.expectedClientPid = m_processId;
    policy.expectedDirectory = directory;
    policy.allowedBasenames = { L"PowerToys.WorkspacesLauncherUI.exe" };
    policy.expectedVersion = version;
    policy.requireMicrosoftSignature = true;
    policy.logReject = [](const auto& result) {
        Logger::warn(L"Rejected Workspaces UI peer {}: {}", result.pid, result.reasonCode);
    };
    return m_channel.Start(m_process.get(), policy);
}

bool LauncherUIHelper::IsRunning() const
{
    return m_process && WaitForSingleObject(m_process.get(), 0) == WAIT_TIMEOUT;
}

bool LauncherUIHelper::IsReady() const
{
    return m_ready && m_channel.IsConnected() && IsRunning();
}

bool LauncherUIHelper::MarkReady()
{
    if (!m_channel.IsConnected() || m_ready.exchange(true))
    {
        return false;
    }
    SetEvent(m_readyEvent.get());
    Logger::trace(L"Authenticated Workspaces UI process {} is ready", m_processId);
    return true;
}

bool LauncherUIHelper::WaitForReady() const
{
    if (!m_process || !m_readyEvent)
    {
        return false;
    }
    const HANDLE handles[] = { m_process.get(), m_readyEvent.get() };
    return WaitForMultipleObjects(ARRAYSIZE(handles), handles, FALSE, 10000) == WAIT_OBJECT_0 + 1 && IsReady();
}

void LauncherUIHelper::Disconnect()
{
    m_channel.Disconnect();
}

bool LauncherUIHelper::RequestApproval(const std::wstring& requestId, const std::wstring& name, const std::wstring& path,
                                      const std::wstring& arguments, const SignatureVerification::Result& result) const
{
    if (!IsReady())
    {
        return false;
    }
    auto message = Message(L"elevation-warning");
    message.SetNamedValue(L"requestId", json::value(requestId));
    message.SetNamedValue(L"appName", json::value(name));
    message.SetNamedValue(L"path", json::value(path));
    message.SetNamedValue(L"arguments", json::value(arguments));
    message.SetNamedValue(L"reason", json::value(result.Reason()));
    message.SetNamedValue(L"status", json::value(fmt::format(L"0x{:08X}", static_cast<uint32_t>(result.error))));
    return m_channel.Send(message.Stringify().c_str());
}

void LauncherUIHelper::DismissApproval(const std::wstring& requestId) const
{
    if (IsReady())
    {
        auto message = Message(L"dismiss-warning");
        message.SetNamedValue(L"requestId", json::value(requestId));
        m_channel.Send(message.Stringify().c_str());
    }
}

void LauncherUIHelper::UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const
{
    if (!IsReady())
    {
        return;
    }
    WorkspacesData::AppLaunchData appData;
    appData.launcherProcessID = GetCurrentProcessId();
    for (auto& [app, data] : launchedApps)
    {
        appData.appsStateList.insert({ app, { app, nullptr, data.state } });
    }
    auto message = WorkspacesData::AppLaunchDataJSON::ToJson(appData);
    message.SetNamedValue(L"protocolVersion", json::value(1));
    message.SetNamedValue(L"type", json::value(L"launch-status"));
    m_channel.Send(message.Stringify().c_str());
}
