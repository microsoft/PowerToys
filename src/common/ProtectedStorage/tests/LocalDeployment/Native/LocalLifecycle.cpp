#include "../../../ProtectedStorage.Common/Authentication.h"
#include "../../../ProtectedStorage.Common/SignatureTrust.h"
#include "../../../ProtectedStorage.Runtime/BlobStore.h"
#include <filesystem>
#include <iostream>
#include <memory>
#include <userenv.h>
#include <shellapi.h>

using namespace PowerToys::ProtectedStorage;
namespace fs = std::filesystem;

namespace
{
    static_assert(std::wstring_view(App) == L"PowerToysProtectedStorageLocalTest");
    std::wstring action, owner, source;
    SERVICE_STATUS_HANDLE statusHandle = nullptr;

    std::wstring Known(REFKNOWNFOLDERID id)
    {
        PWSTR raw = nullptr;
        if (FAILED(SHGetKnownFolderPath(id, KF_FLAG_DEFAULT, nullptr, &raw)))
            Fail("local-test known folder");
        std::wstring result(raw);
        CoTaskMemFree(raw);
        return result;
    }
    struct Layout
    {
        std::wstring service = std::wstring(App) + L"_" + owner;
        std::wstring root = Known(FOLDERID_ProgramFiles) + L"\\" + App;
        std::wstring state = Known(FOLDERID_ProgramData) + L"\\" + App;
        std::wstring ownerRoot = root + L"\\Owners\\" + owner;
        std::wstring code = ownerRoot + L"\\Code";
        std::wstring data = state + L"\\Data\\" + owner;
        std::wstring policy = state + L"\\Policy\\" + owner;
        std::wstring record = state + L"\\Installer\\" + owner;
        std::wstring image = Quote(code + L"\\Bootstrap.exe") + L" --service " + Quote(owner);
    };
    const std::wstring SharedAcl = L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;GRGX;;;BU)";
    std::wstring ServiceSid(std::wstring name)
    {
        std::transform(name.begin(), name.end(), name.begin(), towupper);
        std::array<BYTE, 20> digest{};
        if (BCryptHash(BCRYPT_SHA1_ALG_HANDLE, nullptr, 0, reinterpret_cast<BYTE*>(name.data()),
            static_cast<ULONG>(name.size() * sizeof(wchar_t)), digest.data(), static_cast<ULONG>(digest.size())) < 0)
            Fail("derive local-test service SID", ERROR_GEN_FAILURE);
        std::array<DWORD, 5> parts{};
        memcpy(parts.data(), digest.data(), digest.size());
        std::wstring sid = L"S-1-5-80";
        for (auto part : parts) sid += L"-" + std::to_wstring(part);
        return sid;
    }
    void Acl(const std::wstring& path, const std::wstring& text)
    {
        PSECURITY_DESCRIPTOR raw = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(text.c_str(), SDDL_REVISION_1, &raw, nullptr))
            Fail("local-test security descriptor");
        std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
        if (!SetFileSecurityW(path.c_str(), OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION |
            PROTECTED_DACL_SECURITY_INFORMATION, raw))
            Fail("local-test path ACL");
    }
    void Directory(const std::wstring& path, const std::wstring& acl, bool fresh = false)
    {
        if (!CreateDirectoryW(path.c_str(), nullptr))
        {
            if (GetLastError() != ERROR_ALREADY_EXISTS || fresh)
                Fail("local-test directory already exists; use the cleanup command");
            auto held = HoldDirectories(path);
            CheckAcl(held.back().get(), {}, false);
            return;
        }
        Acl(path, acl);
    }
    void Write(const std::wstring& path, std::string_view bytes)
    {
        WriteNew(path, bytes);
        PSECURITY_DESCRIPTOR raw = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(L"O:SY", SDDL_REVISION_1, &raw, nullptr))
            Fail("local-test file owner descriptor");
        std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
        if (!SetFileSecurityW(path.c_str(), OWNER_SECURITY_INFORMATION, raw))
            Fail("local-test file owner");
    }
    void CheckService(SC_HANDLE service, const Layout& p)
    {
        DWORD size = 0;
        QueryServiceConfigW(service, nullptr, 0, &size);
        Require(size && size <= 65536, "local-test service configuration size");
        std::vector<BYTE> buffer(size);
        auto config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.data());
        if (!QueryServiceConfigW(service, config, size, &size))
            Fail("local-test service configuration");
        Require(config->dwServiceType == SERVICE_WIN32_OWN_PROCESS &&
            p.image == config->lpBinaryPathName &&
            _wcsicmp(config->lpServiceStartName, (L"NT SERVICE\\" + p.service).c_str()) == 0,
            "refusing a service with an unexpected image or account");
    }
    SERVICE_STATUS_PROCESS State(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS state{};
        DWORD bytes = 0;
        if (!QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&state), sizeof(state), &bytes))
            Fail("local-test service status");
        return state;
    }
    void WaitState(SC_HANDLE service, DWORD expected)
    {
        const auto deadline = GetTickCount64() + 30000;
        do
        {
            const auto state = State(service);
            if (state.dwCurrentState == expected)
                return;
            if (expected == SERVICE_RUNNING && state.dwCurrentState == SERVICE_STOPPED)
                throw Error("local-test service failed to start", state.dwWin32ExitCode);
            Sleep(50);
        } while (GetTickCount64() < deadline);
        throw Error("local-test service transition timed out", ERROR_TIMEOUT);
    }
    void ValidateTree(const std::wstring& path)
    {
        if (!Exists(path))
            return;
        auto ancestors = HoldDirectories(path);
        for (const auto& entry : fs::recursive_directory_iterator(path))
        {
            const auto attributes = GetFileAttributesW(entry.path().c_str());
            Require(attributes != INVALID_FILE_ATTRIBUTES && !(attributes & FILE_ATTRIBUTE_REPARSE_POINT),
                "refusing cleanup of a tree containing a reparse point");
        }
    }
    Value DataHashes(const Layout& p)
    {
        ValidateTree(p.data);
        Value::Object hashes;
        for (const auto& entry : fs::directory_iterator(p.data))
        {
            const auto name = entry.path().filename().wstring();
            if (name != L"storage.index" && !name.starts_with(L"blob.")) continue;
            auto file = OpenRead(entry.path().wstring());
            hashes[Utf8(name)] = Hash(file.get());
        }
        Require(hashes.contains("storage.index"), "existing local-test data index is required");
        return hashes;
    }
    Value Execute()
    {
        Require(TokenSid() == L"S-1-5-18", "the fixed local-test lifecycle must run as SYSTEM");
        Require(ValidOwner(owner), "local-test owner must be a canonical user SID");
        Handle gate;
        if (action != L"inspect")
        {
            const auto name = L"Global\\" + std::wstring(App) + L"Lifecycle_" + owner;
            gate = Handle(CreateMutexW(nullptr, FALSE, name.c_str()));
            if (!gate) Fail("local-test lifecycle gate");
            const auto acquired = WaitForSingleObject(gate.get(), 0);
            if (acquired != WAIT_OBJECT_0)
                throw Error("another local-test lifecycle is active or interrupted",
                    acquired == WAIT_TIMEOUT ? ERROR_BUSY : ERROR_INVALID_STATE);
        }
        struct ReleaseGate
        {
            HANDLE handle;
            ~ReleaseGate() { if (handle) ReleaseMutex(handle); }
        } release{ gate.get() };
        Layout p;
        ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE));
        if (!manager) Fail("local-test SCM");
        Value::Object report{ { "app", Utf8(App) }, { "ownerSid", Utf8(owner) }, { "action", Utf8(action) } };
        if (action == L"deploy" || action == L"deploy-auto")
        {
            auto candidate = ValidateBundle(source, { ReleaseTrustPolicy, OwnVersion() }, OwnVersion());
            for (const auto& path : { p.code, p.data, p.policy })
                Require(!Exists(path), "previous local deployment remains; run cleanup before deploying");
            for (const auto& path : { p.root, p.root + L"\\Owners", p.ownerRoot, p.state,
                p.state + L"\\Data", p.state + L"\\Policy", p.state + L"\\Installer" })
                Directory(path, SharedAcl);
            ServiceHandle service(CreateServiceW(manager.get(), p.service.c_str(), p.service.c_str(),
                SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS,
                action == L"deploy-auto" ? SERVICE_AUTO_START : SERVICE_DEMAND_START, SERVICE_ERROR_NORMAL,
                p.image.c_str(), nullptr, nullptr, nullptr, (L"NT SERVICE\\" + p.service).c_str(), nullptr));
            if (!service) Fail("create isolated VA service");
            const auto va = AccountSid(L"NT SERVICE\\" + p.service);
            Require(va == ServiceSid(p.service), "local-test service SID derivation mismatch");
            Directory(p.record, SharedAcl);
            const auto marker = Value(Value::Object{
                { "app", Utf8(App) }, { "ownerSid", Utf8(owner) }, { "vaSid", Utf8(va) } }).Stringify();
            if (Exists(p.record + L"\\local-test.json"))
                Require(ReadText(p.record + L"\\local-test.json") == marker, "retained local-test profile identity mismatch");
            else
                Write(p.record + L"\\local-test.json", marker);
            const auto privateAcl = L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + va + L")";
            Directory(p.code, privateAcl, true);
            Directory(p.data, privateAcl, true);
            Directory(p.policy, SharedAcl, true);
            Write(p.record + L"\\maintenance.lock", "");
            Acl(p.record + L"\\maintenance.lock", L"O:SYG:SYD:P(A;;FA;;;SY)(A;;FR;;;" + va + L")");
            Write(p.record + L"\\active", "");
            Write(p.data + L"\\storage.index", BlobStore::InitialIndex(Utf8(NewId()), { "workspaces.repository" }).Stringify());
            EnsureDataReadAccess(p.data, owner, va);
            Write(p.policy + L"\\policy.txt", "format=2\napp=" + Utf8(App) +
                "\nsigner_policy=" + ReleaseTrustPolicy + "\nminimum_version=" + VersionText(OwnVersion()) + "\n");
            Acl(p.policy + L"\\policy.txt", SharedAcl);
            for (const auto& entry : std::vector<std::pair<const wchar_t*, HANDLE>>{
                { L"Bootstrap.exe", candidate.bootstrap.get() }, { L"Runtime.exe", candidate.runtime.get() },
                { L"manifest.txt", candidate.manifest.get() }, { L"manifest.p7s", candidate.signature.get() },
                { L"ClientCatalog.json", candidate.catalog.get() }, { L"ClientCatalog.p7s", candidate.catalogSignature.get() } })
            {
                const auto bytes = ReadAll(entry.second);
                Write(p.code + L"\\" + entry.first,
                    std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
            }
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            const auto sddl = L"D:P(A;;GA;;;SY)(A;;0x00020035;;;" + va + L")(A;;0x00020005;;;" + owner + L")";
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &descriptor, nullptr))
                Fail("local-test service DACL");
            std::unique_ptr<void, decltype(&LocalFree)> security(descriptor, LocalFree);
            if (!SetServiceObjectSecurity(service.get(), DACL_SECURITY_INFORMATION, descriptor))
                Fail("set local-test service DACL");
            if (!StartServiceW(service.get(), 0, nullptr)) Fail("start isolated VA service");
            WaitState(service.get(), SERVICE_RUNNING);
            report["vaSid"] = Utf8(va);
            report["running"] = true;
        }
        else if (action == L"repair")
        {
            Paths paths(owner);
            paths.Check();
            auto markerFile = OpenRead(p.record + L"\\local-test.json");
            const auto markerBytes = ReadAll(markerFile.get());
            auto marker = Value::Parse(std::string_view(reinterpret_cast<const char*>(markerBytes.data()), markerBytes.size()));
            Require(marker.At("app").Text() == Utf8(App) && marker.At("ownerSid").Text() == Utf8(owner) &&
                marker.At("vaSid").Text() == Utf8(paths.va), "local repair identity mismatch");
            auto candidate = ValidateBundle(source, LoadPolicy(paths), OwnVersion());
            ServiceHandle service(OpenServiceW(manager.get(), p.service.c_str(),
                SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_STOP | SERVICE_START));
            if (!service) Fail("open local-test repair service");
            CheckService(service.get(), p);
            auto before = State(service.get());
            Handle host;
            if (before.dwProcessId)
            {
                host = Handle(OpenProcess(SYNCHRONIZE, FALSE, before.dwProcessId));
                if (!host) Fail("hold exact repair host");
                Require(State(service.get()).dwProcessId == before.dwProcessId, "repair host changed");
            }
            if (before.dwCurrentState != SERVICE_STOPPED)
            {
                SERVICE_STATUS ignored{};
                if (!ControlService(service.get(), SERVICE_CONTROL_STOP, &ignored)) Fail("stop repair host");
                WaitState(service.get(), SERVICE_STOPPED);
            }
            if (host && WaitForSingleObject(host.get(), 30000) != WAIT_OBJECT_0)
                throw Error("repair host did not exit", ERROR_TIMEOUT);
            const auto beforeHashes = DataHashes(p);
            const auto next = p.ownerRoot + L"\\Repair-" + NewId();
            const auto previous = p.ownerRoot + L"\\Previous-" + NewId();
            Directory(next, L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + paths.va + L")", true);
            for (const auto& entry : std::vector<std::pair<const wchar_t*, HANDLE>>{
                { L"Bootstrap.exe", candidate.bootstrap.get() }, { L"Runtime.exe", candidate.runtime.get() },
                { L"manifest.txt", candidate.manifest.get() }, { L"manifest.p7s", candidate.signature.get() },
                { L"ClientCatalog.json", candidate.catalog.get() }, { L"ClientCatalog.p7s", candidate.catalogSignature.get() } })
            {
                const auto bytes = ReadAll(entry.second);
                Write(next + L"\\" + entry.first, std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
            }
            ValidateTree(p.code);
            if (!MoveFileExW(p.code.c_str(), previous.c_str(), MOVEFILE_WRITE_THROUGH)) Fail("retain previous local code");
            if (!MoveFileExW(next.c_str(), p.code.c_str(), MOVEFILE_WRITE_THROUGH))
            {
                const auto error = GetLastError();
                if (!MoveFileExW(previous.c_str(), p.code.c_str(), MOVEFILE_WRITE_THROUGH))
                    Fail("local repair rollback failed; previous code retained");
                throw Error("publish repaired local code", error);
            }
            if (!StartServiceW(service.get(), 0, nullptr)) Fail("start repaired local service; previous code retained");
            WaitState(service.get(), SERVICE_RUNNING);
            const auto afterHashes = DataHashes(p);
            Require(beforeHashes.Stringify() == afterHashes.Stringify(), "local repair changed business data");
            // Keep the exact prior code unit for inspection; do not discard evidence on failure.
            report["previousCode"] = Utf8(previous);
            report["dataBefore"] = beforeHashes;
            report["dataAfter"] = afterHashes;
            report["dataPreserved"] = true;
            report["running"] = true;
        }
        else if (action == L"inspect")
        {
            ServiceHandle service(OpenServiceW(manager.get(), p.service.c_str(), SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
            if (service)
            {
                CheckService(service.get(), p);
                report["serviceState"] = static_cast<uint64_t>(State(service.get()).dwCurrentState);
            }
            else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
                Fail("query isolated service");
            for (const auto& name : { L"service-errors.log", L"runtime-errors.log", L"ready.txt" })
                if (Exists(p.data + L"\\" + name))
                    report[Utf8(name)] = ReadText(p.data + L"\\" + name);
            Value::Object ownership;
            for (const auto& path : { p.root, p.ownerRoot, p.code, p.state, p.data, p.policy, p.record,
                p.code + L"\\Bootstrap.exe", p.data + L"\\storage.index", p.policy + L"\\policy.txt" })
            {
                if (!Exists(path)) continue;
                Handle file(CreateFileW(path.c_str(), READ_CONTROL, FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
                if (!file) Fail("inspect local-test path owner");
                PSECURITY_DESCRIPTOR raw = nullptr;
                PSID sid = nullptr;
                const auto result = GetSecurityInfo(file.get(), SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION,
                    &sid, nullptr, nullptr, nullptr, &raw);
                std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
                if (result != ERROR_SUCCESS) throw Error("inspect local-test owner", result);
                ownership[Utf8(path)] = Utf8(SidText(sid));
            }
            report["pathOwners"] = std::move(ownership);
            if (Exists(p.data + L"\\storage.index"))
                report["dataHashes"] = DataHashes(p);
        }
        else if (action == L"clean" || action == L"clean-backup")
        {
            std::wstring va;
            if (Exists(p.record + L"\\local-test.json"))
            {
                auto receipt = OpenRead(p.record + L"\\local-test.json", MaxMetadata);
                const auto parent = HoldDirectories(p.record);
                CheckAcl(parent.back().get(), {}, false);
                const auto bytes = ReadAll(receipt.get());
                const auto marker = Value::Parse(std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
                Require(marker.At("app").Text() == Utf8(App) && marker.At("ownerSid").Text() == Utf8(owner),
                    "local-test cleanup owner mismatch");
                va = Wide(marker.At("vaSid").Text());
                Require(va == ServiceSid(p.service), "refusing cleanup of a profile outside the exact test service identity");
            }
            ServiceHandle service(OpenServiceW(manager.get(), p.service.c_str(),
                SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
            if (service)
            {
                CheckService(service.get(), p);
                const auto actualVa = AccountSid(L"NT SERVICE\\" + p.service);
                Require(va.empty() || va == actualVa, "local-test VA receipt mismatch");
                va = actualVa;
                const auto before = State(service.get());
                Handle process;
                if (before.dwProcessId)
                {
                    process = Handle(OpenProcess(SYNCHRONIZE, FALSE, before.dwProcessId));
                    if (!process) Fail("hold exact local-test host");
                }
                if (before.dwCurrentState != SERVICE_STOPPED)
                {
                    SERVICE_STATUS ignored{};
                    if (!ControlService(service.get(), SERVICE_CONTROL_STOP, &ignored)) Fail("stop local-test service");
                    WaitState(service.get(), SERVICE_STOPPED);
                }
                if (process && WaitForSingleObject(process.get(), 30000) != WAIT_OBJECT_0)
                    throw Error("local-test host has not exited", ERROR_TIMEOUT);
            }
            else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
                Fail("query local-test cleanup target");
            for (const auto& path : { p.ownerRoot, p.data, p.policy, p.record })
            {
                if (Exists(path))
                {
                    const auto held = HoldDirectories(path);
                    CheckAcl(held.back().get(), va, path == p.data);
                }
                ValidateTree(path);
            }
            if (action == L"clean-backup" && Exists(p.data))
            {
                Require(va == ServiceSid(p.service), "backup requires the verified test VA identity");
                Directory(p.state + L"\\Backups", SharedAcl);
                const auto backups = p.state + L"\\Backups\\" + owner;
                Directory(backups, SharedAcl);
                const auto backup = backups + L"\\" + NewId();
                const auto backupAcl = L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;GRGX;;;" + owner + L")";
                Directory(backup, backupAcl, true);
                for (const auto& section : std::vector<std::pair<std::wstring, std::wstring>>{
                    { p.data, L"Data" }, { p.record, L"Installer" }, { p.policy, L"Policy" } })
                {
                    if (!Exists(section.first)) continue;
                    const auto destination = backup + L"\\" + section.second;
                    Directory(destination, backupAcl, true);
                    for (const auto& entry : fs::recursive_directory_iterator(section.first))
                    {
                        const auto target = fs::path(destination) / fs::relative(entry.path(), section.first);
                        if (entry.is_directory())
                        {
                            Directory(target.wstring(), backupAcl, true);
                            continue;
                        }
                        auto sourceFile = OpenRead(entry.path().wstring());
                        const auto bytes = ReadAll(sourceFile.get());
                        Write(target.wstring(), std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
                        auto copied = OpenRead(target.wstring());
                        Require(Hash(sourceFile.get()) == Hash(copied.get()), "local backup content mismatch");
                    }
                }
                report["backupPath"] = Utf8(backup);
                report["backupHashes"] = DataHashes(p);
                try
                {
                    BlobStore store(p.data, [&](HANDLE file) { CheckAcl(file, va, true, false); });
                    Frame query{ DataCommand::GetState, GuidBytes(Utf8(NewId())),
                        Value::Object{ { "target", "workspaces.repository" } }, {} };
                    const auto current = store.Dispatch(query);
                    if (current.metadata.At("state").Text() == "Initialized")
                    {
                        query.command = DataCommand::GetBlob;
                        const auto blob = store.Dispatch(query);
                        Write(backup + L"\\workspaces.json",
                            std::string_view(reinterpret_cast<const char*>(blob.bytes.data()), blob.bytes.size()));
                        report["exportedWorkspaces"] = true;
                    }
                }
                catch (const std::exception& failure)
                {
                    report["exportError"] = failure.what();
                }
                report["backupVerified"] = true;
                Write(backup + L"\\backup.json", Value(report).Stringify());
            }
            if (service && !DeleteService(service.get())) Fail("delete local-test service");
            for (const auto& path : { p.ownerRoot, p.data, p.policy, p.record })
                if (Exists(path)) fs::remove_all(path);
            report["serviceRemoved"] = true;
            report["testDataRemoved"] = true;
            DWORD profileError = 0;
            if (!va.empty() && !DeleteProfileW(va.c_str(), nullptr, nullptr))
            {
                profileError = GetLastError();
                if (profileError == ERROR_FILE_NOT_FOUND || profileError == ERROR_PATH_NOT_FOUND)
                    profileError = 0;
            }
            report["profileCleanupPending"] = profileError != 0;
            report["profileNativeCode"] = static_cast<uint64_t>(profileError);
            if (profileError)
            {
                Directory(p.record, SharedAcl, true);
                Write(p.record + L"\\local-test.json", Value(Value::Object{
                    { "app", Utf8(App) }, { "ownerSid", Utf8(owner) }, { "vaSid", Utf8(va) } }).Stringify());
            }
        }
        else
            throw Error("unsupported local-test lifecycle action", ERROR_BAD_ARGUMENTS);
        report["ok"] = true;
        return report;
    }
    DWORD WINAPI Control(DWORD, DWORD, void*, void*) { return NO_ERROR; }
    bool Elevated(HANDLE token)
    {
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        if (!GetTokenInformation(token, TokenElevation, &elevation, sizeof(elevation), &size))
            Fail("local setup token elevation");
        return elevation.TokenIsElevated != 0;
    }
    DWORD AuthorizeSetup(DWORD pid, uint64_t birth)
    {
        Handle token;
        HANDLE raw = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw)) Fail("local authorization token");
        token = Handle(raw);
        Require(Elevated(token.get()), "local setup requires administrator authorization");
        Handle requester(OpenProcess(PROCESS_QUERY_INFORMATION | SYNCHRONIZE, FALSE, pid));
        if (!requester) Fail("hold local setup requester");
        Require(ProcessBirth(requester.get()) == birth && WaitForSingleObject(requester.get(), 0) == WAIT_TIMEOUT,
            "local setup requester changed");
        Require(_wcsicmp(ProcessImage(requester.get()).c_str(), ModulePath().c_str()) == 0,
            "local setup requester is not the same executable");
        Handle image(CreateFileW(ModulePath().c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ,
            nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        if (!image) Fail("hold authorized local setup image");
        VerifyProcessImageFile(GetCurrentProcess(), image.get());
        VerifyProcessImageFile(requester.get(), image.get());
        if (!OpenProcessToken(requester.get(), TOKEN_QUERY, &raw)) Fail("local setup requester token");
        Handle callerToken(raw);
        Require(!Elevated(callerToken.get()), "local setup caller must be the normal owner");
        owner = TokenSid(requester.get());
        Require(ValidOwner(owner), "canonical local setup owner");
        const auto sourceBundle = Parent(Parent(ModulePath())) + L"\\Bundle";
        auto candidate = ValidateBundle(sourceBundle, { ReleaseTrustPolicy, OwnVersion() }, OwnVersion());
        const auto tools = Known(FOLDERID_ProgramFiles) + L"\\PowerToysProtectedStorageLocalTestTools";
        const auto id = NewId();
        const auto stage = tools + L"\\" + id;
        auto ancestors = HoldDirectories(tools);
        const auto stagingAcl = L"O:BAG:BAD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(stagingAcl, SDDL_REVISION_1, &descriptor, nullptr))
            Fail("local authorization staging descriptor");
        std::unique_ptr<void, decltype(&LocalFree)> security(descriptor, LocalFree);
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        if (!CreateDirectoryW(stage.c_str(), &attributes)) Fail("create local authorization stage");
        const auto bytes = ReadAll(image.get());
        WriteNew(stage + L"\\Lifecycle.exe", std::string_view(reinterpret_cast<const char*>(bytes.data()), bytes.size()));
        // The service executes the exact image approved by UAC, never an adjacent
        // user-writable helper or a script selected by the requester.
        const auto name = std::wstring(App) + L"Provision_" + id;
        const auto command = Quote(stage + L"\\Lifecycle.exe") + L" --service deploy-auto " + owner + L" " + Quote(sourceBundle);
        ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE));
        if (!manager) Fail("local authorization SCM");
        ServiceHandle service(CreateServiceW(manager.get(), name.c_str(), name.c_str(),
            SERVICE_START | SERVICE_QUERY_STATUS | DELETE, SERVICE_WIN32_OWN_PROCESS, SERVICE_DEMAND_START,
            SERVICE_ERROR_NORMAL, command.c_str(), nullptr, nullptr, nullptr, nullptr, nullptr));
        if (!service) Fail("create authorized local provisioning service");
        try
        {
            if (!StartServiceW(service.get(), 0, nullptr)) Fail("start authorized local provisioner");
            const auto deadline = GetTickCount64() + 90000;
            SERVICE_STATUS_PROCESS result{};
            do
            {
                result = State(service.get());
                if (result.dwCurrentState == SERVICE_STOPPED) break;
                Sleep(100);
            } while (GetTickCount64() < deadline);
            Require(result.dwCurrentState == SERVICE_STOPPED, "local provisioning outcome unknown; stage retained");
            if (!Exists(stage + L"\\result.json")) Fail("local provisioning result missing", ERROR_INVALID_STATE);
            if (!DeleteService(service.get())) Fail("delete completed local provisioner");
            return result.dwWin32ExitCode;
        }
        catch (...)
        {
            if (State(service.get()).dwCurrentState == SERVICE_STOPPED && !DeleteService(service.get()))
                OutputDebugStringA("Failed to remove stopped local provisioning service");
            throw;
        }
    }
    int PublicSetup(std::wstring_view verb)
    {
        DWORD code = 0;
        std::string state = "Failed";
        auto id = Utf8(NewId());
        id.erase(std::remove_if(id.begin(), id.end(), [](char c) { return c == '{' || c == '}' || c == '-'; }), id.end());
        const auto apartment = CoInitializeEx(nullptr, COINIT_APARTMENTTHREADED);
        struct Apartment
        {
            bool initialized;
            ~Apartment() { if (initialized) CoUninitialize(); }
        } apartmentLifetime{ SUCCEEDED(apartment) };
        try
        {
            Require(SUCCEEDED(apartment), "local setup COM initialization");
            HANDLE raw = nullptr;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw)) Fail("normal local setup token");
            Handle token(raw);
            Require(!Elevated(token.get()), "local setup must start as the non-elevated business owner");
            owner = TokenSid();
            Require(ValidOwner(owner), "local setup owner");
            Layout p;
            ServiceHandle manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
            if (!manager) Fail("local setup SCM");
            ServiceHandle existing(OpenServiceW(manager.get(), p.service.c_str(), SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
            if (existing)
            {
                CheckService(existing.get(), p);
                auto status = State(existing.get());
                if (status.dwCurrentState != SERVICE_RUNNING)
                    throw Error("existing local service requires data-preserving repair", status.dwWin32ExitCode ? status.dwWin32ExitCode : ERROR_SERVICE_NOT_ACTIVE);
                state = "Ready";
            }
            else
            {
                if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST) Fail("query local service");
                state = "NotProvisioned";
                if (verb == L"ensure")
                {
                    const auto command = L"--authorize-local " + std::to_wstring(GetCurrentProcessId()) + L" " +
                        std::to_wstring(ProcessBirth(GetCurrentProcess()));
                    const auto path = ModulePath();
                    SHELLEXECUTEINFOW launch{ sizeof(launch) };
                    launch.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
                    launch.lpVerb = L"runas";
                    launch.lpFile = path.c_str();
                    launch.lpParameters = command.c_str();
                    launch.nShow = SW_HIDE;
                    if (!ShellExecuteExW(&launch)) Fail("local setup authorization");
                    Handle helper(launch.hProcess);
                    if (WaitForSingleObject(helper.get(), 120000) != WAIT_OBJECT_0)
                        throw Error("local setup outcome unknown; inspect before retrying", WAIT_TIMEOUT);
                    if (!GetExitCodeProcess(helper.get(), &code)) Fail("local setup result");
                    state = code == ERROR_SUCCESS ? "Ready" : "Failed";
                }
            }
        }
        catch (const Error& error)
        {
            code = error.code == ERROR_CANCELLED ? ERROR_INSTALL_USEREXIT : error.code;
            state = "Failed";
            std::cerr << error.what() << '\n';
        }
        catch (const std::exception& error)
        {
            code = ERROR_GEN_FAILURE;
            state = "Failed";
            std::cerr << error.what() << '\n';
        }
        const auto result = Value(Value::Object{
            { "operationId", id }, { "state", state }, { "nativeCode", static_cast<uint64_t>(code) },
            { "retryKind", code == WAIT_TIMEOUT ? "InspectUnknownOutcome" : code ? "RetryAfterInputFix" : "None" },
            { "cleanupPending", false }, { "dataRetained", false },
            { "diagnosticsRetained", verb == L"ensure" },
            { "installedVersion", state == "Ready" ? VersionText(OwnVersion()) : "" } }).Stringify() + "\n";
        DWORD written = 0;
        if (!WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), result.data(), static_cast<DWORD>(result.size()), &written, nullptr))
            return ERROR_WRITE_FAULT;
        return static_cast<int>(code);
    }
    int RunAsDesktopOwner(const std::wstring& executable, const std::wstring& mode, const std::wstring& reportPath)
    {
        Require(IsAbsoluteLocal(executable), "absolute local-test client path");
        const bool editor = mode == L"editor";
        const bool runner = mode == L"powertoys";
        Require(editor || runner || mode == L"seed" || mode == L"exercise" || mode == L"verify", "local-test client mode");
        DWORD shellPid = 0;
        GetWindowThreadProcessId(GetShellWindow(), &shellPid);
        Require(shellPid != 0, "a real interactive desktop is required");
        Handle shell(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, shellPid));
        if (!shell) Fail("open desktop owner");
        Require(TokenSid(shell.get()) == TokenSid(), "desktop must belong to the invoking test owner");
        wchar_t windows[MAX_PATH]{};
        Require(GetWindowsDirectoryW(windows, MAX_PATH) != 0 &&
            _wcsicmp(ProcessImage(shell.get()).c_str(), (std::wstring(windows) + L"\\explorer.exe").c_str()) == 0,
            "desktop process must be the Windows shell");
        HANDLE raw = nullptr;
        if (!OpenProcessToken(shell.get(), TOKEN_QUERY | TOKEN_DUPLICATE, &raw)) Fail("desktop owner token");
        Handle token(raw);
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        Require(GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size) &&
            !elevation.TokenIsElevated, "local-test clients require a non-elevated desktop token");
        HANDLE primaryRaw = nullptr;
        if (!DuplicateTokenEx(token.get(), TOKEN_ALL_ACCESS, nullptr,
            SecurityImpersonation, TokenPrimary, &primaryRaw))
            Fail("duplicate desktop primary token");
        Handle primary(primaryRaw);
        auto command = Quote(executable);
        if (runner) command += L" --open-settings=Workspaces";
        else if (!editor) command += L" " + Quote(mode) + L" " + Quote(reportPath);
        wchar_t desktop[] = L"winsta0\\default";
        STARTUPINFOW startup{ sizeof(startup) };
        startup.lpDesktop = desktop;
        PROCESS_INFORMATION child{};
        if (!CreateProcessWithTokenW(primary.get(), 0, executable.c_str(), command.data(),
            editor || runner ? 0 : CREATE_NO_WINDOW, nullptr, Parent(executable).c_str(), &startup, &child))
            Fail("start normal desktop test client");
        Handle process(child.hProcess), thread(child.hThread);
        if (editor || runner)
        {
            std::cout << (runner ? "{\"runnerPid\":" : "{\"editorPid\":") << child.dwProcessId << ",\"elevated\":false}\n";
            return 0;
        }
        if (WaitForSingleObject(process.get(), 60000) != WAIT_OBJECT_0)
            throw Error("local-test client outcome unknown; process retained", ERROR_TIMEOUT);
        DWORD result = 0;
        if (!GetExitCodeProcess(process.get(), &result)) Fail("local-test client result");
        return static_cast<int>(result);
    }
    void WINAPI ServiceMain(DWORD, LPWSTR*)
    {
        statusHandle = RegisterServiceCtrlHandlerExW(L"", Control, nullptr);
        if (!statusHandle) return;
        SERVICE_STATUS state{ SERVICE_WIN32_OWN_PROCESS, SERVICE_RUNNING, 0, 0, 0, 0, 0 };
        SetServiceStatus(statusHandle, &state);
        DWORD error = 0;
        Value report;
        try { report = Execute(); }
        catch (const std::exception& failure)
        {
            const auto native = dynamic_cast<const Error*>(&failure);
            error = native ? native->code : ERROR_GEN_FAILURE;
            if (!error) error = ERROR_GEN_FAILURE;
            report = Value::Object{ { "ok", false }, { "nativeCode", static_cast<uint64_t>(error) }, { "error", failure.what() } };
        }
        try { Write(Parent(ModulePath()) + L"\\result.json", report.Stringify()); }
        catch (const std::exception& failure) { OutputDebugStringA(failure.what()); error = ERROR_WRITE_FAULT; }
        state.dwCurrentState = SERVICE_STOPPED;
        state.dwWin32ExitCode = error;
        SetServiceStatus(statusHandle, &state);
    }
}

int wmain(int argc, wchar_t** argv)
{
    if (argc == 3 && std::wstring_view(argv[2]) == L"--json" &&
        (std::wstring_view(argv[1]) == L"ensure" || std::wstring_view(argv[1]) == L"inspect" || std::wstring_view(argv[1]) == L"sync"))
        return PublicSetup(argv[1]);
    if (argc == 4 && std::wstring_view(argv[1]) == L"--authorize-local")
    {
        try
        {
            const auto pid = std::stoull(argv[2]);
            const auto birth = std::stoull(argv[3]);
            Require(pid && pid <= MAXDWORD && birth, "local setup requester arguments");
            return static_cast<int>(AuthorizeSetup(static_cast<DWORD>(pid), birth));
        }
        catch (const std::exception& failure) { std::cerr << failure.what() << '\n'; return 1; }
    }
    if (argc == 4 && std::wstring_view(argv[1]) == L"--verify-test-cms")
    {
        try
        {
            auto document = OpenRead(argv[2], MaxMetadata);
            auto signature = OpenRead(argv[3], 1024 * 1024);
            VerifyDetachedReleaseSignature(ReadAll(document.get()), ReadAll(signature.get()));
            std::cout << "Verified pinned local-test CMS\n";
            return 0;
        }
        catch (const std::exception& failure) { std::cerr << failure.what() << "\n"; return 1; }
    }
    if (argc == 5 && std::wstring_view(argv[1]) == L"--owner-process")
    {
        try { return RunAsDesktopOwner(argv[2], argv[3], argv[4]); }
        catch (const std::exception& failure) { std::cerr << failure.what() << "\n"; return 1; }
    }
    if (argc != 5 || std::wstring_view(argv[1]) != L"--service")
        return ERROR_BAD_ARGUMENTS;
    action = argv[2];
    owner = argv[3];
    source = argv[4];
    wchar_t serviceName[] = L"";
    SERVICE_TABLE_ENTRYW table[]{ { serviceName, ServiceMain }, {} };
    if (!StartServiceCtrlDispatcherW(table))
        return static_cast<int>(GetLastError());
    return 0;
}
