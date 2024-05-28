#include "pch.h"

#include <iostream>

#include "../projects-common/Data.h"

#include "AppLauncher.h"

int main(int argc, char* argv[])
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

    if (argc > 1)
    {
        std::string idStr = argv[1];
        std::wstring id(idStr.begin(), idStr.end());
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
    for (const auto& app : projectToLaunch.apps)
    {
        Launch(app);
    }

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
