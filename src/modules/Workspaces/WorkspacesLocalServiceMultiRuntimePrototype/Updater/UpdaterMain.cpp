#include "../Common/LsmrCommon.h"

#include <aclapi.h>
#include <sddl.h>
#include <shellapi.h>
#include <tlhelp32.h>

#include <algorithm>
#include <array>
#include <exception>
#include <filesystem>
#include <iostream>
#include <map>
#include <optional>
#include <sstream>
#include <thread>

#pragma comment(lib, "advapi32.lib")

namespace
{
    thread_local std::wstring g_enginePhase = L"idle";
    thread_local bool g_releaseStagingCleanupPending = false;

    class service_handle
    {
    public:
        explicit service_handle(SC_HANDLE value = nullptr) :
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
            const SC_HANDLE value = m_value;
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

    class overlapped_pipe_operation
    {
    public:
        overlapped_pipe_operation()
        {
            m_event.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!m_event)
            {
                throw ptlsmr::win32_error("CreateEventW(pipe operation)", GetLastError());
            }
            m_value.hEvent = m_event.get();
        }

        [[nodiscard]] OVERLAPPED* get() noexcept
        {
            return &m_value;
        }

        [[nodiscard]] HANDLE event() const noexcept
        {
            return m_event.get();
        }

    private:
        ptlsmr::unique_handle m_event;
        OVERLAPPED m_value{};
    };

    struct managed_instance
    {
        std::wstring ownerSid;
        uint16_t runtimeTrack{};
        ptlsmr::file_version runtimeVersion{};
        std::wstring runtimeSha256;
        std::wstring transactionId;
    };

    struct installed_candidate
    {
        uint16_t runtimeTrack{};
        ptlsmr::file_version version{};
        std::filesystem::path stagingDirectory;
        std::filesystem::path stagingExecutable;
        std::filesystem::path executable;
        std::wstring sha256;
    };

    struct transaction
    {
        std::wstring ownerSid;
        std::wstring serviceName;
        uint16_t runtimeTrack{};
        bool existing{};
        bool previousWasRunning{};
        ptlsmr::file_version previousVersion{};
        std::filesystem::path previousPath;
        std::wstring previousSha256;
        std::wstring previousTransactionId;
        ptlsmr::file_version candidateVersion{};
        std::filesystem::path stagingPath;
        std::filesystem::path candidatePath;
        std::wstring candidateSha256;
        std::wstring candidateTransactionId;
        std::wstring phase;
    };

    struct cleanup_transaction
    {
        std::wstring ownerSid;
        std::wstring serviceName;
        uint16_t runtimeTrack{};
        ptlsmr::file_version runtimeVersion{};
        std::wstring runtimeSha256;
        std::wstring transactionId;
        std::wstring phase;
    };

    struct acquisition_transaction
    {
        std::wstring ownerSid;
        std::wstring releaseId;
        std::wstring manifestHash;
        uint16_t runtimeTrack{};
        ptlsmr::file_version runtimeVersion{};
        std::wstring targetRuntimeSha256;
        std::wstring targetTransactionId;
        bool previousRuntimePresent{};
        uint16_t previousRuntimeTrack{};
        ptlsmr::file_version previousRuntimeVersion{};
        std::wstring previousRuntimeSha256;
        std::wstring previousTransactionId;
        ptlsmr::file_version beforeRuntimeFloor{};
        ptlsmr::file_version targetRuntimeFloor{};
        uint64_t beforeSecurityEpoch{};
        uint64_t targetSecurityEpoch{};
        std::wstring beforeSecurityStateHash;
        std::wstring targetSecurityStateHash;
        std::wstring phase;
    };

    constexpr size_t MaxManagedOwners = ptlsmr::MaxLeases;
    constexpr size_t MaxAcceptedReleases = 128;

    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    SERVICE_STATUS g_status{};
    ptlsmr::unique_handle g_stopEvent;

    enum class pipe_io_result
    {
        completed,
        disconnected,
        stopped,
    };

    [[nodiscard]] constexpr bool is_expected_pipe_disconnect(DWORD error) noexcept
    {
        return error == ERROR_BROKEN_PIPE ||
            error == ERROR_NO_DATA ||
            error == ERROR_PIPE_NOT_CONNECTED ||
            error == ERROR_MORE_DATA ||
            error == ERROR_OPERATION_ABORTED;
    }

    void cancel_and_reap_pending_io(HANDLE pipe, OVERLAPPED* operation)
    {
        if (!CancelIoEx(pipe, operation))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_NOT_FOUND)
            {
                throw ptlsmr::win32_error("CancelIoEx(pipe operation)", error);
            }
        }

        DWORD transferred = 0;
        if (!GetOverlappedResult(pipe, operation, &transferred, TRUE) &&
            !is_expected_pipe_disconnect(GetLastError()))
        {
            throw ptlsmr::win32_error("GetOverlappedResult(pipe cancellation)", GetLastError());
        }
    }

    [[nodiscard]] pipe_io_result perform_stop_aware_pipe_io(
        HANDLE pipe,
        void* buffer,
        DWORD bytes,
        DWORD& transferred,
        bool writeOperation)
    {
        overlapped_pipe_operation operation;
        const BOOL completed = writeOperation
            ? WriteFile(pipe, buffer, bytes, &transferred, operation.get())
            : ReadFile(pipe, buffer, bytes, &transferred, operation.get());
        if (completed)
        {
            return pipe_io_result::completed;
        }

        const DWORD initialError = GetLastError();
        if (is_expected_pipe_disconnect(initialError))
        {
            return pipe_io_result::disconnected;
        }
        if (initialError != ERROR_IO_PENDING)
        {
            throw ptlsmr::win32_error(
                writeOperation ? "WriteFile(overlapped pipe)" : "ReadFile(overlapped pipe)",
                initialError);
        }

        const HANDLE waits[] = { g_stopEvent.get(), operation.event() };
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0)
        {
            cancel_and_reap_pending_io(pipe, operation.get());
            return pipe_io_result::stopped;
        }
        if (wait != WAIT_OBJECT_0 + 1)
        {
            const DWORD error = wait == WAIT_FAILED ? GetLastError() : ERROR_GEN_FAILURE;
            cancel_and_reap_pending_io(pipe, operation.get());
            throw ptlsmr::win32_error("WaitForMultipleObjects(pipe I/O)", error);
        }
        if (!GetOverlappedResult(pipe, operation.get(), &transferred, FALSE))
        {
            const DWORD error = GetLastError();
            if (is_expected_pipe_disconnect(error))
            {
                return pipe_io_result::disconnected;
            }
            throw ptlsmr::win32_error(
                writeOperation ? "GetOverlappedResult(pipe write)" : "GetOverlappedResult(pipe read)",
                error);
        }
        return pipe_io_result::completed;
    }

    [[nodiscard]] pipe_io_result connect_stop_aware_pipe(HANDLE pipe)
    {
        overlapped_pipe_operation operation;
        if (ConnectNamedPipe(pipe, operation.get()))
        {
            return pipe_io_result::completed;
        }

        const DWORD initialError = GetLastError();
        if (initialError == ERROR_PIPE_CONNECTED)
        {
            return pipe_io_result::completed;
        }
        if (is_expected_pipe_disconnect(initialError))
        {
            return pipe_io_result::disconnected;
        }
        if (initialError != ERROR_IO_PENDING)
        {
            throw ptlsmr::win32_error("ConnectNamedPipe(overlapped)", initialError);
        }

        const HANDLE waits[] = { g_stopEvent.get(), operation.event() };
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0)
        {
            cancel_and_reap_pending_io(pipe, operation.get());
            return pipe_io_result::stopped;
        }
        if (wait != WAIT_OBJECT_0 + 1)
        {
            const DWORD error = wait == WAIT_FAILED ? GetLastError() : ERROR_GEN_FAILURE;
            cancel_and_reap_pending_io(pipe, operation.get());
            throw ptlsmr::win32_error("WaitForMultipleObjects(pipe connect)", error);
        }

        DWORD transferred = 0;
        if (!GetOverlappedResult(pipe, operation.get(), &transferred, FALSE))
        {
            const DWORD error = GetLastError();
            if (is_expected_pipe_disconnect(error))
            {
                return pipe_io_result::disconnected;
            }
            throw ptlsmr::win32_error("GetOverlappedResult(pipe connect)", error);
        }
        return pipe_io_result::completed;
    }

    [[nodiscard]] std::filesystem::path module_path()
    {
        std::wstring path(32768, L'\0');
        const DWORD characters = GetModuleFileNameW(
            nullptr,
            path.data(),
            static_cast<DWORD>(path.size()));
        if (characters == 0 || characters >= path.size())
        {
            throw ptlsmr::win32_error("GetModuleFileNameW(updater)", GetLastError());
        }
        path.resize(characters);
        return path;
    }

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw ptlsmr::win32_error("bounded reply", ERROR_BUFFER_OVERFLOW);
        }
        std::copy(source.begin(), source.end(), destination);
        destination[source.size()] = L'\0';
    }

    void report_status(DWORD state, DWORD error = ERROR_SUCCESS)
    {
        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = error;
        g_status.dwControlsAccepted =
            state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        g_status.dwCheckPoint = state == SERVICE_START_PENDING ? 1 : 0;
        g_status.dwWaitHint = state == SERVICE_START_PENDING ? 10000 : 0;
        if (g_statusHandle)
        {
            SetServiceStatus(g_statusHandle, &g_status);
        }
    }

    [[nodiscard]] std::filesystem::path inventory_path()
    {
        return ptlsmr::program_data_root() / L"runtime-inventory.txt";
    }

    [[nodiscard]] std::filesystem::path journal_path()
    {
        return ptlsmr::program_data_root() / L"runtime-transaction.txt";
    }

    [[nodiscard]] std::filesystem::path cleanup_journal_path()
    {
        return ptlsmr::program_data_root() / L"runtime-cleanup-transaction.txt";
    }

    template<typename Validate>
    void recover_atomic_journal_replacement(
        const std::filesystem::path& primary,
        Validate&& validate,
        const char* operation)
    {
        const std::filesystem::path replacement = primary.wstring() + L".new";
        if (std::filesystem::exists(primary))
        {
            if (std::filesystem::exists(replacement) &&
                !DeleteFileW(replacement.c_str()))
            {
                throw ptlsmr::win32_error(operation, GetLastError());
            }
            return;
        }
        if (!std::filesystem::exists(replacement))
        {
            return;
        }
        validate(replacement);
        ptlsmr::check_bool(
            MoveFileExW(
                replacement.c_str(),
                primary.c_str(),
                MOVEFILE_WRITE_THROUGH),
            operation);
    }

    [[nodiscard]] std::vector<std::wstring_view> split(
        std::wstring_view input,
        wchar_t separator)
    {
        std::vector<std::wstring_view> output;
        size_t start = 0;
        while (start <= input.size())
        {
            const size_t end = input.find(separator, start);
            output.push_back(input.substr(
                start,
                (end == std::wstring_view::npos ? input.size() : end) - start));
            if (end == std::wstring_view::npos)
            {
                break;
            }
            start = end + 1;
        }
        return output;
    }

    [[nodiscard]] bool is_hex_sha256(std::wstring_view value);

    [[nodiscard]] constexpr bool is_transaction_id(std::wstring_view value)
    {
        return value.size() == ptlsmr::TransactionIdChars &&
            std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return (character >= L'0' && character <= L'9') ||
                    (character >= L'a' && character <= L'f');
            });
    }

    [[nodiscard]] std::vector<managed_instance> read_instances()
    {
        if (!std::filesystem::exists(inventory_path()))
        {
            throw ptlsmr::win32_error("required runtime inventory state", ERROR_FILE_NOT_FOUND);
        }
        const std::wstring contents = ptlsmr::read_utf8_file(inventory_path(), 16 * 1024);
        std::vector<managed_instance> instances;
        size_t start = 0;
        while (start < contents.size())
        {
            const size_t end = contents.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                contents.data() + start,
                (end == std::wstring::npos ? contents.size() : end) - start);
            if (!line.empty())
            {
                const auto fields = split(line, L'|');
                if (fields.size() != 5 ||
                    (fields[1] != L"1" && fields[1] != L"2"))
                {
                    throw ptlsmr::win32_error("runtime inventory format", ERROR_INVALID_DATA);
                }
                managed_instance instance{
                    ptlsmr::canonical_owner_sid(fields[0]),
                    static_cast<uint16_t>(fields[1][0] - L'0'),
                    ptlsmr::parse_version(fields[2]),
                    ptlsmr::canonical_signer_sha256(fields[3]),
                    std::wstring(fields[4]),
                };
                if (instance.runtimeVersion.major != instance.runtimeTrack ||
                    !is_hex_sha256(instance.runtimeSha256) ||
                    !is_transaction_id(instance.transactionId) ||
                    std::any_of(
                        instances.begin(),
                        instances.end(),
                        [&](const managed_instance& value) {
                            return value.ownerSid == instance.ownerSid;
                        }))
                {
                    throw ptlsmr::win32_error("runtime inventory identity policy", ERROR_INVALID_DATA);
                }
                instances.push_back(std::move(instance));
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (contents[end] == L'\r' && start < contents.size() && contents[start] == L'\n')
            {
                ++start;
            }
        }
        if (instances.size() > MaxManagedOwners)
        {
            throw ptlsmr::win32_error("runtime inventory limit", ERROR_TOO_MANY_NAMES);
        }
        return instances;
    }

    void write_instances(std::vector<managed_instance> instances)
    {
        if (instances.size() > MaxManagedOwners)
        {
            throw ptlsmr::win32_error("runtime inventory write limit", ERROR_TOO_MANY_NAMES);
        }
        std::sort(
            instances.begin(),
            instances.end(),
            [](const managed_instance& left, const managed_instance& right) {
                return left.ownerSid < right.ownerSid;
            });
        std::wstringstream output;
        for (const auto& instance : instances)
        {
            if (ptlsmr::canonical_owner_sid(instance.ownerSid) != instance.ownerSid ||
                (instance.runtimeTrack != 1 && instance.runtimeTrack != 2) ||
                instance.runtimeVersion.major != instance.runtimeTrack ||
                !is_hex_sha256(instance.runtimeSha256) ||
                !is_transaction_id(instance.transactionId))
            {
                throw ptlsmr::win32_error("runtime inventory write policy", ERROR_INVALID_DATA);
            }
            output << instance.ownerSid << L"|"
                   << instance.runtimeTrack << L"|"
                   << ptlsmr::format_version(instance.runtimeVersion) << L"|"
                   << instance.runtimeSha256 << L"|"
                   << instance.transactionId << L"\r\n";
        }
        ptlsmr::write_utf8_file_atomic(inventory_path(), output.str());
    }

    [[nodiscard]] std::optional<managed_instance> find_instance(
        const std::vector<managed_instance>& instances,
        std::wstring_view owner)
    {
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& instance) {
                return instance.ownerSid == owner;
            });
        if (found == instances.end())
        {
            return std::nullopt;
        }
        return *found;
    }

    [[nodiscard]] std::optional<std::wstring> sibling_owner(
        const std::vector<managed_instance>& instances,
        std::wstring_view owner)
    {
        for (const auto& instance : instances)
        {
            if (instance.ownerSid != owner)
            {
                return instance.ownerSid;
            }
        }
        return std::nullopt;
    }

    void upsert_instance(
        const std::wstring& owner,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& version,
        std::wstring_view runtimeSha256,
        std::wstring_view transactionId)
    {
        auto instances = read_instances();
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& instance) {
                return instance.ownerSid == owner;
            });
        if (found == instances.end())
        {
                if (instances.size() >= MaxManagedOwners)
                {
                    throw ptlsmr::win32_error("runtime inventory append limit", ERROR_TOO_MANY_NAMES);
                }
                instances.push_back({
                    owner,
                    runtimeTrack,
                    version,
                    std::wstring(runtimeSha256),
                    std::wstring(transactionId),
                });
            }
        else
        {
            found->runtimeTrack = runtimeTrack;
            found->runtimeVersion = version;
            found->runtimeSha256 = runtimeSha256;
            found->transactionId = transactionId;
        }
        write_instances(std::move(instances));
    }

    [[nodiscard]] std::optional<managed_instance> remove_instance(const std::wstring& owner)
    {
        auto instances = read_instances();
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& instance) {
                return instance.ownerSid == owner;
            });
        if (found == instances.end())
        {
            return std::nullopt;
        }
        const auto result = *found;
        instances.erase(found);
        write_instances(std::move(instances));
        return result;
    }

    [[nodiscard]] bool version_is_referenced(
        const std::vector<managed_instance>& instances,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& version)
    {
        return std::any_of(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& instance) {
                return instance.runtimeTrack == runtimeTrack &&
                    instance.runtimeVersion == version;
            });
    }

    [[nodiscard]] bool equal_path(
        const std::filesystem::path& left,
        const std::filesystem::path& right)
    {
        const auto canonicalLeft = std::filesystem::weakly_canonical(left).wstring();
        const auto canonicalRight = std::filesystem::weakly_canonical(right).wstring();
        return CompareStringOrdinal(
                   canonicalLeft.c_str(),
                   static_cast<int>(canonicalLeft.size()),
                   canonicalRight.c_str(),
                   static_cast<int>(canonicalRight.size()),
                   TRUE) == CSTR_EQUAL;
    }

    [[nodiscard]] std::wstring runtime_command(
        const std::filesystem::path& executable,
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& runtimeVersion,
        const std::optional<std::wstring>& sibling)
    {
        std::wstring command =
            ptlsmr::quote_argument(executable.wstring()) +
            L" --service-name " + ptlsmr::quote_argument(names.serviceName) +
            L" --owner-sid " + ptlsmr::quote_argument(names.ownerSid) +
            L" --runtime-track " + std::to_wstring(runtimeTrack) +
            L" --runtime-version " + ptlsmr::quote_argument(ptlsmr::format_version(runtimeVersion));
        if (sibling)
        {
            command += L" --sibling-owner-sid " + ptlsmr::quote_argument(*sibling);
        }
        return command;
    }

    [[nodiscard]] bool matches_runtime_command(
        const std::wstring& command,
        const std::filesystem::path& executable,
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& runtimeVersion,
        const std::optional<std::wstring>& sibling)
    {
        int count = 0;
        LPWSTR* raw = CommandLineToArgvW(command.c_str(), &count);
        if (!raw)
        {
            return false;
        }
        ptlsmr::local_memory arguments(raw);
        const int expectedCount = sibling ? 11 : 9;
        if (count != expectedCount ||
            !equal_path(raw[0], executable) ||
            raw[1] != std::wstring_view(L"--service-name") ||
            raw[2] != names.serviceName ||
            raw[3] != std::wstring_view(L"--owner-sid") ||
            raw[4] != names.ownerSid ||
            raw[5] != std::wstring_view(L"--runtime-track") ||
            raw[6] != std::to_wstring(runtimeTrack) ||
            raw[7] != std::wstring_view(L"--runtime-version") ||
            raw[8] != ptlsmr::format_version(runtimeVersion))
        {
            return false;
        }
        return !sibling ||
            (raw[9] == std::wstring_view(L"--sibling-owner-sid") &&
             raw[10] == *sibling);
    }

    [[nodiscard]] service_handle open_scm()
    {
        SC_HANDLE raw = OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE);
        if (!raw)
        {
            throw ptlsmr::win32_error("OpenSCManagerW", GetLastError());
        }
        return service_handle(raw);
    }

    [[nodiscard]] std::vector<BYTE> query_service_config(SC_HANDLE service)
    {
        DWORD bytes = 0;
        QueryServiceConfigW(service, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("QueryServiceConfigW(size)", GetLastError());
        }
        std::vector<BYTE> buffer(bytes);
        ptlsmr::check_bool(
            QueryServiceConfigW(
                service,
                reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.data()),
                bytes,
                &bytes),
            "QueryServiceConfigW");
        return buffer;
    }

    [[nodiscard]] SERVICE_STATUS_PROCESS query_status(SC_HANDLE service)
    {
        SERVICE_STATUS_PROCESS status{};
        DWORD bytes = 0;
        ptlsmr::check_bool(
            QueryServiceStatusEx(
                service,
                SC_STATUS_PROCESS_INFO,
                reinterpret_cast<BYTE*>(&status),
                sizeof(status),
                &bytes),
            "QueryServiceStatusEx(runtime)");
        return status;
    }

    void wait_for_state(SC_HANDLE service, DWORD expected)
    {
        for (DWORD elapsed = 0; elapsed < 30000; elapsed += 200)
        {
            const auto status = query_status(service);
            if (status.dwCurrentState == expected)
            {
                return;
            }
            if (status.dwCurrentState == SERVICE_STOPPED && expected != SERVICE_STOPPED)
            {
                throw ptlsmr::win32_error("runtime service readiness", status.dwWin32ExitCode);
            }
            Sleep(200);
        }
        throw ptlsmr::win32_error("runtime service state timeout", ERROR_TIMEOUT);
    }

    void stop_service(SC_HANDLE service)
    {
        if (query_status(service).dwCurrentState == SERVICE_STOPPED)
        {
            return;
        }
        SERVICE_STATUS ignored{};
        if (!ControlService(service, SERVICE_CONTROL_STOP, &ignored) &&
            GetLastError() != ERROR_SERVICE_NOT_ACTIVE)
        {
            throw ptlsmr::win32_error("ControlService(STOP)", GetLastError());
        }
        wait_for_state(service, SERVICE_STOPPED);
    }

    void start_service(SC_HANDLE service)
    {
        if (query_status(service).dwCurrentState == SERVICE_RUNNING)
        {
            return;
        }
        if (!StartServiceW(service, 0, nullptr) &&
            GetLastError() != ERROR_SERVICE_ALREADY_RUNNING)
        {
            throw ptlsmr::win32_error("StartServiceW(runtime)", GetLastError());
        }
        wait_for_state(service, SERVICE_RUNNING);
    }

    [[nodiscard]] service_handle create_or_open_service(
        const service_handle& scm,
        const ptlsmr::InstanceNames& names,
        const std::wstring& desiredCommand,
        bool& created)
    {
        const std::wstring virtualAccount = L"NT SERVICE\\" + names.serviceName;
        SC_HANDLE raw = CreateServiceW(
            scm.get(),
            names.serviceName.c_str(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG |
                SERVICE_START | SERVICE_STOP | DELETE,
            SERVICE_WIN32_OWN_PROCESS,
            SERVICE_DEMAND_START,
            SERVICE_ERROR_NORMAL,
            desiredCommand.c_str(),
            nullptr,
            nullptr,
            nullptr,
            virtualAccount.c_str(),
            nullptr);
        if (raw)
        {
            created = true;
            return service_handle(raw);
        }
        if (GetLastError() != ERROR_SERVICE_EXISTS)
        {
            throw ptlsmr::win32_error("CreateServiceW(runtime)", GetLastError());
        }
        raw = OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG |
                SERVICE_START | SERVICE_STOP | DELETE);
        if (!raw)
        {
            throw ptlsmr::win32_error("OpenServiceW(runtime)", GetLastError());
        }
        return service_handle(raw);
    }

    void configure_service_sid(SC_HANDLE service)
    {
        SERVICE_SID_INFO sidInfo{ SERVICE_SID_TYPE_UNRESTRICTED };
        ptlsmr::check_bool(
            ChangeServiceConfig2W(
                service,
                SERVICE_CONFIG_SERVICE_SID_INFO,
                &sidInfo),
            "ChangeServiceConfig2W(SERVICE_SID_TYPE_UNRESTRICTED)");
    }

    void verify_service_account(
        SC_HANDLE service,
        const ptlsmr::InstanceNames& names)
    {
        const auto configBuffer = query_service_config(service);
        const auto* config = reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(configBuffer.data());
        const std::wstring expectedAccount = L"NT SERVICE\\" + names.serviceName;
        if (config->dwServiceType != SERVICE_WIN32_OWN_PROCESS ||
            !config->lpServiceStartName ||
            _wcsicmp(config->lpServiceStartName, expectedAccount.c_str()) != 0)
        {
            throw ptlsmr::win32_error("runtime virtual-account policy", ERROR_ACCESS_DENIED);
        }
    }

    void repath_service(
        SC_HANDLE service,
        const std::filesystem::path& executable,
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& runtimeVersion,
        const std::optional<std::wstring>& sibling)
    {
        const auto command = runtime_command(
            executable,
            names,
            runtimeTrack,
            runtimeVersion,
            sibling);
        ptlsmr::check_bool(
            ChangeServiceConfigW(
                service,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                command.c_str(),
                nullptr,
                nullptr,
                nullptr,
                nullptr,
                nullptr,
                nullptr),
            "ChangeServiceConfigW(runtime ImagePath)");
    }

    [[nodiscard]] bool service_matches(
        SC_HANDLE service,
        const std::filesystem::path& executable,
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack,
        const ptlsmr::file_version& runtimeVersion,
        const std::optional<std::wstring>& sibling)
    {
        verify_service_account(service, names);
        const auto configBuffer = query_service_config(service);
        const auto* config = reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(configBuffer.data());
        return config->lpBinaryPathName &&
            matches_runtime_command(
                config->lpBinaryPathName,
                executable,
                names,
                runtimeTrack,
                runtimeVersion,
                sibling);
    }

    void grant_runtime_execute_access(
        const std::filesystem::path& runtimeDirectory,
        std::wstring_view serviceSid)
    {
        std::wstring sidText(serviceSid);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(sidText.c_str(), &sid))
        {
            throw ptlsmr::win32_error("ConvertStringSidToSidW(runtime access)", GetLastError());
        }
        ptlsmr::local_memory sidMemory(sid);
        for (const auto& [path, inheritance] : {
                 std::pair{ runtimeDirectory, static_cast<DWORD>(SUB_CONTAINERS_AND_OBJECTS_INHERIT) },
                 std::pair{ runtimeDirectory / ptlsmr::RuntimeExe, static_cast<DWORD>(NO_INHERITANCE) } })
        {
            PACL currentDacl = nullptr;
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            std::wstring mutablePath = path.wstring();
            const DWORD query = GetNamedSecurityInfoW(
                mutablePath.data(),
                SE_FILE_OBJECT,
                DACL_SECURITY_INFORMATION,
                nullptr,
                nullptr,
                &currentDacl,
                nullptr,
                &descriptor);
            if (query != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("GetNamedSecurityInfoW(runtime access)", query);
            }
            ptlsmr::local_memory descriptorMemory(descriptor);
            EXPLICIT_ACCESSW access{};
            access.grfAccessPermissions = FILE_GENERIC_READ | FILE_GENERIC_EXECUTE;
            access.grfAccessMode = GRANT_ACCESS;
            access.grfInheritance = inheritance;
            access.Trustee.TrusteeForm = TRUSTEE_IS_SID;
            access.Trustee.TrusteeType = TRUSTEE_IS_USER;
            access.Trustee.ptstrName = static_cast<LPWSTR>(sid);
            PACL updatedDacl = nullptr;
            const DWORD update = SetEntriesInAclW(1, &access, currentDacl, &updatedDacl);
            if (update != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("SetEntriesInAclW(runtime access)", update);
            }
            ptlsmr::local_memory aclMemory(updatedDacl);
            const DWORD set = SetNamedSecurityInfoW(
                mutablePath.data(),
                SE_FILE_OBJECT,
                DACL_SECURITY_INFORMATION,
                nullptr,
                nullptr,
                updatedDacl,
                nullptr);
            if (set != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("SetNamedSecurityInfoW(runtime access)", set);
            }
        }
    }

    [[nodiscard]] installed_candidate intake_runtime(
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedCandidate)
    {
        ptlsmr::protect_runtime_directory(ptlsmr::installation_root());
        ptlsmr::protect_runtime_directory(ptlsmr::runtime_root());
        ptlsmr::protect_runtime_directory(
            ptlsmr::runtime_root() / (L"Track" + std::to_wstring(runtimeTrack)));
        const auto stagedDirectory = ptlsmr::create_protected_staging_directory(
            ptlsmr::installation_root() / L"Staging",
            L"runtime");
        const auto stagedExecutable = stagedDirectory / ptlsmr::RuntimeExe;
        try
        {
            (void)ptlsmr::copy_file_to_protected_stage(
                suppliedCandidate,
                suppliedCandidate.parent_path(),
                stagedExecutable,
                ptlsmr::MaxRuntimeArtifactBytes);
            const auto version = ptlsmr::validate_runtime_candidate(
                stagedExecutable,
                runtimeTrack,
                ptlsmr::read_trusted_signer_pin());
            const auto runtimeDirectory = ptlsmr::runtime_install_directory(runtimeTrack, version);
            const auto destination = runtimeDirectory / ptlsmr::RuntimeExe;
            return {
                runtimeTrack,
                version,
                stagedDirectory,
                stagedExecutable,
                destination,
                ptlsmr::sha256_file(stagedExecutable),
            };
        }
        catch (...)
        {
            std::filesystem::remove_all(stagedDirectory);
            throw;
        }
    }

    void cleanup_unreferenced_runtimes()
    {
        const auto instances = read_instances();
        const auto signerPin = ptlsmr::read_trusted_signer_pin();
        for (const uint16_t track : std::array<uint16_t, 2>{ 1, 2 })
        {
            const auto trackDirectory = ptlsmr::runtime_root() / (L"Track" + std::to_wstring(track));
            if (!std::filesystem::is_directory(trackDirectory))
            {
                continue;
            }
            for (const auto& entry : std::filesystem::directory_iterator(trackDirectory))
            {
                if (!entry.is_directory())
                {
                    throw ptlsmr::win32_error("runtime directory layout policy", ERROR_INVALID_DATA);
                }
                const auto version = ptlsmr::parse_version(entry.path().filename().wstring());
                const auto expectedDirectory = ptlsmr::runtime_install_directory(track, version);
                if (version.major != track || !equal_path(entry.path(), expectedDirectory))
                {
                    throw ptlsmr::win32_error("runtime directory identity policy", ERROR_INVALID_DATA);
                }
                if (!version_is_referenced(instances, track, version))
                {
                    std::filesystem::remove_all(entry.path());
                    continue;
                }
                const auto executable = expectedDirectory / ptlsmr::RuntimeExe;
                if (!std::filesystem::is_regular_file(executable) ||
                    !(ptlsmr::validate_runtime_candidate(executable, track, signerPin) == version))
                {
                    throw ptlsmr::win32_error(
                        "referenced runtime directory identity policy",
                        ERROR_INVALID_DATA);
                }
            }
        }
    }

    void write_journal(const transaction& value)
    {
        std::wstringstream content;
        content << L"owner=" << value.ownerSid << L"\r\n";
        content << L"service=" << value.serviceName << L"\r\n";
        content << L"track=" << value.runtimeTrack << L"\r\n";
        content << L"existing=" << (value.existing ? L"1" : L"0") << L"\r\n";
        content << L"previousWasRunning=" << (value.previousWasRunning ? L"1" : L"0") << L"\r\n";
        content << L"previousVersion=" <<
            (value.existing ? ptlsmr::format_version(value.previousVersion) : L"") << L"\r\n";
        content << L"previousPath=" <<
            (value.existing ? value.previousPath.wstring() : L"") << L"\r\n";
        content << L"previousSha256=" <<
            (value.existing ? value.previousSha256 : L"") << L"\r\n";
        content << L"previousTransactionId=" <<
            (value.existing ? value.previousTransactionId : L"") << L"\r\n";
        content << L"candidateVersion=" << ptlsmr::format_version(value.candidateVersion) << L"\r\n";
        content << L"stagingPath=" << value.stagingPath.wstring() << L"\r\n";
        content << L"candidatePath=" << value.candidatePath.wstring() << L"\r\n";
        content << L"candidateSha256=" << value.candidateSha256 << L"\r\n";
        content << L"candidateTransactionId=" << value.candidateTransactionId << L"\r\n";
        content << L"phase=" << value.phase << L"\r\n";
        ptlsmr::write_utf8_file_atomic(journal_path(), content.str());
    }

    void set_phase(transaction& value, std::wstring_view phase)
    {
        value.phase = phase;
        write_journal(value);
    }

    [[nodiscard]] transaction read_journal(
        const std::filesystem::path& path = journal_path())
    {
        const auto content = ptlsmr::read_utf8_file(path, 16 * 1024);
        std::map<std::wstring, std::wstring, std::less<>> fields;
        size_t start = 0;
        while (start < content.size())
        {
            const size_t end = content.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                content.data() + start,
                (end == std::wstring::npos ? content.size() : end) - start);
            if (!line.empty())
            {
                const size_t separator = line.find(L'=');
                if (separator == std::wstring_view::npos ||
                    !fields.emplace(
                        std::wstring(line.substr(0, separator)),
                        std::wstring(line.substr(separator + 1))).second)
                {
                    throw ptlsmr::win32_error("transaction journal format", ERROR_INVALID_DATA);
                }
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (content[end] == L'\r' && start < content.size() && content[start] == L'\n')
            {
                ++start;
            }
        }
        const auto value = [&](std::wstring_view name) -> const std::wstring& {
            const auto found = fields.find(name);
            if (found == fields.end())
            {
                throw ptlsmr::win32_error("transaction journal missing field", ERROR_INVALID_DATA);
            }
            return found->second;
        };
        if (fields.size() != 15 ||
            (value(L"existing") != L"0" && value(L"existing") != L"1") ||
            (value(L"previousWasRunning") != L"0" && value(L"previousWasRunning") != L"1") ||
            (value(L"track") != L"1" && value(L"track") != L"2"))
        {
            throw ptlsmr::win32_error("transaction journal policy", ERROR_INVALID_DATA);
        }
        transaction output{};
        output.ownerSid = ptlsmr::canonical_owner_sid(value(L"owner"));
        output.serviceName = value(L"service");
        output.runtimeTrack = static_cast<uint16_t>(value(L"track")[0] - L'0');
        output.existing = value(L"existing") == L"1";
        output.previousWasRunning = value(L"previousWasRunning") == L"1";
        output.candidateVersion = ptlsmr::parse_version(value(L"candidateVersion"));
        output.stagingPath = value(L"stagingPath");
        output.candidatePath = value(L"candidatePath");
        output.candidateSha256 =
            ptlsmr::canonical_signer_sha256(value(L"candidateSha256"));
        output.candidateTransactionId = value(L"candidateTransactionId");
        output.phase = value(L"phase");
        if (output.existing)
        {
            output.previousVersion = ptlsmr::parse_version(value(L"previousVersion"));
            output.previousPath = value(L"previousPath");
            output.previousSha256 =
                ptlsmr::canonical_signer_sha256(value(L"previousSha256"));
            output.previousTransactionId = value(L"previousTransactionId");
        }
        else if (!value(L"previousVersion").empty() ||
                 !value(L"previousPath").empty() ||
                 !value(L"previousSha256").empty() ||
                 !value(L"previousTransactionId").empty())
        {
            throw ptlsmr::win32_error("first-install journal policy", ERROR_INVALID_DATA);
        }
        return output;
    }

    void clear_journal()
    {
        if (!DeleteFileW(journal_path().c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
        {
            throw ptlsmr::win32_error("DeleteFileW(transaction journal)", GetLastError());
        }
    }

    [[nodiscard]] bool phase_can_finalize_committed_candidate(std::wstring_view phase)
    {
        static constexpr std::array<std::wstring_view, 4> phases = {
            L"inventory-committed",
            L"sibling-sync-pending",
            L"siblings-synchronized",
            L"unreferenced-cleanup-pending",
        };
        return std::find(phases.begin(), phases.end(), phase) != phases.end();
    }

    [[nodiscard]] bool inventory_commits_candidate(const transaction& value)
    {
        if (!phase_can_finalize_committed_candidate(value.phase))
        {
            return false;
        }
        const auto committed = find_instance(read_instances(), value.ownerSid);
        return committed &&
            committed->runtimeTrack == value.runtimeTrack &&
            committed->runtimeVersion == value.candidateVersion &&
            committed->runtimeSha256 == value.candidateSha256 &&
            committed->transactionId == value.candidateTransactionId;
    }

    void validate_journal(const transaction& value)
    {
        const auto names = ptlsmr::instance_names(value.ownerSid);
        if (names.serviceName != value.serviceName ||
            value.runtimeTrack != value.candidateVersion.major ||
            !ptlsmr::path_is_within(
                value.stagingPath,
                ptlsmr::installation_root() / L"Staging") ||
            _wcsicmp(value.stagingPath.filename().c_str(), ptlsmr::RuntimeExe) != 0 ||
            !ptlsmr::path_is_within(value.candidatePath, ptlsmr::runtime_root()) ||
            !equal_path(
                value.candidatePath,
                ptlsmr::runtime_executable_path(value.runtimeTrack, value.candidateVersion)) ||
            !is_hex_sha256(value.candidateSha256) ||
            !is_transaction_id(value.candidateTransactionId))
        {
            throw ptlsmr::win32_error("transaction journal candidate policy", ERROR_INVALID_DATA);
        }
        static constexpr std::array<std::wstring_view, 13> phases = {
            L"validated-staged",
            L"final-installed",
            L"service-created",
            L"stop-pending",
            L"repath-pending",
            L"repathed",
            L"ready",
            L"inventory-commit-pending",
            L"inventory-committed",
            L"sibling-sync-pending",
            L"siblings-synchronized",
            L"unreferenced-cleanup-pending",
            L"rollback-cleanup-pending",
        };
        if (std::find(phases.begin(), phases.end(), value.phase) == phases.end())
        {
            throw ptlsmr::win32_error("transaction journal phase policy", ERROR_INVALID_DATA);
        }

        const bool stagingExists = std::filesystem::is_regular_file(value.stagingPath);
        const bool candidateExists = std::filesystem::is_regular_file(value.candidatePath);
        const auto signerPin = ptlsmr::read_trusted_signer_pin();
        if (stagingExists)
        {
            (void)ptlsmr::validate_runtime_candidate(
                value.stagingPath,
                value.runtimeTrack,
                signerPin);
            if (ptlsmr::sha256_file(value.stagingPath) != value.candidateSha256)
            {
                throw ptlsmr::win32_error(
                    "transaction journal staged hash policy",
                    ERROR_CRC);
            }
        }
        if (candidateExists)
        {
            (void)ptlsmr::validate_runtime_candidate(
                value.candidatePath,
                value.runtimeTrack,
                signerPin);
            const auto actualCandidateHash = ptlsmr::sha256_file(value.candidatePath);
            const bool sameVersionPath =
                value.existing &&
                equal_path(value.previousPath, value.candidatePath);
            if (actualCandidateHash != value.candidateSha256 &&
                !(sameVersionPath &&
                  value.phase == L"validated-staged" &&
                  actualCandidateHash == value.previousSha256))
            {
                throw ptlsmr::win32_error(
                    "transaction journal candidate hash policy",
                    ERROR_CRC);
            }
        }
        if ((!stagingExists && !candidateExists) &&
            value.phase != L"rollback-cleanup-pending")
        {
            throw ptlsmr::win32_error("transaction journal candidate presence policy", ERROR_FILE_NOT_FOUND);
        }
        if (value.phase != L"validated-staged" &&
            value.phase != L"rollback-cleanup-pending" &&
            !candidateExists)
        {
            throw ptlsmr::win32_error("transaction journal final candidate policy", ERROR_FILE_NOT_FOUND);
        }
        if (value.existing)
        {
            if (value.previousVersion.major != value.runtimeTrack ||
                !is_hex_sha256(value.previousSha256) ||
                !is_transaction_id(value.previousTransactionId) ||
                !ptlsmr::path_is_within(value.previousPath, ptlsmr::runtime_root()) ||
                !equal_path(
                    value.previousPath,
                    ptlsmr::runtime_executable_path(value.runtimeTrack, value.previousVersion)))
            {
                throw ptlsmr::win32_error("transaction journal previous policy", ERROR_INVALID_DATA);
            }
            const bool previousExists = std::filesystem::is_regular_file(value.previousPath);
            if (!previousExists && !inventory_commits_candidate(value))
            {
                throw ptlsmr::win32_error(
                    "transaction journal previous presence policy",
                    ERROR_FILE_NOT_FOUND);
            }
            if (previousExists)
            {
                (void)ptlsmr::validate_runtime_candidate(
                    value.previousPath,
                    value.runtimeTrack,
                    signerPin);
                const auto previousHash = ptlsmr::sha256_file(value.previousPath);
                const bool candidateReplacedSamePath =
                    equal_path(value.previousPath, value.candidatePath) &&
                    previousHash == value.candidateSha256 &&
                    phase_can_finalize_committed_candidate(value.phase);
                if (previousHash != value.previousSha256 &&
                    !candidateReplacedSamePath)
                {
                    throw ptlsmr::win32_error(
                        "transaction journal previous hash policy",
                        ERROR_CRC);
                }
            }
        }
    }

    void discard_staged_candidate(const std::filesystem::path& stagedExecutable)
    {
        if (!stagedExecutable.empty() && std::filesystem::exists(stagedExecutable.parent_path()))
        {
            std::filesystem::remove_all(stagedExecutable.parent_path());
        }
    }

    void maybe_crash(std::wstring_view requestedPhase, std::wstring_view actualPhase);

    void install_validated_candidate(transaction& value, std::wstring_view crashPhase)
    {
        if (std::filesystem::exists(value.candidatePath))
        {
            (void)ptlsmr::validate_runtime_candidate(
                value.candidatePath,
                value.runtimeTrack,
                ptlsmr::read_trusted_signer_pin());
            if (!ptlsmr::files_are_identical(value.stagingPath, value.candidatePath))
            {
                throw ptlsmr::win32_error("runtime version collision policy", ERROR_FILE_EXISTS);
            }
            discard_staged_candidate(value.stagingPath);
        }
        else
        {
            ptlsmr::protect_runtime_directory(value.candidatePath.parent_path());
            maybe_crash(crashPhase, L"after-target-directory-created");
            ptlsmr::move_file_atomically(value.stagingPath, value.candidatePath);
            discard_staged_candidate(value.stagingPath);
        }
        set_phase(value, L"final-installed");
    }

    void synchronize_probe_targets();

    void restore_transaction(const transaction& value)
    {
        transaction state = value;
        validate_journal(state);
        const auto names = ptlsmr::instance_names(state.ownerSid);
        const auto instances = read_instances();
        const auto committed = find_instance(instances, state.ownerSid);
        if (committed &&
            committed->runtimeTrack == state.runtimeTrack &&
            committed->runtimeVersion == state.candidateVersion &&
            committed->runtimeSha256 == state.candidateSha256 &&
            committed->transactionId == state.candidateTransactionId)
        {
            discard_staged_candidate(state.stagingPath);
            set_phase(state, L"sibling-sync-pending");
            synchronize_probe_targets();
            set_phase(state, L"siblings-synchronized");
            set_phase(state, L"unreferenced-cleanup-pending");
            cleanup_unreferenced_runtimes();
            clear_journal();
            return;
        }

        auto scm = open_scm();
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG |
                SERVICE_START | SERVICE_STOP | DELETE));
        if (!state.existing)
        {
            if (service)
            {
                stop_service(service.get());
                ptlsmr::check_bool(DeleteService(service.get()), "DeleteService(incomplete runtime)");
            }
            else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
            {
                throw ptlsmr::win32_error("OpenServiceW(incomplete runtime)", GetLastError());
            }
            if (std::filesystem::exists(names.storeDirectory))
            {
                std::filesystem::remove_all(names.storeDirectory);
            }
        }
        else
        {
            if (!service)
            {
                throw ptlsmr::win32_error("OpenServiceW(transaction recovery)", GetLastError());
            }
            const auto sibling = sibling_owner(instances, value.ownerSid);
            const bool candidateConfigured = service_matches(
                service.get(),
                state.candidatePath,
                names,
                state.runtimeTrack,
                state.candidateVersion,
                sibling);
            const bool previousConfigured = service_matches(
                service.get(),
                state.previousPath,
                names,
                state.runtimeTrack,
                state.previousVersion,
                sibling);
            if (!candidateConfigured && !previousConfigured)
            {
                throw ptlsmr::win32_error("transaction recovery ImagePath policy", ERROR_ACCESS_DENIED);
            }
            stop_service(service.get());
            if (!previousConfigured)
            {
                repath_service(
                    service.get(),
                    state.previousPath,
                    names,
                    state.runtimeTrack,
                    state.previousVersion,
                    sibling);
            }
            if (state.previousWasRunning)
            {
                start_service(service.get());
            }
        }
        discard_staged_candidate(state.stagingPath);
        set_phase(state, L"rollback-cleanup-pending");
        cleanup_unreferenced_runtimes();
        clear_journal();
    }

    void recover_incomplete_transaction()
    {
        recover_atomic_journal_replacement(
            journal_path(),
            [](const std::filesystem::path& path) {
                validate_journal(read_journal(path));
            },
            "recover runtime journal replacement");
        if (std::filesystem::exists(journal_path()))
        {
            restore_transaction(read_journal());
        }
    }

    void require_runtime_readiness(
        const ptlsmr::InstanceNames& names,
        const ptlsmr::file_version& expectedVersion)
    {
        const auto evidence = ptlsmr::read_utf8_file(names.evidencePath, 16 * 1024);
        if (evidence.find(L"runtimeVersion=" + ptlsmr::format_version(expectedVersion) + L"\r\n") ==
                std::wstring::npos ||
            evidence.find(L"readiness=ready\r\n") == std::wstring::npos)
        {
            throw ptlsmr::win32_error("runtime readiness evidence", ERROR_SERVICE_NOT_ACTIVE);
        }
    }

    void synchronize_probe_targets()
    {
        const auto instances = read_instances();
        auto scm = open_scm();
        const auto signerPin = ptlsmr::read_trusted_signer_pin();
        for (const auto& instance : instances)
        {
            const auto names = ptlsmr::instance_names(instance.ownerSid);
            const auto executable = ptlsmr::runtime_executable_path(
                instance.runtimeTrack,
                instance.runtimeVersion);
            const auto sibling = sibling_owner(instances, instance.ownerSid);
            (void)ptlsmr::validate_runtime_candidate(
                executable,
                instance.runtimeTrack,
                signerPin);
            if (ptlsmr::sha256_file(executable) != instance.runtimeSha256)
            {
                throw ptlsmr::win32_error(
                    "runtime inventory artifact hash policy",
                    ERROR_CRC);
            }
            bool created = false;
            auto service = create_or_open_service(
                scm,
                names,
                runtime_command(
                    executable,
                    names,
                    instance.runtimeTrack,
                    instance.runtimeVersion,
                    sibling),
                created);
            configure_service_sid(service.get());
            const auto runtimeServiceSid = ptlsmr::service_sid(names.serviceName);
            if (created)
            {
                grant_runtime_execute_access(executable.parent_path(), runtimeServiceSid);
            }
            ptlsmr::protect_directory_for_service(names.storeDirectory, runtimeServiceSid);
            if (!service_matches(
                    service.get(),
                    executable,
                    names,
                    instance.runtimeTrack,
                    instance.runtimeVersion,
                    sibling))
            {
                stop_service(service.get());
                repath_service(
                    service.get(),
                    executable,
                    names,
                    instance.runtimeTrack,
                    instance.runtimeVersion,
                    sibling);
                start_service(service.get());
                require_runtime_readiness(names, instance.runtimeVersion);
            }
            else if (query_status(service.get()).dwCurrentState != SERVICE_RUNNING)
            {
                start_service(service.get());
                require_runtime_readiness(names, instance.runtimeVersion);
            }
        }
    }

    void maybe_crash(std::wstring_view requestedPhase, std::wstring_view actualPhase)
    {
        if (requestedPhase == actualPhase)
        {
            TerminateProcess(GetCurrentProcess(), ERROR_PROCESS_ABORTED);
            Sleep(INFINITE);
        }
    }

    void maybe_fail(std::wstring_view requestedPhase, std::wstring_view actualPhase)
    {
        if (requestedPhase == actualPhase)
        {
            throw ptlsmr::win32_error("deterministic cleanup failure injection", ERROR_WRITE_FAULT);
        }
    }

    void write_cleanup_journal(const cleanup_transaction& value)
    {
        std::wstringstream content;
        content << L"owner=" << value.ownerSid << L"\r\n";
        content << L"service=" << value.serviceName << L"\r\n";
        content << L"track=" << value.runtimeTrack << L"\r\n";
        content << L"version=" << ptlsmr::format_version(value.runtimeVersion) << L"\r\n";
        content << L"runtimeSha256=" << value.runtimeSha256 << L"\r\n";
        content << L"transactionId=" << value.transactionId << L"\r\n";
        content << L"phase=" << value.phase << L"\r\n";
        ptlsmr::write_utf8_file_atomic(cleanup_journal_path(), content.str());
    }

    void set_cleanup_phase(cleanup_transaction& value, std::wstring_view phase)
    {
        value.phase = phase;
        write_cleanup_journal(value);
    }

    [[nodiscard]] cleanup_transaction read_cleanup_journal(
        const std::filesystem::path& path = cleanup_journal_path())
    {
        const auto content = ptlsmr::read_utf8_file(path, 4096);
        std::map<std::wstring, std::wstring, std::less<>> fields;
        size_t start = 0;
        while (start < content.size())
        {
            const size_t end = content.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                content.data() + start,
                (end == std::wstring::npos ? content.size() : end) - start);
            if (!line.empty())
            {
                const size_t separator = line.find(L'=');
                if (separator == std::wstring_view::npos ||
                    !fields.emplace(
                        std::wstring(line.substr(0, separator)),
                        std::wstring(line.substr(separator + 1))).second)
                {
                    throw ptlsmr::win32_error("cleanup journal format", ERROR_INVALID_DATA);
                }
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (content[end] == L'\r' && start < content.size() && content[start] == L'\n')
            {
                ++start;
            }
        }
        const auto value = [&](std::wstring_view name) -> const std::wstring& {
            const auto found = fields.find(name);
            if (found == fields.end())
            {
                throw ptlsmr::win32_error("cleanup journal missing field", ERROR_INVALID_DATA);
            }
            return found->second;
        };
        if (fields.size() != 7 || (value(L"track") != L"1" && value(L"track") != L"2"))
        {
            throw ptlsmr::win32_error("cleanup journal policy", ERROR_INVALID_DATA);
        }
        return {
            ptlsmr::canonical_owner_sid(value(L"owner")),
            value(L"service"),
            static_cast<uint16_t>(value(L"track")[0] - L'0'),
            ptlsmr::parse_version(value(L"version")),
            ptlsmr::canonical_signer_sha256(value(L"runtimeSha256")),
            value(L"transactionId"),
            value(L"phase"),
        };
    }

    void clear_cleanup_journal()
    {
        if (!DeleteFileW(cleanup_journal_path().c_str()) && GetLastError() != ERROR_FILE_NOT_FOUND)
        {
            throw ptlsmr::win32_error("DeleteFileW(cleanup journal)", GetLastError());
        }
    }

    void validate_cleanup_journal(const cleanup_transaction& value)
    {
        const auto names = ptlsmr::instance_names(value.ownerSid);
        static constexpr std::array<std::wstring_view, 5> phases = {
            L"prepared",
            L"service-deleted",
            L"inventory-removed",
            L"store-removed",
            L"sibling-sync-pending",
        };
        if (names.serviceName != value.serviceName ||
            value.runtimeTrack != value.runtimeVersion.major ||
            !is_hex_sha256(value.runtimeSha256) ||
            !is_transaction_id(value.transactionId) ||
            std::find(phases.begin(), phases.end(), value.phase) == phases.end())
        {
            throw ptlsmr::win32_error("cleanup journal identity policy", ERROR_INVALID_DATA);
        }
    }

    void delete_runtime_service(
        const service_handle& scm,
        const ptlsmr::InstanceNames& names,
        const char* operation)
    {
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
        if (!service)
        {
            if (GetLastError() == ERROR_SERVICE_DOES_NOT_EXIST)
            {
                return;
            }
            throw ptlsmr::win32_error(operation, GetLastError());
        }
        stop_service(service.get());
        ptlsmr::check_bool(DeleteService(service.get()), std::string(operation).c_str());
    }

    void finalize_cleanup_transaction(cleanup_transaction state)
    {
        validate_cleanup_journal(state);
        const auto current = find_instance(read_instances(), state.ownerSid);
        if (current &&
            (current->runtimeTrack != state.runtimeTrack ||
             current->runtimeVersion != state.runtimeVersion ||
             current->runtimeSha256 != state.runtimeSha256 ||
             current->transactionId != state.transactionId))
        {
            // A later committed provision supersedes this cleanup journal.
            // Reconcile against that durable inventory before discarding the stale intent.
            synchronize_probe_targets();
            cleanup_unreferenced_runtimes();
            clear_cleanup_journal();
            return;
        }
        const auto names = ptlsmr::instance_names(state.ownerSid);
        auto scm = open_scm();
        delete_runtime_service(scm, names, "DeleteService(cleanup runtime)");
        set_cleanup_phase(state, L"service-deleted");
        (void)remove_instance(state.ownerSid);
        set_cleanup_phase(state, L"inventory-removed");
        if (std::filesystem::exists(names.storeDirectory))
        {
            std::filesystem::remove_all(names.storeDirectory);
        }
        set_cleanup_phase(state, L"store-removed");
        set_cleanup_phase(state, L"sibling-sync-pending");
        synchronize_probe_targets();
        cleanup_unreferenced_runtimes();
        clear_cleanup_journal();
    }

    void cleanup_unreferenced_owner_stores()
    {
        const auto is_owner_store_suffix = [](std::wstring_view value) {
            return value.size() == 16 &&
                std::all_of(value.begin(), value.end(), [](wchar_t character) {
                    return (character >= L'0' && character <= L'9') ||
                        (character >= L'a' && character <= L'f');
                });
        };
        const auto instances = read_instances();
        std::vector<std::filesystem::path> referenced;
        referenced.reserve(instances.size());
        for (const auto& instance : instances)
        {
            referenced.push_back(ptlsmr::instance_names(instance.ownerSid).storeDirectory);
        }
        const auto root = ptlsmr::program_data_root();
        if (!std::filesystem::is_directory(root))
        {
            return;
        }
        for (const auto& entry : std::filesystem::directory_iterator(root))
        {
            if (entry.is_symlink())
            {
                throw ptlsmr::win32_error(
                    "protected owner-store reparse policy",
                    ERROR_REPARSE_TAG_INVALID);
            }
            if (!entry.is_directory())
            {
                continue;
            }
            const auto name = entry.path().filename().wstring();
            if (_wcsicmp(name.c_str(), L"Policy") == 0 ||
                _wcsicmp(name.c_str(), L"Requests") == 0)
            {
                continue;
            }
            if (!is_owner_store_suffix(name) || !ptlsmr::path_is_within(entry.path(), root))
            {
                throw ptlsmr::win32_error(
                    "protected owner-store directory identity policy",
                    ERROR_INVALID_DATA);
            }
            const bool known = std::any_of(
                referenced.begin(),
                referenced.end(),
                [&](const auto& path) {
                    return equal_path(entry.path(), path);
                });
            if (!known)
            {
                std::filesystem::remove_all(entry.path());
            }
        }
    }

    void cleanup_unreferenced_runtime_services()
    {
        auto scm = service_handle(OpenSCManagerW(
            nullptr,
            nullptr,
            SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(service reconciliation)", GetLastError());
        }
        DWORD bytes = 0;
        DWORD count = 0;
        DWORD resume = 0;
        (void)EnumServicesStatusExW(
            scm.get(),
            SC_ENUM_PROCESS_INFO,
            SERVICE_WIN32,
            SERVICE_STATE_ALL,
            nullptr,
            0,
            &bytes,
            &count,
            &resume,
            nullptr);
        if (GetLastError() != ERROR_MORE_DATA)
        {
            throw ptlsmr::win32_error("EnumServicesStatusExW(service reconciliation size)", GetLastError());
        }
        std::vector<BYTE> buffer(bytes);
        resume = 0;
        ptlsmr::check_bool(
            EnumServicesStatusExW(
                scm.get(),
                SC_ENUM_PROCESS_INFO,
                SERVICE_WIN32,
                SERVICE_STATE_ALL,
                buffer.data(),
                static_cast<DWORD>(buffer.size()),
                &bytes,
                &count,
                &resume,
                nullptr),
            "EnumServicesStatusExW(service reconciliation)");
        const auto instances = read_instances();
        const auto* services = reinterpret_cast<const ENUM_SERVICE_STATUS_PROCESSW*>(buffer.data());
        for (DWORD index = 0; index < count; ++index)
        {
            const std::wstring_view name(services[index].lpServiceName);
            if (!name.starts_with(L"PtPuvrRuntime_"))
            {
                continue;
            }
            const bool referenced = std::any_of(
                instances.begin(),
                instances.end(),
                [&](const managed_instance& instance) {
                    return ptlsmr::instance_names(instance.ownerSid).serviceName == name;
                });
            if (!referenced)
            {
                service_handle service(OpenServiceW(
                    scm.get(),
                    services[index].lpServiceName,
                    SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
                if (!service)
                {
                    throw ptlsmr::win32_error(
                        "OpenServiceW(unreferenced runtime reconciliation)",
                        GetLastError());
                }
                stop_service(service.get());
                ptlsmr::check_bool(
                    DeleteService(service.get()),
                    "DeleteService(unreferenced runtime reconciliation)");
            }
        }
    }

    void cleanup_staging_directories()
    {
        const auto staging = ptlsmr::installation_root() / L"Staging";
        if (!std::filesystem::is_directory(staging))
        {
            return;
        }
        for (const auto& entry : std::filesystem::directory_iterator(staging))
        {
            std::filesystem::remove_all(entry.path());
        }
    }

    void reconcile_protected_state()
    {
        synchronize_probe_targets();
        cleanup_unreferenced_runtime_services();
        cleanup_unreferenced_owner_stores();
        cleanup_unreferenced_runtimes();
        cleanup_staging_directories();
    }

    void recover_incomplete_cleanup()
    {
        recover_atomic_journal_replacement(
            cleanup_journal_path(),
            [](const std::filesystem::path& path) {
                validate_cleanup_journal(read_cleanup_journal(path));
            },
            "recover cleanup journal replacement");
        if (std::filesystem::exists(cleanup_journal_path()))
        {
            finalize_cleanup_transaction(read_cleanup_journal());
        }
    }

    void recover_incomplete_acquisition();

    void converge_pending_transactions()
    {
        recover_incomplete_transaction();
        recover_incomplete_cleanup();
        recover_incomplete_acquisition();
        if (std::filesystem::exists(journal_path()) ||
            std::filesystem::exists(cleanup_journal_path()) ||
            std::filesystem::exists(ptlsmr::acquisition_journal_path()) ||
            std::filesystem::exists(journal_path().wstring() + L".new") ||
            std::filesystem::exists(cleanup_journal_path().wstring() + L".new") ||
            std::filesystem::exists(
                ptlsmr::acquisition_journal_path().wstring() + L".new"))
        {
            throw ptlsmr::win32_error("transaction convergence policy", ERROR_BUSY);
        }
        reconcile_protected_state();
    }

    void provision(
        const std::wstring& owner,
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedCandidate,
        std::wstring_view crashPhase,
        std::wstring_view requestedTransactionId)
    {
        bool journalWritten = false;
        std::optional<installed_candidate> candidate;
        try
        {
            const auto names = ptlsmr::instance_names(owner);
            const auto before = read_instances();
            const auto previous = find_instance(before, owner);
            if (!previous && before.size() >= MaxManagedOwners)
            {
                throw ptlsmr::win32_error(
                    "runtime inventory pre-staging limit",
                    ERROR_TOO_MANY_NAMES);
            }
            candidate = intake_runtime(runtimeTrack, suppliedCandidate);
            const std::wstring transactionId = requestedTransactionId.empty()
                ? ptlsmr::random_hex_identifier(16)
                : std::wstring(requestedTransactionId);
            if (!is_transaction_id(transactionId))
            {
                throw ptlsmr::win32_error(
                    "runtime transaction identifier policy",
                    ERROR_INVALID_DATA);
            }
            if (previous &&
                (candidate->version < previous->runtimeVersion ||
                 candidate->version == previous->runtimeVersion &&
                    candidate->runtimeTrack != previous->runtimeTrack))
            {
                throw ptlsmr::win32_error("runtime anti-downgrade policy", ERROR_REVISION_MISMATCH);
            }

            auto prospective = before;
            if (previous)
            {
                const auto found = std::find_if(
                    prospective.begin(),
                    prospective.end(),
                    [&](const managed_instance& instance) {
                        return instance.ownerSid == owner;
                    });
                found->runtimeTrack = candidate->runtimeTrack;
                found->runtimeVersion = candidate->version;
                found->runtimeSha256 = candidate->sha256;
                found->transactionId = transactionId;
            }
            else
            {
                if (prospective.size() >= MaxManagedOwners)
                {
                    throw ptlsmr::win32_error(
                        "runtime inventory prospective limit",
                        ERROR_TOO_MANY_NAMES);
                }
                prospective.push_back({
                    owner,
                    candidate->runtimeTrack,
                    candidate->version,
                    candidate->sha256,
                    transactionId,
                });
            }

            transaction state{};
            state.ownerSid = owner;
            state.serviceName = names.serviceName;
            state.runtimeTrack = candidate->runtimeTrack;
            state.existing = previous.has_value();
            state.previousWasRunning = false;
            state.candidateVersion = candidate->version;
            state.stagingPath = candidate->stagingExecutable;
            state.candidatePath = candidate->executable;
            state.phase = L"validated-staged";
            if (previous)
            {
                state.previousVersion = previous->runtimeVersion;
                state.previousPath = ptlsmr::runtime_executable_path(
                    previous->runtimeTrack,
                    previous->runtimeVersion);
                state.previousSha256 = previous->runtimeSha256;
                state.previousTransactionId = previous->transactionId;
                if (previous->runtimeTrack != candidate->runtimeTrack)
                {
                    throw ptlsmr::win32_error("runtime track migration policy", ERROR_REVISION_MISMATCH);
                }
                auto preflightScm = open_scm();
                service_handle preflightService(OpenServiceW(
                    preflightScm.get(),
                    names.serviceName.c_str(),
                    SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG));
                if (!preflightService)
                {
                    throw ptlsmr::win32_error("OpenServiceW(runtime preflight)", GetLastError());
                }
                verify_service_account(preflightService.get(), names);
                state.previousWasRunning =
                    query_status(preflightService.get()).dwCurrentState == SERVICE_RUNNING;
            }
            state.candidateSha256 = candidate->sha256;
            state.candidateTransactionId = transactionId;
            write_journal(state);
            journalWritten = true;
            maybe_crash(crashPhase, L"after-journal-prepared");
            install_validated_candidate(state, crashPhase);
            maybe_crash(crashPhase, L"after-final-install");

            auto scm = open_scm();
            const auto desiredSibling = sibling_owner(prospective, owner);
            bool created = false;
            auto service = create_or_open_service(
                scm,
                names,
                runtime_command(
                    candidate->executable,
                    names,
                    candidate->runtimeTrack,
                    candidate->version,
                    desiredSibling),
                created);
            configure_service_sid(service.get());
            const auto runtimeServiceSid = ptlsmr::service_sid(names.serviceName);
            grant_runtime_execute_access(candidate->executable.parent_path(), runtimeServiceSid);
            ptlsmr::protect_directory_for_service(names.storeDirectory, runtimeServiceSid);
            if (created)
            {
                set_phase(state, L"service-created");
            }
            else
            {
                verify_service_account(service.get(), names);
                if (!previous)
                {
                    throw ptlsmr::win32_error("runtime inventory/service mismatch", ERROR_INVALID_DATA);
                }
                const auto currentSibling = sibling_owner(before, owner);
                if (!service_matches(
                        service.get(),
                        state.previousPath,
                        names,
                        state.runtimeTrack,
                        state.previousVersion,
                        currentSibling))
                {
                    throw ptlsmr::win32_error("runtime ImagePath policy", ERROR_ACCESS_DENIED);
                }
                set_phase(state, L"stop-pending");
                stop_service(service.get());
                set_phase(state, L"repath-pending");
                repath_service(
                    service.get(),
                    candidate->executable,
                    names,
                    candidate->runtimeTrack,
                    candidate->version,
                    desiredSibling);
                set_phase(state, L"repathed");
                maybe_crash(crashPhase, L"after-scm-repath");
            }

            if (created)
            {
                set_phase(state, L"repath-pending");
                set_phase(state, L"repathed");
                maybe_crash(crashPhase, L"after-scm-repath");
            }
            start_service(service.get());
            require_runtime_readiness(names, candidate->version);
            set_phase(state, L"ready");
            set_phase(state, L"inventory-commit-pending");
            upsert_instance(
                owner,
                candidate->runtimeTrack,
                candidate->version,
                candidate->sha256,
                transactionId);
            set_phase(state, L"inventory-committed");
            maybe_crash(crashPhase, L"after-inventory-before-sync");
            set_phase(state, L"sibling-sync-pending");
            synchronize_probe_targets();
            set_phase(state, L"siblings-synchronized");
            set_phase(state, L"unreferenced-cleanup-pending");
            cleanup_unreferenced_runtimes();
            maybe_crash(crashPhase, L"after-unreferenced-runtime-delete");
            clear_journal();
            journalWritten = false;
        }
        catch (...)
        {
            if (journalWritten && std::filesystem::exists(journal_path()))
            {
                restore_transaction(read_journal());
            }
            else
            {
                if (candidate)
                {
                    discard_staged_candidate(candidate->stagingExecutable);
                    cleanup_unreferenced_runtimes();
                }
            }
            throw;
        }
    }

    void fill_status(const std::wstring& owner, ptlsmr::reply& response)
    {
        const auto instance = find_instance(read_instances(), owner);
        if (!instance)
        {
            throw ptlsmr::win32_error("owner inventory lookup", ERROR_NOT_FOUND);
        }
        const auto names = ptlsmr::instance_names(owner);
        auto scm = open_scm();
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(status)", GetLastError());
        }
        if (!service_matches(
                service.get(),
                ptlsmr::runtime_executable_path(instance->runtimeTrack, instance->runtimeVersion),
                names,
                instance->runtimeTrack,
                instance->runtimeVersion,
                sibling_owner(read_instances(), owner)))
        {
            throw ptlsmr::win32_error("status ImagePath policy", ERROR_ACCESS_DENIED);
        }
        if (ptlsmr::sha256_file(
                ptlsmr::runtime_executable_path(
                    instance->runtimeTrack,
                    instance->runtimeVersion)) != instance->runtimeSha256)
        {
            throw ptlsmr::win32_error("status runtime artifact hash policy", ERROR_CRC);
        }
        const auto status = query_status(service.get());
        response.scmState = status.dwCurrentState;
        response.processId = status.dwProcessId;
        response.serviceExit = status.dwWin32ExitCode;
        copy_bounded(
            response.runtimeVersion,
            ARRAYSIZE(response.runtimeVersion),
            ptlsmr::format_version(instance->runtimeVersion));
        if (std::filesystem::exists(names.evidencePath))
        {
            copy_bounded(
                response.detail,
                ARRAYSIZE(response.detail),
                ptlsmr::read_utf8_file(names.evidencePath, 8192));
        }
    }

    void cleanup(const std::wstring& owner, std::wstring_view crashPhase)
    {
        const auto instance = find_instance(read_instances(), owner);
        if (!instance)
        {
            reconcile_protected_state();
            return;
        }
        cleanup_transaction state{
            owner,
            ptlsmr::instance_names(owner).serviceName,
            instance->runtimeTrack,
            instance->runtimeVersion,
            instance->runtimeSha256,
            instance->transactionId,
            L"prepared",
        };
        write_cleanup_journal(state);
        const auto names = ptlsmr::instance_names(owner);
        auto scm = open_scm();
        delete_runtime_service(scm, names, "DeleteService(runtime)");
        set_cleanup_phase(state, L"service-deleted");
        maybe_crash(crashPhase, L"after-cleanup-service-delete");
        maybe_fail(crashPhase, L"fail-after-cleanup-service-delete");
        (void)remove_instance(owner);
        set_cleanup_phase(state, L"inventory-removed");
        maybe_crash(crashPhase, L"after-cleanup-inventory");
        if (std::filesystem::exists(names.storeDirectory))
        {
            std::filesystem::remove_all(names.storeDirectory);
        }
        set_cleanup_phase(state, L"store-removed");
        set_cleanup_phase(state, L"sibling-sync-pending");
        synchronize_probe_targets();
        cleanup_unreferenced_runtimes();
        clear_cleanup_journal();
    }

    [[nodiscard]] bool is_request_admin(HANDLE pipe)
    {
        ptlsmr::check_bool(ImpersonateNamedPipeClient(pipe), "ImpersonateNamedPipeClient");
        struct revert_guard
        {
            ~revert_guard()
            {
                RevertToSelf();
            }
        } revert;
        HANDLE raw = nullptr;
        ptlsmr::check_bool(
            OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &raw),
            "OpenThreadToken(pipe caller)");
        ptlsmr::unique_handle token(raw);
        return ptlsmr::token_is_administrator(token.get());
    }

    void handle_request(
        const ptlsmr::request& input,
        ptlsmr::reply& output,
        bool reconcileBeforeRequest = true)
    {
        output.command = input.command;
        if (input.magic != ptlsmr::ProtocolMagic ||
            input.version != ptlsmr::ProtocolVersion ||
            input.reserved != 0 ||
            input.ownerSid[ARRAYSIZE(input.ownerSid) - 1] != L'\0' ||
            input.candidatePath[ARRAYSIZE(input.candidatePath) - 1] != L'\0' ||
            input.crashPhase[ARRAYSIZE(input.crashPhase) - 1] != L'\0' ||
            input.transactionId[ARRAYSIZE(input.transactionId) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("pipe request protocol", ERROR_INVALID_DATA);
        }
        if (reconcileBeforeRequest)
        {
            converge_pending_transactions();
        }
        const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
        switch (static_cast<ptlsmr::command>(input.command))
        {
        case ptlsmr::command::provision:
            if ((input.runtimeTrack != 1 && input.runtimeTrack != 2) ||
                input.candidatePath[0] == L'\0' ||
                (input.transactionId[0] != L'\0' &&
                 !is_transaction_id(input.transactionId)) ||
                (std::wstring_view(input.crashPhase) != L"" &&
                 std::wstring_view(input.crashPhase) != L"after-journal-prepared" &&
                 std::wstring_view(input.crashPhase) != L"after-target-directory-created" &&
                 std::wstring_view(input.crashPhase) != L"after-final-install" &&
                 std::wstring_view(input.crashPhase) != L"after-scm-repath" &&
                 std::wstring_view(input.crashPhase) != L"after-inventory-before-sync" &&
                 std::wstring_view(input.crashPhase) != L"after-unreferenced-runtime-delete"))
            {
                throw ptlsmr::win32_error("provision request policy", ERROR_INVALID_PARAMETER);
            }
            provision(
                owner,
                input.runtimeTrack,
                input.candidatePath,
                input.crashPhase,
                input.transactionId);
            fill_status(owner, output);
            break;
        case ptlsmr::command::status:
            if (input.runtimeTrack != 0 || input.candidatePath[0] != L'\0' ||
                input.transactionId[0] != L'\0' ||
                input.crashPhase[0] != L'\0')
            {
                throw ptlsmr::win32_error("status request policy", ERROR_INVALID_PARAMETER);
            }
            fill_status(owner, output);
            break;
        case ptlsmr::command::cleanup:
            if (input.runtimeTrack != 0 || input.candidatePath[0] != L'\0' ||
                input.transactionId[0] != L'\0' ||
                (std::wstring_view(input.crashPhase) != L"" &&
                 std::wstring_view(input.crashPhase) != L"after-cleanup-service-delete" &&
                std::wstring_view(input.crashPhase) != L"after-cleanup-inventory" &&
                std::wstring_view(input.crashPhase) != L"fail-after-cleanup-service-delete"))
            {
                throw ptlsmr::win32_error("cleanup request policy", ERROR_INVALID_PARAMETER);
            }
            cleanup(owner, input.crashPhase);
            break;
        default:
            throw ptlsmr::win32_error("pipe command policy", ERROR_INVALID_FUNCTION);
        }
    }

    void set_failure_service_status(const ptlsmr::request& input, ptlsmr::reply& output) noexcept
    {
        try
        {
            if (input.ownerSid[0] == L'\0')
            {
                return;
            }
            const auto names = ptlsmr::instance_names(input.ownerSid);
            auto scm = open_scm();
            service_handle service(OpenServiceW(scm.get(), names.serviceName.c_str(), SERVICE_QUERY_STATUS));
            if (!service)
            {
                return;
            }
            const auto status = query_status(service.get());
            output.scmState = status.dwCurrentState;
            output.processId = status.dwProcessId;
            output.serviceExit = status.dwWin32ExitCode;
        }
        catch (...)
        {
        }
    }

    void serve_client(HANDLE pipe)
    {
        ptlsmr::request input{};
        ptlsmr::reply output{};
        DWORD transferred = 0;
        if (perform_stop_aware_pipe_io(
                pipe,
                &input,
                sizeof(input),
                transferred,
                false) != pipe_io_result::completed ||
            transferred != sizeof(input))
        {
            return;
        }
        try
        {
            if (!is_request_admin(pipe))
            {
                throw ptlsmr::win32_error("pipe caller admin policy", ERROR_ACCESS_DENIED);
            }
            handle_request(input, output);
        }
        catch (const ptlsmr::win32_error& error)
        {
            output.win32Status = error.code();
            const std::string_view message(error.what());
            copy_bounded(
                output.detail,
                ARRAYSIZE(output.detail),
                std::wstring(message.begin(), message.end()));
            set_failure_service_status(input, output);
        }
        catch (...)
        {
            output.win32Status = ERROR_UNHANDLED_EXCEPTION;
            set_failure_service_status(input, output);
        }
        (void)perform_stop_aware_pipe_io(
            pipe,
            &output,
            sizeof(output),
            transferred,
            true);
    }

    void pipe_server()
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                L"D:P(A;;GA;;;SY)(A;;GA;;;BA)",
                SDDL_REVISION_1,
                &descriptor,
                nullptr))
        {
            throw ptlsmr::win32_error("ConvertStringSecurityDescriptorToSecurityDescriptorW(pipe)", GetLastError());
        }
        ptlsmr::local_memory security(descriptor);
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        while (WaitForSingleObject(g_stopEvent.get(), 0) != WAIT_OBJECT_0)
        {
            ptlsmr::unique_handle pipe(CreateNamedPipeW(
                ptlsmr::UpdaterPipeName,
                PIPE_ACCESS_DUPLEX | FILE_FLAG_FIRST_PIPE_INSTANCE | FILE_FLAG_OVERLAPPED,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT | PIPE_REJECT_REMOTE_CLIENTS,
                1,
                sizeof(ptlsmr::reply),
                sizeof(ptlsmr::request),
                0,
                &attributes));
            if (!pipe)
            {
                const DWORD error = GetLastError();
                if (error == ERROR_PIPE_BUSY)
                {
                    Sleep(100);
                    continue;
                }
                throw ptlsmr::win32_error("CreateNamedPipeW", error);
            }
            const auto connection = connect_stop_aware_pipe(pipe.get());
            if (connection == pipe_io_result::stopped)
            {
                return;
            }
            if (connection == pipe_io_result::disconnected)
            {
                continue;
            }
            serve_client(pipe.get());
            DisconnectNamedPipe(pipe.get());
        }
    }

    void write_updater_evidence()
    {
        const auto executable = module_path();
        const auto expected = ptlsmr::updater_install_directory(
            ptlsmr::parse_version(ptlsmr::UpdaterVersion)) / ptlsmr::UpdaterExe;
        if (!equal_path(executable, expected) ||
            ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("updater protected execution policy", ERROR_ACCESS_DENIED);
        }
        const auto signerPin = ptlsmr::read_trusted_signer_pin();
        (void)ptlsmr::validate_updater_candidate(executable, signerPin);
        const auto packageFullNameResult = ptlsmr::require_no_package_identity();
        std::wstringstream evidence;
        evidence << L"serviceName=" << ptlsmr::UpdaterServiceName << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"tokenUserSid=" << ptlsmr::current_token_user_sid() << L"\r\n";
        evidence << L"packageFullNameResult=" << packageFullNameResult << L"\r\n";
        evidence << L"packageIdentityPresent=false\r\n";
        evidence << L"trustedSignerSha256=" << signerPin << L"\r\n";
        evidence << L"updaterVersion=" << ptlsmr::UpdaterVersion << L"\r\n";
        evidence << L"executablePath=" << executable.wstring() << L"\r\n";
        evidence << L"bootstrapTrustAssumption=trusted-installer-simulation\r\n";
        evidence << L"pipePolicy=administrators-only\r\n";
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::program_data_root() / L"updater-evidence.txt",
            evidence.str());
    }

    DWORD WINAPI service_control_handler(DWORD control, DWORD, void*, void*)
    {
        if (control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN)
        {
            report_status(SERVICE_STOP_PENDING);
            if (g_stopEvent)
            {
                SetEvent(g_stopEvent.get());
            }
        }
        return ERROR_SUCCESS;
    }

    void WINAPI service_main(DWORD, LPWSTR*)
    {
        g_statusHandle = RegisterServiceCtrlHandlerExW(
            ptlsmr::UpdaterServiceName,
            service_control_handler,
            nullptr);
        if (!g_statusHandle)
        {
            return;
        }
        report_status(SERVICE_START_PENDING);
        try
        {
            if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
            {
                throw ptlsmr::win32_error("updater LocalSystem token policy", ERROR_ACCESS_DENIED);
            }
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            ptlsmr::protect_runtime_directory(ptlsmr::installation_root());
            const auto executable = module_path();
            const auto expected = ptlsmr::updater_install_directory(
                ptlsmr::parse_version(ptlsmr::UpdaterVersion)) / ptlsmr::UpdaterExe;
            if (!equal_path(executable, expected))
            {
                throw ptlsmr::win32_error("updater protected execution path policy", ERROR_ACCESS_DENIED);
            }
            (void)ptlsmr::validate_updater_candidate(
                executable,
                ptlsmr::read_trusted_signer_pin());
            (void)ptlsmr::require_no_package_identity();
            g_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!g_stopEvent)
            {
                throw ptlsmr::win32_error("CreateEventW(updater stop)", GetLastError());
            }
            converge_pending_transactions();
            write_updater_evidence();
            std::thread server(pipe_server);
            report_status(SERVICE_RUNNING);
            WaitForSingleObject(g_stopEvent.get(), INFINITE);
            report_status(SERVICE_STOP_PENDING);
            server.join();
            report_status(SERVICE_STOPPED);
        }
        catch (const ptlsmr::win32_error& error)
        {
            report_status(SERVICE_STOPPED, error.code());
        }
        catch (...)
        {
            report_status(SERVICE_STOPPED, ERROR_UNHANDLED_EXCEPTION);
        }
    }

    struct release_manifest
    {
        std::wstring releaseId;
        uint64_t securityEpoch{};
        ptlsmr::file_version minimumHostVersion{};
        uint16_t runtimeTrack{};
        ptlsmr::file_version runtimeVersion{};
        std::wstring runtimeFile;
        uint64_t runtimeLength{};
        std::wstring runtimeSha256;
        std::optional<ptlsmr::file_version> engineVersion;
        std::wstring engineFile;
        std::optional<uint64_t> engineLength;
        std::wstring engineSha256;
        std::wstring engineCrashPhase;
        std::wstring runtimeCrashPhase;
    };

    struct lease
    {
        std::wstring ownerSid;
    };

    [[nodiscard]] std::filesystem::path runtime_floor_path(uint16_t track)
    {
        if (track != 1 && track != 2)
        {
            throw ptlsmr::win32_error("runtime floor track policy", ERROR_INVALID_PARAMETER);
        }
        return ptlsmr::program_data_root() /
            (L"runtime-version-floor-track" + std::to_wstring(track) + L".txt");
    }

    [[nodiscard]] bool is_hex_sha256(std::wstring_view value)
    {
        if (value.size() != 64)
        {
            return false;
        }
        return std::all_of(value.begin(), value.end(), [](wchar_t character) {
            return (character >= L'0' && character <= L'9') ||
                (character >= L'a' && character <= L'f') ||
                (character >= L'A' && character <= L'F');
        });
    }

    [[nodiscard]] bool is_safe_basename(std::wstring_view value)
    {
        if (value.empty() || value.size() > 128 || value == L"." || value == L".." ||
            value.find_first_of(L"\\/:") != std::wstring_view::npos ||
            value.back() == L'.' || value.back() == L' ')
        {
            return false;
        }
        return std::all_of(value.begin(), value.end(), [](wchar_t character) {
            return character >= 0x20 && character != L'|';
        });
    }

    [[nodiscard]] bool is_valid_release_id(std::wstring_view value)
    {
        if (value.size() < 11 || value.size() >= ptlsmr::MaxReleaseIdChars ||
            !value.starts_with(L"release-") || value.back() == L'-')
        {
            return false;
        }
        bool digitSeen = false;
        for (size_t index = 8; index < value.size(); ++index)
        {
            const wchar_t character = value[index];
            if (character >= L'0' && character <= L'9')
            {
                digitSeen = true;
            }
            else if (!((character >= L'a' && character <= L'z') || character == L'-'))
            {
                return false;
            }
        }
        return digitSeen;
    }

    [[nodiscard]] bool is_runtime_crash_phase(std::wstring_view value)
    {
        return value == L"after-journal-prepared" ||
            value == L"after-target-directory-created" ||
            value == L"after-final-install" ||
            value == L"after-scm-repath" ||
            value == L"after-inventory-before-sync" ||
            value == L"after-unreferenced-runtime-delete";
    }

    [[nodiscard]] std::map<std::wstring, std::wstring, std::less<>> parse_manifest_fields(
        std::wstring_view input)
    {
        constexpr std::array<std::wstring_view, 15> allowed{
            L"schemaVersion",
            L"releaseId",
            L"securityEpoch",
            L"minimumHostVersion",
            L"runtimeTrack",
            L"runtimeVersion",
            L"runtimeFile",
            L"runtimeLength",
            L"runtimeSha256",
            L"engineVersion",
            L"engineFile",
            L"engineLength",
            L"engineSha256",
            L"testEngineCrashPhase",
            L"testRuntimeCrashPhase",
        };
        std::map<std::wstring, std::wstring, std::less<>> fields;
        size_t start = 0;
        while (start < input.size())
        {
            const size_t end = input.find_first_of(L"\r\n", start);
            const auto line = input.substr(
                start,
                (end == std::wstring_view::npos ? input.size() : end) - start);
            if (!line.empty())
            {
                const size_t separator = line.find(L'=');
                if (separator == 0 || separator == std::wstring_view::npos ||
                    separator + 1 == line.size() ||
                    line.find(L'=', separator + 1) != std::wstring_view::npos)
                {
                    throw ptlsmr::win32_error("release manifest field syntax", ERROR_INVALID_DATA);
                }
                const std::wstring name(line.substr(0, separator));
                const bool known = std::find(allowed.begin(), allowed.end(), name) != allowed.end();
                if (!known || !fields.emplace(name, std::wstring(line.substr(separator + 1))).second)
                {
                    throw ptlsmr::win32_error("release manifest unknown or duplicate field", ERROR_INVALID_DATA);
                }
            }
            if (end == std::wstring_view::npos)
            {
                break;
            }
            start = end + 1;
            if (input[end] == L'\r' && start < input.size() && input[start] == L'\n')
            {
                ++start;
            }
        }
        constexpr std::array<std::wstring_view, 13> required{
            L"schemaVersion",
            L"releaseId",
            L"securityEpoch",
            L"minimumHostVersion",
            L"runtimeTrack",
            L"runtimeVersion",
            L"runtimeFile",
            L"runtimeLength",
            L"runtimeSha256",
            L"engineVersion",
            L"engineFile",
            L"engineLength",
            L"engineSha256",
        };
        for (const auto name : required)
        {
            if (!fields.contains(name))
            {
                throw ptlsmr::win32_error("release manifest required field", ERROR_INVALID_DATA);
            }
        }
        return fields;
    }

    [[nodiscard]] uint64_t parse_epoch(std::wstring_view value)
    {
        if (value.empty() || value.size() > 19 ||
            !std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return character >= L'0' && character <= L'9';
            }))
        {
            throw ptlsmr::win32_error("security epoch format", ERROR_INVALID_DATA);
        }
        try
        {
            const auto result = std::stoull(std::wstring(value));
            if (result == 0)
            {
                throw ptlsmr::win32_error("security epoch range", ERROR_INVALID_DATA);
            }
            return result;
        }
        catch (const std::invalid_argument&)
        {
            throw ptlsmr::win32_error("security epoch format", ERROR_INVALID_DATA);
        }
        catch (const std::out_of_range&)
        {
            throw ptlsmr::win32_error("security epoch range", ERROR_INVALID_DATA);
        }
    }

    [[nodiscard]] uint64_t parse_artifact_length(
        std::wstring_view value,
        uint64_t maximum,
        const char* operation)
    {
        if (value.empty() || value.size() > 20 ||
            !std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return character >= L'0' && character <= L'9';
            }))
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
        }
        try
        {
            const auto result = std::stoull(std::wstring(value));
            if (result == 0)
            {
                throw ptlsmr::win32_error(operation, ERROR_HANDLE_EOF);
            }
            if (result > maximum)
            {
                throw ptlsmr::win32_error(operation, ERROR_FILE_TOO_LARGE);
            }
            return result;
        }
        catch (const std::invalid_argument&)
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
        }
        catch (const std::out_of_range&)
        {
            throw ptlsmr::win32_error(operation, ERROR_FILE_TOO_LARGE);
        }
    }

    [[nodiscard]] release_manifest parse_release_manifest(
        const std::filesystem::path& stagedManifest,
        std::wstring_view expectedReleaseId)
    {
        const std::wstring metadataPin = ptlsmr::read_metadata_signer_pin();
        (void)ptlsmr::validate_release_manifest_candidate(stagedManifest, metadataPin);
        const auto fields = parse_manifest_fields(
            ptlsmr::read_rcdata_text(stagedManifest, L"PTPUVR_MANIFEST", 8192));
        if (fields.at(L"schemaVersion") != L"2" ||
            fields.at(L"releaseId") != expectedReleaseId ||
            !is_valid_release_id(fields.at(L"releaseId")))
        {
            throw ptlsmr::win32_error("release manifest schema or release identity", ERROR_INVALID_DATA);
        }
        release_manifest manifest;
        manifest.releaseId = fields.at(L"releaseId");
        manifest.securityEpoch = parse_epoch(fields.at(L"securityEpoch"));
        manifest.minimumHostVersion = ptlsmr::parse_version(fields.at(L"minimumHostVersion"));
        if (ptlsmr::parse_version(ptlsmr::HostVersion) < manifest.minimumHostVersion)
        {
            throw ptlsmr::win32_error("release manifest host version floor", ERROR_OLD_WIN_VERSION);
        }
        if (fields.at(L"runtimeTrack") != L"1" && fields.at(L"runtimeTrack") != L"2")
        {
            throw ptlsmr::win32_error("release manifest runtime track", ERROR_INVALID_DATA);
        }
        manifest.runtimeTrack = static_cast<uint16_t>(fields.at(L"runtimeTrack")[0] - L'0');
        manifest.runtimeVersion = ptlsmr::parse_version(fields.at(L"runtimeVersion"));
        if (manifest.runtimeVersion.major != manifest.runtimeTrack ||
            !is_safe_basename(fields.at(L"runtimeFile")) ||
            !is_hex_sha256(fields.at(L"runtimeSha256")))
        {
            throw ptlsmr::win32_error("release manifest runtime artifact", ERROR_INVALID_DATA);
        }
        manifest.runtimeFile = fields.at(L"runtimeFile");
        manifest.runtimeLength = parse_artifact_length(
            fields.at(L"runtimeLength"),
            ptlsmr::MaxRuntimeArtifactBytes,
            "release manifest runtime length");
        manifest.runtimeSha256 = ptlsmr::canonical_signer_sha256(fields.at(L"runtimeSha256"));

        const auto& engineVersion = fields.at(L"engineVersion");
        const auto& engineFile = fields.at(L"engineFile");
        const auto& engineLength = fields.at(L"engineLength");
        const auto& engineSha256 = fields.at(L"engineSha256");
        if (engineVersion == L"none")
        {
            if (engineFile != L"none" || engineLength != L"none" ||
                engineSha256 != L"none")
            {
                throw ptlsmr::win32_error("release manifest no-engine shape", ERROR_INVALID_DATA);
            }
        }
        else
        {
            manifest.engineVersion = ptlsmr::parse_version(engineVersion);
            if (manifest.engineVersion->major != 5 ||
                !is_safe_basename(engineFile) ||
                !is_hex_sha256(engineSha256))
            {
                throw ptlsmr::win32_error("release manifest engine artifact", ERROR_INVALID_DATA);
            }
            manifest.engineFile = engineFile;
            manifest.engineLength = parse_artifact_length(
                engineLength,
                ptlsmr::MaxEngineArtifactBytes,
                "release manifest engine length");
            manifest.engineSha256 = ptlsmr::canonical_signer_sha256(engineSha256);
        }
        manifest.engineCrashPhase = fields.contains(L"testEngineCrashPhase")
            ? fields.at(L"testEngineCrashPhase")
            : L"none";
        manifest.runtimeCrashPhase = fields.contains(L"testRuntimeCrashPhase")
            ? fields.at(L"testRuntimeCrashPhase")
            : L"none";
        if (manifest.engineCrashPhase != L"none" &&
            manifest.engineCrashPhase != L"before-active-switch" &&
            manifest.engineCrashPhase != L"after-active-switch-before-journal-clear")
        {
            throw ptlsmr::win32_error("release manifest engine crash phase", ERROR_INVALID_DATA);
        }
        if (manifest.runtimeCrashPhase != L"none" &&
            !is_runtime_crash_phase(manifest.runtimeCrashPhase))
        {
            throw ptlsmr::win32_error("release manifest runtime crash phase", ERROR_INVALID_DATA);
        }
        return manifest;
    }

    [[nodiscard]] std::filesystem::path stage_inbox_file(
        const std::filesystem::path& inbox,
        std::wstring_view fileName,
        const std::filesystem::path& stage,
        uint64_t maximumBytes,
        std::optional<uint64_t> expectedBytes = std::nullopt)
    {
        if (!is_safe_basename(fileName))
        {
            throw ptlsmr::win32_error("release inbox basename policy", ERROR_INVALID_NAME);
        }
        const auto source = inbox / std::wstring(fileName);
        const auto target = stage / std::wstring(fileName);
        (void)ptlsmr::copy_file_to_protected_stage(
            source,
            inbox,
            target,
            maximumBytes,
            expectedBytes);
        return target;
    }

    [[nodiscard]] ptlsmr::file_version read_runtime_floor(uint16_t track)
    {
        return ptlsmr::parse_version(ptlsmr::read_utf8_file(runtime_floor_path(track), 64));
    }

    void advance_runtime_floor(uint16_t track, const ptlsmr::file_version& version)
    {
        const auto previous = read_runtime_floor(track);
        if (version < previous)
        {
            throw ptlsmr::win32_error("runtime version floor regression", ERROR_REVISION_MISMATCH);
        }
        ptlsmr::write_utf8_file_atomic(runtime_floor_path(track), ptlsmr::format_version(version));
    }

    struct accepted_release
    {
        std::wstring releaseId;
        uint64_t epoch{};
        std::wstring manifestHash;
    };

    struct accepted_security_state
    {
        uint64_t epoch{};
        std::vector<accepted_release> releases;
    };

    [[nodiscard]] std::wstring serialize_accepted_security_state(
        accepted_security_state state)
    {
        if (state.epoch == 0 || state.releases.size() > MaxAcceptedReleases)
        {
            throw ptlsmr::win32_error(
                "accepted release state write bounds",
                ERROR_INVALID_DATA);
        }
        std::sort(
            state.releases.begin(),
            state.releases.end(),
            [](const accepted_release& left, const accepted_release& right) {
                return left.releaseId < right.releaseId;
            });
        std::wstringstream text;
        text << L"schema=1\r\n";
        text << L"epoch=" << state.epoch << L"\r\n";
        std::wstring previous;
        for (const auto& item : state.releases)
        {
            if (!is_valid_release_id(item.releaseId) ||
                item.epoch == 0 ||
                item.epoch > state.epoch ||
                !is_hex_sha256(item.manifestHash) ||
                (!previous.empty() && previous == item.releaseId))
            {
                throw ptlsmr::win32_error(
                    "accepted release state write policy",
                    ERROR_INVALID_DATA);
            }
            text << L"release=" << item.releaseId << L"|" << item.epoch << L"|" <<
                ptlsmr::canonical_signer_sha256(item.manifestHash) << L"\r\n";
            previous = item.releaseId;
        }
        const auto serialized = text.str();
        if (serialized.size() > 32 * 1024)
        {
            throw ptlsmr::win32_error(
                "accepted release state serialization bound",
                ERROR_FILE_TOO_LARGE);
        }
        return serialized;
    }

    [[nodiscard]] accepted_security_state read_accepted_security_state()
    {
        const auto path = ptlsmr::accepted_release_state_path();
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error(
                "required accepted release state",
                ERROR_FILE_NOT_FOUND);
        }
        const auto text = ptlsmr::read_utf8_file(path, 32 * 1024);
        accepted_security_state output;
        size_t record = 0;
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const auto line = std::wstring_view(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            if (line.empty())
            {
                throw ptlsmr::win32_error(
                    "accepted release state empty record",
                    ERROR_INVALID_DATA);
            }
            if (record == 0)
            {
                if (line != L"schema=1")
                {
                    throw ptlsmr::win32_error(
                        "accepted release state schema",
                        ERROR_INVALID_DATA);
                }
            }
            else if (record == 1)
            {
                if (!line.starts_with(L"epoch="))
                {
                    throw ptlsmr::win32_error(
                        "accepted release state epoch record",
                        ERROR_INVALID_DATA);
                }
                output.epoch = parse_epoch(line.substr(6));
            }
            else
            {
                if (!line.starts_with(L"release="))
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record type",
                        ERROR_INVALID_DATA);
                }
                const auto values = split(line.substr(8), L'|');
                if (values.size() != 3 ||
                    !is_valid_release_id(values[0]) ||
                    !is_hex_sha256(values[2]))
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record format",
                        ERROR_INVALID_DATA);
                }
                const auto epoch = parse_epoch(values[1]);
                if (epoch > output.epoch ||
                    std::any_of(
                        output.releases.begin(),
                        output.releases.end(),
                        [&](const accepted_release& item) {
                            return item.releaseId == values[0];
                        }))
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record identity",
                        ERROR_INVALID_DATA);
                }
                output.releases.push_back({
                    std::wstring(values[0]),
                    epoch,
                    ptlsmr::canonical_signer_sha256(values[2]),
                });
                if (output.releases.size() > MaxAcceptedReleases)
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record count",
                        ERROR_TOO_MANY_NAMES);
                }
            }
            ++record;
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (text[end] == L'\r' && start < text.size() && text[start] == L'\n')
            {
                ++start;
            }
        }
        if (record < 2)
        {
            throw ptlsmr::win32_error(
                "accepted release state required records",
                ERROR_INVALID_DATA);
        }
        if (serialize_accepted_security_state(output) != text)
        {
            throw ptlsmr::win32_error(
                "accepted release state canonical serialization",
                ERROR_INVALID_DATA);
        }
        return output;
    }

    [[nodiscard]] std::wstring accepted_security_state_hash(
        const accepted_security_state& state)
    {
        return ptlsmr::canonical_signer_sha256(
            ptlsmr::sha256_text(serialize_accepted_security_state(state)));
    }

    [[nodiscard]] accepted_security_state advance_accepted_security_state(
        accepted_security_state state,
        std::wstring_view releaseId,
        uint64_t securityEpoch,
        std::wstring_view manifestHash)
    {
        if (!is_valid_release_id(releaseId) || !is_hex_sha256(manifestHash))
        {
            throw ptlsmr::win32_error(
                "accepted release state input policy",
                ERROR_INVALID_DATA);
        }
        const auto found = std::find_if(
            state.releases.begin(),
            state.releases.end(),
            [&](const accepted_release& item) { return item.releaseId == releaseId; });
        if (found != state.releases.end() &&
            (found->epoch != securityEpoch || found->manifestHash != manifestHash))
        {
            throw ptlsmr::win32_error(
                "accepted release metadata collision",
                ERROR_REVISION_MISMATCH);
        }
        if (securityEpoch < state.epoch ||
            (securityEpoch == state.epoch && found == state.releases.end()))
        {
            throw ptlsmr::win32_error(
                "accepted security epoch replay policy",
                ERROR_REVISION_MISMATCH);
        }
        if (securityEpoch > state.epoch)
        {
            if (state.releases.size() >= MaxAcceptedReleases)
            {
                throw ptlsmr::win32_error(
                    "accepted release state append limit",
                    ERROR_TOO_MANY_NAMES);
            }
            state.epoch = securityEpoch;
            state.releases.push_back({
                std::wstring(releaseId),
                securityEpoch,
                std::wstring(manifestHash),
            });
        }
        (void)serialize_accepted_security_state(state);
        return state;
    }

    [[nodiscard]] accepted_security_state advance_accepted_security_state(
        accepted_security_state state,
        const release_manifest& manifest,
        std::wstring_view manifestHash)
    {
        return advance_accepted_security_state(
            std::move(state),
            manifest.releaseId,
            manifest.securityEpoch,
            manifestHash);
    }

    void write_accepted_security_state(const accepted_security_state& state)
    {
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::accepted_release_state_path(),
            serialize_accepted_security_state(state));
    }

    [[nodiscard]] std::filesystem::path runtime_crash_consumption_path()
    {
        return ptlsmr::program_data_root() / L"runtime-crash-injections.txt";
    }

    [[nodiscard]] std::wstring consume_runtime_crash_phase(
        std::wstring_view releaseId,
        std::wstring_view crashPhase)
    {
        if (crashPhase == L"none")
        {
            return {};
        }
        if (!is_valid_release_id(releaseId) || !is_runtime_crash_phase(crashPhase))
        {
            throw ptlsmr::win32_error(
                "runtime crash injection state request",
                ERROR_INVALID_DATA);
        }
        std::vector<std::pair<std::wstring, std::wstring>> consumed;
        const auto path = runtime_crash_consumption_path();
        if (std::filesystem::exists(path))
        {
            const auto text = ptlsmr::read_utf8_file(path, 16 * 1024);
            size_t start = 0;
            while (start < text.size())
            {
                const size_t end = text.find_first_of(L"\r\n", start);
                const std::wstring_view line(
                    text.data() + start,
                    (end == std::wstring::npos ? text.size() : end) - start);
                if (!line.empty())
                {
                    const auto fields = split(line, L'|');
                    if (fields.size() != 2 || !is_valid_release_id(fields[0]) ||
                        !is_runtime_crash_phase(fields[1]) ||
                        std::any_of(consumed.begin(), consumed.end(), [&](const auto& item) {
                            return item.first == fields[0];
                        }))
                    {
                        throw ptlsmr::win32_error(
                            "runtime crash injection state format",
                            ERROR_INVALID_DATA);
                    }
                    consumed.emplace_back(std::wstring(fields[0]), std::wstring(fields[1]));
                }
                if (end == std::wstring::npos)
                {
                    break;
                }
                start = end + 1;
                if (text[end] == L'\r' && start < text.size() && text[start] == L'\n')
                {
                    ++start;
                }
            }
        }
        const auto existing = std::find_if(
            consumed.begin(),
            consumed.end(),
            [&](const auto& item) { return item.first == releaseId; });
        if (existing != consumed.end())
        {
            if (existing->second != crashPhase)
            {
                throw ptlsmr::win32_error(
                    "runtime crash injection state collision",
                    ERROR_REVISION_MISMATCH);
            }
            return {};
        }
        consumed.emplace_back(std::wstring(releaseId), std::wstring(crashPhase));
        std::sort(consumed.begin(), consumed.end());
        std::wstringstream text;
        for (const auto& item : consumed)
        {
            text << item.first << L"|" << item.second << L"\r\n";
        }
        ptlsmr::write_utf8_file_atomic(path, text.str());
        return std::wstring(crashPhase);
    }

    [[nodiscard]] std::vector<lease> read_leases()
    {
        const auto path = ptlsmr::lease_state_path();
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error("required lease state", ERROR_FILE_NOT_FOUND);
        }
        const auto text = ptlsmr::read_utf8_file(path, 16 * 1024);
        std::vector<lease> leases;
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const auto line = std::wstring_view(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            if (line.empty() || line.find(L'|') != std::wstring_view::npos)
            {
                throw ptlsmr::win32_error("lease state SID-only format", ERROR_INVALID_DATA);
            }
            const auto owner = ptlsmr::canonical_owner_sid(line);
            if (std::any_of(leases.begin(), leases.end(), [&](const lease& value) {
                    return value.ownerSid == owner;
                }))
            {
                throw ptlsmr::win32_error("lease state duplicate owner", ERROR_INVALID_DATA);
            }
            leases.push_back({ owner });
            if (leases.size() > ptlsmr::MaxLeases)
            {
                throw ptlsmr::win32_error("lease state count limit", ERROR_TOO_MANY_NAMES);
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (text[end] == L'\r' && start < text.size() && text[start] == L'\n')
            {
                ++start;
            }
        }
        return leases;
    }

    void write_leases(std::vector<lease> leases)
    {
        if (leases.size() > ptlsmr::MaxLeases)
        {
            throw ptlsmr::win32_error("lease state write count limit", ERROR_TOO_MANY_NAMES);
        }
        std::sort(
            leases.begin(),
            leases.end(),
            [](const lease& left, const lease& right) {
                return left.ownerSid < right.ownerSid;
            });
        std::wstringstream text;
        std::wstring previous;
        for (const auto& item : leases)
        {
            if (ptlsmr::canonical_owner_sid(item.ownerSid) != item.ownerSid ||
                (!previous.empty() && previous == item.ownerSid))
            {
                throw ptlsmr::win32_error("lease state write policy", ERROR_INVALID_DATA);
            }
            text << item.ownerSid << L"\r\n";
            previous = item.ownerSid;
        }
        const auto serialized = text.str();
        if (serialized.size() > 16 * 1024)
        {
            throw ptlsmr::win32_error(
                "lease state serialization bound",
                ERROR_FILE_TOO_LARGE);
        }
        ptlsmr::write_utf8_file_atomic(ptlsmr::lease_state_path(), serialized);
    }

    [[nodiscard]] bool has_lease(
        const std::vector<lease>& leases,
        std::wstring_view owner)
    {
        return std::any_of(
            leases.begin(),
            leases.end(),
            [&](const lease& value) { return value.ownerSid == owner; });
    }

    void ensure_lease(const std::wstring& owner)
    {
        auto leases = read_leases();
        if (has_lease(leases, owner))
        {
            return;
        }
        if (leases.size() >= ptlsmr::MaxLeases)
        {
            throw ptlsmr::win32_error("lease acquisition limit", ERROR_TOO_MANY_NAMES);
        }
        leases.push_back({ owner });
        write_leases(std::move(leases));
    }

    [[nodiscard]] bool valid_acquisition_phase(std::wstring_view phase)
    {
        static constexpr std::array<std::wstring_view, 5> phases{
            L"prepared",
            L"runtime-provisioning",
            L"runtime-committed",
            L"floor-committed",
            L"security-committed",
        };
        return std::find(phases.begin(), phases.end(), phase) != phases.end();
    }

    void validate_acquisition_transaction(const acquisition_transaction& value)
    {
        if (ptlsmr::canonical_owner_sid(value.ownerSid) != value.ownerSid ||
            !is_valid_release_id(value.releaseId) ||
            !is_hex_sha256(value.manifestHash) ||
            (value.runtimeTrack != 1 && value.runtimeTrack != 2) ||
            value.runtimeVersion.major != value.runtimeTrack ||
            !is_hex_sha256(value.targetRuntimeSha256) ||
            !is_transaction_id(value.targetTransactionId) ||
            (value.previousRuntimePresent &&
             ((value.previousRuntimeTrack != 1 && value.previousRuntimeTrack != 2) ||
              value.previousRuntimeVersion.major != value.previousRuntimeTrack ||
              !is_hex_sha256(value.previousRuntimeSha256) ||
              !is_transaction_id(value.previousTransactionId))) ||
            (!value.previousRuntimePresent &&
             (value.previousRuntimeTrack != 0 ||
              value.previousRuntimeVersion.major != 0 ||
              value.previousRuntimeVersion.minor != 0 ||
              value.previousRuntimeVersion.build != 0 ||
              value.previousRuntimeVersion.revision != 0 ||
              !value.previousRuntimeSha256.empty() ||
              !value.previousTransactionId.empty())) ||
            value.beforeRuntimeFloor.major != value.runtimeTrack ||
            value.targetRuntimeFloor.major != value.runtimeTrack ||
            value.targetRuntimeFloor < value.beforeRuntimeFloor ||
            !(value.targetRuntimeFloor == value.runtimeVersion) ||
            value.beforeSecurityEpoch == 0 ||
            value.targetSecurityEpoch < value.beforeSecurityEpoch ||
            !is_hex_sha256(value.beforeSecurityStateHash) ||
            !is_hex_sha256(value.targetSecurityStateHash) ||
            !valid_acquisition_phase(value.phase))
        {
            throw ptlsmr::win32_error(
                "acquisition journal policy",
                ERROR_INVALID_DATA);
        }
    }

    void write_acquisition_journal(const acquisition_transaction& value)
    {
        validate_acquisition_transaction(value);
        std::wstringstream text;
        text << L"schema=2\r\n";
        text << L"owner=" << value.ownerSid << L"\r\n";
        text << L"releaseId=" << value.releaseId << L"\r\n";
        text << L"manifestHash=" << value.manifestHash << L"\r\n";
        text << L"track=" << value.runtimeTrack << L"\r\n";
        text << L"runtimeVersion=" << ptlsmr::format_version(value.runtimeVersion) << L"\r\n";
        text << L"targetRuntimeSha256=" << value.targetRuntimeSha256 << L"\r\n";
        text << L"targetTransactionId=" << value.targetTransactionId << L"\r\n";
        text << L"previousRuntimePresent=" <<
            (value.previousRuntimePresent ? L"1" : L"0") << L"\r\n";
        text << L"previousRuntimeTrack=" <<
            (value.previousRuntimePresent ? std::to_wstring(value.previousRuntimeTrack) : L"0") <<
            L"\r\n";
        text << L"previousRuntimeVersion=" <<
            (value.previousRuntimePresent
                ? ptlsmr::format_version(value.previousRuntimeVersion)
                : L"none") << L"\r\n";
        text << L"previousRuntimeSha256=" <<
            (value.previousRuntimePresent ? value.previousRuntimeSha256 : L"none") << L"\r\n";
        text << L"previousTransactionId=" <<
            (value.previousRuntimePresent ? value.previousTransactionId : L"none") << L"\r\n";
        text << L"beforeRuntimeFloor=" <<
            ptlsmr::format_version(value.beforeRuntimeFloor) << L"\r\n";
        text << L"targetRuntimeFloor=" <<
            ptlsmr::format_version(value.targetRuntimeFloor) << L"\r\n";
        text << L"beforeSecurityEpoch=" << value.beforeSecurityEpoch << L"\r\n";
        text << L"targetSecurityEpoch=" << value.targetSecurityEpoch << L"\r\n";
        text << L"beforeSecurityStateHash=" << value.beforeSecurityStateHash << L"\r\n";
        text << L"targetSecurityStateHash=" << value.targetSecurityStateHash << L"\r\n";
        text << L"phase=" << value.phase << L"\r\n";
        const auto serialized = text.str();
        if (serialized.size() > 8192)
        {
            throw ptlsmr::win32_error(
                "acquisition journal serialization bound",
                ERROR_FILE_TOO_LARGE);
        }
        ptlsmr::write_utf8_file_atomic(ptlsmr::acquisition_journal_path(), serialized);
    }

    void set_acquisition_phase(
        acquisition_transaction& value,
        std::wstring_view phase)
    {
        value.phase = phase;
        write_acquisition_journal(value);
    }

    [[nodiscard]] acquisition_transaction read_acquisition_journal(
        const std::filesystem::path& path = ptlsmr::acquisition_journal_path())
    {
        const auto text = ptlsmr::read_utf8_file(
            path,
            8192);
        std::map<std::wstring, std::wstring, std::less<>> fields;
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            const size_t separator = line.find(L'=');
            if (line.empty() ||
                separator == 0 ||
                separator == std::wstring_view::npos ||
                line.find(L'=', separator + 1) != std::wstring_view::npos ||
                !fields.emplace(
                    std::wstring(line.substr(0, separator)),
                    std::wstring(line.substr(separator + 1))).second)
            {
                throw ptlsmr::win32_error(
                    "acquisition journal format",
                    ERROR_INVALID_DATA);
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
            if (text[end] == L'\r' && start < text.size() && text[start] == L'\n')
            {
                ++start;
            }
        }
        const auto field = [&](std::wstring_view name) -> const std::wstring& {
            const auto found = fields.find(name);
            if (found == fields.end())
            {
                throw ptlsmr::win32_error(
                    "acquisition journal missing field",
                    ERROR_INVALID_DATA);
            }
            return found->second;
        };
        if (fields.size() != 20 ||
            field(L"schema") != L"2" ||
            (field(L"track") != L"1" && field(L"track") != L"2") ||
            (field(L"previousRuntimePresent") != L"0" &&
             field(L"previousRuntimePresent") != L"1"))
        {
            throw ptlsmr::win32_error(
                "acquisition journal field policy",
                ERROR_INVALID_DATA);
        }
        acquisition_transaction output{};
        output.ownerSid = ptlsmr::canonical_owner_sid(field(L"owner"));
        output.releaseId = field(L"releaseId");
        output.manifestHash =
            ptlsmr::canonical_signer_sha256(field(L"manifestHash"));
        output.runtimeTrack = static_cast<uint16_t>(field(L"track")[0] - L'0');
        output.runtimeVersion = ptlsmr::parse_version(field(L"runtimeVersion"));
        output.targetRuntimeSha256 =
            ptlsmr::canonical_signer_sha256(field(L"targetRuntimeSha256"));
        output.targetTransactionId = field(L"targetTransactionId");
        output.previousRuntimePresent = field(L"previousRuntimePresent") == L"1";
        if (output.previousRuntimePresent)
        {
            if (field(L"previousRuntimeTrack") != L"1" &&
                field(L"previousRuntimeTrack") != L"2")
            {
                throw ptlsmr::win32_error(
                    "acquisition journal previous track",
                    ERROR_INVALID_DATA);
            }
            output.previousRuntimeTrack =
                static_cast<uint16_t>(field(L"previousRuntimeTrack")[0] - L'0');
            output.previousRuntimeVersion =
                ptlsmr::parse_version(field(L"previousRuntimeVersion"));
            output.previousRuntimeSha256 =
                ptlsmr::canonical_signer_sha256(field(L"previousRuntimeSha256"));
            output.previousTransactionId = field(L"previousTransactionId");
        }
        else if (field(L"previousRuntimeTrack") != L"0" ||
                 field(L"previousRuntimeVersion") != L"none" ||
                 field(L"previousRuntimeSha256") != L"none" ||
                 field(L"previousTransactionId") != L"none")
        {
            throw ptlsmr::win32_error(
                "acquisition journal absent previous identity",
                ERROR_INVALID_DATA);
        }
        output.beforeRuntimeFloor =
            ptlsmr::parse_version(field(L"beforeRuntimeFloor"));
        output.targetRuntimeFloor =
            ptlsmr::parse_version(field(L"targetRuntimeFloor"));
        output.beforeSecurityEpoch = parse_epoch(field(L"beforeSecurityEpoch"));
        output.targetSecurityEpoch = parse_epoch(field(L"targetSecurityEpoch"));
        output.beforeSecurityStateHash =
            ptlsmr::canonical_signer_sha256(field(L"beforeSecurityStateHash"));
        output.targetSecurityStateHash =
            ptlsmr::canonical_signer_sha256(field(L"targetSecurityStateHash"));
        output.phase = field(L"phase");
        validate_acquisition_transaction(output);
        return output;
    }

    void clear_acquisition_journal()
    {
        if (!DeleteFileW(ptlsmr::acquisition_journal_path().c_str()) &&
            GetLastError() != ERROR_FILE_NOT_FOUND)
        {
            throw ptlsmr::win32_error(
                "DeleteFileW(acquisition journal)",
                GetLastError());
        }
    }

    [[nodiscard]] acquisition_transaction begin_acquisition(
        const std::wstring& owner,
        const release_manifest& manifest,
        std::wstring_view manifestHash)
    {
        const auto journalPath = ptlsmr::acquisition_journal_path();
        if (std::filesystem::exists(journalPath) ||
            std::filesystem::exists(journalPath.wstring() + L".new"))
        {
            throw ptlsmr::win32_error("acquisition journal already pending", ERROR_BUSY);
        }
        const auto beforeState = read_accepted_security_state();
        const auto targetState = advance_accepted_security_state(
            beforeState,
            manifest,
            manifestHash);
        const auto beforeFloor = read_runtime_floor(manifest.runtimeTrack);
        const auto previous = find_instance(read_instances(), owner);
        acquisition_transaction value{};
        value.ownerSid = owner;
        value.releaseId = manifest.releaseId;
        value.manifestHash = ptlsmr::canonical_signer_sha256(manifestHash);
        value.runtimeTrack = manifest.runtimeTrack;
        value.runtimeVersion = manifest.runtimeVersion;
        value.targetRuntimeSha256 = manifest.runtimeSha256;
        value.previousRuntimePresent = previous.has_value();
        if (previous)
        {
            value.previousRuntimeTrack = previous->runtimeTrack;
            value.previousRuntimeVersion = previous->runtimeVersion;
            value.previousRuntimeSha256 = previous->runtimeSha256;
            value.previousTransactionId = previous->transactionId;
        }
        value.targetTransactionId =
            previous &&
                previous->runtimeTrack == manifest.runtimeTrack &&
                previous->runtimeVersion == manifest.runtimeVersion &&
                previous->runtimeSha256 == manifest.runtimeSha256
            ? previous->transactionId
            : ptlsmr::random_hex_identifier(16);
        value.beforeRuntimeFloor = beforeFloor;
        value.targetRuntimeFloor = manifest.runtimeVersion;
        value.beforeSecurityEpoch = beforeState.epoch;
        value.targetSecurityEpoch = targetState.epoch;
        value.beforeSecurityStateHash = accepted_security_state_hash(beforeState);
        value.targetSecurityStateHash = accepted_security_state_hash(targetState);
        value.phase = L"prepared";
        write_acquisition_journal(value);
        return value;
    }

    [[nodiscard]] bool acquisition_previous_is_current(
        const acquisition_transaction& value)
    {
        const auto instance = find_instance(read_instances(), value.ownerSid);
        if (!value.previousRuntimePresent)
        {
            return !instance;
        }
        return instance &&
            instance->runtimeTrack == value.previousRuntimeTrack &&
            instance->runtimeVersion == value.previousRuntimeVersion &&
            instance->runtimeSha256 == value.previousRuntimeSha256 &&
            instance->transactionId == value.previousTransactionId;
    }

    [[nodiscard]] bool acquisition_target_preexisted(
        const acquisition_transaction& value)
    {
        return value.previousRuntimePresent &&
            value.previousRuntimeTrack == value.runtimeTrack &&
            value.previousRuntimeVersion == value.runtimeVersion &&
            value.previousRuntimeSha256 == value.targetRuntimeSha256;
    }

    [[nodiscard]] bool acquisition_runtime_is_committed(
        const acquisition_transaction& value)
    {
        const auto instance = find_instance(read_instances(), value.ownerSid);
        return instance &&
            instance->runtimeTrack == value.runtimeTrack &&
            instance->runtimeVersion == value.runtimeVersion &&
            instance->runtimeSha256 == value.targetRuntimeSha256 &&
            instance->transactionId == value.targetTransactionId;
    }

    void finalize_acquisition_transaction(acquisition_transaction value)
    {
        validate_acquisition_transaction(value);
        if (acquisition_target_preexisted(value) &&
            value.phase != L"runtime-committed" &&
            value.phase != L"floor-committed" &&
            value.phase != L"security-committed")
        {
            throw ptlsmr::win32_error(
                "same-runtime acquisition post-readiness commit",
                ERROR_INVALID_STATE);
        }
        if (!acquisition_runtime_is_committed(value))
        {
            throw ptlsmr::win32_error(
                "acquisition target runtime commit",
                ERROR_INVALID_STATE);
        }

        const auto currentFloor = read_runtime_floor(value.runtimeTrack);
        if (!(currentFloor == value.beforeRuntimeFloor) &&
            !(currentFloor == value.targetRuntimeFloor))
        {
            throw ptlsmr::win32_error(
                "acquisition runtime floor recovery state",
                ERROR_INVALID_DATA);
        }
        if (currentFloor == value.beforeRuntimeFloor &&
            !(currentFloor == value.targetRuntimeFloor))
        {
            advance_runtime_floor(value.runtimeTrack, value.targetRuntimeFloor);
        }
        set_acquisition_phase(value, L"floor-committed");

        const auto currentState = read_accepted_security_state();
        const auto currentHash = accepted_security_state_hash(currentState);
        if (currentState.epoch != value.beforeSecurityEpoch &&
            currentState.epoch != value.targetSecurityEpoch)
        {
            throw ptlsmr::win32_error(
                "acquisition security epoch recovery state",
                ERROR_INVALID_DATA);
        }
        if (currentHash != value.beforeSecurityStateHash &&
            currentHash != value.targetSecurityStateHash)
        {
            throw ptlsmr::win32_error(
                "acquisition security state recovery hash",
                ERROR_INVALID_DATA);
        }
        if (currentHash == value.beforeSecurityStateHash &&
            currentHash != value.targetSecurityStateHash)
        {
            const auto targetState = advance_accepted_security_state(
                currentState,
                value.releaseId,
                value.targetSecurityEpoch,
                value.manifestHash);
            if (accepted_security_state_hash(targetState) != value.targetSecurityStateHash)
            {
                throw ptlsmr::win32_error(
                    "acquisition target security state hash",
                    ERROR_INVALID_DATA);
            }
            write_accepted_security_state(targetState);
        }
        set_acquisition_phase(value, L"security-committed");
        clear_acquisition_journal();
    }

    void recover_incomplete_acquisition()
    {
        const auto path = ptlsmr::acquisition_journal_path();
        recover_atomic_journal_replacement(
            path,
            [](const std::filesystem::path& replacement) {
                (void)read_acquisition_journal(replacement);
            },
            "recover acquisition journal replacement");
        if (!std::filesystem::exists(path))
        {
            return;
        }
        auto value = read_acquisition_journal();
        const auto currentFloor = read_runtime_floor(value.runtimeTrack);
        const auto currentState = read_accepted_security_state();
        const auto currentHash = accepted_security_state_hash(currentState);

        if (value.phase == L"prepared")
        {
            if (!(currentFloor == value.beforeRuntimeFloor) ||
                currentState.epoch != value.beforeSecurityEpoch ||
                currentHash != value.beforeSecurityStateHash ||
                !acquisition_previous_is_current(value))
            {
                throw ptlsmr::win32_error(
                    "prepared acquisition recovery state",
                    ERROR_INVALID_DATA);
            }
            clear_acquisition_journal();
            return;
        }
        if (value.phase == L"runtime-provisioning" &&
            acquisition_target_preexisted(value))
        {
            if (!acquisition_runtime_is_committed(value) &&
                !acquisition_previous_is_current(value))
            {
                throw ptlsmr::win32_error(
                    "same-runtime acquisition recovery identity",
                    ERROR_INVALID_DATA);
            }
            if (acquisition_runtime_is_committed(value))
            {
                upsert_instance(
                    value.ownerSid,
                    value.previousRuntimeTrack,
                    value.previousRuntimeVersion,
                    value.previousRuntimeSha256,
                    value.previousTransactionId);
            }
            if (!(currentFloor == value.beforeRuntimeFloor) ||
                currentState.epoch != value.beforeSecurityEpoch ||
                currentHash != value.beforeSecurityStateHash)
            {
                throw ptlsmr::win32_error(
                    "same-runtime acquisition recovery state",
                    ERROR_INVALID_DATA);
            }
            clear_acquisition_journal();
            return;
        }
        if (!acquisition_runtime_is_committed(value))
        {
            if (!(currentFloor == value.beforeRuntimeFloor) ||
                currentState.epoch != value.beforeSecurityEpoch ||
                currentHash != value.beforeSecurityStateHash ||
                !acquisition_previous_is_current(value))
            {
                throw ptlsmr::win32_error(
                    "rolled-back acquisition recovery state",
                    ERROR_INVALID_DATA);
            }
            clear_acquisition_journal();
            return;
        }
        if (value.phase == L"runtime-provisioning")
        {
            set_acquisition_phase(value, L"runtime-committed");
        }
        finalize_acquisition_transaction(std::move(value));
    }

    [[nodiscard]] DWORD parent_process_id()
    {
        ptlsmr::unique_handle snapshot(CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0));
        if (!snapshot)
        {
            throw ptlsmr::win32_error("CreateToolhelp32Snapshot(engine parent)", GetLastError());
        }
        PROCESSENTRY32W entry{};
        entry.dwSize = sizeof(entry);
        if (!Process32FirstW(snapshot.get(), &entry))
        {
            throw ptlsmr::win32_error("Process32FirstW(engine parent)", GetLastError());
        }
        do
        {
            if (entry.th32ProcessID == GetCurrentProcessId())
            {
                return entry.th32ParentProcessID;
            }
        } while (Process32NextW(snapshot.get(), &entry));
        throw ptlsmr::win32_error("engine parent process lookup", ERROR_NOT_FOUND);
    }

    [[nodiscard]] DWORD parse_host_pid(const std::vector<std::wstring>& arguments)
    {
        const auto value = ptlsmr::argument_value(arguments, L"--host-pid");
        if (value.empty() || value.size() > 10 ||
            !std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return character >= L'0' && character <= L'9';
            }))
        {
            throw ptlsmr::win32_error("engine host PID argument", ERROR_INVALID_PARAMETER);
        }
        try
        {
            const auto pid = std::stoul(value);
            if (pid == 0 || pid > MAXDWORD)
            {
                throw ptlsmr::win32_error("engine host PID range", ERROR_INVALID_PARAMETER);
            }
            return static_cast<DWORD>(pid);
        }
        catch (const std::invalid_argument&)
        {
            throw ptlsmr::win32_error("engine host PID argument", ERROR_INVALID_PARAMETER);
        }
        catch (const std::out_of_range&)
        {
            throw ptlsmr::win32_error("engine host PID range", ERROR_INVALID_PARAMETER);
        }
    }

    [[nodiscard]] uint16_t parse_request_command(const std::vector<std::wstring>& arguments)
    {
        const auto value = ptlsmr::argument_value(arguments, L"--request-command");
        if (value.size() != 1 ||
            value[0] < static_cast<wchar_t>(L'0' + static_cast<uint16_t>(ptlsmr::public_command::acquire)) ||
            value[0] > static_cast<wchar_t>(L'0' + static_cast<uint16_t>(ptlsmr::public_command::release)))
        {
            throw ptlsmr::win32_error("engine request command argument", ERROR_INVALID_PARAMETER);
        }
        return static_cast<uint16_t>(value[0] - L'0');
    }

    void validate_host_parent(DWORD hostPid)
    {
        if (parent_process_id() != hostPid ||
            !equal_path(
                ptlsmr::raw_process_image_path(hostPid),
                ptlsmr::host_executable_path()))
        {
            throw ptlsmr::win32_error("engine host parent and image policy", ERROR_ACCESS_DENIED);
        }
        (void)ptlsmr::validate_host_candidate(
            ptlsmr::host_executable_path(),
            ptlsmr::read_code_signer_pin());
    }

    void validate_engine_execution(
        DWORD hostPid,
        const ptlsmr::file_version& expectedVersion)
    {
        validate_host_parent(hostPid);
        if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("engine LocalSystem token policy", ERROR_ACCESS_DENIED);
        }
        const auto executable = module_path();
        const auto expected = ptlsmr::engine_executable_path(expectedVersion);
        if (!equal_path(executable, expected))
        {
            throw ptlsmr::win32_error("engine protected execution path and version", ERROR_ACCESS_DENIED);
        }
        const auto validated = ptlsmr::validate_engine_candidate(
            executable,
            ptlsmr::read_code_signer_pin());
        if (!(validated == expectedVersion))
        {
            throw ptlsmr::win32_error("engine executable version policy", ERROR_REVISION_MISMATCH);
        }
        (void)ptlsmr::require_no_package_identity();
    }

    void write_engine_reply_atomic(
        const std::filesystem::path& path,
        const ptlsmr::engine_reply& value)
    {
        const auto temporary = path.wstring() + L".new";
        ptlsmr::unique_handle file(CreateFileW(
            temporary.c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_ALWAYS,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr));
        if (!file)
        {
            throw ptlsmr::win32_error("CreateFileW(engine response)", GetLastError());
        }
        DWORD written = 0;
        ptlsmr::check_bool(
            WriteFile(file.get(), &value, sizeof(value), &written, nullptr) &&
                written == sizeof(value),
            "WriteFile(engine response)");
        ptlsmr::check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers(engine response)");
        file.reset();
        ptlsmr::check_bool(
            MoveFileExW(
                temporary.c_str(),
                path.c_str(),
                MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH),
            "MoveFileExW(engine response)");
    }

    [[nodiscard]] ptlsmr::engine_request read_engine_request(
        const std::filesystem::path& path)
    {
        if (!ptlsmr::path_is_within(path, ptlsmr::requests_root()))
        {
            throw ptlsmr::win32_error("engine request protected path policy", ERROR_ACCESS_DENIED);
        }
        ptlsmr::unique_handle file(CreateFileW(
            path.c_str(),
            GENERIC_READ,
            FILE_SHARE_READ,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_OPEN_REPARSE_POINT,
            nullptr));
        if (!file)
        {
            throw ptlsmr::win32_error("CreateFileW(engine request)", GetLastError());
        }
        LARGE_INTEGER size{};
        ptlsmr::check_bool(GetFileSizeEx(file.get(), &size), "GetFileSizeEx(engine request)");
        if (size.QuadPart != sizeof(ptlsmr::engine_request))
        {
            throw ptlsmr::win32_error("engine request size policy", ERROR_INVALID_DATA);
        }
        ptlsmr::engine_request input{};
        DWORD read = 0;
        ptlsmr::check_bool(
            ReadFile(file.get(), &input, sizeof(input), &read, nullptr) && read == sizeof(input),
            "ReadFile(engine request)");
        if (input.magic != ptlsmr::ProtocolMagic ||
            input.version != ptlsmr::ProtocolVersion ||
            input.reserved != 0 ||
            input.ownerSid[ARRAYSIZE(input.ownerSid) - 1] != L'\0' ||
            input.releaseId[ARRAYSIZE(input.releaseId) - 1] != L'\0' ||
            input.inboxPath[ARRAYSIZE(input.inboxPath) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("engine request protocol", ERROR_INVALID_DATA);
        }
        (void)ptlsmr::canonical_owner_sid(input.ownerSid);
        return input;
    }

    void fill_engine_status(
        const std::wstring& owner,
        ptlsmr::engine_reply& output)
    {
        ptlsmr::reply status{};
        fill_status(owner, status);
        output.scmState = status.scmState;
        output.processId = status.processId;
        copy_bounded(output.runtimeVersion, ARRAYSIZE(output.runtimeVersion), status.runtimeVersion);
        copy_bounded(output.detail, ARRAYSIZE(output.detail), status.detail);
    }

    void prepare_engine_update(
        const release_manifest& manifest,
        const std::filesystem::path& stagedEngine,
        ptlsmr::engine_reply& output)
    {
        if (!manifest.engineVersion)
        {
            throw ptlsmr::win32_error("engine update candidate policy", ERROR_INVALID_DATA);
        }
        const auto active = ptlsmr::parse_version(
            ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
        if (!(active < *manifest.engineVersion))
        {
            throw ptlsmr::win32_error("engine update monotonic policy", ERROR_REVISION_MISMATCH);
        }
        const auto targetDirectory = ptlsmr::engine_install_directory(*manifest.engineVersion);
        ptlsmr::protect_system_directory(targetDirectory);
        const auto target = targetDirectory / ptlsmr::EngineExe;
        if (std::filesystem::exists(target))
        {
            (void)ptlsmr::validate_engine_candidate(target, ptlsmr::read_code_signer_pin());
            if (!ptlsmr::files_are_identical(target, stagedEngine))
            {
                throw ptlsmr::win32_error("engine version collision policy", ERROR_FILE_EXISTS);
            }
            std::filesystem::remove(stagedEngine);
        }
        else
        {
            ptlsmr::move_file_atomically(stagedEngine, target);
        }
        output.action = static_cast<uint16_t>(ptlsmr::engine_action::activate_engine);
        copy_bounded(
            output.candidateEngineVersion,
            ARRAYSIZE(output.candidateEngineVersion),
            ptlsmr::format_version(*manifest.engineVersion));
        copy_bounded(
            output.candidateEnginePath,
            ARRAYSIZE(output.candidateEnginePath),
            target.wstring());
        if (manifest.engineCrashPhase != L"none")
        {
            copy_bounded(
                output.engineCrashPhase,
                ARRAYSIZE(output.engineCrashPhase),
                manifest.engineCrashPhase);
        }
    }

    void require_metadata_epoch(
        const release_manifest& manifest,
        std::wstring_view manifestHash)
    {
        (void)advance_accepted_security_state(
            read_accepted_security_state(),
            manifest,
            manifestHash);
    }

    void process_acquire(
        const ptlsmr::engine_request& input,
        ptlsmr::engine_reply& output)
    {
        g_releaseStagingCleanupPending = false;
        const std::wstring owner = ptlsmr::canonical_owner_sid(input.ownerSid);
        const std::wstring releaseId(input.releaseId);
        if (!is_valid_release_id(releaseId) || input.inboxPath[0] == L'\0')
        {
            throw ptlsmr::win32_error("acquire engine request shape", ERROR_INVALID_PARAMETER);
        }
        const auto inbox = std::filesystem::path(input.inboxPath);
        if (inbox.wstring().starts_with(L"\\\\") ||
            inbox.filename() != releaseId ||
            GetDriveTypeW(inbox.root_path().c_str()) == DRIVE_REMOTE)
        {
            throw ptlsmr::win32_error("acquire inbox root policy", ERROR_ACCESS_DENIED);
        }
        g_enginePhase = L"transaction reconciliation";
        converge_pending_transactions();
        g_enginePhase = L"release staging creation";
        const auto stage = ptlsmr::create_protected_staging_directory(
            ptlsmr::installation_root() / L"Staging",
            L"release");
        try
        {
            g_enginePhase = L"release manifest intake";
            const auto stagedManifest = stage_inbox_file(
                inbox,
                ptlsmr::ReleaseManifestExe,
                stage,
                ptlsmr::MaxReleaseManifestBytes);
            const auto manifest = parse_release_manifest(stagedManifest, releaseId);
            const auto manifestHash = ptlsmr::sha256_file(stagedManifest);
            require_metadata_epoch(manifest, manifestHash);

            g_enginePhase = L"runtime artifact intake";
            const auto stagedRuntime = stage_inbox_file(
                inbox,
                manifest.runtimeFile,
                stage,
                ptlsmr::MaxRuntimeArtifactBytes,
                manifest.runtimeLength);
            if (ptlsmr::sha256_file(stagedRuntime) != manifest.runtimeSha256)
            {
                throw ptlsmr::win32_error("release runtime SHA-256 policy", ERROR_CRC);
            }
            const auto validatedRuntime = ptlsmr::validate_runtime_candidate(
                stagedRuntime,
                manifest.runtimeTrack,
                ptlsmr::read_code_signer_pin());
            if (!(validatedRuntime == manifest.runtimeVersion) ||
                manifest.runtimeVersion < read_runtime_floor(manifest.runtimeTrack))
            {
                throw ptlsmr::win32_error("release runtime version floor", ERROR_REVISION_MISMATCH);
            }

            std::optional<std::filesystem::path> stagedEngine;
            std::optional<ptlsmr::file_version> activeEngine;
            if (manifest.engineVersion)
            {
                g_enginePhase = L"engine artifact intake";
                stagedEngine = stage_inbox_file(
                    inbox,
                    manifest.engineFile,
                    stage,
                    ptlsmr::MaxEngineArtifactBytes,
                    manifest.engineLength);
                if (ptlsmr::sha256_file(*stagedEngine) != manifest.engineSha256)
                {
                    throw ptlsmr::win32_error("release engine SHA-256 policy", ERROR_CRC);
                }
                const auto validatedEngine = ptlsmr::validate_engine_candidate(
                    *stagedEngine,
                    ptlsmr::read_code_signer_pin());
                if (!(validatedEngine == *manifest.engineVersion))
                {
                    throw ptlsmr::win32_error("release engine version policy", ERROR_REVISION_MISMATCH);
                }
                activeEngine = ptlsmr::parse_version(
                    ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
                if (*manifest.engineVersion < *activeEngine)
                {
                    throw ptlsmr::win32_error("release engine downgrade policy", ERROR_REVISION_MISMATCH);
                }
                if (*manifest.engineVersion == *activeEngine &&
                    !ptlsmr::files_are_identical(
                        *stagedEngine,
                        ptlsmr::engine_executable_path(*activeEngine)))
                {
                    throw ptlsmr::win32_error("engine version collision policy", ERROR_FILE_EXISTS);
                }
            }

            g_enginePhase = L"durable SID lease insertion";
            ensure_lease(owner);
            g_enginePhase = L"outer acquisition journal preparation";
            auto acquisition = begin_acquisition(owner, manifest, manifestHash);

            if (manifest.engineVersion && *activeEngine < *manifest.engineVersion)
            {
                prepare_engine_update(manifest, *stagedEngine, output);
                g_enginePhase = L"release staging cleanup";
                std::filesystem::remove_all(stage);
                return;
            }

            ptlsmr::request runtimeRequest{};
            runtimeRequest.magic = ptlsmr::ProtocolMagic;
            runtimeRequest.version = ptlsmr::ProtocolVersion;
            runtimeRequest.command = static_cast<uint16_t>(ptlsmr::command::provision);
            runtimeRequest.runtimeTrack = manifest.runtimeTrack;
            copy_bounded(runtimeRequest.ownerSid, ARRAYSIZE(runtimeRequest.ownerSid), owner);
            copy_bounded(
                runtimeRequest.candidatePath,
                ARRAYSIZE(runtimeRequest.candidatePath),
                stagedRuntime.wstring());
            copy_bounded(
                runtimeRequest.transactionId,
                ARRAYSIZE(runtimeRequest.transactionId),
                acquisition.targetTransactionId);
            const auto runtimeCrashPhase = consume_runtime_crash_phase(
                manifest.releaseId,
                manifest.runtimeCrashPhase);
            set_acquisition_phase(acquisition, L"runtime-provisioning");
            if (!runtimeCrashPhase.empty())
            {
                copy_bounded(
                    runtimeRequest.crashPhase,
                    ARRAYSIZE(runtimeRequest.crashPhase),
                    runtimeCrashPhase);
            }
            ptlsmr::reply runtimeReply{};
            g_enginePhase = L"runtime provisioning";
            handle_request(runtimeRequest, runtimeReply, false);

            g_enginePhase = L"outer acquisition commit";
            set_acquisition_phase(acquisition, L"runtime-committed");
            finalize_acquisition_transaction(acquisition);

            output.scmState = runtimeReply.scmState;
            output.processId = runtimeReply.processId;
            output.leaseCount = 1;
            copy_bounded(
                output.runtimeVersion,
                ARRAYSIZE(output.runtimeVersion),
                runtimeReply.runtimeVersion);
            copy_bounded(output.detail, ARRAYSIZE(output.detail), runtimeReply.detail);
            g_enginePhase = L"release staging cleanup";
            std::filesystem::remove_all(stage);
        }
        catch (...)
        {
            const auto failure = std::current_exception();
            g_enginePhase = L"release staging cleanup after failure";
            std::error_code cleanupError;
            std::filesystem::remove_all(stage, cleanupError);
            g_releaseStagingCleanupPending = static_cast<bool>(cleanupError);
            if (std::filesystem::exists(ptlsmr::acquisition_journal_path()))
            {
                g_enginePhase = L"outer acquisition failure recovery";
                recover_incomplete_acquisition();
            }
            std::rethrow_exception(failure);
        }
    }

    void process_status(
        const ptlsmr::engine_request& input,
        ptlsmr::engine_reply& output)
    {
        if (input.releaseId[0] != L'\0' || input.inboxPath[0] != L'\0')
        {
            throw ptlsmr::win32_error("status engine request shape", ERROR_INVALID_PARAMETER);
        }
        const std::wstring owner = ptlsmr::canonical_owner_sid(input.ownerSid);
        const auto leases = read_leases();
        if (!has_lease(leases, owner))
        {
            throw ptlsmr::win32_error("caller lease status policy", ERROR_NOT_FOUND);
        }
        converge_pending_transactions();
        fill_engine_status(owner, output);
        output.leaseCount = 1;
    }

    void process_release(
        const ptlsmr::engine_request& input,
        ptlsmr::engine_reply& output)
    {
        if (input.releaseId[0] != L'\0' || input.inboxPath[0] != L'\0')
        {
            throw ptlsmr::win32_error("release engine request shape", ERROR_INVALID_PARAMETER);
        }
        const std::wstring owner = ptlsmr::canonical_owner_sid(input.ownerSid);
        auto leases = read_leases();
        const auto found = std::find_if(
            leases.begin(),
            leases.end(),
            [&](const lease& item) {
                return item.ownerSid == owner;
            });
        if (found == leases.end())
        {
            throw ptlsmr::win32_error("caller lease release policy", ERROR_NOT_FOUND);
        }
        converge_pending_transactions();
        cleanup(owner, L"");
        leases.erase(found);
        write_leases(leases);
        output.leaseCount = 0;
        copy_bounded(output.detail, ARRAYSIZE(output.detail), L"lease released");
    }

    void process_engine_request(
        const ptlsmr::engine_request& input,
        ptlsmr::engine_reply& output)
    {
        output.command = input.command;
        output.action = static_cast<uint16_t>(ptlsmr::engine_action::complete);
        const auto operation = static_cast<ptlsmr::public_command>(input.command);
        switch (operation)
        {
        case ptlsmr::public_command::acquire:
            process_acquire(input, output);
            break;
        case ptlsmr::public_command::status:
            process_status(input, output);
            break;
        case ptlsmr::public_command::release:
            process_release(input, output);
            break;
        default:
            throw ptlsmr::win32_error("engine public command policy", ERROR_INVALID_FUNCTION);
        }
        copy_bounded(
            output.activeEngineVersion,
            ARRAYSIZE(output.activeEngineVersion),
            ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
    }

    int run_engine_request(const std::vector<std::wstring>& arguments)
    {
        if (arguments.size() != 9 ||
            arguments[1] != L"--engine-request" ||
            arguments[3] != L"--engine-response" ||
            arguments[5] != L"--host-pid" ||
            arguments[7] != L"--request-command")
        {
            return ERROR_INVALID_PARAMETER;
        }
        const auto requestPath = std::filesystem::path(arguments[2]);
        const auto responsePath = std::filesystem::path(arguments[4]);
        if (!ptlsmr::path_is_within(responsePath, ptlsmr::requests_root()))
        {
            throw ptlsmr::win32_error("engine response protected path policy", ERROR_ACCESS_DENIED);
        }
        const uint16_t expectedCommand = parse_request_command(arguments);
        ptlsmr::engine_reply output{};
        output.command = expectedCommand;
        try
        {
            g_enginePhase = L"engine identity validation";
            const auto input = read_engine_request(requestPath);
            if (input.command != expectedCommand)
            {
                throw ptlsmr::win32_error("engine request command binding", ERROR_INVALID_DATA);
            }
            const DWORD hostPid = parse_host_pid(arguments);
            const auto activeVersion = ptlsmr::parse_version(
                ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
            validate_engine_execution(hostPid, activeVersion);
            g_enginePhase = L"engine request processing";
            process_engine_request(input, output);
        }
        catch (const ptlsmr::win32_error& error)
        {
            output.win32Status = error.code();
            const std::string_view text(error.what());
            std::wstring detail(text.begin(), text.end());
            if (g_releaseStagingCleanupPending)
            {
                detail += L"; release staging cleanup pending";
            }
            copy_bounded(output.detail, ARRAYSIZE(output.detail), detail);
        }
        catch (const std::exception& error)
        {
            output.win32Status = ERROR_UNHANDLED_EXCEPTION;
            const std::string_view text(error.what());
            std::wstring detail =
                L"engine " + g_enginePhase + L": " +
                std::wstring(text.begin(), text.end());
            if (g_releaseStagingCleanupPending)
            {
                detail += L"; release staging cleanup pending";
            }
            copy_bounded(
                output.detail,
                ARRAYSIZE(output.detail),
                detail);
        }
        catch (...)
        {
            output.win32Status = ERROR_UNHANDLED_EXCEPTION;
            copy_bounded(output.detail, ARRAYSIZE(output.detail), L"unexpected non-standard engine failure");
        }
        write_engine_reply_atomic(responsePath, output);
        return output.win32Status == ERROR_SUCCESS ? ERROR_SUCCESS : static_cast<int>(output.win32Status);
    }

    int run_engine_self_test(const std::vector<std::wstring>& arguments)
    {
        if (arguments.size() != 6 ||
            arguments[1] != L"--self-test" ||
            arguments[2] != L"--host-pid" ||
            arguments[4] != L"--candidate-version")
        {
            return ERROR_INVALID_PARAMETER;
        }
        const auto expectedVersion = ptlsmr::parse_version(arguments[5]);
        validate_engine_execution(parse_host_pid(arguments), expectedVersion);
#if PT_ENGINE_SLOW_QUALIFICATION
        const auto marker =
            ptlsmr::program_data_root() / L"slow-engine-qualification-5.4.0.0.txt";
        if (!std::filesystem::exists(marker))
        {
            ptlsmr::write_utf8_file_atomic(
                marker,
                L"processId=" + std::to_wstring(GetCurrentProcessId()) + L"\r\n" +
                    L"candidateVersion=" + ptlsmr::format_version(expectedVersion) + L"\r\n" +
                    L"state=qualification-entered\r\n");
            Sleep(INFINITE);
        }
#endif
#if PT_ENGINE_FAIL_QUALIFICATION
        return ERROR_SERVICE_NOT_ACTIVE;
#else
        return ERROR_SUCCESS;
#endif
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if (arguments.size() >= 2 && arguments[1] == L"--engine-request")
        {
            return run_engine_request(arguments);
        }
        if (arguments.size() >= 2 && arguments[1] == L"--self-test")
        {
            return run_engine_self_test(arguments);
        }
        return ERROR_INVALID_PARAMETER;
    }
    catch (const ptlsmr::win32_error& error)
    {
        std::cerr << "status=" << error.code() << " operation=" << error.what() << "\n";
        return static_cast<int>(error.code());
    }
    catch (const std::exception&)
    {
        std::cerr << "status=" << ERROR_UNHANDLED_EXCEPTION <<
            " operation=standard engine exception\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
    catch (...)
    {
        std::cerr << "status=" << ERROR_UNHANDLED_EXCEPTION <<
            " operation=non-standard engine exception\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
