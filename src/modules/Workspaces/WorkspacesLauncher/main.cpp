#include "pch.h"

#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/resources.h>

#include <common/Telemetry/EtwTrace/EtwTrace.h>

#include <WorkspacesLib/JsonUtils.h>
#include <WorkspacesLib/utils.h>

#include <Launcher.h>

#include <Generated Files/resource.h>
#include <WorkspacesLib/AppUtils.h>
#include <WorkspacesLib/trace.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesLauncher";
const std::wstring internalPath = L"";
const std::wstring instanceMutexName = L"Local\\PowerToys_WorkspacesLauncher_InstanceMutex";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesLauncherLoggerName);
    InitUnhandledExceptionHandler();

    Trace::Workspaces::RegisterProvider();

    Shared::Trace::ETWTrace trace{};
    trace.UpdateState(true);

    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    std::wstring cmdLineStr{ GetCommandLineW() };
    auto cmdArgs = split(cmdLineStr, L" ");
    if (cmdArgs.workspaceId.empty())
    {
        Logger::warn("Incorrect command line arguments: no workspace id");
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    if (!cmdArgs.isRestarted)
    {
        // check if restart is needed. Only check it if not yet restarted to avoid endless restarting. Restart is needed if the process is elevated.
        if (is_process_elevated())
        {
            Logger::warn("Workspaces Launcher is elevated, restart");

            constexpr DWORD exe_path_size = 0xFFFF;
            auto exe_path = std::make_unique<wchar_t[]>(exe_path_size);
            GetModuleFileNameW(nullptr, exe_path.get(), exe_path_size);

            const auto modulePath = get_module_folderpath();

            std::string cmdLineStr(cmdline);
            std::wstring cmdLineWStr(cmdLineStr.begin(), cmdLineStr.end());

            std::wstring cmd = cmdArgs.workspaceId + L" " + std::to_wstring(cmdArgs.invokePoint) + L" " + NonLocalizable::restartedString;

            RunNonElevatedEx(exe_path.get(), cmd, modulePath);
            return 1;
        }
    }

    auto mutex = CreateMutex(nullptr, true, instanceMutexName.c_str());
    if (mutex == nullptr)
    {
        Logger::error(L"Failed to create mutex. {}", get_last_error_or_default(GetLastError()));
    }

    if (GetLastError() == ERROR_ALREADY_EXISTS)
    {
        Logger::warn(L"WorkspacesLauncher instance is already running");
        return 0;
    }

    // COM should be initialized before ShellExecuteEx is called.
    if (FAILED(CoInitializeEx(NULL, COINIT_MULTITHREADED)))
    {
        Logger::error("CoInitializeEx failed");
        return 1;
    }

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);

    Logger::trace(L"Invoke point: {}", cmdArgs.invokePoint);

    // read workspaces
    std::vector<WorkspacesData::WorkspacesProject> workspaces;
    WorkspacesData::WorkspacesProject projectToLaunch{};
    if (cmdArgs.invokePoint == InvokePoint::LaunchAndEdit)
    {
        // check the temp file in case the project is just created and not saved to the workspaces.json yet
        auto file = WorkspacesData::TempWorkspacesFile();
        auto res = JsonUtils::ReadSingleWorkspace(file);
        if (res.isOk() && projectToLaunch.id == cmdArgs.workspaceId)
        {
            projectToLaunch = res.getValue();
        }
        else if (res.isError())
        {
            std::wstring formattedMessage{};
            switch (res.error())
            {
            case JsonUtils::WorkspacesFileError::FileReadingError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), file);
                break;
            case JsonUtils::WorkspacesFileError::IncorrectFileError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), file);
                break;
            }

            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }
    }

    if (projectToLaunch.id.empty())
    {
        auto file = WorkspacesData::WorkspacesFile();
        auto res = JsonUtils::ReadWorkspaces(file);
        if (res.isOk())
        {
            workspaces = res.getValue();
        }
        else
        {
            std::wstring formattedMessage{};
            switch (res.error())
            {
            case JsonUtils::WorkspacesFileError::FileReadingError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), file);
                break;
            case JsonUtils::WorkspacesFileError::IncorrectFileError:
                formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), file);
                break;
            }

            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        if (workspaces.empty())
        {
            Logger::warn("Workspaces file is empty");
            std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_EMPTY_FILE), file);
            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        for (const auto& proj : workspaces)
        {
            if (proj.id == cmdArgs.workspaceId)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

    if (projectToLaunch.id.empty())
    {
        Logger::critical(L"Workspace {} not found", cmdArgs.workspaceId);
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), cmdArgs.workspaceId);
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    // prepare project in advance
    auto installedApps = Utils::Apps::GetAppsList();
    bool updatedApps = Utils::Apps::UpdateWorkspacesApps(projectToLaunch, installedApps);
    bool updatedIds = false;

    // verify apps have ids
    for (auto& app : projectToLaunch.apps)
    {
        if (app.id.empty())
        {
            app.id = CreateGuidString();
            updatedIds = true;
        }
    }

    // update the file before launching, so WorkspacesWindowArranger and WorkspacesLauncherUI could get updated app paths
    if (updatedApps || updatedIds)
    {
        for (int i = 0; i < workspaces.size(); i++)
        {
            if (workspaces[i].id == projectToLaunch.id)
            {
                workspaces[i] = projectToLaunch;
                break;
            }
        }

        json::to_file(WorkspacesData::WorkspacesFile(), WorkspacesData::WorkspacesListJSON::ToJson(workspaces));
    }

    // launch
    {
        Launcher launcher(projectToLaunch, workspaces, cmdArgs.invokePoint);
    }

    trace.Flush();
    trace.UpdateState(false);

    Trace::Workspaces::UnregisterProvider();

    Logger::trace("Finished");
    CoUninitialize();
    return 0;
}
