#pragma once

#include <WorkspacesLib/WorkspacesData.h>
#include <common/ProtectedStorage/ProtectedStorage.Client/ProtectedStoreClient.h>

namespace Workspaces
{
    struct RepositorySnapshot
    {
        std::vector<WorkspacesData::WorkspacesProject> projects;
        PowerToys::ProtectedStorage::Revision revision;
    };

    class Repository
    {
    public:
        RepositorySnapshot Load();
        WorkspacesData::WorkspacesProject ReadPreview(const std::string& id);
        std::string CreatePreview(const WorkspacesData::WorkspacesProject& project);
        void UpdateLaunchMetadata(const WorkspacesData::WorkspacesProject& original,
                                  const WorkspacesData::WorkspacesProject& updated,
                                  std::optional<time_t> launched = std::nullopt);
        static void Validate(const std::vector<WorkspacesData::WorkspacesProject>& projects, bool preview = false);
        static bool SameIdentity(const std::wstring& first, const std::wstring& second);
        static WorkspacesData::WorkspacesProject ParseProject(const std::vector<BYTE>& bytes);
        static std::vector<WorkspacesData::WorkspacesProject> ParseDocument(const std::vector<BYTE>& bytes);
        static std::vector<BYTE> SerializeProject(const WorkspacesData::WorkspacesProject& project);
        static void MergeLaunchMetadata(std::vector<WorkspacesData::WorkspacesProject>& projects,
                                        const WorkspacesData::WorkspacesProject& original,
                                        const WorkspacesData::WorkspacesProject& updated,
                                        std::optional<time_t> launched);

    private:
        void VerifyReady();
        PowerToys::ProtectedStorage::ProtectedStoreClient client;
        std::optional<PowerToys::ProtectedStorage::WriteRequest> pending;
    };
}
