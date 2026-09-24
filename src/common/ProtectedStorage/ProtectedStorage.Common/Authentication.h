#pragma once
#include "Protocol.h"
#include <set>

namespace PowerToys::ProtectedStorage
{
    struct CallerIdentity
    {
        Handle process;
        DWORD pid = 0;
        uint64_t birth = 0;
        std::wstring owner;
        std::string imageHash;
        std::set<std::string> roles;
        std::vector<Handle> pinnedDirectories;
        std::vector<Handle> pinnedImages;
    };
    struct SignedClientCatalog
    {
        std::vector<BYTE> document;
        std::vector<BYTE> signature;
    };
    void VerifyDetached(HANDLE content, HANDLE signature, const Policy& policy);
    SignedClientCatalog ReadSignedClientCatalog(const std::wstring& directory);
    Value VerifySignedClientCatalog(const SignedClientCatalog& proof, const Policy& policy, std::optional<uint64_t> release = {});
    // Schema validation only; authorization must enter through VerifySignedClientCatalog.
    Value ParseClientCatalogDocument(const std::vector<BYTE>& document, const Policy& policy, std::optional<uint64_t> release = {});
    Value VerifyClientCatalog(const std::wstring& directory, const Policy& policy, uint64_t release);
    CallerIdentity VerifyPeerImage(HANDLE process, uint64_t expectedBirth, const std::wstring& originalOwnerSid, const SignedClientCatalog& proof, const std::string& expectedRole, const std::wstring& expectedImagePath);
    void AllowPeerIdentityQuery(const std::wstring& peerSid);
    void RequireOrdinaryMaintenanceProcess(HANDLE process);
    CallerIdentity AuthenticateOwner(HANDLE pipe, const Paths& paths);
    CallerIdentity AuthenticateCaller(HANDLE pipe, const Paths& paths, bool maintenance = false, const Value* candidateCatalog = nullptr);
    std::set<std::string> CatalogRoles(const Value& catalog, std::wstring_view imageName, std::string_view imageHash, bool maintenance);
    void AuthorizeTarget(const CallerIdentity& caller, std::string_view target, DataCommand command);
    void AuthorizeRequest(const CallerIdentity& caller, const Frame& request);
    void VerifyServer(HANDLE pipe, const Paths& paths, bool bootstrap);
    void VerifyLiveRelease(const Paths& paths);
    Handle ConnectOwnerPipe(const Paths& paths, const std::wstring& endpoint, DWORD timeout);
    Handle OpenOwnerConnectionToken(const std::wstring& owner);
    constexpr bool UsableOwnerTokenType(TOKEN_TYPE type, SECURITY_IMPERSONATION_LEVEL level)
    {
        return type == TokenPrimary || (type == TokenImpersonation && level == SecurityImpersonation);
    }
    void VerifyProcessImageFile(HANDLE process, HANDLE executable);
}
