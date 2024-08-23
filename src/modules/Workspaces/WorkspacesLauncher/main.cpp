#include "pch.h"

#include <WorkspacesLib/WorkspacesData.h>
#include <WorkspacesLib/trace.h>

#include <AppLauncher.h>
#include <utils.h>

#include <Generated Files/resource.h>

#include <workspaces-common/InvokePoint.h>
#include <workspaces-common/MonitorUtils.h>

#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/resources.h>

const std::wstring moduleName = L"Workspaces\\WorkspacesLauncher";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::workspacesLauncherLoggerName);
    InitUnhandledExceptionHandler();  

    if (powertoys_gpo::getConfiguredWorkspacesEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    if (is_process_elevated())
    {
        Logger::warn("Workspaces Launcher is elevated, restart");

        constexpr DWORD exe_path_size = 0xFFFF;
        auto exe_path = std::make_unique<wchar_t[]>(exe_path_size);
        GetModuleFileNameW(nullptr, exe_path.get(), exe_path_size);

        const auto modulePath = get_module_folderpath();
        
        std::string cmdLineStr(cmdline);
        std::wstring cmdLineWStr(cmdLineStr.begin(), cmdLineStr.end());

        run_non_elevated(exe_path.get(), cmdLineWStr, nullptr, modulePath.c_str());
        return 1;
    }

    // COM should be initialized before ShellExecuteEx is called.
    if (FAILED(CoInitializeEx(NULL, COINIT_MULTITHREADED)))
    {
        Logger::error("CoInitializeEx failed");
        return 1;
    }

    SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    
    std::string cmdLineStr(cmdline);
    auto cmdArgs = split(cmdLineStr, " ");
    if (cmdArgs.size() < 1)
    {
        Logger::warn("Incorrect command line arguments");
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }
    
    std::wstring id(cmdArgs[0].begin(), cmdArgs[0].end());
    if (id.empty())
    {
        Logger::warn("Incorrect command line arguments: no workspace id");
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    InvokePoint invokePoint = InvokePoint::EditorButton;
    if (cmdArgs.size() > 1)
    {
        try
        {
            invokePoint = static_cast<InvokePoint>(std::stoi(cmdArgs[1]));
        }
        catch (std::exception)
        {
        }
    }

    Logger::trace(L"Invoke point: {}", invokePoint);

    // read workspaces
    std::vector<WorkspacesData::WorkspacesProject> workspaces;
    WorkspacesData::WorkspacesProject projectToLaunch{};
    if (invokePoint == InvokePoint::LaunchAndEdit)
    {
        // check the temp file in case the project is just created and not saved to the workspaces.json yet
        if (std::filesystem::exists(WorkspacesData::TempWorkspacesFile()))
        {
            try
            {
                auto savedWorkspacesJson = json::from_file(WorkspacesData::TempWorkspacesFile());
                if (savedWorkspacesJson.has_value())
                {
                    auto savedWorkspaces = WorkspacesData::WorkspacesProjectJSON::FromJson(savedWorkspacesJson.value());
                    if (savedWorkspaces.has_value())
                    {
                        projectToLaunch = savedWorkspaces.value();
                    }
                    else
                    {
                        Logger::critical("Incorrect Workspaces file");
                        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), WorkspacesData::TempWorkspacesFile());
                        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
                        return 1;
                    }
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), WorkspacesData::TempWorkspacesFile());
                    MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
                    return 1;
                }
            }
            catch (std::exception ex)
            {
                Logger::critical("Exception on reading Workspaces file: {}", ex.what());
                std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), WorkspacesData::TempWorkspacesFile());
                MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
                return 1;
            }
        }
    }
    
    if (projectToLaunch.id.empty())
    {
        try
        {
            auto savedWorkspacesJson = json::from_file(WorkspacesData::WorkspacesFile());
            if (savedWorkspacesJson.has_value())
            {
                auto savedWorkspaces = WorkspacesData::WorkspacesListJSON::FromJson(savedWorkspacesJson.value());
                if (savedWorkspaces.has_value())
                {
                    workspaces = savedWorkspaces.value();
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), WorkspacesData::WorkspacesFile());
                    MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
                    return 1;
                }
            }
            else
            {
                Logger::critical("Incorrect Workspaces file");
                std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), WorkspacesData::WorkspacesFile());
                MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
                return 1;
            }
        }
        catch (std::exception ex)
        {
            Logger::critical("Exception on reading Workspaces file: {}", ex.what());
            std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), WorkspacesData::WorkspacesFile());
            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        if (workspaces.empty())
        {
            Logger::warn("Workspaces file is empty");
            std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_EMPTY_FILE), WorkspacesData::WorkspacesFile());
            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }

        for (const auto& proj : workspaces)
        {
            if (proj.id == id)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

    if (projectToLaunch.id.empty())
    {
        Logger::critical(L"Workspace {} not found", id);
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), id);
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_WORKSPACES).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    // launch apps
    Logger::info(L"Launch Workspace {} : {}", projectToLaunch.name, projectToLaunch.id);
    auto monitors = MonitorUtils::IdentifyMonitors();
    std::vector<std::pair<std::wstring, std::wstring>> launchErrors{};
    auto start = std::chrono::high_resolution_clock::now();
    bool launchedSuccessfully = Launch(projectToLaunch, monitors, launchErrors);
    
    // update last-launched time
    if (invokePoint != InvokePoint::LaunchAndEdit)
    {
        time_t launchedTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
        projectToLaunch.lastLaunchedTime = launchedTime;
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

    // telemetry
    auto end = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double> duration = end - start;
    Logger::trace(L"Launching time: {} s", duration.count());

    bool differentSetup = monitors.size() != projectToLaunch.monitors.size();
    if (!differentSetup)
    {
        for (const auto& monitor : projectToLaunch.monitors)
        {
            auto setup = std::find_if(monitors.begin(), monitors.end(), [&](const WorkspacesData::WorkspacesProject::Monitor& val) { return val.dpi == monitor.dpi && val.monitorRectDpiAware == monitor.monitorRectDpiAware; });
            if (setup == monitors.end())
            {
                differentSetup = true;
                break;
            }
        }
    }

    Trace::Workspaces::Launch(launchedSuccessfully, projectToLaunch, invokePoint, duration.count(), differentSetup, launchErrors);

    CoUninitialize();
    return 0;
}
