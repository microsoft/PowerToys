#include "pch.h"

#include <iostream>

#include "../projects-common/Data.h"

#include "AppLauncher.h"

int APIENTRY WinMain(HINSTANCE hInst, HINSTANCE hInstPrev, PSTR cmdline, int cmdshow)
{
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
    catch (std::exception)
    {
        return 1;
    }

    if (projects.empty())
    {
        return 1;
    }

    Project projectToLaunch = projects[0];

    int len = MultiByteToWideChar(CP_ACP, 0, cmdline, -1, NULL, 0);
    if (len > 1)
    {
        std::wstring id(len, L'\0');
        MultiByteToWideChar(CP_ACP, 0, cmdline, -1, &id[0], len);

        for (const auto& proj : projects)
        {
            if (proj.id == id)
            {
                projectToLaunch = proj;
                break;
            }
        }
    }

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
