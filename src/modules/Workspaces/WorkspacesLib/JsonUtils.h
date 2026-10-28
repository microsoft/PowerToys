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

    // Explicit import/export codecs only. Live storage uses Workspaces::Repository.
    Result<WorkspacesData::WorkspacesProject, WorkspacesFileError> ImportSingleWorkspace(const std::wstring& fileName);
    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ImportWorkspaces(const std::wstring& fileName);

    bool Export(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects);
    bool Export(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project);
}