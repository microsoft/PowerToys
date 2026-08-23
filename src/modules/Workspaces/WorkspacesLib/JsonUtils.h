#pragma once

#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>

namespace JsonUtils
{
    enum class WorkspacesFileError
    {
        FileReadingError,
        IncorrectFileError,
    };

    Result<WorkspacesData::WorkspacesProject, WorkspacesFileError> ReadSingleWorkspace(const std::wstring& fileName);
    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspaces(const std::wstring& fileName);

    bool Write(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects);
    bool Write(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project);
}