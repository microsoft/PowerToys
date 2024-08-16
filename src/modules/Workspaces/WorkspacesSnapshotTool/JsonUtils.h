#pragma once

#include <vector>

#include <WorkspacesLib/WorkspacesData.h>

#include <common/logger/logger.h>

namespace WorkspacesJsonUtils
{
    inline std::vector<WorkspacesData::WorkspacesProject> Read(const std::wstring& fileName)
    {
        std::vector<WorkspacesData::WorkspacesProject> projects{};
        try
        {
            auto savedProjectsJson = json::from_file(fileName);
            if (savedProjectsJson.has_value())
            {
                auto savedProjects = WorkspacesData::WorkspacesListJSON::FromJson(savedProjectsJson.value());
                if (savedProjects.has_value())
                {
                    projects = savedProjects.value();
                }
            }
        }
        catch (std::exception ex)
        {
            Logger::error("Error reading workspaces file. {}", ex.what());
        }

        return projects;
    }

    inline void Write(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesListJSON::ToJson(projects));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
        }
    }

    inline void Write(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesProjectJSON::ToJson(project));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
        }
    }
}