#include "../Common/ProtoCommon.h"

#include <bcrypt.h>
#include <lm.h>
#include <ntsecapi.h>
#include <sddl.h>
#include <userenv.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Storage.h>
#include <winrt/base.h>

#include <filesystem>
#include <algorithm>
#include <cwctype>
#include <fstream>
#include <iostream>
#include <optional>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "bcrypt.lib")
#pragma comment(lib, "netapi32.lib")
#pragma comment(lib, "userenv.lib")

namespace
{
    class service_handle
    {
    public:
        service_handle() = default;
        explicit service_handle(SC_HANDLE value) :
            m_value(value)
        {
        }
        ~service_handle()
        {
            if (m_value)
            {
                CloseServiceHandle(m_value);
            }
        }
        service_handle(const service_handle&) = delete;
        service_handle& operator=(const service_handle&) = delete;
        service_handle(service_handle&& other) noexcept :
            m_value(other.release())
        {
        }
        service_handle& operator=(service_handle&& other) noexcept
        {
            if (this != &other)
            {
                if (m_value)
                {
                    CloseServiceHandle(m_value);
                }
                m_value = other.release();
            }
            return *this;
        }
        [[nodiscard]] SC_HANDLE get() const noexcept
        {
            return m_value;
        }
        [[nodiscard]] SC_HANDLE release() noexcept
        {
            const auto value = m_value;
            m_value = nullptr;
            return value;
        }
        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        SC_HANDLE m_value{};
    };

    bool is_elevated()
    {
        HANDLE rawToken = nullptr;
        ptap::check_bool(OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken), "OpenProcessToken(elevation)");
        ptap::unique_handle token(rawToken);
        TOKEN_ELEVATION elevation{};
        DWORD bytes = 0;
        ptap::check_bool(
            GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &bytes),
            "GetTokenInformation(TokenElevation)");
        return elevation.TokenIsElevated != 0;
    }

    void require_elevated()
    {
        if (!is_elevated())
        {
            throw ptap::win32_error("Elevation required", ERROR_ELEVATION_REQUIRED);
        }
    }

    ptap::secret_buffer generate_password()
    {
        constexpr wchar_t alphabet[] =
            L"ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!#$%&*+-=?@";
        constexpr size_t passwordCharacters = 40;
        std::array<UCHAR, passwordCharacters> random{};
        const NTSTATUS status = BCryptGenRandom(
            nullptr,
            random.data(),
            static_cast<ULONG>(random.size()),
            BCRYPT_USE_SYSTEM_PREFERRED_RNG);
        if (status != 0)
        {
            throw std::runtime_error("BCryptGenRandom failed");
        }
        ptap::secret_buffer password(passwordCharacters + 1);
        for (size_t index = 0; index < passwordCharacters; ++index)
        {
            password.data()[index] = alphabet[random[index] % (ARRAYSIZE(alphabet) - 1)];
        }
        password.data()[passwordCharacters] = L'\0';
        SecureZeroMemory(random.data(), random.size());
        return password;
    }

    void validate_sid(std::wstring_view value)
    {
        std::wstring copy(value);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(copy.c_str(), &sid))
        {
            throw ptap::win32_error("ConvertStringSidToSidW(owner)", GetLastError());
        }
        ptap::local_memory memory(sid);
        if (!IsValidSid(sid))
        {
            throw ptap::win32_error("Owner SID validation", ERROR_INVALID_SID);
        }
    }

    std::wstring selected_owner_sid(const std::vector<std::wstring>& args)
    {
        auto owner = ptap::argument_value(args, L"--owner-sid");
        if (owner.empty())
        {
            owner = ptap::current_token_user_sid();
        }
        validate_sid(owner);
        return owner;
    }

    void add_account_rights(std::wstring_view accountSid)
    {
        std::wstring sidText(accountSid);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(sidText.c_str(), &sid))
        {
            throw ptap::win32_error("ConvertStringSidToSidW(rights)", GetLastError());
        }
        ptap::local_memory sidMemory(sid);
        LSA_OBJECT_ATTRIBUTES attributes{};
        LSA_HANDLE rawPolicy = nullptr;
        const NTSTATUS openStatus = LsaOpenPolicy(
            nullptr,
            &attributes,
            POLICY_LOOKUP_NAMES | POLICY_CREATE_ACCOUNT,
            &rawPolicy);
        if (openStatus != 0)
        {
            throw ptap::win32_error("LsaOpenPolicy", LsaNtStatusToWinError(openStatus));
        }
        struct policy_guard
        {
            LSA_HANDLE value;
            ~policy_guard()
            {
                LsaClose(value);
            }
        } policy{ rawPolicy };
        std::array<std::wstring, 5> names{
            L"SeServiceLogonRight",
            L"SeDenyInteractiveLogonRight",
            L"SeDenyRemoteInteractiveLogonRight",
            L"SeDenyNetworkLogonRight",
            L"SeDenyBatchLogonRight",
        };
        std::array<LSA_UNICODE_STRING, names.size()> rights{};
        for (size_t index = 0; index < names.size(); ++index)
        {
            rights[index].Buffer = names[index].data();
            rights[index].Length = static_cast<USHORT>(names[index].size() * sizeof(wchar_t));
            rights[index].MaximumLength = rights[index].Length;
        }
        const NTSTATUS addStatus =
            LsaAddAccountRights(rawPolicy, sid, rights.data(), static_cast<ULONG>(rights.size()));
        if (addStatus != 0)
        {
            throw ptap::win32_error("LsaAddAccountRights", LsaNtStatusToWinError(addStatus));
        }
    }

    void remove_account_rights(std::wstring_view accountSid)
    {
        std::wstring sidText(accountSid);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(sidText.c_str(), &sid))
        {
            throw ptap::win32_error("ConvertStringSidToSidW(remove rights)", GetLastError());
        }
        ptap::local_memory sidMemory(sid);
        LSA_OBJECT_ATTRIBUTES attributes{};
        LSA_HANDLE rawPolicy = nullptr;
        const NTSTATUS openStatus =
            LsaOpenPolicy(nullptr, &attributes, POLICY_LOOKUP_NAMES, &rawPolicy);
        if (openStatus != 0)
        {
            throw ptap::win32_error("LsaOpenPolicy(remove rights)", LsaNtStatusToWinError(openStatus));
        }
        struct policy_guard
        {
            LSA_HANDLE value;
            ~policy_guard()
            {
                LsaClose(value);
            }
        } policy{ rawPolicy };
        std::array<std::wstring, 5> names{
            L"SeServiceLogonRight",
            L"SeDenyInteractiveLogonRight",
            L"SeDenyRemoteInteractiveLogonRight",
            L"SeDenyNetworkLogonRight",
            L"SeDenyBatchLogonRight",
        };
        std::array<LSA_UNICODE_STRING, names.size()> rights{};
        for (size_t index = 0; index < names.size(); ++index)
        {
            rights[index].Buffer = names[index].data();
            rights[index].Length = static_cast<USHORT>(names[index].size() * sizeof(wchar_t));
            rights[index].MaximumLength = rights[index].Length;
        }
        const NTSTATUS removeStatus =
            LsaRemoveAccountRights(rawPolicy, sid, FALSE, rights.data(), static_cast<ULONG>(rights.size()));
        if (removeStatus != 0)
        {
            const DWORD error = LsaNtStatusToWinError(removeStatus);
            if (error != ERROR_FILE_NOT_FOUND && error != ERROR_NONE_MAPPED)
            {
                throw ptap::win32_error("LsaRemoveAccountRights", error);
            }
        }
    }

    void create_local_account(const std::wstring& accountName, ptap::secret_buffer& password)
    {
        std::wstring mutableAccountName = accountName;
        std::wstring comment = L"PowerToys Workspaces App Execution Alias prototype service account";
        USER_INFO_1 user{};
        user.usri1_name = mutableAccountName.data();
        user.usri1_password = password.data();
        user.usri1_priv = USER_PRIV_USER;
        user.usri1_flags = UF_SCRIPT | UF_DONT_EXPIRE_PASSWD | UF_PASSWD_CANT_CHANGE;
        user.usri1_comment = comment.data();
        DWORD parameterError = 0;
        const NET_API_STATUS result = NetUserAdd(nullptr, 1, reinterpret_cast<LPBYTE>(&user), &parameterError);
        if (result == NERR_UserExists)
        {
            throw ptap::win32_error("Deterministic prototype account already exists", ERROR_ALREADY_EXISTS);
        }
        if (result != NERR_Success)
        {
            throw ptap::win32_error("NetUserAdd", result);
        }
    }

    void add_account_to_builtin_users(const std::wstring& accountName)
    {
        BYTE sidBuffer[SECURITY_MAX_SID_SIZE]{};
        DWORD sidBytes = sizeof(sidBuffer);
        ptap::check_bool(
            CreateWellKnownSid(WinBuiltinUsersSid, nullptr, sidBuffer, &sidBytes),
            "CreateWellKnownSid(Users)");
        wchar_t groupName[256]{};
        wchar_t domainName[256]{};
        DWORD groupChars = ARRAYSIZE(groupName);
        DWORD domainChars = ARRAYSIZE(domainName);
        SID_NAME_USE use{};
        ptap::check_bool(
            LookupAccountSidW(
                nullptr,
                sidBuffer,
                groupName,
                &groupChars,
                domainName,
                &domainChars,
                &use),
            "LookupAccountSidW(Users)");
        std::wstring memberName = accountName;
        LOCALGROUP_MEMBERS_INFO_3 member{};
        member.lgrmi3_domainandname = memberName.data();
        const NET_API_STATUS result = NetLocalGroupAddMembers(
            nullptr,
            groupName,
            3,
            reinterpret_cast<LPBYTE>(&member),
            1);
        if (result != NERR_Success && result != ERROR_MEMBER_IN_ALIAS)
        {
            throw ptap::win32_error("NetLocalGroupAddMembers(Users)", result);
        }
    }

    void reset_account_password(const std::wstring& accountName, ptap::secret_buffer& password)
    {
        USER_INFO_1003 information{};
        information.usri1003_password = password.data();
        const NET_API_STATUS result =
            NetUserSetInfo(nullptr, accountName.c_str(), 1003, reinterpret_cast<LPBYTE>(&information), nullptr);
        if (result != NERR_Success)
        {
            throw ptap::win32_error("NetUserSetInfo(password)", result);
        }
    }

    void ensure_profile(std::wstring_view accountSid, std::wstring_view accountName)
    {
        wchar_t profile[MAX_PATH]{};
        const HRESULT result = CreateProfile(
            std::wstring(accountSid).c_str(),
            std::wstring(accountName).c_str(),
            profile,
            ARRAYSIZE(profile));
        if (FAILED(result) && result != HRESULT_FROM_WIN32(ERROR_ALREADY_EXISTS))
        {
            throw ptap::win32_error("CreateProfile", HRESULT_CODE(result));
        }
    }

    std::optional<std::filesystem::path> profile_path_for_sid(std::wstring_view accountSid)
    {
        const std::wstring keyPath =
            L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + std::wstring(accountSid);
        HKEY rawKey = nullptr;
        const LSTATUS openResult =
            RegOpenKeyExW(HKEY_LOCAL_MACHINE, keyPath.c_str(), 0, KEY_QUERY_VALUE, &rawKey);
        if (openResult == ERROR_FILE_NOT_FOUND)
        {
            return std::nullopt;
        }
        ptap::check_lstatus(openResult, "RegOpenKeyExW(profile cleanup)");
        struct key_guard
        {
            HKEY value;
            ~key_guard()
            {
                RegCloseKey(value);
            }
        } key{ rawKey };
        DWORD type = 0;
        DWORD bytes = 0;
        ptap::check_lstatus(
            RegQueryValueExW(rawKey, L"ProfileImagePath", nullptr, &type, nullptr, &bytes),
            "RegQueryValueExW(ProfileImagePath size)");
        if ((type != REG_SZ && type != REG_EXPAND_SZ) || bytes < sizeof(wchar_t) || bytes > 32768)
        {
            throw ptap::win32_error("Profile path registry policy", ERROR_INVALID_DATA);
        }
        std::vector<wchar_t> value(bytes / sizeof(wchar_t));
        ptap::check_lstatus(
            RegQueryValueExW(
                rawKey,
                L"ProfileImagePath",
                nullptr,
                &type,
                reinterpret_cast<LPBYTE>(value.data()),
                &bytes),
            "RegQueryValueExW(ProfileImagePath)");
        const std::wstring rawPath(value.data());
        if (type == REG_EXPAND_SZ)
        {
            const DWORD chars = ExpandEnvironmentStringsW(rawPath.c_str(), nullptr, 0);
            if (chars == 0 || chars > 32768)
            {
                throw ptap::win32_error("ExpandEnvironmentStringsW(profile size)", GetLastError());
            }
            std::wstring expanded(chars, L'\0');
            if (!ExpandEnvironmentStringsW(rawPath.c_str(), expanded.data(), chars))
            {
                throw ptap::win32_error("ExpandEnvironmentStringsW(profile)", GetLastError());
            }
            expanded.resize(chars - 1);
            return expanded;
        }
        return rawPath;
    }

    bool delete_profile_with_retry(
        std::wstring_view ownerSid,
        std::wstring_view accountSid,
        DWORD timeoutMs)
    {
        const auto profile = profile_path_for_sid(accountSid);
        if (!profile)
        {
            return true;
        }
        const auto names = ptap::instance_names(ownerSid);
        wchar_t profilesRoot[MAX_PATH]{};
        DWORD rootChars = ARRAYSIZE(profilesRoot);
        ptap::check_bool(GetProfilesDirectoryW(profilesRoot, &rootChars), "GetProfilesDirectoryW");
        const auto canonicalProfile = std::filesystem::weakly_canonical(*profile);
        const auto canonicalRoot = std::filesystem::weakly_canonical(profilesRoot);
        const std::wstring profileLeaf = canonicalProfile.filename().wstring();
        const bool exactProfile =
            profileLeaf == names.accountName ||
            profileLeaf.starts_with(names.accountName + L".");
        if (canonicalProfile.parent_path() != canonicalRoot || !exactProfile)
        {
            throw ptap::win32_error("Profile cleanup path policy", ERROR_ACCESS_DENIED);
        }

        const ULONGLONG deadline = GetTickCount64() + timeoutMs;
        for (;;)
        {
            if (DeleteProfileW(std::wstring(accountSid).c_str(), nullptr, nullptr))
            {
                return true;
            }
            const DWORD error = GetLastError();
            if (error == ERROR_FILE_NOT_FOUND)
            {
                return true;
            }
            if (error != ERROR_BUSY &&
                error != ERROR_SHARING_VIOLATION &&
                error != ERROR_ACCESS_DENIED)
            {
                throw ptap::win32_error("DeleteProfileW", error);
            }
            if (GetTickCount64() >= deadline)
            {
                return false;
            }
            Sleep(500);
        }
    }

    service_handle open_scm(DWORD access)
    {
        service_handle result(OpenSCManagerW(nullptr, nullptr, access));
        if (!result)
        {
            throw ptap::win32_error("OpenSCManagerW", GetLastError());
        }
        return result;
    }

    service_handle open_service(SC_HANDLE scm, std::wstring_view name, DWORD access)
    {
        std::wstring copy(name);
        service_handle result(OpenServiceW(scm, copy.c_str(), access));
        if (!result)
        {
            throw ptap::win32_error("OpenServiceW", GetLastError());
        }
        return result;
    }

    SERVICE_STATUS_PROCESS query_service(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS status{};
        DWORD bytes = 0;
        ptap::check_bool(
            QueryServiceStatusEx(
                service,
                SC_STATUS_PROCESS_INFO,
                reinterpret_cast<LPBYTE>(&status),
                sizeof(status),
                &bytes),
            "QueryServiceStatusEx");
        return status;
    }

    SERVICE_STATUS_PROCESS wait_for_service_state(SC_HANDLE service, DWORD desired, DWORD timeoutMs)
    {
        const ULONGLONG deadline = GetTickCount64() + timeoutMs;
        for (;;)
        {
            const auto status = query_service(service);
            if (status.dwCurrentState == desired)
            {
                return status;
            }
            if (desired == SERVICE_RUNNING && status.dwCurrentState == SERVICE_STOPPED)
            {
                throw ptap::win32_error(
                    "Service stopped during startup",
                    status.dwWin32ExitCode ? status.dwWin32ExitCode : ERROR_SERVICE_NOT_ACTIVE);
            }
            if (GetTickCount64() >= deadline)
            {
                throw ptap::win32_error("Service state wait", ERROR_TIMEOUT);
            }
            Sleep(200);
        }
    }

    void stop_service_if_running(SC_HANDLE service)
    {
        const auto status = query_service(service);
        if (status.dwCurrentState == SERVICE_STOPPED)
        {
            return;
        }
        if (status.dwCurrentState != SERVICE_STOP_PENDING)
        {
            SERVICE_STATUS ignored{};
            if (!ControlService(service, SERVICE_CONTROL_STOP, &ignored))
            {
                const DWORD error = GetLastError();
                if (error != ERROR_SERVICE_NOT_ACTIVE)
                {
                    throw ptap::win32_error("ControlService(stop)", error);
                }
            }
        }
        wait_for_service_state(service, SERVICE_STOPPED, 30000);
    }

    void start_service_and_wait(SC_HANDLE service)
    {
        if (!StartServiceW(service, 0, nullptr))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_SERVICE_ALREADY_RUNNING)
            {
                throw ptap::win32_error("StartServiceW", error);
            }
        }
        wait_for_service_state(service, SERVICE_RUNNING, 30000);
    }

    void wait_for_service_deletion(std::wstring_view serviceName)
    {
        const ULONGLONG deadline = GetTickCount64() + 30000;
        auto scm = open_scm(SC_MANAGER_CONNECT);
        for (;;)
        {
            const std::wstring name(serviceName);
            service_handle service(OpenServiceW(scm.get(), name.c_str(), SERVICE_QUERY_STATUS));
            if (!service)
            {
                const DWORD error = GetLastError();
                if (error == ERROR_SERVICE_DOES_NOT_EXIST)
                {
                    return;
                }
                if (error != ERROR_SERVICE_MARKED_FOR_DELETE)
                {
                    throw ptap::win32_error("OpenServiceW(wait deletion)", error);
                }
            }
            if (GetTickCount64() >= deadline)
            {
                throw ptap::win32_error("Service deletion wait", ERROR_TIMEOUT);
            }
            Sleep(200);
        }
    }

    std::vector<std::byte> pipe_call(
        const ptap::InstanceNames& names,
        ptap::Command command,
        const std::vector<std::byte>& payload)
    {
        if (payload.size() > ptap::MaxProtocolPayload)
        {
            throw ptap::win32_error("Client payload policy", ERROR_INVALID_DATA);
        }
        auto scm = open_scm(SC_MANAGER_CONNECT);
        auto service = open_service(scm.get(), names.serviceName, SERVICE_QUERY_STATUS);
        const auto serviceStatus = query_service(service.get());
        if (serviceStatus.dwCurrentState != SERVICE_RUNNING || serviceStatus.dwProcessId == 0)
        {
            throw ptap::win32_error("Pipe server SCM state", ERROR_SERVICE_NOT_ACTIVE);
        }
        ptap::RequestHeader request;
        request.command = static_cast<uint16_t>(command);
        request.requestId = GetTickCount();
        request.payloadBytes = static_cast<uint32_t>(payload.size());
        std::vector<std::byte> input(sizeof(request) + payload.size());
        memcpy(input.data(), &request, sizeof(request));
        if (!payload.empty())
        {
            memcpy(input.data() + sizeof(request), payload.data(), payload.size());
        }
        if (!WaitNamedPipeW(names.pipeName.c_str(), 10000))
        {
            throw ptap::win32_error("WaitNamedPipeW", GetLastError());
        }
        ptap::unique_handle pipe(CreateFileW(
            names.pipeName.c_str(),
            GENERIC_READ | GENERIC_WRITE,
            0,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | SECURITY_SQOS_PRESENT | SECURITY_IDENTIFICATION,
            nullptr));
        if (!pipe)
        {
            throw ptap::win32_error("CreateFileW(pipe client)", GetLastError());
        }
        ULONG serverProcessId = 0;
        ptap::check_bool(
            GetNamedPipeServerProcessId(pipe.get(), &serverProcessId),
            "GetNamedPipeServerProcessId");
        if (serverProcessId != serviceStatus.dwProcessId)
        {
            throw ptap::win32_error("Named-pipe server PID verification", ERROR_ACCESS_DENIED);
        }
        auto transferExact = [&](bool write, void* buffer, DWORD bytes) {
            DWORD total = 0;
            while (total < bytes)
            {
                DWORD transferred = 0;
                const BOOL success = write ?
                                         WriteFile(
                                             pipe.get(),
                                             static_cast<std::byte*>(buffer) + total,
                                             bytes - total,
                                             &transferred,
                                             nullptr) :
                                         ReadFile(
                                             pipe.get(),
                                             static_cast<std::byte*>(buffer) + total,
                                             bytes - total,
                                             &transferred,
                                             nullptr);
                if (!success || transferred == 0)
                {
                    throw ptap::win32_error(write ? "WriteFile(pipe client)" : "ReadFile(pipe client)", GetLastError());
                }
                total += transferred;
            }
        };
        transferExact(true, input.data(), static_cast<DWORD>(input.size()));
        ptap::ReplyHeader reply{};
        transferExact(false, &reply, sizeof(reply));
        if (reply.magic != ptap::ProtocolMagic ||
            reply.version != ptap::ProtocolVersion ||
            reply.command != request.command ||
            reply.requestId != request.requestId ||
            reply.payloadBytes > ptap::MaxProtocolPayload)
        {
            throw ptap::win32_error("Pipe reply validation", ERROR_INVALID_DATA);
        }
        std::vector<std::byte> result(reply.payloadBytes);
        if (!result.empty())
        {
            transferExact(false, result.data(), static_cast<DWORD>(result.size()));
        }
        if (reply.win32Status != ERROR_SUCCESS)
        {
            throw ptap::win32_error("Service command", reply.win32Status);
        }
        return result;
    }

    ptap::PrototypeState load_owner_state(std::wstring_view ownerSid)
    {
        const auto names = ptap::instance_names(ownerSid);
        const auto state = ptap::read_state(names.statePath);
        const auto stateOwner = ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid));
        const auto accountName = ptap::bounded_string(state.accountName, ARRAYSIZE(state.accountName));
        const auto accountSid = ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
        const auto serviceName = ptap::bounded_string(state.serviceName, ARRAYSIZE(state.serviceName));
        const auto serviceSid = ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid));
        if (stateOwner != ownerSid ||
            accountName != names.accountName ||
            serviceName != names.serviceName ||
            serviceSid != ptap::service_sid(names.serviceName) ||
            accountSid != ptap::sid_for_account(names.accountName))
        {
            throw ptap::win32_error("Protected state identity validation", ERROR_INVALID_DATA);
        }
        const auto desired =
            ptap::bounded_string(state.desiredPackageFullName, ARRAYSIZE(state.desiredPackageFullName));
        const auto lastGood =
            ptap::bounded_string(state.lastGoodPackageFullName, ARRAYSIZE(state.lastGoodPackageFullName));
        if (!desired.empty())
        {
            (void)ptap::validate_package_full_name(desired);
        }
        if (!lastGood.empty())
        {
            (void)ptap::validate_package_full_name(lastGood);
        }
        return state;
    }

    void configure_synced_password(
        const ptap::PrototypeState& state,
        bool updateScm,
        bool startAfter)
    {
        require_elevated();
        const auto accountName = ptap::bounded_string(state.accountName, ARRAYSIZE(state.accountName));
        const auto serviceName = ptap::bounded_string(state.serviceName, ARRAYSIZE(state.serviceName));
        auto password = generate_password();
        auto scm = open_scm(SC_MANAGER_CONNECT);
        auto service = open_service(
            scm.get(),
            serviceName,
            SERVICE_STOP | SERVICE_START | SERVICE_QUERY_STATUS | SERVICE_CHANGE_CONFIG);
        stop_service_if_running(service.get());
        reset_account_password(accountName, password);
        if (updateScm)
        {
            const std::wstring logonName = L".\\" + accountName;
            if (!ChangeServiceConfigW(
                    service.get(),
                    SERVICE_NO_CHANGE,
                    SERVICE_NO_CHANGE,
                    SERVICE_NO_CHANGE,
                    nullptr,
                    nullptr,
                    nullptr,
                    nullptr,
                    logonName.c_str(),
                    password.data(),
                    nullptr))
            {
                throw ptap::win32_error("ChangeServiceConfigW(credentials)", GetLastError());
            }
        }
        if (startAfter)
        {
            start_service_and_wait(service.get());
        }
    }

    void install(const std::vector<std::wstring>& args)
    {
        require_elevated();
        const auto ownerSid = selected_owner_sid(args);
        const auto launcherSource = ptap::argument_value(args, L"--launcher");
        const auto packageFullName = ptap::argument_value(args, L"--package-full-name");
        if (launcherSource.empty() || packageFullName.empty())
        {
            throw ptap::win32_error("install arguments", ERROR_INVALID_PARAMETER);
        }
        const auto packagePolicy = ptap::validate_package_full_name(packageFullName);
        (void)packagePolicy;
        if (!ptap::is_package_staged(packageFullName))
        {
            throw ptap::win32_error("Install exact package is not staged", ERROR_NOT_FOUND);
        }
        const auto launcherCanonical = std::filesystem::weakly_canonical(launcherSource);
        if (!std::filesystem::is_regular_file(launcherCanonical))
        {
            throw ptap::win32_error("Trusted launcher artifact", ERROR_FILE_NOT_FOUND);
        }

        const auto names = ptap::instance_names(ownerSid);
        auto password = generate_password();
        bool accountCreated = false;
        bool serviceCreated = false;
        std::wstring accountSid;
        try
        {
            create_local_account(names.accountName, password);
            accountCreated = true;
            add_account_to_builtin_users(names.accountName);
            accountSid = ptap::sid_for_account(names.accountName);
            add_account_rights(accountSid);
            ensure_profile(accountSid, names.accountName);

            ptap::set_protected_root_acl(names.storeDirectory.parent_path());
            ptap::set_protected_root_acl(names.launcherDirectory.parent_path());
            std::filesystem::create_directories(names.storeDirectory);
            ptap::set_protected_directory_acl(names.storeDirectory, accountSid, ownerSid, true, true);
            std::filesystem::create_directories(names.launcherDirectory);
            ptap::set_protected_directory_acl(names.launcherDirectory, accountSid, L"", true, false);
            if (!CopyFileW(launcherCanonical.c_str(), names.launcherPath.c_str(), FALSE))
            {
                throw ptap::win32_error("CopyFileW(protected launcher)", GetLastError());
            }

            auto scm = open_scm(SC_MANAGER_CREATE_SERVICE);
            const std::wstring imagePath =
                ptap::quote_argument(names.launcherPath.wstring()) +
                L" --service --state " + ptap::quote_argument(names.statePath.wstring());
            const std::wstring logonName = L".\\" + names.accountName;
            service_handle service(CreateServiceW(
                scm.get(),
                names.serviceName.c_str(),
                names.serviceName.c_str(),
                SERVICE_CHANGE_CONFIG | SERVICE_START | SERVICE_STOP | SERVICE_QUERY_STATUS | DELETE,
                SERVICE_WIN32_OWN_PROCESS,
                SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL,
                imagePath.c_str(),
                nullptr,
                nullptr,
                nullptr,
                logonName.c_str(),
                password.data()));
            if (!service)
            {
                throw ptap::win32_error("CreateServiceW", GetLastError());
            }
            serviceCreated = true;
            SERVICE_SID_INFO sidInfo{};
            sidInfo.dwServiceSidType = SERVICE_SID_TYPE_UNRESTRICTED;
            ptap::check_bool(
                ChangeServiceConfig2W(service.get(), SERVICE_CONFIG_SERVICE_SID_INFO, &sidInfo),
                "ChangeServiceConfig2W(SERVICE_SID_INFO)");
            const auto serviceSid = ptap::service_sid(names.serviceName);

            ptap::PrototypeState state;
            ptap::copy_bounded(state.ownerSid, ARRAYSIZE(state.ownerSid), ownerSid);
            ptap::copy_bounded(state.accountName, ARRAYSIZE(state.accountName), names.accountName);
            ptap::copy_bounded(state.accountSid, ARRAYSIZE(state.accountSid), accountSid);
            ptap::copy_bounded(state.serviceSid, ARRAYSIZE(state.serviceSid), serviceSid);
            ptap::copy_bounded(state.serviceName, ARRAYSIZE(state.serviceName), names.serviceName);
            ptap::copy_bounded(
                state.desiredPackageFullName,
                ARRAYSIZE(state.desiredPackageFullName),
                packageFullName);
            ptap::write_state_atomic(names.statePath, state);

            start_service_and_wait(service.get());
            std::wcout << L"Installed " << names.serviceName << L" for owner " << ownerSid << L"\n";
        }
        catch (...)
        {
            const auto logPath = names.storeDirectory / L"prototype.log";
            if (std::filesystem::exists(logPath))
            {
                std::wifstream log(logPath, std::ios::binary);
                std::wcerr << L"protected launcher log before rollback:\n" << log.rdbuf() << L"\n";
            }
            if (serviceCreated)
            {
                try
                {
                    auto scm = open_scm(SC_MANAGER_CONNECT);
                    auto service = open_service(scm.get(), names.serviceName, DELETE | SERVICE_STOP | SERVICE_QUERY_STATUS);
                    try
                    {
                        stop_service_if_running(service.get());
                    }
                    catch (const std::exception& error)
                    {
                        std::cerr << "rollback warning: could not stop partial service: " << error.what() << "\n";
                    }
                    if (!DeleteService(service.get()))
                    {
                        std::cerr << "rollback warning: DeleteService failed with " << GetLastError() << "\n";
                    }
                    service = {};
                    wait_for_service_deletion(names.serviceName);
                }
                catch (const std::exception& error)
                {
                    std::cerr << "rollback warning: could not open partial service: " << error.what() << "\n";
                }
            }
            if (accountCreated)
            {
                if (!accountSid.empty())
                {
                    try
                    {
                        remove_account_rights(accountSid);
                    }
                    catch (const std::exception& error)
                    {
                        std::cerr << "rollback warning: account-right cleanup failed: " << error.what() << "\n";
                    }
                }
                const NET_API_STATUS deleteUser = NetUserDel(nullptr, names.accountName.c_str());
                if (deleteUser != NERR_Success && deleteUser != NERR_UserNotFound)
                {
                    std::cerr << "rollback warning: account deletion failed with " << deleteUser << "\n";
                }
                if (!accountSid.empty())
                {
                    try
                    {
                        if (!delete_profile_with_retry(ownerSid, accountSid, 30000))
                        {
                            std::wcerr << L"ROLLBACK_PROFILE_CLEANUP_PENDING owner=" << ownerSid
                                       << L" accountSid=" << accountSid << L"\n";
                        }
                    }
                    catch (const std::exception&)
                    {
                        std::wcerr << L"ROLLBACK_PROFILE_CLEANUP_PENDING owner=" << ownerSid
                                   << L" accountSid=" << accountSid << L"\n";
                    }
                }
            }
            std::error_code ignored;
            std::filesystem::remove_all(names.launcherDirectory, ignored);
            if (ignored)
            {
                std::cerr << "rollback warning: launcher cleanup failed: " << ignored.message() << "\n";
            }
            ignored.clear();
            std::filesystem::remove_all(names.storeDirectory, ignored);
            if (ignored)
            {
                std::cerr << "rollback warning: store cleanup failed: " << ignored.message() << "\n";
            }
            throw;
        }
    }

    void status(const std::vector<std::wstring>& args)
    {
        const auto ownerSid = selected_owner_sid(args);
        const auto names = ptap::instance_names(ownerSid);
        const auto state = load_owner_state(ownerSid);
        std::wcout << L"accountSid="
                   << ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid))
                   << L"\nserviceSid="
                   << ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid))
                   << L"\n";
        auto scm = open_scm(SC_MANAGER_CONNECT);
        auto service = open_service(scm.get(), names.serviceName, SERVICE_QUERY_STATUS);
        const auto scmStatus = query_service(service.get());
        std::wcout << L"service=" << names.serviceName << L" scmState=" << scmStatus.dwCurrentState
                   << L" pid=" << scmStatus.dwProcessId << L" serviceExit=" << scmStatus.dwWin32ExitCode << L"\n";
        if (scmStatus.dwCurrentState == SERVICE_RUNNING)
        {
            const auto reply = pipe_call(names, ptap::Command::Status, {});
            if (reply.size() != sizeof(ptap::StatusPayload))
            {
                throw ptap::win32_error("Status payload size", ERROR_INVALID_DATA);
            }
            ptap::StatusPayload payload{};
            memcpy(&payload, reply.data(), sizeof(payload));
            std::wcout << L"workerPid=" << payload.workerPid
                       << L" lastError=" << payload.lastWin32Error
                       << L" desiredVersion=0x" << std::hex << payload.desiredVersion
                       << L" lastGoodVersion=0x" << payload.lastGoodVersion << std::dec
                       << L" package=" << ptap::bounded_string(payload.packageFullName, ARRAYSIZE(payload.packageFullName))
                       << L"\n";
        }
        if (std::filesystem::exists(names.evidencePath))
        {
            const auto evidence = ptap::read_evidence(names.evidencePath);
            const auto evidencePackage =
                ptap::bounded_string(evidence.packageFullName, ARRAYSIZE(evidence.packageFullName));
            const auto evidenceFamily =
                ptap::bounded_string(evidence.packageFamilyName, ARRAYSIZE(evidence.packageFamilyName));
            const auto evidenceUser = ptap::bounded_string(evidence.userSid, ARRAYSIZE(evidence.userSid));
            const auto evidenceServiceSid =
                ptap::bounded_string(evidence.serviceSid, ARRAYSIZE(evidence.serviceSid));
            const auto lastGood =
                ptap::bounded_string(state.lastGoodPackageFullName, ARRAYSIZE(state.lastGoodPackageFullName));
            const auto expectedUser = ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
            const auto expectedServiceSid = ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid));
            if (evidencePackage != lastGood ||
                evidenceFamily != ptap::expected_package_family_name() ||
                evidenceUser != expectedUser ||
                evidenceServiceSid != expectedServiceSid ||
                evidence.hasExpectedServiceSid != 1)
            {
                throw ptap::win32_error("Evidence identity verification", ERROR_INVALID_DATA);
            }
            std::wcout << L"evidence pid=" << evidence.processId
                       << L" session=" << evidence.sessionId
                       << L" package=" << evidencePackage
                       << L" family="
                       << evidenceFamily
                       << L" user=" << evidenceUser
                       << L" serviceSidPresent=" << evidence.hasExpectedServiceSid
                       << L" launchCount=" << evidence.launchCount << L"\n";
        }
    }

    void ensure_package(const std::vector<std::wstring>& args)
    {
        const auto ownerSid = selected_owner_sid(args);
        const auto package = ptap::argument_value(args, L"--package-full-name");
        if (package.empty())
        {
            throw ptap::win32_error("ensure-package arguments", ERROR_INVALID_PARAMETER);
        }
        const auto packagePolicy = ptap::validate_package_full_name(package);
        (void)packagePolicy;
        std::vector<std::byte> payload((package.size() + 1) * sizeof(wchar_t));
        memcpy(payload.data(), package.c_str(), payload.size());
        pipe_call(ptap::instance_names(ownerSid), ptap::Command::EnsurePackage, payload);
        std::wcout << L"Ensured exact package " << package << L"\n";
    }

    void unstage_package(const std::vector<std::wstring>& args)
    {
        require_elevated();
        const auto package = ptap::argument_value(args, L"--package-full-name");
        if (package.empty())
        {
            throw ptap::win32_error("unstage-package arguments", ERROR_INVALID_PARAMETER);
        }
        const auto policy = ptap::validate_package_full_name(package);
        (void)policy;
        winrt::init_apartment(winrt::apartment_type::multi_threaded);
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto result = manager.RemovePackageAsync(
                                       package,
                                       winrt::Windows::Management::Deployment::RemovalOptions::RemoveForAllUsers)
                                .get();
        const HRESULT removalError = result.ExtendedErrorCode();
        if (FAILED(removalError) &&
            HRESULT_CODE(removalError) != ERROR_NOT_FOUND &&
            HRESULT_CODE(removalError) != APPMODEL_ERROR_NO_PACKAGE)
        {
            throw ptap::win32_error("RemovePackageAsync(RemoveForAllUsers)", HRESULT_CODE(removalError));
        }
        const ULONGLONG deadline = GetTickCount64() + 30000;
        while (ptap::is_package_staged(package) && GetTickCount64() < deadline)
        {
            Sleep(200);
        }
        if (ptap::is_package_staged(package))
        {
            throw ptap::win32_error("Package remained staged after removal", ERROR_BUSY);
        }
        std::wcout << L"Removed exact staged prototype package " << package << L"\n";
    }

    void package_status()
    {
        require_elevated();
        winrt::init_apartment(winrt::apartment_type::multi_threaded);
        winrt::Windows::Management::Deployment::PackageManager manager;
        for (const auto& package : manager.FindPackages(ptap::PackageName, ptap::PackagePublisher))
        {
            const auto fullName = std::wstring(package.Id().FullName());
            std::wcout << L"package=" << fullName
                       << L" staged=" << (ptap::is_package_staged(fullName) ? 1 : 0)
                       << L" statusOk=" << (package.Status().VerifyIsOK() ? 1 : 0)
                       << L" path=" << package.InstalledLocation().Path().c_str() << L"\n";
            for (const auto& user : manager.FindUsers(fullName))
            {
                std::wcout << L"  user=" << user.UserSecurityId().c_str()
                           << L" state=" << static_cast<uint32_t>(user.InstallState()) << L"\n";
            }
        }
    }

    void simple_pipe_command(const std::vector<std::wstring>& args, ptap::Command command)
    {
        const auto ownerSid = selected_owner_sid(args);
        pipe_call(ptap::instance_names(ownerSid), command, {});
    }

    void tamper_alias(const std::vector<std::wstring>& args)
    {
        require_elevated();
        const auto ownerSid = selected_owner_sid(args);
        const auto state = load_owner_state(ownerSid);
        simple_pipe_command(args, ptap::Command::StopWorker);
        const auto accountSid = ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
        HKEY rawKey = nullptr;
        const std::wstring keyPath =
            L"SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\ProfileList\\" + accountSid;
        ptap::check_lstatus(
            RegOpenKeyExW(HKEY_LOCAL_MACHINE, keyPath.c_str(), 0, KEY_QUERY_VALUE, &rawKey),
            "RegOpenKeyExW(profile)");
        struct key_guard
        {
            HKEY value;
            ~key_guard()
            {
                RegCloseKey(value);
            }
        } key{ rawKey };
        wchar_t profile[MAX_PATH]{};
        DWORD type = 0;
        DWORD bytes = sizeof(profile);
        ptap::check_lstatus(
            RegQueryValueExW(
                rawKey,
                L"ProfileImagePath",
                nullptr,
                &type,
                reinterpret_cast<LPBYTE>(profile),
                &bytes),
            "RegQueryValueExW(ProfileImagePath)");
        if (type != REG_SZ && type != REG_EXPAND_SZ)
        {
            throw ptap::win32_error("Profile path type", ERROR_INVALID_DATA);
        }
        wchar_t expanded[MAX_PATH]{};
        if (!ExpandEnvironmentStringsW(profile, expanded, ARRAYSIZE(expanded)))
        {
            throw ptap::win32_error("ExpandEnvironmentStringsW", GetLastError());
        }
        const auto alias =
            std::filesystem::path(expanded) / L"AppData\\Local\\Microsoft\\WindowsApps" / ptap::AliasName;
        std::wstring modulePath(32768, L'\0');
        const DWORD moduleChars =
            GetModuleFileNameW(nullptr, modulePath.data(), static_cast<DWORD>(modulePath.size()));
        if (moduleChars == 0 || moduleChars >= modulePath.size())
        {
            throw ptap::win32_error("GetModuleFileNameW", GetLastError());
        }
        modulePath.resize(moduleChars);
        const auto ordinaryExe =
            std::filesystem::path(modulePath).parent_path() / L"PtAliasProtoWorker.exe";
        if (!std::filesystem::is_regular_file(ordinaryExe))
        {
            throw ptap::win32_error("Unpackaged tamper probe artifact", ERROR_FILE_NOT_FOUND);
        }
        DeleteFileW(alias.c_str());
        if (!CopyFileW(ordinaryExe.c_str(), alias.c_str(), FALSE))
        {
            throw ptap::win32_error("CopyFileW(tampered alias)", GetLastError());
        }
        std::wcout << L"Replaced exact alias leaf with the unpackaged worker tamper probe: " << alias << L"\n";
    }

    void remove_exact_tree(const std::filesystem::path& path, std::wstring_view expectedLeaf)
    {
        const auto canonical = std::filesystem::weakly_canonical(path);
        if (canonical.filename() != expectedLeaf || expectedLeaf.size() != 8)
        {
            throw ptap::win32_error("Exact cleanup path policy", ERROR_ACCESS_DENIED);
        }
        std::error_code error;
        std::filesystem::remove_all(canonical, error);
        if (error)
        {
            throw ptap::win32_error("remove_all(exact prototype path)", error.value());
        }
    }

    void uninstall(const std::vector<std::wstring>& args)
    {
        require_elevated();
        const auto ownerSid = selected_owner_sid(args);
        const auto names = ptap::instance_names(ownerSid);
        std::optional<ptap::PrototypeState> state;
        try
        {
            state = load_owner_state(ownerSid);
        }
        catch (const std::exception& error)
        {
            std::cerr << "uninstall warning: protected state is unavailable: " << error.what() << "\n";
        }
        const std::wstring accountName = state ?
                                             ptap::bounded_string(
                                                 state->accountName,
                                                 ARRAYSIZE(state->accountName)) :
                                             names.accountName;
        std::wstring accountSid;
        if (state)
        {
            accountSid = ptap::bounded_string(state->accountSid, ARRAYSIZE(state->accountSid));
        }
        else
        {
            try
            {
                accountSid = ptap::sid_for_account(accountName);
            }
            catch (const ptap::win32_error& error)
            {
                if (error.code() != ERROR_NONE_MAPPED)
                {
                    throw;
                }
            }
        }
        auto scm = open_scm(SC_MANAGER_CONNECT);
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_START | SERVICE_STOP | SERVICE_QUERY_STATUS | SERVICE_CHANGE_CONFIG | DELETE));
        if (!service && GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
        {
            throw ptap::win32_error("OpenServiceW(uninstall)", GetLastError());
        }
        bool registrationCleanupPending = false;
        if (service && state)
        {
            try
            {
                if (query_service(service.get()).dwCurrentState != SERVICE_RUNNING)
                {
                    configure_synced_password(*state, true, true);
                }
                pipe_call(names, ptap::Command::CleanupRegistration, {});
            }
            catch (const std::exception& error)
            {
                registrationCleanupPending = true;
                std::cerr << "uninstall warning: package registration cleanup is pending: "
                          << error.what() << "\n";
            }
        }
        else if (service)
        {
            registrationCleanupPending = true;
        }
        if (service)
        {
            try
            {
                stop_service_if_running(service.get());
            }
            catch (const std::exception& error)
            {
                std::cerr << "uninstall warning: service stop did not complete: " << error.what() << "\n";
            }
            if (!DeleteService(service.get()) && GetLastError() != ERROR_SERVICE_MARKED_FOR_DELETE)
            {
                throw ptap::win32_error("DeleteService", GetLastError());
            }
            service = {};
            wait_for_service_deletion(names.serviceName);
        }
        if (!accountSid.empty())
        {
            remove_account_rights(accountSid);
        }
        const NET_API_STATUS deleteUser = NetUserDel(nullptr, accountName.c_str());
        if (deleteUser != NERR_Success && deleteUser != NERR_UserNotFound)
        {
            throw ptap::win32_error("NetUserDel", deleteUser);
        }
        const bool profileDeleted =
            accountSid.empty() || delete_profile_with_retry(ownerSid, accountSid, 30000);
        remove_exact_tree(names.launcherDirectory, names.suffix);
        remove_exact_tree(names.storeDirectory, names.suffix);
        for (const auto& root : { names.launcherDirectory.parent_path(), names.storeDirectory.parent_path() })
        {
            std::error_code error;
            if (std::filesystem::is_directory(root, error) &&
                !error &&
                std::filesystem::is_empty(root, error) &&
                !error)
            {
                std::filesystem::remove(root, error);
            }
        }
        std::wcout << L"Uninstalled exact prototype instance " << names.suffix << L"\n";
        if (registrationCleanupPending)
        {
            std::wcout << L"PACKAGE_CLEANUP_PENDING owner=" << ownerSid
                       << L" (exact staged-package cleanup remains available to the elevated harness)\n";
        }
        if (!profileDeleted)
        {
            std::wcout << L"PROFILE_CLEANUP_PENDING owner=" << ownerSid
                       << L" accountSid=" << accountSid
                       << L" (retry cleanup-profile after reboot)\n";
        }
    }

    void cleanup_profile(const std::vector<std::wstring>& args)
    {
        require_elevated();
        const auto ownerSid = selected_owner_sid(args);
        const auto accountSid = ptap::argument_value(args, L"--account-sid");
        if (accountSid.empty())
        {
            throw ptap::win32_error("cleanup-profile arguments", ERROR_INVALID_PARAMETER);
        }
        validate_sid(accountSid);
        const DWORD timeout = ptap::has_argument(args, L"--no-wait") ? 0 : 30000;
        if (!delete_profile_with_retry(ownerSid, accountSid, timeout))
        {
            throw ptap::win32_error("DeleteProfileW remains pending", ERROR_BUSY);
        }
        remove_account_rights(accountSid);
        std::wcout << L"Removed exact prototype profile " << accountSid << L"\n";
    }

    void print_usage()
    {
        std::wcout
            << L"PtAliasProtoController commands:\n"
            << L"  install --launcher <path> --package-full-name <fullName> [--owner-sid <SID>]\n"
            << L"  status [--owner-sid <SID>]\n"
            << L"  ensure-package --package-full-name <fullName> [--owner-sid <SID>]\n"
            << L"  stop-worker | unregister [--owner-sid <SID>]\n"
            << L"  unstage-package --package-full-name <fullName>\n"
            << L"  cleanup-profile --account-sid <SID> [--owner-sid <SID>] [--no-wait]\n"
            << L"  package-status\n"
            << L"  rotate | repair | break-1069 [--owner-sid <SID>]\n"
            << L"  tamper-alias | uninstall [--owner-sid <SID>]\n";
    }
}

int wmain()
{
    try
    {
        const auto args = ptap::command_line_arguments();
        if (args.size() < 2)
        {
            print_usage();
            return ERROR_INVALID_PARAMETER;
        }
        const auto& command = args[1];
        if (command == L"install")
        {
            install(args);
        }
        else if (command == L"status")
        {
            status(args);
        }
        else if (command == L"ensure-package")
        {
            ensure_package(args);
        }
        else if (command == L"stop-worker")
        {
            simple_pipe_command(args, ptap::Command::StopWorker);
        }
        else if (command == L"unregister")
        {
            simple_pipe_command(args, ptap::Command::CleanupRegistration);
        }
        else if (command == L"unstage-package")
        {
            unstage_package(args);
        }
        else if (command == L"cleanup-profile")
        {
            cleanup_profile(args);
        }
        else if (command == L"package-status")
        {
            package_status();
        }
        else if (command == L"rotate" || command == L"repair")
        {
            const auto ownerSid = selected_owner_sid(args);
            configure_synced_password(load_owner_state(ownerSid), true, true);
            std::wcout << L"Reset account password and synchronized SCM credentials.\n";
        }
        else if (command == L"break-1069")
        {
            require_elevated();
            const auto ownerSid = selected_owner_sid(args);
            const auto state = load_owner_state(ownerSid);
            configure_synced_password(state, false, false);
            auto scm = open_scm(SC_MANAGER_CONNECT);
            auto service = open_service(
                scm.get(),
                ptap::bounded_string(state.serviceName, ARRAYSIZE(state.serviceName)),
                SERVICE_START | SERVICE_QUERY_STATUS);
            if (StartServiceW(service.get(), 0, nullptr))
            {
                throw ptap::win32_error("Expected service logon failure did not occur", ERROR_INVALID_STATE);
            }
            const DWORD error = GetLastError();
            if (error != ERROR_SERVICE_LOGON_FAILED)
            {
                throw ptap::win32_error("Expected ERROR_SERVICE_LOGON_FAILED", error);
            }
            std::wcout << L"Reproduced SCM error 1069 without exposing either password.\n";
        }
        else if (command == L"tamper-alias")
        {
            tamper_alias(args);
        }
        else if (command == L"uninstall")
        {
            uninstall(args);
        }
        else
        {
            print_usage();
            return ERROR_INVALID_PARAMETER;
        }
        return 0;
    }
    catch (const ptap::win32_error& error)
    {
        std::wcerr << L"error " << error.code() << L": " << ptap::format_error(error.code())
                   << L" (" << error.what() << L")\n";
        return static_cast<int>(error.code());
    }
    catch (const std::exception& error)
    {
        std::cerr << "error: " << error.what() << "\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
