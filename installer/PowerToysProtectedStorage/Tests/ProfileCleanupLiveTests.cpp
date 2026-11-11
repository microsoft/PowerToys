// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "..\..\..\src\common\ProtectedStorage\ProtectedStorage.Lifecycle\ProfileCleanup.h"

namespace
{
    using namespace PowerToysProtectedStorage;
    namespace cleanup = PowerToysProtectedStorage::ProfileCleanup;
    const std::wstring testOwner = L"S-1-5-21-100-200-300-400";
    const std::wstring testPrefix = L"PowerToysProfileCleanupTest_";

    void Require(bool condition, const char* message)
    {
        if (!condition)
            throw std::runtime_error(message);
    }

    std::vector<BYTE> WorkerImage()
    {
        handle image(CreateFileW(product::module_path().c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
        check(image.value != INVALID_HANDLE_VALUE, "hold live-test image");
        auto bytes = Maintenance::ReadLocked(image.value);
        Require(bytes.size() >= sizeof(IMAGE_DOS_HEADER), "test DOS header");
        const auto* dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(bytes.data());
        Require(dos->e_magic == IMAGE_DOS_SIGNATURE && dos->e_lfanew > 0 &&
                    static_cast<size_t>(dos->e_lfanew) + sizeof(IMAGE_NT_HEADERS) <= bytes.size(),
                "test NT header location");
        auto* nt = reinterpret_cast<IMAGE_NT_HEADERS*>(bytes.data() + dos->e_lfanew);
        Require(nt->Signature == IMAGE_NT_SIGNATURE, "test PE signature");
        // Match Lifecycle's GUI subsystem: a console-only fixture would miss
        // command-wrapper bugs that retire the worker before it has exited.
        nt->OptionalHeader.Subsystem = IMAGE_SUBSYSTEM_WINDOWS_GUI;
        return bytes;
    }

    template<typename Inspect>
    void VisitTask(const std::wstring& operation, Inspect&& inspect)
    {
        const auto initialized = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        Require(SUCCEEDED(initialized), "initialize live scheduler observation");
        struct Uninitialize
        {
            ~Uninitialize() { CoUninitialize(); }
        } uninitialize;
        Microsoft::WRL::ComPtr<ITaskService> scheduler;
        Require(SUCCEEDED(CoCreateInstance(CLSID_TaskScheduler, nullptr, CLSCTX_INPROC_SERVER, IID_PPV_ARGS(&scheduler))), "observe scheduler");
        VARIANT empty{};
        Require(SUCCEEDED(scheduler->Connect(empty, empty, empty, empty)), "connect scheduler observation");
        BSTR root = SysAllocString(L"\\"), name = SysAllocString(cleanup::TaskName(operation).c_str());
        Microsoft::WRL::ComPtr<ITaskFolder> folder;
        const auto opened = scheduler->GetFolder(root, &folder);
        SysFreeString(root);
        Require(SUCCEEDED(opened), "open task observation folder");
        Microsoft::WRL::ComPtr<IRegisteredTask> task;
        const auto found = folder->GetTask(name, &task);
        SysFreeString(name);
        Require(SUCCEEDED(found), "busy cleanup task must remain registered");
        inspect(task.Get());
    }

    cleanup::Context WorkerContext(const std::wstring& operation)
    {
        const auto record = std::filesystem::path(product::module_path()).parent_path().parent_path().parent_path();
        const auto root = record.parent_path().parent_path();
        Require(record.filename() == testOwner && record.parent_path().filename() == L"Installer" &&
                    root.parent_path() == cleanup::WindowsDirectory() / L"Temp" &&
                    root.filename() == L"PowerToysProfileCleanupTests-" + operation,
                "live profile worker is outside its isolated fixture");
        product::assert_system_directory(record);
        product::assert_system_directory(record / L"ProfileCleanup");
        product::assert_system_directory(record / L"ProfileCleanup" / operation);
        const auto fields = PowerToys::ProtectedStorage::ParseKeys(
            ToUtf8(cleanup::ReadText(record / L"ProfileCleanup" / operation / L"request")),
            { "format", "owner", "operation", "service", "sid", "leaf", "volume", "file" });
        const auto& serviceText = fields.at("service");
        const std::wstring service(serviceText.begin(), serviceText.end());
        Require(service == testPrefix + operation || service.rfind(L"PowerToysProfileCleanupProbe_", 0) == 0,
                "live worker must not target a product service");
        return { record, testOwner, service };
    }

    void RunLiveCase(bool superseded)
    {
        const auto operation = setup::unique_id();
        const auto root = cleanup::WindowsDirectory() / L"Temp" / (L"PowerToysProfileCleanupTests-" + operation);
        const cleanup::Context context{ root / L"Installer" / testOwner, testOwner, testPrefix + operation };
        std::wcout << L"fixture=" << root << L" superseded=" << superseded << L"\n"
                   << std::flush;
        for (const auto& path : { root, root / L"Installer", context.record, context.Stage(operation).parent_path(), context.Stage(operation) })
            setup::new_directory(path, system_acl());
        service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE));
        check(scm.value != nullptr, "live profile test SCM");
        const auto account = L"NT SERVICE\\" + context.service;
        service service(CreateServiceW(scm.value, context.service.c_str(), context.service.c_str(), DELETE, SERVICE_WIN32_OWN_PROCESS, SERVICE_DEMAND_START, SERVICE_ERROR_NORMAL, cleanup::SystemExecutable(L"cmd.exe").c_str(), nullptr, nullptr, nullptr, account.c_str(), nullptr));
        check(service.value != nullptr, "create dormant test VA identity");
        const auto sid = account_sid(account);
        wchar_t path[MAX_PATH]{};
        const auto created = CreateProfile(sid.c_str(), context.service.c_str(), path, static_cast<DWORD>(std::size(path)));
        const auto deleted = DeleteService(service.value);
        check(deleted != FALSE, "delete dormant test service");
        CloseServiceHandle(service.value);
        service.value = nullptr;
        if (FAILED(created))
            throw failure("create isolated unloaded profile", static_cast<DWORD>(created));
        std::wcout << L"profile=" << path << L" sid=" << sid << L"\n"
                   << std::flush;
        // CreateProfile does not perform an SCM logon and uses Users. Relocate
        // only this unloaded fixture to the production service-profile layout.
        Require(std::filesystem::path(path).filename() == context.service, "unexpected isolated profile name");
        const auto destination = cleanup::WindowsDirectory() / L"ServiceProfiles" / context.service;
        check(MoveFileExW(path, destination.c_str(), 0) != FALSE, "move isolated unloaded profile");
        HKEY registration = nullptr;
        const auto key = L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + sid;
        result(RegOpenKeyExW(HKEY_LOCAL_MACHINE, key.c_str(), 0, KEY_SET_VALUE | KEY_WOW64_64KEY, &registration),
               "open isolated profile registration");
        const auto destinationText = destination.wstring();
        const auto moved = RegSetValueExW(registration, L"ProfileImagePath", 0, REG_EXPAND_SZ, reinterpret_cast<const BYTE*>(destinationText.c_str()), static_cast<DWORD>((destinationText.size() + 1) * sizeof(wchar_t)));
        RegCloseKey(registration);
        result(moved, "set isolated service-profile layout");
        const auto profile = cleanup::Inspect(sid, context.service);
        Require(profile.has_value(), "isolated profile was not created");
        const auto sentinel = cleanup::ProfilePath(*profile) / L"cleanup-test-sentinel";
        product::write_new(sentinel, { 'k', 'e', 'e', 'p' }, private_acl(sid));
        handle occupied(CreateFileW(sentinel.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, 0, nullptr));
        check(occupied.value != INVALID_HANDLE_VALUE, "hold isolated profile file");
        const auto busy = cleanup::Remove(*profile, context.service, 0);
        Require(busy != ERROR_SUCCESS, "occupied profile must not be reported deleted");
        if (superseded)
        {
            CloseHandle(occupied.value);
            occupied.value = nullptr;
        }
        const auto external = root / L"DoNotDelete";
        setup::new_directory(external, system_acl());
        product::write_new(external / L"sentinel", { 's', 'a', 'f', 'e' });
        check(CreateSymbolicLinkW((cleanup::ProfilePath(*profile) / L"outside-link").c_str(),
                                  external.c_str(),
                                  SYMBOLIC_LINK_FLAG_DIRECTORY) != FALSE,
              "isolated orphan-profile reparse fixture");

        product::write_new(context.Stage(operation) / L"Cleanup.exe", WorkerImage());
        cleanup::WriteRequest(context, operation, *profile);
        cleanup::Pending(context, operation, busy);
        if (superseded)
            cleanup::WriteText(context.record / L"profile-cleanup-id", setup::unique_id());
        cleanup::Schedule(context.Stage(operation), testOwner, operation);
        if (!superseded)
        {
            bool attempted = false;
            LONG outcome = 0;
            const auto deadline = GetTickCount64() + 90000;
            while (!attempted && GetTickCount64() < deadline)
            {
                VisitTask(operation, [&](IRegisteredTask* task) {
                    TASK_STATE state{};
                    DATE lastRun = 0;
                    Require(SUCCEEDED(task->get_State(&state)) && SUCCEEDED(task->get_LastRunTime(&lastRun)) &&
                                SUCCEEDED(task->get_LastTaskResult(&outcome)),
                            "observe busy cleanup result");
                    attempted = lastRun != 0 && state != TASK_STATE_RUNNING &&
                                outcome != SCHED_S_TASK_HAS_NOT_RUN && cleanup::Exists(context.record / L"profile-maintenance.lock");
                });
                if (!attempted)
                    Sleep(250);
            }
            Require(attempted && outcome == 1, "busy GUI worker must report a failed scheduled attempt");
            Require(cleanup::Exists(context.Stage(operation) / L"Cleanup.exe") &&
                        cleanup::Exists(context.Stage(operation) / L"request"),
                    "busy cleanup must retain its retry worker/request");
            Require(!cleanup::Exists(context.Stage(operation).parent_path() / (operation + L".done")),
                    "busy cleanup must not publish completion");
            CloseHandle(occupied.value);
            occupied.value = nullptr;
            VisitTask(operation, [](IRegisteredTask* task) {
                VARIANT empty{};
                Microsoft::WRL::ComPtr<IRunningTask> running;
                Require(SUCCEEDED(task->Run(empty, &running)), "retry the released profile through the same task");
            });
            std::wcout << L"PASS busy GUI task retained worker; result=" << outcome << L"\n"
                       << std::flush;
        }
        const auto deadline = GetTickCount64() + 120000;
        while (cleanup::Exists(context.Stage(operation).parent_path()) && GetTickCount64() < deadline)
            Sleep(250);
        Require(!cleanup::Exists(context.Stage(operation)), "scheduled worker/stage did not self-clean");
        if (superseded)
        {
            Require(cleanup::Inspect(sid, context.service).has_value(), "superseded cleanup deleted the newer instance's profile");
            result(cleanup::Remove(*profile, context.service), "remove isolated preserved profile after assertion");
            cleanup::Cancel(context);
        }
        else
        {
            Require(!cleanup::Inspect(sid, context.service), "scheduled retry did not delete the released profile");
            Require(!cleanup::Exists(context.record / L"cleanup-pending"), "successful retry left pending metadata");
        }
        Require(cleanup::Exists(external / L"sentinel"), "profile cleanup followed a link outside the profile");
        cleanup::DeleteMetadata(external / L"sentinel");
        check(RemoveDirectoryW(external.c_str()) != FALSE, "remove external safety fixture");
        cleanup::DeleteMetadata(context.record / L"profile-maintenance.lock");
        check(RemoveDirectoryW(context.record.c_str()) != FALSE, "remove isolated owner record");
        check(RemoveDirectoryW((root / L"Installer").c_str()) != FALSE, "remove isolated inventory");
        check(RemoveDirectoryW(root.c_str()) != FALSE, "remove isolated test root");
        std::wcout << L"PASS scheduled profile cleanup; superseded=" << superseded << L"\n"
                   << std::flush;
    }
}

std::optional<int> RunProfileCleanupLiveTests(int argc, wchar_t** argv)
{
    if (argc == 4 && std::wstring(argv[1]) == L"retry-profile")
    {
        require_system();
        Require(argv[2] == testOwner && setup::valid_id(argv[3]), "invalid isolated profile retry arguments");
        return static_cast<int>(cleanup::Run(WorkerContext(argv[3]), argv[3]));
    }
    if (argc == 4 && std::wstring(argv[1]) == L"--profile-cleanup-defer-probe")
    {
        require_system();
        const std::wstring service(argv[2]), sid(argv[3]);
        Require(service.rfind(L"PowerToysProfileCleanupProbe_", 0) == 0 &&
                    service.find_first_not_of(L"ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_") == std::wstring::npos,
                "only an isolated SCM probe may be adopted");
        const auto profile = cleanup::Inspect(sid, service);
        Require(profile.has_value(), "probe profile must exist");
        const auto operation = setup::unique_id();
        const auto root = cleanup::WindowsDirectory() / L"Temp" / (L"PowerToysProfileCleanupTests-" + operation);
        const cleanup::Context context{ root / L"Installer" / testOwner, testOwner, service };
        Require(!cleanup::ServiceExists(context), "probe service must already be removed");
        for (const auto& path : { root, root / L"Installer", context.record, context.Stage(operation).parent_path(), context.Stage(operation) })
            setup::new_directory(path, system_acl());
        product::write_new(context.Stage(operation) / L"Cleanup.exe", WorkerImage());
        cleanup::WriteRequest(context, operation, *profile);
        cleanup::Pending(context, operation, ERROR_SHARING_VIOLATION);
        cleanup::Schedule(context.Stage(operation), testOwner, operation);
        std::wcout << L"deferredProbe=" << service << L"\noperation=" << operation << L"\nfixture=" << root << L"\n";
        return 0;
    }
    if (argc != 2 || std::wstring(argv[1]) != L"--profile-cleanup-live")
        return std::nullopt;
    require_system();
    product::privilege(SE_BACKUP_NAME);
    product::privilege(SE_RESTORE_NAME);
    product::privilege(SE_CREATE_SYMBOLIC_LINK_NAME);
    RunLiveCase(false);
    RunLiveCase(true);
    return 0;
}
