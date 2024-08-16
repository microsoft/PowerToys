#pragma once

#include <vector>

#include <ProjectsLib/ProjectsData.h>

#include <common/logger/logger.h>

namespace ProjectsJsonUtils
{
    inline std::vector<ProjectsData::Project> Read(const std::wstring& fileName)
    {
        std::vector<ProjectsData::Project> projects{};
        try
        {
            auto savedProjectsJson = json::from_file(fileName);
            if (savedProjectsJson.has_value())
            {
                auto savedProjects = ProjectsData::ProjectsListJSON::FromJson(savedProjectsJson.value());
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

    inline void Write(const std::wstring& fileName, const std::vector<ProjectsData::Project>& projects)
    {
        try
        {
            json::to_file(fileName, ProjectsData::ProjectsListJSON::ToJson(projects));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing projects file. {}", ex.what());
        }
    }

    inline void Write(const std::wstring& fileName, const ProjectsData::Project& project)
    {
        try
        {
            json::to_file(fileName, ProjectsData::ProjectJSON::ToJson(project));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing projects file. {}", ex.what());
        }
    }
}