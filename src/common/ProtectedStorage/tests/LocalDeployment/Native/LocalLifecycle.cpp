#include "../../../ProtectedStorage.Common/Authentication.h"
#include "../../../ProtectedStorage.Common/SignatureTrust.h"
#include "../../../ProtectedStorage.Runtime/BlobStore.h"
#include <filesystem>
#include <iostream>
#include <memory>
#include <userenv.h>

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
        if (action == L"deploy")
        {
            auto candidate = ValidateBundle(source, { ReleaseTrustPolicy, OwnVersion() }, OwnVersion());
            for (const auto& path : { p.code, p.data, p.policy })
                Require(!Exists(path), "previous local deployment remains; run cleanup before deploying");
            for (const auto& path : { p.root, p.root + L"\\Owners", p.ownerRoot, p.state,
                p.state + L"\\Data", p.state + L"\\Policy", p.state + L"\\Installer" })
                Directory(path, SharedAcl);
            ServiceHandle service(CreateServiceW(manager.get(), p.service.c_str(), p.service.c_str(),
                SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS, SERVICE_DEMAND_START, SERVICE_ERROR_NORMAL,
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
        }
        else if (action == L"clean")
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
                if (!DeleteService(service.get())) Fail("delete local-test service");
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
    int RunAsDesktopOwner(const std::wstring& executable, const std::wstring& mode, const std::wstring& reportPath)
    {
        Require(IsAbsoluteLocal(executable), "absolute local-test client path");
        const bool editor = mode == L"editor";
        Require(editor || mode == L"seed" || mode == L"exercise" || mode == L"verify", "local-test client mode");
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
        if (!editor) command += L" " + Quote(mode) + L" " + Quote(reportPath);
        wchar_t desktop[] = L"winsta0\\default";
        STARTUPINFOW startup{ sizeof(startup) };
        startup.lpDesktop = desktop;
        PROCESS_INFORMATION child{};
        if (!CreateProcessWithTokenW(primary.get(), 0, executable.c_str(), command.data(),
            editor ? 0 : CREATE_NO_WINDOW, nullptr, Parent(executable).c_str(), &startup, &child))
            Fail("start normal desktop test client");
        Handle process(child.hProcess), thread(child.hThread);
        if (editor)
        {
            std::cout << "{\"editorPid\":" << child.dwProcessId << ",\"elevated\":false}\n";
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
