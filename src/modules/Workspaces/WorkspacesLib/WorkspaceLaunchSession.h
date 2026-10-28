#pragma once

#include <WorkspacesLib/WorkspacesRepository.h>

namespace Workspaces
{
    // The reference identifies a session, not an owner or a bearer authorization.
    class LaunchSession
    {
    public:
        LaunchSession();
        std::wstring Arguments() const;
        void Send(HANDLE arranger, const WorkspacesData::WorkspacesProject& project);
        static WorkspacesData::WorkspacesProject Receive(const std::wstring& reference);

    private:
        std::wstring nonce;
        uint64_t birth;
        PowerToys::ProtectedStorage::SignedClientCatalog catalog;
        PowerToys::ProtectedStorage::Handle pipe;
    };
}
