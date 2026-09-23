#include "pch.h"
#include "WorkspaceLaunchSession.h"

#include <memory>

namespace Workspaces
{
    namespace Storage = PowerToys::ProtectedStorage;
    namespace
    {
        std::wstring PipeName(const std::wstring& reference)
        {
            return L"\\\\.\\pipe\\PowerToys.Workspaces.Plan." + reference;
        }

        void RequireSameSession(HANDLE process)
        {
            DWORD peerSession = 0, ownSession = 0;
            Storage::Check(process && WaitForSingleObject(process, 0) == WAIT_TIMEOUT &&
                ProcessIdToSessionId(GetProcessId(process), &peerSession) &&
                ProcessIdToSessionId(GetCurrentProcessId(), &ownSession) && peerSession == ownSession,
                Storage::ErrorCode::Unauthorized);
        }

        void AllowAdministratorIdentityQuery()
        {
            const auto owner = Storage::TokenSid();
            const auto grant = [&](HANDLE object, DWORD access)
            {
                PACL previous = nullptr;
                PSECURITY_DESCRIPTOR raw = nullptr;
                auto error = GetSecurityInfo(object, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                    nullptr, nullptr, &previous, nullptr, &raw);
                Storage::Check(error == ERROR_SUCCESS && previous, Storage::ErrorCode::Unauthorized, error);
                std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
                auto updated = Storage::BuildIdentityQueryAcl(previous, L"S-1-5-32-544", owner, access);
                std::unique_ptr<void, decltype(&LocalFree)> acl(updated, LocalFree);
                error = SetSecurityInfo(object, SE_KERNEL_OBJECT, DACL_SECURITY_INFORMATION,
                    nullptr, nullptr, updated, nullptr);
                Storage::Check(error == ERROR_SUCCESS, Storage::ErrorCode::Unauthorized, error);
            };

            // An OTS administrator is not known until UAC completes. This grants only
            // identity observation on this launcher, never VM_READ or mutation.
            grant(GetCurrentProcess(), PROCESS_QUERY_INFORMATION | PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE);
            HANDLE raw = nullptr;
            Storage::Check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | READ_CONTROL | WRITE_DAC, &raw),
                Storage::ErrorCode::Unauthorized, GetLastError());
            Storage::Handle token(raw);
            grant(token.get(), TOKEN_QUERY);
        }

        std::wstring Reference(DWORD pid, uint64_t birth, const std::wstring& nonce)
        {
            return std::to_wstring(pid) + L"." + std::to_wstring(birth) + L"." + nonce;
        }

        Storage::Frame MakeFrame(Storage::DataCommand command, const char* phase, const std::wstring& nonce,
            DWORD launcher, uint64_t launcherBirth, DWORD arranger, uint64_t arrangerBirth,
            const std::wstring& owner, std::vector<BYTE> bytes = {})
        {
            return {command, Storage::GuidBytes(Storage::Utf8(nonce)),
                Storage::Value::Object{{"phase", phase}, {"launcher", static_cast<uint64_t>(launcher)},
                    {"launcherBirth", std::to_string(launcherBirth)}, {"arranger", static_cast<uint64_t>(arranger)},
                    {"arrangerBirth", std::to_string(arrangerBirth)}, {"owner", Storage::Utf8(owner)}}, std::move(bytes)};
        }

        void CheckFrame(const Storage::Frame& frame, Storage::DataCommand command, const char* phase,
            const std::wstring& nonce, DWORD launcher, uint64_t launcherBirth,
            DWORD arranger, uint64_t arrangerBirth, const std::wstring& owner)
        {
            Storage::Check(frame.command == command && frame.requestId == Storage::GuidBytes(Storage::Utf8(nonce)) &&
                frame.metadata.At("phase").Text() == phase &&
                frame.metadata.At("launcher").Number() == launcher &&
                Storage::Decimal(frame.metadata.At("launcherBirth").Text()) == launcherBirth &&
                frame.metadata.At("arranger").Number() == arranger &&
                Storage::Decimal(frame.metadata.At("arrangerBirth").Text()) == arrangerBirth &&
                frame.metadata.At("owner").Text() == Storage::Utf8(owner), Storage::ErrorCode::Unauthorized);
        }
    }

    LaunchSession::LaunchSession() :
        nonce(Storage::NewId()),
        birth(Storage::ProcessBirth(GetCurrentProcess())),
        catalog(Storage::ProtectedStoreClient().GetSignedClientCatalog())
    {
        const auto owner = Storage::TokenSid();
        Storage::VerifySignedClientCatalog(catalog, Storage::LoadPolicy(Storage::Paths(owner)));
        AllowAdministratorIdentityQuery();
        const auto descriptor = L"D:P(A;;GA;;;" + owner + L")(A;;GA;;;BA)(A;;GA;;;SY)S:(ML;;NW;;;ME)";
        PSECURITY_DESCRIPTOR security = nullptr;
        Storage::Check(ConvertStringSecurityDescriptorToSecurityDescriptorW(descriptor.c_str(), SDDL_REVISION_1, &security, nullptr),
            Storage::ErrorCode::Unauthorized);
        SECURITY_ATTRIBUTES attributes{sizeof(attributes), security, FALSE};
        pipe.reset(CreateNamedPipeW(PipeName(Reference(GetCurrentProcessId(), birth, nonce)).c_str(),
            PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
            PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
            1, 65536, 65536, 0, &attributes));
        LocalFree(security);
        Storage::Check(static_cast<bool>(pipe), Storage::ErrorCode::Unauthorized, GetLastError());
    }

    std::wstring LaunchSession::Arguments() const
    {
        return L"--plan=" + Reference(GetCurrentProcessId(), birth, nonce);
    }

    void LaunchSession::Send(HANDLE arranger, const WorkspacesData::WorkspacesProject& project)
    {
        RequireSameSession(arranger);
        const auto arrangerBirth = Storage::ProcessBirth(arranger);
        const auto owner = Storage::TokenSid();
        Storage::Handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        Storage::Check(static_cast<bool>(event), Storage::ErrorCode::InternalError, GetLastError());
        OVERLAPPED overlapped{};
        overlapped.hEvent = event.get();
        if (!ConnectNamedPipe(pipe.get(), &overlapped))
        {
            const auto error = GetLastError();
            if (error == ERROR_IO_PENDING)
            {
                if (WaitForSingleObject(event.get(), 10000) != WAIT_OBJECT_0)
                {
                    CancelIoEx(pipe.get(), &overlapped);
                    DWORD ignored = 0;
                    GetOverlappedResult(pipe.get(), &overlapped, &ignored, TRUE);
                    throw Storage::StorageError(Storage::ErrorCode::Timeout);
                }
                DWORD transferred = 0;
                Storage::Check(GetOverlappedResult(pipe.get(), &overlapped, &transferred, FALSE),
                    Storage::ErrorCode::Unauthorized);
            }
            else
            {
                Storage::Check(error == ERROR_PIPE_CONNECTED, Storage::ErrorCode::Unauthorized);
            }
        }

        ULONG pid = 0;
        Storage::Check(GetNamedPipeClientProcessId(pipe.get(), &pid) && pid == GetProcessId(arranger),
            Storage::ErrorCode::Unauthorized);

        // The catalog is public signed identity metadata, not workspace data.
        auto proof = MakeFrame(Storage::DataCommand::GetCapabilities, "catalog", nonce,
            GetCurrentProcessId(), birth, pid, arrangerBirth, owner, catalog.document);
        proof.bytes.insert(proof.bytes.end(), catalog.signature.begin(), catalog.signature.end());
        std::get<Storage::Value::Object>(proof.metadata.data)["catalogLength"] = static_cast<uint64_t>(catalog.document.size());
        Storage::WriteFrame(pipe.get(), proof);

        auto ready = Storage::ReadFrame(pipe.get());
        CheckFrame(ready, Storage::DataCommand::GetCapabilities, "peer-ready", nonce,
            GetCurrentProcessId(), birth, pid, arrangerBirth, owner);
        Storage::Check(ready.bytes.empty(), Storage::ErrorCode::InvalidPayload);
        RequireSameSession(arranger);
        const auto peer = Storage::VerifyPeerImage(arranger, arrangerBirth, owner, catalog, "workspaces.arranger",
            Storage::Parent(Storage::ModulePath()) + L"\\PowerToys.WorkspacesWindowArranger.exe");
        Storage::Check(GetNamedPipeClientProcessId(pipe.get(), &pid) && pid == peer.pid, Storage::ErrorCode::Unauthorized);
        auto plan = MakeFrame(Storage::DataCommand::GetBlob, "plan", nonce,
            GetCurrentProcessId(), birth, peer.pid, peer.birth, owner, Repository::SerializeProject(project));
        Storage::WriteFrame(pipe.get(), plan);
    }

    WorkspacesData::WorkspacesProject LaunchSession::Receive(const std::wstring& reference)
    {
        const auto first = reference.find(L'.');
        const auto second = reference.find(L'.', first == reference.npos ? first : first + 1);
        Storage::Check(first != reference.npos && second != reference.npos && reference.size() <= 96,
            Storage::ErrorCode::InvalidPayload);
        const auto pidNumber = Storage::Decimal(Storage::Utf8(reference.substr(0, first)));
        const auto birth = Storage::Decimal(Storage::Utf8(reference.substr(first + 1, second - first - 1)));
        const auto nonce = reference.substr(second + 1);
        Storage::Check(pidNumber > 0 && pidNumber <= MAXDWORD && Storage::ValidId(nonce), Storage::ErrorCode::InvalidPayload);
        const auto pid = static_cast<DWORD>(pidNumber);
        Storage::Handle pipe(CreateFileW(PipeName(reference).c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING,
            FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION, nullptr));
        Storage::Check(static_cast<bool>(pipe), Storage::ErrorCode::Unauthorized, GetLastError());
        ULONG server = 0;
        Storage::Check(GetNamedPipeServerProcessId(pipe.get(), &server) && server == pid, Storage::ErrorCode::Unauthorized);
        Storage::Handle launcher(OpenProcess(PROCESS_QUERY_INFORMATION | SYNCHRONIZE, FALSE, pid));
        RequireSameSession(launcher.get());
        Storage::Check(Storage::ProcessBirth(launcher.get()) == birth, Storage::ErrorCode::Unauthorized);
        const auto owner = Storage::TokenSid(launcher.get());
        Storage::Check(Storage::ValidOwner(owner), Storage::ErrorCode::Unauthorized);
        const auto ownBirth = Storage::ProcessBirth(GetCurrentProcess());

        const auto proof = Storage::ReadFrame(pipe.get());
        CheckFrame(proof, Storage::DataCommand::GetCapabilities, "catalog", nonce,
            pid, birth, GetCurrentProcessId(), ownBirth, owner);
        const auto length = proof.metadata.At("catalogLength").Number();
        Storage::Check(length > 0 && length <= Storage::MaxMetadata && length < proof.bytes.size() &&
            proof.bytes.size() - length <= 1024 * 1024, Storage::ErrorCode::InvalidPayload);
        Storage::SignedClientCatalog catalog{
            {proof.bytes.begin(), proof.bytes.begin() + static_cast<size_t>(length)},
            {proof.bytes.begin() + static_cast<size_t>(length), proof.bytes.end()}};
        Storage::VerifySignedClientCatalog(catalog, Storage::LoadPolicy(Storage::Paths(owner)));
        const auto peer = Storage::VerifyPeerImage(launcher.get(), birth, owner, catalog, "workspaces.launcher",
            Storage::Parent(Storage::ModulePath()) + L"\\PowerToys.WorkspacesLauncher.exe");

        // Grant only to the original owner derived from the verified actual launcher,
        // not to a SID supplied by the command line or the placement payload.
        Storage::AllowPeerIdentityQuery(peer.owner);
        auto ready = MakeFrame(Storage::DataCommand::GetCapabilities, "peer-ready", nonce,
            pid, birth, GetCurrentProcessId(), ownBirth, owner);
        Storage::WriteFrame(pipe.get(), ready);
        const auto plan = Storage::ReadFrame(pipe.get());
        CheckFrame(plan, Storage::DataCommand::GetBlob, "plan", nonce,
            pid, birth, GetCurrentProcessId(), ownBirth, owner);
        Storage::Check(GetNamedPipeServerProcessId(pipe.get(), &server) && server == peer.pid &&
            WaitForSingleObject(peer.process.get(), 0) == WAIT_TIMEOUT, Storage::ErrorCode::Unauthorized);
        return Repository::ParseProject(plan.bytes);
    }
}
