#include "Common.h"
#include "ServiceHealth.h"
#include "MaintenanceAuthority.h"
#include "Authentication.h"
#include <algorithm>
#include <iostream>
#include <memory>

using namespace PowerToys::ProtectedStorage;

namespace
{
    SERVICE_STATUS_HANDLE serviceStatusHandle = nullptr;
    SERVICE_STATUS serviceStatus{};
    HANDLE serviceStop = nullptr;
    std::wstring serviceOwner;

    void Report(DWORD state, DWORD error = ERROR_SUCCESS)
    {
        serviceStatus.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        serviceStatus.dwCurrentState = state;
        serviceStatus.dwControlsAccepted = state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        serviceStatus.dwWin32ExitCode = error;
        serviceStatus.dwWaitHint = state == SERVICE_START_PENDING || state == SERVICE_STOP_PENDING ? 30000 : 0;
        serviceStatus.dwCheckPoint = serviceStatus.dwWaitHint ? serviceStatus.dwCheckPoint + 1 : 0;
        if (serviceStatusHandle && !SetServiceStatus(serviceStatusHandle, &serviceStatus))
            Fail("SetServiceStatus");
    }
    DWORD WINAPI Control(DWORD control, DWORD, void*, void*)
    {
        if (control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN)
        {
            if (!SetEvent(serviceStop))
                return GetLastError();
            return ERROR_SUCCESS;
        }
        if (control == SERVICE_CONTROL_INTERROGATE)
            return ERROR_SUCCESS;
        return ERROR_CALL_NOT_IMPLEMENTED;
    }
    struct Scm
    {
        ServiceHandle manager, service;
        std::wstring command;
        explicit Scm(const Paths& paths, bool mutableService) :
            manager(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT)),
            service(manager ? OpenServiceW(manager.get(), paths.service.c_str(), SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | (mutableService ? SERVICE_START | SERVICE_STOP : 0)) : nullptr)
        {
            if (!manager || !service)
                Fail("open own SCM service");
            DWORD size = 0;
            QueryServiceConfigW(service.get(), nullptr, 0, &size);
            Require(size && size <= 32768, "service configuration size");
            std::vector<BYTE> bytes(size);
            auto config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(bytes.data());
            if (!QueryServiceConfigW(service.get(), config, size, &size))
                Fail("query own service configuration");
            command = config->lpBinaryPathName;
            const auto expected = Quote(paths.code + L"\\Bootstrap.exe") + L" --service " + Quote(paths.owner);
            Require(_wcsicmp(command.c_str(), expected.c_str()) == 0, "SCM ImagePath differs from fixed contract");
            Require(_wcsicmp(config->lpServiceStartName, (L"NT SERVICE\\" + paths.service).c_str()) == 0,
                    "SCM service account differs from fixed VA");
            Require(config->dwServiceType == SERVICE_WIN32_OWN_PROCESS, "own-process service required");
        }
        SERVICE_STATUS_PROCESS Query() const
        {
            SERVICE_STATUS_PROCESS result{};
            DWORD size = 0;
            if (!QueryServiceStatusEx(service.get(), SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&result), sizeof(result), &size))
                Fail("SCM query status");
            return result;
        }
        void Stop() const
        {
            auto status = Query();
            if (status.dwCurrentState == SERVICE_STOPPED)
                return;
            Handle exactHost;
            if (status.dwProcessId)
            {
                exactHost.reset(OpenProcess(SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION, FALSE, status.dwProcessId));
                if (!exactHost)
                {
                    DWORD error = GetLastError();
                    // Never classify an inaccessible active actor as dead.
                    if (error != ERROR_INVALID_PARAMETER || Query().dwCurrentState != SERVICE_STOPPED)
                        Fail("open exact SCM host before stop", error);
                }
            }
            if (status.dwCurrentState != SERVICE_STOP_PENDING)
            {
                SERVICE_STATUS ignored{};
                if (!ControlService(service.get(), SERVICE_CONTROL_STOP, &ignored))
                {
                    DWORD error = GetLastError();
                    if (error != ERROR_SERVICE_NOT_ACTIVE)
                        Fail("SCM stop own service", error);
                }
            }
            ULONGLONG deadline = GetTickCount64() + StopTimeout;
            while (Query().dwCurrentState != SERVICE_STOPPED)
            {
                if (GetTickCount64() >= deadline)
                    Fail("SCM stop timeout", ERROR_TIMEOUT);
                Sleep(100);
            }
            if (exactHost && WaitForSingleObject(exactHost.get(), StopTimeout) != WAIT_OBJECT_0)
                Fail("exact stopped SCM host exit timeout", ERROR_TIMEOUT);
        }
        void Start() const
        {
            if (!StartServiceW(service.get(), 0, nullptr))
            {
                DWORD error = GetLastError();
                if (error != ERROR_SERVICE_ALREADY_RUNNING)
                    Fail("SCM start own service", error);
            }
        }
    };
    uint64_t ParseDecimal(std::string_view text)
    {
        return PowerToys::ProtectedStorage::Decimal(text);
    }
    void ExactProcess(HANDLE process, const Paths& paths, const std::wstring& executable)
    {
        Require(WaitForSingleObject(process, 0) == WAIT_TIMEOUT, "expected process is not running");
        Require(TokenSid(process) == paths.va, "process VA SID mismatch");
        Require(_wcsicmp(ProcessImage(process).c_str(), executable.c_str()) == 0, "process image path mismatch");
    }
    void WaitExit(HANDLE process, DWORD timeout)
    {
        DWORD result = WaitForSingleObject(process, timeout);
        if (result != WAIT_OBJECT_0)
            Fail("exact process did not exit", result == WAIT_FAILED ? GetLastError() : ERROR_TIMEOUT);
    }
    struct Worker
    {
        Handle job, stop, ready, recovery;
        Child child;
        explicit Worker(const Paths& paths)
        {
            job.reset(CreateJobObjectW(nullptr, nullptr));
            if (!job)
                Fail("worker job");
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
            limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            if (!SetInformationJobObject(job.get(), JobObjectExtendedLimitInformation, &limits, sizeof(limits)))
                Fail("worker job limit");
            stop.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            ready.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            recovery.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!stop || !ready || !recovery)
                Fail("worker events");
            auto stopCopy = InheritableDuplicate(stop.get());
            auto readyCopy = InheritableDuplicate(ready.get());
            auto recoveryCopy = InheritableDuplicate(recovery.get());
            child = Spawn(paths.code + L"\\Runtime.exe", L"--worker " + Quote(paths.owner) + L" " + HandleText(readyCopy.get()) + L" " + HandleText(stopCopy.get()) + L" " + HandleText(recoveryCopy.get()), { readyCopy.get(), stopCopy.get(), recoveryCopy.get() }, CREATE_SUSPENDED);
            if (!AssignProcessToJobObject(job.get(), child.process.get()))
            {
                DWORD error = GetLastError();
                TerminateProcess(child.process.get(), error);
                WaitForSingleObject(child.process.get(), 5000);
                Fail("assign owned worker to job", error);
            }
            if (ResumeThread(child.thread.get()) == static_cast<DWORD>(-1))
                Fail("resume worker");
            HANDLE waits[]{ ready.get(), child.process.get(), serviceStop };
            DWORD result = WaitForMultipleObjects(3, waits, FALSE, 12000);
            Require(result == WAIT_OBJECT_0, "worker readiness failed");
            ExactProcess(child.process.get(), paths, paths.code + L"\\Runtime.exe");
            Require(FileVersion(paths.code + L"\\Runtime.exe") == OwnVersion(), "worker/Bootstrap version mismatch");
        }
        ~Worker()
        {
            if (stop)
                SetEvent(stop.get());
            if (child.process && WaitForSingleObject(child.process.get(), 4000) != WAIT_OBJECT_0)
            {
                // Only this host's dedicated worker job is terminated.
                TerminateJobObject(job.get(), ERROR_PROCESS_ABORTED);
                WaitForSingleObject(child.process.get(), 4000);
            }
        }
    };
    std::string ReadyText(const Worker& worker)
    {
        const auto recovery = WaitForSingleObject(worker.recovery.get(), 0);
        Require(recovery == WAIT_OBJECT_0 || recovery == WAIT_TIMEOUT, "worker readiness health unavailable");
        return "host_pid=" + std::to_string(GetCurrentProcessId()) +
               "\nhost_birth=" + std::to_string(ProcessBirth(GetCurrentProcess())) +
               "\nworker_pid=" + std::to_string(worker.child.pid) +
               "\nworker_birth=" + std::to_string(ProcessBirth(worker.child.process.get())) +
               "\nversion=" + VersionText(OwnVersion()) +
               "\ndata_recovery_required=" + (recovery == WAIT_OBJECT_0 ? "1\n" : "0\n");
    }
    void VerifyReady(const Paths& paths, const Scm& scm, uint64_t expectedVersion, const std::string& bootstrapHash, const std::string& runtimeHash)
    {
        ULONGLONG deadline = GetTickCount64() + StartTimeout;
        while (GetTickCount64() < deadline)
        {
            auto state = scm.Query();
            if (state.dwCurrentState == SERVICE_STOPPED)
                Fail("candidate service stopped before readiness", ERROR_SERVICE_NOT_ACTIVE);
            if (state.dwCurrentState == SERVICE_RUNNING && state.dwProcessId && Exists(paths.data + L"\\ready.txt"))
            {
                Handle host(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, state.dwProcessId));
                if (!host)
                    Fail("open newly started Bootstrap");
                auto values = ParseKeys(ReadObservation(paths.data + L"\\ready.txt", TextObservation::AtomicallyReplaced),
                                        { "host_pid", "host_birth", "worker_pid", "worker_birth", "version", "data_recovery_required" });
                Require(values.at("data_recovery_required") == "0", "worker storage requires recovery");
                if (ParseDecimal(values.at("host_pid")) == state.dwProcessId &&
                    ParseDecimal(values.at("host_birth")) == ProcessBirth(host.get()))
                {
                    auto workerPid = ParseDecimal(values.at("worker_pid"));
                    Require(workerPid && workerPid <= MAXDWORD, "worker PID range");
                    Handle worker(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, static_cast<DWORD>(workerPid)));
                    if (!worker)
                        Fail("open newly started worker");
                    Require(ParseDecimal(values.at("worker_birth")) == ProcessBirth(worker.get()), "worker generation mismatch");
                    ExactProcess(host.get(), paths, paths.code + L"\\Bootstrap.exe");
                    ExactProcess(worker.get(), paths, paths.code + L"\\Runtime.exe");
                    Require(ParseVersion(values.at("version")) == expectedVersion &&
                                FileVersion(paths.code + L"\\Bootstrap.exe") == expectedVersion &&
                                FileVersion(paths.code + L"\\Runtime.exe") == expectedVersion,
                            "new readiness PE version mismatch");
                    Require(HashPath(paths.code + L"\\Bootstrap.exe") == bootstrapHash &&
                                HashPath(paths.code + L"\\Runtime.exe") == runtimeHash,
                            "new readiness physical hash mismatch");
                    // Hold both exact process objects across a short stability window.
                    HANDLE processes[]{ host.get(), worker.get() };
                    Require(WaitForMultipleObjects(2, processes, FALSE, 1000) == WAIT_TIMEOUT, "new processes exited during readiness stability window");
                    auto still = scm.Query();
                    Require(still.dwCurrentState == SERVICE_RUNNING && still.dwProcessId == state.dwProcessId, "new SCM generation changed");
                    return;
                }
            }
            Sleep(100);
        }
        Fail("candidate readiness timeout", ERROR_TIMEOUT);
    }
    std::string Status(const Paths& paths, const Worker& worker, const std::string& lastResponseError)
    {
        VerifyLiveRelease(paths);
        ExactProcess(worker.child.process.get(), paths, paths.code + L"\\Runtime.exe");
        Scm scm(paths, false);
        const auto scmStatus = scm.Query();
        auto current = CurrentTransaction(paths);
        auto journal = Journal(current);
        const auto phase = current.empty() ? std::string("none") : Phase(journal);
        const auto operation = current.empty() ? std::wstring() : current.substr(current.find_last_of(L'\\') + 1);
        const auto recovery = WaitForSingleObject(worker.recovery.get(), 0);
        Require(recovery == WAIT_OBJECT_0 || recovery == WAIT_TIMEOUT, "worker health event unavailable");
        ServiceHealth health;
        health.workerReady = scmStatus.dwCurrentState == SERVICE_RUNNING && scmStatus.dwProcessId == GetCurrentProcessId() &&
                             WaitForSingleObject(worker.ready.get(), 0) == WAIT_OBJECT_0 &&
                             WaitForSingleObject(worker.stop.get(), 0) == WAIT_TIMEOUT && WaitForSingleObject(serviceStop, 0) == WAIT_TIMEOUT;
        health.dataRecoveryRequired = recovery == WAIT_OBJECT_0;
        health.hasUnresolvedTransaction = !current.empty() && !IsTerminal(journal);
        health.maintenance = paths.Draining() || health.hasUnresolvedTransaction;
        Handle idleLease;
        if (!health.maintenance)
        {
            try
            {
                idleLease = paths.Lock();
            }
            catch (const Error& error)
            {
                if (error.code != ERROR_SHARING_VIOLATION && error.code != ERROR_LOCK_VIOLATION)
                    throw;
                health.maintenance = true;
            }
        }
        const auto boolean = [](bool value) { return value ? "true" : "false"; };
        auto lastError = Exists(paths.data + L"\\last-update-error.txt") ?
                             ReadObservation(paths.data + L"\\last-update-error.txt", TextObservation::AtomicallyReplaced) :
                             "";
        return "{\"ok\":true,\"owner\":" + Json(paths.owner) +
               ",\"version\":" + Json(VersionText(OwnVersion())) +
               ",\"protocolMajor\":1,\"protocolMinor\":0" +
               ",\"workerReady\":" + boolean(health.workerReady) +
               ",\"dataRecoveryRequired\":" + boolean(health.dataRecoveryRequired) +
               ",\"hasUnresolvedTransaction\":" + boolean(health.hasUnresolvedTransaction) +
               ",\"maintenance\":" + boolean(health.maintenance) +
               ",\"healthy\":" + boolean(health.Healthy()) +
               ",\"currentOperation\":" + Json(operation) +
               ",\"phase\":" + Json(phase) +
               ",\"service\":" + Json(paths.service) +
               ",\"servicePath\":" + Json(scm.command) +
               ",\"lastResponseError\":" + Json(lastResponseError) +
               ",\"bootstrap\":{\"version\":" + Json(VersionText(FileVersion(paths.code + L"\\Bootstrap.exe"))) +
               ",\"pid\":" + std::to_string(GetCurrentProcessId()) +
               ",\"creationTime\":" + std::to_string(ProcessBirth(GetCurrentProcess())) +
               ",\"tokenSid\":" + Json(TokenSid()) +
               ",\"sha256\":" + Json(HashPath(paths.code + L"\\Bootstrap.exe")) +
               "},\"worker\":{\"version\":" + Json(VersionText(FileVersion(paths.code + L"\\Runtime.exe"))) +
               ",\"pid\":" + std::to_string(worker.child.pid) +
               ",\"creationTime\":" + std::to_string(ProcessBirth(worker.child.process.get())) +
               ",\"tokenSid\":" + Json(TokenSid(worker.child.process.get())) +
               ",\"sha256\":" + Json(HashPath(paths.code + L"\\Runtime.exe")) +
               "},\"update\":{\"transaction\":" + Json(current.empty() ? L"" : current.substr(current.find_last_of(L'\\') + 1)) +
               ",\"operation\":" + Json(current.empty() ? L"" : current.substr(current.find_last_of(L'\\') + 1)) +
               ",\"phase\":" + Json(current.empty() ? "none" : Phase(journal)) +
               ",\"journal\":" + Json(journal) +
               ",\"lastRejectedRequest\":" + Json(lastError) + "}}";
    }
    struct Pending
    {
        std::string response;
        Handle activate, lock;
        Child updater;
    };
    void CheckUpdater(const Paths& paths, const std::wstring& transaction, const std::wstring& operation)
    {
        auto gate = TransactionGate(transaction);
        auto journal = Journal(transaction);
        if (IsTerminal(journal) || Phase(journal) == "recovery_required")
            return;
        try
        {
            const auto actor = ParseKeys(ReadText(transaction + L"\\updater.txt"),
                                         { "operation", "pid", "creation_time", "va_sid" });
            Require(actor.at("operation") == Utf8(operation) && actor.at("va_sid") == Utf8(paths.va),
                    "updater operation/VA mismatch");
            const auto pid = ParseDecimal(actor.at("pid"));
            Require(pid && pid <= MAXDWORD, "updater PID range");
            Handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE, FALSE, static_cast<DWORD>(pid)));
            if (!process)
                Fail("cannot observe pending updater; not proof of death");
            Require(ProcessBirth(process.get()) == ParseDecimal(actor.at("creation_time")), "updater process generation changed");
            ExactProcess(process.get(), paths, transaction + L"\\Updater.exe");
        }
        catch (const std::exception& failure)
        {
            // Historical PID alone is never authoritative; unknown actors fail closed.
            Record(transaction, "recovery_required", failure.what());
        }
    }
    std::string MsiDispatch(const Paths& paths, Command command, const std::wstring& operation)
    {
        const auto transaction = ResolveMsiOperation(CurrentTransaction(paths), paths.owner, operation, command);
        if (transaction.empty())
            return AbsentMsiResponse(operation, OwnVersion());
        CheckUpdater(paths, transaction, operation);
        if (command == Command::MsiCommit || command == Command::MsiRollback)
            DecideOperation(transaction, paths.owner, operation, command);
        auto response = MsiResponse(transaction, paths.owner, operation);
        if (command == Command::MsiCommit)
        {
            const auto deadline = GetTickCount64() + StartTimeout + IoTimeout;
            while (ParseMsiResponse(response, operation).at("phase") == "prepared")
            {
                Require(GetTickCount64() < deadline, "commit outcome not yet known; query this exact operation");
                Sleep(100);
                CheckUpdater(paths, transaction, operation);
                response = MsiResponse(transaction, paths.owner, operation);
            }
        }
        return response;
    }
    Pending Prepare(const Paths& paths, const Worker& worker, const std::wstring& operation, const std::wstring& bundlePath)
    {
        Require(WaitForSingleObject(worker.recovery.get(), 0) == WAIT_TIMEOUT, "storage recovery refuses ordinary code update");
        Pending pending;
        paths.Check();
        auto previous = CurrentTransaction(paths);
        if (!previous.empty() && previous == paths.data + L"\\transactions\\" + operation)
        {
            const auto metadata = ReadTransaction(previous, paths.owner, operation);
            auto bundle = ValidateBundle(bundlePath, LoadPolicy(paths), OwnVersion());
            Require(bundle.version == metadata.newVersion && bundle.bootstrapHash == metadata.newBootstrapHash &&
                        bundle.runtimeHash == metadata.newRuntimeHash && bundle.catalogHash == metadata.newCatalogHash,
                    "prepare retry must supply the exact original signed release");
            pending.response = MsiDispatch(paths, Command::MsiQuery, operation);
            return pending;
        }
        RequireNewOperation(previous, paths.owner, operation);
        pending.lock = paths.Lock();
        Require(!paths.Draining(), "installer maintenance drain active");
        Require(CurrentTransaction(paths) == previous, "current operation changed during prepare");
        auto bundle = ValidateBundle(bundlePath, LoadPolicy(paths), OwnVersion());
        auto bootstrap = OpenRead(paths.code + L"\\Bootstrap.exe");
        auto runtime = OpenRead(paths.code + L"\\Runtime.exe");
        CheckAcl(bootstrap.get(), paths.va, true, false);
        CheckAcl(runtime.get(), paths.va, true, false);
        Require(FileVersion(paths.code + L"\\Bootstrap.exe") == OwnVersion() &&
                    FileVersion(paths.code + L"\\Runtime.exe") == OwnVersion(),
                "current live PE version mismatch");
        const auto bootstrapHash = Hash(bootstrap.get()), runtimeHash = Hash(runtime.get());
        const auto catalogHash = HashPath(paths.code + L"\\ClientCatalog.json");
        VerifySameVersion(bundle, OwnVersion(), bootstrapHash, runtimeHash);
        Require(bundle.version != OwnVersion() || bundle.catalogHash == catalogHash, "same-version catalog differs");
        MakeDirectory(paths.data + L"\\transactions");
        const auto& id = operation;
        std::wstring transaction = paths.data + L"\\transactions\\" + id;
        Require(!Exists(transaction), "MSI operation GUID was already used");
        MakeDirectory(transaction);
        WriteTransaction(transaction, { paths.owner, operation, OwnVersion(), bundle.version, bootstrapHash, runtimeHash, bundle.bootstrapHash, bundle.runtimeHash, catalogHash, bundle.catalogHash });
        if (bundle.version == OwnVersion())
        {
            for (const auto* generation : { L"new", L"old" })
            {
                const auto directory = transaction + L"\\" + generation;
                MakeDirectory(directory);
                CopyHeld(bundle.catalog.get(), directory + L"\\ClientCatalog.json");
                CopyHeld(bundle.catalogSignature.get(), directory + L"\\ClientCatalog.p7s");
            }
            Scm scm(paths, false);
            VerifyReady(paths, scm, OwnVersion(), bootstrapHash, runtimeHash);
            RecordOperation(transaction, "unchanged", "operation=" + Utf8(operation) + "; signed manifest and both installed hashes/PE versions/readiness verified; no replacement");
            ReplaceText(paths.data + L"\\current.txt", Utf8(id));
            pending.response = MsiResponse(transaction, paths.owner, operation);
            return pending;
        }
        MakeDirectory(transaction + L"\\new");
        MakeDirectory(transaction + L"\\old");
        // No current pointer until all source and rollback copies are durable.
        CopyHeld(bundle.bootstrap.get(), transaction + L"\\new\\Bootstrap.exe");
        CopyHeld(bundle.runtime.get(), transaction + L"\\new\\Runtime.exe");
        CopyHeld(bundle.manifest.get(), transaction + L"\\new\\manifest.txt");
        CopyHeld(bundle.signature.get(), transaction + L"\\new\\manifest.p7s");
        CopyHeld(bundle.catalog.get(), transaction + L"\\new\\ClientCatalog.json");
        CopyHeld(bundle.catalogSignature.get(), transaction + L"\\new\\ClientCatalog.p7s");
        CopyHeld(bootstrap.get(), transaction + L"\\old\\Bootstrap.exe");
        CopyHeld(runtime.get(), transaction + L"\\old\\Runtime.exe");
        for (const auto* name : { L"manifest.txt", L"manifest.p7s", L"ClientCatalog.json", L"ClientCatalog.p7s" })
        {
            auto source = OpenRead(paths.code + L"\\" + name, 1024 * 1024);
            CopyHeld(source.get(), transaction + L"\\old\\" + name);
        }
        CopyHeld(bootstrap.get(), transaction + L"\\Updater.exe");
        RecordOperation(transaction, "staged", "operation=" + Utf8(operation) + "; both payloads and rollback copies durably staged");
        ReplaceText(paths.data + L"\\current.txt", Utf8(id));
        try
        {
            pending.activate.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!pending.activate)
                Fail("updater activation event");
            auto lock = InheritableDuplicate(pending.lock.get());
            auto host = InheritableDuplicate(GetCurrentProcess(), SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION);
            auto child = InheritableDuplicate(worker.child.process.get(), SYNCHRONIZE | PROCESS_QUERY_LIMITED_INFORMATION);
            auto activate = InheritableDuplicate(pending.activate.get());
            auto args = L"--updater " + Quote(paths.owner) + L" " + Quote(id) + L" " + HandleText(lock.get()) + L" " +
                        HandleText(host.get()) + L" " + HandleText(child.get()) + L" " + HandleText(activate.get());
            pending.updater = Spawn(transaction + L"\\Updater.exe", args, { lock.get(), host.get(), child.get(), activate.get() });
            BOOL inJob = TRUE;
            if (!IsProcessInJob(pending.updater.process.get(), worker.job.get(), &inJob))
                Fail("updater job query");
            Require(!inJob, "updater must be outside worker kill-on-close job");
            Require(TokenSid(pending.updater.process.get()) == paths.va, "updater VA SID mismatch");
            WriteNew(transaction + L"\\updater.txt", "operation=" + Utf8(operation) + "\npid=" + std::to_string(pending.updater.pid) + "\ncreation_time=" + std::to_string(ProcessBirth(pending.updater.process.get())) + "\nva_sid=" + Utf8(paths.va) + "\n");
            RecordOperation(transaction, "updater_spawned", "operation=" + Utf8(operation) + " pid=" + std::to_string(pending.updater.pid) + " creationTime=" + std::to_string(ProcessBirth(pending.updater.process.get())) + " tokenSid=" + Utf8(paths.va));
            pending.response = MsiResponse(transaction, paths.owner, operation);
        }
        catch (const std::exception& error)
        {
            if (pending.updater.process)
            {
                TerminateProcess(pending.updater.process.get(), ERROR_PROCESS_ABORTED);
                WaitForSingleObject(pending.updater.process.get(), 5000);
            }
            RecordOperation(transaction, "recovery_required", error.what());
            throw;
        }
        return pending;
    }
    Value ReadTransactionCatalogs(const Paths& paths, const std::wstring& transaction, const std::wstring& operation, std::vector<Handle>& pins)
    {
        const auto metadata = ReadTransaction(transaction, paths.owner, operation);
        const auto policy = LoadPolicy(paths);
        Value::Array clients;
        const auto read = [&](const std::wstring& directory, uint64_t version, const std::string& hash) {
            auto directories = HoldDirectories(directory);
            CheckAcl(directories.back().get(), paths.va, true, false);
            auto document = OpenRead(directory + L"\\ClientCatalog.json", MaxMetadata);
            auto signature = OpenRead(directory + L"\\ClientCatalog.p7s", 1024 * 1024);
            CheckAcl(document.get(), paths.va, true, false);
            CheckAcl(signature.get(), paths.va, true, false);
            Require(Hash(document.get()) == hash, "protected transaction catalog hash mismatch");
            const auto catalog = VerifySignedClientCatalog(
                { ReadAll(document.get(), MaxMetadata), ReadAll(signature.get(), 1024 * 1024) }, policy, version);
            for (const auto& entry : catalog.At("clients").Items())
                clients.push_back(entry);
            for (auto& directoryHandle : directories)
                pins.push_back(std::move(directoryHandle));
            pins.push_back(std::move(document));
            pins.push_back(std::move(signature));
        };
        read(transaction + L"\\old", metadata.oldVersion, metadata.oldCatalogHash);
        read(transaction + L"\\new", metadata.newVersion, metadata.newCatalogHash);
        return Value::Object{ { "clients", std::move(clients) } };
    }
    void Serve(const Paths& paths, const Worker& worker)
    {
        auto sddl = PipeSddl(paths.owner, paths.va);
        PSECURITY_DESCRIPTOR raw = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1, &raw, nullptr))
            Fail("pipe descriptor");
        std::unique_ptr<void, decltype(&LocalFree)> descriptor(raw, LocalFree);
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), raw, FALSE };
        // Keep the first-instance handle throughout service lifetime, preventing name takeover.
        Handle pipe(CreateNamedPipeW(paths.pipe.c_str(), PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE, PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS, 1, MaxText, sizeof(Request), 0, &attributes));
        if (!pipe)
            Fail("create service pipe");
        std::string lastResponseError;
        while (WaitForSingleObject(serviceStop, 0) == WAIT_TIMEOUT)
        {
            Handle connected(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!connected)
                Fail("pipe connect event");
            OVERLAPPED operation{};
            operation.hEvent = connected.get();
            BOOL result = ConnectNamedPipe(pipe.get(), &operation);
            DWORD error = result ? ERROR_SUCCESS : GetLastError();
            if (!result && error != ERROR_IO_PENDING && error != ERROR_PIPE_CONNECTED)
                Fail("connect named pipe", error);
            if (!result && error == ERROR_IO_PENDING)
            {
                HANDLE waits[]{ connected.get(), serviceStop, worker.child.process.get() };
                DWORD wait = WaitForMultipleObjects(3, waits, FALSE, INFINITE);
                if (wait != WAIT_OBJECT_0)
                {
                    CancelIoEx(pipe.get(), &operation);
                    DWORD ignored = 0;
                    GetOverlappedResult(pipe.get(), &operation, &ignored, TRUE);
                    Require(wait == WAIT_OBJECT_0 + 1, "worker exited unexpectedly");
                    break;
                }
                DWORD ignored = 0;
                if (!GetOverlappedResult(pipe.get(), &operation, &ignored, FALSE))
                    Fail("pipe connect completion");
            }
            Pending pending;
            std::optional<Bundle> callerProof;
            std::optional<CallerIdentity> caller;
            std::vector<Handle> transactionProof;
            std::string response;
            Request request{};
            try
            {
                Require(BoundedIo(pipe.get(), false, &request, sizeof(request), serviceStop) == sizeof(request), "request size mismatch");
                ValidateRequest(request);
                std::optional<Value> candidateCatalog;
                std::wstring matchingTransaction;
                bool terminal = false;
                if (request.command != Command::Status && request.command != Command::MsiPrepare)
                {
                    matchingTransaction = ResolveMsiOperation(CurrentTransaction(paths), paths.owner, request.operation, request.command);
                    if (!matchingTransaction.empty())
                    {
                        terminal = IsTerminal(Journal(matchingTransaction));
                        candidateCatalog = ReadTransactionCatalogs(paths, matchingTransaction, request.operation, transactionProof);
                    }
                }
                const auto authenticateIncoming = [&] {
                    Require(request.bundle[0] &&
                                MayUseIncomingMaintenanceProof(request.command, !matchingTransaction.empty(), terminal),
                            "incoming proof cannot replace transaction decision authority");
                    const auto policy = LoadPolicy(paths);
                    callerProof.emplace(ValidateBundle(request.bundle, policy, OwnVersion()));
                    candidateCatalog = VerifyClientCatalog(request.bundle, policy, callerProof->version);
                    caller.emplace(AuthenticateCaller(pipe.get(), paths, true, &*candidateCatalog));
                };
                if (request.command == Command::Status)
                    caller.emplace(AuthenticateOwner(pipe.get(), paths));
                else if (request.command == Command::MsiPrepare)
                    authenticateIncoming();
                else if (candidateCatalog)
                {
                    try
                    {
                        caller.emplace(AuthenticateCaller(pipe.get(), paths, true, &*candidateCatalog));
                    }
                    catch (const StorageError& error)
                    {
                        if (error.errorCode != ErrorCode::Unauthorized || !request.bundle[0] ||
                            !MayUseIncomingMaintenanceProof(request.command, true, terminal))
                            throw;
                        authenticateIncoming();
                    }
                }
                else if (request.bundle[0])
                    authenticateIncoming();
                else
                    caller.emplace(AuthenticateCaller(pipe.get(), paths, true));
                if (request.command == Command::Status)
                    response = Status(paths, worker, lastResponseError);
                else
                {
                    try
                    {
                        if (request.command == Command::MsiPrepare)
                        {
                            pending = Prepare(paths, worker, request.operation, request.bundle);
                            response = pending.response;
                        }
                        else
                            response = MsiDispatch(paths, request.command, request.operation);
                    }
                    catch (const std::exception& failure)
                    {
                        try
                        {
                            ReplaceText(paths.data + L"\\last-update-error.txt", failure.what());
                        }
                        catch (const std::exception& loggingFailure)
                        {
                            OutputDebugStringA(loggingFailure.what());
                        }
                        throw;
                    }
                }
            }
            catch (const std::exception& failure)
            {
                response = ErrorJson(failure);
                const auto end = std::find(std::begin(request.operation), std::end(request.operation), L'\0');
                const auto operationId = std::wstring_view(request.operation, end - std::begin(request.operation));
                if (ValidId(operationId))
                    response = "{\"ok\":false,\"operation\":" + Json(operationId) + ",\"error\":" + Json(failure.what()) + "}";
            }
            auto responseError = CompleteResponse(pending.activate.get(), [&] {
                Require(response.size() < MaxText, "response size bound");
                Require(BoundedIo(pipe.get(), true, response.data(), static_cast<DWORD>(response.size()), serviceStop) == response.size(), "short response");
                DWORD acknowledgement = 0;
                Require(BoundedIo(pipe.get(), false, &acknowledgement, sizeof(acknowledgement), serviceStop) == sizeof(acknowledgement) &&
                    acknowledgement == ProtocolMagic, "invalid response acknowledgement"); }, [&](std::string_view failure) { AppendText(paths.data + L"\\service-errors.log", std::string(failure) + "\n"); });
            if (!responseError.empty())
            {
                lastResponseError = std::move(responseError);
                OutputDebugStringA(lastResponseError.c_str());
            }
            DisconnectNamedPipe(pipe.get());
        }
    }
    void WINAPI ServiceMain(DWORD, LPWSTR*)
    {
        DWORD error = ERROR_SUCCESS;
        try
        {
            Paths paths(serviceOwner);
            CheckOwnVa(paths);
            Handle stop(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!stop)
                Fail("service stop event");
            serviceStop = stop.get();
            serviceStatusHandle = RegisterServiceCtrlHandlerExW(paths.service.c_str(), Control, nullptr);
            if (!serviceStatusHandle)
                Fail("register service handler");
            Report(SERVICE_START_PENDING);
            paths.Check();
            VerifyLiveRelease(paths);
            Require(_wcsicmp(ModulePath().c_str(), (paths.code + L"\\Bootstrap.exe").c_str()) == 0, "Bootstrap fixed image path mismatch");
            Require(FileVersion(ModulePath()) == OwnVersion(), "Bootstrap resource/build version mismatch");
            auto bootstrapImage = OpenRead(paths.code + L"\\Bootstrap.exe");
            auto runtimeImage = OpenRead(paths.code + L"\\Runtime.exe");
            CheckAcl(bootstrapImage.get(), paths.va, true, false);
            CheckAcl(runtimeImage.get(), paths.va, true, false);
            AllowIdentityQuery(paths);
            Scm scm(paths, false);
            const auto pendingTransaction = CurrentTransaction(paths);
            if (!pendingTransaction.empty())
                CheckUpdater(paths, pendingTransaction, pendingTransaction.substr(pendingTransaction.find_last_of(L'\\') + 1));
            {
                Worker worker(paths);
                ReplaceText(paths.data + L"\\ready.txt", ReadyText(worker));
                Report(SERVICE_RUNNING);
                Serve(paths, worker);
                Report(SERVICE_STOP_PENDING);
            }
            serviceStop = nullptr;
        }
        catch (const Error& failure)
        {
            error = failure.code ? failure.code : ERROR_GEN_FAILURE;
            try
            {
                Paths paths(serviceOwner);
                CheckOwnVa(paths);
                AppendText(paths.data + L"\\service-errors.log", std::string(failure.what()) + "\n");
            }
            catch (const std::exception& loggingError)
            {
                OutputDebugStringA(loggingError.what());
            }
        }
        catch (const std::exception& failure)
        {
            error = ERROR_GEN_FAILURE;
            OutputDebugStringA(failure.what());
        }
        try
        {
            Report(SERVICE_STOPPED, error);
        }
        catch (const std::exception& failure)
        {
            OutputDebugStringA(failure.what());
        }
    }
    int Updater(int argc, wchar_t** argv)
    {
        Require(argc == 8, "invalid updater arguments");
        Paths paths(argv[2]);
        CheckOwnVa(paths);
        AllowIdentityQuery(paths);
        paths.Check();
        std::wstring id(argv[3]);
        Require(ValidId(id), "invalid updater generation");
        std::wstring transaction = paths.data + L"\\transactions\\" + id;
        Require(CurrentTransaction(paths) == transaction, "updater generation no longer current");
        Require(_wcsicmp(ModulePath().c_str(), (transaction + L"\\Updater.exe").c_str()) == 0, "updater fixed generation image mismatch");
        Handle lock(ParseHandle(argv[4])), oldHost(ParseHandle(argv[5])), oldWorker(ParseHandle(argv[6])), activate(ParseHandle(argv[7]));
        try
        {
            // Inherited exact process objects survive PID reuse and deliberate host shutdown.
            ExactProcess(oldHost.get(), paths, paths.code + L"\\Bootstrap.exe");
            ExactProcess(oldWorker.get(), paths, paths.code + L"\\Runtime.exe");
            CheckAcl(lock.get(), paths.va, false);
            wchar_t lockName[4096]{};
            DWORD length = GetFinalPathNameByHandleW(lock.get(), lockName, 4096, FILE_NAME_NORMALIZED | VOLUME_NAME_DOS);
            Require(length && length < 4096 && _wcsicmp(lockName, (L"\\\\?\\" + paths.installer + L"\\maintenance.lock").c_str()) == 0, "inherited lock identity mismatch");
            Require(WaitForSingleObject(activate.get(), 30000) == WAIT_OBJECT_0, "updater activation timed out; unresolved journal retained");
            auto metadata = ReadTransaction(transaction, paths.owner, id);
            auto oldVersion = metadata.oldVersion;
            Require(oldVersion == OwnVersion(), "updater old version mismatch");
            auto candidate = ValidateBundle(transaction + L"\\new", LoadPolicy(paths), oldVersion);
            Require(candidate.version > oldVersion && candidate.version == metadata.newVersion &&
                        candidate.bootstrapHash == metadata.newBootstrapHash && candidate.runtimeHash == metadata.newRuntimeHash &&
                        candidate.catalogHash == metadata.newCatalogHash,
                    "staged candidate identity changed");
            auto oldBootstrap = OpenRead(transaction + L"\\old\\Bootstrap.exe");
            auto oldRuntime = OpenRead(transaction + L"\\old\\Runtime.exe");
            Require(Hash(oldBootstrap.get()) == metadata.oldBootstrapHash && Hash(oldRuntime.get()) == metadata.oldRuntimeHash &&
                        FileVersion(transaction + L"\\old\\Bootstrap.exe") == oldVersion &&
                        FileVersion(transaction + L"\\old\\Runtime.exe") == oldVersion,
                    "rollback hash/version mismatch");
            const auto rollbackBundle = ValidateBundle(transaction + L"\\old", LoadPolicy(paths), oldVersion);
            Require(rollbackBundle.catalogHash == metadata.oldCatalogHash, "rollback catalog mismatch");
            {
                auto gate = TransactionGate(transaction);
                Require(Phase(Journal(transaction)) == "updater_spawned", "updater transaction not in initial phase");
            }
            Scm scm(paths, true);
            Require(!paths.Draining(), "installer drain appeared before update stop");
            return RunUpdate(transaction, { [&] {
                                               scm.Stop();
                                               WaitExit(oldHost.get(), StopTimeout);
                                               WaitExit(oldWorker.get(), StopTimeout);
                                           },
                                            [&] { PublishPair(transaction + L"\\new", paths.code); },
                                            [&] { scm.Start(); },
                                            [&] { VerifyReady(paths, scm, candidate.version, candidate.bootstrapHash, candidate.runtimeHash); },
                                            [&] { PublishPair(transaction + L"\\old", paths.code); },
                                            [&] { scm.Start(); },
                                            [&] { VerifyReady(paths, scm, oldVersion, metadata.oldBootstrapHash, metadata.oldRuntimeHash); },
                                            [&] { return WaitDecision(transaction, paths.owner, id); } });
        }
        catch (const std::exception& failure)
        {
            RecordOperation(transaction, "recovery_required", failure.what());
            throw;
        }
    }
}

int wmain(int argc, wchar_t** argv)
{
    try
    {
        if (argc == 2 && std::wstring_view(argv[1]) == L"--version")
        {
            std::cout << "{\"component\":\"Bootstrap\",\"version\":" << Json(VersionText(FileVersion(ModulePath()))) << "}\n";
            return 0;
        }
        if (argc >= 2 && std::wstring_view(argv[1]) == L"--updater")
            return Updater(argc, argv);
        Require(argc == 3 && std::wstring_view(argv[1]) == L"--service", "usage: Bootstrap.exe --service <ownerSID>");
        serviceOwner = argv[2];
        Paths paths(serviceOwner);
        CheckOwnVa(paths);
        SERVICE_TABLE_ENTRYW entries[]{ { paths.service.data(), ServiceMain }, { nullptr, nullptr } };
        if (!StartServiceCtrlDispatcherW(entries))
            Fail("StartServiceCtrlDispatcher");
        return 0;
    }
    catch (const std::exception& failure)
    {
        std::cout << ErrorJson(failure) << "\n";
        return 1;
    }
}
