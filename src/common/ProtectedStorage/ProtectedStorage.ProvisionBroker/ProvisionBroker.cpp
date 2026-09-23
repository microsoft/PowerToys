// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "SetupSupport.h"
#include "MaintenanceContract.h"

namespace
{
    DWORD ownerPid = 0;
    ULONGLONG ownerBirth = 0;
    std::wstring serviceName;
    std::wstring transaction;
    std::wstring requestNonce;
    std::wstring authorizedVerb;
    std::filesystem::path stage;
    SERVICE_STATUS_HANDLE statusHandle = nullptr;
    SERVICE_STATUS status{ SERVICE_WIN32_OWN_PROCESS, SERVICE_START_PENDING, 0, 0, 0, 0, 3000 };
    struct unknown_outcome : PowerToysProtectedStorage::failure
    {
        explicit unknown_outcome(DWORD code) : failure("provisioner outcome unknown; retain stage and do not race rollback", code) {}
    };
    std::wstring read_text(const std::filesystem::path& path)
    {
        std::wifstream file(path);
        std::wstring value;
        if (!(file >> value)) throw PowerToysProtectedStorage::failure("read protected provisioning receipt", ERROR_INVALID_DATA);
        return value;
    }
    void extract_provisioner()
    {
        product::write_new(stage / L"Carrier.msi", product::resource(101), PowerToysProtectedStorage::system_acl());
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(stage / L"Carrier.msi");
        setup::msi_handle database, view, record;
        PowerToysProtectedStorage::result(MsiOpenDatabaseW((stage / L"Carrier.msi").c_str(), MSIDBOPEN_READONLY, &database.value), "open exact embedded MSI read-only");
        setup::verify_package(database.value);
        PowerToysProtectedStorage::result(MsiDatabaseOpenViewW(database.value, L"SELECT `Data` FROM `Binary` WHERE `Name` = 'Provisioner'", &view.value), "select Binary.Provisioner");
        PowerToysProtectedStorage::result(MsiViewExecute(view.value, 0), "execute Binary.Provisioner view");
        PowerToysProtectedStorage::result(MsiViewFetch(view.value, &record.value), "fetch Binary.Provisioner");
        std::vector<BYTE> executable;
        BYTE buffer[65536]{};
        for (;;)
        {
            DWORD size = sizeof(buffer);
            PowerToysProtectedStorage::result(MsiRecordReadStream(record.value, 1, reinterpret_cast<char*>(buffer), &size), "read Binary.Provisioner");
            if (!size) break;
            if (executable.size() + size > 256ull * 1024 * 1024) throw PowerToysProtectedStorage::failure("embedded provisioner size limit", ERROR_FILE_TOO_LARGE);
            executable.insert(executable.end(), buffer, buffer + size);
        }
        if (executable.empty()) throw PowerToysProtectedStorage::failure("empty Binary.Provisioner", ERROR_INVALID_DATA);
        product::write_new(stage / L"Provisioner.exe", executable, PowerToysProtectedStorage::system_acl());
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(stage / L"Provisioner.exe");
    }
    void provisioner(const std::wstring& verb, const std::wstring& owner, bool initial, std::wofstream& evidence)
    {
        product::assert_system_directory(stage);
        const auto executable = stage / L"Provisioner.exe";
        const DWORD flags = GetFileAttributesW(executable.c_str());
        if (flags == INVALID_FILE_ATTRIBUTES || (flags & (FILE_ATTRIBUTE_REPARSE_POINT | FILE_ATTRIBUTE_DIRECTORY)))
            throw PowerToysProtectedStorage::failure("protected embedded provisioner missing or unsafe", ERROR_ACCESS_DENIED);
        std::wstring command = L"\"" + executable.wstring() + L"\" " + verb + L" " + owner + L" " + transaction +
            (initial && verb == L"prepare-remove" ? L" initial" :
             authorizedVerb == L"purge" && (verb == L"prepare-remove" || verb == L"commit-remove") ? L" purge" : L"");
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), nullptr, TRUE };
        PowerToysProtectedStorage::handle output(CreateFileW((stage / (verb + L"-" + setup::unique_id() + L".log")).c_str(),
            GENERIC_WRITE, FILE_SHARE_READ, &attributes, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr));
        PowerToysProtectedStorage::check(output.value != INVALID_HANDLE_VALUE, "create protected provisioner log");
        STARTUPINFOW startup{ sizeof(startup) };
        startup.dwFlags = STARTF_USESTDHANDLES;
        startup.hStdOutput = output.value;
        startup.hStdError = output.value;
        PROCESS_INFORMATION child{};
        evidence << L"provisionerBegin=" << verb << L" owner=" << owner << L" transaction=" << (initial ? transaction : L"explicit-remove") << L"\n" << std::flush;
        // Only the Binary stream extracted from this broker's embedded MSI is executed as SYSTEM.
        // The installed Bootstrap/Runtime are always started by SCM under their VA account.
        PowerToysProtectedStorage::check(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW,
            nullptr, stage.c_str(), &startup, &child) != FALSE, "start trusted embedded provisioner");
        PowerToysProtectedStorage::handle process(child.hProcess), thread(child.hThread);
        const DWORD wait = WaitForSingleObject(process.value, 300000);
        if (wait != WAIT_OBJECT_0)
            throw unknown_outcome(wait == WAIT_TIMEOUT ? WAIT_TIMEOUT : GetLastError());
        DWORD result = 0;
        PowerToysProtectedStorage::check(GetExitCodeProcess(process.value, &result) != FALSE, "provisioner native result");
        evidence << L"provisionerEnd=" << verb << L" nativeResult=" << result << L"\n" << std::flush;
        if (!setup::success(result))
            PowerToysProtectedStorage::result(result, "trusted embedded provisioner failed");
        if (result != ERROR_SUCCESS && !setup::exists(stage / L"cleanup-pending"))
            product::write_new_text(stage / L"cleanup-pending", std::to_wstring(result));
    }
    bool owns_initial(const std::wstring& owner)
    {
        const auto record = product::state_root() / L"Installer" / owner;
        if (!setup::exists(record / L"provisioning-id")) return false;
        product::assert_system_directory(record);
        return read_text(record / L"provisioning-id") == transaction;
    }
    void rollback_initial(const std::wstring& owner, std::wofstream& evidence)
    {
        if (!owns_initial(owner)) return;
        const auto record = product::state_root() / L"Installer" / owner;
        if (setup::exists(record / L"active"))
        {
            if (!setup::exists(record / L"pending-remove")) provisioner(L"prepare-remove", owner, true, evidence);
            provisioner(L"commit-remove", owner, true, evidence);
        }
        else provisioner(L"undo-install", owner, true, evidence);
    }
    void phase(const std::wstring& mode, std::wofstream& evidence)
    {
        const bool initial = mode == L"provision";
        const bool removal = mode == L"prepare-remove" || mode == L"prepare-purge";
        const bool repair = mode == L"repair-bootstrap";
        if (initial || removal || repair)
        {
            PowerToysProtectedStorage::handle requester;
            const auto owner = setup::requester_owner(ownerPid, ownerBirth, requester, false);
            std::wifstream request(stage / L"request");
            DWORD recordedPid = 0;
            ULONGLONG recordedBirth = 0;
            std::wstring nonce, verb, recordedOwner;
            if (!(request >> recordedPid >> recordedBirth >> nonce >> verb >> recordedOwner) ||
                recordedPid != ownerPid || recordedBirth != ownerBirth || nonce != requestNonce || recordedOwner != owner ||
                (initial && verb != L"install") || (repair && verb != L"repair") ||
                (mode == L"prepare-purge" && verb != L"purge") || (mode == L"prepare-remove" && verb != L"remove"))
                throw PowerToysProtectedStorage::failure("protected authorization request mismatch", ERROR_ACCESS_DENIED);
            PowerToysProtectedStorage::Maintenance::ValidateOwnerBinding(
                { ownerPid, ownerBirth, requestNonce, owner, initial ? L"install" : repair ? L"repair" :
                  mode == L"prepare-purge" ? L"purge" : L"remove" },
                { recordedPid, recordedBirth, nonce, recordedOwner, verb });
            authorizedVerb = verb;
            std::vector<wchar_t> image(32768);
            DWORD length = static_cast<DWORD>(image.size());
            PowerToysProtectedStorage::check(QueryFullProcessImageNameW(requester.value, 0, image.data(), &length) != FALSE,
                "requester signed image");
            const std::filesystem::path original(std::wstring(image.data(), length));
            PowerToysProtectedStorage::Maintenance::VerifySignedFile(original);
            PowerToysProtectedStorage::handle originalFile(CreateFileW(original.c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            PowerToysProtectedStorage::handle authorizedFile(CreateFileW((stage / L"AuthorizedSetup.exe").c_str(), GENERIC_READ,
                FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            PowerToysProtectedStorage::check(originalFile.value != INVALID_HANDLE_VALUE && authorizedFile.value != INVALID_HANDLE_VALUE,
                "hold original and authorized Setup bytes");
            PowerToysProtectedStorage::Maintenance::VerifyMappedImage(requester.value, originalFile.value);
            if (PowerToysProtectedStorage::Maintenance::Hash(PowerToysProtectedStorage::Maintenance::ReadLocked(originalFile.value)) !=
                PowerToysProtectedStorage::Maintenance::Hash(PowerToysProtectedStorage::Maintenance::ReadLocked(authorizedFile.value)))
                throw PowerToysProtectedStorage::failure("requester image differs from authorized Setup", ERROR_ACCESS_DENIED);
            product::write_new_text(stage / L"owner.txt", owner);
            product::write_new_text(stage / L"operation.txt", initial ? L"install" : repair ? L"repair" : verb);
            evidence << L"brokerActor=" << PowerToysProtectedStorage::actor_sid() << L"\nbusinessOwnerSid=" << owner
                << L"\nownerPid=" << ownerPid << L"\nownerCreation=" << ownerBirth << L"\nownerElevated=0\n";
            extract_provisioner();
            try
            {
                if (initial)
                {
                    provisioner(L"install", owner, true, evidence);
                    provisioner(L"commit-install", owner, true, evidence);
                }
                else provisioner(repair ? L"repair-bootstrap" : L"prepare-remove", owner, false, evidence);
                product::write_new(stage / L"prepared", {});
            }
            catch (const unknown_outcome&)
            {
                throw;
            }
            catch (const PowerToysProtectedStorage::failure& error)
            {
                evidence << L"preparationFailure=" << error.code << L"\n" << std::flush;
                if (initial) rollback_initial(owner, evidence);
                else if (!repair)
                {
                    const auto record = product::state_root() / L"Installer" / owner;
                    if (setup::exists(record / L"pending-remove"))
                    {
                        product::assert_system_directory(record);
                        std::wifstream pending(record / L"pending-remove");
                        std::wstring previousState, removalId;
                        if ((pending >> previousState >> removalId) && removalId == transaction)
                        {
                            pending.close();
                            provisioner(L"undo-remove", owner, false, evidence);
                        }
                    }
                }
                throw;
            }
            return;
        }
        if (!setup::exists(stage / L"prepared") || setup::exists(stage / L"finalized"))
            throw PowerToysProtectedStorage::failure("broker phase does not match protected transaction state", ERROR_INVALID_STATE);
        const auto owner = PowerToysProtectedStorage::canonical_owner(read_text(stage / L"owner.txt"));
        const auto operation = read_text(stage / L"operation.txt");
        authorizedVerb = operation;
        if (operation == L"install" && mode == L"finalize-install")
        {
            if (!owns_initial(owner)) throw PowerToysProtectedStorage::failure("initial instance transaction changed", ERROR_INVALID_OWNER);
            setup::verify_owner_registration(owner, true);
        }
        else if (operation == L"install" && mode == L"undo-install") rollback_initial(owner, evidence);
        else if ((operation == L"remove" || operation == L"purge") && (mode == L"commit-remove" || mode == L"undo-remove"))
        {
            if (mode == L"commit-remove") setup::verify_owner_registration(owner, false);
            provisioner(mode, owner, false, evidence);
        }
        else if (operation == L"repair" && mode == L"finalize-repair") {}
        else throw PowerToysProtectedStorage::failure("invalid fixed broker finalization phase", ERROR_BAD_ARGUMENTS);
        product::write_new_text(stage / L"finalized", mode);
    }
    DWORD WINAPI control(DWORD, DWORD, LPVOID, LPVOID) { return NO_ERROR; }
    void WINAPI run(DWORD argc, LPWSTR* argv)
    {
        statusHandle = RegisterServiceCtrlHandlerExW(serviceName.c_str(), control, nullptr);
        if (!statusHandle) return;
        status.dwCurrentState = SERVICE_RUNNING;
        if (!SetServiceStatus(statusHandle, &status)) return;
        DWORD result = ERROR_INSTALL_FAILURE;
        std::wstring mode;
        std::wofstream evidence(stage / L"evidence.txt", std::ios::app);
        try
        {
            PowerToysProtectedStorage::require_system();
            if (!evidence) throw PowerToysProtectedStorage::failure("open protected broker evidence", ERROR_WRITE_FAULT);
            if (argc != 2) throw PowerToysProtectedStorage::failure("one fixed broker startup phase required", ERROR_BAD_ARGUMENTS);
            mode = argv[1];
            if (mode != L"provision" && mode != L"prepare-remove" && mode != L"prepare-purge" && mode != L"repair-bootstrap" &&
                mode != L"finalize-repair" && mode != L"finalize-install" &&
                mode != L"undo-install" && mode != L"commit-remove" && mode != L"undo-remove")
                throw PowerToysProtectedStorage::failure("unsupported broker startup phase", ERROR_BAD_ARGUMENTS);
            evidence << L"phase=" << mode << L"\n" << std::flush;
            phase(mode, evidence);
            result = setup::exists(stage / L"cleanup-pending") ? ERROR_SUCCESS_REBOOT_REQUIRED : ERROR_SUCCESS;
        }
        catch (const PowerToysProtectedStorage::failure& error)
        {
            result = error.code ? error.code : ERROR_INSTALL_FAILURE;
            evidence << L"error=" << error.what() << L" nativeResult=" << result << L"\n";
        }
        catch (const std::exception& error)
        {
            evidence << L"exception=" << error.what() << L"\n";
        }
        evidence << L"final=" << result << L"\n";
        evidence.close();
        if (!mode.empty() && mode.find_first_not_of(L"abcdefghijklmnopqrstuvwxyz-") == std::wstring::npos)
        {
            try { product::write_new_text(stage / (mode + L".result"), std::to_wstring(result)); }
            catch (const PowerToysProtectedStorage::failure& error) { result = error.code ? error.code : ERROR_WRITE_FAULT; }
        }
        status.dwCurrentState = SERVICE_STOPPED;
        status.dwWin32ExitCode = result;
        SetServiceStatus(statusHandle, &status);
    }
}
int wmain(int argc, wchar_t** argv)
{
    try
    {
        PowerToysProtectedStorage::initialize_process_security();
        PowerToysProtectedStorage::require_system();
        if (argc == 2 && std::wstring(argv[1]) == L"--machine-remove")
        {
            stage = std::filesystem::path(product::module_path()).parent_path();
            product::assert_system_directory(stage);
            PowerToysProtectedStorage::Maintenance::VerifySignedFile(product::module_path());
            extract_provisioner();
            return static_cast<int>(setup::fixed_machine_child(stage / L"Provisioner.exe", L"machine-remove --keep-data"));
        }
        if (argc != 5 || std::wstring(argv[1]) != L"--service") return ERROR_BAD_ARGUMENTS;
        const auto pid = product::unsigned_number(argv[2]);
        if (pid > MAXDWORD) return ERROR_BAD_ARGUMENTS;
        ownerPid = static_cast<DWORD>(pid);
        ownerBirth = product::unsigned_number(argv[3]);
        requestNonce = argv[4];
        if (!setup::valid_id(requestNonce)) return ERROR_BAD_ARGUMENTS;
        stage = std::filesystem::path(product::module_path()).parent_path();
        serviceName = stage.filename().wstring();
        const size_t prefixSize = std::size(product::setupPrefix) - 1;
        if (stage.parent_path() != product::stage_root() ||
            serviceName.rfind(product::setupPrefix, 0) != 0 || serviceName.size() != prefixSize + 32)
            throw PowerToysProtectedStorage::failure("unexpected protected initial provisioning stage", ERROR_ACCESS_DENIED);
        transaction = serviceName.substr(prefixSize);
        if (!setup::valid_id(transaction)) throw PowerToysProtectedStorage::failure("invalid provisioning stage identifier", ERROR_ACCESS_DENIED);
        product::assert_system_directory(stage);
        SERVICE_TABLE_ENTRYW table[] = { { serviceName.data(), run }, { nullptr, nullptr } };
        PowerToysProtectedStorage::check(StartServiceCtrlDispatcherW(table) != FALSE, "broker SCM dispatcher");
        return 0;
    }
    catch (const PowerToysProtectedStorage::failure& error)
    {
        std::cerr << "ProtectedStorage provision broker: " << error.what() << " nativeError=" << error.code << "\n";
        return static_cast<int>(error.code ? error.code : ERROR_INSTALL_FAILURE);
    }
}
