#include "pch.h"
#include "JsonUtils.h"

#include <filesystem>

#include <common/logger/logger.h>

namespace JsonUtils
{
    Result<WorkspacesData::WorkspacesProject, WorkspacesFileError> ReadSingleWorkspace(const std::wstring& fileName)
    {
        if (std::filesystem::exists(fileName))
        {
            try
            {
                auto tempWorkspacesJson = json::from_file(fileName);
                if (tempWorkspacesJson.has_value())
                {
                    auto tempWorkspace = WorkspacesData::WorkspacesProjectJSON::FromJson(tempWorkspacesJson.value());
                    if (tempWorkspace.has_value())
                    {
                        return Ok(tempWorkspace.value());
                    }
                    else
                    {
                        Logger::critical("Incorrect Workspaces file");
                        return Error(WorkspacesFileError::IncorrectFileError);
                    }
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    return Error(WorkspacesFileError::IncorrectFileError);
                }
            }
            catch (std::exception ex)
            {
                Logger::critical("Exception on reading Workspaces file: {}", ex.what());
                return Error(WorkspacesFileError::FileReadingError);
            }
        }

        return Ok(WorkspacesData::WorkspacesProject{});
    }

    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspaces(const std::wstring& fileName)
    {
        try
        {
            auto savedWorkspacesJson = json::from_file(fileName);
            if (savedWorkspacesJson.has_value())
            {
                auto savedWorkspaces = WorkspacesData::WorkspacesListJSON::FromJson(savedWorkspacesJson.value());
                if (savedWorkspaces.has_value())
                {
                    return Ok(savedWorkspaces.value());
                }
                else
                {
                    Logger::critical("Incorrect Workspaces file");
                    return Error(WorkspacesFileError::IncorrectFileError);
                }
            }
            else
            {
                Logger::critical("Incorrect Workspaces file");
                return Error(WorkspacesFileError::IncorrectFileError);
            }
        }
        catch (std::exception ex)
        {
            Logger::critical("Exception on reading Workspaces file: {}", ex.what());
            return Error(WorkspacesFileError::FileReadingError);
        }
    }

    bool Write(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesListJSON::ToJson(projects));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
            return false;
        }

        return true;
    }

    bool Write(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project)
    {
        try
        {
            json::to_file(fileName, WorkspacesData::WorkspacesProjectJSON::ToJson(project));
        }
        catch (std::exception ex)
        {
            Logger::error("Error writing workspaces file. {}", ex.what());
            return false;
        }

        return true;
    }
}
