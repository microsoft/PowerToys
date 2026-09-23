// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
#include "SetupSupport.h"
#include "MaintenanceContract.h"
#include <shellapi.h>

namespace
{
    bool maintenanceCleanupPending = false;
    std::wstring observedProductCode;
    std::wstring observedVersion;
    std::wstring syncState;
    struct registration
    {
        std::wstring code;
        std::wstring version;
        unsigned long long release;
    };
    std::wstring product_info(const wchar_t* code, const wchar_t* property)
    {
        DWORD size = 0;
        wchar_t empty = 0;
        UINT result = MsiGetProductInfoExW(code, nullptr, MSIINSTALLCONTEXT_USERUNMANAGED, property, &empty, &size);
        if (result != ERROR_MORE_DATA && result != ERROR_SUCCESS) PowerToysProtectedStorage::result(result, "registered product property size");
        std::vector<wchar_t> text(static_cast<size_t>(size) + 1);
        ++size;
        PowerToysProtectedStorage::result(MsiGetProductInfoExW(code, nullptr, MSIINSTALLCONTEXT_USERUNMANAGED, property, text.data(), &size), "registered product property");
        return text.data();
    }
    std::vector<registration> installed()
    {
        const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::actor_sid());
        std::vector<registration> answer;
        for (DWORD index = 0;; ++index)
        {
            wchar_t candidate[39]{};
            const UINT related = MsiEnumRelatedProductsW(product::upgradeCode, 0, index, candidate);
            if (related == ERROR_NO_MORE_ITEMS) break;
            PowerToysProtectedStorage::result(related, "enumerate carrier UpgradeCode");
            wchar_t code[39]{}, sid[256]{};
            DWORD length = 256;
            MSIINSTALLCONTEXT context{};
            const UINT result = MsiEnumProductsExW(candidate, nullptr, MSIINSTALLCONTEXT_USERUNMANAGED, 0, code, &context, sid, &length);
            if (result == ERROR_NO_MORE_ITEMS) continue;
            PowerToysProtectedStorage::result(result, "enumerate current owner's USERUNMANAGED products");
            if (context != MSIINSTALLCONTEXT_USERUNMANAGED || owner != sid || _wcsicmp(code, candidate))
                throw PowerToysProtectedStorage::failure("MSI registration owner/context/code mismatch", ERROR_INVALID_OWNER);
            if (product_info(code, INSTALLPROPERTY_PRODUCTSTATE) != std::to_wstring(INSTALLSTATE_DEFAULT))
                throw PowerToysProtectedStorage::failure("product is registered but not installed; no advertised-state fallback", ERROR_BAD_CONFIGURATION);
            const auto version = product_info(code, INSTALLPROPERTY_VERSIONSTRING);
            answer.push_back({ code, version, PowerToysProtectedStorage::Maintenance::ReleaseNumber(version) });
        }
        return answer;
    }
    registration selected(const std::vector<registration>& products)
    {
        if (products.empty()) throw PowerToysProtectedStorage::failure("no current-user context2 product is installed", ERROR_UNKNOWN_PRODUCT);
        if (products.size() != 1) throw PowerToysProtectedStorage::failure("multiple installed releases require explicit recovery; refusing ambiguous maintenance", ERROR_BAD_CONFIGURATION);
        return products.front();
    }
    bool instance_exists(const std::wstring& owner)
    {
        PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        PowerToysProtectedStorage::check(scm.value != nullptr, "SCM query");
        PowerToysProtectedStorage::service service(OpenServiceW(scm.value, product::service_name(owner).c_str(), SERVICE_QUERY_CONFIG | SERVICE_QUERY_STATUS));
        if (!service.value)
        {
            if (GetLastError() == ERROR_SERVICE_DOES_NOT_EXIST) return false;
            throw PowerToysProtectedStorage::failure("query owner VA service", GetLastError());
        }
        DWORD needed = 0;
        QueryServiceConfigW(service.value, nullptr, 0, &needed);
        PowerToysProtectedStorage::check(GetLastError() == ERROR_INSUFFICIENT_BUFFER, "owner VA service configuration size");
        std::vector<BYTE> buffer(needed);
        auto* config = reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.data());
        PowerToysProtectedStorage::check(QueryServiceConfigW(service.value, config, needed, &needed) != FALSE, "owner VA service configuration");
        if (config->dwServiceType != SERVICE_WIN32_OWN_PROCESS || product::image_path(owner) != config->lpBinaryPathName ||
            _wcsicmp(config->lpServiceStartName, product::service_account(owner).c_str()))
            throw PowerToysProtectedStorage::failure("existing service is not the expected owner VA instance", ERROR_ACCESS_DENIED);
        const auto record = product::state_root() / L"Installer" / owner;
        if (!setup::exists(record / L"active")) throw PowerToysProtectedStorage::failure("incomplete provisioning requires explicit recovery", ERROR_INVALID_STATE);
        SERVICE_STATUS_PROCESS status{};
        DWORD returned = 0;
        PowerToysProtectedStorage::check(QueryServiceStatusEx(service.value, SC_STATUS_PROCESS_INFO, reinterpret_cast<BYTE*>(&status),
            sizeof(status), &returned) != FALSE, "owner service readiness");
        if (status.dwCurrentState != SERVICE_RUNNING)
            throw PowerToysProtectedStorage::failure("owner bootstrap is not running; explicit repair may be required", ERROR_SERVICE_NOT_ACTIVE);
        return true;
    }
    bool pending_inventory(const std::wstring& owner)
    {
        const auto record = product::state_root() / L"Installer" / owner;
        if (!setup::exists(record)) return false;
        product::assert_system_directory(record);
        for (const auto* name : { L"pending-install", L"pending-remove", L"pending-repair", L"drain" })
            if (setup::exists(record / name)) return true;
        return !setup::exists(record / L"active") && !setup::exists(record / L"retained") && !setup::exists(record / L"purged");
    }
    bool retained_instance(const std::wstring& owner)
    {
        const auto record = product::state_root() / L"Installer" / owner;
        if (!setup::exists(record) || (!setup::exists(record / L"retained") && !setup::exists(record / L"purged")))
            return false;
        product::assert_system_directory(record);
        if (pending_inventory(owner) || setup::exists(record / L"active")) return false;
        std::wifstream receipt(record / (setup::exists(record / L"retained") ? L"retained" : L"purged"));
        std::wstring format, recordedOwner;
        if (!std::getline(receipt, format) || !std::getline(receipt, recordedOwner) ||
            format != L"format=1" || recordedOwner != L"owner=" + owner)
            throw PowerToysProtectedStorage::failure("retained owner inventory mismatch", ERROR_INVALID_OWNER);
        return true;
    }
    UINT inspect_live_release()
    {
        const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::actor_sid());
        const auto parent = product::known_folder(FOLDERID_LocalAppData) / product::name / L"Inspection";
        std::filesystem::create_directories(parent);
        const auto stage = parent / setup::unique_id();
        setup::new_directory(stage, L"O:" + owner + L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + owner + L")");
        const auto package = stage / L"Carrier.msi";
        const auto executable = stage / L"PowerToys.ProtectedStorageMsiAction.exe";
        product::write_new(package, product::resource(102));
        DWORD result = ERROR_INSTALL_FAILURE;
        {
            PowerToysProtectedStorage::handle packageLock(CreateFileW(package.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            PowerToysProtectedStorage::check(packageLock.value != INVALID_HANDLE_VALUE, "hold inspection package");
            PowerToysProtectedStorage::Maintenance::VerifySignedFile(package);
            setup::msi_handle database;
            PowerToysProtectedStorage::result(MsiOpenDatabaseW(package.c_str(), MSIDBOPEN_READONLY, &database.value),
                "open inspection package without an MSI transaction");
            setup::verify_package(database.value);
            product::write_new(executable, setup::binary_stream(database.value, L"MsiAction"));
            PowerToysProtectedStorage::handle imageLock(CreateFileW(executable.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr,
                OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            PowerToysProtectedStorage::check(imageLock.value != INVALID_HANDLE_VALUE, "hold inspection caller");
            PowerToysProtectedStorage::Maintenance::VerifySignedFile(executable);
            std::wstring command = L"\"" + executable.wstring() + L"\" inspect \"" + package.wstring() + L"\"";
            STARTUPINFOW startup{ sizeof(startup) };
            startup.dwFlags = STARTF_USESTDHANDLES;
            startup.hStdInput = INVALID_HANDLE_VALUE;
            startup.hStdOutput = INVALID_HANDLE_VALUE;
            startup.hStdError = INVALID_HANDLE_VALUE;
            PROCESS_INFORMATION child{};
            PowerToysProtectedStorage::check(CreateProcessW(executable.c_str(), command.data(), nullptr, nullptr, FALSE,
                CREATE_NO_WINDOW, nullptr, stage.c_str(), &startup, &child) != FALSE, "inspect signed existing release");
            PowerToysProtectedStorage::handle process(child.hProcess), thread(child.hThread);
            if (WaitForSingleObject(process.value, 120000) != WAIT_OBJECT_0)
                throw PowerToysProtectedStorage::failure("live release inspection outcome unknown", WAIT_TIMEOUT);
            PowerToysProtectedStorage::check(GetExitCodeProcess(process.value, &result) != FALSE, "live inspection result");
        }
        if (!DeleteFileW(executable.c_str()) || !DeleteFileW(package.c_str()) || !RemoveDirectoryW(stage.c_str()))
            maintenanceCleanupPending = true;
        return result;
    }
    UINT configure(const std::wstring& verb, const std::wstring& selectedCode)
    {
        PowerToysProtectedStorage::handle token;
        PowerToysProtectedStorage::check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token.value) != FALSE, "native MSI owner token");
        setup::require_normal(token.value);
        const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::token_sid(token.value));
        setup::require_profile(owner);
        const auto sources = product::known_folder(FOLDERID_LocalAppData) / product::name / L"Sources";
        std::filesystem::create_directories(sources);
        const auto source = sources / setup::unique_id();
        setup::new_directory(source, L"O:" + owner + L"D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;" + owner + L")");
        const auto package = source / L"Carrier.msi";
        product::write_new(package, product::resource(102));
        PowerToysProtectedStorage::handle packageLock(CreateFileW(package.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        PowerToysProtectedStorage::check(packageLock.value != INVALID_HANDLE_VALUE, "hold exact ordinary-owner MSI source");
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(package);
        {
            setup::msi_handle database;
            PowerToysProtectedStorage::result(MsiOpenDatabaseW(package.c_str(), MSIDBOPEN_READONLY, &database.value), "open exact owner MSI read-only");
            setup::verify_package(database.value);
        }
        std::wofstream evidence(source / L"result.txt");
        if (!evidence) throw PowerToysProtectedStorage::failure("owner-context setup evidence", ERROR_WRITE_FAULT);
        evidence << L"ownerSid=" << owner << L"\nownerElevated=0\ncontext=USERUNMANAGED(2)\nverb=" << verb
            << L"\nembeddedProductCode=" << product::code << L"\nembeddedVersion=" << product::version
            << L"\nselectedInstalledProduct=" << selectedCode << L"\nsource=" << package.wstring() << L"\n" << std::flush;
        MsiSetInternalUI(INSTALLUILEVEL_NONE, nullptr);
        PowerToysProtectedStorage::result(MsiEnableLogW(INSTALLLOGMODE_ERROR | INSTALLLOGMODE_WARNING,
            (source / L"native-msi.log").c_str(), INSTALLLOGATTRIBUTES_FLUSHEACHLINE), "enable owner-context native MSI log");
        std::wcout << L"nativeApiBegin ownerSid=" << owner << L" verb=" << verb << L" source=" << package.wstring() << L"\n" << std::flush;
        UINT result = ERROR_INVALID_PARAMETER;
        if (verb == L"install" || verb == L"upgrade")
            result = MsiInstallProductW(package.c_str(), L"REBOOT=ReallySuppress");
        else if (verb == L"repair")
        {
            if (selectedCode != product::code)
                throw PowerToysProtectedStorage::failure("repair requires the matching installed release", ERROR_PRODUCT_VERSION);
            result = MsiInstallProductW(package.c_str(), L"REINSTALL=ALL REINSTALLMODE=vomus REBOOT=ReallySuppress");
        }
        else if (verb == L"remove")
            result = MsiConfigureProductExW(selectedCode.c_str(), INSTALLLEVEL_DEFAULT, INSTALLSTATE_ABSENT, L"REBOOT=ReallySuppress");
        evidence << L"nativeMsiResult=" << result << L"\n" << std::flush;
        std::wcout << L"nativeApiEnd ownerSid=" << owner << L" verb=" << verb << L" msiResult=" << result << L"\n" << std::flush;
        const UINT logging = MsiEnableLogW(0, nullptr, 0);
        if (logging != ERROR_SUCCESS) std::wcerr << L"disableNativeMsiLogError=" << logging << L"\n";
        if (setup::success(result))
        {
            const auto flags = GetFileAttributesW(product::cleanup_hint().c_str());
            const auto error = GetLastError();
            maintenanceCleanupPending = maintenanceCleanupPending || flags != INVALID_FILE_ATTRIBUTES ||
                (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND);
        }
        return result;
    }
    SERVICE_STATUS_PROCESS service_status(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS state{};
        DWORD bytes = 0;
        PowerToysProtectedStorage::check(QueryServiceStatusEx(service, SC_STATUS_PROCESS_INFO, reinterpret_cast<LPBYTE>(&state), sizeof(state), &bytes) != FALSE, "temporary broker service status");
        return state;
    }
    UINT broker_phase(SC_HANDLE service, const std::filesystem::path& stage, const wchar_t* mode)
    {
        LPCWSTR arguments[] = { mode };
        PowerToysProtectedStorage::check(StartServiceW(service, 1, arguments) != FALSE, "start fixed temporary broker phase");
        const auto deadline = GetTickCount64() + 390000;
        PowerToysProtectedStorage::handle process;
        SERVICE_STATUS_PROCESS state{};
        do
        {
            state = service_status(service);
            if (!process.value && state.dwProcessId)
            {
                process.value = OpenProcess(SYNCHRONIZE, FALSE, state.dwProcessId);
                if (!process.value && GetLastError() != ERROR_INVALID_PARAMETER) throw PowerToysProtectedStorage::failure("hold broker process", GetLastError());
            }
            if (state.dwCurrentState == SERVICE_STOPPED) break;
            Sleep(100);
        } while (GetTickCount64() < deadline);
        if (state.dwCurrentState != SERVICE_STOPPED)
            throw PowerToysProtectedStorage::failure("broker outcome unknown; protected evidence retained", WAIT_TIMEOUT);
        if (process.value && WaitForSingleObject(process.value, 30000) != WAIT_OBJECT_0)
            throw PowerToysProtectedStorage::failure("broker process has not exited; no overlapping restart", WAIT_TIMEOUT);
        UINT result = ERROR_INSTALL_FAILURE;
        std::wifstream completed(stage / (std::wstring(mode) + L".result"));
        if (!(completed >> result))
            throw PowerToysProtectedStorage::failure("broker stopped without protected native result", state.dwWin32ExitCode ? state.dwWin32ExitCode : ERROR_INVALID_DATA);
        std::wcout << L"brokerPhase=" << mode << L" nativeResult=" << result << L" stage=" << stage.wstring() << L"\n" << std::flush;
        if (!setup::success(result)) PowerToysProtectedStorage::result(result, "protected broker phase failed");
        if (!setup::success(state.dwWin32ExitCode)) PowerToysProtectedStorage::result(state.dwWin32ExitCode, "temporary broker SCM exit");
        return result;
    }
    void check_event(HANDLE event, const std::wstring& owner)
    {
        PSID sid = nullptr;
        PACL acl = nullptr;
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PowerToysProtectedStorage::result(GetSecurityInfo(event, SE_KERNEL_OBJECT, OWNER_SECURITY_INFORMATION | DACL_SECURITY_INFORMATION,
            &sid, nullptr, &acl, nullptr, &descriptor), "rendezvous event security");
        struct release { PSECURITY_DESCRIPTOR value; ~release() { LocalFree(value); } } free{ descriptor };
        SECURITY_DESCRIPTOR_CONTROL control{};
        DWORD revision = 0;
        PowerToysProtectedStorage::check(GetSecurityDescriptorControl(descriptor, &control, &revision) != FALSE, "rendezvous event control");
        if (PowerToysProtectedStorage::sid_string(sid) != owner || !acl || !(control & SE_DACL_PROTECTED) || acl->AceCount != 3)
            throw PowerToysProtectedStorage::failure("rendezvous event owner/DACL mismatch", ERROR_ACCESS_DENIED);
        bool hasOwner = false, hasSystem = false, hasAdmin = false;
        for (DWORD index = 0; index < acl->AceCount; ++index)
        {
            LPVOID raw = nullptr;
            PowerToysProtectedStorage::check(GetAce(acl, index, &raw) != FALSE, "rendezvous event ACE");
            auto* ace = static_cast<ACCESS_ALLOWED_ACE*>(raw);
            if (ace->Header.AceType != ACCESS_ALLOWED_ACE_TYPE || ace->Header.AceFlags ||
                (ace->Mask != GENERIC_ALL && ace->Mask != EVENT_ALL_ACCESS))
                throw PowerToysProtectedStorage::failure("unexpected rendezvous authority", ERROR_ACCESS_DENIED);
            const auto trustee = PowerToysProtectedStorage::sid_string(&ace->SidStart);
            if (trustee == owner) hasOwner = true;
            else if (trustee == L"S-1-5-18") hasSystem = true;
            else if (trustee == L"S-1-5-32-544") hasAdmin = true;
            else throw PowerToysProtectedStorage::failure("unexpected rendezvous writer", ERROR_ACCESS_DENIED);
        }
        if (!hasOwner || !hasSystem || !hasAdmin) throw PowerToysProtectedStorage::failure("rendezvous lacks expected roles", ERROR_ACCESS_DENIED);
    }
    UINT authorize(const std::wstring& verb, DWORD requesterPid, ULONGLONG birth, const std::wstring& nonce)
    {
        if (verb != L"install" && verb != L"remove" && verb != L"purge" && verb != L"repair")
            throw PowerToysProtectedStorage::failure("unsupported fixed authorization operation", ERROR_BAD_ARGUMENTS);
        if (!setup::valid_id(nonce)) throw PowerToysProtectedStorage::failure("authorization nonce", ERROR_BAD_ARGUMENTS);
        product::privilege(SE_DEBUG_NAME);
        product::privilege(SE_RESTORE_NAME);
        PowerToysProtectedStorage::handle requester;
        const auto owner = setup::requester_owner(requesterPid, birth, requester, true);
        PowerToysProtectedStorage::handle ready(OpenEventW(EVENT_MODIFY_STATE | SYNCHRONIZE | READ_CONTROL, FALSE, setup::event_name(requesterPid, birth, nonce, (verb + L"-ready").c_str()).c_str()));
        PowerToysProtectedStorage::handle succeeded(OpenEventW(SYNCHRONIZE | READ_CONTROL, FALSE, setup::event_name(requesterPid, birth, nonce, (verb + L"-succeeded").c_str()).c_str()));
        PowerToysProtectedStorage::handle failed(OpenEventW(SYNCHRONIZE | READ_CONTROL, FALSE, setup::event_name(requesterPid, birth, nonce, (verb + L"-failed").c_str()).c_str()));
        PowerToysProtectedStorage::check(ready.value && succeeded.value && failed.value, "original requester rendezvous");
        for (HANDLE event : { ready.value, succeeded.value, failed.value })
        {
            check_event(event, owner);
            if (WaitForSingleObject(event, 0) != WAIT_TIMEOUT) throw PowerToysProtectedStorage::failure("rendezvous already signaled", ERROR_INVALID_STATE);
        }
        const auto serviceName = std::wstring(product::setupPrefix) + setup::unique_id();
        const auto stage = product::stage_root() / serviceName;
        const DWORD parentFlags = GetFileAttributesW(stage.parent_path().c_str());
        if (parentFlags == INVALID_FILE_ATTRIBUTES || (parentFlags & FILE_ATTRIBUTE_REPARSE_POINT))
            throw PowerToysProtectedStorage::failure("unsafe ProgramData staging parent", ERROR_ACCESS_DENIED);
        const std::wstring stagingAcl = L"O:SYG:SYD:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)";
        setup::new_directory(stage, stagingAcl);
        product::write_new(stage / L"ProvisionBroker.exe", product::resource(101), stagingAcl);
        product::write_new(stage / L"AuthorizedSetup.exe",
                          PowerToysProtectedStorage::Maintenance::ReadLocked([&]() {
                              static PowerToysProtectedStorage::handle image(CreateFileW(product::module_path().c_str(),
                                  GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
                              PowerToysProtectedStorage::check(image.value != INVALID_HANDLE_VALUE, "lock authorized Setup");
                              return image.value;
                          }()), stagingAcl);
        product::write_new_text(stage / L"request", std::to_wstring(requesterPid) + L"\n" + std::to_wstring(birth) +
            L"\n" + nonce + L"\n" + verb + L"\n" + owner + L"\n");
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(stage / L"ProvisionBroker.exe");
        PowerToysProtectedStorage::set_acl(stage / L"ProvisionBroker.exe", PowerToysProtectedStorage::system_acl());
        PowerToysProtectedStorage::set_acl(stage / L"AuthorizedSetup.exe", PowerToysProtectedStorage::system_acl());
        PowerToysProtectedStorage::set_acl(stage / L"request", PowerToysProtectedStorage::system_acl());
        PowerToysProtectedStorage::set_acl(stage, PowerToysProtectedStorage::system_acl());
        product::assert_system_directory(stage);
        std::wcout << L"explicitAuthorization actor=" << PowerToysProtectedStorage::actor_sid() << L" businessOwner=" << owner
            << L" verb=" << verb << L" stage=" << stage.wstring() << L"\n" << std::flush;
        PowerToysProtectedStorage::service scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CREATE_SERVICE));
        PowerToysProtectedStorage::check(scm.value != nullptr, "SCM temporary provisioning broker");
        const auto command = L"\"" + (stage / L"ProvisionBroker.exe").wstring() + L"\" --service " +
            std::to_wstring(requesterPid) + L" " + std::to_wstring(birth) + L" " + nonce;
        PowerToysProtectedStorage::service service(CreateServiceW(scm.value, serviceName.c_str(), serviceName.c_str(),
            SERVICE_START | SERVICE_QUERY_STATUS | DELETE, SERVICE_WIN32_OWN_PROCESS, SERVICE_DEMAND_START,
            SERVICE_ERROR_NORMAL, command.c_str(), nullptr, nullptr, nullptr, nullptr, nullptr));
        PowerToysProtectedStorage::check(service.value != nullptr, "create one-shot SYSTEM provisioning broker");
        bool prepared = false, released = false;
        try
        {
            broker_phase(service.value, stage, verb == L"install" ? L"provision" :
                verb == L"repair" ? L"repair-bootstrap" : verb == L"purge" ? L"prepare-purge" : L"prepare-remove");
            prepared = true;
            if (WaitForSingleObject(requester.value, 0) != WAIT_TIMEOUT)
                throw PowerToysProtectedStorage::failure("original requester exited before ordinary MSI was authorized", ERROR_PROCESS_ABORTED);
            PowerToysProtectedStorage::check(SetEvent(ready.value) != FALSE, "signal owner to execute ordinary MSI");
            released = true;
            const HANDLE wait[] = { succeeded.value, failed.value, requester.value };
            const DWORD ownerWait = WaitForMultipleObjects(3, wait, FALSE, 1800000);
            if (ownerWait == WAIT_TIMEOUT)
                throw PowerToysProtectedStorage::failure("ordinary MSI outcome still unknown; retained protected transaction for recovery", WAIT_TIMEOUT);
            if (ownerWait == WAIT_FAILED) throw PowerToysProtectedStorage::failure("wait original owner's native MSI result", GetLastError());
            if (ownerWait == WAIT_OBJECT_0 + 2)
                throw PowerToysProtectedStorage::failure("owner exited without a native MSI result; do not guess rollback while MSI may still run", ERROR_PROCESS_ABORTED);
            const bool successful = ownerWait == WAIT_OBJECT_0;
            if (successful && WaitForSingleObject(failed.value, 0) != WAIT_TIMEOUT)
                throw PowerToysProtectedStorage::failure("conflicting owner MSI completion signals", ERROR_INVALID_DATA);
            const UINT finalized = broker_phase(service.value, stage, verb == L"install" ?
                (successful ? L"finalize-install" : L"undo-install") : verb == L"repair" ? L"finalize-repair" :
                (successful ? L"commit-remove" : L"undo-remove"));
            PowerToysProtectedStorage::check(DeleteService(service.value) != FALSE, "delete temporary provisioning service");
            return finalized;
        }
        catch (...)
        {
            if (prepared && !released)
            {
                try { broker_phase(service.value, stage, verb == L"install" ? L"undo-install" :
                    verb == L"repair" ? L"finalize-repair" : L"undo-remove"); }
                catch (const PowerToysProtectedStorage::failure& cleanup)
                {
                    std::cerr << "pre-MSI authorization cleanup failed: " << cleanup.what() << " nativeError=" << cleanup.code << "\n";
                }
            }
            // Retain SYSTEM-owned package, provisioner, receipts and native logs, but never a permanent SYSTEM updater.
            if (!DeleteService(service.value)) std::wcerr << L"temporaryBrokerDeleteError=" << GetLastError() << L"\n";
            throw;
        }
    }
    UINT authorized_msi(const std::wstring& verb, const std::wstring& selectedCode)
    {
        maintenanceCleanupPending = true;
        const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::actor_sid());
        const DWORD pid = GetCurrentProcessId();
        const ULONGLONG birth = product::process_birth(GetCurrentProcess());
        const auto nonce = setup::unique_id();
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        PowerToysProtectedStorage::check(ConvertStringSecurityDescriptorToSecurityDescriptorW(
            (L"O:" + owner + L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" + owner + L")").c_str(),
            SDDL_REVISION_1, &descriptor, nullptr) != FALSE, "setup rendezvous security");
        struct release { PSECURITY_DESCRIPTOR value; ~release() { LocalFree(value); } } free{ descriptor };
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        PowerToysProtectedStorage::handle ready(CreateEventW(&attributes, TRUE, FALSE, setup::event_name(pid, birth, nonce, (verb + L"-ready").c_str()).c_str()));
        const DWORD readyError = GetLastError();
        PowerToysProtectedStorage::handle succeeded(CreateEventW(&attributes, TRUE, FALSE, setup::event_name(pid, birth, nonce, (verb + L"-succeeded").c_str()).c_str()));
        const DWORD successError = GetLastError();
        PowerToysProtectedStorage::handle failed(CreateEventW(&attributes, TRUE, FALSE, setup::event_name(pid, birth, nonce, (verb + L"-failed").c_str()).c_str()));
        const DWORD failureError = GetLastError();
        PowerToysProtectedStorage::check(ready.value && succeeded.value && failed.value, "create owner rendezvous events");
        if (readyError == ERROR_ALREADY_EXISTS || successError == ERROR_ALREADY_EXISTS || failureError == ERROR_ALREADY_EXISTS)
            throw PowerToysProtectedStorage::failure("rendezvous event precreation", ERROR_ALREADY_EXISTS);
        const auto arguments = L"--authorize " + verb + L" " + std::to_wstring(pid) + L" " + std::to_wstring(birth) + L" " + nonce;
        const auto executable = product::module_path();
        SHELLEXECUTEINFOW launch{ sizeof(launch) };
        launch.fMask = SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NOASYNC;
        launch.lpVerb = L"runas";
        launch.lpFile = executable.c_str();
        launch.lpParameters = arguments.c_str();
        launch.nShow = SW_HIDE;
        std::wcout << L"authorizationArguments=" << arguments << L"\n" << std::flush;
        PowerToysProtectedStorage::check(ShellExecuteExW(&launch) != FALSE, "explicit initial provisioning/removal authorization");
        PowerToysProtectedStorage::handle helper(launch.hProcess);
        PowerToysProtectedStorage::check(helper.value != nullptr, "authorization process handle");
        const HANDLE wait[] = { ready.value, helper.value };
        const DWORD outcome = WaitForMultipleObjects(2, wait, FALSE, 450000);
        if (outcome == WAIT_OBJECT_0 + 1)
        {
            DWORD result = 0;
            PowerToysProtectedStorage::check(GetExitCodeProcess(helper.value, &result) != FALSE, "authorization native failure");
            throw PowerToysProtectedStorage::failure("authorized preparation failed; ordinary MSI was not started", result ? result : ERROR_INSTALL_FAILURE);
        }
        if (outcome != WAIT_OBJECT_0)
            throw PowerToysProtectedStorage::failure("authorized preparation outcome unknown; ordinary MSI was not started", outcome == WAIT_TIMEOUT ? WAIT_TIMEOUT : GetLastError());
        UINT msiResult = ERROR_INSTALL_FAILURE;
        try { msiResult = configure(verb == L"purge" ? L"remove" : verb, selectedCode); }
        catch (const PowerToysProtectedStorage::failure& error)
        {
            msiResult = error.code ? error.code : ERROR_INSTALL_FAILURE;
            std::cerr << "ordinary MSI preparation failed: " << error.what() << " nativeError=" << msiResult << "\n";
        }
        catch (const std::exception& error) { std::cerr << "ordinary MSI exception: " << error.what() << "\n"; }
        PowerToysProtectedStorage::check(SetEvent(setup::success(msiResult) ? succeeded.value : failed.value) != FALSE, "signal native MSI success or failure");
        if (WaitForSingleObject(helper.value, 450000) != WAIT_OBJECT_0)
            throw PowerToysProtectedStorage::failure("authorized finalization pending; owner MSI result already recorded", WAIT_TIMEOUT);
        DWORD helperResult = 0;
        PowerToysProtectedStorage::check(GetExitCodeProcess(helper.value, &helperResult) != FALSE, "authorized finalization result");
        if (!setup::success(helperResult))
            PowerToysProtectedStorage::result(helperResult, "authorized protected finalization failed");
        return setup::success(msiResult) && helperResult == ERROR_SUCCESS_REBOOT_REQUIRED ? helperResult : msiResult;
    }
}
int Execute(const PowerToysProtectedStorage::Maintenance::Request& request)
{
    PowerToysProtectedStorage::handle token;
    PowerToysProtectedStorage::check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token.value) != FALSE, "Setup owner token");
    setup::require_normal(token.value);
    const auto owner = PowerToysProtectedStorage::canonical_owner(PowerToysProtectedStorage::token_sid(token.value));
    setup::require_profile(owner);
    const auto products = installed();
    if (products.size() == 1)
    {
        observedProductCode = products.front().code;
        observedVersion = products.front().version;
    }
    if (request.verb == L"inspect")
    {
        if (products.size() > 1)
            throw PowerToysProtectedStorage::failure("ambiguous owner registrations require recovery", ERROR_BAD_CONFIGURATION);
        if (products.empty())
        {
            if (instance_exists(owner))
                throw PowerToysProtectedStorage::failure("live instance has no committed owner carrier registration", ERROR_INVALID_STATE);
            return ERROR_UNKNOWN_PRODUCT;
        }
        if (!instance_exists(owner))
            return ERROR_SERVICE_DOES_NOT_EXIST;
        try
        {
            PowerToys::ProtectedStorage::Paths paths(owner);
            const auto status = PowerToys::ProtectedStorage::Value::Parse(
                PowerToys::ProtectedStorage::Call(paths, PowerToys::ProtectedStorage::Request{}));
            PowerToysProtectedStorage::Maintenance::RequireHealthyControlStatus(status,
                PowerToysProtectedStorage::ToUtf8(selected(products).version + L".0"));
        }
        catch (const PowerToys::ProtectedStorage::Error&)
        {
            throw PowerToysProtectedStorage::failure("owner inspection is unverified; reconciliation required", ERROR_INVALID_STATE);
        }
        return ERROR_SUCCESS;
    }
    if (request.verb == L"sync")
    {
        const bool present = instance_exists(owner);
        const auto record = product::state_root() / L"Installer" / owner;
        const bool pending = pending_inventory(owner) ||
            (!present && products.empty() && setup::exists(record / L"active"));
        const auto plan = PowerToysProtectedStorage::Maintenance::PlanSync(products.size(), present, pending,
            products.empty() ? 0 : products.front().release,
            PowerToysProtectedStorage::Maintenance::ReleaseNumber(product::version),
            products.size() == 1 && products.front().code == product::code);
        if (plan == PowerToysProtectedStorage::Maintenance::SyncPlan::Absent)
        {
            syncState = L"NotProvisioned";
            return ERROR_SUCCESS;
        }
        if (!product::has_resource(102))
            throw PowerToysProtectedStorage::failure("sync requires a finalized signed release package", ERROR_TRUST_FAILURE);
        PowerToysProtectedStorage::Maintenance::VerifySignedFile(product::module_path());
        if (plan == PowerToysProtectedStorage::Maintenance::SyncPlan::VerifyCurrent)
        {
            const auto result = inspect_live_release();
            if (result == ERROR_SUCCESS) syncState = L"Unchanged";
            return static_cast<int>(result);
        }
        syncState = L"Ready";
        return static_cast<int>(configure(L"upgrade", selected(products).code));
    }
    if (!product::has_resource(102))
        throw PowerToysProtectedStorage::failure("this is an unsigned build, not a finalized release package", ERROR_TRUST_FAILURE);
    PowerToysProtectedStorage::Maintenance::VerifySignedFile(product::module_path());
    bool instance = false;
    try { instance = instance_exists(owner); }
    catch (const PowerToysProtectedStorage::failure& error)
    {
        if (request.verb == L"remove" && error.code == ERROR_SERVICE_NOT_ACTIVE) instance = true;
        else if (request.verb != L"repair" || !request.authorizeRepair) throw;
    }
    if (request.verb == L"ensure" || request.verb == L"upgrade")
    {
        if (!products.empty() && selected(products).release > PowerToysProtectedStorage::Maintenance::ReleaseNumber(product::version))
            throw PowerToysProtectedStorage::failure("refusing a carrier downgrade", ERROR_PRODUCT_VERSION);
        if (!instance)
        {
            if (request.verb == L"upgrade" || (!products.empty() && !retained_instance(owner)))
                throw PowerToysProtectedStorage::failure("upgrade requires a healthy existing instance", ERROR_SERVICE_DOES_NOT_EXIST);
            return static_cast<int>(authorized_msi(L"install", L""));
        }
        return static_cast<int>(configure(L"upgrade", products.empty() ? L"" : selected(products).code));
    }
    const auto current = selected(products);
    if (current.release > PowerToysProtectedStorage::Maintenance::ReleaseNumber(product::version))
        throw PowerToysProtectedStorage::failure("maintenance requires this or a newer signed release", ERROR_PRODUCT_VERSION);
    if (request.verb == L"repair" && current.code != product::code)
        throw PowerToysProtectedStorage::failure("repair requires the matching registered carrier release", ERROR_PRODUCT_VERSION);
    if (request.verb == L"repair" && request.authorizeRepair)
    {
        if (!instance && retained_instance(owner))
            return static_cast<int>(authorized_msi(L"install", current.code));
        return static_cast<int>(authorized_msi(L"repair", current.code));
    }
    if (!instance)
        throw PowerToysProtectedStorage::failure("explicit authorized bootstrap repair required", ERROR_SERVICE_DOES_NOT_EXIST);
    if (request.verb == L"remove")
        return static_cast<int>(authorized_msi(request.removal == PowerToysProtectedStorage::Maintenance::RemovalMode::PurgeData ?
            L"purge" : L"remove", current.code));
    return static_cast<int>(configure(L"repair", current.code));
}

int wmain(int argc, wchar_t** argv)
{
    using namespace PowerToysProtectedStorage;
    using namespace PowerToysProtectedStorage::Maintenance;
    std::wstring operation;
    std::wstring state = L"Failed";
    DWORD result = ERROR_INSTALL_FAILURE;
    bool retained = false;
    try
    {
        initialize_process_security();
        handle selfImage(CreateFileW(product::module_path().c_str(), GENERIC_READ | GENERIC_EXECUTE, FILE_SHARE_READ, nullptr,
                                    OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
        check(selfImage.value != INVALID_HANDLE_VALUE, "hold original Setup image throughout authorization");
        // The public executable has a Windows subsystem and writes only one JSON
        // result to an inherited pipe; internal diagnostics never create a console.
        std::wcout.setstate(std::ios::failbit);
        std::cout.setstate(std::ios::failbit);
        if (argc == 2 && std::wstring(argv[1]) == L"--machine-remove")
        {
            require_system();
            VerifyMappedImage(GetCurrentProcess(), selfImage.value);
            VerifySignedFile(product::module_path());
            const auto stage = product::stage_root() / (std::wstring(product::setupPrefix) + setup::unique_id());
            setup::new_directory(stage, system_acl());
            product::write_new(stage / L"ProvisionBroker.exe", product::resource(101), system_acl());
            return static_cast<int>(setup::fixed_machine_child(stage / L"ProvisionBroker.exe", L"--machine-remove"));
        }
        if (argc == 6 && std::wstring(argv[1]) == L"--authorize")
        {
            const auto pid = product::unsigned_number(argv[3]);
            if (pid > MAXDWORD) return ERROR_BAD_ARGUMENTS;
            return static_cast<int>(authorize(argv[2], static_cast<DWORD>(pid), product::unsigned_number(argv[4]), argv[5]));
        }
        auto request = Parse(std::vector<std::wstring>(argv + 1, argv + argc));
        handle ownerToken;
        check(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &ownerToken.value) != FALSE, "ordinary owner maintenance token");
        setup::require_normal(ownerToken.value);
        operation = request.verb == L"retry" ? request.operation : setup::unique_id();
        const auto owner = canonical_owner(actor_sid());
        const auto journalRoot = product::known_folder(FOLDERID_LocalAppData) / product::name / L"Maintenance";
        std::filesystem::create_directories(journalRoot);
        const auto receipt = journalRoot / (operation + L".txt");
        handle gate(CreateMutexW(nullptr, FALSE, (L"Global\\PowerToysProtectedStorageMaintenance_" + owner).c_str()));
        check(gate.value != nullptr, "owner maintenance gate");
        const DWORD acquired = WaitForSingleObject(gate.value, 0);
        if (acquired != WAIT_OBJECT_0)
        {
            const DWORD gateError = acquired == WAIT_ABANDONED ? ERROR_INVALID_STATE :
                acquired == WAIT_TIMEOUT ? ERROR_INSTALL_ALREADY_RUNNING : GetLastError();
            if (acquired == WAIT_TIMEOUT && request.verb != L"retry" &&
                (request.verb == L"ensure" || request.verb == L"upgrade" || request.verb == L"repair" ||
                 request.verb == L"sync" || request.verb == L"inspect"))
            {
                product::write_new_text(receipt, L"1 " + request.verb + L" " + product::code + L" " +
                    std::to_wstring(ERROR_INSTALL_ALREADY_RUNNING) + L"\n");
            }
            throw failure("owner maintenance is busy or interrupted", gateError ? gateError : ERROR_GEN_FAILURE);
        }
        struct release_mutex { HANDLE value; ~release_mutex() { ReleaseMutex(value); } } release{ gate.value };
        if (request.verb == L"retry")
        {
            handle file(CreateFileW(receipt.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_FLAG_OPEN_REPARSE_POINT, nullptr));
            check(file.value != INVALID_HANDLE_VALUE, "known maintenance receipt required");
            const auto bytes = ReadLocked(file.value, 4096);
            std::istringstream input(std::string(bytes.begin(), bytes.end()));
            std::string version, verb, identity;
            DWORD previous = ERROR_INVALID_STATE;
            if (!(input >> version >> verb >> identity >> previous) || version != "1" ||
                identity != ToUtf8(product::code) ||
                (verb != "ensure" && verb != "upgrade" && verb != "repair" && verb != "sync" && verb != "inspect"))
                throw failure("retry hint invalid; it is not authorization", ERROR_INVALID_DATA);
            if (!MayRetryMsi(previous))
                throw failure("inspect the existing operation; do not replay an unknown commit or privilege request", ERROR_INVALID_STATE);
            request = Parse({ std::wstring(verb.begin(), verb.end()), L"--json" });
        }
        {
            std::ofstream pending(receipt, std::ios::trunc);
            pending << "1 " << ToUtf8(request.verb) << " " << ToUtf8(product::code) << " " << ERROR_INVALID_STATE << "\n";
            if (!pending) throw failure("persist maintenance intent", ERROR_WRITE_FAULT);
        }
        result = static_cast<DWORD>(Execute(request));
        if (request.verb != L"inspect" && !(request.verb == L"sync" && syncState == L"NotProvisioned") && setup::success(result))
        {
            const auto confirmed = installed();
            if (request.verb == L"remove")
            {
                if (!confirmed.empty()) throw failure("removal registration outcome requires reconciliation", ERROR_INVALID_STATE);
                observedProductCode.clear();
                observedVersion.clear();
            }
            else
            {
                const auto committedRelease = selected(confirmed);
                if (committedRelease.code != product::code || committedRelease.version != product::version)
                    throw failure("registration differs from committed carrier", ERROR_INVALID_STATE);
                observedProductCode = committedRelease.code;
                observedVersion = committedRelease.version;
            }
        }
        if (request.verb == L"inspect")
        {
            if (result == ERROR_UNKNOWN_PRODUCT)
            {
                const auto record = product::state_root() / L"Installer" / owner;
                retained = setup::exists(record / L"retained");
                state = retained ? L"DataRetained" : L"NotProvisioned";
                result = ERROR_SUCCESS;
            }
            else state = setup::success(result) ? L"Ready" : L"RecoveryRequired";
        }
        else state = setup::success(result) ?
            (request.verb == L"sync" ? syncState : request.verb == L"remove" ? L"Removed" : L"Ready") : L"Failed";
        if (request.verb == L"sync" && state == L"NotProvisioned")
            retained = setup::exists(product::state_root() / L"Installer" / owner / L"retained");
        retained = retained || (request.verb == L"remove" && request.removal == RemovalMode::KeepData && setup::success(result));
        std::ofstream completed(receipt, std::ios::trunc);
        completed << "1 " << ToUtf8(request.verb) << " " << ToUtf8(product::code) << " " << result << "\n";
        if (!completed) throw failure("persist maintenance result", ERROR_WRITE_FAULT);
    }
    catch (const PowerToysProtectedStorage::failure& error)
    {
        result = error.code ? error.code : ERROR_INSTALL_FAILURE;
        if (result == ERROR_CANCELLED) result = ERROR_INSTALL_USEREXIT;
        if (std::wstring(RetryClass(result)) == L"InspectUnknownOutcome") state = L"RecoveryRequired";
    }
    catch (const std::exception&)
    {
        result = ERROR_INSTALL_FAILURE;
    }
    const auto text = ResultJson(operation, state, result, maintenanceCleanupPending || result == ERROR_SUCCESS_REBOOT_REQUIRED, retained,
        observedProductCode, observedVersion);
    const std::string bytes = ToUtf8(text);
    DWORD written = 0;
    WriteFile(GetStdHandle(STD_OUTPUT_HANDLE), bytes.data(), static_cast<DWORD>(bytes.size()), &written, nullptr);
    return static_cast<int>(result);
}
