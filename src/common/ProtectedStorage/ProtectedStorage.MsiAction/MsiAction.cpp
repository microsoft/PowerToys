// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "SetupSupport.h"
#include "MaintenanceContract.h"
#include "PayloadCleanup.h"
#include "..\ProtectedStorage.Common\Common.h"
#include "..\ProtectedStorage.Common\Protocol.h"
#include "..\ProtectedStorage.Client\ProtectedStoreClient.h"

namespace PowerToysProtectedStorage
{
    using namespace PowerToys::ProtectedStorage;
}

namespace
{
    std::filesystem::path operation_file()
    {
        auto directory = product::known_folder(FOLDERID_LocalAppData) / product::name / L"MsiOperations";
        std::filesystem::create_directories(directory);
        return directory / (std::wstring(product::code) + L".txt");
    }
    void log(const std::string& text)
    {
        auto directory = product::known_folder(FOLDERID_LocalAppData) / product::name / L"Logs";
        std::filesystem::create_directories(directory);
        std::ofstream stream(directory / L"msi-actions.log", std::ios::app | std::ios::binary);
        stream << text << "\n";
        stream.flush();
        if (!stream) throw PowerToysProtectedStorage::failure("write user MSI action log", ERROR_WRITE_FAULT);
        std::cout << text << "\n";
    }
    void ordinary_owner()
    {
        PowerToysProtectedStorage::handle token;
        PowerToysProtectedStorage::check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token.value) != FALSE, "MSI action token");
        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        PowerToysProtectedStorage::check(GetTokenInformation(token.value, TokenElevation, &elevation, sizeof(elevation), &size) != FALSE, "MSI action elevation");
        log("actor=" + PowerToysProtectedStorage::Utf8(PowerToysProtectedStorage::token_sid(token.value)) + " elevated=" + std::to_string(elevation.TokenIsElevated) +
            " product=" + PowerToysProtectedStorage::Utf8(product::code) + " msiVersion=" + PowerToysProtectedStorage::Utf8(product::version));
        if (elevation.TokenIsElevated) throw PowerToysProtectedStorage::failure("MSI carrier actions must run as the ordinary owner", ERROR_ACCESS_DENIED);
        PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::actor_sid());
    }
    struct Operation
    {
        std::wstring id;
        std::filesystem::path stage;
    };
    constexpr bool restarting(DWORD code)
    {
        return code == ERROR_FILE_NOT_FOUND || code == ERROR_PIPE_BUSY || code == ERROR_BROKEN_PIPE ||
            code == ERROR_NO_DATA || code == ERROR_PIPE_NOT_CONNECTED || code == WAIT_TIMEOUT || code == ERROR_SEM_TIMEOUT;
    }
    std::map<std::string, std::string> call(const PowerToysProtectedStorage::Paths& paths, PowerToysProtectedStorage::Command command,
        const Operation& operation, const std::wstring& bundle = {})
    {
        const auto deadline = GetTickCount64() + 60000;
        for (;;)
        {
            try { return PowerToysProtectedStorage::MsiCall(paths, command, operation.id,
                bundle.empty() ? operation.stage.wstring() : bundle); }
            catch (const PowerToysProtectedStorage::Error& error)
            {
                if (!restarting(error.code) || GetTickCount64() >= deadline) throw;
            }
            Sleep(100);
        }
    }
    Operation load()
    {
        auto values = PowerToysProtectedStorage::ParseKeys(PowerToysProtectedStorage::ReadText(operation_file().wstring()), { "format", "operation", "stage" });
        if (values.at("format") != "1") throw PowerToysProtectedStorage::failure("operation format", ERROR_INVALID_DATA);
        Operation operation{ PowerToysProtectedStorage::Wide(values.at("operation")), PowerToysProtectedStorage::Wide(values.at("stage")) };
        if (!PowerToysProtectedStorage::ValidId(operation.id) ||
            operation.stage.parent_path() != product::known_folder(FOLDERID_PublicDocuments) ||
            operation.stage.filename() != L"PowerToysProtectedStoragePayload_" + operation.id)
            throw PowerToysProtectedStorage::failure("unexpected operation stage", ERROR_INVALID_DATA);
        return operation;
    }
    void clean_stage(const Operation& operation)
    {
        PowerToysProtectedStorage::Maintenance::CleanupPayloadStage(operation.stage);
    }
    std::map<std::string, std::string> wait_for(const PowerToysProtectedStorage::Paths& paths, const Operation& operation,
        const std::vector<std::string>& desired)
    {
        const auto deadline = GetTickCount64() + 120000;
        std::string lastFailure;
        do
        {
            try
            {
                auto result = PowerToysProtectedStorage::MsiCall(paths, PowerToysProtectedStorage::Command::MsiQuery, operation.id, operation.stage.wstring());
                const auto& phase = result.at("phase");
                for (const auto& expected : desired) if (phase == expected) return result;
                if (phase == "recovery_required" || phase == "rolled_back" || phase == "absent")
                    throw PowerToysProtectedStorage::failure(("VA operation terminal without desired result: " + phase).c_str(), ERROR_INSTALL_FAILURE);
            }
            catch (const PowerToysProtectedStorage::Error& error)
            {
                // The fixed pipe disappears briefly while the same VA restarts both PEs.
                if (!restarting(error.code)) throw;
                lastFailure = error.what();
            }
            Sleep(100);
        } while (GetTickCount64() < deadline);
        throw PowerToysProtectedStorage::failure(("VA operation wait expired: " + lastFailure).c_str(), WAIT_TIMEOUT);
    }
    void extract(const PowerToysProtectedStorage::Paths& paths, const Operation& operation, const std::filesystem::path& package);
    void begin(const PowerToysProtectedStorage::Paths& paths, const std::filesystem::path& package)
    {
        Operation operation{ PowerToysProtectedStorage::NewId(), {} };
        operation.stage = product::known_folder(FOLDERID_PublicDocuments) / (L"PowerToysProtectedStoragePayload_" + operation.id);
        extract(paths, operation, package);
        if (PowerToysProtectedStorage::Exists(operation_file().wstring()))
        {
            auto previous = load();
            auto state = PowerToysProtectedStorage::MsiCall(paths, PowerToysProtectedStorage::Command::MsiQuery, previous.id, operation.stage.wstring());
            const auto& phase = state.at("phase");
            if (!PowerToysProtectedStorage::Maintenance::KnownTerminalPhase(phase))
            {
                clean_stage(operation);
                throw PowerToysProtectedStorage::failure("prior MSI operation still pending", ERROR_BUSY);
            }
            clean_stage(previous);
            PowerToysProtectedStorage::check(DeleteFileW(operation_file().c_str()) != FALSE, "retire prior MSI operation");
            DeleteFileW(product::cleanup_hint().c_str());
        }
        log("begin operation=" + PowerToysProtectedStorage::Utf8(operation.id));
        PowerToysProtectedStorage::WriteNew(operation_file().wstring(), "format=1\noperation=" + PowerToysProtectedStorage::Utf8(operation.id) +
            "\nstage=" + PowerToysProtectedStorage::Utf8(operation.stage.wstring()) + "\n");
    }
    void extract(const PowerToysProtectedStorage::Paths& paths, const Operation& operation, const std::filesystem::path& package)
    {
        PowerToysProtectedStorage::handle source(CreateFileW(package.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
            OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        PowerToysProtectedStorage::check(source.value != INVALID_HANDLE_VALUE, "hold matching MSI package");
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(package);
        setup::msi_handle database;
        PowerToysProtectedStorage::result(MsiOpenDatabaseW(package.c_str(), MSIDBOPEN_READONLY, &database.value), "open signed MSI Binary payloads");
        setup::verify_package(database.value);
        PSECURITY_DESCRIPTOR security = nullptr;
        const auto sddl = L"O:" + paths.owner + L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + paths.owner +
            L")(A;OICI;FRFX;;;" + paths.va + L")";
        PowerToysProtectedStorage::check(ConvertStringSecurityDescriptorToSecurityDescriptorW(sddl.c_str(), SDDL_REVISION_1,
            &security, nullptr) != FALSE, "payload stage DACL");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), security, FALSE };
        const BOOL created = CreateDirectoryW(operation.stage.c_str(), &attributes);
        const DWORD error = GetLastError();
        LocalFree(security);
        if (!created) throw PowerToysProtectedStorage::failure("create fresh owner/VA-readable payload stage", error);
        struct Payload { const wchar_t* binary; const wchar_t* name; };
        const Payload payload[] = {
            { L"Bootstrap", L"Bootstrap.exe" }, { L"Runtime", L"Runtime.exe" },
            { L"Manifest", L"manifest.txt" }, { L"ManifestSignature", L"manifest.p7s" },
            { L"ClientCatalog", L"ClientCatalog.json" }, { L"ClientCatalogSignature", L"ClientCatalog.p7s" }
        };
        for (const auto& [binary, name] : payload)
        {
            setup::msi_handle view, record;
            const auto query = L"SELECT `Data` FROM `Binary` WHERE `Name` = '" + std::wstring(binary) + L"'";
            PowerToysProtectedStorage::result(MsiDatabaseOpenViewW(database.value, query.c_str(), &view.value), "open fixed Binary stream");
            PowerToysProtectedStorage::result(MsiViewExecute(view.value, 0), "execute fixed Binary query");
            PowerToysProtectedStorage::result(MsiViewFetch(view.value, &record.value), "read fixed Binary record");
            std::vector<BYTE> bytes;
            for (;;)
            {
                BYTE buffer[65536]{};
                DWORD count = sizeof(buffer);
                PowerToysProtectedStorage::result(MsiRecordReadStream(record.value, 1, reinterpret_cast<char*>(buffer), &count), "read fixed payload stream");
                if (!count) break;
                if (bytes.size() + count > 64ull * 1024 * 1024)
                    throw PowerToysProtectedStorage::failure("payload resource too large", ERROR_FILE_TOO_LARGE);
                bytes.insert(bytes.end(), buffer, buffer + count);
            }
            if (bytes.empty()) throw PowerToysProtectedStorage::failure("missing matching MSI payload", ERROR_INVALID_DATA);
            product::write_new(operation.stage / name, bytes);
        }
    }
    void prepare(const PowerToysProtectedStorage::Paths& paths, const Operation& operation)
    {
        log("payloadSource=matching-signed-MSI-Binary operation=" + PowerToysProtectedStorage::Utf8(operation.id) +
            " manifest_sha256=" + PowerToysProtectedStorage::HashPath((operation.stage / L"manifest.txt").wstring()));
        auto response = call(paths, PowerToysProtectedStorage::Command::MsiPrepare, operation, operation.stage.wstring());
        log("prepare response phase=" + response.at("phase") + " operation=" + response.at("operation"));
        response = wait_for(paths, operation, { "prepared", "unchanged" });
        if (response.at("version") != PowerToysProtectedStorage::Utf8(product::liveSeedVersion))
            throw PowerToysProtectedStorage::failure("prepared runtime version does not match MSI payload", ERROR_REVISION_MISMATCH);
        log("prepare complete operation=" + PowerToysProtectedStorage::Utf8(operation.id) + " version=" + response.at("version"));
    }
    void decision(const PowerToysProtectedStorage::Paths& paths, const Operation& operation, bool commit)
    {
        // Retain the signed caller proof and payload until the exact decision is
        // known. A lost commit reply is never permission to silently roll back.
        log(std::string(commit ? "commit" : "rollback") + " decision requested operation=" + PowerToysProtectedStorage::Utf8(operation.id));
        auto response = call(paths, commit ? PowerToysProtectedStorage::Command::MsiCommit : PowerToysProtectedStorage::Command::MsiRollback, operation);
        response = wait_for(paths, operation, commit ? std::vector<std::string>{ "committed", "unchanged" } :
            std::vector<std::string>{ "rolled_back", "unchanged", "absent" });
        if (commit)
        {
            const bool cleaned = PowerToysProtectedStorage::Maintenance::TryTerminalCleanup(response.at("phase"), [&] {
                log("commit complete operation=" + PowerToysProtectedStorage::Utf8(operation.id) + " phase=" + response.at("phase"));
                clean_stage(operation);
                if (!DeleteFileW(product::cleanup_hint().c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
                    throw PowerToysProtectedStorage::failure("retire cleanup hint", GetLastError());
            });
            if (!cleaned)
            {
                try
                {
                    if (!setup::exists(product::cleanup_hint()))
                        product::write_new_text(product::cleanup_hint(), operation.id);
                }
                catch (const std::exception&) {}
            }
            return;
        }
        log("rollback complete operation=" + PowerToysProtectedStorage::Utf8(operation.id) +
            " phase=" + response.at("phase") + " version=" + response.at("version"));
        clean_stage(operation);
        PowerToysProtectedStorage::check(DeleteFileW(operation_file().c_str()) != FALSE, "remove rolled-back operation");
    }
    void inspect(const PowerToysProtectedStorage::Paths& paths, const std::filesystem::path& package)
    {
        Operation operation{ PowerToysProtectedStorage::NewId(), {} };
        operation.stage = product::known_folder(FOLDERID_PublicDocuments) / (L"PowerToysProtectedStoragePayload_" + operation.id);
        extract(paths, operation, package);
        struct cleanup
        {
            const Operation& operation;
            ~cleanup()
            {
                try { clean_stage(operation); }
                catch (const std::exception&) {}
            }
        } cleanupStage{ operation };
        auto bundle = PowerToysProtectedStorage::ValidateBundle(operation.stage.wstring(),
            PowerToysProtectedStorage::LoadPolicy(paths), PowerToysProtectedStorage::ParseVersion(PowerToysProtectedStorage::Utf8(product::liveSeedVersion)));
        PowerToysProtectedStorage::Request statusRequest;
        const auto status = PowerToys::ProtectedStorage::Value::Parse(PowerToysProtectedStorage::Call(paths, statusRequest));
        PowerToysProtectedStorage::Maintenance::RequireHealthyControlStatus(status,
            PowerToysProtectedStorage::Utf8(product::liveSeedVersion));
        if (!status.At("ok").Boolean() || status.At("owner").Text() != PowerToysProtectedStorage::Utf8(paths.owner) ||
            status.At("service").Text() != PowerToysProtectedStorage::Utf8(paths.service) ||
            status.At("bootstrap").At("version").Text() != PowerToysProtectedStorage::Utf8(product::liveSeedVersion) ||
            status.At("worker").At("version").Text() != PowerToysProtectedStorage::Utf8(product::liveSeedVersion) ||
            status.At("bootstrap").At("sha256").Text() != bundle.bootstrapHash ||
            status.At("worker").At("sha256").Text() != bundle.runtimeHash)
            throw PowerToysProtectedStorage::failure("live release does not match the registered signed carrier", ERROR_REVISION_MISMATCH);
        const auto& phase = status.At("update").At("phase").Text();
        if (phase != "none" && !PowerToysProtectedStorage::Maintenance::KnownTerminalPhase(phase))
            throw PowerToysProtectedStorage::failure("existing update is pending or requires recovery", ERROR_INVALID_STATE);
        PowerToys::ProtectedStorage::ProtectedStoreClient client;
        const auto capabilities = client.GetCapabilities();
        if (capabilities.At("recoveryRequired").Boolean() || capabilities.At("maintenance").Boolean() ||
            capabilities.At("release").Text() != PowerToysProtectedStorage::Utf8(product::liveSeedVersion))
            throw PowerToysProtectedStorage::failure("protected store requires recovery or is under maintenance", ERROR_INVALID_STATE);
        const auto liveCatalog = client.GetSignedClientCatalog();
        PowerToys::ProtectedStorage::VerifySignedClientCatalog(liveCatalog, PowerToysProtectedStorage::LoadPolicy(paths), bundle.version);
        if (PowerToysProtectedStorage::HashBytes(liveCatalog.document.data(), static_cast<DWORD>(liveCatalog.document.size())) != bundle.catalogHash)
            throw PowerToysProtectedStorage::failure("live caller catalog differs from the registered signed release", ERROR_REVISION_MISMATCH);
    }
}
int wmain(int argc, wchar_t** argv)
{
    try
    {
        PowerToysProtectedStorage::initialize_process_security();
        if (argc != 2 && argc != 3) throw PowerToysProtectedStorage::failure("MSI action verb required", ERROR_BAD_ARGUMENTS);
        ordinary_owner();
        const std::wstring verb(argv[1]);
        log("action=" + PowerToysProtectedStorage::Utf8(verb));
        PowerToysProtectedStorage::Paths paths(PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::actor_sid()));
        if (verb == L"remove-guard")
        {
            const auto record = product::state_root() / L"Installer" / paths.owner;
            if (!PowerToysProtectedStorage::Exists((record / L"drain").wstring()) || !PowerToysProtectedStorage::Exists((record / L"pending-remove").wstring()))
                throw PowerToysProtectedStorage::failure("use Setup remove to authorize protected service teardown", ERROR_ACCESS_DENIED);
        }
        else if (verb == L"inspect" && argc == 3) inspect(paths, argv[2]);
        else if (verb == L"begin" && argc == 3) begin(paths, argv[2]);
        else if (verb == L"prepare") prepare(paths, load());
        else if (verb == L"commit") decision(paths, load(), true);
        else if (verb == L"rollback") decision(paths, load(), false);
        else throw PowerToysProtectedStorage::failure("unknown MSI action", ERROR_BAD_ARGUMENTS);
        return 0;
    }
    catch (const std::exception& error)
    {
        const auto* native = dynamic_cast<const PowerToysProtectedStorage::failure*>(&error);
        const std::string text = "MSI_ACTION_FAILED " + PowerToysProtectedStorage::ErrorJson(error) +
            (native ? " installer_win32=" + std::to_string(native->code) : "");
        try { log(text); }
        catch (const std::exception& logError) { std::cerr << text << "\nlog failure: " << logError.what() << "\n"; }
        if (argc > 1 && std::wstring(argv[1]) == L"inspect")
        {
            if (native) return static_cast<int>(native->code ? native->code : ERROR_INSTALL_FAILURE);
            if (const auto* runtime = dynamic_cast<const PowerToysProtectedStorage::Error*>(&error))
                return static_cast<int>(runtime->code ? runtime->code : ERROR_INSTALL_FAILURE);
        }
        return ERROR_INSTALL_FAILURE;
    }
}
