#pragma once

#include <WorkspacesLib/Result.h>
#include <WorkspacesLib/WorkspacesData.h>

namespace JsonUtils
{
    enum class WorkspacesFileError
    {
        FileReadingError,
        IncorrectFileError,
        ServiceAccessError,   // the settings service rejected/failed the request
                              // (e.g. caller not authorized after an update) —
                              // this is NOT "empty"; data is intact but unreachable.
    };

    Result<WorkspacesData::WorkspacesProject, WorkspacesFileError> ReadSingleWorkspace(const std::wstring& fileName);
    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspaces(const std::wstring& fileName);

    // v6: read/write the workspaces list through the PTSettingsSvc service
    // (PTSettingsClient GetBlob/PutBlob) so the protected %ProgramData% store is
    // the single source of truth.  Both fall back to direct file IO on
    // WorkspacesData::WorkspacesFile() only when the service is unavailable
    // (no-admin / declined-UAC).
    Result<std::vector<WorkspacesData::WorkspacesProject>, WorkspacesFileError> ReadWorkspacesFromService();
    bool WriteWorkspacesToService(const std::vector<WorkspacesData::WorkspacesProject>& projects);

    bool Write(const std::wstring& fileName, const std::vector<WorkspacesData::WorkspacesProject>& projects);
    bool Write(const std::wstring& fileName, const WorkspacesData::WorkspacesProject& project);
}