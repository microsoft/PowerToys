#pragma once

#include <vector>

#include <projects-common/Data.h>

#include <common/logger/logger.h>

namespace ProjectsJsonUtils
{
    inline std::vector<Project> Read(const std::wstring& fileName)
    {
        std::vector<Project> projects{};
        try
        {
            auto savedProjectsJson = json::from_file(fileName);
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
            Logger::error("Error reading projects file. {}", ex.what());
        }

        return projects;
    }

    inline void Write(const std::wstring& fileName, const std::vector<Project>& projects)
    {
        try
        {
            json::to_file(fileName, JsonUtils::ProjectsListJSON::ToJson(projects));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing projects file. {}", ex.what());
        }
    }
}