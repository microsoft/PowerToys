#include "../Common/ProtoCommon.h"

#include <sddl.h>
#include <userenv.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/base.h>

#include <atomic>
#include <filesystem>
#include <mutex>
#include <thread>

#pragma comment(lib, "userenv.lib")
#pragma comment(lib, "windowsapp.lib")

namespace
{
    std::filesystem::path g_statePath;
    std::wstring g_serviceName;
    SERVICE_STATUS_HANDLE g_statusHandle{};
    SERVICE_STATUS g_status{};

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

    void verify_service_cannot_reconfigure_itself(std::wstring_view serviceName)
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptap::win32_error("OpenSCManagerW(self-rights)", GetLastError());
        }
        const std::wstring name(serviceName);
        service_handle service(OpenServiceW(scm.get(), name.c_str(), SERVICE_CHANGE_CONFIG));
        if (service)
        {
            throw ptap::win32_error("Service self-reconfiguration policy", ERROR_ACCESS_DENIED);
        }
        const DWORD error = GetLastError();
        if (error != ERROR_ACCESS_DENIED)
        {
            throw ptap::win32_error("OpenServiceW(self-rights)", error);
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

    bool overlapped_transfer(
        HANDLE pipe,
        bool write,
        void* buffer,
        DWORD bytes,
        DWORD& transferred,
        DWORD timeoutMs)
    {
        ptap::unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            throw ptap::win32_error("CreateEventW(pipe IO)", GetLastError());
        }
        OVERLAPPED overlapped{};
        overlapped.hEvent = event.get();
        BOOL started = write ?
                           WriteFile(pipe, buffer, bytes, nullptr, &overlapped) :
                           ReadFile(pipe, buffer, bytes, nullptr, &overlapped);
        if (!started && GetLastError() != ERROR_IO_PENDING)
        {
            return false;
        }
        if (WaitForSingleObject(event.get(), timeoutMs) != WAIT_OBJECT_0)
        {
            if (!CancelIoEx(pipe, &overlapped) && GetLastError() != ERROR_NOT_FOUND)
            {
                return false;
            }
            DWORD ignored = 0;
            GetOverlappedResult(pipe, &overlapped, &ignored, TRUE);
            return false;
        }
        return GetOverlappedResult(pipe, &overlapped, &transferred, FALSE) != FALSE;
    }

    bool overlapped_transfer_exact(
        HANDLE pipe,
        bool write,
        void* buffer,
        DWORD bytes,
        DWORD timeoutMs)
    {
        DWORD total = 0;
        while (total < bytes)
        {
            DWORD transferred = 0;
            if (!overlapped_transfer(
                    pipe,
                    write,
                    static_cast<std::byte*>(buffer) + total,
                    bytes - total,
                    transferred,
                    timeoutMs) ||
                transferred == 0)
            {
                return false;
            }
            total += transferred;
        }
        return true;
    }

    class service_host
    {
    public:
        explicit service_host(const std::filesystem::path& statePath) :
            m_statePath(statePath),
            m_state(ptap::read_state(statePath)),
            m_names(ptap::instance_names(ptap::bounded_string(m_state.ownerSid, ARRAYSIZE(m_state.ownerSid)))),
            m_stopEvent(CreateEventW(nullptr, TRUE, FALSE, nullptr))
        {
            if (!m_stopEvent)
            {
                throw ptap::win32_error("CreateEventW(service stop)", GetLastError());
            }
            const auto expectedAccountSid = ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid));
            const auto accountName =
                ptap::bounded_string(m_state.accountName, ARRAYSIZE(m_state.accountName));
            const auto serviceName =
                ptap::bounded_string(m_state.serviceName, ARRAYSIZE(m_state.serviceName));
            const auto configuredServiceSid =
                ptap::bounded_string(m_state.serviceSid, ARRAYSIZE(m_state.serviceSid));
            if (!std::filesystem::equivalent(m_statePath, m_names.statePath) ||
                accountName != m_names.accountName ||
                serviceName != m_names.serviceName ||
                expectedAccountSid != ptap::sid_for_account(m_names.accountName) ||
                configuredServiceSid != ptap::service_sid(m_names.serviceName) ||
                ptap::current_token_user_sid() != expectedAccountSid)
            {
                throw ptap::win32_error("Service state identity validation", ERROR_ACCESS_DENIED);
            }
            const auto desired =
                ptap::bounded_string(
                    m_state.desiredPackageFullName,
                    ARRAYSIZE(m_state.desiredPackageFullName));
            const auto lastGood =
                ptap::bounded_string(
                    m_state.lastGoodPackageFullName,
                    ARRAYSIZE(m_state.lastGoodPackageFullName));
            if (!desired.empty())
            {
                (void)ptap::validate_package_full_name(desired);
            }
            if (!lastGood.empty())
            {
                (void)ptap::validate_package_full_name(lastGood);
            }
            verify_service_cannot_reconfigure_itself(
                serviceName);
        }

        ~service_host()
        {
            request_stop();
            if (m_pipeThread.joinable())
            {
                m_pipeThread.join();
            }
            stop_worker();
        }

        void initialize()
        {
            m_pipeThread = std::thread([this] {
                pipe_loop();
            });
            const std::wstring desired =
                ptap::bounded_string(m_state.desiredPackageFullName, ARRAYSIZE(m_state.desiredPackageFullName));
            if (desired.empty())
            {
                throw ptap::win32_error("Initial package configuration", ERROR_INVALID_DATA);
            }
            ensure_package(desired, false);
        }

        void run()
        {
            while (WaitForSingleObject(m_stopEvent.get(), 500) == WAIT_TIMEOUT)
            {
                std::wstring restartPackage;
                {
                    std::lock_guard lock(m_operationMutex);
                    if (consume_worker_exit_unlocked())
                    {
                        m_restartPending = true;
                        m_nextRestartTick = GetTickCount64();
                    }
                    if (m_restartPending && GetTickCount64() >= m_nextRestartTick)
                    {
                        restartPackage = ptap::bounded_string(
                            m_state.lastGoodPackageFullName,
                            ARRAYSIZE(m_state.lastGoodPackageFullName));
                    }
                }
                if (!restartPackage.empty())
                {
                    try
                    {
                        ensure_package(restartPackage, false);
                        std::lock_guard lock(m_operationMutex);
                        m_restartPending = false;
                    }
                    catch (const std::exception& error)
                    {
                        ptap::append_log(
                            m_names.storeDirectory,
                            L"launcher",
                            L"worker restart failed: " + widen(error.what()));
                        std::lock_guard lock(m_operationMutex);
                        m_nextRestartTick = GetTickCount64() + 5000;
                    }
                }
            }
            stop_worker();
            if (m_pipeThread.joinable())
            {
                m_pipeThread.join();
            }
        }

        void request_stop() noexcept
        {
            SetEvent(m_stopEvent.get());
        }

    public:
        static std::wstring widen(const char* value)
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

    private:
        void save_state()
        {
            ptap::write_state_atomic(m_statePath, m_state);
        }

        void save_state_best_effort(std::wstring_view operation) noexcept
        {
            try
            {
                save_state();
            }
            catch (const std::exception& error)
            {
                ptap::append_log(
                    m_names.storeDirectory,
                    L"launcher",
                    std::wstring(operation) + L": state persistence failed: " + widen(error.what()));
            }
        }

        void register_exact_package(const std::wstring& fullName)
        {
            const auto policy = ptap::validate_package_full_name(fullName);
            if (!ptap::is_package_staged(fullName))
            {
                throw ptap::win32_error("Exact package is not staged", ERROR_NOT_FOUND);
            }
            winrt::Windows::Management::Deployment::PackageManager manager;
            const auto dependencies = winrt::single_threaded_vector<winrt::hstring>().GetView();
            const auto registration = manager.RegisterPackageByFullNameAsync(
                                                  fullName,
                                                  dependencies,
                                                  winrt::Windows::Management::Deployment::DeploymentOptions::ForceUpdateFromAnyVersion)
                                          .get();
            const HRESULT registrationError = registration.ExtendedErrorCode();
            if (FAILED(registrationError))
            {
                throw ptap::win32_error("RegisterPackageByFullNameAsync", HRESULT_CODE(registrationError));
            }
            const auto package = manager.FindPackageForUser(L"", fullName);
            if (!package)
            {
                throw ptap::win32_error("FindPackageForUser", ERROR_NOT_FOUND);
            }
            const auto id = package.Id();
            if (id.FullName() != fullName ||
                id.Name() != ptap::PackageName ||
                id.Publisher() != ptap::PackagePublisher ||
                id.FamilyName() != policy.familyName)
            {
                throw ptap::win32_error("Registered package identity verification", ERROR_INVALID_DATA);
            }
        }

        void unregister_package(const std::wstring& fullName)
        {
            if (fullName.empty())
            {
                return;
            }
            const auto policy = ptap::validate_package_full_name(fullName);
            (void)policy;
            winrt::Windows::Management::Deployment::PackageManager manager;
            const auto result = manager.RemovePackageAsync(fullName).get();
            const HRESULT removalError = result.ExtendedErrorCode();
            if (FAILED(removalError) && HRESULT_CODE(removalError) != ERROR_NOT_FOUND)
            {
                throw ptap::win32_error("RemovePackageAsync", HRESULT_CODE(removalError));
            }
        }

        ptap::local_memory event_security()
        {
            const std::wstring accountSid =
                ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid));
            return ptap::security_descriptor_from_sddl(
                L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" + accountSid + L")");
        }

        bool verify_worker_process(HANDLE process, const ptap::PackageIdentity& expected)
        {
            HANDLE rawToken = nullptr;
            ptap::check_bool(
                OpenProcessToken(process, TOKEN_QUERY, &rawToken),
                "OpenProcessToken(worker)");
            ptap::unique_handle token(rawToken);
            const auto fullName = package_full_name_from_process(process);
            const auto family = package_family_from_process(process);
            if (fullName != expected.fullName || family != expected.familyName)
            {
                return false;
            }
            const auto accountSid =
                ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid));
            const auto expectedServiceSid =
                ptap::bounded_string(m_state.serviceSid, ARRAYSIZE(m_state.serviceSid));
            return ptap::token_user_sid(token.get()) == accountSid &&
                   ptap::token_contains_sid(token.get(), expectedServiceSid);
        }

        void launch_worker_once(const ptap::PackageIdentity& identity)
        {
            const auto security = event_security();
            SECURITY_ATTRIBUTES attributes{
                sizeof(SECURITY_ATTRIBUTES),
                security.get(),
                FALSE,
            };
            const std::wstring nonce = ptap::make_nonce();
            const std::wstring readyName = L"PtAliasProtoReady_" + m_names.suffix + L"_" + nonce;
            const std::wstring stopName = L"PtAliasProtoStop_" + m_names.suffix + L"_" + nonce;
            ptap::unique_handle ready(CreateEventW(&attributes, TRUE, FALSE, readyName.c_str()));
            ptap::unique_handle stop(CreateEventW(&attributes, TRUE, FALSE, stopName.c_str()));
            if (!ready || !stop)
            {
                throw ptap::win32_error("CreateEventW(worker handshake)", GetLastError());
            }
            ptap::unique_handle job(CreateJobObjectW(nullptr, nullptr));
            if (!job)
            {
                throw ptap::win32_error("CreateJobObjectW", GetLastError());
            }
            JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
            limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
            ptap::check_bool(
                SetInformationJobObject(job.get(), JobObjectExtendedLimitInformation, &limits, sizeof(limits)),
                "SetInformationJobObject");

            const auto alias = ptap::alias_path();
            std::wstring commandLine =
                ptap::quote_argument(alias.wstring()) +
                L" --config " + ptap::quote_argument(m_statePath.wstring()) +
                L" --ready-event " + ptap::quote_argument(readyName) +
                L" --stop-event " + ptap::quote_argument(stopName);
            STARTUPINFOW startup{};
            startup.cb = sizeof(startup);
            PROCESS_INFORMATION process{};
            if (!CreateProcessW(
                    alias.c_str(),
                    commandLine.data(),
                    nullptr,
                    nullptr,
                    FALSE,
                    CREATE_UNICODE_ENVIRONMENT | CREATE_SUSPENDED,
                    nullptr,
                    nullptr,
                    &startup,
                    &process))
            {
                throw ptap::win32_error("CreateProcessW(alias)", GetLastError());
            }
            ptap::unique_handle processHandle(process.hProcess);
            ptap::unique_handle threadHandle(process.hThread);
            if (!AssignProcessToJobObject(job.get(), processHandle.get()))
            {
                const DWORD error = GetLastError();
                TerminateProcess(processHandle.get(), error);
                WaitForSingleObject(processHandle.get(), 2000);
                throw ptap::win32_error("AssignProcessToJobObject", error);
            }

            bool identityVerified = false;
            for (DWORD attempt = 0; attempt < 20; ++attempt)
            {
                if (WaitForSingleObject(processHandle.get(), 0) == WAIT_OBJECT_0)
                {
                    break;
                }
                try
                {
                    identityVerified = verify_worker_process(processHandle.get(), identity);
                    if (identityVerified)
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
            if (!identityVerified)
            {
                TerminateProcess(processHandle.get(), ERROR_ACCESS_DENIED);
                WaitForSingleObject(processHandle.get(), 2000);
                throw ptap::win32_error("Worker token package identity", ERROR_ACCESS_DENIED);
            }
            if (ResumeThread(threadHandle.get()) == static_cast<DWORD>(-1))
            {
                const DWORD error = GetLastError();
                TerminateProcess(processHandle.get(), error);
                WaitForSingleObject(processHandle.get(), 2000);
                throw ptap::win32_error("ResumeThread(worker)", error);
            }
            const DWORD readyWait = WaitForSingleObject(ready.get(), ptap::WorkerReadyTimeoutMs);
            if (readyWait != WAIT_OBJECT_0)
            {
                TerminateProcess(processHandle.get(), ERROR_TIMEOUT);
                WaitForSingleObject(processHandle.get(), 2000);
                throw ptap::win32_error("Worker readiness handshake", ERROR_TIMEOUT);
            }
            const auto evidence = ptap::read_evidence(m_names.evidencePath);
            if (ptap::bounded_string(evidence.packageFullName, ARRAYSIZE(evidence.packageFullName)) != identity.fullName ||
                ptap::bounded_string(evidence.packageFamilyName, ARRAYSIZE(evidence.packageFamilyName)) != identity.familyName ||
                evidence.hasExpectedServiceSid != 1)
            {
                TerminateProcess(processHandle.get(), ERROR_INVALID_DATA);
                WaitForSingleObject(processHandle.get(), 2000);
                throw ptap::win32_error("Worker evidence verification", ERROR_INVALID_DATA);
            }

            m_workerProcess = std::move(processHandle);
            m_workerStopEvent = std::move(stop);
            m_workerJob = std::move(job);
            m_state.lastWorkerPid = process.dwProcessId;
            m_state.lastWin32Error = ERROR_SUCCESS;
            save_state_best_effort(L"worker ready");
            ptap::append_log(
                m_names.storeDirectory,
                L"launcher",
                L"worker ready, pid=" + std::to_wstring(process.dwProcessId) + L", package=" + identity.fullName);
        }

        void launch_worker_with_tamper_recovery(const ptap::PackageIdentity& identity)
        {
            try
            {
                launch_worker_once(identity);
                return;
            }
            catch (const std::exception& error)
            {
                ptap::append_log(
                    m_names.storeDirectory,
                    L"launcher",
                    L"initial alias launch rejected; deleting exact leaf and retrying once: " + widen(error.what()));
            }
            stop_worker_unlocked();
            const auto alias = ptap::alias_path();
            if (!DeleteFileW(alias.c_str()))
            {
                const DWORD error = GetLastError();
                if (error != ERROR_FILE_NOT_FOUND)
                {
                    throw ptap::win32_error("DeleteFileW(tampered alias leaf)", error);
                }
            }
            register_exact_package(identity.fullName);
            launch_worker_once(identity);
        }

        void ensure_package(const std::wstring& fullName, bool fromClient)
        {
            std::lock_guard lock(m_operationMutex);
            const auto candidate = ptap::validate_package_full_name(fullName);
            const std::wstring lastGood =
                ptap::bounded_string(m_state.lastGoodPackageFullName, ARRAYSIZE(m_state.lastGoodPackageFullName));
            if (fromClient && !lastGood.empty())
            {
                const auto previous = ptap::validate_package_full_name(lastGood);
                if (ptap::version_value(candidate.version) < ptap::version_value(previous.version))
                {
                    throw ptap::win32_error("Monotonic package version policy", ERROR_REVISION_MISMATCH);
                }
            }
            if (!ptap::is_package_staged(fullName))
            {
                throw ptap::win32_error("Exact package is not staged", ERROR_NOT_FOUND);
            }

            stop_worker_unlocked();
            try
            {
                register_exact_package(fullName);
                launch_worker_with_tamper_recovery(candidate);
                ptap::copy_bounded(
                    m_state.desiredPackageFullName,
                    ARRAYSIZE(m_state.desiredPackageFullName),
                    fullName);
                ptap::copy_bounded(
                    m_state.lastGoodPackageFullName,
                    ARRAYSIZE(m_state.lastGoodPackageFullName),
                    fullName);
                m_state.lastWin32Error = ERROR_SUCCESS;
                save_state_best_effort(L"package update");
            }
            catch (const ptap::win32_error& error)
            {
                m_state.lastWin32Error = error.code();
                save_state_best_effort(L"package update failure");
                if (!lastGood.empty() && lastGood != fullName)
                {
                    ptap::append_log(m_names.storeDirectory, L"launcher", L"update failed; restoring last known good");
                    register_exact_package(lastGood);
                    launch_worker_with_tamper_recovery(ptap::validate_package_full_name(lastGood));
                }
                else if (lastGood.empty())
                {
                    unregister_package(fullName);
                }
                throw;
            }
            catch (const winrt::hresult_error& error)
            {
                const DWORD code = HRESULT_CODE(error.code());
                m_state.lastWin32Error = code;
                save_state_best_effort(L"WinRT package update failure");
                if (!lastGood.empty() && lastGood != fullName)
                {
                    ptap::append_log(m_names.storeDirectory, L"launcher", L"WinRT update failed; restoring last known good");
                    register_exact_package(lastGood);
                    launch_worker_with_tamper_recovery(ptap::validate_package_full_name(lastGood));
                }
                else if (lastGood.empty())
                {
                    unregister_package(fullName);
                }
                throw ptap::win32_error("Package deployment operation", code);
            }
            catch (const std::exception&)
            {
                m_state.lastWin32Error = ERROR_GEN_FAILURE;
                save_state_best_effort(L"package update failure");
                if (!lastGood.empty() && lastGood != fullName)
                {
                    ptap::append_log(m_names.storeDirectory, L"launcher", L"update failed; restoring last known good");
                    register_exact_package(lastGood);
                    launch_worker_with_tamper_recovery(ptap::validate_package_full_name(lastGood));
                }
                else if (lastGood.empty())
                {
                    unregister_package(fullName);
                }
                throw;
            }
        }

        void stop_worker_unlocked() noexcept
        {
            if (m_workerStopEvent)
            {
                SetEvent(m_workerStopEvent.get());
            }
            if (m_workerProcess)
            {
                if (WaitForSingleObject(m_workerProcess.get(), ptap::WorkerStopTimeoutMs) != WAIT_OBJECT_0)
                {
                    TerminateProcess(m_workerProcess.get(), ERROR_PROCESS_ABORTED);
                    WaitForSingleObject(m_workerProcess.get(), 2000);
                }
            }
            m_workerJob.reset();
            m_workerProcess.reset();
            m_workerStopEvent.reset();
            m_state.lastWorkerPid = 0;
            save_state_best_effort(L"worker stopped");
        }

        bool consume_worker_exit_unlocked()
        {
            if (!m_workerProcess || WaitForSingleObject(m_workerProcess.get(), 0) != WAIT_OBJECT_0)
            {
                return false;
            }
            DWORD exitCode = ERROR_PROCESS_ABORTED;
            GetExitCodeProcess(m_workerProcess.get(), &exitCode);
            m_workerJob.reset();
            m_workerProcess.reset();
            m_workerStopEvent.reset();
            m_state.lastWorkerPid = 0;
            m_state.lastWin32Error = exitCode == ERROR_SUCCESS ? ERROR_PROCESS_ABORTED : exitCode;
            save_state_best_effort(L"unexpected worker exit");
            ptap::append_log(
                m_names.storeDirectory,
                L"launcher",
                L"worker exited unexpectedly, code=" + std::to_wstring(exitCode));
            return true;
        }

        void stop_worker() noexcept
        {
            std::lock_guard lock(m_operationMutex);
            stop_worker_unlocked();
        }

        bool authenticate_pipe_client(HANDLE pipe)
        {
            if (!ImpersonateNamedPipeClient(pipe))
            {
                throw ptap::win32_error("ImpersonateNamedPipeClient", GetLastError());
            }
            struct revert_guard
            {
                ~revert_guard()
                {
                    RevertToSelf();
                }
            } revert;
            HANDLE rawToken = nullptr;
            ptap::check_bool(
                OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &rawToken),
                "OpenThreadToken(pipe)");
            ptap::unique_handle token(rawToken);
            const auto ownerSid = ptap::bounded_string(m_state.ownerSid, ARRAYSIZE(m_state.ownerSid));
            return ptap::token_user_sid(token.get()) == ownerSid || ptap::token_is_administrator(token.get());
        }

        std::vector<std::byte> handle_request(
            ptap::Command command,
            const std::vector<std::byte>& payload)
        {
            if (command == ptap::Command::Status)
            {
                if (!payload.empty())
                {
                    throw ptap::win32_error("Status payload", ERROR_INVALID_DATA);
                }
                std::lock_guard lock(m_operationMutex);
                consume_worker_exit_unlocked();
                ptap::StatusPayload status;
                status.scmState = SERVICE_RUNNING;
                status.workerPid = m_state.lastWorkerPid;
                status.lastWin32Error = m_state.lastWin32Error;
                const auto desired =
                    ptap::bounded_string(m_state.desiredPackageFullName, ARRAYSIZE(m_state.desiredPackageFullName));
                const auto lastGood =
                    ptap::bounded_string(m_state.lastGoodPackageFullName, ARRAYSIZE(m_state.lastGoodPackageFullName));
                if (!desired.empty())
                {
                    status.desiredVersion = ptap::compact_version(ptap::validate_package_full_name(desired).version);
                }
                if (!lastGood.empty())
                {
                    status.lastGoodVersion = ptap::compact_version(ptap::validate_package_full_name(lastGood).version);
                    ptap::copy_bounded(status.packageFullName, ARRAYSIZE(status.packageFullName), lastGood);
                }
                std::vector<std::byte> result(sizeof(status));
                memcpy(result.data(), &status, sizeof(status));
                return result;
            }
            if (command == ptap::Command::EnsurePackage)
            {
                if (payload.empty() || payload.size() > 512 || payload.size() % sizeof(wchar_t) != 0)
                {
                    throw ptap::win32_error("Ensure package payload size", ERROR_INVALID_DATA);
                }
                const auto* text = reinterpret_cast<const wchar_t*>(payload.data());
                const size_t characters = payload.size() / sizeof(wchar_t);
                if (text[characters - 1] != L'\0' || wcsnlen_s(text, characters) != characters - 1)
                {
                    throw ptap::win32_error("Ensure package payload termination", ERROR_INVALID_DATA);
                }
                ensure_package(std::wstring(text, characters - 1), true);
                return {};
            }
            if (command == ptap::Command::StopWorker)
            {
                if (!payload.empty())
                {
                    throw ptap::win32_error("Stop worker payload", ERROR_INVALID_DATA);
                }
                stop_worker();
                return {};
            }
            if (command == ptap::Command::CleanupRegistration)
            {
                if (!payload.empty())
                {
                    throw ptap::win32_error("Cleanup payload", ERROR_INVALID_DATA);
                }
                std::lock_guard lock(m_operationMutex);
                stop_worker_unlocked();
                const auto desired =
                    ptap::bounded_string(m_state.desiredPackageFullName, ARRAYSIZE(m_state.desiredPackageFullName));
                const auto lastGood =
                    ptap::bounded_string(m_state.lastGoodPackageFullName, ARRAYSIZE(m_state.lastGoodPackageFullName));
                unregister_package(desired);
                if (!lastGood.empty() && lastGood != desired)
                {
                    unregister_package(lastGood);
                }
                const auto alias = ptap::alias_path();
                if (!DeleteFileW(alias.c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
                {
                    throw ptap::win32_error("DeleteFileW(alias cleanup)", GetLastError());
                }
                return {};
            }
            throw ptap::win32_error("Unknown protocol command", ERROR_NOT_SUPPORTED);
        }

        void serve_pipe(HANDLE pipe)
        {
            ptap::ReplyHeader reply;
            ptap::RequestHeader request;
            if (!overlapped_transfer_exact(pipe, false, &request, sizeof(request), 5000))
            {
                return;
            }
            reply.command = request.command;
            reply.requestId = request.requestId;
            std::vector<std::byte> replyPayload;
            try
            {
                if (request.magic != ptap::ProtocolMagic ||
                    request.version != ptap::ProtocolVersion ||
                    request.payloadBytes > ptap::MaxProtocolPayload)
                {
                    throw ptap::win32_error("Protocol header", ERROR_INVALID_DATA);
                }
                if (!authenticate_pipe_client(pipe))
                {
                    throw ptap::win32_error("Pipe caller authorization", ERROR_ACCESS_DENIED);
                }
                std::vector<std::byte> payload(request.payloadBytes);
                if (request.payloadBytes != 0)
                {
                    if (!overlapped_transfer_exact(
                            pipe,
                            false,
                            payload.data(),
                            request.payloadBytes,
                            5000))
                    {
                        throw ptap::win32_error("Protocol payload read", ERROR_READ_FAULT);
                    }
                }
                replyPayload = handle_request(static_cast<ptap::Command>(request.command), payload);
            }
            catch (const ptap::win32_error& error)
            {
                reply.win32Status = error.code();
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoLauncher: unhandled pipe request exception.\n");
                reply.win32Status = ERROR_UNHANDLED_EXCEPTION;
            }
            reply.payloadBytes = static_cast<uint32_t>(replyPayload.size());
            if (!overlapped_transfer_exact(pipe, true, &reply, sizeof(reply), 5000))
            {
                return;
            }
            if (!replyPayload.empty())
            {
                overlapped_transfer_exact(
                    pipe,
                    true,
                    replyPayload.data(),
                    static_cast<DWORD>(replyPayload.size()),
                    5000);
            }
            FlushFileBuffers(pipe);
        }

        void pipe_loop() noexcept
        {
            try
            {
                const auto accountSid =
                    ptap::bounded_string(m_state.accountSid, ARRAYSIZE(m_state.accountSid));
                const auto ownerSid =
                    ptap::bounded_string(m_state.ownerSid, ARRAYSIZE(m_state.ownerSid));
                auto descriptor = ptap::security_descriptor_from_sddl(
                    L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GA;;;" + accountSid + L")(A;;GRGW;;;" + ownerSid + L")");
                SECURITY_ATTRIBUTES attributes{
                    sizeof(SECURITY_ATTRIBUTES),
                    descriptor.get(),
                    FALSE,
                };
                ptap::unique_handle pipe(CreateNamedPipeW(
                    m_names.pipeName.c_str(),
                    PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED | FILE_FLAG_FIRST_PIPE_INSTANCE,
                    PIPE_TYPE_BYTE | PIPE_READMODE_BYTE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                    1,
                    4096,
                    4096,
                    0,
                    &attributes));
                if (!pipe)
                {
                    throw ptap::win32_error("CreateNamedPipeW", GetLastError());
                }
                while (WaitForSingleObject(m_stopEvent.get(), 0) == WAIT_TIMEOUT)
                {
                    ptap::unique_handle connected(CreateEventW(nullptr, TRUE, FALSE, nullptr));
                    if (!connected)
                    {
                        throw ptap::win32_error("CreateEventW(pipe connect)", GetLastError());
                    }
                    OVERLAPPED overlapped{};
                    overlapped.hEvent = connected.get();
                    BOOL result = ConnectNamedPipe(pipe.get(), &overlapped);
                    const DWORD error = result ? ERROR_SUCCESS : GetLastError();
                    if (!result && error != ERROR_IO_PENDING && error != ERROR_PIPE_CONNECTED)
                    {
                        throw ptap::win32_error("ConnectNamedPipe", error);
                    }
                    HANDLE waits[]{ m_stopEvent.get(), connected.get() };
                    const DWORD wait = error == ERROR_PIPE_CONNECTED ?
                                           WAIT_OBJECT_0 + 1 :
                                           WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
                    if (wait == WAIT_OBJECT_0)
                    {
                        if (error == ERROR_IO_PENDING)
                        {
                            CancelIoEx(pipe.get(), &overlapped);
                            DWORD ignored = 0;
                            GetOverlappedResult(pipe.get(), &overlapped, &ignored, TRUE);
                        }
                        break;
                    }
                    DWORD transferred = 0;
                    if (error == ERROR_IO_PENDING &&
                        !GetOverlappedResult(pipe.get(), &overlapped, &transferred, FALSE))
                    {
                        continue;
                    }
                    serve_pipe(pipe.get());
                    DisconnectNamedPipe(pipe.get());
                }
            }
            catch (const std::exception& error)
            {
                ptap::append_log(m_names.storeDirectory, L"launcher", L"pipe loop failed: " + widen(error.what()));
                request_stop();
            }
        }

        std::filesystem::path m_statePath;
        ptap::PrototypeState m_state;
        ptap::InstanceNames m_names;
        ptap::unique_handle m_stopEvent;
        ptap::unique_handle m_workerProcess;
        ptap::unique_handle m_workerStopEvent;
        ptap::unique_handle m_workerJob;
        std::thread m_pipeThread;
        std::mutex m_operationMutex;
        bool m_restartPending{};
        ULONGLONG m_nextRestartTick{};
    };

    service_host* g_host{};

    DWORD WINAPI control_handler(DWORD control, DWORD, void*, void*)
    {
        if ((control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN) && g_host)
        {
            try
            {
                set_service_status(SERVICE_STOP_PENDING, ERROR_SUCCESS, ptap::WorkerStopTimeoutMs);
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoLauncher: SetServiceStatus(STOP_PENDING) failed.\n");
            }
            g_host->request_stop();
        }
        return NO_ERROR;
    }

    void WINAPI service_main(DWORD, LPWSTR*)
    {
        DWORD exitCode = ERROR_SUCCESS;
        try
        {
            g_statusHandle = RegisterServiceCtrlHandlerExW(g_serviceName.c_str(), control_handler, nullptr);
            if (!g_statusHandle)
            {
                throw ptap::win32_error("RegisterServiceCtrlHandlerExW", GetLastError());
            }
            set_service_status(SERVICE_START_PENDING, ERROR_SUCCESS, 30000);
            winrt::init_apartment(winrt::apartment_type::multi_threaded);
            service_host host(g_statePath);
            g_host = &host;
            host.initialize();
            set_service_status(SERVICE_RUNNING);
            host.run();
            g_host = nullptr;
        }
        catch (const ptap::win32_error& error)
        {
            exitCode = error.code();
            if (!g_statePath.empty())
            {
                try
                {
                    const auto state = ptap::read_state(g_statePath);
                    const auto names =
                        ptap::instance_names(ptap::bounded_string(state.ownerSid, ARRAYSIZE(state.ownerSid)));
                    ptap::append_log(
                        names.storeDirectory,
                        L"launcher",
                        L"service failure: " + service_host::widen(error.what()) +
                            L"; " + ptap::format_error(error.code()));
                }
                catch (...)
                {
                    OutputDebugStringW(L"PtAliasProtoLauncher: unable to write protected failure log.\n");
                }
            }
        }
        catch (const std::exception&)
        {
            exitCode = ERROR_UNHANDLED_EXCEPTION;
            OutputDebugStringW(L"PtAliasProtoLauncher: unhandled native service exception.\n");
        }
        if (g_statusHandle)
        {
            try
            {
                set_service_status(SERVICE_STOPPED, exitCode);
            }
            catch (...)
            {
                OutputDebugStringW(L"PtAliasProtoLauncher: SetServiceStatus(STOPPED) failed.\n");
            }
        }
    }
}

int wmain()
{
    try
    {
        const auto args = ptap::command_line_arguments();
        if (!ptap::has_argument(args, L"--service"))
        {
            fwprintf(stderr, L"PtAliasProtoLauncher is an SCM-only prototype launcher.\n");
            return ERROR_INVALID_PARAMETER;
        }
        const auto state = ptap::argument_value(args, L"--state");
        if (state.empty() || state.size() >= 1024)
        {
            return ERROR_INVALID_PARAMETER;
        }
        g_statePath = std::filesystem::weakly_canonical(state);
        const auto configuration = ptap::read_state(g_statePath);
        g_serviceName =
            ptap::bounded_string(configuration.serviceName, ARRAYSIZE(configuration.serviceName));
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
