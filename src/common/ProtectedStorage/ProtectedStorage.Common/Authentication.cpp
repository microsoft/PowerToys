#include "Authentication.h"
#include "SignatureTrust.h"
#include <algorithm>
#include <memory>
#include <winternl.h>

namespace PowerToys::ProtectedStorage
{
    namespace
    {
        class PipeImpersonation
        {
        public:
            explicit PipeImpersonation(HANDLE pipe)
            {
                Check(ImpersonateNamedPipeClient(pipe) != FALSE, ErrorCode::Unauthorized, GetLastError());
            }
            ~PipeImpersonation()
            {
                if (!RevertToSelf())
                    TerminateProcess(GetCurrentProcess(), ERROR_CANNOT_IMPERSONATE);
            }
            PipeImpersonation(const PipeImpersonation&) = delete;
            PipeImpersonation& operator=(const PipeImpersonation&) = delete;
        };
        void RequireFilteredPipeToken()
        {
            HANDLE raw = nullptr;
            Check(OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &raw) != FALSE, ErrorCode::Unauthorized, GetLastError());
            Handle token(raw);
            TOKEN_ELEVATION elevation{};
            DWORD size = 0;
            Check(GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size) && !elevation.TokenIsElevated,
                  ErrorCode::Unauthorized);
            SECURITY_IMPERSONATION_LEVEL level{};
            Check(GetTokenInformation(token.get(), TokenImpersonationLevel, &level, sizeof(level), &size) &&
                      level == SecurityImpersonation,
                  ErrorCode::Unauthorized);
        }
        void VerifyProcess(HANDLE process, const Paths& paths, std::wstring_view image)
        {
            Check(WaitForSingleObject(process, 0) == WAIT_TIMEOUT && TokenSid(process) == paths.va &&
                      _wcsicmp(ProcessImage(process).c_str(), std::wstring(image).c_str()) == 0,
                  ErrorCode::Unauthorized);
        }
        DWORD ScmHost(const Paths& paths)
        {
            ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
            Check(static_cast<bool>(manager), ErrorCode::Unauthorized, GetLastError());
            ServiceHandle service(OpenServiceW(manager.get(), paths.service.c_str(), SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG));
            Check(static_cast<bool>(service), ErrorCode::Unauthorized, GetLastError());
            DWORD size = 0;
            QueryServiceConfigW(service.get(), nullptr, 0, &size);
            Check(size && size <= 32768, ErrorCode::Unauthorized);
            std::vector<BYTE> bytes(size);
            auto config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(bytes.data());
            Check(QueryServiceConfigW(service.get(), config, size, &size) != FALSE, ErrorCode::Unauthorized, GetLastError());
            const auto expected = Quote(paths.code + L"\\Bootstrap.exe") + L" --service " + Quote(paths.owner);
            Check(config->dwServiceType == SERVICE_WIN32_OWN_PROCESS &&
                      _wcsicmp(config->lpBinaryPathName, expected.c_str()) == 0 &&
                      _wcsicmp(config->lpServiceStartName, (L"NT SERVICE\\" + paths.service).c_str()) == 0,
                  ErrorCode::Unauthorized);
            SERVICE_STATUS_PROCESS status{};
            Check(QueryServiceStatusEx(service.get(), SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&status), sizeof(status), &size) != FALSE,
                  ErrorCode::Unauthorized,
                  GetLastError());
            Check(status.dwProcessId && (status.dwCurrentState == SERVICE_RUNNING || status.dwCurrentState == SERVICE_START_PENDING),
                  ErrorCode::Unauthorized);
            return status.dwProcessId;
        }
    }
    void VerifyProcessImageFile(HANDLE process, HANDLE executable)
    {
        using Query = NTSTATUS(NTAPI*)(HANDLE, PROCESSINFOCLASS, PVOID, ULONG, PULONG);
        auto query = reinterpret_cast<Query>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationProcess"));
        Check(query != nullptr, ErrorCode::Unauthorized);
        // ProcessImageFileMapping binds a FILE_EXECUTE handle to the actual process image section.
        // A path/hash alone permits rename-and-replace of an already running executable.
        constexpr auto imageFileMapping = static_cast<PROCESSINFOCLASS>(44);
        Check(query(process, imageFileMapping, &executable, sizeof(executable), nullptr) >= 0, ErrorCode::Unauthorized);
    }
    static void VerifyDetachedBytes(const std::vector<BYTE>& bytes, const std::vector<BYTE>& signedBytes, const Policy& policy)
    {
        Check(!bytes.empty() && bytes.size() <= MaxMetadata && !signedBytes.empty() && signedBytes.size() <= 1024 * 1024,
              ErrorCode::QuotaExceeded);
        Check(policy.signerPolicy == ReleaseTrustPolicy, ErrorCode::Unauthorized);
        try
        {
            VerifyDetachedReleaseSignature(bytes, signedBytes);
        }
        catch (const Error& error)
        {
            throw StorageError(ErrorCode::Unauthorized, error.code);
        }
    }
    void VerifyDetached(HANDLE content, HANDLE signature, const Policy& policy)
    {
        VerifyDetachedBytes(ReadAll(content, MaxMetadata), ReadAll(signature, 1024 * 1024), policy);
    }
    SignedClientCatalog ReadSignedClientCatalog(const std::wstring& directory)
    {
        auto ancestors = HoldDirectories(directory);
        auto content = OpenRead(directory + L"\\ClientCatalog.json", MaxMetadata);
        auto signature = OpenRead(directory + L"\\ClientCatalog.p7s", 1024 * 1024);
        return { ReadAll(content.get(), MaxMetadata), ReadAll(signature.get(), 1024 * 1024) };
    }
    Value VerifySignedClientCatalog(const SignedClientCatalog& proof, const Policy& policy, std::optional<uint64_t> release)
    {
        VerifyDetachedBytes(proof.document, proof.signature, policy);
        return ParseClientCatalogDocument(proof.document, policy, release);
    }
    Value ParseClientCatalogDocument(const std::vector<BYTE>& document, const Policy& policy, std::optional<uint64_t> release)
    {
        Check(!document.empty() && document.size() <= MaxMetadata && policy.signerPolicy == ReleaseTrustPolicy, ErrorCode::Unauthorized);
        Value catalog = Value::Parse(std::string_view(reinterpret_cast<const char*>(document.data()), document.size()));
        const auto version = ParseVersion(catalog.At("version").Text());
        Check(catalog.At("format").Number() == 1 && catalog.At("app").Text() == Utf8(App) &&
                  version >= policy.minimum && (!release || version == *release),
              ErrorCode::IncompatibleVersion);
        const auto& clients = catalog.At("clients").Items();
        Check(!clients.empty() && clients.size() <= 256, ErrorCode::Unauthorized);
        const std::set<std::string> roles{ "workspaces.writer", "workspaces.reader", "workspaces.launcher", "workspaces.preview", "workspaces.arranger", "maintenance", "maintenance.ca" };
        for (const auto& client : clients)
        {
            const auto& image = client.At("image").Text();
            Check(!image.empty() && image.size() <= 255 && image.find_first_of("\\/:") == image.npos &&
                      IsHash(client.At("sha256").Text()) && roles.contains(client.At("role").Text()),
                  ErrorCode::Unauthorized);
        }
        return catalog;
    }
    Value VerifyClientCatalog(const std::wstring& directory, const Policy& policy, uint64_t release)
    {
        return VerifySignedClientCatalog(ReadSignedClientCatalog(directory), policy, release);
    }
    CallerIdentity VerifyPeerImage(HANDLE process, uint64_t expectedBirth, const std::wstring& originalOwnerSid, const SignedClientCatalog& proof, const std::string& expectedRole, const std::wstring& expectedImagePath)
    {
        Check(process && expectedBirth && IsAbsoluteLocal(expectedImagePath), ErrorCode::Unauthorized);
        Check(WaitForSingleObject(process, 0) == WAIT_TIMEOUT && ProcessBirth(process) == expectedBirth,
              ErrorCode::Unauthorized);
        CallerIdentity result;
        result.pid = GetProcessId(process);
        Check(result.pid != 0, ErrorCode::Unauthorized, GetLastError());
        result.process.reset(OpenProcess(PROCESS_QUERY_INFORMATION | SYNCHRONIZE, FALSE, result.pid));
        Check(static_cast<bool>(result.process) && ProcessBirth(result.process.get()) == expectedBirth, ErrorCode::Unauthorized);
        result.birth = expectedBirth;
        result.owner = TokenSid(result.process.get());
        const auto currentOwner = TokenSid();
        Check(ValidOwner(result.owner) && ValidOwner(currentOwner) &&
                  (originalOwnerSid == currentOwner || originalOwnerSid == result.owner),
              ErrorCode::Unauthorized);
        Paths authority(originalOwnerSid);
        std::vector<Handle> policyDirectories;
        for (const auto& directory : { authority.state, authority.state + L"\\Policy", Parent(authority.policy) })
        {
            auto ancestors = HoldDirectories(directory);
            CheckAcl(ancestors.back().get(), authority.va, false);
            for (auto& ancestor : ancestors)
                policyDirectories.push_back(std::move(ancestor));
        }
        const auto catalog = VerifySignedClientCatalog(proof, LoadPolicy(authority));
        const auto image = ProcessImage(result.process.get());
        Check(_wcsicmp(image.c_str(), expectedImagePath.c_str()) == 0, ErrorCode::Unauthorized);
        result.pinnedDirectories = HoldDirectories(Parent(image));
        auto executable = OpenRead(image);
        Handle imageIdentity(CreateFileW(image.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        Check(static_cast<bool>(imageIdentity), ErrorCode::Unauthorized, GetLastError());
        VerifyProcessImageFile(result.process.get(), imageIdentity.get());
        result.imageHash = Hash(executable.get());
        result.roles = CatalogRoles(catalog, image.substr(image.find_last_of(L'\\') + 1), result.imageHash, false);
        Check(result.roles.contains(expectedRole) && WaitForSingleObject(result.process.get(), 0) == WAIT_TIMEOUT &&
                  ProcessBirth(result.process.get()) == expectedBirth,
              ErrorCode::Unauthorized);
        result.pinnedImages.push_back(std::move(executable));
        return result;
    }
    void RequireOrdinaryMaintenanceProcess(HANDLE process)
    {
        HANDLE raw = nullptr;
        Check(OpenProcessToken(process, TOKEN_QUERY, &raw) != FALSE, ErrorCode::Unauthorized, GetLastError());
        Handle token(raw);
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        Check(GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size) != FALSE,
              ErrorCode::Unauthorized,
              GetLastError());
        Check(!elevation.TokenIsElevated, ErrorCode::Unauthorized, ERROR_ACCESS_DENIED);
    }
    CallerIdentity AuthenticateOwner(HANDLE pipe, const Paths& paths)
    {
        Check(PipeCaller(pipe) == paths.owner, ErrorCode::Unauthorized, ERROR_ACCESS_DENIED);
        ULONG pid = 0;
        Check(GetNamedPipeClientProcessId(pipe, &pid) && pid, ErrorCode::Unauthorized, GetLastError());
        CallerIdentity result;
        result.process.reset(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, pid));
        Check(static_cast<bool>(result.process), ErrorCode::Unauthorized, GetLastError());
        result.pid = pid;
        result.birth = ProcessBirth(result.process.get());
        result.owner = TokenSid(result.process.get());
        Check(result.owner == paths.owner && WaitForSingleObject(result.process.get(), 0) == WAIT_TIMEOUT, ErrorCode::Unauthorized);
        ULONG current = 0;
        Check(GetNamedPipeClientProcessId(pipe, &current) && current == pid &&
                  ProcessBirth(result.process.get()) == result.birth,
              ErrorCode::Unauthorized);
        return result;
    }
    std::set<std::string> CatalogRoles(const Value& catalog, std::wstring_view imageName, std::string_view imageHash, bool maintenance)
    {
        std::wstring name(imageName);
        std::transform(name.begin(), name.end(), name.begin(), towlower);
        std::set<std::string> result;
        for (const auto& client : catalog.At("clients").Items())
        {
            const auto& role = client.At("role").Text();
            const bool maintenanceRole = role == "maintenance" || role == "maintenance.ca";
            if (imageHash == client.At("sha256").Text() &&
                (_wcsicmp(name.c_str(), Wide(client.At("image").Text()).c_str()) == 0 ||
                 (maintenance && maintenanceRole)))
                result.insert(role == "maintenance.ca" ? "maintenance" : role);
        }
        return result;
    }
    CallerIdentity AuthenticateCaller(HANDLE pipe, const Paths& paths, bool maintenance, const Value* candidateCatalog)
    {
        auto result = AuthenticateOwner(pipe, paths);
        if (maintenance)
            RequireOrdinaryMaintenanceProcess(result.process.get());
        Handle process(OpenProcess(PROCESS_QUERY_INFORMATION | SYNCHRONIZE, FALSE, result.pid));
        Check(static_cast<bool>(process) && ProcessBirth(process.get()) == result.birth, ErrorCode::Unauthorized, GetLastError());
        result.process = std::move(process);
        auto image = ProcessImage(result.process.get());
        Value catalog;
        if (candidateCatalog)
        {
            Check(maintenance, ErrorCode::Unauthorized);
            Value::Array clients;
            for (const auto& entry : candidateCatalog->At("clients").Items())
            {
                if (entry.At("role").Text() == "maintenance" || entry.At("role").Text() == "maintenance.ca")
                    clients.push_back(entry);
            }
            catalog = Value::Object{ { "clients", std::move(clients) } };
        }
        else
            catalog = VerifyClientCatalog(paths.code, LoadPolicy(paths), OwnVersion());
        {
            PipeImpersonation impersonation(pipe);
            RequireFilteredPipeToken();
            result.pinnedDirectories = HoldDirectories(Parent(image));
            auto executable = OpenRead(image);
            Handle executableIdentity(CreateFileW(image.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            Check(static_cast<bool>(executableIdentity), ErrorCode::Unauthorized, GetLastError());
            VerifyProcessImageFile(result.process.get(), executableIdentity.get());
            result.imageHash = Hash(executable.get());
            result.pinnedImages.push_back(std::move(executable));
            result.roles = CatalogRoles(catalog, image.substr(image.find_last_of(L'\\') + 1), result.imageHash, maintenance);
        }
        ULONG current = 0;
        Check(GetNamedPipeClientProcessId(pipe, &current) && current == result.pid &&
                  ProcessBirth(result.process.get()) == result.birth && WaitForSingleObject(result.process.get(), 0) == WAIT_TIMEOUT,
              ErrorCode::Unauthorized);
        Check(!result.roles.empty() && (!maintenance || result.roles.contains("maintenance")), ErrorCode::Unauthorized, ERROR_ACCESS_DENIED);
        return result;
    }
    void AuthorizeTarget(const CallerIdentity& caller, std::string_view target, DataCommand command)
    {
        if (command == DataCommand::GetCapabilities)
            return;
        const bool repository = target == "workspaces.repository";
        const bool preview = target == "workspaces.preview";
        Check(repository || preview, ErrorCode::TargetDenied, ERROR_ACCESS_DENIED);
        const bool writer = caller.roles.contains("workspaces.writer");
        const bool launcher = caller.roles.contains("workspaces.launcher");
        const bool reader = caller.roles.contains("workspaces.reader");
        const bool transient = command >= DataCommand::CreateTransient;
        if (transient)
        {
            Check(preview && caller.roles.contains("workspaces.preview"), ErrorCode::TargetDenied);
            return;
        }
        Check(repository, ErrorCode::TargetDenied);
        const bool read = command == DataCommand::GetState || command == DataCommand::GetBlob || command == DataCommand::QueryWrite;
        Check(writer || launcher || (read && reader), ErrorCode::TargetDenied);
    }
    void VerifyServer(HANDLE pipe, const Paths& paths, bool bootstrap)
    {
        ULONG pid = 0;
        Check(GetNamedPipeServerProcessId(pipe, &pid) && pid, ErrorCode::Unauthorized, GetLastError());
        Handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, pid));
        Check(static_cast<bool>(process), ErrorCode::Unauthorized, GetLastError());
        auto birth = ProcessBirth(process.get());
        VerifyProcess(process.get(), paths, paths.code + (bootstrap ? L"\\Bootstrap.exe" : L"\\Runtime.exe"));
        DWORD host = ScmHost(paths);
        if (bootstrap)
            Check(pid == host, ErrorCode::Unauthorized);
        else
        {
            using Query = NTSTATUS(NTAPI*)(HANDLE, PROCESSINFOCLASS, PVOID, ULONG, PULONG);
            auto query = reinterpret_cast<Query>(GetProcAddress(GetModuleHandleW(L"ntdll.dll"), "NtQueryInformationProcess"));
            Check(query != nullptr, ErrorCode::Unauthorized);
            PROCESS_BASIC_INFORMATION info{};
            Check(query(process.get(), ProcessBasicInformation, &info, sizeof(info), nullptr) >= 0 &&
                      reinterpret_cast<ULONG_PTR>(info.Reserved3) == host,
                  ErrorCode::Unauthorized);
            Handle parent(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, host));
            Check(static_cast<bool>(parent), ErrorCode::Unauthorized, GetLastError());
            VerifyProcess(parent.get(), paths, paths.code + L"\\Bootstrap.exe");
            Check(ProcessBirth(parent.get()) <= birth, ErrorCode::Unauthorized);
        }
        ULONG again = 0;
        Check(GetNamedPipeServerProcessId(pipe, &again) && again == pid && ScmHost(paths) == host &&
                  ProcessBirth(process.get()) == birth && WaitForSingleObject(process.get(), 0) == WAIT_TIMEOUT,
              ErrorCode::Unauthorized);
    }
    void AuthorizeRequest(const CallerIdentity& caller, const Frame& request)
    {
        const auto target = request.metadata.Find("target");
        AuthorizeTarget(caller, target ? target->Text() : "", request.command);
        if (request.command == DataCommand::AcknowledgeSourceCleanup ||
            (request.command == DataCommand::PutBlob &&
             request.metadata.At("condition").At("kind").Text() == "IfUninitialized"))
            Check(caller.roles.contains("workspaces.writer"), ErrorCode::TargetDenied);
    }
    void VerifyLiveRelease(const Paths& paths)
    {
        auto bundle = ValidateBundle(paths.code, LoadPolicy(paths), OwnVersion());
        Check(bundle.version == OwnVersion(), ErrorCode::IncompatibleVersion);
        VerifyClientCatalog(paths.code, LoadPolicy(paths), bundle.version);
    }
    Handle OpenOwnerConnectionToken(const std::wstring& owner)
    {
        Check(ValidOwner(owner) && TokenSid() == owner, ErrorCode::Unauthorized);
        HANDLE raw = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, &raw))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        Handle token(raw);
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        if (!GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        if (elevation.TokenIsElevated)
        {
            TOKEN_LINKED_TOKEN value{};
            if (!GetTokenInformation(token.get(), TokenLinkedToken, &value, sizeof(value), &size))
                throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
            token.reset(value.LinkedToken);
        }
        if (!GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        Check(!elevation.TokenIsElevated, ErrorCode::OwnerContextRequired, ERROR_BAD_IMPERSONATION_LEVEL);
        TOKEN_TYPE type{};
        if (!GetTokenInformation(token.get(), TokenType, &type, sizeof(type), &size))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        SECURITY_IMPERSONATION_LEVEL level = SecurityAnonymous;
        if (type == TokenImpersonation &&
            !GetTokenInformation(token.get(), TokenImpersonationLevel, &level, sizeof(level), &size))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        Check(UsableOwnerTokenType(type, level), ErrorCode::OwnerContextRequired, ERROR_BAD_IMPERSONATION_LEVEL);
        size = 0;
        GetTokenInformation(token.get(), TokenUser, nullptr, 0, &size);
        Check(size && size <= 65536, ErrorCode::OwnerContextRequired);
        std::vector<BYTE> user(size);
        if (!GetTokenInformation(token.get(), TokenUser, user.data(), size, &size))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        Check(SidText(reinterpret_cast<TOKEN_USER*>(user.data())->User.Sid) == owner,
              ErrorCode::OwnerContextRequired,
              ERROR_INVALID_OWNER);
        return token;
    }
    Handle ConnectOwnerPipe(const Paths& paths, const std::wstring& endpoint, DWORD timeout)
    {
        auto token = OpenOwnerConnectionToken(paths.owner);
        AllowIdentityQuery(paths);
        if (!WaitNamedPipeW(endpoint.c_str(), timeout))
        {
            const DWORD error = GetLastError();
            throw StorageError(error == ERROR_FILE_NOT_FOUND ? ErrorCode::NotProvisioned :
                               error == ERROR_SEM_TIMEOUT    ? ErrorCode::Timeout :
                                                               ErrorCode::Unauthorized,
                               error);
        }
        HANDLE previousRaw = nullptr;
        Handle previous;
        if (OpenThreadToken(GetCurrentThread(), TOKEN_QUERY | TOKEN_IMPERSONATE, TRUE, &previousRaw))
            previous.reset(previousRaw);
        else if (GetLastError() != ERROR_NO_TOKEN)
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        if (!ImpersonateLoggedOnUser(token.get()))
            throw StorageError(ErrorCode::OwnerContextRequired, GetLastError());
        HANDLE pipe = CreateFileW(endpoint.c_str(), GENERIC_READ | GENERIC_WRITE, 0, nullptr, OPEN_EXISTING, FILE_FLAG_OVERLAPPED | SECURITY_SQOS_PRESENT | SECURITY_IMPERSONATION, nullptr);
        const DWORD error = GetLastError();
        if (!(previous ? SetThreadToken(nullptr, previous.get()) : RevertToSelf()))
            TerminateProcess(GetCurrentProcess(), ERROR_CANNOT_IMPERSONATE);
        Handle result(pipe);
        Check(static_cast<bool>(result), error == ERROR_BAD_IMPERSONATION_LEVEL || error == ERROR_CANNOT_IMPERSONATE ? ErrorCode::OwnerContextRequired : ErrorCode::Unauthorized, error);
        return result;
    }
}
