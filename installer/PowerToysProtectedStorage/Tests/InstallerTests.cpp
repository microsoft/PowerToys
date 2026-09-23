// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "MaintenanceContract.h"
#include "..\..\..\src\common\ProtectedStorage\ProtectedStorage.Lifecycle\StorageInitialization.h"
#include "..\..\..\src\common\ProtectedStorage\ProtectedStorage.MsiAction\PayloadCleanup.h"
#include <functional>
#include <cstddef>
#include <iterator>
#include <random>

namespace
{
    using namespace PowerToysProtectedStorage;
    using namespace PowerToysProtectedStorage::Maintenance;
    unsigned checks = 0;
    void Expect(bool condition, const char* message)
    {
        ++checks;
        if (!condition) throw std::runtime_error(message);
    }
    void Reject(const std::function<void()>& operation)
    {
        ++checks;
        try { operation(); }
        catch (const failure&) { return; }
        throw std::runtime_error("unsafe input accepted");
    }

    void ParserTests()
    {
        Expect(Parse({ L"remove" }).removal == RemovalMode::KeepData, "production default must retain data");
        Expect(Parse({ L"remove", L"--purge-data", L"--json" }).removal == RemovalMode::PurgeData, "explicit purge");
        Expect(Parse({ L"repair", L"--authorize-repair" }).authorizeRepair, "explicit repair authorization");
        Expect(Parse({ L"sync", L"--json" }).verb == L"sync", "background sync verb");
        Reject([] { (void)Parse({ L"sync", L"--authorize-repair" }); });
        Reject([] { (void)Parse({ L"sync", L"--purge-data" }); });
        Expect(Parse({ L"retry", L"0123456789abcdef0123456789abcdef" }).operation.size() == 32, "opaque retry ID");
        for (const auto& args : std::vector<std::vector<std::wstring>>{
            {}, {L"--owner", L"S-1-5-18"}, {L"install"}, {L"ensure", L"--purge-data"},
            {L"remove", L"--purge-data", L"--keep-data"}, {L"retry"}, {L"retry", L"..\\other"},
            {L"inspect", L"--json", L"--json"}, {L"upgrade", L"--authorize-repair"}, {L"machine-remove"},
            {L"ensure", L"--msi", L"arbitrary.msi"}, {L"remove", L"--keep-data", L"--keep-data"}})
            Reject([&] { (void)Parse(args); });
        Expect(ReleaseNumber(L"255.255.65535") == 0xffffffff, "maximum MSI version");
        for (const auto* version : { L"256.0.0", L"0.256.0", L"0.0.65536", L"1.2.3.4", L"-1.2.3", L"1.2.3 trailing",
             L"+1.2.3", L"01.2.3", L" 1.2.3" })
            Reject([&] { (void)ReleaseNumber(version); });
    }

    void OwnerTests()
    {
        const OwnerBinding request{ 42, 123456, L"0123456789abcdef0123456789abcdef", L"S-1-5-21-1-2-3-1001", L"remove" };
        ValidateOwnerBinding(request, request);
        for (int field = 0; field < 5; ++field)
        {
            auto changed = request;
            if (field == 0) ++changed.pid;
            if (field == 1) ++changed.birth;
            if (field == 2) changed.nonce[0] = L'f';
            if (field == 3) changed.owner = L"S-1-5-21-1-2-3-1002";
            if (field == 4) changed.verb = L"purge";
            Reject([&] { ValidateOwnerBinding(request, changed); });
        }
        for (const auto* sid : { L"S-1-5-18", L"S-1-5-80-1-2-3-4-5", L"S-1-5-32-544", L"..\\SID", L"S-1-5-21-01-2-3-1001" })
            Reject([&] { (void)canonical_owner(sid); });
        Expect(canonical_owner(L"S-1-12-1-1-2-3-4") == L"S-1-12-1-1-2-3-4", "Entra owner");
        const auto ready = setup::event_name(request.pid, request.birth, request.nonce, L"remove-ready");
        Expect(ready != setup::event_name(request.pid, request.birth + 1, request.nonce, L"remove-ready"), "birth-bound rendezvous");
        Expect(ready != setup::event_name(request.pid, request.birth, request.nonce, L"purge-ready"), "verb-bound rendezvous");
    }

    void StateTests()
    {
        auto keep = PlanRemoval(RemovalMode::KeepData);
        Expect(keep.removeService && keep.removeCode && !keep.removeData && keep.preserveInventory, "keep removal plan");
        auto purge = PlanRemoval(RemovalMode::PurgeData);
        Expect(purge.removeData && purge.preserveInventory && purge.suppressLegacyImport, "purge suppression tombstone");
        const std::wstring id = L"0123456789abcdef0123456789abcdef";
        ValidateRemovalReceipt(id, RemovalMode::KeepData, id, L"keep");
        Reject([&] { ValidateRemovalReceipt(id, RemovalMode::PurgeData, id, L"keep"); });
        Reject([&] { ValidateRemovalReceipt(id, RemovalMode::KeepData, L"ffffffffffffffffffffffffffffffff", L"keep"); });
        Expect(KnownTerminalPhase("committed") && KnownTerminalPhase("rolled_back") && KnownTerminalPhase("unchanged"), "known terminal decisions");
        for (const auto* phase : { "absent", "prepared", "preparing", "recovery_required", "", "unknown" })
            Expect(!KnownTerminalPhase(phase), "unknown decision must not become success");
        Expect(MayRetryMsi(1618) && !MayRetryMsi(1603) && !MayRetryMsi(WAIT_TIMEOUT), "known busy is retryable; unknown commit is not");
        Expect(std::wstring(RetryClass(3010)) == L"RetryAfterRestart", "3010 preserved");
        Expect(std::wstring(RetryClass(1603)) == L"InspectUnknownOutcome", "MSI failed commit reconciliation");
        Expect(TryTerminalCleanup("committed", [] {}), "committed cleanup succeeds");
        Expect(!TryTerminalCleanup("committed", [] { throw std::runtime_error("sharing violation"); }),
            "cleanup failure cannot roll back a known commit");
        bool cleanupRan = false;
        Reject([&] { (void)TryTerminalCleanup("recovery_required", [&] { cleanupRan = true; }); });
        Expect(!cleanupRan, "unknown commit preserves recovery materials");
        Expect(ResultJson(id, L"Removed", 3010, true, true).find(L"\"dataRetained\":true") != std::wstring::npos, "structured retained result");
        Reject([] { (void)JsonEscape(L"bad\nstate"); });
        Expect(PlanSync(0, false, false, 0, 20, false) == SyncPlan::Absent, "sync absent is a no-op");
        Expect(PlanSync(1, true, false, 20, 20, true) == SyncPlan::VerifyCurrent, "same release only inspects");
        Expect(PlanSync(1, true, false, 19, 20, false) == SyncPlan::Upgrade, "older release uses MSI");
        Reject([] { (void)PlanSync(0, true, false, 0, 20, false); });
        Reject([] { (void)PlanSync(1, false, false, 20, 20, true); });
        Reject([] { (void)PlanSync(0, false, true, 0, 20, false); });
        Reject([] { (void)PlanSync(1, true, true, 20, 20, true); });
        Reject([] { (void)PlanSync(2, true, false, 20, 20, true); });
        Reject([] { (void)PlanSync(1, true, false, 21, 20, false); });
        Reject([] { (void)PlanSync(1, true, false, 20, 20, false); });
        RequireMachineKeepData({ L"machine-remove", L"--keep-data" });
        Reject([] { RequireMachineKeepData({ L"machine-remove", L"--purge-data" }); });
        Reject([] { RequireMachineKeepData({ L"machine-remove", L"--keep-data", L"--owner", L"S-1-5-18" }); });
        Reject([] { RequireMachineKeepData({ L"machine-remove" }); });
        MachineOwnerResult residual;
        residual.owner = L"S-1-5-21-1-2-3-1001";
        residual.serviceStopped = true;
        residual.serviceRemoved = true;
        residual.codeRemoved = true;
        Expect(!residual.Complete(), "offline registration is a residual even after runtime removal");
        Expect(residual.Json().find(L"owner-context-msi-reconciliation") != std::wstring::npos, "offline registration is visible");
        Expect(residual.Json().find(L"running-or-unverified-instance") == std::wstring::npos, "stopped service is not reported running");
        residual.msiRegistrationRemoved = true;
        residual.profileRemoved = true;
        Expect(residual.Complete(), "all observed runtime resources removed");
        residual.nativeErrors.push_back(ERROR_ACCESS_DENIED);
        Expect(!residual.Complete(), "native inspection errors prevent full-clean claims");
        using NativeValue = PowerToys::ProtectedStorage::Value;
        NativeValue::Object healthy{
            { "protocolMajor", uint64_t{ 1 } }, { "protocolMinor", uint64_t{ 0 } },
            { "version", "1.2.3.0" }, { "healthy", true }, { "workerReady", true },
            { "dataRecoveryRequired", false }, { "hasUnresolvedTransaction", false },
            { "maintenance", false }, { "phase", "committed" } };
        RequireHealthyControlStatus(NativeValue(healthy), "1.2.3.0");
        Expect(true, "authoritative healthy current instance accepted");
        for (const auto* flag : { "healthy", "workerReady", "dataRecoveryRequired", "hasUnresolvedTransaction", "maintenance" })
        {
            auto unhealthy = healthy;
            unhealthy.at(flag) = !unhealthy.at(flag).Boolean();
            Reject([&] { RequireHealthyControlStatus(NativeValue(unhealthy), "1.2.3.0"); });
        }
        auto unknown = healthy;
        unknown.at("phase") = "prepared";
        Reject([&] { RequireHealthyControlStatus(NativeValue(unknown), "1.2.3.0"); });
        unknown.erase("healthy");
        Reject([&] { RequireHealthyControlStatus(NativeValue(unknown), "1.2.3.0"); });
        Reject([&] { RequireHealthyControlStatus(NativeValue(healthy), "1.2.4.0"); });
    }

    void FileTests()
    {
        const auto root = std::filesystem::current_path() / (L"InstallerTest-" + setup::unique_id());
        std::filesystem::create_directory(root);
        struct cleanup { std::filesystem::path path; ~cleanup() { std::error_code error; std::filesystem::remove_all(path, error); } } remove{ root };
        product::write_new(root / L"record", { 'i', 'n', 'i', 't' });
        Reject([&] { product::write_new(root / L"record", {}); });
        {
            handle file(CreateFileW((root / L"record").c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            Expect(ReadLocked(file.value) == std::vector<BYTE>({ 'i', 'n', 'i', 't' }), "immutable retained record");
            Reject([&] { (void)ReadLocked(file.value, 2); });
        }
        check(CreateHardLinkW((root / L"alias").c_str(), (root / L"record").c_str(), nullptr) != FALSE, "test hardlink");
        handle linked(CreateFileW((root / L"record").c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        Reject([&] { (void)ReadLocked(linked.value); });
        Reject([&] { VerifySignedFile(root / L"record"); });
        handle ownImage(CreateFileW(product::module_path().c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ,
            nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(ownImage.value != INVALID_HANDLE_VALUE, "open actual test process image");
        VerifyMappedImage(GetCurrentProcess(), ownImage.value);
        Expect(true, "actual process image mapping accepted");
        const auto copiedImage = root / L"identical-copy.exe";
        check(CopyFileW(product::module_path().c_str(), copiedImage.c_str(), TRUE) != FALSE, "copy image identity fixture");
        handle copy(CreateFileW(copiedImage.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ,
            nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(copy.value != INVALID_HANDLE_VALUE, "open identical image copy");
        Reject([&] { VerifyMappedImage(GetCurrentProcess(), copy.value); });
    }

    void LoaderPolicyTests()
    {
        initialize_process_security();
        const auto base = reinterpret_cast<const BYTE*>(GetModuleHandleW(nullptr));
        const auto dos = reinterpret_cast<const IMAGE_DOS_HEADER*>(base);
        const auto headers = reinterpret_cast<const IMAGE_NT_HEADERS*>(base + dos->e_lfanew);
        const auto& directory = headers->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_LOAD_CONFIG];
        Expect(directory.VirtualAddress != 0 &&
            directory.Size >= offsetof(IMAGE_LOAD_CONFIG_DIRECTORY, DependentLoadFlags) + sizeof(WORD),
            "helper loader policy exists before entry point");
        const auto configuration = reinterpret_cast<const IMAGE_LOAD_CONFIG_DIRECTORY*>(base + directory.VirtualAddress);
        Expect(configuration->DependentLoadFlags == LOAD_LIBRARY_SEARCH_SYSTEM32, "helper static dependencies are System32-only");
    }

    void PayloadCleanupTests()
    {
        ULONGLONG now = 0;
        unsigned attempts = 0;
        RetryPayloadCleanup([&] {
            if (++attempts < 3) throw failure("retained proof handle", ERROR_SHARING_VIOLATION);
        }, [&] { return now; }, [&](DWORD delay) { now += delay; }, 1000);
        Expect(attempts == 3 && now == 100, "sharing violations get bounded retry");
        now = 0;
        attempts = 0;
        const bool cleaned = TryTerminalCleanup("committed", [&] {
            RetryPayloadCleanup([&] {
                ++attempts;
                throw failure("busy ACL/filter", ERROR_ACCESS_DENIED);
            }, [&] { return now; }, [&](DWORD delay) { now += delay; }, 100);
        });
        Expect(!cleaned && now == 100 && attempts == 3, "exhausted cleanup is pending, not rollback");
        now = 0;
        Reject([&] {
            RetryPayloadCleanup([] { throw failure("unsafe reparse point", ERROR_INVALID_DATA); },
                [&] { return now; }, [&](DWORD delay) { now += delay; }, 1000);
        });
        Expect(now == 0, "unsafe cleanup paths are not retried");

        const auto parent = std::filesystem::current_path() / (L"PayloadCleanupTest-" + setup::unique_id());
        const auto stage = parent / L"Stage";
        std::filesystem::create_directories(stage);
        struct cleanup
        {
            std::filesystem::path path;
            ~cleanup() { std::error_code error; std::filesystem::remove_all(path, error); }
        } remove{ parent };
        for (const auto* leaf : { L"Bootstrap.exe", L"Runtime.exe", L"manifest.txt", L"manifest.p7s",
                                 L"ClientCatalog.json", L"ClientCatalog.p7s" })
            product::write_new(stage / leaf, { 'f', 'i', 'x', 't', 'u', 'r', 'e' });
        handle proof(CreateFileW(stage.c_str(), FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, nullptr));
        check(proof.value != INVALID_HANDLE_VALUE, "hold cleanup proof fixture");
        handle payloadProof(CreateFileW((stage / L"Bootstrap.exe").c_str(), GENERIC_READ, FILE_SHARE_READ,
            nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(payloadProof.value != INVALID_HANDLE_VALUE, "hold signed payload proof fixture");
        Reject([&] { CleanupPayloadStage(stage, 0); });
        Expect(std::filesystem::exists(stage / L"Bootstrap.exe"), "payload proof pin prevents cleanup before release");
        CloseHandle(payloadProof.value);
        payloadProof.value = nullptr;
        CloseHandle(proof.value);
        proof.value = nullptr;
        CleanupPayloadStage(stage);
        Expect(!std::filesystem::exists(stage), "cleanup succeeds after proof handle release");
        CleanupPayloadStage(stage);
        Expect(!std::filesystem::exists(stage), "completed cleanup is idempotent");
    }

    void ParserFuzzTests()
    {
        std::mt19937 random(0x50545053);
        for (unsigned sample = 0; sample < 2000; ++sample)
        {
            std::wstring value;
            for (unsigned count = random() % 96; count; --count)
                value.push_back(static_cast<wchar_t>(random() % 128));
            try { (void)Parse({ L"retry", value }); }
            catch (const failure&) { ++checks; }
            try { (void)canonical_owner(value); }
            catch (const failure&) { ++checks; }
        }
    }

    void StorageIndexTests()
    {
        namespace provisioning = PowerToysProtectedStorage::Provisioning;
        namespace native = PowerToys::ProtectedStorage;
        using provisioning::IndexPlan;
        Expect(provisioning::PlanIndex(false, false, false, false) == IndexPlan::SeedNewDirectory, "brand-new store seeds once");
        Expect(provisioning::PlanIndex(true, true, true, false) == IndexPlan::PreserveExisting, "retained store preserves index");
        Expect(provisioning::PlanIndex(false, false, true, true) == IndexPlan::SeedNewDirectory, "explicit purge permits a new empty store");
        Reject([] { (void)provisioning::PlanIndex(false, true, true, false); });
        Reject([] { (void)provisioning::PlanIndex(false, true, true, true); });
        Reject([] { (void)provisioning::PlanIndex(false, false, true, false); });
        const auto initial = provisioning::InitialIndex("00112233-4455-6677-8899-AABBCCDDEEFF");
        provisioning::ValidateIndex(initial);
        const auto initialValue = native::Value::Parse(initial);
        Expect(initialValue.At("epoch").Text() == "00112233-4455-6677-8899-aabbccddeeff", "seed epoch is canonical");
        Expect(initialValue.At("targets").At("workspaces.repository").At("sequence").Text() == "0", "fresh sequence");
        Expect(initialValue.At("targets").At("workspaces.repository").At("receipts").Items().empty(), "fresh receipts");
        Reject([] { provisioning::ValidateIndex("{}"); });
        Reject([] { provisioning::ValidateIndex("{bad"); });

        const auto root = std::filesystem::current_path() / (L"StorageIndexTest-" + setup::unique_id());
        std::filesystem::create_directory(root);
        struct cleanup
        {
            std::filesystem::path path;
            ~cleanup() { std::error_code error; std::filesystem::remove_all(path, error); }
        } remove{ root };
        const std::wstring owner = L"S-1-5-21-1-2-3-1001";
        product::write_new_text(root / L"purged", L"format=1\nowner=" + owner + L"\nmode=purge\n");
        Expect(!provisioning::HasPurgeRecreationTicket(root, owner), "historical purge marker does not authorize a new epoch");
        Reject([&] { (void)provisioning::PlanIndex(false, false, true, provisioning::HasPurgeRecreationTicket(root, owner)); });
        const auto ticket = ToUtf8(L"format=1\nowner=" + owner + L"\nmode=purge\n");
        product::write_new(root / L"purge-recreation.ticket", std::vector<BYTE>(ticket.begin(), ticket.end()),
            L"O:" + actor_sid() + L"D:P(A;;FA;;;SY)(A;;FA;;;WD)");
        Reject([&] { (void)provisioning::HasPurgeRecreationTicket(root, owner); });
        std::filesystem::remove(root / L"purge-recreation.ticket");
        const auto read = [&] {
            std::ifstream file(root / L"storage.index", std::ios::binary);
            return std::string(std::istreambuf_iterator<char>(file), {});
        };
        const auto write = [&](const std::string& text) {
            std::ofstream file(root / L"storage.index", std::ios::binary | std::ios::trunc);
            file << text;
            if (!file) throw std::runtime_error("write index fixture");
        };
        provisioning::EnsureIndex(root, IndexPlan::SeedNewDirectory);
        const auto seeded = read();
        provisioning::EnsureIndex(root, IndexPlan::PreserveExisting);
        Expect(read() == seeded, "ordinary reattachment preserves initial index byte-for-byte");
        Reject([&] { provisioning::EnsureIndex(root, IndexPlan::SeedNewDirectory); });
        Expect(read() == seeded, "seed cannot replace an existing index");

        auto initialized = native::Value::Parse(seeded);
        auto& targets = std::get<native::Value::Object>(std::get<native::Value::Object>(initialized.data).at("targets").data);
        auto& repository = std::get<native::Value::Object>(targets.at("workspaces.repository").data);
        const std::string operation = "11112233-4455-6677-8899-aabbccddeeff";
        repository["sequence"] = "1";
        repository["generation"] = "22222233-4455-6677-8899-aabbccddeeff";
        repository["initializationOperationId"] = operation;
        repository["receipts"] = native::Value::Array{ native::Value::Object{
            { "operationId", operation }, { "digest", std::string(64, 'a') }, { "sequence", "1" } } };
        repository["migrationSource"] = native::Value::Object{ { "opaqueSourceIdentity", "retained" } };
        const auto initializedText = initialized.Stringify();
        write(initializedText);
        provisioning::EnsureIndex(root, IndexPlan::PreserveExisting);
        Expect(read() == initializedText, "epoch, initialization, revision, and migration receipt survive reattachment");
        write("{corrupt");
        Reject([&] { provisioning::EnsureIndex(root, IndexPlan::PreserveExisting); });
        Expect(read() == "{corrupt", "corrupt index is not silently reset");
        std::filesystem::remove(root / L"storage.index");
        Reject([&] { provisioning::EnsureIndex(root, IndexPlan::PreserveExisting); });
        Expect(!std::filesystem::exists(root / L"storage.index"), "missing retained index is never recreated");
    }
}

int wmain()
{
    try
    {
        ParserTests();
        OwnerTests();
        StateTests();
        FileTests();
        LoaderPolicyTests();
        PayloadCleanupTests();
        ParserFuzzTests();
        StorageIndexTests();
        std::cout << "PASS " << checks << " installer contract checks; no services, profiles, accounts or MSI registrations changed.\n";
        return 0;
    }
    catch (const std::exception& error)
    {
        std::cerr << "FAIL after " << checks << " checks: " << error.what() << "\n";
        return 1;
    }
}
