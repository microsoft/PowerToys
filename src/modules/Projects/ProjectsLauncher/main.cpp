#include "pch.h"

#include <ProjectsLib/ProjectsData.h>

#include <AppLauncher.h>

#include <common/utils/elevation.h>
#include <common/utils/gpo.h>
#include <common/utils/logger_helper.h>
#include <common/utils/process_path.h>
#include <common/utils/UnhandledExceptionHandler.h>

const std::wstring moduleName = L"Projects\\ProjectsLauncher";
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
        Logger::warn("Projects Launcher is elevated, restart");

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
                // TODO: show error message
                Logger::critical("Incorrect Projects file");
                return 1;
            }
        }
        else
        {
            // TODO: show error message
            Logger::critical("Incorrect Projects file");
            return 1;
        }
    }
    catch (std::exception ex)
    {
        Logger::error("Exception on reading projects: {}", ex.what());
        return 1;
    }

    if (projects.empty())
    {
        Logger::warn("Projects file is empty");
        return 1;
    }

    ProjectsData::Project projectToLaunch{};
    std::string idStr(cmdline);
    std::wstring id(idStr.begin(), idStr.end());
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
        Logger::info(L"Project {} not found", id);
        return 1;
    }

    Logger::info(L"Launch Project {} : {}", projectToLaunch.name, projectToLaunch.id);

    // launch apps
    projectToLaunch = Launch(projectToLaunch);
    
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

    CoUninitialize();
    return 0;
}
