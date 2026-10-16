#include "pch.h"
#include "LauncherUIHelper.h"

#include <filesystem>
#include <common/utils/winapi_error.h>

#include <AppLauncher.h>

namespace
{
    json::JsonObject Message(const wchar_t* type)
    {
        json::JsonObject message;
        message.SetNamedValue(L"type", json::value(type));
        return message;
    }
}

LauncherUIHelper::LauncherUIHelper(std::function<void(const std::wstring&)> ipcCallback) :
    m_processId{},
    m_ipcHelper(IPCHelperStrings::LauncherUIPipeName, IPCHelperStrings::UIPipeName, ipcCallback)
{
}

LauncherUIHelper::~LauncherUIHelper()
{
    if (m_process && WaitForSingleObject(m_process.get(), 1000) == WAIT_TIMEOUT)
    {
        Logger::info(L"Stopping WorkspacesLauncherUI with pid {}", m_processId);
        if (!TerminateProcess(m_process.get(), 0))
        {
            Logger::error(L"Unable to stop the Workspaces UI process: {}", GetLastError());
        }
    }
}

bool LauncherUIHelper::LaunchUI()
{
    Logger::trace(L"Starting WorkspacesLauncherUI");

    TCHAR buffer[MAX_PATH] = { 0 };
    GetModuleFileName(NULL, buffer, MAX_PATH);
    const auto path = std::filesystem::path(buffer).parent_path() / L"PowerToys.WorkspacesLauncherUI.exe";
    auto result = AppLauncher::LaunchApp(path.wstring(), L"", false);
    if (result.isError())
    {
        Logger::error(L"Failed to launch PowerToys.WorkspacesLauncherUI: {}", result.error().message);
        return false;
    }
    m_process.reset(result.value().hProcess);
    m_processId = GetProcessId(m_process.get());
    Logger::info(L"WorkspacesLauncherUI started with pid {}", m_processId);
    return IsRunning();
}

bool LauncherUIHelper::IsRunning() const
{
    return m_process && WaitForSingleObject(m_process.get(), 0) == WAIT_TIMEOUT;
}

bool LauncherUIHelper::RequestApproval(const std::wstring& requestId, const std::wstring& name, const std::wstring& path,
                                      const std::wstring& arguments, const SignatureVerification::Result& result) const
{
    if (!IsRunning())
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
    m_ipcHelper.send(message.Stringify().c_str());
    return true;
}

void LauncherUIHelper::DismissApproval(const std::wstring& requestId) const
{
    if (IsRunning())
    {
        auto message = Message(L"dismiss-warning");
        message.SetNamedValue(L"requestId", json::value(requestId));
        m_ipcHelper.send(message.Stringify().c_str());
    }
}

void LauncherUIHelper::UpdateLaunchStatus(WorkspacesData::LaunchingAppStateMap launchedApps) const
{
    WorkspacesData::AppLaunchData appData;
    appData.launcherProcessID = GetCurrentProcessId();
    for (auto& [app, data] : launchedApps)
    {
        appData.appsStateList.insert({ app, { app, nullptr, data.state } });
    }
    m_ipcHelper.send(WorkspacesData::AppLaunchDataJSON::ToJson(appData).ToString().c_str());
}
