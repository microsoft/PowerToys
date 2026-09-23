// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "SetupSupport.h"
#include "MaintenanceContract.h"
#include "StorageInitialization.h"

namespace
{
    struct paths
    {
        std::wstring owner;
        std::wstring va;
        std::filesystem::path code;
        std::filesystem::path data;
        std::filesystem::path policy;
        std::filesystem::path record;
        explicit paths(const std::wstring& sid, bool resolveVa = true) :
            owner(PowerToysProtectedStorage::canonical_owner(sid)), va(resolveVa ? PowerToysProtectedStorage::account_sid(product::service_account(owner)) : L""),
            code(product::owner_code(owner)), data(product::state_root() / L"Data" / owner),
            policy(product::state_root() / L"Policy" / owner), record(product::state_root() / L"Installer" / owner) {}
    };
    bool path_exists(const std::filesystem::path& path)
    {
        const DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes != INVALID_FILE_ATTRIBUTES) return true;
        const DWORD error = GetLastError();
        if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND) throw PowerToysProtectedStorage::failure("GetFileAttributes", error);
        return false;
    }
    void validate_directory(const std::filesystem::path& path, const std::wstring& va = {})
    {
        const DWORD attributes = GetFileAttributesW(path.c_str());
        if (attributes == INVALID_FILE_ATTRIBUTES || !(attributes & FILE_ATTRIBUTE_DIRECTORY) || (attributes & FILE_ATTRIBUTE_REPARSE_POINT))
            throw PowerToysProtectedStorage::failure("unsafe protected directory", ERROR_ACCESS_DENIED);
        PSID owner = nullptr;
        PACL dacl = nullptr;
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PowerToysProtectedStorage::result(GetNamedSecurityInfoW(path.c_str(), SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
            &owner, nullptr, &dacl, nullptr, &descriptor), "GetNamedSecurityInfo");
        struct free_descriptor { PSECURITY_DESCRIPTOR value; ~free_descriptor() { LocalFree(value); } } free{ descriptor };
        const auto ownerSid = PowerToysProtectedStorage::sid_string(owner);
        if (ownerSid != L"S-1-5-18" && (va.empty() || ownerSid != va)) throw PowerToysProtectedStorage::failure("unexpected protected owner", ERROR_ACCESS_DENIED);
        SECURITY_DESCRIPTOR_CONTROL control{};
        DWORD revision = 0;
        PowerToysProtectedStorage::check(GetSecurityDescriptorControl(descriptor, &control, &revision) != FALSE, "descriptor control");
        if (!dacl || !(control & SE_DACL_PROTECTED)) throw PowerToysProtectedStorage::failure("unprotected DACL", ERROR_ACCESS_DENIED);
        constexpr DWORD mutation = FILE_WRITE_DATA | FILE_APPEND_DATA | FILE_WRITE_EA | FILE_WRITE_ATTRIBUTES |
            FILE_DELETE_CHILD | DELETE | WRITE_DAC | WRITE_OWNER | GENERIC_WRITE | GENERIC_ALL;
        for (DWORD index = 0; index < dacl->AceCount; ++index)
        {
            LPVOID raw = nullptr;
            PowerToysProtectedStorage::check(GetAce(dacl, index, &raw) != FALSE, "GetAce");
            const auto* header = static_cast<ACE_HEADER*>(raw);
            if (header->AceType != ACCESS_ALLOWED_ACE_TYPE) throw PowerToysProtectedStorage::failure("unexpected protected ACE", ERROR_ACCESS_DENIED);
            auto* ace = static_cast<ACCESS_ALLOWED_ACE*>(raw);
            const auto trustee = PowerToysProtectedStorage::sid_string(&ace->SidStart);
            if ((ace->Mask & mutation) && trustee != L"S-1-5-18" && (va.empty() || trustee != va))
                throw PowerToysProtectedStorage::failure("unauthorized protected writer", ERROR_ACCESS_DENIED);
        }
    }
    void shared_directory(const std::filesystem::path& path)
    {
        PowerToysProtectedStorage::directory(path, PowerToysProtectedStorage::system_acl());
        validate_directory(path);
    }
    void private_directory(const std::filesystem::path& path, const std::wstring& va)
    {
        if (!path_exists(path)) PowerToysProtectedStorage::directory(path, PowerToysProtectedStorage::private_acl(va));
        validate_directory(path, va);
    }
    void ancestors(const std::wstring& owner)
    {
        shared_directory(product::code_root());
        shared_directory(product::code_root() / L"Owners");
        shared_directory(product::code_root() / L"Owners" / owner);
        shared_directory(product::state_root());
        for (const auto* child : { L"Data", L"Policy", L"Installer" }) shared_directory(product::state_root() / child);
    }
    void validate_layout(const paths& p)
    {
        for (const auto& path : { product::code_root(), product::code_root() / L"Owners", product::code_root() / L"Owners" / p.owner,
            product::state_root(), product::state_root() / L"Data", product::state_root() / L"Policy", product::state_root() / L"Installer", p.record, p.policy })
            validate_directory(path);
        validate_directory(p.code, p.va);
        validate_directory(p.data, p.va);
    }
    void append_log(const paths& p, const std::wstring& action)
    {
        std::wofstream log(p.record / L"lifecycle.log", std::ios::app);
        log << L"action=" << action << L" actor=" << PowerToysProtectedStorage::actor_sid() << L" owner=" << p.owner
            << L" pid=" << GetCurrentProcessId() << L" msi_seed=" << product::version << L"\n";
        if (!log) throw PowerToysProtectedStorage::failure("lifecycle log", ERROR_WRITE_FAULT);
    }
    void check_service(SC_HANDLE service, const std::wstring& owner)
    {
        DWORD needed = 0;
        QueryServiceConfigW(service, nullptr, 0, &needed);
        PowerToysProtectedStorage::check(GetLastError() == ERROR_INSUFFICIENT_BUFFER, "QueryServiceConfig size");
        std::vector<BYTE> data(needed);
        auto* config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(data.data());
        PowerToysProtectedStorage::check(QueryServiceConfigW(service, config, needed, &needed) != FALSE, "QueryServiceConfig");
        if (config->dwServiceType != SERVICE_WIN32_OWN_PROCESS || product::image_path(owner) != config->lpBinaryPathName ||
            _wcsicmp(product::service_account(owner).c_str(), config->lpServiceStartName) != 0)
            throw PowerToysProtectedStorage::failure("service configuration not owned by product", ERROR_ACCESS_DENIED);
    }
    DWORD service_state(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS state{};
        DWORD size = 0;
        PowerToysProtectedStorage::check(QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&state), sizeof(state), &size) != FALSE, "QueryServiceStatus");
        return state.dwCurrentState;
    }
    void wait_state(SC_HANDLE service, DWORD desired)
    {
        const auto deadline = GetTickCount64() + 30000;
        do
        {
            SERVICE_STATUS_PROCESS state{};
            DWORD size = 0;
            PowerToysProtectedStorage::check(QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&state), sizeof(state), &size) != FALSE, "service transition status");
            if (state.dwCurrentState == desired) return;
            if (desired == SERVICE_RUNNING && state.dwCurrentState == SERVICE_STOPPED)
                throw PowerToysProtectedStorage::failure("service stopped during startup", state.dwWin32ExitCode ? state.dwWin32ExitCode : ERROR_SERVICE_NOT_ACTIVE);
            Sleep(100);
        } while (GetTickCount64() < deadline);
        throw PowerToysProtectedStorage::failure("bounded service transition timeout", WAIT_TIMEOUT);
    }
    void stop_service(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS before{};
        DWORD bytes = 0;
        PowerToysProtectedStorage::check(QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&before), sizeof(before), &bytes) != FALSE, "service before stop");
        if (before.dwCurrentState == SERVICE_STOPPED) return;
        PowerToysProtectedStorage::handle process;
        if (before.dwProcessId)
        {
            process.value = OpenProcess(SYNCHRONIZE, FALSE, before.dwProcessId);
            PowerToysProtectedStorage::check(process.value != nullptr, "hold current SCM process");
            SERVICE_STATUS_PROCESS confirmed{};
            PowerToysProtectedStorage::check(QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&confirmed), sizeof(confirmed), &bytes) != FALSE, "confirm SCM process");
            if (confirmed.dwProcessId != before.dwProcessId) throw PowerToysProtectedStorage::failure("service process changed before maintenance", ERROR_BUSY);
        }
        SERVICE_STATUS status{};
        if (!ControlService(service, SERVICE_CONTROL_STOP, &status) && GetLastError() != ERROR_SERVICE_NOT_ACTIVE)
            throw PowerToysProtectedStorage::failure("ControlService", GetLastError());
        wait_state(service, SERVICE_STOPPED);
        if (process.value && WaitForSingleObject(process.value, 30000) != WAIT_OBJECT_0)
            throw PowerToysProtectedStorage::failure("stopped service process has not exited", WAIT_TIMEOUT);
    }
    void start_service(SC_HANDLE service)
    {
        if (service_state(service) == SERVICE_RUNNING) return;
        PowerToysProtectedStorage::check(StartServiceW(service, 0, nullptr) != FALSE, "StartService");
        wait_state(service, SERVICE_RUNNING);
    }
    void set_service_acl(SC_HANDLE service, const paths& p)
    {
        const auto text = L"D:P(A;;GA;;;SY)(A;;0x00020035;;;" + p.va + L")(A;;0x00020005;;;" + p.owner + L")";
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PowerToysProtectedStorage::check(ConvertStringSecurityDescriptorToSecurityDescriptorW(text.c_str(), SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "service SDDL");
        const BOOL changed = SetServiceObjectSecurity(service, DACL_SECURITY_INFORMATION, descriptor);
        const DWORD error = GetLastError();
        LocalFree(descriptor);
        if (!changed) throw PowerToysProtectedStorage::failure("SetServiceObjectSecurity", error);
    }
    void acquire_lock(const paths& p, PowerToysProtectedStorage::handle& lock)
    {
        const auto deadline = GetTickCount64() + 30000;
        do
        {
            lock.value = CreateFileW((p.record / L"maintenance.lock").c_str(), GENERIC_READ | READ_CONTROL, 0, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr);
            if (lock.value != INVALID_HANDLE_VALUE)
            {
                FILE_ATTRIBUTE_TAG_INFO attributes{};
                PowerToysProtectedStorage::check(GetFileInformationByHandleEx(lock.value, FileAttributeTagInfo, &attributes, sizeof(attributes)) != FALSE, "lock attributes");
                if (attributes.FileAttributes & (FILE_ATTRIBUTE_DIRECTORY | FILE_ATTRIBUTE_REPARSE_POINT))
                    throw PowerToysProtectedStorage::failure("invalid runtime maintenance lock", ERROR_ACCESS_DENIED);
                BY_HANDLE_FILE_INFORMATION information{};
                PowerToysProtectedStorage::check(GetFileInformationByHandle(lock.value, &information) != FALSE, "maintenance lock identity");
                if (information.nNumberOfLinks != 1 || information.nFileSizeHigh || information.nFileSizeLow)
                    throw PowerToysProtectedStorage::failure("maintenance lock is not empty/single-link", ERROR_ACCESS_DENIED);
                PSECURITY_DESCRIPTOR descriptor = nullptr;
                PSID owner = nullptr;
                PACL acl = nullptr;
                PowerToysProtectedStorage::result(GetSecurityInfo(lock.value, SE_FILE_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
                    &owner, nullptr, &acl, nullptr, &descriptor), "maintenance lock security");
                struct release { PSECURITY_DESCRIPTOR value; ~release() { LocalFree(value); } } free{ descriptor };
                SECURITY_DESCRIPTOR_CONTROL control{};
                DWORD revision = 0;
                PowerToysProtectedStorage::check(GetSecurityDescriptorControl(descriptor, &control, &revision) != FALSE, "maintenance lock control");
                if (PowerToysProtectedStorage::sid_string(owner) != L"S-1-5-18" || !(control & SE_DACL_PROTECTED) || !acl || acl->AceCount != 2)
                    throw PowerToysProtectedStorage::failure("maintenance lock ownership/DACL", ERROR_ACCESS_DENIED);
                const auto expectedVa = p.va.empty() ? PowerToysProtectedStorage::account_sid(product::service_account(p.owner)) : p.va;
                bool system = false, va = false;
                for (DWORD index = 0; index < acl->AceCount; ++index)
                {
                    LPVOID raw = nullptr;
                    PowerToysProtectedStorage::check(GetAce(acl, index, &raw) != FALSE, "maintenance lock ACE");
                    auto* ace = static_cast<ACCESS_ALLOWED_ACE*>(raw);
                    if (ace->Header.AceType != ACCESS_ALLOWED_ACE_TYPE || ace->Header.AceFlags)
                        throw PowerToysProtectedStorage::failure("maintenance lock unexpected ACE", ERROR_ACCESS_DENIED);
                    const auto sid = PowerToysProtectedStorage::sid_string(&ace->SidStart);
                    if (sid == L"S-1-5-18" && ace->Mask == FILE_ALL_ACCESS) system = true;
                    else if (sid == expectedVa && ace->Mask == FILE_GENERIC_READ) va = true;
                    else throw PowerToysProtectedStorage::failure("maintenance lock has unexpected authority", ERROR_ACCESS_DENIED);
                }
                if (!system || !va) throw PowerToysProtectedStorage::failure("maintenance lock lacks exact SYSTEM/VA roles", ERROR_ACCESS_DENIED);
                return;
            }
            if (GetLastError() != ERROR_SHARING_VIOLATION) throw PowerToysProtectedStorage::failure("open existing maintenance lock", GetLastError());
            Sleep(100);
        } while (GetTickCount64() < deadline);
        throw PowerToysProtectedStorage::failure("runtime update still holds maintenance lock", WAIT_TIMEOUT);
    }
    void delete_file(const std::filesystem::path& path)
    {
        if (!DeleteFileW(path.c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
            throw PowerToysProtectedStorage::failure("DeleteFile", GetLastError());
    }
    void delete_tree(const std::filesystem::path& path, unsigned depth = 0)
    {
        if (!path_exists(path)) return;
        if (depth > 32) throw PowerToysProtectedStorage::failure("unexpected private tree depth", ERROR_DIRECTORY);
        PowerToysProtectedStorage::handle object(CreateFileW(path.c_str(), DELETE | FILE_READ_ATTRIBUTES, FILE_SHARE_READ | FILE_SHARE_WRITE,
            nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT | FILE_FLAG_BACKUP_SEMANTICS, nullptr));
        PowerToysProtectedStorage::check(object.value != INVALID_HANDLE_VALUE, "open exact removal object");
        FILE_ATTRIBUTE_TAG_INFO attributes{};
        PowerToysProtectedStorage::check(GetFileInformationByHandleEx(object.value, FileAttributeTagInfo, &attributes, sizeof(attributes)) != FALSE, "removal attributes");
        if ((attributes.FileAttributes & FILE_ATTRIBUTE_DIRECTORY) && !(attributes.FileAttributes & FILE_ATTRIBUTE_REPARSE_POINT))
        {
            WIN32_FIND_DATAW found{};
            HANDLE find = FindFirstFileW((path / L"*").c_str(), &found);
            if (find == INVALID_HANDLE_VALUE)
            {
                if (GetLastError() != ERROR_FILE_NOT_FOUND) throw PowerToysProtectedStorage::failure("enumerate exact removal directory", GetLastError());
            }
            else
            {
                struct close_find { HANDLE value; ~close_find() { FindClose(value); } } close{ find };
                do
                {
                    if (wcscmp(found.cFileName, L".") && wcscmp(found.cFileName, L".."))
                        delete_tree(path / found.cFileName, depth + 1);
                } while (FindNextFileW(find, &found));
                if (GetLastError() != ERROR_NO_MORE_FILES) throw PowerToysProtectedStorage::failure("FindNextFile", GetLastError());
            }
        }
        FILE_DISPOSITION_INFO disposition{ TRUE };
        PowerToysProtectedStorage::check(SetFileInformationByHandle(object.value, FileDispositionInfo, &disposition, sizeof(disposition)) != FALSE, "delete exact object by handle");
    }
    void remove_empty(const std::filesystem::path& path)
    {
        if (!RemoveDirectoryW(path.c_str()))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_DIR_NOT_EMPTY && error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
                throw PowerToysProtectedStorage::failure("remove empty shared directory", error);
        }
    }
    void erase_owner(const paths& p, PowerToysProtectedStorage::handle* heldLock = nullptr, bool purge = false)
    {
        delete_file(p.record / L"purge-recreation.ticket");
        const auto plan = PowerToysProtectedStorage::Maintenance::PlanRemoval(purge ?
            PowerToysProtectedStorage::Maintenance::RemovalMode::PurgeData : PowerToysProtectedStorage::Maintenance::RemovalMode::KeepData);
        const auto virtualSid = p.va.empty() ? PowerToysProtectedStorage::account_sid(product::service_account(p.owner)) : p.va;
        PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        PowerToysProtectedStorage::check(scm.value != nullptr, "SCM connect");
        {
            PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(p.owner).c_str(),
                SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
            if (service.value)
            {
                check_service(service.value, p.owner);
                stop_service(service.value);
                PowerToysProtectedStorage::check(DeleteService(service.value) != FALSE, "DeleteService");
            }
            else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST) throw PowerToysProtectedStorage::failure("OpenService removal", GetLastError());
        }
        for (const auto& ancestor : { product::code_root(), product::code_root() / L"Owners", p.code.parent_path() })
            if (path_exists(ancestor)) validate_directory(ancestor);
        delete_tree(p.code);
        remove_empty(p.code.parent_path());
        if (plan.removeData) delete_tree(p.data);
        if (heldLock && heldLock->value && heldLock->value != INVALID_HANDLE_VALUE)
        {
            CloseHandle(heldLock->value);
            heldLock->value = nullptr;
        }
        for (const auto* leaf : { L"active", L"pending-install", L"pending-remove", L"pending-repair", L"drain", L"service-created" })
            delete_file(p.record / leaf);
        const auto tombstone = p.record / (purge ? L"purged" : L"retained");
        if (!path_exists(tombstone))
            product::write_new_text(tombstone, L"format=1\nowner=" + p.owner + L"\nmode=" + (purge ? L"purge\n" : L"keep\n"));
        if (path_exists(p.record / L"minimum-reinstall-release"))
        {
            std::wifstream minimumFile(p.record / L"minimum-reinstall-release");
            std::wstring minimum;
            if (!(minimumFile >> minimum))
                throw PowerToysProtectedStorage::failure("retained release floor is unreadable", ERROR_INVALID_DATA);
            minimumFile.close();
            if (PowerToysProtectedStorage::Maintenance::ReleaseNumber(minimum) <
                PowerToysProtectedStorage::Maintenance::ReleaseNumber(product::version))
                delete_file(p.record / L"minimum-reinstall-release");
        }
        if (!path_exists(p.record / L"minimum-reinstall-release"))
            product::write_new_text(p.record / L"minimum-reinstall-release", product::version);
        if (purge)
        {
            delete_file(p.record / L"retained");
            product::write_new_text(p.record / L"purge-recreation.ticket", L"format=1\nowner=" + p.owner + L"\nmode=purge\n");
        }
        // VA profiles may still have loaded hives/worker handles. Never delete a
        // real user's profile or force-unload any hive to make cleanup look done.
        HKEY profile = nullptr;
        const auto profileKey = L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" +
            virtualSid;
        const LSTATUS profileStatus = RegOpenKeyExW(HKEY_LOCAL_MACHINE, profileKey.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &profile);
        if (profile) RegCloseKey(profile);
        if (profileStatus != ERROR_FILE_NOT_FOUND && !path_exists(p.record / L"cleanup-pending"))
            product::write_new_text(p.record / L"cleanup-pending", L"va-profile-review\n");
        remove_empty(product::code_root() / L"Owners");
        remove_empty(product::code_root());
        for (const auto* child : { L"Data", L"Policy", L"Installer" }) remove_empty(product::state_root() / child);
        remove_empty(product::state_root());
    }
    void maintain(const paths& p, bool removing, const std::wstring& transaction, bool purge = false)
    {
        if (removing && !setup::valid_id(transaction)) throw PowerToysProtectedStorage::failure("removal transaction required", ERROR_BAD_ARGUMENTS);
        validate_layout(p);
        PowerToysProtectedStorage::handle lock;
        acquire_lock(p, lock);
        if (path_exists(p.record / L"drain")) throw PowerToysProtectedStorage::failure("previous maintenance needs recovery", ERROR_INSTALL_ALREADY_RUNNING);
        PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        PowerToysProtectedStorage::check(scm.value != nullptr, "SCM connect");
        PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(p.owner).c_str(),
            SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP));
        PowerToysProtectedStorage::check(service.value != nullptr, "OpenService maintenance");
        check_service(service.value, p.owner);
        const bool running = service_state(service.value) != SERVICE_STOPPED;
        product::write_new_text(p.record / (removing ? L"pending-remove" : L"pending-repair"),
            std::wstring(running ? L"running\n" : L"stopped\n") + (removing ? transaction + L"\n" + (purge ? L"purge\n" : L"keep\n") : L""));
        if (removing)
        {
            delete_file(p.record / L"minimum-reinstall-release");
            product::write_new_text(p.record / L"minimum-reinstall-release", product::version);
        }
        product::write_new(p.record / L"drain", {});
        stop_service(service.value);
        append_log(p, removing ? L"prepare-remove" : L"repair-preserve-live-code-and-data");
        if (!removing)
        {
            delete_file(p.record / L"drain");
            if (running) start_service(service.value);
            delete_file(p.record / L"pending-repair");
        }
    }
    void install(const std::wstring& owner, const std::wstring& transaction)
    {
        if (!setup::valid_id(transaction)) throw PowerToysProtectedStorage::failure("initial provisioning transaction required", ERROR_BAD_ARGUMENTS);
        setup::require_profile(owner);
        ancestors(owner);
        const auto record = product::state_root() / L"Installer" / owner;
        paths absent(owner, false);
        const bool retained = path_exists(record / L"retained") || path_exists(record / L"purged");
        const bool dataExisted = path_exists(absent.data);
        if (retained)
        {
            validate_directory(record);
            std::wifstream receipt(record / (path_exists(record / L"purged") ? L"purged" : L"retained"));
            std::wstring format, recordedOwner;
            if (!std::getline(receipt, format) || !std::getline(receipt, recordedOwner) ||
                format != L"format=1" || recordedOwner != L"owner=" + owner || path_exists(record / L"active") ||
                path_exists(record / L"pending-install") || path_exists(absent.code))
                throw PowerToysProtectedStorage::failure("retained inventory mismatch", ERROR_INVALID_OWNER);
            std::wifstream minimumFile(record / L"minimum-reinstall-release");
            std::wstring minimum;
            if (!(minimumFile >> minimum) ||
                PowerToysProtectedStorage::Maintenance::ReleaseNumber(minimum) >
                    PowerToysProtectedStorage::Maintenance::ReleaseNumber(product::version))
                throw PowerToysProtectedStorage::failure("retained store requires matching or newer signed release", ERROR_PRODUCT_VERSION);
        }
        else for (const auto& path : { record, absent.code, absent.data, absent.policy })
            if (path_exists(path)) throw PowerToysProtectedStorage::failure("preexisting instance requires explicit recovery", ERROR_INVALID_STATE);
        const bool purgeRecreation = retained && path_exists(record / L"purged") &&
            PowerToysProtectedStorage::Provisioning::HasPurgeRecreationTicket(record, owner);
        const auto indexPlan = PowerToysProtectedStorage::Provisioning::PlanIndex(dataExisted,
            path_exists(record / L"retained"), retained, purgeRecreation);
        if (dataExisted)
            PowerToysProtectedStorage::Provisioning::EnsureIndex(absent.data, indexPlan);
        PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE | SC_MANAGER_CONNECT));
        PowerToysProtectedStorage::check(scm.value != nullptr, "SCM create");
        {
            PowerToysProtectedStorage::service existing(OpenServiceW(scm.value, product::service_name(owner).c_str(), SERVICE_QUERY_STATUS));
            if (existing.value) throw PowerToysProtectedStorage::failure("service already exists without product record", ERROR_SERVICE_EXISTS);
            if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST) throw PowerToysProtectedStorage::failure("preflight service absence", GetLastError());
        }
        if (!retained) setup::new_directory(record, PowerToysProtectedStorage::system_acl());
        else
        {
            delete_file(record / L"provisioning-id");
            delete_file(record / L"service-created");
            delete_file(record / L"seed.txt");
            if (!path_exists(record / L"reattached"))
                product::write_new(record / L"reattached", {});
        }
        product::write_new_text(record / L"provisioning-id", transaction);
        product::write_new(record / L"pending-install", {});
        PowerToysProtectedStorage::service service(CreateServiceW(scm.value, product::service_name(owner).c_str(), product::service_name(owner).c_str(),
            SERVICE_ALL_ACCESS, SERVICE_WIN32_OWN_PROCESS, SERVICE_DEMAND_START, SERVICE_ERROR_NORMAL,
            product::image_path(owner).c_str(), nullptr, nullptr, nullptr, product::service_account(owner).c_str(), nullptr));
        PowerToysProtectedStorage::check(service.value != nullptr, "CreateService");
        try { product::write_new(record / L"service-created", {}); }
        catch (...)
        {
            if (!DeleteService(service.value)) std::cerr << "new service cleanup nativeError=" << GetLastError() << "\n";
            throw;
        }
        paths p(owner);
        set_service_acl(service.value, p);
        private_directory(p.code, p.va);
        delete_file(p.record / L"purge-recreation.ticket");
        private_directory(p.data, p.va);
        PowerToysProtectedStorage::Provisioning::EnsureIndex(p.data, indexPlan);
        shared_directory(p.policy);
        if (!path_exists(p.record / L"maintenance.lock"))
            product::write_new(p.record / L"maintenance.lock", {}, L"O:SYG:SYD:P(A;;FA;;;SY)(A;;FR;;;" + p.va + L")");
        product::write_new(p.record / L"drain", {});
        if (!path_exists(p.policy / L"policy.txt"))
            product::write_new(p.policy / L"policy.txt", product::resource(103), PowerToysProtectedStorage::system_acl());
        product::write_new(p.code / L"Bootstrap.exe", product::resource(101));
        product::write_new(p.code / L"Runtime.exe", product::resource(102));
        for (const auto& [id, leaf] : std::vector<std::pair<WORD, const wchar_t*>>{
            {104, L"manifest.txt"}, {105, L"manifest.p7s"}, {106, L"ClientCatalog.json"}, {107, L"ClientCatalog.p7s"}})
            product::write_new(p.code / leaf, product::resource(id));
        product::write_new_text(record / L"seed.txt", L"owner=" + owner + L"\nproduct=" + product::code + L"\nmsi_seed=" + product::version +
            L"\nlive_seed=" + product::liveSeedVersion + L"\ninitial_actor=" + PowerToysProtectedStorage::actor_sid() + L"\nva=" + p.va + L"\nimage=" + product::image_path(owner) + L"\n");
        append_log(p, retained ? L"reattach-preserving-initialization" : L"initial-code-seed");
        check_service(service.value, owner);
        start_service(service.value);
    }
    void lifecycle(const std::wstring& operation, const std::wstring& owner, const std::wstring& transaction, bool initialRemoval, bool purge)
    {
        PowerToysProtectedStorage::require_system();
        if (operation == L"install") { install(owner, transaction); return; }
        paths p(owner, operation != L"undo-install");
        if (operation == L"commit-install" || operation == L"undo-install" || initialRemoval)
        {
            if (!setup::valid_id(transaction)) throw PowerToysProtectedStorage::failure("provisioning transaction required", ERROR_BAD_ARGUMENTS);
            if (operation == L"undo-install" && !path_exists(p.record)) return;
            validate_directory(p.record);
            std::wifstream receipt(p.record / L"provisioning-id");
            std::wstring recorded;
            if (!(receipt >> recorded) || recorded != transaction)
                throw PowerToysProtectedStorage::failure("refusing cleanup of another provisioning transaction", ERROR_INVALID_OWNER);
        }
        if (operation == L"undo-install")
        {
            if (!path_exists(p.record / L"active"))
            {
                if (!path_exists(p.record / L"service-created"))
                {
                    // CreateService never completed: no existing service or private code belongs to us.
                    if (!path_exists(p.record / L"reattached")) delete_tree(p.record);
                    else
                    {
                        delete_file(p.record / L"pending-install");
                        delete_file(p.record / L"provisioning-id");
                    }
                    return;
                }
                PowerToysProtectedStorage::handle lock;
                if (path_exists(p.record / L"maintenance.lock")) acquire_lock(p, lock);
                erase_owner(p, &lock);
            }
        }
        else if (operation == L"commit-install")
        {
            if (path_exists(p.record / L"pending-install"))
            {
                validate_layout(p);
                PowerToysProtectedStorage::handle lock;
                acquire_lock(p, lock);
                append_log(p, L"commit-install");
                PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
                PowerToysProtectedStorage::check(scm.value != nullptr, "SCM commit install");
                PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(owner).c_str(),
                    SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG));
                PowerToysProtectedStorage::check(service.value != nullptr, "OpenService commit install");
                check_service(service.value, owner);
                PowerToysProtectedStorage::check(ChangeServiceConfigW(service.value, SERVICE_NO_CHANGE, SERVICE_AUTO_START, SERVICE_NO_CHANGE,
                    nullptr, nullptr, nullptr, nullptr, nullptr, nullptr, nullptr) != FALSE, "enable provisioned VA automatic startup");
                delete_file(p.record / L"drain");
                // Publish only after every fallible preparation succeeds; rollback owns pending state.
                if (!MoveFileExW((p.record / L"pending-install").c_str(), (p.record / L"active").c_str(), MOVEFILE_WRITE_THROUGH))
                {
                    const DWORD error = GetLastError();
                    try { product::write_new(p.record / L"drain", {}); }
                    catch (const PowerToysProtectedStorage::failure& restore)
                    {
                        const auto detail = "promotion failed nativeError=" + std::to_string(error) + "; drain restoration failed";
                        throw PowerToysProtectedStorage::failure(detail.c_str(), restore.code);
                    }
                    throw PowerToysProtectedStorage::failure("promote pending installation atomically; drain restored under lease", error);
                }
                delete_file(p.record / L"retained");
                delete_file(p.record / L"reattached");
            }
        }
        else if (operation == L"prepare-remove") maintain(p, true, transaction, purge);
        else if (operation == L"repair-bootstrap")
        {
            validate_layout(p);
            PowerToysProtectedStorage::handle lock;
            acquire_lock(p, lock);
            if (path_exists(p.record / L"pending-remove") || path_exists(p.record / L"pending-install"))
                throw PowerToysProtectedStorage::failure("cannot repair over unresolved lifecycle operation", ERROR_INVALID_STATE);
            PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
            PowerToysProtectedStorage::check(scm.value != nullptr, "repair SCM");
            PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(owner).c_str(),
                SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP));
            PowerToysProtectedStorage::check(service.value != nullptr, "repair fixed service");
            check_service(service.value, owner);
            if (!path_exists(p.record / L"drain")) product::write_new(p.record / L"drain", {});
            stop_service(service.value);
            for (const auto& [id, leaf] : std::vector<std::pair<WORD, const wchar_t*>>{
                {101, L"Bootstrap.exe"}, {102, L"Runtime.exe"}, {104, L"manifest.txt"},
                {105, L"manifest.p7s"}, {106, L"ClientCatalog.json"}, {107, L"ClientCatalog.p7s"}})
            {
                delete_file(p.code / leaf);
                product::write_new(p.code / leaf, product::resource(id));
            }
            delete_file(p.record / L"drain");
            start_service(service.value);
            append_log(p, L"authorized-bootstrap-repair");
        }
        else if (operation == L"undo-remove" || operation == L"undo-repair")
        {
            validate_layout(p);
            const auto pendingPath = p.record / (operation == L"undo-remove" ? L"pending-remove" : L"pending-repair");
            if (path_exists(pendingPath))
            {
                PowerToysProtectedStorage::handle lock;
                acquire_lock(p, lock);
                std::wifstream pending(pendingPath);
                std::wstring state;
                std::getline(pending, state);
                if (operation == L"undo-remove")
                {
                    std::wstring removalId;
                    if (!setup::valid_id(transaction) || !(pending >> removalId) || removalId != transaction)
                        throw PowerToysProtectedStorage::failure("refusing to undo another removal transaction", ERROR_INVALID_OWNER);
                }
                pending.close();
                delete_file(p.record / L"drain");
                if (state == L"running")
                {
                    PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
                    PowerToysProtectedStorage::check(scm.value != nullptr, "SCM rollback");
                    PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(owner).c_str(), SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS | SERVICE_START));
                    PowerToysProtectedStorage::check(service.value != nullptr, "OpenService rollback");
                    check_service(service.value, owner);
                    start_service(service.value);
                }
                delete_file(pendingPath);
            }
        }
        else if (operation == L"commit-remove")
        {
            if (!path_exists(p.record / L"pending-remove") || !path_exists(p.record / L"drain"))
                throw PowerToysProtectedStorage::failure("uninstall was not drained", ERROR_INVALID_STATE);
            validate_layout(p);
            PowerToysProtectedStorage::handle lock;
            acquire_lock(p, lock);
            std::wifstream pending(p.record / L"pending-remove");
            std::wstring previousState, removalId, mode;
            if (!(pending >> previousState >> removalId >> mode))
                throw PowerToysProtectedStorage::failure("refusing to commit another removal transaction", ERROR_INVALID_OWNER);
            PowerToysProtectedStorage::Maintenance::ValidateRemovalReceipt(transaction,
                purge ? PowerToysProtectedStorage::Maintenance::RemovalMode::PurgeData :
                PowerToysProtectedStorage::Maintenance::RemovalMode::KeepData, removalId, mode);
            pending.close();
            append_log(p, L"commit-remove");
            erase_owner(p, &lock, purge);
        }
        else throw PowerToysProtectedStorage::failure("unsupported product lifecycle verb", ERROR_BAD_ARGUMENTS);
    }

    DWORD machine_remove()
    {
        using namespace PowerToysProtectedStorage::Maintenance;
        const auto inventory = product::state_root() / L"Installer";
        shared_directory(product::state_root());
        shared_directory(inventory);
        const auto reports = inventory / L"MachineRemovalReports";
        shared_directory(reports);
        const auto operation = setup::unique_id();
        const auto reportPath = reports / (operation + L".json");
        std::wstring report = L"{\"operationId\":\"" + operation + L"\",\"mode\":\"KeepData\",\"reportPath\":\"" +
            JsonEscape(reportPath.wstring()) + L"\",\"mainProductRemoved\":false,\"perOwner\":[";
        bool first = true;
        bool residual = false;
        const auto deadline = GetTickCount64() + 180000;
        if (path_exists(inventory))
        {
            validate_directory(product::state_root());
            validate_directory(inventory);
            for (const auto& entry : std::filesystem::directory_iterator(inventory))
            {
                const auto sid = entry.path().filename().wstring();
                if (sid == L"Staging" || sid == L"MachineRemovalReports") continue;
                MachineOwnerResult ownerResult;
                ownerResult.owner = sid;
                if (GetTickCount64() >= deadline)
                {
                    ownerResult.nativeErrors.push_back(ERROR_TIMEOUT);
                    if (!first) report += L",";
                    first = false;
                    report += ownerResult.Json();
                    residual = true;
                    continue;
                }
                std::wstring virtualSid;
                try
                {
                    paths p(PowerToysProtectedStorage::canonical_owner(sid), false);
                    validate_directory(p.record);
                    if (GetTickCount64() >= deadline)
                        throw PowerToysProtectedStorage::failure("machine cleanup budget exhausted", ERROR_TIMEOUT);
                    PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
                    PowerToysProtectedStorage::check(scm.value != nullptr, "machine cleanup SCM");
                    PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(sid).c_str(),
                        SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
                    if (service.value) check_service(service.value, sid);
                    else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
                        throw PowerToysProtectedStorage::failure("machine cleanup service inspection", GetLastError());
                    if (service.value || path_exists(p.code))
                    {
                        p.va = PowerToysProtectedStorage::account_sid(product::service_account(p.owner));
                        virtualSid = p.va;
                        PowerToysProtectedStorage::handle lock;
                        acquire_lock(p, lock);
                        if (!path_exists(p.record / L"drain")) product::write_new(p.record / L"drain", {});
                        erase_owner(p, &lock);
                    }
                }
                catch (const PowerToysProtectedStorage::failure& failure)
                {
                    ownerResult.nativeErrors.push_back(failure.code);
                }
                catch (const std::exception&)
                {
                    ownerResult.nativeErrors.push_back(ERROR_INSTALL_FAILURE);
                }
                // Observe each resource after the attempt. A code-removal failure
                // must not be reported as a still-running service (or vice versa).
                try
                {
                    paths p(PowerToysProtectedStorage::canonical_owner(sid), false);
                    validate_directory(p.record);
                    PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
                    PowerToysProtectedStorage::check(scm.value != nullptr, "machine report SCM");
                    PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(sid).c_str(),
                        SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
                    if (!service.value)
                    {
                        if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
                            throw PowerToysProtectedStorage::failure("machine service residual", GetLastError());
                        ownerResult.serviceRemoved = true;
                        ownerResult.serviceStopped = true;
                    }
                    else
                    {
                        check_service(service.value, sid);
                        ownerResult.serviceStopped = service_state(service.value) == SERVICE_STOPPED;
                    }
                    ownerResult.codeRemoved = !path_exists(p.code);
                }
                catch (const PowerToysProtectedStorage::failure& failure)
                {
                    ownerResult.nativeErrors.push_back(failure.code);
                }
                try
                {
                    setup::verify_owner_registration(PowerToysProtectedStorage::canonical_owner(sid), false);
                    ownerResult.msiRegistrationRemoved = true;
                }
                catch (const PowerToysProtectedStorage::failure& failure)
                {
                    if (failure.code != ERROR_INVALID_STATE)
                        ownerResult.nativeErrors.push_back(failure.code);
                }
                try
                {
                    if (virtualSid.empty())
                        virtualSid = PowerToysProtectedStorage::account_sid(product::service_account(PowerToysProtectedStorage::canonical_owner(sid)));
                    HKEY profile = nullptr;
                    const auto key = L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + virtualSid;
                    const auto status = RegOpenKeyExW(HKEY_LOCAL_MACHINE, key.c_str(), 0, KEY_READ | KEY_WOW64_64KEY, &profile);
                    if (profile) RegCloseKey(profile);
                    ownerResult.profileRemoved = status == ERROR_FILE_NOT_FOUND;
                    if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND)
                        ownerResult.nativeErrors.push_back(static_cast<DWORD>(status));
                }
                catch (const PowerToysProtectedStorage::failure& failure)
                {
                    ownerResult.nativeErrors.push_back(failure.code);
                }
                if (!first) report += L",";
                first = false;
                report += ownerResult.Json();
                residual = residual || !ownerResult.Complete();
            }
        }
        report += L"],\"completeCleanup\":" + std::wstring(residual ? L"false" : L"true") + L"}\n";
        const std::string bytes = PowerToysProtectedStorage::ToUtf8(report);
        product::write_new(reportPath, std::vector<BYTE>(bytes.begin(), bytes.end()));
        const auto pendingLatest = reports / (operation + L".pending");
        product::write_new(pendingLatest, std::vector<BYTE>(bytes.begin(), bytes.end()));
        PowerToysProtectedStorage::check(MoveFileExW(pendingLatest.c_str(), (reports / L"latest.json").c_str(),
            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH) != FALSE, "publish durable machine removal report");
        DWORD written = 0;
        WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr);
        // A completed best-effort pass is successful even when offline-owner or
        // profile residuals remain. The report, not a forced reboot, describes them.
        return ERROR_SUCCESS;
    }
}
int wmain(int argc, wchar_t** argv)
{
    try
    {
        PowerToysProtectedStorage::initialize_process_security();
        PowerToysProtectedStorage::require_system();
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(product::module_path());
        if (argc >= 2 && std::wstring(argv[1]) == L"machine-remove")
        {
            PowerToysProtectedStorage::Maintenance::RequireMachineKeepData(std::vector<std::wstring>(argv + 1, argv + argc));
            return static_cast<int>(machine_remove());
        }
        if (argc != 4 && argc != 5) throw PowerToysProtectedStorage::failure("lifecycle arguments: verb ownerSid transactionId [initial]", ERROR_BAD_ARGUMENTS);
        const bool initialRemoval = argc == 5 && std::wstring(argv[4]) == L"initial";
        const bool purge = argc == 5 && std::wstring(argv[4]) == L"purge";
        if (argc == 5 && !initialRemoval && !purge)
            throw PowerToysProtectedStorage::failure("unsupported lifecycle mode", ERROR_BAD_ARGUMENTS);
        if (initialRemoval && std::wstring(argv[1]) != L"prepare-remove")
            throw PowerToysProtectedStorage::failure("initial cleanup guard is valid only for prepare-remove", ERROR_BAD_ARGUMENTS);
        lifecycle(argv[1], PowerToysProtectedStorage::canonical_owner(argv[2]), argv[3], initialRemoval, purge);
        return std::wstring(argv[1]) == L"commit-remove" &&
            path_exists(product::state_root() / L"Installer" / argv[2] / L"cleanup-pending") ?
            ERROR_SUCCESS_REBOOT_REQUIRED : ERROR_SUCCESS;
    }
    catch (const PowerToysProtectedStorage::failure& error)
    {
        std::cerr << "ProtectedStorage lifecycle: " << error.what() << " nativeError=" << error.code << "\n";
        const auto message = L"ProtectedStorage lifecycle operation=" + std::wstring(argc > 1 ? argv[1] : L"invalid") +
            L" owner=" + std::wstring(argc > 2 ? argv[2] : L"invalid") + L" failed=" +
            std::wstring(error.what(), error.what() + strlen(error.what())) + L" nativeError=" + std::to_wstring(error.code);
        HANDLE source = RegisterEventSourceW(nullptr, product::name);
        if (source)
        {
            LPCWSTR strings[] = { message.c_str() };
            if (!ReportEventW(source, EVENTLOG_ERROR_TYPE, 0, 1, nullptr, 1, 0, strings, nullptr))
                std::cerr << "Lifecycle diagnostic event failed: " << GetLastError() << "\n";
            DeregisterEventSource(source);
        }
        else std::cerr << "Lifecycle diagnostic source failed: " << GetLastError() << "\n";
        return static_cast<int>(error.code ? error.code : ERROR_INSTALL_FAILURE);
    }
    catch (const std::filesystem::filesystem_error& error)
    {
        std::cerr << "ProtectedStorage lifecycle filesystem: " << error.what() << "\n";
        return ERROR_INSTALL_FAILURE;
    }
}
