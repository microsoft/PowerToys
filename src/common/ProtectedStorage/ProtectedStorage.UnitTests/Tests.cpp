#include "../ProtectedStorage.Runtime/BlobStore.h"
#include "../ProtectedStorage.Common/Authentication.h"
#include "../ProtectedStorage.Common/SignatureTrust.h"
#include "../ProtectedStorage.Client/ProtectedStoreClient.h"
#include "../ProtectedStorage.Client/ClientProtocol.h"
#include "../ProtectedStorage.Bootstrap/ServiceHealth.h"
#include "../ProtectedStorage.Bootstrap/MaintenanceAuthority.h"
#include <filesystem>
#include <atomic>
#include <iostream>
#include <random>
#include <thread>
#include <memory>
#include "CatalogFixture.h"

using namespace PowerToys::ProtectedStorage;

namespace
{
    unsigned passed = 0;
    void Expect(bool condition, const char* message)
    {
        if (!condition)
            throw std::runtime_error(std::string(message) + " (win32=" + std::to_string(GetLastError()) + ")");
        ++passed;
    }
    template<typename F>
    void Reject(F&& action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (const std::exception&)
        {
            rejected = true;
        }
        Expect(rejected, "expected failure");
    }
    struct Fixture
    {
        std::filesystem::path path;
        Fixture()
        {
            path = std::filesystem::current_path() / (L"protected-storage-test-" + NewId());
            std::filesystem::create_directory(path);
            WriteNew((path / L"storage.index").wstring(), BlobStore::InitialIndex(Utf8(NewId()), { "workspaces.repository" }).Stringify());
        }
        ~Fixture()
        {
            std::error_code error;
            std::filesystem::remove_all(path, error);
        }
    };
    Frame Request(DataCommand command, Value::Object fields = {})
    {
        fields["target"] = "workspaces.repository";
        return { command, GuidBytes(Utf8(NewId())), std::move(fields), {} };
    }
    Frame WriteRequestFor(std::string operation = {})
    {
        if (operation.empty())
            operation = Utf8(NewId());
        auto request = Request(DataCommand::PutBlob, { { "operationId", operation }, { "contentSchema", "unit.test/1" }, { "condition", Value::Object{ { "kind", "IfUninitialized" } } } });
        request.bytes = { 0, 1, 2, 0xff };
        return request;
    }
    void ProtocolTests()
    {
        auto id = GuidBytes("00112233-4455-6677-8899-aabbccddeeff");
        Expect(id[0] == 0 && id[1] == 0x11 && id[4] == 0x44 && id[15] == 0xff, "UUID network order");
        Expect(GuidText(id) == "00112233-4455-6677-8899-aabbccddeeff", "UUID round trip");
        Frame golden{ DataCommand::GetState, id, Value::Object{}, { 0, 0xff } };
        const auto encoded = EncodeFrame(golden);
        Expect(encoded.size() == 44 && encoded[0] == 'P' && encoded[4] == 1 && encoded[8] == 2 &&
                   encoded[12] == 36 && encoded[16] == 8 && encoded[36] == 2 && encoded[40] == '{',
               "golden frame");
        auto decoded = DecodeFrame(encoded);
        Expect(decoded.bytes == golden.bytes && decoded.requestId == id, "frame round trip");
        ValidateNetworkFrame(decoded);
        auto internal = golden;
        internal.requestId = {};
        Expect(DecodeFrame(EncodeFrame(internal)).requestId == internal.requestId, "nil IDs remain valid for internal records");
        Reject([&] { ValidateNetworkFrame(internal); });
        for (size_t i = 0; i < encoded.size(); ++i)
            Reject([&] { DecodeFrame(std::vector<BYTE>(encoded.begin(), encoded.begin() + i)); });
        for (size_t offset : { size_t{ 0 }, size_t{ 4 }, size_t{ 6 }, size_t{ 12 }, size_t{ 16 }, size_t{ 36 } })
        {
            auto invalid = encoded;
            invalid[offset] = 0xff;
            Reject([&] { DecodeFrame(invalid); });
        }
        for (const auto* invalid : { "{\"a\":1,\"a\":2}", "{\"a\":01}", "{\"a\":-1}", "{\"a\":1.0}", "{\"a\":18446744073709551616}", "{\"a\":\"\\uD800\"}", "{\"a\":\"\\u0000\"}", "[]x", "{\"a\":true,}", "{\"a\":\"\xC0\xAF\"}" })
            Reject([&] { Value::Parse(invalid); });
        Expect(Value::Parse("{\"text\":\"\\uD83D\\uDE00\",\"n\":\"18446744073709551615\"}").At("text").Text().size() == 4,
               "UTF8 surrogate pair");
        Expect(Decimal("18446744073709551615") == UINT64_MAX, "full precision decimal");
        std::mt19937 random(1234);
        for (unsigned i = 0; i < 20000; ++i)
        {
            auto fuzz = encoded;
            const unsigned mutations = random() % 5 + 1;
            for (unsigned j = 0; j < mutations; ++j)
                fuzz[random() % fuzz.size()] = static_cast<BYTE>(random());
            try
            {
                auto frame = DecodeFrame(fuzz);
                Expect(EncodeFrame(frame).size() <= MaxBody + HeaderLength, "bounded fuzz parse");
            }
            catch (const std::exception&)
            {
            }
        }
    }
    void ClientOutcomeTests()
    {
        auto request = WriteRequestFor();
        const auto operation = request.metadata.At("operationId").Text();
        Frame response{ request.command, request.requestId, Value::Object{ { "errorCode", "None" }, { "operationId", GuidText(GuidBytes(operation)) }, { "outcome", "Committed" }, { "revision", Value::Object{ { "epoch", Utf8(NewId()) }, { "sequence", "1" } } } }, {} };
        ClientProtocol::ValidateResponse(request, response);
        const StorageError timeout(ErrorCode::Timeout, ERROR_TIMEOUT);
        const auto lost = ClientProtocol::TransportFailure(request, true, timeout);
        Expect(lost.errorCode == ErrorCode::OutcomeUnknown && lost.operationId == operation && lost.code == ERROR_TIMEOUT,
               "mutation timeout preserves operation and becomes unknown");
        const StorageError malformed(ErrorCode::InvalidPayload);
        Expect(ClientProtocol::TransportFailure(request, true, malformed).errorCode == ErrorCode::OutcomeUnknown,
               "malformed mutation response cannot be a known rejection");
        Expect(ClientProtocol::TransportFailure(request, false, malformed).errorCode == ErrorCode::InvalidPayload,
               "local pre-write validation remains known");
        const auto read = Request(DataCommand::GetBlob);
        Expect(ClientProtocol::TransportFailure(read, true, timeout).errorCode == ErrorCode::Timeout,
               "read-only timeout does not imply an unknown write");
        const Error disconnected("disconnected", ERROR_BROKEN_PIPE);
        Expect(ClientProtocol::TransportFailure(request, true, disconnected).errorCode == ErrorCode::OutcomeUnknown,
               "disconnected write is queried");
        auto invalid = response;
        std::get<Value::Object>(invalid.metadata.data).erase("revision");
        Reject([&] { ClientProtocol::ValidateResponse(request, invalid); });
        invalid = response;
        invalid.requestId[0] ^= 1;
        Reject([&] { ClientProtocol::ValidateResponse(request, invalid); });
        Frame rejection{ request.command, request.requestId, ErrorMetadata(StorageError(ErrorCode::RevisionConflict), operation), {} };
        ClientProtocol::ValidateResponse(request, rejection);
        try
        {
            ClientProtocol::ThrowResponseError(request, rejection);
            Expect(false, "explicit rejection must throw");
        }
        catch (const StorageError& error)
        {
            Expect(error.errorCode == ErrorCode::RevisionConflict && error.operationId == operation,
                   "validated explicit server rejection retains its known outcome");
        }
        std::get<Value::Object>(rejection.metadata.data)["nativeCode"] = "bad";
        Reject([&] { ClientProtocol::ValidateResponse(request, rejection); });
        Frame uncertain{ request.command, request.requestId, ErrorMetadata(timeout, operation), {} };
        ClientProtocol::ValidateResponse(request, uncertain);
        try
        {
            ClientProtocol::ThrowResponseError(request, uncertain);
            Expect(false, "server query-outcome result must remain uncertain");
        }
        catch (const StorageError& error)
        {
            Expect(error.errorCode == ErrorCode::OutcomeUnknown && error.operationId == operation,
                   "server QueryOutcome retry class preserves original write identity");
        }
        wchar_t system[MAX_PATH]{};
        const auto length = GetSystemDirectoryW(system, MAX_PATH);
        Expect(length && length < MAX_PATH &&
                   FileVersion(std::wstring(system, length) + L"\\kernel32.dll") != 0,
               "version API loads from System32 without version.lib");
    }
    void StoreTests()
    {
        Fixture fixture;
        BlobStore store(fixture.path.wstring(), [](HANDLE) {});
        store.Recover();
        Expect(store.Dispatch(Request(DataCommand::GetState)).metadata.At("state").Text() == "Uninitialized", "logical initial state");
        Reject([&] { store.Dispatch(Request(DataCommand::GetBlob)); });
        auto write = WriteRequestFor();
        std::get<Value::Object>(write.metadata.data)["migrationSource"] = Value::Object{ { "identity", "opaque" }, { "hash", "module-controlled" } };
        auto result = store.Dispatch(write);
        Expect(result.metadata.At("outcome").Text() == "Committed", "initial commit");
        Expect(store.Dispatch(write).metadata.At("revision").At("sequence").Text() == "1", "idempotent retry");
        auto mismatch = write;
        mismatch.bytes.push_back(3);
        Reject([&] { store.Dispatch(mismatch); });
        Expect(store.Dispatch(Request(DataCommand::GetBlob)).bytes == write.bytes, "opaque bytes preserved");
        auto query = Request(DataCommand::QueryWrite, { { "operationId", write.metadata.At("operationId") } });
        Expect(store.Dispatch(query).metadata.At("outcome").Text() == "Committed", "query committed receipt");
        Reject([&] { store.Dispatch(WriteRequestFor()); });
        auto replace = WriteRequestFor();
        auto& metadata = std::get<Value::Object>(replace.metadata.data);
        metadata["condition"] = Value::Object{ { "kind", "IfRevision" }, { "expected", result.metadata.At("revision") } };
        replace.bytes = { 9, 0, 7 };
        auto replaced = store.Dispatch(replace);
        Expect(replaced.metadata.At("revision").At("sequence").Text() == "2", "CAS increment");
        auto stale = replace;
        std::get<Value::Object>(stale.metadata.data)["operationId"] = Utf8(NewId());
        Reject([&] { store.Dispatch(stale); });
        auto acknowledge = Request(DataCommand::AcknowledgeSourceCleanup, { { "operationId", write.metadata.At("operationId") } });
        store.Dispatch(acknowledge);
        auto state = store.Dispatch(Request(DataCommand::GetState));
        Expect(state.metadata.At("cleanupAcknowledged").Boolean() && state.metadata.At("migrationSource").At("identity").Text() == "opaque",
               "source receipt retained across save and cleanup");
        BlobStore restarted(fixture.path.wstring(), [](HANDLE) {});
        restarted.Recover();
        Expect(restarted.Dispatch(Request(DataCommand::GetBlob)).bytes == replace.bytes, "restart persistence");
        // Flushed generation without committed index is an orphan, not an initialized store.
        const auto orphan = fixture.path / (L"blob." + NewId());
        WriteNew(orphan.wstring(), "incomplete-generation");
        restarted.Recover();
        Expect(!std::filesystem::exists(orphan), "precommit orphan reclaimed");
        for (unsigned i = 0; i < 130; ++i)
        {
            auto next = WriteRequestFor();
            std::get<Value::Object>(next.metadata.data)["condition"] = Value::Object{ { "kind", "IfRevision" }, { "expected", replaced.metadata.At("revision") } };
            replaced = restarted.Dispatch(next);
        }
        Expect(restarted.Dispatch(query).metadata.At("outcome").Text() == "Unknown", "expired receipt cannot authorize replay");
        Expect(restarted.Dispatch(Request(DataCommand::GetState)).metadata.At("cleanupAcknowledged").Boolean(), "cleanup survives receipt pruning");
        for (const auto& entry : std::filesystem::directory_iterator(fixture.path))
        {
            if (entry.path().filename().wstring().starts_with(L"blob."))
                std::filesystem::remove(entry.path());
        }
        Expect(restarted.Dispatch(Request(DataCommand::GetState)).metadata.At("state").Text() == "RecoveryRequired", "lost initialized blob cannot reinitialize");
        Reject([&] { restarted.Dispatch(WriteRequestFor()); });
        std::filesystem::remove(fixture.path / L"storage.index");
        Expect(restarted.Dispatch(Request(DataCommand::GetState)).metadata.At("state").Text() == "RecoveryRequired", "lost index not uninitialized");
    }
    void ConcurrentTests()
    {
        Fixture fixture;
        BlobStore store(fixture.path.wstring(), [](HANDLE) {});
        std::atomic<unsigned> committed = 0;
        auto initialize = [&] {
            try
            {
                store.Dispatch(WriteRequestFor());
                ++committed;
            }
            catch (const StorageError& failure)
            {
                if (failure.errorCode != ErrorCode::AlreadyInitialized)
                    throw;
            }
        };
        std::thread first(initialize), second(initialize);
        first.join();
        second.join();
        Expect(committed == 1, "single initialization winner");
    }
    void TransientTests()
    {
        TransientBlobStore store;
        Frame request{ DataCommand::CreateTransient, {}, Value::Object{ { "target", "workspaces.preview" }, { "operationId", Utf8(NewId()) }, { "contentSchema", "preview/1" } }, { 1, 0, 2 } };
        auto created = store.Dispatch(request);
        Expect(store.Dispatch(request).metadata.At("id").Text() == created.metadata.At("id").Text(), "transient operation deduplication");
        Frame read{ DataCommand::GetTransient, {}, Value::Object{ { "target", "workspaces.preview" }, { "id", created.metadata.At("id") } }, {} };
        Expect(store.Dispatch(read).bytes == request.bytes, "transient round trip");
        const auto initial = store.Dispatch(read);
        Frame update{ DataCommand::UpdateTransient, GuidBytes(Utf8(NewId())), Value::Object{ { "target", "workspaces.preview" }, { "id", created.metadata.At("id") }, { "operationId", Utf8(NewId()) }, { "contentSchema", "preview/2" }, { "condition", Value::Object{ { "kind", "IfRevision" }, { "expected", initial.metadata.At("revision") } } } }, { 8, 0, 6 } };
        Expect(DecodeFrame(EncodeFrame(update)).command == DataCommand::UpdateTransient, "transient CAS command framing");
        const auto updated = store.Dispatch(update);
        Expect(updated.metadata.At("revision").At("sequence").Text() == "2" &&
                   store.Dispatch(read).bytes == update.bytes,
               "Snapshot updates server-issued preview slot with CAS");
        Expect(store.Dispatch(update).metadata.At("revision").At("sequence").Text() == "2", "transient CAS idempotent retry");
        auto conflict = update;
        std::get<Value::Object>(conflict.metadata.data)["operationId"] = Utf8(NewId());
        Reject([&] { store.Dispatch(conflict); });
        auto changedReplay = update;
        changedReplay.bytes.push_back(1);
        Reject([&] { store.Dispatch(changedReplay); });
        auto wrongTarget = read;
        std::get<Value::Object>(wrongTarget.metadata.data)["target"] = "another.preview";
        Reject([&] { store.Dispatch(wrongTarget); });
        Expect(store.Dispatch(request).metadata.At("id").Text() == created.metadata.At("id").Text(),
               "original creation retry cannot overwrite updated preview");
        read.command = DataCommand::DeleteTransient;
        store.Dispatch(read);
        store.Dispatch(read);
        read.command = DataCommand::GetTransient;
        Reject([&] { store.Dispatch(read); });
    }
    void AuthorizationTests()
    {
        Expect(UsableOwnerTokenType(TokenPrimary, SecurityAnonymous), "ordinary primary token supports owner connection");
        Expect(UsableOwnerTokenType(TokenImpersonation, SecurityImpersonation), "usable owner impersonation token");
        Expect(!UsableOwnerTokenType(TokenImpersonation, SecurityIdentification), "identification linked token cannot perform owner file access");
        Expect(!UsableOwnerTokenType(TokenImpersonation, SecurityAnonymous), "anonymous linked token refused");
        HANDLE tokenRaw = nullptr;
        Expect(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &tokenRaw) != FALSE, "query test primary token");
        Handle primary(tokenRaw);
        TOKEN_ELEVATION elevation{};
        DWORD elevationSize = 0;
        Expect(GetTokenInformation(primary.get(), TokenElevation, &elevation, sizeof(elevation), &elevationSize) != FALSE,
               "observe actual primary-token elevation");
        if (elevation.TokenIsElevated)
        {
            Reject([&] { RequireOrdinaryMaintenanceProcess(GetCurrentProcess()); });
            TOKEN_LINKED_TOKEN linkedValue{};
            DWORD linkedSize = 0;
            if (GetTokenInformation(primary.get(), TokenLinkedToken, &linkedValue, sizeof(linkedValue), &linkedSize))
            {
                Handle linked(linkedValue.LinkedToken);
                TOKEN_TYPE linkedType{};
                Expect(GetTokenInformation(linked.get(), TokenType, &linkedType, sizeof(linkedType), &linkedSize) != FALSE,
                       "observe linked token type");
                SECURITY_IMPERSONATION_LEVEL linkedLevel = SecurityAnonymous;
                if (linkedType == TokenImpersonation)
                    Expect(GetTokenInformation(linked.get(), TokenImpersonationLevel, &linkedLevel, sizeof(linkedLevel), &linkedSize) != FALSE,
                           "observe linked impersonation level");
                if (!UsableOwnerTokenType(linkedType, linkedLevel))
                {
                    try
                    {
                        ProtectedStoreClient client;
                        Expect(false, "unusable linked token must fail before any service lookup");
                    }
                    catch (const StorageError& error)
                    {
                        Expect(error.errorCode == ErrorCode::OwnerContextRequired && error.code == ERROR_BAD_IMPERSONATION_LEVEL,
                               "unusable elevated context is not misclassified as missing storage");
                    }
                }
            }
        }
        else
        {
            RequireOrdinaryMaintenanceProcess(GetCurrentProcess());
            auto ordinary = OpenOwnerConnectionToken(TokenSid());
            Expect(static_cast<bool>(ordinary), "ordinary owner connection context remains available");
        }
        Expect((IdentityProcessAccess & PROCESS_QUERY_INFORMATION) != 0 &&
                   (IdentityProcessAccess & PROCESS_QUERY_LIMITED_INFORMATION) != 0 &&
                   (IdentityProcessAccess & SYNCHRONIZE) != 0 &&
                   (IdentityProcessAccess & (PROCESS_VM_READ | PROCESS_DUP_HANDLE | PROCESS_VM_WRITE | PROCESS_TERMINATE)) == 0,
               "identity ACL explicitly permits both query classes without mutation or VM reads");
        CallerIdentity statusOnly;
        AuthorizeTarget(statusOnly, "", DataCommand::GetCapabilities);
        Reject([&] { AuthorizeTarget(statusOnly, "workspaces.repository", DataCommand::GetState); });
        Reject([&] { AuthorizeTarget(statusOnly, "workspaces.repository", DataCommand::PutBlob); });
        CallerIdentity caller;
        caller.roles.insert("workspaces.reader");
        AuthorizeTarget(caller, "workspaces.repository", DataCommand::GetBlob);
        Reject([&] { AuthorizeTarget(caller, "workspaces.repository", DataCommand::PutBlob); });
        Reject([&] { AuthorizeTarget(caller, "..\\secret", DataCommand::GetBlob); });
        caller.roles.insert("workspaces.writer");
        Reject([&] { AuthorizeTarget(caller, "workspaces.preview", DataCommand::CreateTransient); });
        caller.roles.insert("workspaces.preview");
        AuthorizeTarget(caller, "workspaces.preview", DataCommand::CreateTransient);
        Reject([&] { AuthorizeTarget(caller, "workspaces.preview", DataCommand::PutBlob); });
        auto image = ModulePath();
        Handle executable(CreateFileW(image.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        Expect(static_cast<bool>(executable), "open own executable identity");
        VerifyProcessImageFile(GetCurrentProcess(), executable.get());
        Reject([&] {
            VerifyPeerImage(GetCurrentProcess(), ProcessBirth(GetCurrentProcess()) + 1, TokenSid(), {}, "workspaces.reader", image);
        });
        Fixture fixture;
        const auto copy = fixture.path / L"same-bytes.exe";
        {
            auto source = OpenRead(image);
            CopyHeld(source.get(), copy.wstring());
        }
        Handle replacement(CreateFileW(copy.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        Reject([&] { VerifyProcessImageFile(GetCurrentProcess(), replacement.get()); });
        replacement.reset();
        const auto link = fixture.path / L"hardlink.exe";
        Expect(CreateHardLinkW(link.c_str(), copy.c_str(), nullptr) != FALSE, "create isolated hardlink fixture");
        Reject([&] { OpenRead(copy.wstring()); });
        CallerIdentity launcher;
        launcher.roles.insert("workspaces.launcher");
        Reject([&] { AuthorizeRequest(launcher, WriteRequestFor()); });
        CallerIdentity arranger;
        arranger.roles.insert("workspaces.arranger");
        Reject([&] { AuthorizeTarget(arranger, "workspaces.repository", DataCommand::GetState); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.repository", DataCommand::GetBlob); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.repository", DataCommand::PutBlob); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.preview", DataCommand::GetTransient); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.preview", DataCommand::CreateTransient); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.preview", DataCommand::UpdateTransient); });
        Reject([&] { AuthorizeTarget(arranger, "workspaces.preview", DataCommand::DeleteTransient); });
        const Value planCatalog = Value::Object{ { "clients", Value::Array{ Value::Object{ { "image", "PowerToys.WorkspacesWindowArranger.exe" }, { "sha256", std::string(64, 'a') }, { "role", "workspaces.arranger" } } } } };
        const auto planRoles = CatalogRoles(planCatalog, L"PowerToys.WorkspacesWindowArranger.exe", std::string(64, 'a'), false);
        Expect(planRoles.contains("workspaces.arranger") && !planRoles.contains("workspaces.reader") &&
                   !planRoles.contains("workspaces.writer") && !planRoles.contains("workspaces.preview"),
               "arranger authenticates peer identity without granting storage roles");
        Value catalog = Value::Object{ { "clients", Value::Array{ Value::Object{ { "image", "PowerToys.ProtectedStorageMsiAction.exe" }, { "sha256", std::string(64, 'a') }, { "role", "maintenance.ca" } }, Value::Object{ { "image", "Writer.exe" }, { "sha256", std::string(64, 'b') }, { "role", "workspaces.writer" } } } } };
        Expect(CatalogRoles(catalog, L"MSI1234.tmp", std::string(64, 'a'), true).contains("maintenance"),
               "exact executable CA hash survives MSI extraction name");
        Expect(CatalogRoles(catalog, L"arbitrary-installer-generated-name.exe", std::string(64, 'a'), true).contains("maintenance"),
               "maintenance authority is exact mapped hash and signed role, not extraction filename");
        Expect(CatalogRoles(catalog, L"PowerToys.ProtectedStorageMsiAction.exe", std::string(64, 'a'), true).contains("maintenance"),
               "canonical executable CA identity");
        Expect(CatalogRoles(catalog, L"MSI1234.tmp", std::string(64, 'c'), true).empty(), "unapproved action hash refused");
        Expect(CatalogRoles(catalog, L"MSI1234.tmp", std::string(64, 'b'), true).empty(), "data image cannot use maintenance extraction exception");
        Expect(CatalogRoles(catalog, L"MSI1234.tmp", std::string(64, 'a'), false).empty(), "data endpoint has no extraction-name exception");
        PowerToys::ProtectedStorage::Request control{};
        control.command = Command::MsiQuery;
        const auto operation = NewId();
        std::copy(operation.begin(), operation.end(), control.operation);
        const std::wstring proof = L"C:\\signed-caller-proof";
        std::copy(proof.begin(), proof.end(), control.bundle);
        ValidateRequest(control);
        control.command = Command::MsiCommit;
        ValidateRequest(control);
        control.command = Command::MsiRollback;
        ValidateRequest(control);
        control.command = Command::Status;
        std::fill(std::begin(control.operation), std::end(control.operation), 0);
        Reject([&] { ValidateRequest(control); });
    }
    void UpdateTests()
    {
        Expect(MayUseIncomingMaintenanceProof(Command::MsiPrepare, false, false), "prepare requires incoming candidate authority");
        Expect(MayUseIncomingMaintenanceProof(Command::MsiQuery, false, false), "absent pre-transaction query accepts incoming caller proof");
        Expect(MayUseIncomingMaintenanceProof(Command::MsiRollback, false, false), "absent rollback can authenticate before reporting absent");
        Expect(MayUseIncomingMaintenanceProof(Command::MsiQuery, true, true), "next release may inspect terminal prior operation");
        Expect(!MayUseIncomingMaintenanceProof(Command::MsiQuery, true, false), "pending query uses protected transaction catalogs");
        for (const auto command : { Command::MsiCommit, Command::MsiRollback })
        {
            Expect(!MayUseIncomingMaintenanceProof(command, true, false), "pending decisions cannot replace protected authority");
            Expect(!MayUseIncomingMaintenanceProof(command, true, true), "terminal decisions retain original authority");
        }
        Expect(!MayUseIncomingMaintenanceProof(Command::MsiCommit, false, false), "absent operation cannot be committed");
        for (unsigned flags = 0; flags < 16; ++flags)
        {
            const ServiceHealth health{ (flags & 1) != 0, (flags & 2) != 0, (flags & 4) != 0, (flags & 8) != 0 };
            Expect(health.Healthy() == (flags == 1), "sync requires ready, recovered, settled, non-maintenance generation");
        }
        const auto owner = L"S-1-5-21-111-222-333-1001";
        const std::string hash(64, 'a');
        {
            Fixture fixture;
            const auto active = NewId();
            const auto current = fixture.path / active;
            MakeDirectory(current.wstring());
            WriteTransaction(current.wstring(), { owner, active, ParseVersion("1.0.0.0"), ParseVersion("2.0.0.0"), hash, hash, hash, hash, hash, hash });
            RecordOperation(current.wstring(), "prepared", "isolated pending operation");
            Expect(ResolveMsiOperation(current.wstring(), owner, NewId(), Command::MsiQuery).empty() &&
                       !IsTerminal(Journal(current.wstring())),
                   "unrelated absent query is not a settled-service health proof");
        }
        for (unsigned mode = 0; mode < 4; ++mode)
        {
            Fixture fixture;
            const auto operation = NewId();
            Transaction transaction{ owner, operation, ParseVersion("1.0.0.0"), ParseVersion("2.0.0.0"), hash, hash, hash, hash, hash, hash };
            WriteTransaction(fixture.path.wstring(), transaction);
            unsigned verification = 0, rollbacks = 0;
            UpdateActions actions{
                [] {}, [] {}, [] {}, [&] {
                    ++verification;
                    if ((mode == 1 && verification == 1) || (mode == 3 && verification == 2))
                        throw Error("injected readiness failure"); }, [&] { ++rollbacks; }, [] {}, [] {}, [&] { return mode == 2 ? Command::MsiRollback : Command::MsiCommit; }
            };
            const auto result = RunUpdate(fixture.path.wstring(), actions);
            const auto phase = Phase(Journal(fixture.path.wstring()));
            Expect(mode == 0 ? result == 0 && phase == "committed" && rollbacks == 0 :
                   mode == 3 ? result == 3 && phase == "recovery_required" && rollbacks == 0 :
                               result == 2 && phase == "rolled_back" && rollbacks == 1,
                   "durable update decision semantics");
        }
    }
    void PublicationFaultTests()
    {
        Fixture fixture;
        BlobStore store(fixture.path.wstring(), [](HANDLE) {});
        auto request = WriteRequestFor();
        // Holding the old index without delete-sharing forces the single commit point to fail.
        auto oldIndex = OpenRead((fixture.path / L"storage.index").wstring(), MaxMetadata);
        Reject([&] { store.Dispatch(request); });
        oldIndex.reset();
        store.Recover();
        Expect(store.Dispatch(Request(DataCommand::GetState)).metadata.At("state").Text() == "Uninitialized",
               "failed index publication cannot partially initialize");
        auto committed = store.Dispatch(request);
        Expect(committed.metadata.At("outcome").Text() == "Committed", "retry exact precommit operation");
        {
            BlobStore afterResponseLoss(fixture.path.wstring(), [](HANDLE) {});
            afterResponseLoss.Recover();
            auto query = Request(DataCommand::QueryWrite, { { "operationId", request.metadata.At("operationId") } });
            Expect(afterResponseLoss.Dispatch(query).metadata.At("outcome").Text() == "Committed", "recover after lost response");
        }
    }
    void PurgeTests()
    {
        Fixture fixture;
        const std::wstring owner = L"S-1-5-21-111-222-333-1001";
        const auto marker = "format=1\nowner=" + Utf8(owner) + "\nmode=purge\n";
        const auto path = fixture.path / L"purged";
        WriteNew(path.wstring(), marker);
        ValidatePurgeTombstone(ReadText(path.wstring()), owner);
        Reject([&] { ValidatePurgeTombstone(marker, L"S-1-5-21-111-222-333-1002"); });
        Reject([&] { ValidatePurgeTombstone("format=1\nowner=" + Utf8(owner) + "\nmode=keep\n", owner); });
        Reject([&] { ValidatePurgeTombstone(marker + "mode=purge\n", owner); });
        Reject([&] { ValidatePurgeTombstone("format=1\nowner=" + Utf8(owner) + "\n", owner); });
        auto initialize = WriteRequestFor();
        ValidateImportIntent(initialize, true);
        std::get<Value::Object>(initialize.metadata.data)["migrationSource"] = Value::Object{ { "origin", "legacy" } };
        ValidateImportIntent(initialize, false);
        Reject([&] { ValidateImportIntent(initialize, true); });
        Expect(ReadText(path.wstring()) == marker, "suppression validation does not consume purge tombstone");
    }
    void CatalogTests()
    {
        const auto decode = [](const char* text) {
            DWORD size = 0;
            Expect(CryptStringToBinaryA(text, 0, CRYPT_STRING_BASE64, nullptr, &size, nullptr, nullptr) != FALSE, "fixture base64 size");
            std::vector<BYTE> bytes(size);
            Expect(CryptStringToBinaryA(text, 0, CRYPT_STRING_BASE64, bytes.data(), &size, nullptr, nullptr) != FALSE, "fixture base64");
            bytes.resize(size);
            return bytes;
        };
        const auto signature = decode(CatalogSignatureBase64);
        Value catalog = Value::Object{ { "format", uint64_t{ 1 } }, { "app", "PowerToysProtectedStorage" }, { "version", "1.0.0.0" }, { "clients", Value::Array{ Value::Object{ { "image", "test.exe" }, { "sha256", std::string(64, 'a') }, { "role", "workspaces.writer" } } } } };
        const auto text = catalog.Stringify();
        Fixture fixture;
        const auto catalogPath = fixture.path / L"ClientCatalog.json";
        WriteNew(catalogPath.wstring(), text);
        WriteNew((fixture.path / L"ClientCatalog.p7s").wstring(),
                 std::string_view(reinterpret_cast<const char*>(signature.data()), signature.size()));
        Policy policy{ ReleaseTrustPolicy, 0 };
        const auto proof = ReadSignedClientCatalog(fixture.path.wstring());
        CRYPT_VERIFY_MESSAGE_PARA verify{ sizeof(verify), X509_ASN_ENCODING | PKCS_7_ASN_ENCODING };
        const BYTE* content = proof.document.data();
        DWORD contentSize = static_cast<DWORD>(proof.document.size());
        Expect(CryptVerifyDetachedMessageSignature(&verify, 0, proof.signature.data(), static_cast<DWORD>(proof.signature.size()),
                                                  1, &content, &contentSize, nullptr) != FALSE,
               "self-signed fixture has a mathematically valid signature");
        Reject([&] { VerifyClientCatalog(fixture.path.wstring(), policy, ParseVersion("1.0.0.0")); });
        Reject([&] { VerifySignedClientCatalog(proof, policy); });
        Expect(ParseClientCatalogDocument(proof.document, policy).At("version").Text() == "1.0.0.0", "catalog schema independent of signature trust");
        Policy raisedFloor{ policy.signerPolicy, ParseVersion("2.0.0.0") };
        Reject([&] { ParseClientCatalogDocument(proof.document, raisedFloor); });
        Reject([&] { ParseClientCatalogDocument(proof.document, policy, ParseVersion("2.0.0.0")); });
        Reject([&] { VerifySignedClientCatalog(proof, raisedFloor); });
        auto tamperedProof = proof;
        tamperedProof.document.push_back(' ');
        Reject([&] { VerifySignedClientCatalog(tamperedProof, policy); });
        Frame proofFrame{ DataCommand::GetCapabilities, {}, Value::Object{ { "catalogLength", uint64_t{ proof.document.size() } } }, proof.document };
        proofFrame.bytes.insert(proofFrame.bytes.end(), proof.signature.begin(), proof.signature.end());
        auto receivedProof = DecodeFrame(EncodeFrame(proofFrame));
        const auto separator = receivedProof.bytes.begin() + static_cast<size_t>(receivedProof.metadata.At("catalogLength").Number());
        SignedClientCatalog received{ { receivedProof.bytes.begin(), separator }, { separator, receivedProof.bytes.end() } };
        Expect(received.document == proof.document && received.signature == proof.signature, "signed peer proof bytes survive wire round trip");
        Reject([&] { VerifySignedClientCatalog(received, policy); });
        const auto ownerStage = fixture.path / L"owner-stage";
        const auto protectedTransaction = fixture.path / L"transaction";
        MakeDirectory(ownerStage.wstring());
        MakeDirectory(protectedTransaction.wstring());
        for (const auto* name : { L"ClientCatalog.json", L"ClientCatalog.p7s" })
        {
            auto original = OpenRead((fixture.path / name).wstring(), 1024 * 1024);
            CopyHeld(original.get(), (ownerStage / name).wstring());
            auto staged = OpenRead((ownerStage / name).wstring(), 1024 * 1024);
            CopyHeld(staged.get(), (protectedTransaction / name).wstring());
        }
        std::filesystem::remove_all(ownerStage);
        const auto retained = ReadSignedClientCatalog(protectedTransaction.wstring());
        Expect(retained.document == proof.document && retained.signature == proof.signature,
               "transaction retains exact catalog proof after owner stage disappears");
        Policy incorrect{ std::string(64, 'b'), 0 };
        Reject([&] { VerifyClientCatalog(fixture.path.wstring(), incorrect, ParseVersion("1.0.0.0")); });
        Reject([&] { ParseClientCatalogDocument(proof.document, incorrect); });
        Reject([&] { VerifyClientCatalog(fixture.path.wstring(), policy, ParseVersion("2.0.0.0")); });
        ReplaceText(catalogPath.wstring(), text + " ");
        Reject([&] { VerifyClientCatalog(fixture.path.wstring(), policy, ParseVersion("1.0.0.0")); });
    }
    void SigningPolicyTests()
    {
        const std::string policy = "format=2\napp=PowerToysProtectedStorage\nsigner_policy=microsoft-production-v1\nminimum_version=1.2.3.0\n";
        const auto parsed = ParsePolicy(policy);
        Expect(parsed.signerPolicy == ReleaseTrustPolicy && parsed.minimum == ParseVersion("1.2.3.0"),
               "fixed publisher policy replaces caller-selected certificate pins");
        Reject([&] { ParsePolicy("format=1\napp=PowerToysProtectedStorage\nsigner_sha256=" + std::string(64, 'a') + "\nminimum_version=1.2.3.0\n"); });
        Reject([&] { ParsePolicy("format=2\napp=PowerToysProtectedStorage\nsigner_policy=any-trusted-publisher\nminimum_version=1.2.3.0\n"); });
        Reject([&] { ParsePolicy("format=2\napp=OtherProduct\nsigner_policy=microsoft-production-v1\nminimum_version=1.2.3.0\n"); });
        Reject([&] { ParsePolicy(policy + "signer_policy=microsoft-production-v1\n"); });
    }
}
int main()
{
    try
    {
        ProtocolTests();
        ClientOutcomeTests();
        StoreTests();
        ConcurrentTests();
        TransientTests();
        AuthorizationTests();
        UpdateTests();
        PublicationFaultTests();
        PurgeTests();
        CatalogTests();
        SigningPolicyTests();
        std::cout << "PASS " << passed << " assertions; 20000 deterministic framing mutations; isolated local fixtures\n";
        return 0;
    }
    catch (const std::exception& failure)
    {
        std::cerr << "FAIL: " << failure.what() << '\n';
        return 1;
    }
}
