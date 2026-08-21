#include "../Common/LsmrCommon.h"

#include <aclapi.h>
#include <sddl.h>
#include <shellapi.h>

#include <algorithm>
#include <array>
#include <filesystem>
#include <map>
#include <optional>
#include <sstream>
#include <thread>

#pragma comment(lib, "advapi32.lib")

namespace
{
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
    };

    struct installed_candidate
    {
        uint16_t runtimeTrack{};
        ptlsmr::file_version version{};
        std::filesystem::path stagingDirectory;
        std::filesystem::path stagingExecutable;
        std::filesystem::path executable;
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
        ptlsmr::file_version candidateVersion{};
        std::filesystem::path stagingPath;
        std::filesystem::path candidatePath;
        std::wstring phase;
    };

    struct cleanup_transaction
    {
        std::wstring ownerSid;
        std::wstring serviceName;
        uint16_t runtimeTrack{};
        ptlsmr::file_version runtimeVersion{};
        std::wstring phase;
    };

    constexpr size_t MaxManagedOwners = 32;

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

    [[nodiscard]] std::vector<managed_instance> read_instances()
    {
        if (!std::filesystem::exists(inventory_path()))
        {
            return {};
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
                if (fields.size() != 3 ||
                    (fields[1] != L"1" && fields[1] != L"2"))
                {
                    throw ptlsmr::win32_error("runtime inventory format", ERROR_INVALID_DATA);
                }
                managed_instance instance{
                    ptlsmr::canonical_owner_sid(fields[0]),
                    static_cast<uint16_t>(fields[1][0] - L'0'),
                    ptlsmr::parse_version(fields[2]),
                };
                if (instance.runtimeVersion.major != instance.runtimeTrack ||
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
                instance.runtimeVersion.major != instance.runtimeTrack)
            {
                throw ptlsmr::win32_error("runtime inventory write policy", ERROR_INVALID_DATA);
            }
            output << instance.ownerSid << L"|"
                   << instance.runtimeTrack << L"|"
                   << ptlsmr::format_version(instance.runtimeVersion) << L"\r\n";
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
        const ptlsmr::file_version& version)
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
                instances.push_back({ owner, runtimeTrack, version });
            }
        else
        {
            found->runtimeTrack = runtimeTrack;
            found->runtimeVersion = version;
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
            ptlsmr::copy_file_to_protected_stage(suppliedCandidate, stagedExecutable);
            const auto version = ptlsmr::validate_runtime_candidate(
                stagedExecutable,
                runtimeTrack,
                ptlsmr::read_trusted_signer_pin());
            const auto runtimeDirectory = ptlsmr::runtime_install_directory(runtimeTrack, version);
            const auto destination = runtimeDirectory / ptlsmr::RuntimeExe;
            return { runtimeTrack, version, stagedDirectory, stagedExecutable, destination };
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
        content << L"candidateVersion=" << ptlsmr::format_version(value.candidateVersion) << L"\r\n";
        content << L"stagingPath=" << value.stagingPath.wstring() << L"\r\n";
        content << L"candidatePath=" << value.candidatePath.wstring() << L"\r\n";
        content << L"phase=" << value.phase << L"\r\n";
        ptlsmr::write_utf8_file_atomic(journal_path(), content.str());
    }

    void set_phase(transaction& value, std::wstring_view phase)
    {
        value.phase = phase;
        write_journal(value);
    }

    [[nodiscard]] transaction read_journal()
    {
        const auto content = ptlsmr::read_utf8_file(journal_path(), 16 * 1024);
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
        if (fields.size() != 11 ||
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
        output.phase = value(L"phase");
        if (output.existing)
        {
            output.previousVersion = ptlsmr::parse_version(value(L"previousVersion"));
            output.previousPath = value(L"previousPath");
        }
        else if (!value(L"previousVersion").empty() || !value(L"previousPath").empty())
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
            committed->runtimeVersion == value.candidateVersion;
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
                ptlsmr::runtime_executable_path(value.runtimeTrack, value.candidateVersion)))
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
        }
        if (candidateExists)
        {
            (void)ptlsmr::validate_runtime_candidate(
                value.candidatePath,
                value.runtimeTrack,
                signerPin);
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
            committed->runtimeVersion == state.candidateVersion)
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
        content << L"phase=" << value.phase << L"\r\n";
        ptlsmr::write_utf8_file_atomic(cleanup_journal_path(), content.str());
    }

    void set_cleanup_phase(cleanup_transaction& value, std::wstring_view phase)
    {
        value.phase = phase;
        write_cleanup_journal(value);
    }

    [[nodiscard]] cleanup_transaction read_cleanup_journal()
    {
        const auto content = ptlsmr::read_utf8_file(cleanup_journal_path(), 4096);
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
        if (fields.size() != 5 || (value(L"track") != L"1" && value(L"track") != L"2"))
        {
            throw ptlsmr::win32_error("cleanup journal policy", ERROR_INVALID_DATA);
        }
        return {
            ptlsmr::canonical_owner_sid(value(L"owner")),
            value(L"service"),
            static_cast<uint16_t>(value(L"track")[0] - L'0'),
            ptlsmr::parse_version(value(L"version")),
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
             current->runtimeVersion != state.runtimeVersion))
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
            if (!entry.is_directory())
            {
                continue;
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
        if (std::filesystem::exists(cleanup_journal_path()))
        {
            finalize_cleanup_transaction(read_cleanup_journal());
        }
    }

    void converge_pending_transactions()
    {
        recover_incomplete_transaction();
        recover_incomplete_cleanup();
        if (std::filesystem::exists(journal_path()) ||
            std::filesystem::exists(cleanup_journal_path()))
        {
            throw ptlsmr::win32_error("transaction convergence policy", ERROR_BUSY);
        }
        reconcile_protected_state();
    }

    void provision(
        const std::wstring& owner,
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedCandidate,
        std::wstring_view crashPhase)
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
            }
            else
            {
                if (prospective.size() >= MaxManagedOwners)
                {
                    throw ptlsmr::win32_error(
                        "runtime inventory prospective limit",
                        ERROR_TOO_MANY_NAMES);
                }
                prospective.push_back({ owner, candidate->runtimeTrack, candidate->version });
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
            upsert_instance(owner, candidate->runtimeTrack, candidate->version);
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

    void handle_request(const ptlsmr::request& input, ptlsmr::reply& output)
    {
        output.command = input.command;
        if (input.magic != ptlsmr::ProtocolMagic ||
            input.version != ptlsmr::ProtocolVersion ||
            input.reserved != 0 ||
            input.ownerSid[ARRAYSIZE(input.ownerSid) - 1] != L'\0' ||
            input.candidatePath[ARRAYSIZE(input.candidatePath) - 1] != L'\0' ||
            input.crashPhase[ARRAYSIZE(input.crashPhase) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("pipe request protocol", ERROR_INVALID_DATA);
        }
        converge_pending_transactions();
        const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
        switch (static_cast<ptlsmr::command>(input.command))
        {
        case ptlsmr::command::provision:
            if ((input.runtimeTrack != 1 && input.runtimeTrack != 2) ||
                input.candidatePath[0] == L'\0' ||
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
                input.crashPhase);
            fill_status(owner, output);
            break;
        case ptlsmr::command::status:
            if (input.runtimeTrack != 0 || input.candidatePath[0] != L'\0' ||
                input.crashPhase[0] != L'\0')
            {
                throw ptlsmr::win32_error("status request policy", ERROR_INVALID_PARAMETER);
            }
            fill_status(owner, output);
            break;
        case ptlsmr::command::cleanup:
            if (input.runtimeTrack != 0 || input.candidatePath[0] != L'\0' ||
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
}

int wmain()
{
    wchar_t updaterName[] = L"PtPuvrUpdater";
    SERVICE_TABLE_ENTRYW table[] = {
        { updaterName, service_main },
        { nullptr, nullptr },
    };
    if (!StartServiceCtrlDispatcherW(table))
    {
        return static_cast<int>(GetLastError());
    }
    return ERROR_SUCCESS;
}
