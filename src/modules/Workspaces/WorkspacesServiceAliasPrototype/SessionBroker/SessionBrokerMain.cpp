#include "../Common/ProtoCommon.h"

#include <userenv.h>
#include <wtsapi32.h>

#include <filesystem>
#include <string>

#pragma comment(lib, "userenv.lib")
#pragma comment(lib, "wtsapi32.lib")

namespace
{
    std::filesystem::path g_statePath;
    std::wstring g_serviceName;
    DWORD g_targetSession{};
    SERVICE_STATUS_HANDLE g_statusHandle{};
    SERVICE_STATUS g_status{};
    HANDLE g_serviceStopEvent{};

    std::wstring widen(const char* value)
    {
        if (!value)
        {
            return {};
        }
        const int chars = MultiByteToWideChar(CP_UTF8, 0, value, -1, nullptr, 0);
        if (chars <= 1)
        {
            return L"native error";
        }
        std::wstring result(chars, L'\0');
        MultiByteToWideChar(CP_UTF8, 0, value, -1, result.data(), chars);
        result.resize(static_cast<size_t>(chars) - 1);
        return result;
    }

    class service_handle
    {
    public:
        explicit service_handle(SC_HANDLE value = nullptr) noexcept :
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

        [[nodiscard]] SC_HANDLE get() const noexcept
        {
            return m_value;
        }

        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        SC_HANDLE m_value{};
    };

    class environment_block
    {
    public:
        ~environment_block()
        {
            if (m_value)
            {
                DestroyEnvironmentBlock(m_value);
            }
        }

        [[nodiscard]] void** address() noexcept
        {
            return &m_value;
        }

        [[nodiscard]] void* get() const noexcept
        {
            return m_value;
        }

    private:
        void* m_value{};
    };

    class window_station_handle
    {
    public:
        explicit window_station_handle(HWINSTA value = nullptr) noexcept :
            m_value(value)
        {
        }

        ~window_station_handle()
        {
            if (m_value)
            {
                CloseWindowStation(m_value);
            }
        }

        window_station_handle(const window_station_handle&) = delete;
        window_station_handle& operator=(const window_station_handle&) = delete;

        [[nodiscard]] HWINSTA get() const noexcept
        {
            return m_value;
        }

        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        HWINSTA m_value{};
    };

    class desktop_handle
    {
    public:
        explicit desktop_handle(HDESK value = nullptr) noexcept :
            m_value(value)
        {
        }

        ~desktop_handle()
        {
            if (m_value)
            {
                CloseDesktop(m_value);
            }
        }

        desktop_handle(const desktop_handle&) = delete;
        desktop_handle& operator=(const desktop_handle&) = delete;

        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        HDESK m_value{};
    };

    void set_service_status(DWORD state, DWORD error = ERROR_SUCCESS, DWORD waitHint = 0)
    {
        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = error;
        g_status.dwWaitHint = waitHint;
        g_status.dwControlsAccepted =
            state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        if (!SetServiceStatus(g_statusHandle, &g_status))
        {
            throw ptap::win32_error("SetServiceStatus", GetLastError());
        }
    }

    void enable_privilege(const wchar_t* name)
    {
        HANDLE rawToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_ADJUST_PRIVILEGES, &rawToken),
            "OpenProcessToken(privileges)");
        ptap::unique_handle token(rawToken);
        LUID luid{};
        ptap::check_bool(LookupPrivilegeValueW(nullptr, name, &luid), "LookupPrivilegeValueW");
        TOKEN_PRIVILEGES privileges{};
        privileges.PrivilegeCount = 1;
        privileges.Privileges[0].Luid = luid;
        privileges.Privileges[0].Attributes = SE_PRIVILEGE_ENABLED;
        SetLastError(ERROR_SUCCESS);
        ptap::check_bool(
            AdjustTokenPrivileges(token.get(), FALSE, &privileges, sizeof(privileges), nullptr, nullptr),
            "AdjustTokenPrivileges");
        if (GetLastError() == ERROR_NOT_ALL_ASSIGNED)
        {
            throw ptap::win32_error("AdjustTokenPrivileges", ERROR_PRIVILEGE_NOT_HELD);
        }
    }

    std::wstring package_full_name_from_process(HANDLE process)
    {
        UINT32 chars = 0;
        LONG result = GetPackageFullName(process, &chars, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptap::win32_error("GetPackageFullName(size)", result);
        }
        std::wstring value(chars, L'\0');
        result = GetPackageFullName(process, &chars, value.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptap::win32_error("GetPackageFullName", result);
        }
        value.resize(chars - 1);
        return value;
    }

    std::wstring package_family_from_process(HANDLE process)
    {
        UINT32 chars = 0;
        LONG result = GetPackageFamilyName(process, &chars, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptap::win32_error("GetPackageFamilyName(size)", result);
        }
        std::wstring value(chars, L'\0');
        result = GetPackageFamilyName(process, &chars, value.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptap::win32_error("GetPackageFamilyName", result);
        }
        value.resize(chars - 1);
        return value;
    }

    DWORD token_session_id(HANDLE token)
    {
        DWORD sessionId = 0;
        DWORD bytes = 0;
        ptap::check_bool(
            GetTokenInformation(token, TokenSessionId, &sessionId, sizeof(sessionId), &bytes),
            "GetTokenInformation(TokenSessionId)");
        return sessionId;
    }

    std::filesystem::path profile_directory_for_token(HANDLE token)
    {
        DWORD required = 0;
        GetUserProfileDirectoryW(token, nullptr, &required);
        if (required == 0 || GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptap::win32_error("GetUserProfileDirectoryW(size)", GetLastError());
        }
        std::wstring value(required, L'\0');
        ptap::check_bool(
            GetUserProfileDirectoryW(token, value.data(), &required),
            "GetUserProfileDirectoryW");
        value.resize(wcslen(value.c_str()));
        return std::filesystem::path(value);
    }

    ptap::unique_handle duplicate_anchor_token(
        const ptap::PrototypeState& state,
        const ptap::InstanceNames& names)
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptap::win32_error("OpenSCManagerW(anchor)", GetLastError());
        }
        service_handle service(OpenServiceW(scm.get(), names.serviceName.c_str(), SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw ptap::win32_error("OpenServiceW(anchor)", GetLastError());
        }
        SERVICE_STATUS_PROCESS status{};
        DWORD bytes = 0;
        ptap::check_bool(
            QueryServiceStatusEx(
                service.get(),
                SC_STATUS_PROCESS_INFO,
                reinterpret_cast<BYTE*>(&status),
                sizeof(status),
                &bytes),
            "QueryServiceStatusEx(anchor)");
        if (status.dwCurrentState != SERVICE_RUNNING || status.dwProcessId == 0)
        {
            throw ptap::win32_error("Anchor service state", ERROR_SERVICE_NOT_ACTIVE);
        }

        ptap::unique_handle process(OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, FALSE, status.dwProcessId));
        if (!process)
        {
            throw ptap::win32_error("OpenProcess(anchor)", GetLastError());
        }
        HANDLE rawToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(process.get(), TOKEN_QUERY | TOKEN_DUPLICATE, &rawToken),
            "OpenProcessToken(anchor)");
        ptap::unique_handle anchorToken(rawToken);
        const auto accountSid = ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
        const auto serviceSid = ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid));
        if (ptap::token_user_sid(anchorToken.get()) != accountSid ||
            !ptap::token_contains_sid(anchorToken.get(), serviceSid))
        {
            throw ptap::win32_error("Anchor token identity", ERROR_ACCESS_DENIED);
        }

        HANDLE rawDuplicate = nullptr;
        ptap::check_bool(
            DuplicateTokenEx(
                anchorToken.get(),
                TOKEN_QUERY | TOKEN_DUPLICATE | TOKEN_ASSIGN_PRIMARY |
                    TOKEN_ADJUST_SESSIONID | TOKEN_ADJUST_DEFAULT,
                nullptr,
                SecurityIdentification,
                TokenPrimary,
                &rawDuplicate),
            "DuplicateTokenEx(anchor)");
        return ptap::unique_handle(rawDuplicate);
    }

    ptap::unique_handle duplicate_current_primary_token()
    {
        HANDLE rawToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY | TOKEN_DUPLICATE, &rawToken),
            "OpenProcessToken(current)");
        ptap::unique_handle token(rawToken);
        HANDLE rawDuplicate = nullptr;
        ptap::check_bool(
            DuplicateTokenEx(
                token.get(),
                TOKEN_QUERY |
                    TOKEN_ASSIGN_PRIMARY |
                    TOKEN_DUPLICATE |
                    TOKEN_ADJUST_DEFAULT |
                    TOKEN_ADJUST_SESSIONID,
                nullptr,
                SecurityIdentification,
                TokenPrimary,
                &rawDuplicate),
            "DuplicateTokenEx(current)");
        return ptap::unique_handle(rawDuplicate);
    }

    std::filesystem::path current_module_path()
    {
        std::wstring path(32768, L'\0');
        const DWORD chars = GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (chars == 0 || chars >= path.size())
        {
            throw ptap::win32_error("GetModuleFileNameW", GetLastError());
        }
        path.resize(chars);
        return std::filesystem::path(path);
    }

    bool verify_packaged_process(
        HANDLE process,
        const ptap::PrototypeState& state,
        const ptap::PackageIdentity& identity,
        DWORD expectedSession)
    {
        HANDLE rawToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(process, TOKEN_QUERY, &rawToken),
            "OpenProcessToken(packaged worker)");
        ptap::unique_handle token(rawToken);
        return package_full_name_from_process(process) == identity.fullName &&
               package_family_from_process(process) == identity.familyName &&
               ptap::token_user_sid(token.get()) ==
                   ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid)) &&
               ptap::token_contains_sid(
                   token.get(),
                   ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid))) &&
               token_session_id(token.get()) == expectedSession;
    }

    int run_account_bridge(const std::vector<std::wstring>& args)
    {
        const auto stateArgument = ptap::argument_value(args, L"--state");
        const auto targetSessionArgument = ptap::argument_value(args, L"--target-session");
        const auto bridgeReadyName = ptap::argument_value(args, L"--ready-event");
        const auto bridgeStopName = ptap::argument_value(args, L"--stop-event");
        const auto aliasArgument = ptap::argument_value(args, L"--alias-path");
        const auto bridgeReadyHandleArgument =
            ptap::argument_value(args, L"--ready-handle");
        const auto bridgeStopHandleArgument =
            ptap::argument_value(args, L"--stop-handle");
        if (stateArgument.empty() ||
            targetSessionArgument.empty() ||
            bridgeReadyName.empty() ||
            bridgeStopName.empty() ||
            aliasArgument.empty() ||
            bridgeReadyHandleArgument.empty() ||
            bridgeStopHandleArgument.empty())
        {
            return ERROR_INVALID_PARAMETER;
        }
        wchar_t* end = nullptr;
        const unsigned long parsedSession = wcstoul(targetSessionArgument.c_str(), &end, 10);
        if (!end || *end != L'\0' || parsedSession == 0)
        {
            return ERROR_INVALID_PARAMETER;
        }
        const DWORD targetSession = static_cast<DWORD>(parsedSession);
        const auto statePath = std::filesystem::weakly_canonical(stateArgument);
        const auto state = ptap::read_state(statePath);
        const auto names = ptap::instance_names(
            ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid)));
        const std::filesystem::path alias =
            std::filesystem::absolute(aliasArgument).lexically_normal();
        if (alias.filename() != ptap::AliasName ||
            alias.parent_path().filename() != L"WindowsApps" ||
            alias.parent_path().parent_path().filename() != L"Microsoft")
        {
            return ERROR_INVALID_DATA;
        }
        wchar_t* readyHandleEnd = nullptr;
        wchar_t* stopHandleEnd = nullptr;
        const unsigned long long readyHandleValue =
            _wcstoui64(bridgeReadyHandleArgument.c_str(), &readyHandleEnd, 10);
        const unsigned long long stopHandleValue =
            _wcstoui64(bridgeStopHandleArgument.c_str(), &stopHandleEnd, 10);
        if (!readyHandleEnd ||
            *readyHandleEnd != L'\0' ||
            !stopHandleEnd ||
            *stopHandleEnd != L'\0' ||
            readyHandleValue == 0 ||
            stopHandleValue == 0)
        {
            return ERROR_INVALID_HANDLE;
        }
        HANDLE rawSelfToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawSelfToken),
            "OpenProcessToken(account bridge)");
        ptap::unique_handle selfToken(rawSelfToken);
        if (!std::filesystem::equivalent(statePath, names.statePath) ||
            ptap::token_user_sid(selfToken.get()) !=
                ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid)) ||
            !ptap::token_contains_sid(
                selfToken.get(),
                ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid))) ||
            token_session_id(selfToken.get()) != targetSession ||
            !bridgeReadyName.starts_with(L"Global\\PtAliasProtoBridgeReady_" + names.suffix + L"_") ||
            !bridgeStopName.starts_with(L"Global\\PtAliasProtoBridgeStop_" + names.suffix + L"_"))
        {
            return ERROR_ACCESS_DENIED;
        }
        ptap::append_log(
            names.storeDirectory,
            L"account-bridge",
            L"entered target session " + std::to_wstring(targetSession));

        const auto packageFullName =
            ptap::bounded_string(
                state.lastGoodPackageFullName,
                ARRAYSIZE(state.lastGoodPackageFullName));
        const auto identity = ptap::validate_package_full_name(packageFullName);
        ptap::unique_handle ready(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        ptap::unique_handle stop(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        ptap::unique_handle bridgeReady(
            reinterpret_cast<HANDLE>(static_cast<uintptr_t>(readyHandleValue)));
        ptap::unique_handle bridgeStop(
            reinterpret_cast<HANDLE>(static_cast<uintptr_t>(stopHandleValue)));
        if (!ready)
        {
            throw ptap::win32_error("CreateEventW(account ready)", GetLastError());
        }
        if (!stop)
        {
            throw ptap::win32_error("CreateEventW(account stop)", GetLastError());
        }
        ptap::check_bool(
            SetHandleInformation(ready.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT),
            "SetHandleInformation(worker ready)");
        ptap::check_bool(
            SetHandleInformation(stop.get(), HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT),
            "SetHandleInformation(worker stop)");
        if (!bridgeReady || !bridgeStop)
        {
            throw ptap::win32_error("Inherited account bridge event value", ERROR_INVALID_HANDLE);
        }
        DWORD inheritedHandleFlags = 0;
        if (!GetHandleInformation(bridgeReady.get(), &inheritedHandleFlags) ||
            !GetHandleInformation(bridgeStop.get(), &inheritedHandleFlags))
        {
            throw ptap::win32_error("Inherited account bridge events", GetLastError());
        }
        ptap::unique_handle job(CreateJobObjectW(nullptr, nullptr));
        if (!job)
        {
            throw ptap::win32_error("CreateJobObjectW(account bridge)", GetLastError());
        }
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        ptap::check_bool(
            SetInformationJobObject(
                job.get(),
                JobObjectExtendedLimitInformation,
                &limits,
                sizeof(limits)),
            "SetInformationJobObject(account bridge)");

        ptap::append_log(
            names.storeDirectory,
            L"account-bridge",
            L"using host-resolved alias path");
        std::wstring commandLine =
            ptap::quote_argument(alias.wstring()) +
            L" --config " + ptap::quote_argument(statePath.wstring()) +
            L" --ready-handle " +
                std::to_wstring(reinterpret_cast<uintptr_t>(ready.get())) +
            L" --stop-handle " +
                std::to_wstring(reinterpret_cast<uintptr_t>(stop.get()));
        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(
                alias.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                TRUE,
                CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED | CREATE_NO_WINDOW,
                nullptr,
                nullptr,
                &startup,
                &process))
        {
            const DWORD error = GetLastError();
            ptap::append_log(
                names.storeDirectory,
                L"account-bridge",
                L"CreateProcessW(alias) failed: " + ptap::format_error(error));
            throw ptap::win32_error("CreateProcessW(account bridge alias)", error);
        }
        ptap::append_log(
            names.storeDirectory,
            L"account-bridge",
            L"alias process created, pid=" + std::to_wstring(process.dwProcessId));
        ptap::unique_handle processHandle(process.hProcess);
        ptap::unique_handle threadHandle(process.hThread);
        if (!AssignProcessToJobObject(job.get(), processHandle.get()))
        {
            const DWORD error = GetLastError();
            TerminateProcess(processHandle.get(), error);
            throw ptap::win32_error("AssignProcessToJobObject(account worker)", error);
        }
        bool verified = false;
        for (DWORD attempt = 0; attempt < 20; ++attempt)
        {
            if (WaitForSingleObject(processHandle.get(), 0) == WAIT_OBJECT_0)
            {
                break;
            }
            try
            {
                verified = verify_packaged_process(
                    processHandle.get(),
                    state,
                    identity,
                    targetSession);
                if (verified)
                {
                    break;
                }
            }
            catch (const ptap::win32_error& error)
            {
                if (error.code() != APPMODEL_ERROR_NO_PACKAGE)
                {
                    throw;
                }
            }
            Sleep(50);
        }
        if (!verified)
        {
            DWORD exitCode = STILL_ACTIVE;
            GetExitCodeProcess(processHandle.get(), &exitCode);
            TerminateProcess(processHandle.get(), ERROR_ACCESS_DENIED);
            ptap::append_log(
                names.storeDirectory,
                L"account-bridge",
                L"packaged worker identity failed, process-exit=" +
                    std::to_wstring(exitCode));
            throw ptap::win32_error("Account bridge worker identity", ERROR_ACCESS_DENIED);
        }
        if (ResumeThread(threadHandle.get()) == static_cast<DWORD>(-1))
        {
            const DWORD error = GetLastError();
            TerminateProcess(processHandle.get(), error);
            throw ptap::win32_error("ResumeThread(account worker)", error);
        }
        if (WaitForSingleObject(ready.get(), ptap::WorkerReadyTimeoutMs) != WAIT_OBJECT_0)
        {
            TerminateProcess(processHandle.get(), ERROR_TIMEOUT);
            throw ptap::win32_error("Account bridge worker readiness", ERROR_TIMEOUT);
        }
        const auto evidence = ptap::read_evidence(names.evidencePath);
        if (evidence.processId != process.dwProcessId ||
            evidence.sessionId != targetSession ||
            evidence.hasExpectedServiceSid != 1)
        {
            TerminateProcess(processHandle.get(), ERROR_INVALID_DATA);
            throw ptap::win32_error("Account bridge evidence", ERROR_INVALID_DATA);
        }
        ptap::check_bool(SetEvent(bridgeReady.get()), "SetEvent(account bridge ready)");
        ptap::append_log(
            names.storeDirectory,
            L"account-bridge",
            L"packaged worker ready, pid=" + std::to_wstring(process.dwProcessId));

        HANDLE waits[]{ bridgeStop.get(), processHandle.get() };
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0)
        {
            SetEvent(stop.get());
            if (WaitForSingleObject(processHandle.get(), ptap::WorkerStopTimeoutMs) != WAIT_OBJECT_0)
            {
                TerminateProcess(processHandle.get(), ERROR_PROCESS_ABORTED);
            }
            return ERROR_SUCCESS;
        }
        if (wait == WAIT_OBJECT_0 + 1)
        {
            DWORD exitCode = ERROR_PROCESS_ABORTED;
            GetExitCodeProcess(processHandle.get(), &exitCode);
            return exitCode == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : static_cast<int>(exitCode);
        }
        return GetLastError();
    }

    int run_bridge(const std::vector<std::wstring>& args)
    {
        const auto stateArgument = ptap::argument_value(args, L"--state");
        const auto targetSessionArgument = ptap::argument_value(args, L"--target-session");
        const auto bridgeReadyName = ptap::argument_value(args, L"--ready-event");
        const auto bridgeStopName = ptap::argument_value(args, L"--stop-event");
        if (stateArgument.empty() ||
            targetSessionArgument.empty() ||
            bridgeReadyName.empty() ||
            bridgeStopName.empty())
        {
            return ERROR_INVALID_PARAMETER;
        }
        wchar_t* end = nullptr;
        const unsigned long parsedSession = wcstoul(targetSessionArgument.c_str(), &end, 10);
        if (!end || *end != L'\0' || parsedSession == 0)
        {
            return ERROR_INVALID_PARAMETER;
        }
        const DWORD targetSession = static_cast<DWORD>(parsedSession);
        const auto statePath = std::filesystem::weakly_canonical(stateArgument);
        const auto state = ptap::read_state(statePath);
        const auto ownerSid = ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid));
        const auto names = ptap::instance_names(ownerSid);
        HANDLE rawSelfToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawSelfToken),
            "OpenProcessToken(session host)");
        ptap::unique_handle selfToken(rawSelfToken);
        if (!std::filesystem::equivalent(statePath, names.statePath) ||
            ptap::token_user_sid(selfToken.get()) != L"S-1-5-18" ||
            token_session_id(selfToken.get()) != targetSession ||
            !bridgeReadyName.starts_with(L"Global\\PtAliasProtoBridgeReady_" + names.suffix + L"_") ||
            !bridgeStopName.starts_with(L"Global\\PtAliasProtoBridgeStop_" + names.suffix + L"_"))
        {
            return ERROR_ACCESS_DENIED;
        }
        ptap::append_log(
            names.storeDirectory,
            L"session-host",
            L"LocalSystem helper entered target session " + std::to_wstring(targetSession));

        enable_privilege(SE_TCB_NAME);
        enable_privilege(SE_INCREASE_QUOTA_NAME);
        enable_privilege(SE_ASSIGNPRIMARYTOKEN_NAME);
        auto workerToken = duplicate_anchor_token(state, names);
        DWORD workerSession = targetSession;
        ptap::check_bool(
            SetTokenInformation(
                workerToken.get(),
                TokenSessionId,
                &workerSession,
                sizeof(workerSession)),
            "SetTokenInformation(host worker session)");
        environment_block environment;
        ptap::check_bool(
            CreateEnvironmentBlock(environment.address(), workerToken.get(), FALSE),
            "CreateEnvironmentBlock(host worker)");
        ptap::unique_handle job(CreateJobObjectW(nullptr, nullptr));
        if (!job)
        {
            throw ptap::win32_error("CreateJobObjectW(session host)", GetLastError());
        }
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        ptap::check_bool(
            SetInformationJobObject(
                job.get(),
                JobObjectExtendedLimitInformation,
                &limits,
                sizeof(limits)),
            "SetInformationJobObject(session host)");

        const auto accountSid =
            ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid));
        const auto security = ptap::security_descriptor_from_sddl(
            L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" + accountSid + L")");
        SECURITY_ATTRIBUTES attributes{
            sizeof(SECURITY_ATTRIBUTES),
            security.get(),
            FALSE,
        };
        const std::wstring windowStationName =
            L"PtAliasProto_" + names.suffix + L"_" + ptap::make_nonce();
        window_station_handle windowStation(
            CreateWindowStationW(
                windowStationName.c_str(),
                0,
                WINSTA_ALL_ACCESS,
                &attributes));
        if (!windowStation)
        {
            throw ptap::win32_error("CreateWindowStationW(session host)", GetLastError());
        }
        ptap::check_bool(
            SetProcessWindowStation(windowStation.get()),
            "SetProcessWindowStation(session host)");
        desktop_handle desktop(
            CreateDesktopW(
                L"Default",
                nullptr,
                nullptr,
                0,
                GENERIC_ALL,
                &attributes));
        if (!desktop)
        {
            throw ptap::win32_error("CreateDesktopW(session host)", GetLastError());
        }
        ptap::append_log(
            names.storeDirectory,
            L"session-host",
            L"created private desktop " + windowStationName + L"\\Default");

        const auto executable = current_module_path();
        const auto alias =
            profile_directory_for_token(workerToken.get()) /
            L"AppData" /
            L"Local" /
            L"Microsoft" /
            L"WindowsApps" /
            ptap::AliasName;
        ptap::unique_handle inheritedReady(
            OpenEventW(EVENT_MODIFY_STATE, FALSE, bridgeReadyName.c_str()));
        ptap::unique_handle inheritedStop(
            OpenEventW(SYNCHRONIZE, FALSE, bridgeStopName.c_str()));
        if (!inheritedReady || !inheritedStop)
        {
            throw ptap::win32_error("OpenEventW(session host control)", GetLastError());
        }
        ptap::check_bool(
            SetHandleInformation(
                inheritedReady.get(),
                HANDLE_FLAG_INHERIT,
                HANDLE_FLAG_INHERIT),
            "SetHandleInformation(ready)");
        ptap::check_bool(
            SetHandleInformation(
                inheritedStop.get(),
                HANDLE_FLAG_INHERIT,
                HANDLE_FLAG_INHERIT),
            "SetHandleInformation(stop)");
        std::wstring commandLine =
            ptap::quote_argument(executable.wstring()) +
            L" --account-bridge" +
            L" --state " + ptap::quote_argument(statePath.wstring()) +
            L" --target-session " + std::to_wstring(targetSession) +
            L" --ready-event " + ptap::quote_argument(bridgeReadyName) +
            L" --stop-event " + ptap::quote_argument(bridgeStopName) +
            L" --alias-path " + ptap::quote_argument(alias.wstring()) +
            L" --ready-handle " +
                std::to_wstring(
                    reinterpret_cast<uintptr_t>(inheritedReady.get())) +
            L" --stop-handle " +
                std::to_wstring(
                    reinterpret_cast<uintptr_t>(inheritedStop.get()));
        STARTUPINFOW startup{};
        startup.cb = sizeof(startup);
        std::wstring desktopName = windowStationName + L"\\Default";
        startup.lpDesktop = desktopName.data();
        PROCESS_INFORMATION process{};
        if (!CreateProcessAsUserW(
                workerToken.get(),
                executable.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                TRUE,
                CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED | CREATE_NO_WINDOW,
                environment.get(),
                nullptr,
                &startup,
                &process))
        {
            const DWORD error = GetLastError();
            ptap::append_log(
                names.storeDirectory,
                L"session-host",
                L"CreateProcessAsUserW(account bridge) failed: " + ptap::format_error(error));
            throw ptap::win32_error("CreateProcessAsUserW(account bridge)", error);
        }
        ptap::unique_handle processHandle(process.hProcess);
        ptap::unique_handle threadHandle(process.hThread);
        if (!AssignProcessToJobObject(job.get(), processHandle.get()))
        {
            const DWORD error = GetLastError();
            TerminateProcess(processHandle.get(), error);
            throw ptap::win32_error("AssignProcessToJobObject(account bridge)", error);
        }
        HANDLE rawBridgeToken = nullptr;
        ptap::check_bool(
            OpenProcessToken(processHandle.get(), TOKEN_QUERY, &rawBridgeToken),
            "OpenProcessToken(account bridge)");
        ptap::unique_handle bridgeToken(rawBridgeToken);
        if (ptap::token_user_sid(bridgeToken.get()) !=
                ptap::bounded_string(state.accountSid, ARRAYSIZE(state.accountSid)) ||
            !ptap::token_contains_sid(
                bridgeToken.get(),
                ptap::bounded_string(state.serviceSid, ARRAYSIZE(state.serviceSid))) ||
            token_session_id(bridgeToken.get()) != targetSession)
        {
            TerminateProcess(processHandle.get(), ERROR_ACCESS_DENIED);
            throw ptap::win32_error("Account bridge identity", ERROR_ACCESS_DENIED);
        }
        if (ResumeThread(threadHandle.get()) == static_cast<DWORD>(-1))
        {
            const DWORD error = GetLastError();
            TerminateProcess(processHandle.get(), error);
            throw ptap::win32_error("ResumeThread(account bridge)", error);
        }
        ptap::append_log(
            names.storeDirectory,
            L"session-host",
            L"account bridge entered private desktop, pid=" +
                std::to_wstring(process.dwProcessId));
        WaitForSingleObject(processHandle.get(), INFINITE);
        DWORD exitCode = ERROR_PROCESS_ABORTED;
        GetExitCodeProcess(processHandle.get(), &exitCode);
        return static_cast<int>(exitCode);
    }

    class service_host
    {
    public:
        service_host(
            const std::filesystem::path& statePath,
            DWORD targetSession,
            HANDLE serviceStopEvent) :
            m_statePath(statePath),
            m_state(ptap::read_state(statePath)),
            m_names(ptap::instance_names(
                ptap::bounded_string(m_state.ownerSid, ARRAYSIZE(m_state.ownerSid)))),
            m_targetSession(targetSession),
            m_serviceStop(serviceStopEvent)
        {
            if (!m_serviceStop)
            {
                throw ptap::win32_error("Session broker stop event", ERROR_INVALID_HANDLE);
            }
            if (ptap::current_token_user_sid() != L"S-1-5-18")
            {
                throw ptap::win32_error("Session broker LocalSystem policy", ERROR_ACCESS_DENIED);
            }
            enable_privilege(SE_TCB_NAME);
            const auto ownerSid =
                ptap::bounded_string(m_state.ownerSid, ARRAYSIZE(m_state.ownerSid));
            const auto accountName =
                ptap::bounded_string(m_state.accountName, ARRAYSIZE(m_state.accountName));
            const auto accountSid =
                ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid));
            const auto serviceName =
                ptap::bounded_string(m_state.serviceName, ARRAYSIZE(m_state.serviceName));
            const auto serviceSid =
                ptap::bounded_string(m_state.serviceSid, ARRAYSIZE(m_state.serviceSid));
            if (m_targetSession == 0 ||
                !std::filesystem::equivalent(m_statePath, m_names.statePath) ||
                accountName != m_names.accountName ||
                accountSid != ptap::sid_for_account(m_names.accountName) ||
                serviceName != m_names.serviceName ||
                serviceSid != ptap::service_sid(m_names.serviceName) ||
                g_serviceName != L"PtAliasProtoBroker_" + m_names.suffix)
            {
                throw ptap::win32_error("Session broker state policy", ERROR_INVALID_DATA);
            }

            HANDLE rawOwnerToken = nullptr;
            ptap::check_bool(
                WTSQueryUserToken(m_targetSession, &rawOwnerToken),
                "WTSQueryUserToken(target)");
            ptap::unique_handle ownerToken(rawOwnerToken);
            if (ptap::token_user_sid(ownerToken.get()) != ownerSid)
            {
                throw ptap::win32_error("Target session owner", ERROR_ACCESS_DENIED);
            }
        }

        ~service_host()
        {
            request_stop();
            stop_worker();
        }

        void initialize()
        {
            enable_privilege(SE_TCB_NAME);
            enable_privilege(SE_INCREASE_QUOTA_NAME);
            enable_privilege(SE_ASSIGNPRIMARYTOKEN_NAME);

            const std::wstring packageFullName =
                ptap::bounded_string(
                    m_state.lastGoodPackageFullName,
                    ARRAYSIZE(m_state.lastGoodPackageFullName));
            if (packageFullName.empty())
            {
                throw ptap::win32_error("Session broker last-good package", ERROR_INVALID_DATA);
            }
            const auto identity = ptap::validate_package_full_name(packageFullName);
            auto bridgeToken = duplicate_current_primary_token();
            ptap::check_bool(
                SetTokenInformation(
                    bridgeToken.get(),
                    TokenSessionId,
                    &m_targetSession,
                    sizeof(m_targetSession)),
                "SetTokenInformation(TokenSessionId)");
            if (token_session_id(bridgeToken.get()) != m_targetSession)
            {
                throw ptap::win32_error("Session token verification", ERROR_INVALID_DATA);
            }

            environment_block environment;
            ptap::check_bool(
                CreateEnvironmentBlock(environment.address(), bridgeToken.get(), FALSE),
                "CreateEnvironmentBlock(bridge)");
            const auto security = ptap::security_descriptor_from_sddl(
                L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" +
                ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid)) + L")");
            SECURITY_ATTRIBUTES attributes{
                sizeof(SECURITY_ATTRIBUTES),
                security.get(),
                FALSE,
            };
            const std::wstring nonce = ptap::make_nonce();
            const std::wstring readyName =
                L"Global\\PtAliasProtoBridgeReady_" + m_names.suffix + L"_" + nonce;
            const std::wstring stopName =
                L"Global\\PtAliasProtoBridgeStop_" + m_names.suffix + L"_" + nonce;
            ptap::unique_handle ready(CreateEventW(&attributes, TRUE, FALSE, readyName.c_str()));
            ptap::unique_handle stop(CreateEventW(&attributes, TRUE, FALSE, stopName.c_str()));
            if (!ready || !stop)
            {
                throw ptap::win32_error("CreateEventW(bridge handshake)", GetLastError());
            }
            ptap::unique_handle job(CreateJobObjectW(nullptr, nullptr));
            if (!job)
            {
                throw ptap::win32_error("CreateJobObjectW(bridge)", GetLastError());
            }
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
            limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            ptap::check_bool(
                SetInformationJobObject(
                    job.get(),
                    JobObjectExtendedLimitInformation,
                    &limits,
                    sizeof(limits)),
                "SetInformationJobObject(bridge)");

            const auto executable = current_module_path();
            std::wstring commandLine =
                ptap::quote_argument(executable.wstring()) +
                L" --bridge" +
                L" --state " + ptap::quote_argument(m_statePath.wstring()) +
                L" --target-session " + std::to_wstring(m_targetSession) +
                L" --ready-event " + ptap::quote_argument(readyName) +
                L" --stop-event " + ptap::quote_argument(stopName);
            STARTUPINFOW startup{};
            startup.cb = sizeof(startup);
            wchar_t interactiveDesktop[] = L"winsta0\\default";
            startup.lpDesktop = interactiveDesktop;
            PROCESS_INFORMATION process{};
            const BOOL created = CreateProcessAsUserW(
                bridgeToken.get(),
                executable.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED | CREATE_NO_WINDOW,
                environment.get(),
                nullptr,
                &startup,
                &process);
            if (!created)
            {
                throw ptap::win32_error("CreateProcessAsUserW(session bridge)", GetLastError());
            }

            ptap::unique_handle processHandle(process.hProcess);
            ptap::unique_handle threadHandle(process.hThread);
            if (!AssignProcessToJobObject(job.get(), processHandle.get()))
            {
                const DWORD error = GetLastError();
                TerminateProcess(processHandle.get(), error);
                throw ptap::win32_error("AssignProcessToJobObject(session bridge)", error);
            }
            HANDLE rawProcessToken = nullptr;
            ptap::check_bool(
                OpenProcessToken(processHandle.get(), TOKEN_QUERY, &rawProcessToken),
                "OpenProcessToken(session bridge)");
            ptap::unique_handle processToken(rawProcessToken);
            if (ptap::token_user_sid(processToken.get()) != L"S-1-5-18" ||
                token_session_id(processToken.get()) != m_targetSession)
            {
                TerminateProcess(processHandle.get(), ERROR_ACCESS_DENIED);
                throw ptap::win32_error("Session bridge identity", ERROR_ACCESS_DENIED);
            }
            if (ResumeThread(threadHandle.get()) == static_cast<DWORD>(-1))
            {
                const DWORD error = GetLastError();
                TerminateProcess(processHandle.get(), error);
                throw ptap::win32_error("ResumeThread(session bridge)", error);
            }
            HANDLE bridgeWaits[]{ ready.get(), processHandle.get() };
            const DWORD bridgeWait =
                WaitForMultipleObjects(
                    ARRAYSIZE(bridgeWaits),
                    bridgeWaits,
                    FALSE,
                    ptap::WorkerReadyTimeoutMs);
            if (bridgeWait == WAIT_OBJECT_0 + 1)
            {
                DWORD exitCode = ERROR_PROCESS_ABORTED;
                GetExitCodeProcess(processHandle.get(), &exitCode);
                throw ptap::win32_error(
                    "Session bridge exited before readiness",
                    exitCode == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : exitCode);
            }
            if (bridgeWait != WAIT_OBJECT_0)
            {
                TerminateProcess(processHandle.get(), ERROR_TIMEOUT);
                throw ptap::win32_error("Session bridge readiness", ERROR_TIMEOUT);
            }
            const auto evidence = ptap::read_evidence(m_names.evidencePath);
            if (evidence.sessionId != m_targetSession ||
                evidence.hasExpectedServiceSid != 1 ||
                ptap::bounded_string(
                    evidence.packageFullName,
                    ARRAYSIZE(evidence.packageFullName)) != identity.fullName)
            {
                TerminateProcess(processHandle.get(), ERROR_INVALID_DATA);
                throw ptap::win32_error("Session bridge evidence", ERROR_INVALID_DATA);
            }

            m_workerProcess = std::move(processHandle);
            m_workerStop = std::move(stop);
            m_workerJob = std::move(job);
            ptap::append_log(
                m_names.storeDirectory,
                L"session-broker",
                L"session bridge ready, bridge-pid=" + std::to_wstring(process.dwProcessId) +
                    L", worker-pid=" + std::to_wstring(evidence.processId) +
                    L", session=" + std::to_wstring(m_targetSession));
        }

        void run()
        {
            HANDLE waits[]{ m_serviceStop, m_workerProcess.get() };
            const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
            if (wait == WAIT_OBJECT_0 + 1)
            {
                DWORD exitCode = ERROR_PROCESS_ABORTED;
                GetExitCodeProcess(m_workerProcess.get(), &exitCode);
                throw ptap::win32_error(
                    "Cross-session worker exited",
                    exitCode == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : exitCode);
            }
            if (wait != WAIT_OBJECT_0)
            {
                throw ptap::win32_error("WaitForMultipleObjects(session broker)", GetLastError());
            }
            stop_worker();
        }

        void request_stop() noexcept
        {
            SetEvent(m_serviceStop);
        }

    private:
        void stop_worker() noexcept
        {
            if (m_workerStop)
            {
                SetEvent(m_workerStop.get());
            }
            if (m_workerProcess &&
                WaitForSingleObject(m_workerProcess.get(), ptap::WorkerStopTimeoutMs) != WAIT_OBJECT_0)
            {
                TerminateProcess(m_workerProcess.get(), ERROR_PROCESS_ABORTED);
                WaitForSingleObject(m_workerProcess.get(), 2000);
            }
            m_workerJob.reset();
            m_workerProcess.reset();
            m_workerStop.reset();
        }

        std::filesystem::path m_statePath;
        ptap::PrototypeState m_state;
        ptap::InstanceNames m_names;
        DWORD m_targetSession{};
        HANDLE m_serviceStop{};
        ptap::unique_handle m_workerProcess;
        ptap::unique_handle m_workerStop;
        ptap::unique_handle m_workerJob;
    };

    DWORD WINAPI control_handler(DWORD control, DWORD, void*, void*)
    {
        if ((control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN) &&
            g_serviceStopEvent)
        {
            try
            {
                set_service_status(SERVICE_STOP_PENDING, ERROR_SUCCESS, ptap::WorkerStopTimeoutMs);
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoSessionBroker: STOP_PENDING failed.\n");
            }
            SetEvent(g_serviceStopEvent);
        }
        return NO_ERROR;
    }

    void WINAPI service_main(DWORD, LPWSTR*)
    {
        DWORD exitCode = ERROR_SUCCESS;
        try
        {
            g_serviceStopEvent = CreateEventW(nullptr, TRUE, FALSE, nullptr);
            if (!g_serviceStopEvent)
            {
                throw ptap::win32_error("CreateEventW(service stop)", GetLastError());
            }
            g_statusHandle =
                RegisterServiceCtrlHandlerExW(g_serviceName.c_str(), control_handler, nullptr);
            if (!g_statusHandle)
            {
                throw ptap::win32_error("RegisterServiceCtrlHandlerExW", GetLastError());
            }
            set_service_status(SERVICE_START_PENDING, ERROR_SUCCESS, 30000);
            service_host host(g_statePath, g_targetSession, g_serviceStopEvent);
            host.initialize();
            set_service_status(SERVICE_RUNNING);
            host.run();
        }
        catch (const ptap::win32_error& error)
        {
            exitCode = error.code();
            try
            {
                const auto state = ptap::read_state(g_statePath);
                const auto names =
                    ptap::instance_names(ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid)));
                ptap::append_log(
                    names.storeDirectory,
                    L"session-broker",
                    L"service failure: " + widen(error.what()) +
                        L"; " + ptap::format_error(error.code()));
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoSessionBroker: failure log unavailable.\n");
            }
        }
        catch (const std::exception& error)
        {
            exitCode = ERROR_UNHANDLED_EXCEPTION;
            try
            {
                const auto state = ptap::read_state(g_statePath);
                const auto names =
                    ptap::instance_names(
                        ptap::bounded_string(
                            state.ownerSid,
                            ARRAYSIZE(state.ownerSid)));
                ptap::append_log(
                    names.storeDirectory,
                    L"session-broker",
                    L"service failure: " + widen(error.what()));
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoSessionBroker: failure log unavailable.\n");
            }
        }
        catch (...)
        {
            exitCode = ERROR_UNHANDLED_EXCEPTION;
            OutputDebugStringW(L"PtAliasProtoSessionBroker: unknown service failure.\n");
        }
        if (g_statusHandle)
        {
            try
            {
                set_service_status(SERVICE_STOPPED, exitCode);
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoSessionBroker: STOPPED failed.\n");
            }
        }
    }
}

int wmain()
{
    try
    {
        const auto args = ptap::command_line_arguments();
        if (ptap::has_argument(args, L"--account-bridge"))
        {
            try
            {
                return run_account_bridge(args);
            }
            catch (const ptap::win32_error& error)
            {
                try
                {
                    const auto state =
                        ptap::read_state(ptap::argument_value(args, L"--state"));
                    const auto names =
                        ptap::instance_names(
                            ptap::bounded_string(
                                state.ownerSid,
                                ARRAYSIZE(state.ownerSid)));
                    ptap::append_log(
                        names.storeDirectory,
                        L"account-bridge",
                        L"failure: " + widen(error.what()) +
                            L"; " + ptap::format_error(error.code()));
                }
                catch (...)
                {
                    OutputDebugStringW(L"PtAliasProtoSessionBroker: account bridge log unavailable.\n");
                }
                return static_cast<int>(error.code());
            }
        }
        if (ptap::has_argument(args, L"--bridge"))
        {
            return run_bridge(args);
        }
        if (!ptap::has_argument(args, L"--service"))
        {
            fwprintf(stderr, L"PtAliasProtoSessionBroker requires --service or --bridge.\n");
            return ERROR_INVALID_PARAMETER;
        }
        const auto state = ptap::argument_value(args, L"--state");
        g_serviceName = ptap::argument_value(args, L"--service-name");
        const auto targetSession = ptap::argument_value(args, L"--target-session");
        if (state.empty() ||
            g_serviceName.empty() ||
            targetSession.empty() ||
            state.size() >= 1024 ||
            g_serviceName.size() >= 64)
        {
            return ERROR_INVALID_PARAMETER;
        }
        wchar_t* end = nullptr;
        const unsigned long parsed = wcstoul(targetSession.c_str(), &end, 10);
        if (!end || *end != L'\0' || parsed == 0)
        {
            return ERROR_INVALID_PARAMETER;
        }
        g_targetSession = static_cast<DWORD>(parsed);
        g_statePath = std::filesystem::weakly_canonical(state);
        SERVICE_TABLE_ENTRYW entries[]{
            { g_serviceName.data(), service_main },
            { nullptr, nullptr },
        };
        if (!StartServiceCtrlDispatcherW(entries))
        {
            return static_cast<int>(GetLastError());
        }
        return 0;
    }
    catch (const ptap::win32_error& error)
    {
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
