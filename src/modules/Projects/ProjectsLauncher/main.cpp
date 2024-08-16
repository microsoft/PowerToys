#include "pch.h"

#include <ProjectsLib/ProjectsData.h>
#include <ProjectsLib/trace.h>

#include <AppLauncher.h>
#include <utils.h>

#include <Generated Files/resource.h>

#include <projects-common/InvokePoint.h>
#include <projects-common/MonitorUtils.h>

#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>
#include <common/utils/resources.h>

const std::wstring moduleName = L"App Layouts\\ProjectsLauncher";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::projectsLauncherLoggerName);
    InitUnhandledExceptionHandler();  

    if (powertoys_gpo::getConfiguredProjectsEnabledValue() == powertoys_gpo::gpo_rule_configured_disabled)
    {
        Logger::warn(L"Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
        return 0;
    }

    if (is_process_elevated())
    {
        Logger::warn("App Layouts Launcher is elevated, restart");

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
    
    // read projects
    auto projectsFileName = ProjectsData::ProjectsFile();
    std::vector<ProjectsData::Project> projects;
    try
    {
        auto savedProjectsJson = json::from_file(projectsFileName);
        if (savedProjectsJson.has_value())
        {
            auto savedProjects = ProjectsData::ProjectsListJSON::FromJson(savedProjectsJson.value());
            if (savedProjects.has_value())
            {
                projects = savedProjects.value();
            }
            else
            {
                Logger::critical("Incorrect App Layouts file");
                std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), L"app-layouts.json");
                MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
                return 1;
            }
        }
        else
        {
            Logger::critical("Incorrect App Layouts file");
            std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_INCORRECT_FILE_ERROR), L"app-layouts.json");
            MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
            return 1;
        }
    }
    catch (std::exception ex)
    {
        Logger::critical("Exception on reading App Layout: {}", ex.what());
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_FILE_READING_ERROR), L"app-layouts.json");
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    if (projects.empty())
    {
        Logger::warn("App Layouts file is empty");
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_EMPTY_FILE), L"app-layouts.json");
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }

    ProjectsData::Project projectToLaunch{};
    std::string cmdLineStr(cmdline);
    auto cmdArgs = split(cmdLineStr, " ");
    if (cmdArgs.size() < 1)
    {
        Logger::warn("Incorrect command line arguments");
        MessageBox(NULL, GET_RESOURCE_STRING(IDS_INCORRECT_ARGS).c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
        return 1;
    }
    
    std::wstring id(cmdArgs[0].begin(), cmdArgs[0].end());
    if (!id.empty())
    {
        for (const auto& proj : projects)
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
        Logger::critical(L"App Layout {} not found", id);
        std::wstring formattedMessage = fmt::format(GET_RESOURCE_STRING(IDS_PROJECT_NOT_FOUND), id);
        MessageBox(NULL, formattedMessage.c_str(), GET_RESOURCE_STRING(IDS_PROJECTS).c_str(), MB_ICONERROR | MB_OK);
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

    // launch apps
    Logger::info(L"Launch App Layout {} : {}", projectToLaunch.name, projectToLaunch.id);
    auto monitors = MonitorUtils::IdentifyMonitors();
    std::vector<std::pair<std::wstring, std::wstring>> launchErrors{};
    auto start = std::chrono::high_resolution_clock::now();
    bool launchedSuccessfully = Launch(projectToLaunch, monitors, launchErrors);
    
    // update last-launched time
    time_t launchedTime = std::chrono::system_clock::to_time_t(std::chrono::system_clock::now());
    projectToLaunch.lastLaunchedTime = launchedTime;
    for (int i = 0; i < projects.size(); i++)
    {
        if (projects[i].id == projectToLaunch.id)
        {
            projects[i] = projectToLaunch;
            break;
        }
    }
    json::to_file(projectsFileName, ProjectsData::ProjectsListJSON::ToJson(projects));

    // telemetry
    auto end = std::chrono::high_resolution_clock::now();
    std::chrono::duration<double> duration = end - start;
    Logger::trace(L"Launching time: {} s", duration.count());

    bool differentSetup = monitors.size() != projectToLaunch.monitors.size();
    if (!differentSetup)
    {
        for (const auto& monitor : projectToLaunch.monitors)
        {
            auto setup = std::find_if(monitors.begin(), monitors.end(), [&](const ProjectsData::Project::Monitor& val) { return val.dpi == monitor.dpi && val.monitorRectDpiAware == monitor.monitorRectDpiAware; });
            if (setup == monitors.end())
            {
                differentSetup = true;
                break;
            }
        }
    }

    Trace::Projects::Launch(launchedSuccessfully, projectToLaunch, invokePoint, duration.count(), differentSetup, launchErrors);

    CoUninitialize();
    return 0;
}
