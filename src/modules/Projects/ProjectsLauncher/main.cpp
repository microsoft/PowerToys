#include "pch.h"

#include <iostream>

#include <projects-common/Data.h>

#include <AppLauncher.h>

#include <common/utils/logger_helper.h>
#include <common/utils/UnhandledExceptionHandler.h>

const std::wstring moduleName = L"Projects\\ProjectsLauncher";
const std::wstring internalPath = L"";

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, LPSTR cmdline, int cmdShow)
{
    LoggerHelpers::init_logger(moduleName, internalPath, LogSettings::projectsLauncherLoggerName);
    InitUnhandledExceptionHandler();  

    // read projects
    std::vector<Project> projects;
    try
    {
        auto savedProjectsJson = json::from_file(JsonUtils::ProjectsFile());
        if (savedProjectsJson.has_value())
        {
            auto savedProjects = JsonUtils::ProjectsListJSON::FromJson(savedProjectsJson.value());
            if (savedProjects.has_value())
            {
                projects = savedProjects.value();
            }
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

    Project projectToLaunch = projects[0];

    std::string idStr(cmdline);
    std::wstring id(idStr.begin(), idStr.end());
    Logger::info(L"command line: {}", id);
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

    Logger::info(L"Launch Project {} : {}", projectToLaunch.name, projectToLaunch.id);

    // launch apps
    Launch(projectToLaunch);
    
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
    json::to_file(JsonUtils::ProjectsFile(), JsonUtils::ProjectsListJSON::ToJson(projects));

    return 0;
}
