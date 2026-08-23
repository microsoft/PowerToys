#include "../Common/LsmrCommon.h"

#include <sddl.h>
#include <shellapi.h>

#include <algorithm>
#include <array>
#include <filesystem>
#include <functional>
#include <map>
#include <memory>
#include <mutex>
#include <optional>
#include <set>
#include <sstream>
#include <system_error>
#include <thread>
#include <utility>
#include <vector>

namespace
{
    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    SERVICE_STATUS g_status{};
    ptlsmr::unique_handle g_stopEvent;
    ptlsmr::unique_handle g_dispatchMutex;
    std::wstring g_publishedEndpoint;
    DWORD g_localFixedDriveMask{};
    thread_local std::wstring g_operationPhase = L"idle";
    constexpr DWORD PipeIoTimeoutMilliseconds = 5000;
    constexpr DWORD ChildTimeoutMilliseconds = 120000;
    constexpr size_t HostPipeInstanceCount = 4;
    constexpr size_t PerSidActiveConnectionLimit = 1;
    std::mutex g_activeConnectionMutex;
    std::map<std::wstring, size_t> g_activeConnections;

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

    class registry_key
    {
    public:
        explicit registry_key(HKEY value = nullptr) noexcept :
            m_value(value)
        {
        }

        ~registry_key()
        {
            if (m_value)
            {
                RegCloseKey(m_value);
            }
        }

        registry_key(const registry_key&) = delete;
        registry_key& operator=(const registry_key&) = delete;

        [[nodiscard]] HKEY get() const noexcept
        {
            return m_value;
        }

        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        HKEY m_value{};
    };

    class overlapped_pipe_operation
    {
    public:
        overlapped_pipe_operation()
        {
            m_event.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!m_event)
            {
                throw ptlsmr::win32_error("CreateEventW(host pipe operation)", GetLastError());
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

    class process_attribute_list
    {
    public:
        explicit process_attribute_list(HANDLE inheritedHandle) :
            m_inheritedHandle(inheritedHandle)
        {
            SIZE_T bytes = 0;
            (void)InitializeProcThreadAttributeList(nullptr, 1, 0, &bytes);
            if (bytes == 0)
            {
                throw ptlsmr::win32_error(
                    "InitializeProcThreadAttributeList(size)",
                    GetLastError());
            }
            m_storage.resize(bytes);
            m_value = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(m_storage.data());
            ptlsmr::check_bool(
                InitializeProcThreadAttributeList(m_value, 1, 0, &bytes),
                "InitializeProcThreadAttributeList");
            try
            {
                ptlsmr::check_bool(
                    UpdateProcThreadAttribute(
                        m_value,
                        0,
                        PROC_THREAD_ATTRIBUTE_HANDLE_LIST,
                        &m_inheritedHandle,
                        sizeof(m_inheritedHandle),
                        nullptr,
                        nullptr),
                    "UpdateProcThreadAttribute(engine diagnostic handle)");
            }
            catch (...)
            {
                DeleteProcThreadAttributeList(m_value);
                m_value = nullptr;
                throw;
            }
        }

        ~process_attribute_list()
        {
            if (m_value)
            {
                DeleteProcThreadAttributeList(m_value);
            }
        }

        process_attribute_list(const process_attribute_list&) = delete;
        process_attribute_list& operator=(const process_attribute_list&) = delete;

        [[nodiscard]] LPPROC_THREAD_ATTRIBUTE_LIST get() const noexcept
        {
            return m_value;
        }

    private:
        std::vector<BYTE> m_storage;
        LPPROC_THREAD_ATTRIBUTE_LIST m_value{};
        HANDLE m_inheritedHandle{};
    };

    struct job_child
    {
        ptlsmr::unique_handle job;
        ptlsmr::unique_handle process;
        DWORD processId{};
    };

    enum class pipe_io_result
    {
        completed,
        disconnected,
        stopped,
        timed_out,
    };

    [[nodiscard]] pipe_io_result perform_stop_aware_pipe_io(
        HANDLE pipe,
        void* buffer,
        DWORD bytes,
        DWORD& transferred,
        bool writeOperation);

    struct caller_identity
    {
        DWORD processId{};
        std::wstring ownerSid;
        std::wstring imagePath;
        ptlsmr::unique_handle process;
        ptlsmr::unique_handle processToken;
    };

    struct caller_context
    {
        DWORD processId{};
        std::wstring ownerSid;
        std::filesystem::path inboxRoot;
    };

    class pipe_client_impersonation_guard
    {
    public:
        explicit pipe_client_impersonation_guard(HANDLE pipe)
        {
            ptlsmr::check_bool(
                ImpersonateNamedPipeClient(pipe),
                "ImpersonateNamedPipeClient(host caller)");
            m_active = true;
        }

        ~pipe_client_impersonation_guard()
        {
            if (m_active)
            {
                (void)RevertToSelf();
            }
        }

        pipe_client_impersonation_guard(
            const pipe_client_impersonation_guard&) = delete;
        pipe_client_impersonation_guard& operator=(
            const pipe_client_impersonation_guard&) = delete;

        void revert()
        {
            if (!m_active)
            {
                return;
            }
            if (!RevertToSelf())
            {
                const DWORD error = GetLastError();
                throw std::system_error(
                    static_cast<int>(error),
                    std::system_category(),
                    "RevertToSelf(host caller)");
            }
            m_active = false;
        }

    private:
        bool m_active{};
    };

    class active_connection_guard
    {
    public:
        explicit active_connection_guard(std::wstring ownerSid) :
            m_ownerSid(std::move(ownerSid))
        {
            std::lock_guard lock(g_activeConnectionMutex);
            auto& count = g_activeConnections[m_ownerSid];
            if (count >= PerSidActiveConnectionLimit)
            {
                if (count == 0)
                {
                    g_activeConnections.erase(m_ownerSid);
                }
                return;
            }
            ++count;
            m_acquired = true;
        }

        ~active_connection_guard()
        {
            if (!m_acquired)
            {
                return;
            }
            std::lock_guard lock(g_activeConnectionMutex);
            const auto found = g_activeConnections.find(m_ownerSid);
            if (found != g_activeConnections.end() && --found->second == 0)
            {
                g_activeConnections.erase(found);
            }
        }

        active_connection_guard(const active_connection_guard&) = delete;
        active_connection_guard& operator=(const active_connection_guard&) = delete;

        [[nodiscard]] bool acquired() const noexcept
        {
            return m_acquired;
        }

    private:
        std::wstring m_ownerSid;
        bool m_acquired{};
    };

    class dispatch_guard
    {
    public:
        dispatch_guard()
        {
            const HANDLE waits[]{ g_stopEvent.get(), g_dispatchMutex.get() };
            const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, INFINITE);
            if (wait == WAIT_OBJECT_0)
            {
                throw ptlsmr::win32_error(
                    "host dispatch stopped",
                    ERROR_OPERATION_ABORTED);
            }
            if (wait != WAIT_OBJECT_0 + 1)
            {
                throw ptlsmr::win32_error(
                    "WaitForMultipleObjects(host dispatch)",
                    wait == WAIT_FAILED ? GetLastError() : ERROR_GEN_FAILURE);
            }
            m_acquired = true;
        }

        ~dispatch_guard()
        {
            if (m_acquired)
            {
                (void)ReleaseMutex(g_dispatchMutex.get());
            }
        }

        dispatch_guard(const dispatch_guard&) = delete;
        dispatch_guard& operator=(const dispatch_guard&) = delete;

    private:
        bool m_acquired{};
    };

    struct activation_journal
    {
        ptlsmr::file_version previous{};
        ptlsmr::file_version candidate{};
        std::wstring phase;
    };

    [[nodiscard]] constexpr bool is_expected_pipe_disconnect(DWORD error) noexcept
    {
        return error == ERROR_BROKEN_PIPE ||
            error == ERROR_NO_DATA ||
            error == ERROR_PIPE_NOT_CONNECTED ||
            error == ERROR_MORE_DATA ||
            error == ERROR_OPERATION_ABORTED;
    }

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw ptlsmr::win32_error("bounded control-plane protocol field", ERROR_BUFFER_OVERFLOW);
        }
        std::copy(source.begin(), source.end(), destination);
        destination[source.size()] = L'\0';
    }

    [[nodiscard]] constexpr bool is_ascii_drive_letter(wchar_t value) noexcept
    {
        return (value >= L'A' && value <= L'Z') ||
            (value >= L'a' && value <= L'z');
    }

    [[nodiscard]] constexpr wchar_t ascii_upper(wchar_t value) noexcept
    {
        return value >= L'a' && value <= L'z'
            ? static_cast<wchar_t>(value - (L'a' - L'A'))
            : value;
    }

    [[nodiscard]] bool equals_ascii_insensitive(
        std::wstring_view left,
        std::wstring_view right) noexcept
    {
        if (left.size() != right.size())
        {
            return false;
        }
        for (size_t index = 0; index < left.size(); ++index)
        {
            if (ascii_upper(left[index]) != ascii_upper(right[index]))
            {
                return false;
            }
        }
        return true;
    }

    [[nodiscard]] bool is_reserved_dos_component(
        std::wstring_view component) noexcept
    {
        const size_t extension = component.find(L'.');
        std::wstring_view basename = component.substr(0, extension);
        if (equals_ascii_insensitive(basename, L"CON") ||
            equals_ascii_insensitive(basename, L"PRN") ||
            equals_ascii_insensitive(basename, L"AUX") ||
            equals_ascii_insensitive(basename, L"NUL") ||
            equals_ascii_insensitive(basename, L"CLOCK$"))
        {
            return true;
        }
        return basename.size() == 4 &&
            (equals_ascii_insensitive(basename.substr(0, 3), L"COM") ||
             equals_ascii_insensitive(basename.substr(0, 3), L"LPT")) &&
            basename[3] >= L'1' && basename[3] <= L'9';
    }

    [[nodiscard]] std::wstring normalize_local_fixed_dos_path(
        std::wstring_view rawPath,
        const char* operation)
    {
        std::wstring path(rawPath);
        std::replace(path.begin(), path.end(), L'/', L'\\');
        if (path.size() < 3 ||
            !is_ascii_drive_letter(path[0]) ||
            path[1] != L':' ||
            path[2] != L'\\')
        {
            throw ptlsmr::win32_error(operation, ERROR_BAD_PATHNAME);
        }

        const wchar_t driveLetter = ascii_upper(path[0]);
        const DWORD driveBit = 1u << (driveLetter - L'A');
        if ((g_localFixedDriveMask & driveBit) == 0)
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DRIVE);
        }

        std::vector<std::wstring> components;
        size_t start = 3;
        while (start <= path.size())
        {
            const size_t end = path.find(L'\\', start);
            const std::wstring_view component(
                path.data() + start,
                (end == std::wstring::npos ? path.size() : end) - start);
            if (component.empty() || component == L".")
            {
                // Repeated separators and current-directory components are
                // normalized without resolving anything on the filesystem.
            }
            else if (component == L"..")
            {
                if (components.empty())
                {
                    throw ptlsmr::win32_error(operation, ERROR_ACCESS_DENIED);
                }
                components.pop_back();
            }
            else
            {
                if (component.back() == L'.' ||
                    component.back() == L' ' ||
                    component.find_first_of(L"<>:\"|?*") != std::wstring_view::npos ||
                    std::any_of(component.begin(), component.end(), [](wchar_t value) {
                        return value < L' ';
                    }) ||
                    is_reserved_dos_component(component))
                {
                    throw ptlsmr::win32_error(operation, ERROR_INVALID_NAME);
                }
                components.emplace_back(component);
            }
            if (end == std::wstring::npos)
            {
                break;
            }
            start = end + 1;
        }
        if (components.empty())
        {
            throw ptlsmr::win32_error(operation, ERROR_BAD_PATHNAME);
        }

        std::wstring normalized;
        normalized.reserve(path.size());
        normalized += driveLetter;
        normalized += L":\\";
        for (size_t index = 0; index < components.size(); ++index)
        {
            if (index != 0)
            {
                normalized += L'\\';
            }
            normalized += components[index];
        }
        return normalized;
    }

    void initialize_local_fixed_drive_mask()
    {
        const DWORD logicalDrives = GetLogicalDrives();
        if (logicalDrives == 0)
        {
            throw ptlsmr::win32_error(
                "GetLogicalDrives(host fixed-drive policy)",
                GetLastError());
        }
        DWORD fixedDrives = 0;
        for (DWORD index = 0; index < 26; ++index)
        {
            const DWORD bit = 1u << index;
            if ((logicalDrives & bit) == 0)
            {
                continue;
            }
            const wchar_t root[]{
                static_cast<wchar_t>(L'A' + index),
                L':',
                L'\\',
                L'\0',
            };
            if (GetDriveTypeW(root) == DRIVE_FIXED)
            {
                fixedDrives |= bit;
            }
        }
        if (fixedDrives == 0)
        {
            throw ptlsmr::win32_error(
                "host fixed-drive topology policy",
                ERROR_INVALID_DRIVE);
        }
        g_localFixedDriveMask = fixedDrives;
    }

    [[nodiscard]] ptlsmr::unique_handle open_pipe_client_token(HANDLE pipe)
    {
        pipe_client_impersonation_guard impersonation(pipe);
        HANDLE rawToken = nullptr;
        const BOOL opened = OpenThreadToken(
            GetCurrentThread(),
            TOKEN_QUERY,
            TRUE,
            &rawToken);
        const DWORD openError = opened ? ERROR_SUCCESS : GetLastError();
        ptlsmr::unique_handle token(rawToken);
        impersonation.revert();
        if (!opened)
        {
            throw ptlsmr::win32_error(
                "OpenThreadToken(host pipe caller)",
                openError);
        }
        return token;
    }

    void read_pipe_authentication_preface(HANDLE pipe)
    {
        ptlsmr::pipe_authentication_preface preface{};
        preface.magic = 0;
        preface.version = 0;
        DWORD transferred = 0;
        const auto result = perform_stop_aware_pipe_io(
            pipe,
            &preface,
            sizeof(preface),
            transferred,
            false);
        if (result != pipe_io_result::completed ||
            transferred != sizeof(preface) ||
            preface.magic != ptlsmr::PipeAuthenticationMagic ||
            preface.version != ptlsmr::ProtocolVersion ||
            preface.reserved != 0)
        {
            const DWORD error = result == pipe_io_result::timed_out
                ? ERROR_TIMEOUT
                : result == pipe_io_result::stopped
                    ? ERROR_OPERATION_ABORTED
                    : ERROR_ACCESS_DENIED;
            throw ptlsmr::win32_error(
                "host pipe authentication preface policy",
                error);
        }
    }

    template<typename Result, typename Operation>
    [[nodiscard]] std::optional<Result> reject_connection_failures(
        Operation&& operation)
    {
        try
        {
            return std::invoke(std::forward<Operation>(operation));
        }
        catch (const ptlsmr::win32_error&)
        {
            return std::nullopt;
        }
        catch (const std::filesystem::filesystem_error&)
        {
            return std::nullopt;
        }
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
            throw ptlsmr::win32_error("GetModuleFileNameW(host)", GetLastError());
        }
        path.resize(characters);
        return std::filesystem::weakly_canonical(std::filesystem::path(path));
    }

    [[nodiscard]] bool equal_path(
        const std::filesystem::path& left,
        const std::filesystem::path& right)
    {
        return CompareStringOrdinal(
                   std::filesystem::weakly_canonical(left).c_str(),
                   -1,
                   std::filesystem::weakly_canonical(right).c_str(),
                   -1,
                   TRUE) == CSTR_EQUAL;
    }

    void report_status(DWORD state, DWORD error = ERROR_SUCCESS)
    {
        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = error;
        g_status.dwServiceSpecificExitCode = 0;
        g_status.dwControlsAccepted =
            state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        g_status.dwCheckPoint = state == SERVICE_START_PENDING ? 1 : 0;
        g_status.dwWaitHint = state == SERVICE_START_PENDING ? 10000 : 0;
        if (g_statusHandle)
        {
            SetServiceStatus(g_statusHandle, &g_status);
        }
    }

    [[nodiscard]] registry_key open_endpoint_registry_key(REGSAM access)
    {
        HKEY raw = nullptr;
        const LSTATUS status = RegCreateKeyExW(
            HKEY_LOCAL_MACHINE,
            ptlsmr::ControlPlaneRegistryKey,
            0,
            nullptr,
            REG_OPTION_NON_VOLATILE,
            access | KEY_WOW64_64KEY,
            nullptr,
            &raw,
            nullptr);
        if (status != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegCreateKeyExW(host endpoint)", status);
        }
        return registry_key(raw);
    }

    void protect_endpoint_registry_key(HKEY key)
    {
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                L"O:SYG:SYD:P(A;;KA;;;SY)(A;;KA;;;BA)(A;;KR;;;AU)",
                SDDL_REVISION_1,
                &descriptor,
                nullptr))
        {
            throw ptlsmr::win32_error(
                "ConvertStringSecurityDescriptorToSecurityDescriptorW(host endpoint registry)",
                GetLastError());
        }
        ptlsmr::local_memory security(descriptor);
        const DWORD status = RegSetKeySecurity(
            key,
            DACL_SECURITY_INFORMATION |
                PROTECTED_DACL_SECURITY_INFORMATION,
            descriptor);
        if (status != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegSetKeySecurity(host endpoint)", status);
        }
    }

    void clear_published_endpoint()
    {
        auto key = open_endpoint_registry_key(KEY_QUERY_VALUE | KEY_SET_VALUE | WRITE_DAC);
        protect_endpoint_registry_key(key.get());
        const LSTATUS status = RegDeleteValueW(
            key.get(),
            ptlsmr::HostEndpointRegistryValue);
        if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND)
        {
            throw ptlsmr::win32_error("RegDeleteValueW(host endpoint)", status);
        }
        const LSTATUS flush = RegFlushKey(key.get());
        if (flush != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegFlushKey(clear host endpoint)", flush);
        }
        g_publishedEndpoint.clear();
    }

    void clear_published_endpoint_noexcept() noexcept
    {
        try
        {
            clear_published_endpoint();
        }
        catch (...)
        {
        }
    }

    void publish_endpoint(std::wstring_view endpoint)
    {
        auto key = open_endpoint_registry_key(KEY_QUERY_VALUE | KEY_SET_VALUE | WRITE_DAC);
        protect_endpoint_registry_key(key.get());
        const DWORD bytes = static_cast<DWORD>((endpoint.size() + 1) * sizeof(wchar_t));
        const LSTATUS status = RegSetValueExW(
            key.get(),
            ptlsmr::HostEndpointRegistryValue,
            0,
            REG_SZ,
            reinterpret_cast<const BYTE*>(endpoint.data()),
            bytes);
        if (status != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegSetValueExW(host endpoint)", status);
        }
        const LSTATUS flush = RegFlushKey(key.get());
        if (flush != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegFlushKey(publish host endpoint)", flush);
        }
        g_publishedEndpoint.assign(endpoint);
    }

    [[nodiscard]] std::map<std::wstring, std::wstring> parse_exact_fields(
        std::wstring_view value,
        std::initializer_list<std::wstring_view> allowed,
        std::initializer_list<std::wstring_view> required,
        const char* operation)
    {
        std::map<std::wstring, std::wstring> fields;
        size_t start = 0;
        while (start < value.size())
        {
            const size_t end = value.find_first_of(L"\r\n", start);
            const auto line = value.substr(
                start,
                (end == std::wstring_view::npos ? value.size() : end) - start);
            if (!line.empty())
            {
                const size_t separator = line.find(L'=');
                if (separator == std::wstring_view::npos || separator == 0 ||
                    separator == line.size() - 1 || line.find(L'=', separator + 1) != std::wstring_view::npos)
                {
                    throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
                }
                const std::wstring name(line.substr(0, separator));
                const std::wstring fieldValue(line.substr(separator + 1));
                const bool known = std::any_of(
                    allowed.begin(),
                    allowed.end(),
                    [&](std::wstring_view candidate) { return candidate == name; });
                if (!known || !fields.emplace(name, fieldValue).second)
                {
                    throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
                }
            }
            if (end == std::wstring_view::npos)
            {
                break;
            }
            start = end + 1;
            if (value[end] == L'\r' && start < value.size() && value[start] == L'\n')
            {
                ++start;
            }
        }
        for (const auto name : required)
        {
            if (!fields.contains(std::wstring(name)))
            {
                throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
            }
        }
        return fields;
    }

    [[nodiscard]] std::map<std::wstring, std::wstring> parse_exact_fields_allow_empty(
        std::wstring_view value,
        std::initializer_list<std::wstring_view> allowed,
        std::initializer_list<std::wstring_view> required,
        const char* operation)
    {
        std::map<std::wstring, std::wstring> fields;
        size_t start = 0;
        while (start < value.size())
        {
            const size_t end = value.find_first_of(L"\r\n", start);
            const auto line = value.substr(
                start,
                (end == std::wstring_view::npos ? value.size() : end) - start);
            if (!line.empty())
            {
                const size_t separator = line.find(L'=');
                if (separator == std::wstring_view::npos || separator == 0 ||
                    line.find(L'=', separator + 1) != std::wstring_view::npos)
                {
                    throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
                }
                const std::wstring name(line.substr(0, separator));
                const bool known = std::any_of(
                    allowed.begin(),
                    allowed.end(),
                    [&](std::wstring_view candidate) { return candidate == name; });
                if (!known ||
                    !fields.emplace(name, std::wstring(line.substr(separator + 1))).second)
                {
                    throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
                }
            }
            if (end == std::wstring_view::npos)
            {
                break;
            }
            start = end + 1;
            if (value[end] == L'\r' && start < value.size() && value[start] == L'\n')
            {
                ++start;
            }
        }
        for (const auto name : required)
        {
            if (!fields.contains(std::wstring(name)))
            {
                throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
            }
        }
        return fields;
    }

    void validate_installed_policy()
    {
        const std::wstring codePin = ptlsmr::read_code_signer_pin();
        (void)ptlsmr::validate_policy_candidate(
            ptlsmr::code_policy_path(),
            ptlsmr::CodePolicyExe,
            codePin);
        const auto codeFields = parse_exact_fields(
            ptlsmr::read_rcdata_text(
                ptlsmr::code_policy_path(),
                L"PTPUVR_POLICY",
                1024),
            { L"schemaVersion", L"kind", L"codeSignerSha256" },
            { L"schemaVersion", L"kind", L"codeSignerSha256" },
            "code policy RCDATA");
        if (codeFields.at(L"schemaVersion") != L"1" ||
            codeFields.at(L"kind") != L"code" ||
            ptlsmr::canonical_signer_sha256(codeFields.at(L"codeSignerSha256")) != codePin)
        {
            throw ptlsmr::win32_error("code policy identity", ERROR_INVALID_DATA);
        }

        (void)ptlsmr::validate_policy_candidate(
            ptlsmr::metadata_policy_path(),
            ptlsmr::MetadataPolicyExe,
            codePin);
        const auto metadataFields = parse_exact_fields(
            ptlsmr::read_rcdata_text(
                ptlsmr::metadata_policy_path(),
                L"PTPUVR_POLICY",
                1024),
            { L"schemaVersion", L"kind", L"metadataSignerSha256" },
            { L"schemaVersion", L"kind", L"metadataSignerSha256" },
            "metadata policy RCDATA");
        if (metadataFields.at(L"schemaVersion") != L"1" ||
            metadataFields.at(L"kind") != L"metadata" ||
            ptlsmr::canonical_signer_sha256(metadataFields.at(L"metadataSignerSha256")) !=
                ptlsmr::read_metadata_signer_pin())
        {
            throw ptlsmr::win32_error("metadata policy identity", ERROR_INVALID_DATA);
        }
    }

    [[nodiscard]] ptlsmr::file_version read_active_engine_version()
    {
        return ptlsmr::parse_version(ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
    }

    void write_active_engine_version(const ptlsmr::file_version& version)
    {
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::engine_state_path(),
            ptlsmr::format_version(version));
    }

    [[nodiscard]] std::filesystem::path engine_floor_path()
    {
        return ptlsmr::program_data_root() / L"engine-version-floor.txt";
    }

    [[nodiscard]] ptlsmr::file_version read_engine_floor()
    {
        return ptlsmr::parse_version(ptlsmr::read_utf8_file(engine_floor_path(), 64));
    }

    void write_engine_floor(const ptlsmr::file_version& version)
    {
        const auto existing = read_engine_floor();
        if (version < existing)
        {
            throw ptlsmr::win32_error("engine version floor regression", ERROR_REVISION_MISMATCH);
        }
        ptlsmr::write_utf8_file_atomic(engine_floor_path(), ptlsmr::format_version(version));
    }

    void write_activation_journal(const activation_journal& journal)
    {
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::engine_activation_journal_path(),
            L"previous=" + ptlsmr::format_version(journal.previous) + L"\r\n" +
                L"candidate=" + ptlsmr::format_version(journal.candidate) + L"\r\n" +
                L"phase=" + journal.phase + L"\r\n");
    }

    [[nodiscard]] activation_journal read_activation_journal(
        const std::filesystem::path& path = ptlsmr::engine_activation_journal_path())
    {
        const auto fields = parse_exact_fields(
            ptlsmr::read_utf8_file(path, 512),
            { L"previous", L"candidate", L"phase" },
            { L"previous", L"candidate", L"phase" },
            "engine activation journal");
        if (fields.at(L"phase") != L"prepared" && fields.at(L"phase") != L"active-switched")
        {
            throw ptlsmr::win32_error("engine activation journal phase", ERROR_INVALID_DATA);
        }
        return {
            ptlsmr::parse_version(fields.at(L"previous")),
            ptlsmr::parse_version(fields.at(L"candidate")),
            fields.at(L"phase"),
        };
    }

    void clear_activation_journal()
    {
        const auto path = ptlsmr::engine_activation_journal_path();
        if (std::filesystem::exists(path) && !DeleteFileW(path.c_str()))
        {
            throw ptlsmr::win32_error("DeleteFileW(engine activation journal)", GetLastError());
        }
    }

    void remove_unactivated_engine(const ptlsmr::file_version& version)
    {
        const auto directory = ptlsmr::engine_install_directory(version);
        if (std::filesystem::exists(directory))
        {
            std::filesystem::remove_all(directory);
        }
    }

    void recover_engine_activation()
    {
        const auto journalPath = ptlsmr::engine_activation_journal_path();
        const std::filesystem::path replacement = journalPath.wstring() + L".new";
        if (std::filesystem::exists(journalPath))
        {
            if (std::filesystem::exists(replacement) &&
                !DeleteFileW(replacement.c_str()))
            {
                throw ptlsmr::win32_error(
                    "DeleteFileW(stale engine activation replacement)",
                    GetLastError());
            }
        }
        else if (std::filesystem::exists(replacement))
        {
            (void)read_activation_journal(replacement);
            ptlsmr::check_bool(
                MoveFileExW(
                    replacement.c_str(),
                    journalPath.c_str(),
                    MOVEFILE_WRITE_THROUGH),
                "MoveFileExW(recover engine activation replacement)");
        }
        if (!std::filesystem::exists(journalPath))
        {
            return;
        }
        const auto journal = read_activation_journal();
        const auto active = read_active_engine_version();
        const auto candidatePath = ptlsmr::engine_executable_path(journal.candidate);
        const std::wstring codePin = ptlsmr::read_code_signer_pin();
        if (active == journal.candidate)
        {
            (void)ptlsmr::validate_engine_candidate(candidatePath, codePin);
            write_engine_floor(journal.candidate);
            clear_activation_journal();
            return;
        }
        if (active == journal.previous && journal.phase == L"prepared")
        {
            remove_unactivated_engine(journal.candidate);
            clear_activation_journal();
            return;
        }
        throw ptlsmr::win32_error("engine activation recovery state", ERROR_INVALID_DATA);
    }

    void write_binary_atomic(
        const std::filesystem::path& path,
        const void* bytes,
        size_t length)
    {
        const std::filesystem::path temporary = path.wstring() + L".new";
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
            throw ptlsmr::win32_error("CreateFileW(protected engine request)", GetLastError());
        }
        DWORD written = 0;
        ptlsmr::check_bool(
            WriteFile(file.get(), bytes, static_cast<DWORD>(length), &written, nullptr) &&
                written == length,
            "WriteFile(protected engine request)");
        ptlsmr::check_bool(FlushFileBuffers(file.get()), "FlushFileBuffers(protected engine request)");
        file.reset();
        ptlsmr::check_bool(
            MoveFileExW(
                temporary.c_str(),
                path.c_str(),
                MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH),
            "MoveFileExW(protected engine request)");
    }

    template<typename T>
    [[nodiscard]] T read_fixed_binary(const std::filesystem::path& path, const char* operation)
    {
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
            throw ptlsmr::win32_error(operation, GetLastError());
        }
        LARGE_INTEGER size{};
        ptlsmr::check_bool(GetFileSizeEx(file.get(), &size), operation);
        if (size.QuadPart != sizeof(T))
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
        }
        T value{};
        DWORD read = 0;
        ptlsmr::check_bool(
            ReadFile(file.get(), &value, sizeof(value), &read, nullptr) && read == sizeof(value),
            operation);
        return value;
    }

    [[nodiscard]] DWORD host_service_pid()
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(host PID)", GetLastError());
        }
        service_handle service(OpenServiceW(
            scm.get(),
            ptlsmr::HostServiceName,
            SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(host PID)", GetLastError());
        }
        SERVICE_STATUS_PROCESS status{};
        DWORD bytes = 0;
        ptlsmr::check_bool(
            QueryServiceStatusEx(
                service.get(),
                SC_STATUS_PROCESS_INFO,
                reinterpret_cast<BYTE*>(&status),
                sizeof(status),
                &bytes),
            "QueryServiceStatusEx(host PID)");
        if (status.dwCurrentState != SERVICE_RUNNING || status.dwProcessId == 0)
        {
            throw ptlsmr::win32_error("host service running policy", ERROR_SERVICE_NOT_ACTIVE);
        }
        return status.dwProcessId;
    }

    void cancel_and_reap_pending_io(HANDLE pipe, OVERLAPPED* operation)
    {
        if (!CancelIoEx(pipe, operation))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_NOT_FOUND)
            {
                throw ptlsmr::win32_error("CancelIoEx(host pipe operation)", error);
            }
        }
        DWORD transferred = 0;
        if (!GetOverlappedResult(pipe, operation, &transferred, TRUE) &&
            !is_expected_pipe_disconnect(GetLastError()))
        {
            throw ptlsmr::win32_error("GetOverlappedResult(host pipe cancellation)", GetLastError());
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
                writeOperation ? "WriteFile(host pipe)" : "ReadFile(host pipe)",
                initialError);
        }
        const HANDLE waits[] = { g_stopEvent.get(), operation.event() };
        const DWORD wait = WaitForMultipleObjects(
            ARRAYSIZE(waits),
            waits,
            FALSE,
            PipeIoTimeoutMilliseconds);
        if (wait == WAIT_OBJECT_0)
        {
            cancel_and_reap_pending_io(pipe, operation.get());
            return pipe_io_result::stopped;
        }
        if (wait == WAIT_TIMEOUT)
        {
            cancel_and_reap_pending_io(pipe, operation.get());
            return pipe_io_result::timed_out;
        }
        if (wait != WAIT_OBJECT_0 + 1)
        {
            const DWORD error = wait == WAIT_FAILED ? GetLastError() : ERROR_GEN_FAILURE;
            cancel_and_reap_pending_io(pipe, operation.get());
            throw ptlsmr::win32_error("WaitForMultipleObjects(host pipe I/O)", error);
        }
        if (!GetOverlappedResult(pipe, operation.get(), &transferred, FALSE))
        {
            const DWORD error = GetLastError();
            if (is_expected_pipe_disconnect(error))
            {
                return pipe_io_result::disconnected;
            }
            throw ptlsmr::win32_error(
                writeOperation ? "GetOverlappedResult(host pipe write)" :
                    "GetOverlappedResult(host pipe read)",
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
            throw ptlsmr::win32_error("ConnectNamedPipe(host)", initialError);
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
            throw ptlsmr::win32_error("WaitForMultipleObjects(host connect)", error);
        }
        DWORD transferred = 0;
        if (!GetOverlappedResult(pipe, operation.get(), &transferred, FALSE))
        {
            const DWORD error = GetLastError();
            if (is_expected_pipe_disconnect(error))
            {
                return pipe_io_result::disconnected;
            }
            throw ptlsmr::win32_error("GetOverlappedResult(host connect)", error);
        }
        return pipe_io_result::completed;
    }

    [[nodiscard]] bool valid_release_id(std::wstring_view value)
    {
        if (value.size() < 11 || value.size() >= ptlsmr::MaxReleaseIdChars ||
            !value.starts_with(L"release-"))
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
                continue;
            }
            if ((character >= L'a' && character <= L'z') || character == L'-')
            {
                continue;
            }
            return false;
        }
        return digitSeen && value.back() != L'-';
    }

    void cleanup_abandoned_release_stages()
    {
        const auto stagingRoot = ptlsmr::installation_root() / L"Staging";
        if (!std::filesystem::is_directory(stagingRoot))
        {
            return;
        }
        for (const auto& entry : std::filesystem::directory_iterator(stagingRoot))
        {
            const auto name = entry.path().filename().wstring();
            constexpr std::wstring_view prefix = L"release-";
            if (!name.starts_with(prefix) || name.size() != prefix.size() + 32 ||
                !std::all_of(name.begin() + prefix.size(), name.end(), [](wchar_t character) {
                    return (character >= L'0' && character <= L'9') ||
                        (character >= L'a' && character <= L'f');
                }))
            {
                continue;
            }
            const DWORD attributes = GetFileAttributesW(entry.path().c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES)
            {
                throw ptlsmr::win32_error(
                    "GetFileAttributesW(abandoned release stage)",
                    GetLastError());
            }
            if ((attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
                (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                throw ptlsmr::win32_error(
                    "abandoned release stage type policy",
                    ERROR_REPARSE_TAG_INVALID);
            }
            std::error_code error;
            for (size_t attempt = 0; attempt != 40; ++attempt)
            {
                error.clear();
                std::filesystem::remove_all(entry.path(), error);
                if (!error)
                {
                    break;
                }
                if (error.value() != ERROR_SHARING_VIOLATION &&
                    error.value() != ERROR_DIR_NOT_EMPTY)
                {
                    break;
                }
                Sleep(50);
            }
            if (error)
            {
                throw ptlsmr::win32_error(
                    "abandoned release stage cleanup",
                    static_cast<DWORD>(error.value()));
            }
        }
    }

    [[nodiscard]] caller_identity identify_caller(HANDLE pipe)
    {
        DWORD clientPid = 0;
        ptlsmr::check_bool(
            GetNamedPipeClientProcessId(pipe, &clientPid),
            "GetNamedPipeClientProcessId(host)");
        if (clientPid == 0 || clientPid == GetCurrentProcessId())
        {
            throw ptlsmr::win32_error("host pipe client process policy", ERROR_ACCESS_DENIED);
        }
        ptlsmr::unique_handle process(OpenProcess(
            PROCESS_QUERY_LIMITED_INFORMATION | SYNCHRONIZE,
            FALSE,
            clientPid));
        if (!process)
        {
            throw ptlsmr::win32_error("OpenProcess(host caller)", GetLastError());
        }
        const auto actualClient = normalize_local_fixed_dos_path(
            ptlsmr::raw_process_image_path(process.get()).wstring(),
            "host caller raw DOS image path policy");
        HANDLE rawProcessToken = nullptr;
        ptlsmr::check_bool(
            OpenProcessToken(
                process.get(),
                TOKEN_QUERY | TOKEN_DUPLICATE,
                &rawProcessToken),
            "OpenProcessToken(host caller)");
        ptlsmr::unique_handle processToken(rawProcessToken);

        DWORD confirmedClientPid = 0;
        ptlsmr::check_bool(
            GetNamedPipeClientProcessId(pipe, &confirmedClientPid),
            "GetNamedPipeClientProcessId(host confirmation)");
        DWORD pipeSessionId = 0;
        DWORD processSessionId = 0;
        ptlsmr::check_bool(
            GetNamedPipeClientSessionId(pipe, &pipeSessionId),
            "GetNamedPipeClientSessionId(host)");
        ptlsmr::check_bool(
            ProcessIdToSessionId(clientPid, &processSessionId),
            "ProcessIdToSessionId(host caller)");
        const DWORD processWait = WaitForSingleObject(process.get(), 0);
        if (confirmedClientPid != clientPid ||
            pipeSessionId != processSessionId ||
            processWait == WAIT_OBJECT_0)
        {
            throw ptlsmr::win32_error(
                "host pipe client process binding",
                ERROR_ACCESS_DENIED);
        }
        if (processWait == WAIT_FAILED)
        {
            throw ptlsmr::win32_error(
                "WaitForSingleObject(host caller)",
                GetLastError());
        }

        const auto processOwnerSid = ptlsmr::canonical_owner_sid(
            ptlsmr::current_token_user_sid(processToken.get()));

        caller_identity caller;
        caller.processId = clientPid;
        caller.ownerSid = processOwnerSid;
        caller.imagePath = actualClient;
        caller.process = std::move(process);
        caller.processToken = std::move(processToken);
        return caller;
    }

    void bind_pipe_token_to_caller(
        HANDLE pipe,
        const caller_identity& identity)
    {
        // Windows only exposes a named-pipe impersonation token after the
        // server consumes a client message. The process-token SID quota is
        // already held before this fixed, path-free preface is read.
        read_pipe_authentication_preface(pipe);
        auto pipeToken = open_pipe_client_token(pipe);
        const auto pipeOwnerSid = ptlsmr::canonical_owner_sid(
            ptlsmr::current_token_user_sid(pipeToken.get()));
        if (identity.ownerSid != pipeOwnerSid)
        {
            throw ptlsmr::win32_error(
                "host process and pipe token SID binding",
                ERROR_ACCESS_DENIED);
        }
    }

    [[nodiscard]] caller_context authorize_caller(
        const caller_identity& identity)
    {
        // The explicit token plus KF_FLAG_DONT_VERIFY resolves metadata without
        // probing or creating the caller-controlled directory.
        const auto localAppData = normalize_local_fixed_dos_path(
            ptlsmr::token_local_app_data(identity.processToken.get()).wstring(),
            "host caller LocalAppData path policy");
        caller_context caller;
        caller.processId = identity.processId;
        caller.ownerSid = identity.ownerSid;
        caller.inboxRoot = std::filesystem::path(localAppData) /
            L"Microsoft\\PowerToys\\WorkspacesControlPlanePrototype\\ReleaseInbox";
        return caller;
    }

    void throw_if_stopping()
    {
        if (WaitForSingleObject(g_stopEvent.get(), 0) == WAIT_OBJECT_0)
        {
            throw ptlsmr::win32_error("host operation stopped", ERROR_OPERATION_ABORTED);
        }
    }

    [[nodiscard]] ptlsmr::unique_handle create_kill_on_close_job()
    {
        ptlsmr::unique_handle job(CreateJobObjectW(nullptr, nullptr));
        if (!job)
        {
            throw ptlsmr::win32_error("CreateJobObjectW(engine child)", GetLastError());
        }
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION limits{};
        limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        ptlsmr::check_bool(
            SetInformationJobObject(
                job.get(),
                JobObjectExtendedLimitInformation,
                &limits,
                sizeof(limits)),
            "SetInformationJobObject(engine child kill-on-close)");
        return job;
    }

    [[nodiscard]] job_child launch_job_child(
        const std::filesystem::path& executable,
        std::wstring& commandLine,
        const std::filesystem::path& workingDirectory,
        HANDLE diagnosticHandle)
    {
        auto job = create_kill_on_close_job();
        STARTUPINFOEXW startup{};
        startup.StartupInfo.cb = sizeof(startup);
        std::unique_ptr<process_attribute_list> attributes;
        BOOL inheritHandles = FALSE;
        DWORD creationFlags = CREATE_NO_WINDOW | CREATE_SUSPENDED;
        if (diagnosticHandle != nullptr && diagnosticHandle != INVALID_HANDLE_VALUE)
        {
            attributes = std::make_unique<process_attribute_list>(diagnosticHandle);
            startup.lpAttributeList = attributes->get();
            startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
            startup.StartupInfo.hStdInput = diagnosticHandle;
            startup.StartupInfo.hStdOutput = diagnosticHandle;
            startup.StartupInfo.hStdError = diagnosticHandle;
            inheritHandles = TRUE;
            creationFlags |= EXTENDED_STARTUPINFO_PRESENT;
        }

        PROCESS_INFORMATION process{};
        ptlsmr::check_bool(
            CreateProcessW(
                executable.c_str(),
                commandLine.data(),
                nullptr,
                nullptr,
                inheritHandles,
                creationFlags,
                nullptr,
                workingDirectory.c_str(),
                &startup.StartupInfo,
                &process),
            "CreateProcessW(job-controlled engine child)");
        ptlsmr::unique_handle processHandle(process.hProcess);
        ptlsmr::unique_handle threadHandle(process.hThread);
        try
        {
            ptlsmr::check_bool(
                AssignProcessToJobObject(job.get(), processHandle.get()),
                "AssignProcessToJobObject(engine child)");
            const DWORD resume = ResumeThread(threadHandle.get());
            if (resume == MAXDWORD)
            {
                throw ptlsmr::win32_error("ResumeThread(engine child)", GetLastError());
            }
        }
        catch (...)
        {
            const auto failure = std::current_exception();
            (void)TerminateProcess(processHandle.get(), ERROR_OPERATION_ABORTED);
            (void)WaitForSingleObject(processHandle.get(), 30000);
            std::rethrow_exception(failure);
        }
        return { std::move(job), std::move(processHandle), process.dwProcessId };
    }

    void terminate_and_reap_child(
        job_child& child,
        DWORD exitCode,
        const char* terminateOperation,
        const char* waitOperation)
    {
        if (WaitForSingleObject(child.process.get(), 0) != WAIT_OBJECT_0)
        {
            ptlsmr::check_bool(
                TerminateJobObject(child.job.get(), exitCode),
                terminateOperation);
        }
        const DWORD reaped = WaitForSingleObject(child.process.get(), 30000);
        if (reaped != WAIT_OBJECT_0)
        {
            throw ptlsmr::win32_error(
                waitOperation,
                reaped == WAIT_TIMEOUT ? ERROR_TIMEOUT : GetLastError());
        }
    }

    void wait_for_child(job_child& child, DWORD timeout, const char* operation)
    {
        const HANDLE waits[]{ g_stopEvent.get(), child.process.get() };
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(waits), waits, FALSE, timeout);
        if (wait == WAIT_OBJECT_0)
        {
            terminate_and_reap_child(
                child,
                ERROR_OPERATION_ABORTED,
                "TerminateJobObject(stopped engine child)",
                "WaitForSingleObject(stopped engine child)");
            throw ptlsmr::win32_error(operation, ERROR_OPERATION_ABORTED);
        }
        if (wait == WAIT_TIMEOUT)
        {
            terminate_and_reap_child(
                child,
                ERROR_TIMEOUT,
                "TerminateJobObject(timed-out engine child)",
                "WaitForSingleObject(timed-out engine child)");
            throw ptlsmr::win32_error(operation, ERROR_TIMEOUT);
        }
        if (wait != WAIT_OBJECT_0 + 1)
        {
            const DWORD error = wait == WAIT_FAILED ? GetLastError() : ERROR_GEN_FAILURE;
            terminate_and_reap_child(
                child,
                error,
                "TerminateJobObject(failed engine wait)",
                "WaitForSingleObject(failed engine wait)");
            throw ptlsmr::win32_error(operation, error);
        }
    }

    [[nodiscard]] ptlsmr::engine_reply invoke_engine(
        const ptlsmr::engine_request& request,
        const std::filesystem::path& requestPath,
        const std::filesystem::path& responsePath,
        const std::filesystem::path& diagnosticPath)
    {
        const auto active = read_active_engine_version();
        const auto engine = ptlsmr::engine_executable_path(active);
        const std::wstring codePin = ptlsmr::read_code_signer_pin();
        (void)ptlsmr::validate_engine_candidate(engine, codePin);
        if (std::filesystem::exists(responsePath))
        {
            std::filesystem::remove(responsePath);
        }
        if (std::filesystem::exists(diagnosticPath))
        {
            std::filesystem::remove(diagnosticPath);
        }
        SECURITY_ATTRIBUTES inheritableAttributes{
            sizeof(inheritableAttributes),
            nullptr,
            TRUE,
        };
        ptlsmr::unique_handle diagnostic(CreateFileW(
            diagnosticPath.c_str(),
            GENERIC_WRITE,
            FILE_SHARE_READ,
            &inheritableAttributes,
            CREATE_NEW,
            FILE_ATTRIBUTE_NORMAL | FILE_FLAG_WRITE_THROUGH,
            nullptr));
        if (!diagnostic)
        {
            throw ptlsmr::win32_error("CreateFileW(engine diagnostic)", GetLastError());
        }
        std::wstring commandLine = ptlsmr::quote_argument(engine.wstring()) +
            L" --engine-request " + ptlsmr::quote_argument(requestPath.wstring()) +
            L" --engine-response " + ptlsmr::quote_argument(responsePath.wstring()) +
            L" --host-pid " + std::to_wstring(GetCurrentProcessId()) +
            L" --request-command " + std::to_wstring(request.command);
        auto child = launch_job_child(
            engine,
            commandLine,
            engine.parent_path(),
            diagnostic.get());
        wait_for_child(
            child,
            ChildTimeoutMilliseconds,
            "WaitForMultipleObjects(active engine)");
        DWORD exitCode = ERROR_GEN_FAILURE;
        ptlsmr::check_bool(
            GetExitCodeProcess(child.process.get(), &exitCode),
            "GetExitCodeProcess(active engine)");
        diagnostic.reset();
        if (!std::filesystem::is_regular_file(responsePath))
        {
            ptlsmr::engine_reply failure{};
            failure.command = request.command;
            failure.win32Status =
                exitCode == ERROR_SUCCESS ? ERROR_INVALID_DATA : exitCode;
            try
            {
                const auto diagnosticText = ptlsmr::read_utf8_file(diagnosticPath, 2048);
                copy_bounded(
                    failure.detail,
                    ARRAYSIZE(failure.detail),
                    diagnosticText.empty()
                        ? L"engine exited without a protected response"
                        : diagnosticText);
            }
            catch (...)
            {
                copy_bounded(
                    failure.detail,
                    ARRAYSIZE(failure.detail),
                    L"engine exited without a readable protected response");
            }
            return failure;
        }
        const auto reply = read_fixed_binary<ptlsmr::engine_reply>(
            responsePath,
            "ReadFile(engine protected response)");
        if (reply.magic != ptlsmr::ProtocolMagic || reply.version != ptlsmr::ProtocolVersion ||
            reply.command != request.command ||
            reply.candidateEnginePath[ARRAYSIZE(reply.candidateEnginePath) - 1] != L'\0' ||
            reply.detail[ARRAYSIZE(reply.detail) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("engine protected response protocol", ERROR_INVALID_DATA);
        }
        if (exitCode != ERROR_SUCCESS && reply.win32Status == ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("engine process exit policy", exitCode);
        }
        return reply;
    }

    void run_engine_self_test(
        const std::filesystem::path& candidate,
        const ptlsmr::file_version& version)
    {
        std::wstring commandLine = ptlsmr::quote_argument(candidate.wstring()) +
            L" --self-test --host-pid " + std::to_wstring(GetCurrentProcessId()) +
            L" --candidate-version " + ptlsmr::format_version(version);
        auto child = launch_job_child(
            candidate,
            commandLine,
            candidate.parent_path(),
            nullptr);
        wait_for_child(
            child,
            ChildTimeoutMilliseconds,
            "WaitForMultipleObjects(engine qualification)");
        DWORD exitCode = ERROR_GEN_FAILURE;
        ptlsmr::check_bool(
            GetExitCodeProcess(child.process.get(), &exitCode),
            "GetExitCodeProcess(engine qualification)");
        if (exitCode != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("engine qualification readiness", exitCode);
        }
    }

    void write_host_evidence();

    void activate_engine(const ptlsmr::engine_reply& reply)
    {
        if (reply.candidateEngineVersion[0] == L'\0' ||
            reply.candidateEnginePath[0] == L'\0')
        {
            throw ptlsmr::win32_error("engine activation response fields", ERROR_INVALID_DATA);
        }
        const auto candidateVersion = ptlsmr::parse_version(reply.candidateEngineVersion);
        const auto candidate = std::filesystem::path(reply.candidateEnginePath);
        const auto expected = ptlsmr::engine_executable_path(candidateVersion);
        if (!ptlsmr::path_is_within(candidate, ptlsmr::engine_root()) ||
            !equal_path(candidate, expected))
        {
            throw ptlsmr::win32_error("engine activation protected candidate path", ERROR_ACCESS_DENIED);
        }
        const std::wstring codePin = ptlsmr::read_code_signer_pin();
        const auto validatedVersion = ptlsmr::validate_engine_candidate(candidate, codePin);
        if (!(validatedVersion == candidateVersion) || candidateVersion < read_engine_floor())
        {
            throw ptlsmr::win32_error("engine activation version floor", ERROR_REVISION_MISMATCH);
        }
        const auto previous = read_active_engine_version();
        if (!(previous < candidateVersion))
        {
            throw ptlsmr::win32_error("engine activation monotonic version", ERROR_REVISION_MISMATCH);
        }

        activation_journal journal{ previous, candidateVersion, L"prepared" };
        write_activation_journal(journal);
        try
        {
            run_engine_self_test(candidate, candidateVersion);
        }
        catch (const ptlsmr::win32_error& error)
        {
            if (error.code() == ERROR_OPERATION_ABORTED &&
                WaitForSingleObject(g_stopEvent.get(), 0) == WAIT_OBJECT_0)
            {
                throw;
            }
            remove_unactivated_engine(candidateVersion);
            clear_activation_journal();
            throw;
        }
        catch (...)
        {
            remove_unactivated_engine(candidateVersion);
            clear_activation_journal();
            throw;
        }
        if (std::wstring_view(reply.engineCrashPhase) == L"before-active-switch")
        {
            TerminateProcess(GetCurrentProcess(), ERROR_PROCESS_ABORTED);
        }
        if (reply.engineCrashPhase[0] != L'\0' &&
            std::wstring_view(reply.engineCrashPhase) != L"after-active-switch-before-journal-clear")
        {
            throw ptlsmr::win32_error("engine activation crash phase policy", ERROR_INVALID_DATA);
        }
        write_active_engine_version(candidateVersion);
        journal.phase = L"active-switched";
        write_activation_journal(journal);
        if (std::wstring_view(reply.engineCrashPhase) == L"after-active-switch-before-journal-clear")
        {
            TerminateProcess(GetCurrentProcess(), ERROR_PROCESS_ABORTED);
        }
        write_engine_floor(candidateVersion);
        clear_activation_journal();
    }

    void remove_protected_request_directory(const std::filesystem::path& directory)
    {
        if (!ptlsmr::path_is_within(directory, ptlsmr::requests_root()))
        {
            throw ptlsmr::win32_error(
                "protected request cleanup path policy",
                ERROR_ACCESS_DENIED);
        }
        std::error_code error;
        for (size_t attempt = 0; attempt != 40; ++attempt)
        {
            error.clear();
            std::filesystem::remove_all(directory, error);
            if (!error)
            {
                return;
            }
            if (error.value() != ERROR_SHARING_VIOLATION &&
                error.value() != ERROR_DIR_NOT_EMPTY)
            {
                throw ptlsmr::win32_error(
                    "protected request cleanup",
                    static_cast<DWORD>(error.value()));
            }
            Sleep(50);
        }
        throw ptlsmr::win32_error(
            "protected request cleanup retry exhaustion",
            static_cast<DWORD>(error.value()));
    }

    [[nodiscard]] ptlsmr::public_reply dispatch_request(
        const caller_context& caller,
        const ptlsmr::public_request& input)
    {
        if (input.magic != ptlsmr::ProtocolMagic ||
            input.version != ptlsmr::ProtocolVersion ||
            input.reserved != 0 ||
            input.releaseId[ARRAYSIZE(input.releaseId) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("public control-plane request protocol", ERROR_INVALID_DATA);
        }
        const auto operation = static_cast<ptlsmr::public_command>(input.command);
        const std::wstring releaseId(input.releaseId);
        if ((operation == ptlsmr::public_command::acquire && !valid_release_id(releaseId)) ||
            ((operation == ptlsmr::public_command::status ||
              operation == ptlsmr::public_command::release) &&
                !releaseId.empty()))
        {
            throw ptlsmr::win32_error("public control-plane request shape", ERROR_INVALID_PARAMETER);
        }
        if (operation != ptlsmr::public_command::acquire &&
            operation != ptlsmr::public_command::status &&
            operation != ptlsmr::public_command::release)
        {
            throw ptlsmr::win32_error("public control-plane request command", ERROR_INVALID_FUNCTION);
        }
        throw_if_stopping();
        g_operationPhase = L"engine activation recovery";
        recover_engine_activation();
        g_operationPhase = L"abandoned release staging recovery";
        cleanup_abandoned_release_stages();
        write_host_evidence();

        g_operationPhase = L"protected request creation";
        const auto directory = ptlsmr::create_protected_staging_directory(
            ptlsmr::requests_root(),
            L"request");
        const auto requestPath = directory / L"request.bin";
        const auto responsePath = directory / L"response.bin";
        const auto diagnosticPath = directory / L"engine-diagnostic.txt";
        ptlsmr::engine_request engineRequest{};
        engineRequest.magic = ptlsmr::ProtocolMagic;
        engineRequest.version = ptlsmr::ProtocolVersion;
        engineRequest.command = input.command;
        copy_bounded(engineRequest.ownerSid, ARRAYSIZE(engineRequest.ownerSid), caller.ownerSid);
        copy_bounded(engineRequest.releaseId, ARRAYSIZE(engineRequest.releaseId), releaseId);
        if (operation == ptlsmr::public_command::acquire)
        {
            copy_bounded(
                engineRequest.inboxPath,
                ARRAYSIZE(engineRequest.inboxPath),
                (caller.inboxRoot / releaseId).wstring());
        }
        g_operationPhase = L"protected request write";
        write_binary_atomic(requestPath, &engineRequest, sizeof(engineRequest));

        try
        {
            for (size_t attempt = 0; attempt < 2; ++attempt)
            {
                throw_if_stopping();
                g_operationPhase = L"engine invocation";
                const auto engineReply = invoke_engine(
                    engineRequest,
                    requestPath,
                    responsePath,
                    diagnosticPath);
                throw_if_stopping();
                if (engineReply.win32Status != ERROR_SUCCESS)
                {
                    ptlsmr::public_reply output{};
                    output.command = input.command;
                    output.win32Status = engineReply.win32Status;
                    output.scmState = engineReply.scmState;
                    output.processId = engineReply.processId;
                    output.leaseCount = engineReply.leaseCount;
                    copy_bounded(
                        output.runtimeVersion,
                        ARRAYSIZE(output.runtimeVersion),
                        engineReply.runtimeVersion);
                    copy_bounded(
                        output.activeEngineVersion,
                        ARRAYSIZE(output.activeEngineVersion),
                        ptlsmr::format_version(read_active_engine_version()));
                    copy_bounded(output.detail, ARRAYSIZE(output.detail), engineReply.detail);
                    g_operationPhase = L"protected request cleanup";
                    remove_protected_request_directory(directory);
                    return output;
                }
                const auto action = static_cast<ptlsmr::engine_action>(engineReply.action);
                if (action == ptlsmr::engine_action::activate_engine)
                {
                    if (attempt != 0)
                    {
                        throw ptlsmr::win32_error("engine activation retry policy", ERROR_INVALID_DATA);
                    }
                    activate_engine(engineReply);
                    throw_if_stopping();
                    write_host_evidence();
                    continue;
                }
                if (action != ptlsmr::engine_action::complete)
                {
                    throw ptlsmr::win32_error("engine response action policy", ERROR_INVALID_DATA);
                }
                ptlsmr::public_reply output{};
                output.command = input.command;
                output.win32Status = engineReply.win32Status;
                output.scmState = engineReply.scmState;
                output.processId = engineReply.processId;
                output.leaseCount = engineReply.leaseCount;
                copy_bounded(
                    output.runtimeVersion,
                    ARRAYSIZE(output.runtimeVersion),
                    engineReply.runtimeVersion);
                copy_bounded(
                    output.activeEngineVersion,
                    ARRAYSIZE(output.activeEngineVersion),
                    ptlsmr::format_version(read_active_engine_version()));
                copy_bounded(output.detail, ARRAYSIZE(output.detail), engineReply.detail);
                g_operationPhase = L"protected request cleanup";
                remove_protected_request_directory(directory);
                return output;
            }
            throw ptlsmr::win32_error("engine activation retry exhaustion", ERROR_RETRY);
        }
        catch (...)
        {
            g_operationPhase = L"protected request cleanup after failure";
            remove_protected_request_directory(directory);
            throw;
        }
    }

    void set_failure_reply(
        const ptlsmr::public_request& input,
        ptlsmr::public_reply& output,
        DWORD error,
        std::wstring_view detail)
    {
        output.command = input.command;
        output.win32Status = error;
        copy_bounded(output.detail, ARRAYSIZE(output.detail), detail);
        try
        {
            copy_bounded(
                output.activeEngineVersion,
                ARRAYSIZE(output.activeEngineVersion),
                ptlsmr::format_version(read_active_engine_version()));
        }
        catch (...)
        {
            // The failure reply must not be overwritten by secondary diagnostic state.
        }
    }

    void serve_client(HANDLE pipe, const caller_context& caller)
    {
        ptlsmr::public_request input{};
        ptlsmr::public_reply output{};
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
            dispatch_guard serialization;
            g_operationPhase = L"request dispatch";
            output = dispatch_request(caller, input);
        }
        catch (const ptlsmr::win32_error& error)
        {
            const std::string_view text(error.what());
            set_failure_reply(
                input,
                output,
                error.code(),
                std::wstring(text.begin(), text.end()));
        }
        catch (const std::exception& error)
        {
            const std::string_view text(error.what());
            set_failure_reply(
                input,
                output,
                ERROR_UNHANDLED_EXCEPTION,
                L"host " + g_operationPhase + L": " +
                    std::wstring(text.begin(), text.end()));
        }
        catch (...)
        {
            set_failure_reply(input, output, ERROR_UNHANDLED_EXCEPTION, L"unexpected host failure");
        }
        (void)perform_stop_aware_pipe_io(
            pipe,
            &output,
            sizeof(output),
            transferred,
            true);
    }

    [[nodiscard]] std::vector<ptlsmr::unique_handle> create_host_pipes(
        std::wstring& endpoint)
    {
        if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error(
                "secondary host pipe instance creator policy",
                ERROR_ACCESS_DENIED);
        }
        PSECURITY_DESCRIPTOR descriptor = nullptr;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                L"D:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;0x0012019B;;;AU)",
                SDDL_REVISION_1,
                &descriptor,
                nullptr))
        {
            throw ptlsmr::win32_error(
                "ConvertStringSecurityDescriptorToSecurityDescriptorW(host pipe)",
                GetLastError());
        }
        ptlsmr::local_memory security(descriptor);
        SECURITY_ATTRIBUTES attributes{ sizeof(attributes), descriptor, FALSE };
        endpoint = std::wstring(ptlsmr::HostPipePrefix) +
            ptlsmr::random_hex_identifier(16);
        std::vector<ptlsmr::unique_handle> pipes;
        pipes.reserve(HostPipeInstanceCount);
        for (size_t index = 0; index < HostPipeInstanceCount; ++index)
        {
            DWORD access =
                PIPE_ACCESS_DUPLEX | FILE_FLAG_OVERLAPPED;
            if (index == 0)
            {
                access |= FILE_FLAG_FIRST_PIPE_INSTANCE;
            }
            ptlsmr::unique_handle pipe(CreateNamedPipeW(
                endpoint.c_str(),
                access,
                PIPE_TYPE_MESSAGE | PIPE_READMODE_MESSAGE | PIPE_WAIT |
                    PIPE_REJECT_REMOTE_CLIENTS,
                static_cast<DWORD>(HostPipeInstanceCount),
                sizeof(ptlsmr::public_reply),
                sizeof(ptlsmr::public_request),
                0,
                &attributes));
            if (!pipe)
            {
                throw ptlsmr::win32_error(
                    index == 0 ?
                        "CreateNamedPipeW(host first-instance anchor)" :
                        "CreateNamedPipeW(host secondary instance)",
                    GetLastError());
            }
            pipes.push_back(std::move(pipe));
        }
        return pipes;
    }

    void pipe_server(HANDLE pipe)
    {
        while (WaitForSingleObject(g_stopEvent.get(), 0) != WAIT_OBJECT_0)
        {
            const auto connection = connect_stop_aware_pipe(pipe);
            if (connection == pipe_io_result::stopped)
            {
                return;
            }
            if (connection == pipe_io_result::disconnected)
            {
                (void)DisconnectNamedPipe(pipe);
                continue;
            }
            g_operationPhase = L"caller process and token binding";
            const auto identity = reject_connection_failures<caller_identity>(
                [&] {
                    return identify_caller(pipe);
                });
            if (identity)
            {
                g_operationPhase = L"provisional process SID quota";
                active_connection_guard connectionQuota(identity->ownerSid);
                if (connectionQuota.acquired())
                {
                    g_operationPhase = L"pipe token SID binding";
                    const bool pipeTokenBound = reject_connection_failures<bool>(
                        [&] {
                            bind_pipe_token_to_caller(pipe, *identity);
                            return true;
                        }).value_or(false);
                    if (!pipeTokenBound)
                    {
                        (void)DisconnectNamedPipe(pipe);
                        continue;
                    }
                    g_operationPhase =
                        L"protected caller path and profile authorization";
                    const auto caller = reject_connection_failures<caller_context>(
                        [&] {
                            return authorize_caller(*identity);
                        });
                    if (caller)
                    {
                        serve_client(pipe, *caller);
                    }
                }
            }
            if (!DisconnectNamedPipe(pipe) &&
                GetLastError() != ERROR_PIPE_NOT_CONNECTED)
            {
                throw ptlsmr::win32_error("DisconnectNamedPipe(host)", GetLastError());
            }
        }
    }

    void write_host_evidence()
    {
        const auto active = read_active_engine_version();
        std::wstringstream evidence;
        evidence << L"serviceName=" << ptlsmr::HostServiceName << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"tokenUserSid=" << ptlsmr::current_token_user_sid() << L"\r\n";
        evidence << L"executablePath=" << module_path().wstring() << L"\r\n";
        evidence << L"hostVersion=" << ptlsmr::HostVersion << L"\r\n";
        evidence << L"activeEngineVersion=" << ptlsmr::format_version(active) << L"\r\n";
        evidence << L"codeSignerSha256=" << ptlsmr::read_code_signer_pin() << L"\r\n";
        evidence << L"metadataSignerSha256=" << ptlsmr::read_metadata_signer_pin() << L"\r\n";
        evidence << L"pipeEndpoint=" << g_publishedEndpoint << L"\r\n";
        evidence << L"pipePolicy=random-128bit-first-instance-anchor-system-secondary-pool-au-data-rw-no-create-instance-raw-dos-image-provisional-sid-quota-preface-token-match-timeout-5000ms-reject-remote\r\n";
        evidence << L"pipeListenerCount=" << HostPipeInstanceCount << L"\r\n";
        evidence << L"pipePerSidActiveConnectionLimit=" <<
            PerSidActiveConnectionLimit << L"\r\n";
        evidence << L"childProcessPolicy=kill-on-close-job-stop-aware-120000ms\r\n";
        evidence << L"bootstrapOrigin=companion-msi\r\n";
        evidence << L"hostSelfServicing=msi-or-external-repair-only\r\n";
        evidence << L"packageIdentityPresent=false\r\n";
        evidence << L"packageFullNameResult=" << ptlsmr::require_no_package_identity() << L"\r\n";
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::program_data_root() / L"host-evidence.txt",
            evidence.str());
    }

    [[nodiscard]] bool process_is_elevated_administrator()
    {
        HANDLE raw = nullptr;
        ptlsmr::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw),
            "OpenProcessToken(MSI operation)");
        ptlsmr::unique_handle token(raw);
        TOKEN_ELEVATION elevation{};
        DWORD bytes = 0;
        ptlsmr::check_bool(
            GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &bytes),
            "GetTokenInformation(MSI elevation)");
        return elevation.TokenIsElevated != 0 && ptlsmr::token_is_administrator(token.get());
    }

    void protect_msi_owned_bootstrap_files()
    {
        const auto initialEngine = ptlsmr::parse_version(ptlsmr::InitialEngineVersion);
        const std::array files{
            ptlsmr::host_executable_path(),
            ptlsmr::engine_executable_path(initialEngine),
            ptlsmr::code_signer_pin_path(),
            ptlsmr::metadata_signer_pin_path(),
            ptlsmr::code_policy_path(),
            ptlsmr::metadata_policy_path(),
        };
        for (const auto& file : files)
        {
            ptlsmr::protect_system_file(file);
        }
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

    [[nodiscard]] size_t strict_line_count(
        const std::filesystem::path& path,
        size_t maximumBytes,
        const std::function<void(std::wstring_view)>& validate)
    {
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error("required protected state file", ERROR_FILE_NOT_FOUND);
        }
        const auto text = ptlsmr::read_utf8_file(path, maximumBytes);
        size_t count = 0;
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            if (line.empty())
            {
                throw ptlsmr::win32_error("protected state empty record", ERROR_INVALID_DATA);
            }
            validate(line);
            ++count;
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
        return count;
    }

    [[nodiscard]] size_t validate_lease_state(
        const std::filesystem::path& path)
    {
        std::set<std::wstring> owners;
        const size_t count = strict_line_count(
            path,
            16 * 1024,
            [&](std::wstring_view line) {
                if (line.find(L'|') != std::wstring_view::npos)
                {
                    throw ptlsmr::win32_error("lease state SID-only format", ERROR_INVALID_DATA);
                }
                std::wstring owner;
                try
                {
                    owner = ptlsmr::canonical_owner_sid(line);
                }
                catch (const ptlsmr::win32_error& error)
                {
                    if (error.code() != ERROR_INVALID_SID)
                    {
                        throw;
                    }
                    throw ptlsmr::win32_error(
                        "lease state SID format",
                        ERROR_INVALID_DATA);
                }
                if (!owners.emplace(owner).second)
                {
                    throw ptlsmr::win32_error("lease state unique owner policy", ERROR_INVALID_DATA);
                }
            });
        if (count > ptlsmr::MaxLeases)
        {
            throw ptlsmr::win32_error("lease state count policy", ERROR_TOO_MANY_NAMES);
        }
        return count;
    }

    void require_zero_leases()
    {
        const size_t count = validate_lease_state(ptlsmr::lease_state_path());
        if (count != 0)
        {
            throw ptlsmr::win32_error("lease state uninstall policy", ERROR_BUSY);
        }
    }

    [[nodiscard]] size_t validate_inventory_state(
        const std::filesystem::path& path)
    {
        std::set<std::wstring> owners;
        const size_t count = strict_line_count(
            path,
            16 * 1024,
            [&](std::wstring_view line) {
                const auto fields = split(line, L'|');
                if (fields.size() != 5 ||
                    (fields[1] != L"1" && fields[1] != L"2") ||
                    ptlsmr::canonical_signer_sha256(fields[3]) != fields[3] ||
                    fields[4].size() != ptlsmr::TransactionIdChars ||
                    !std::all_of(fields[4].begin(), fields[4].end(), [](wchar_t character) {
                        return (character >= L'0' && character <= L'9') ||
                            (character >= L'a' && character <= L'f');
                    }))
                {
                    throw ptlsmr::win32_error("runtime inventory uninstall format", ERROR_INVALID_DATA);
                }
                std::wstring owner;
                try
                {
                    owner = ptlsmr::canonical_owner_sid(fields[0]);
                }
                catch (const ptlsmr::win32_error& error)
                {
                    if (error.code() != ERROR_INVALID_SID)
                    {
                        throw;
                    }
                    throw ptlsmr::win32_error(
                        "runtime inventory owner SID format",
                        ERROR_INVALID_DATA);
                }
                const auto version = ptlsmr::parse_version(fields[2]);
                if (version.major != static_cast<uint16_t>(fields[1][0] - L'0') ||
                    !owners.emplace(owner).second)
                {
                    throw ptlsmr::win32_error("runtime inventory uninstall identity", ERROR_INVALID_DATA);
                }
            });
        if (count > ptlsmr::MaxLeases)
        {
            throw ptlsmr::win32_error("runtime inventory uninstall count", ERROR_TOO_MANY_NAMES);
        }
        return count;
    }

    void require_zero_inventory()
    {
        const size_t count = validate_inventory_state(
            ptlsmr::program_data_root() / L"runtime-inventory.txt");
        if (count != 0)
        {
            throw ptlsmr::win32_error("runtime inventory uninstall policy", ERROR_BUSY);
        }
    }

    void require_no_runtime_services()
    {
        service_handle scm(OpenSCManagerW(
            nullptr,
            nullptr,
            SC_MANAGER_CONNECT | SC_MANAGER_ENUMERATE_SERVICE));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(uninstall check)", GetLastError());
        }
        DWORD bytes = 0;
        DWORD count = 0;
        DWORD resume = 0;
        const BOOL initial = EnumServicesStatusExW(
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
        if (!initial && GetLastError() != ERROR_MORE_DATA)
        {
            throw ptlsmr::win32_error(
                "EnumServicesStatusExW(uninstall check size)",
                GetLastError());
        }
        if (initial)
        {
            return;
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
            "EnumServicesStatusExW(uninstall check)");
        const auto* services =
            reinterpret_cast<const ENUM_SERVICE_STATUS_PROCESSW*>(buffer.data());
        for (DWORD index = 0; index < count; ++index)
        {
            if (std::wstring_view(services[index].lpServiceName).starts_with(
                    L"PtPuvrRuntime_"))
            {
                throw ptlsmr::win32_error("runtime service uninstall policy", ERROR_BUSY);
            }
        }
    }

    void require_no_pending_journals()
    {
        const auto dataRoot = ptlsmr::program_data_root();
        const std::array paths{
            ptlsmr::engine_activation_journal_path(),
            ptlsmr::acquisition_journal_path(),
            dataRoot / L"runtime-transaction.txt",
            dataRoot / L"runtime-cleanup-transaction.txt",
        };
        for (const auto& path : paths)
        {
            if (std::filesystem::exists(path) ||
                std::filesystem::exists(path.wstring() + L".new"))
            {
                throw ptlsmr::win32_error("pending journal uninstall policy", ERROR_BUSY);
            }
        }
    }

    [[nodiscard]] uint64_t parse_bounded_epoch(std::wstring_view value)
    {
        if (value.empty() || value.size() > 19 ||
            !std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return character >= L'0' && character <= L'9';
            }))
        {
            throw ptlsmr::win32_error(
                "accepted release state epoch format",
                ERROR_INVALID_DATA);
        }
        try
        {
            const auto epoch = std::stoull(std::wstring(value));
            if (epoch == 0)
            {
                throw ptlsmr::win32_error(
                    "accepted release state epoch range",
                    ERROR_INVALID_DATA);
            }
            return epoch;
        }
        catch (const std::invalid_argument&)
        {
            throw ptlsmr::win32_error(
                "accepted release state epoch format",
                ERROR_INVALID_DATA);
        }
        catch (const std::out_of_range&)
        {
            throw ptlsmr::win32_error(
                "accepted release state epoch range",
                ERROR_INVALID_DATA);
        }
    }

    void validate_accepted_release_state(
        const std::filesystem::path& path = ptlsmr::accepted_release_state_path())
    {
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error(
                "required accepted release state",
                ERROR_FILE_NOT_FOUND);
        }
        const auto text = ptlsmr::read_utf8_file(path, 32 * 1024);
        size_t record = 0;
        size_t start = 0;
        uint64_t acceptedEpoch = 0;
        std::set<std::wstring> releases;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const std::wstring_view line(
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
                acceptedEpoch = parse_bounded_epoch(line.substr(6));
            }
            else
            {
                if (!line.starts_with(L"release="))
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record type",
                        ERROR_INVALID_DATA);
                }
                const auto fields = split(line.substr(8), L'|');
                if (fields.size() != 3 ||
                    !valid_release_id(fields[0]) ||
                    parse_bounded_epoch(fields[1]) > acceptedEpoch ||
                    ptlsmr::canonical_signer_sha256(fields[2]) != fields[2] ||
                    !releases.emplace(fields[0]).second)
                {
                    throw ptlsmr::win32_error(
                        "accepted release state record policy",
                        ERROR_INVALID_DATA);
                }
                if (releases.size() > 128)
                {
                    throw ptlsmr::win32_error(
                        "accepted release state count",
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
    }

    struct acquisition_state_intent
    {
        uint16_t runtimeTrack{};
        ptlsmr::file_version beforeRuntimeFloor{};
        ptlsmr::file_version targetRuntimeFloor{};
        std::wstring beforeSecurityStateHash;
        std::wstring targetSecurityStateHash;
        std::wstring ownerSid;
        ptlsmr::file_version targetRuntimeVersion{};
        std::wstring targetRuntimeSha256;
        std::wstring targetTransactionId;
        bool previousRuntimePresent{};
        uint16_t previousRuntimeTrack{};
        ptlsmr::file_version previousRuntimeVersion{};
        std::wstring previousRuntimeSha256;
        std::wstring previousTransactionId;
        std::wstring phase;
    };

    struct runtime_inventory_intent
    {
        std::wstring ownerSid;
        uint16_t runtimeTrack{};
        bool existing{};
        ptlsmr::file_version previousVersion{};
        std::wstring previousSha256;
        std::wstring previousTransactionId;
        ptlsmr::file_version candidateVersion{};
        std::wstring candidateSha256;
        std::wstring candidateTransactionId;
        std::wstring phase;
    };

    struct cleanup_inventory_intent
    {
        std::wstring ownerSid;
        std::wstring phase;
    };

    [[nodiscard]] constexpr bool valid_transaction_id(std::wstring_view value)
    {
        return value.size() == ptlsmr::TransactionIdChars &&
            std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return (character >= L'0' && character <= L'9') ||
                    (character >= L'a' && character <= L'f');
            });
    }

    [[nodiscard]] std::optional<std::filesystem::path> journal_read_path(
        const std::filesystem::path& primary)
    {
        if (std::filesystem::exists(primary))
        {
            return primary;
        }
        const std::filesystem::path replacement = primary.wstring() + L".new";
        if (std::filesystem::exists(replacement))
        {
            return replacement;
        }
        return std::nullopt;
    }

    [[nodiscard]] std::optional<acquisition_state_intent>
    read_acquisition_state_intent()
    {
        const auto path = journal_read_path(ptlsmr::acquisition_journal_path());
        if (!path)
        {
            return std::nullopt;
        }
        const auto fields = parse_exact_fields(
            ptlsmr::read_utf8_file(*path, 8192),
            {
                L"schema", L"owner", L"releaseId", L"manifestHash", L"track",
                L"runtimeVersion", L"targetRuntimeSha256", L"targetTransactionId",
                L"previousRuntimePresent", L"previousRuntimeTrack",
                L"previousRuntimeVersion", L"previousRuntimeSha256",
                L"previousTransactionId", L"beforeRuntimeFloor",
                L"targetRuntimeFloor", L"beforeSecurityEpoch",
                L"targetSecurityEpoch", L"beforeSecurityStateHash",
                L"targetSecurityStateHash", L"phase",
            },
            {
                L"schema", L"owner", L"releaseId", L"manifestHash", L"track",
                L"runtimeVersion", L"targetRuntimeSha256", L"targetTransactionId",
                L"previousRuntimePresent", L"previousRuntimeTrack",
                L"previousRuntimeVersion", L"previousRuntimeSha256",
                L"previousTransactionId", L"beforeRuntimeFloor",
                L"targetRuntimeFloor", L"beforeSecurityEpoch",
                L"targetSecurityEpoch", L"beforeSecurityStateHash",
                L"targetSecurityStateHash", L"phase",
            },
            "mutable state acquisition journal");
        static constexpr std::array<std::wstring_view, 5> phases{
            L"prepared",
            L"runtime-provisioning",
            L"runtime-committed",
            L"floor-committed",
            L"security-committed",
        };
        if (fields.at(L"schema") != L"2" ||
            !valid_release_id(fields.at(L"releaseId")) ||
            (fields.at(L"track") != L"1" && fields.at(L"track") != L"2") ||
            (fields.at(L"previousRuntimePresent") != L"0" &&
             fields.at(L"previousRuntimePresent") != L"1") ||
            std::find(phases.begin(), phases.end(), fields.at(L"phase")) == phases.end())
        {
            throw ptlsmr::win32_error(
                "mutable state acquisition journal policy",
                ERROR_INVALID_DATA);
        }
        acquisition_state_intent intent{};
        intent.ownerSid = ptlsmr::canonical_owner_sid(fields.at(L"owner"));
        (void)ptlsmr::canonical_signer_sha256(fields.at(L"manifestHash"));
        intent.runtimeTrack = static_cast<uint16_t>(fields.at(L"track")[0] - L'0');
        intent.targetRuntimeVersion =
            ptlsmr::parse_version(fields.at(L"runtimeVersion"));
        intent.targetRuntimeSha256 =
            ptlsmr::canonical_signer_sha256(fields.at(L"targetRuntimeSha256"));
        intent.targetTransactionId = fields.at(L"targetTransactionId");
        intent.previousRuntimePresent = fields.at(L"previousRuntimePresent") == L"1";
        intent.beforeRuntimeFloor =
            ptlsmr::parse_version(fields.at(L"beforeRuntimeFloor"));
        intent.targetRuntimeFloor =
            ptlsmr::parse_version(fields.at(L"targetRuntimeFloor"));
        (void)parse_bounded_epoch(fields.at(L"beforeSecurityEpoch"));
        (void)parse_bounded_epoch(fields.at(L"targetSecurityEpoch"));
        intent.beforeSecurityStateHash =
            ptlsmr::canonical_signer_sha256(fields.at(L"beforeSecurityStateHash"));
        intent.targetSecurityStateHash =
            ptlsmr::canonical_signer_sha256(fields.at(L"targetSecurityStateHash"));
        intent.phase = fields.at(L"phase");
        if (!valid_transaction_id(intent.targetTransactionId) ||
            intent.targetRuntimeVersion.major != intent.runtimeTrack ||
            intent.beforeRuntimeFloor.major != intent.runtimeTrack ||
            !(intent.targetRuntimeFloor == intent.targetRuntimeVersion) ||
            intent.targetRuntimeFloor < intent.beforeRuntimeFloor)
        {
            throw ptlsmr::win32_error(
                "mutable state acquisition target policy",
                ERROR_INVALID_DATA);
        }
        if (intent.previousRuntimePresent)
        {
            if (fields.at(L"previousRuntimeTrack") != L"1" &&
                fields.at(L"previousRuntimeTrack") != L"2")
            {
                throw ptlsmr::win32_error(
                    "mutable state acquisition previous track",
                    ERROR_INVALID_DATA);
            }
            intent.previousRuntimeTrack =
                static_cast<uint16_t>(fields.at(L"previousRuntimeTrack")[0] - L'0');
            intent.previousRuntimeVersion =
                ptlsmr::parse_version(fields.at(L"previousRuntimeVersion"));
            intent.previousRuntimeSha256 =
                ptlsmr::canonical_signer_sha256(fields.at(L"previousRuntimeSha256"));
            intent.previousTransactionId = fields.at(L"previousTransactionId");
            if (intent.previousRuntimeVersion.major != intent.previousRuntimeTrack ||
                !valid_transaction_id(intent.previousTransactionId))
            {
                throw ptlsmr::win32_error(
                    "mutable state acquisition previous identity",
                    ERROR_INVALID_DATA);
            }
        }
        else if (fields.at(L"previousRuntimeTrack") != L"0" ||
                 fields.at(L"previousRuntimeVersion") != L"none" ||
                 fields.at(L"previousRuntimeSha256") != L"none" ||
                 fields.at(L"previousTransactionId") != L"none")
        {
            throw ptlsmr::win32_error(
                "mutable state acquisition absent previous identity",
                ERROR_INVALID_DATA);
        }
        return intent;
    }

    [[nodiscard]] std::optional<runtime_inventory_intent>
    read_runtime_inventory_intent()
    {
        const auto dataRoot = ptlsmr::program_data_root();
        const auto path = journal_read_path(dataRoot / L"runtime-transaction.txt");
        if (!path)
        {
            return std::nullopt;
        }
        const auto fields = parse_exact_fields_allow_empty(
            ptlsmr::read_utf8_file(*path, 16 * 1024),
            {
                L"owner", L"service", L"track", L"existing",
                L"previousWasRunning", L"previousVersion", L"previousPath",
                L"previousSha256", L"previousTransactionId", L"candidateVersion",
                L"stagingPath", L"candidatePath", L"candidateSha256",
                L"candidateTransactionId", L"phase",
            },
            {
                L"owner", L"service", L"track", L"existing",
                L"previousWasRunning", L"previousVersion", L"previousPath",
                L"previousSha256", L"previousTransactionId", L"candidateVersion",
                L"stagingPath", L"candidatePath", L"candidateSha256",
                L"candidateTransactionId", L"phase",
            },
            "mutable state runtime journal");
        if ((fields.at(L"track") != L"1" && fields.at(L"track") != L"2") ||
            (fields.at(L"existing") != L"0" && fields.at(L"existing") != L"1") ||
            (fields.at(L"previousWasRunning") != L"0" &&
             fields.at(L"previousWasRunning") != L"1"))
        {
            throw ptlsmr::win32_error(
                "mutable state runtime journal policy",
                ERROR_INVALID_DATA);
        }
        runtime_inventory_intent intent{};
        intent.ownerSid = ptlsmr::canonical_owner_sid(fields.at(L"owner"));
        if (ptlsmr::instance_names(intent.ownerSid).serviceName != fields.at(L"service"))
        {
            throw ptlsmr::win32_error(
                "mutable state runtime journal service",
                ERROR_INVALID_DATA);
        }
        intent.runtimeTrack = static_cast<uint16_t>(fields.at(L"track")[0] - L'0');
        intent.existing = fields.at(L"existing") == L"1";
        intent.candidateVersion = ptlsmr::parse_version(fields.at(L"candidateVersion"));
        intent.candidateSha256 =
            ptlsmr::canonical_signer_sha256(fields.at(L"candidateSha256"));
        intent.candidateTransactionId = fields.at(L"candidateTransactionId");
        intent.phase = fields.at(L"phase");
        static constexpr std::array<std::wstring_view, 13> phases{
            L"validated-staged", L"final-installed", L"service-created",
            L"stop-pending", L"repath-pending", L"repathed", L"ready",
            L"inventory-commit-pending", L"inventory-committed",
            L"sibling-sync-pending", L"siblings-synchronized",
            L"unreferenced-cleanup-pending", L"rollback-cleanup-pending",
        };
        if (intent.candidateVersion.major != intent.runtimeTrack ||
            !valid_transaction_id(intent.candidateTransactionId) ||
            !ptlsmr::path_is_within(
                fields.at(L"stagingPath"),
                ptlsmr::installation_root() / L"Staging") ||
            _wcsicmp(
                std::filesystem::path(fields.at(L"stagingPath")).filename().c_str(),
                ptlsmr::RuntimeExe) != 0 ||
            !equal_path(
                fields.at(L"candidatePath"),
                ptlsmr::runtime_executable_path(
                    intent.runtimeTrack,
                    intent.candidateVersion)) ||
            std::find(phases.begin(), phases.end(), intent.phase) == phases.end())
        {
            throw ptlsmr::win32_error(
                "mutable state runtime journal target",
                ERROR_INVALID_DATA);
        }
        if (intent.existing)
        {
            intent.previousVersion = ptlsmr::parse_version(fields.at(L"previousVersion"));
            intent.previousSha256 =
                ptlsmr::canonical_signer_sha256(fields.at(L"previousSha256"));
            intent.previousTransactionId = fields.at(L"previousTransactionId");
            if (intent.previousVersion.major != intent.runtimeTrack ||
                !valid_transaction_id(intent.previousTransactionId) ||
                !equal_path(
                    fields.at(L"previousPath"),
                    ptlsmr::runtime_executable_path(
                        intent.runtimeTrack,
                        intent.previousVersion)))
            {
                throw ptlsmr::win32_error(
                    "mutable state runtime journal previous",
                    ERROR_INVALID_DATA);
            }
        }
        else if (!fields.at(L"previousVersion").empty() ||
                 !fields.at(L"previousPath").empty() ||
                 !fields.at(L"previousSha256").empty() ||
                 !fields.at(L"previousTransactionId").empty() ||
                 fields.at(L"previousWasRunning") != L"0")
        {
            throw ptlsmr::win32_error(
                "mutable state runtime journal absent previous identity",
                ERROR_INVALID_DATA);
        }
        return intent;
    }

    [[nodiscard]] std::optional<cleanup_inventory_intent>
    read_cleanup_inventory_intent()
    {
        const auto path = journal_read_path(
            ptlsmr::program_data_root() / L"runtime-cleanup-transaction.txt");
        if (!path)
        {
            return std::nullopt;
        }
        const auto fields = parse_exact_fields(
            ptlsmr::read_utf8_file(*path, 4096),
            {
                L"owner", L"service", L"track", L"version",
                L"runtimeSha256", L"transactionId", L"phase",
            },
            {
                L"owner", L"service", L"track", L"version",
                L"runtimeSha256", L"transactionId", L"phase",
            },
            "mutable state cleanup journal");
        static constexpr std::array<std::wstring_view, 5> phases{
            L"prepared", L"service-deleted", L"inventory-removed",
            L"store-removed", L"sibling-sync-pending",
        };
        cleanup_inventory_intent intent{
            ptlsmr::canonical_owner_sid(fields.at(L"owner")),
            fields.at(L"phase"),
        };
        if (ptlsmr::instance_names(intent.ownerSid).serviceName != fields.at(L"service") ||
            (fields.at(L"track") != L"1" && fields.at(L"track") != L"2") ||
            ptlsmr::parse_version(fields.at(L"version")).major !=
                static_cast<uint16_t>(fields.at(L"track")[0] - L'0') ||
            !valid_transaction_id(fields.at(L"transactionId")) ||
            std::find(phases.begin(), phases.end(), intent.phase) == phases.end())
        {
            throw ptlsmr::win32_error(
                "mutable state cleanup journal policy",
                ERROR_INVALID_DATA);
        }
        (void)ptlsmr::canonical_signer_sha256(fields.at(L"runtimeSha256"));
        return intent;
    }

    [[nodiscard]] bool inventory_has_record(
        const std::filesystem::path& path,
        std::wstring_view owner,
        uint16_t track,
        const ptlsmr::file_version& version,
        std::wstring_view sha256,
        std::wstring_view transactionId)
    {
        (void)validate_inventory_state(path);
        const auto text = ptlsmr::read_utf8_file(path, 16 * 1024);
        const std::wstring expected =
            std::wstring(owner) + L"|" + std::to_wstring(track) + L"|" +
            ptlsmr::format_version(version) + L"|" + std::wstring(sha256) +
            L"|" + std::wstring(transactionId);
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            if (line == expected)
            {
                return true;
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
        return false;
    }

    [[nodiscard]] bool inventory_has_owner(
        const std::filesystem::path& path,
        std::wstring_view owner)
    {
        (void)validate_inventory_state(path);
        const auto text = ptlsmr::read_utf8_file(path, 16 * 1024);
        const std::wstring prefix = std::wstring(owner) + L"|";
        size_t start = 0;
        while (start < text.size())
        {
            const size_t end = text.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                text.data() + start,
                (end == std::wstring::npos ? text.size() : end) - start);
            if (line.starts_with(prefix))
            {
                return true;
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
        return false;
    }

    [[nodiscard]] ptlsmr::file_version validate_version_state(
        const std::filesystem::path& path,
        uint16_t expectedMajor,
        const char* operation)
    {
        if (!std::filesystem::is_regular_file(path))
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
        }
        const auto version =
            ptlsmr::parse_version(ptlsmr::read_utf8_file(path, 64));
        if (version.major != expectedMajor)
        {
            throw ptlsmr::win32_error(operation, ERROR_INVALID_DATA);
        }
        return version;
    }

    void validate_mutable_state_file(
        size_t index,
        const std::filesystem::path& path)
    {
        switch (index)
        {
        case 0:
        {
            const auto version = validate_version_state(
                path,
                5,
                "active engine replacement");
            const auto engine = ptlsmr::engine_executable_path(version);
            (void)ptlsmr::validate_engine_candidate(
                engine,
                ptlsmr::read_code_signer_pin());
            return;
        }
        case 1:
            (void)validate_version_state(path, 5, "engine floor replacement");
            return;
        case 2:
            (void)validate_version_state(path, 1, "runtime track 1 floor replacement");
            return;
        case 3:
            (void)validate_version_state(path, 2, "runtime track 2 floor replacement");
            return;
        case 4:
            validate_accepted_release_state(path);
            return;
        case 5:
            (void)validate_lease_state(path);
            return;
        case 6:
            (void)validate_inventory_state(path);
            return;
        default:
            throw ptlsmr::win32_error(
                "mutable state replacement index",
                ERROR_INVALID_PARAMETER);
        }
    }

    [[nodiscard]] bool acquisition_phase_at_least_runtime_committed(
        std::wstring_view phase)
    {
        return phase == L"runtime-committed" ||
            phase == L"floor-committed" ||
            phase == L"security-committed";
    }

    [[nodiscard]] bool should_promote_state_replacement(
        size_t index,
        const std::filesystem::path& primary,
        const std::filesystem::path& replacement)
    {
        if (index == 0 || index == 1)
        {
            const auto journalPath =
                journal_read_path(ptlsmr::engine_activation_journal_path());
            if (!journalPath)
            {
                return false;
            }
            const auto journal = read_activation_journal(*journalPath);
            const auto replacementVersion = validate_version_state(
                replacement,
                5,
                index == 0 ? "active engine replacement" : "engine floor replacement");
            if (index == 0)
            {
                return journal.phase == L"active-switched" &&
                    replacementVersion == journal.candidate;
            }
            const auto active = read_active_engine_version();
            return journal.phase == L"active-switched" &&
                active == journal.candidate &&
                replacementVersion == journal.candidate;
        }

        const auto acquisition = read_acquisition_state_intent();
        if ((index == 2 || index == 3) && acquisition &&
            acquisition->runtimeTrack == (index == 2 ? 1 : 2))
        {
            const auto primaryVersion = validate_version_state(
                primary,
                acquisition->runtimeTrack,
                "runtime floor primary");
            const auto replacementVersion = validate_version_state(
                replacement,
                acquisition->runtimeTrack,
                "runtime floor replacement");
            return acquisition_phase_at_least_runtime_committed(acquisition->phase) &&
                primaryVersion == acquisition->beforeRuntimeFloor &&
                replacementVersion == acquisition->targetRuntimeFloor;
        }
        if (index == 4 && acquisition)
        {
            const auto primaryHash = ptlsmr::canonical_signer_sha256(
                ptlsmr::sha256_text(ptlsmr::read_utf8_file(primary, 32 * 1024)));
            const auto replacementHash = ptlsmr::canonical_signer_sha256(
                ptlsmr::sha256_text(ptlsmr::read_utf8_file(replacement, 32 * 1024)));
            return (acquisition->phase == L"floor-committed" ||
                    acquisition->phase == L"security-committed") &&
                primaryHash == acquisition->beforeSecurityStateHash &&
                replacementHash == acquisition->targetSecurityStateHash;
        }
        if (index == 6)
        {
            const auto runtime = read_runtime_inventory_intent();
            if (runtime)
            {
                static constexpr std::array<std::wstring_view, 5> targetPhases{
                    L"inventory-commit-pending",
                    L"inventory-committed",
                    L"sibling-sync-pending",
                    L"siblings-synchronized",
                    L"unreferenced-cleanup-pending",
                };
                if (std::find(
                        targetPhases.begin(),
                        targetPhases.end(),
                        runtime->phase) != targetPhases.end() &&
                    inventory_has_record(
                        replacement,
                        runtime->ownerSid,
                        runtime->runtimeTrack,
                        runtime->candidateVersion,
                        runtime->candidateSha256,
                        runtime->candidateTransactionId))
                {
                    return true;
                }
            }
            const auto cleanup = read_cleanup_inventory_intent();
            if (cleanup &&
                (cleanup->phase == L"inventory-removed" ||
                 cleanup->phase == L"store-removed" ||
                 cleanup->phase == L"sibling-sync-pending") &&
                inventory_has_owner(primary, cleanup->ownerSid) &&
                !inventory_has_owner(replacement, cleanup->ownerSid))
            {
                return true;
            }
            if (!runtime && !cleanup && acquisition &&
                acquisition_phase_at_least_runtime_committed(acquisition->phase) &&
                inventory_has_record(
                    replacement,
                    acquisition->ownerSid,
                    acquisition->runtimeTrack,
                    acquisition->targetRuntimeVersion,
                    acquisition->targetRuntimeSha256,
                    acquisition->targetTransactionId))
            {
                return true;
            }
        }
        return false;
    }

    void validate_only_replacement_transaction_order(
        size_t index,
        const std::filesystem::path& replacement)
    {
        if (index == 0)
        {
            const auto journalPath =
                journal_read_path(ptlsmr::engine_activation_journal_path());
            if (!journalPath)
            {
                return;
            }
            const auto journal = read_activation_journal(*journalPath);
            const auto version =
                validate_version_state(replacement, 5, "active engine only replacement");
            const bool authoritative =
                (journal.phase == L"prepared" && version == journal.previous) ||
                (journal.phase == L"active-switched" && version == journal.candidate);
            if (!authoritative)
            {
                throw ptlsmr::win32_error(
                    "active engine replacement journal ordering",
                    ERROR_INVALID_DATA);
            }
            return;
        }
        if (index == 1)
        {
            const auto version =
                validate_version_state(replacement, 5, "engine floor only replacement");
            const auto active = read_active_engine_version();
            if (active < version)
            {
                throw ptlsmr::win32_error(
                    "engine floor replacement exceeds active engine",
                    ERROR_INVALID_DATA);
            }
            return;
        }

        const auto acquisition = read_acquisition_state_intent();
        if ((index == 2 || index == 3) && acquisition &&
            acquisition->runtimeTrack == (index == 2 ? 1 : 2))
        {
            const auto actual = validate_version_state(
                replacement,
                acquisition->runtimeTrack,
                "runtime floor only replacement");
            const auto expected =
                acquisition_phase_at_least_runtime_committed(acquisition->phase)
                ? acquisition->targetRuntimeFloor
                : acquisition->beforeRuntimeFloor;
            if (!(actual == expected))
            {
                throw ptlsmr::win32_error(
                    "runtime floor replacement journal ordering",
                    ERROR_INVALID_DATA);
            }
            return;
        }
        if (index == 4 && acquisition)
        {
            const auto actual = ptlsmr::canonical_signer_sha256(
                ptlsmr::sha256_text(ptlsmr::read_utf8_file(replacement, 32 * 1024)));
            const auto& expected =
                (acquisition->phase == L"floor-committed" ||
                 acquisition->phase == L"security-committed")
                ? acquisition->targetSecurityStateHash
                : acquisition->beforeSecurityStateHash;
            if (actual != expected)
            {
                throw ptlsmr::win32_error(
                    "accepted state replacement journal ordering",
                    ERROR_INVALID_DATA);
            }
            return;
        }
        if (index == 6)
        {
            const auto runtime = read_runtime_inventory_intent();
            if (runtime)
            {
                const bool candidate = inventory_has_record(
                    replacement,
                    runtime->ownerSid,
                    runtime->runtimeTrack,
                    runtime->candidateVersion,
                    runtime->candidateSha256,
                    runtime->candidateTransactionId);
                if (candidate)
                {
                    static constexpr std::array<std::wstring_view, 5> targetPhases{
                        L"inventory-commit-pending",
                        L"inventory-committed",
                        L"sibling-sync-pending",
                        L"siblings-synchronized",
                        L"unreferenced-cleanup-pending",
                    };
                    if (std::find(
                            targetPhases.begin(),
                            targetPhases.end(),
                            runtime->phase) == targetPhases.end())
                    {
                        throw ptlsmr::win32_error(
                            "inventory replacement runtime journal ordering",
                            ERROR_INVALID_DATA);
                    }
                }
                return;
            }
            const auto cleanup = read_cleanup_inventory_intent();
            if (cleanup &&
                !inventory_has_owner(replacement, cleanup->ownerSid) &&
                cleanup->phase != L"inventory-removed" &&
                cleanup->phase != L"store-removed" &&
                cleanup->phase != L"sibling-sync-pending")
            {
                throw ptlsmr::win32_error(
                    "inventory replacement cleanup journal ordering",
                    ERROR_INVALID_DATA);
            }
        }
    }

    void recover_mutable_state_replacements()
    {
        const auto dataRoot = ptlsmr::program_data_root();
        const std::array statePaths{
            ptlsmr::engine_state_path(),
            dataRoot / L"engine-version-floor.txt",
            dataRoot / L"runtime-version-floor-track1.txt",
            dataRoot / L"runtime-version-floor-track2.txt",
            ptlsmr::accepted_release_state_path(),
            ptlsmr::lease_state_path(),
            dataRoot / L"runtime-inventory.txt",
        };
        for (size_t index = 0; index < statePaths.size(); ++index)
        {
            const auto& primary = statePaths[index];
            const std::filesystem::path replacement = primary.wstring() + L".new";
            const bool primaryExists = std::filesystem::exists(primary);
            const bool replacementExists = std::filesystem::exists(replacement);
            if (!replacementExists)
            {
                if (primaryExists)
                {
                    validate_mutable_state_file(index, primary);
                }
                continue;
            }
            validate_mutable_state_file(index, replacement);
            if (primaryExists)
            {
                validate_mutable_state_file(index, primary);
                if (should_promote_state_replacement(index, primary, replacement))
                {
                    ptlsmr::check_bool(
                        MoveFileExW(
                            replacement.c_str(),
                            primary.c_str(),
                            MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH),
                        "MoveFileExW(promote journal-authoritative state replacement)");
                    validate_mutable_state_file(index, primary);
                }
                else if (!DeleteFileW(replacement.c_str()))
                {
                    throw ptlsmr::win32_error(
                        "DeleteFileW(stale mutable state replacement)",
                        GetLastError());
                }
                continue;
            }
            validate_only_replacement_transaction_order(index, replacement);
            ptlsmr::check_bool(
                MoveFileExW(
                    replacement.c_str(),
                    primary.c_str(),
                    MOVEFILE_WRITE_THROUGH),
                "MoveFileExW(recover only mutable state replacement)");
            validate_mutable_state_file(index, primary);
        }
    }

    void validate_nontransactional_protected_state()
    {
        (void)ptlsmr::read_code_signer_pin();
        (void)ptlsmr::read_metadata_signer_pin();
        const auto activeEngine = ptlsmr::parse_version(
            ptlsmr::read_utf8_file(ptlsmr::engine_state_path(), 64));
        const auto engineFloor = ptlsmr::parse_version(ptlsmr::read_utf8_file(
            ptlsmr::program_data_root() / L"engine-version-floor.txt",
            64));
        const auto runtimeFloorOne = ptlsmr::parse_version(ptlsmr::read_utf8_file(
            ptlsmr::program_data_root() / L"runtime-version-floor-track1.txt",
            64));
        const auto runtimeFloorTwo = ptlsmr::parse_version(ptlsmr::read_utf8_file(
            ptlsmr::program_data_root() / L"runtime-version-floor-track2.txt",
            64));
        if (activeEngine.major != 5 ||
            engineFloor.major != 5 ||
            activeEngine < engineFloor ||
            runtimeFloorOne.major != 1 ||
            runtimeFloorTwo.major != 2)
        {
            throw ptlsmr::win32_error(
                "protected version state uninstall policy",
                ERROR_INVALID_DATA);
        }
        validate_accepted_release_state();

        const auto dataRoot = ptlsmr::program_data_root();
        const std::array statePaths{
            ptlsmr::engine_state_path(),
            dataRoot / L"engine-version-floor.txt",
            dataRoot / L"runtime-version-floor-track1.txt",
            dataRoot / L"runtime-version-floor-track2.txt",
            ptlsmr::accepted_release_state_path(),
            ptlsmr::lease_state_path(),
            dataRoot / L"runtime-inventory.txt",
        };
        for (const auto& path : statePaths)
        {
            if (std::filesystem::exists(path.wstring() + L".new"))
            {
                throw ptlsmr::win32_error(
                    "interrupted protected state replacement",
                    ERROR_INVALID_DATA);
            }
        }
    }

    [[nodiscard]] bool state_initialized_marker_present()
        {
            auto key = open_endpoint_registry_key(KEY_QUERY_VALUE | WRITE_DAC);
            protect_endpoint_registry_key(key.get());
            DWORD type = 0;
            DWORD value = 0;
            DWORD bytes = sizeof(value);
            const LSTATUS status = RegQueryValueExW(
                key.get(),
                ptlsmr::StateInitializedRegistryValue,
                nullptr,
                &type,
                reinterpret_cast<BYTE*>(&value),
                &bytes);
            if (status == ERROR_FILE_NOT_FOUND)
            {
                return false;
            }
            if (status != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("RegQueryValueExW(state initialized)", status);
            }
            if (type != REG_DWORD || bytes != sizeof(value) || value != 1)
            {
                throw ptlsmr::win32_error(
                    "state initialized registry policy",
                    ERROR_INVALID_DATA);
            }
            return true;
        }

        void set_state_initialized_marker()
        {
            auto key = open_endpoint_registry_key(KEY_SET_VALUE | WRITE_DAC);
            protect_endpoint_registry_key(key.get());
            constexpr DWORD value = 1;
            const LSTATUS status = RegSetValueExW(
                key.get(),
                ptlsmr::StateInitializedRegistryValue,
                0,
                REG_DWORD,
                reinterpret_cast<const BYTE*>(&value),
                sizeof(value));
            if (status != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("RegSetValueExW(state initialized)", status);
            }
            const LSTATUS flush = RegFlushKey(key.get());
            if (flush != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error("RegFlushKey(state initialized)", flush);
            }
        }

        void initialize_or_validate_mutable_state()
        {
            const auto dataRoot = ptlsmr::program_data_root();
            const std::array statePaths{
                ptlsmr::engine_state_path(),
                dataRoot / L"engine-version-floor.txt",
                dataRoot / L"runtime-version-floor-track1.txt",
                dataRoot / L"runtime-version-floor-track2.txt",
                ptlsmr::accepted_release_state_path(),
                ptlsmr::lease_state_path(),
                dataRoot / L"runtime-inventory.txt",
            };
            size_t existing = 0;
            bool replacementExists = false;
            for (const auto& path : statePaths)
            {
                if (std::filesystem::exists(path))
                {
                    if (!std::filesystem::is_regular_file(path))
                    {
                        throw ptlsmr::win32_error(
                            "mutable state file type policy",
                            ERROR_INVALID_DATA);
                    }
                    ++existing;
                }
                replacementExists =
                    replacementExists || std::filesystem::exists(path.wstring() + L".new");
            }
            const bool initialized = state_initialized_marker_present();
            if (replacementExists ||
                (existing != 0 && existing != statePaths.size()) ||
                (initialized && existing != statePaths.size()))
            {
                throw ptlsmr::win32_error(
                    "partial mutable state initialization",
                    ERROR_INVALID_DATA);
            }
            if (existing == 0)
            {
                ptlsmr::write_utf8_file_atomic(
                    ptlsmr::engine_state_path(),
                    ptlsmr::InitialEngineVersion);
                ptlsmr::write_utf8_file_atomic(
                    dataRoot / L"engine-version-floor.txt",
                    ptlsmr::InitialEngineVersion);
                ptlsmr::write_utf8_file_atomic(
                    dataRoot / L"runtime-version-floor-track1.txt",
                    L"1.0.0.0");
                ptlsmr::write_utf8_file_atomic(
                    dataRoot / L"runtime-version-floor-track2.txt",
                    L"2.0.0.0");
                ptlsmr::write_utf8_file_atomic(
                    ptlsmr::accepted_release_state_path(),
                    L"schema=1\r\nepoch=100\r\n");
                ptlsmr::write_utf8_file_atomic(ptlsmr::lease_state_path(), L"");
                ptlsmr::write_utf8_file_atomic(dataRoot / L"runtime-inventory.txt", L"");
            }
            for (const auto& path : statePaths)
            {
                ptlsmr::protect_system_file(path);
            }
            validate_nontransactional_protected_state();
            if (!initialized)
            {
                set_state_initialized_marker();
            }
        }

        [[nodiscard]] constexpr bool exact_tombstone_name(
            std::wstring_view value,
            std::wstring_view prefix)
        {
            return value.starts_with(prefix) &&
                value.size() == prefix.size() + 32 &&
                std::all_of(
                    value.begin() + prefix.size(),
                    value.end(),
                    [](wchar_t character) {
                        return (character >= L'0' && character <= L'9') ||
                            (character >= L'a' && character <= L'f');
                    });
        }

        void remove_exact_directory_retry(
            const std::filesystem::path& directory,
            const char* operation)
        {
            DWORD lastError = ERROR_SUCCESS;
            for (size_t attempt = 0; attempt < 40; ++attempt)
            {
                const DWORD attributes = GetFileAttributesW(directory.c_str());
                if (attributes == INVALID_FILE_ATTRIBUTES)
                {
                    const DWORD error = GetLastError();
                    if (error == ERROR_FILE_NOT_FOUND || error == ERROR_PATH_NOT_FOUND)
                    {
                        return;
                    }
                    throw ptlsmr::win32_error(operation, error);
                }
                if ((attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
                    (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
                {
                    throw ptlsmr::win32_error(operation, ERROR_REPARSE_TAG_INVALID);
                }
                std::error_code error;
                std::filesystem::remove_all(directory, error);
                if (!error)
                {
                    continue;
                }
                lastError = static_cast<DWORD>(error.value());
                if (lastError != ERROR_SHARING_VIOLATION &&
                    lastError != ERROR_ACCESS_DENIED &&
                    lastError != ERROR_DIR_NOT_EMPTY)
                {
                    throw ptlsmr::win32_error(operation, lastError);
                }
                Sleep(100);
            }
            throw ptlsmr::win32_error(
                operation,
                lastError == ERROR_SUCCESS ? ERROR_TIMEOUT : lastError);
        }

        void remove_exact_tombstones(const std::filesystem::path& root)
        {
            const auto parent = root.parent_path();
            const auto prefix = root.filename().wstring() + L".PtPuvrDelete-";
            std::error_code error;
            if (!std::filesystem::exists(parent, error))
            {
                if (error)
                {
                    throw ptlsmr::win32_error(
                        "enumerate cleanup tombstone parent",
                        static_cast<DWORD>(error.value()));
                }
                return;
            }
            if (!std::filesystem::is_directory(parent, error) || error)
            {
                throw ptlsmr::win32_error(
                    "cleanup tombstone parent type",
                    error ? static_cast<DWORD>(error.value()) : ERROR_DIRECTORY);
            }
            std::vector<std::filesystem::path> tombstones;
            for (std::filesystem::directory_iterator iterator(parent, error), end;
                 iterator != end;
                 iterator.increment(error))
            {
                if (error)
                {
                    throw ptlsmr::win32_error(
                        "enumerate cleanup tombstones",
                        static_cast<DWORD>(error.value()));
                }
                const auto name = iterator->path().filename().wstring();
                if (exact_tombstone_name(name, prefix))
                {
                    tombstones.push_back(iterator->path());
                }
            }
            if (error)
            {
                throw ptlsmr::win32_error(
                    "enumerate cleanup tombstones",
                    static_cast<DWORD>(error.value()));
            }
            for (const auto& tombstone : tombstones)
            {
                remove_exact_directory_retry(
                    tombstone,
                    "remove exact cleanup tombstone");
            }
        }

        void remove_exact_root_via_tombstone(const std::filesystem::path& root)
        {
            remove_exact_tombstones(root);
            const DWORD attributes = GetFileAttributesW(root.c_str());
            if (attributes == INVALID_FILE_ATTRIBUTES)
            {
                const DWORD error = GetLastError();
                if (error != ERROR_FILE_NOT_FOUND && error != ERROR_PATH_NOT_FOUND)
                {
                    throw ptlsmr::win32_error("query exact cleanup root", error);
                }
                return;
            }
            if ((attributes & FILE_ATTRIBUTE_DIRECTORY) == 0 ||
                (attributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
            {
                throw ptlsmr::win32_error(
                    "exact cleanup root type",
                    ERROR_REPARSE_TAG_INVALID);
            }
            const auto tombstone = root.parent_path() /
                (root.filename().wstring() + L".PtPuvrDelete-" +
                 ptlsmr::random_hex_identifier(16));
            ptlsmr::check_bool(
                MoveFileExW(root.c_str(), tombstone.c_str(), MOVEFILE_WRITE_THROUGH),
                "MoveFileExW(exact cleanup root tombstone)");
            remove_exact_directory_retry(
                tombstone,
                "remove renamed exact cleanup root");
            remove_exact_tombstones(root);
            const DWORD remaining = GetFileAttributesW(root.c_str());
            if (remaining != INVALID_FILE_ATTRIBUTES ||
                (GetLastError() != ERROR_FILE_NOT_FOUND &&
                 GetLastError() != ERROR_PATH_NOT_FOUND))
            {
                throw ptlsmr::win32_error(
                    "exact cleanup root absence",
                    remaining == INVALID_FILE_ATTRIBUTES ? GetLastError() : ERROR_DIR_NOT_EMPTY);
            }
        }

        [[nodiscard]] registry_key open_cleanup_outcome_key(REGSAM access)
        {
            HKEY raw = nullptr;
            const LSTATUS status = RegCreateKeyExW(
                HKEY_LOCAL_MACHINE,
                ptlsmr::CleanupOutcomeRegistryKey,
                0,
                nullptr,
                REG_OPTION_NON_VOLATILE,
                access | KEY_WOW64_64KEY,
                nullptr,
                &raw,
                nullptr);
            if (status != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "RegCreateKeyExW(cleanup outcome)",
                    status);
            }
            return registry_key(raw);
        }

        void protect_cleanup_outcome_key(HKEY key)
        {
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(
                    L"O:SYG:SYD:P(A;;KA;;;SY)(A;;KA;;;BA)",
                    SDDL_REVISION_1,
                    &descriptor,
                    nullptr))
            {
                throw ptlsmr::win32_error(
                    "ConvertStringSecurityDescriptorToSecurityDescriptorW(cleanup outcome)",
                    GetLastError());
            }
            ptlsmr::local_memory security(descriptor);
            const LSTATUS status = RegSetKeySecurity(
                key,
                DACL_SECURITY_INFORMATION | PROTECTED_DACL_SECURITY_INFORMATION,
                descriptor);
            if (status != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "RegSetKeySecurity(cleanup outcome)",
                    status);
            }
        }

        void write_cleanup_outcome(
            std::wstring_view nonce,
            DWORD status,
            std::wstring_view stage)
        {
            if (nonce.size() != 32 ||
                !std::all_of(nonce.begin(), nonce.end(), [](wchar_t character) {
                    return (character >= L'0' && character <= L'9') ||
                        (character >= L'a' && character <= L'f');
                }) ||
                stage.empty() ||
                stage.size() > 128)
            {
                throw ptlsmr::win32_error(
                    "cleanup outcome value policy",
                    ERROR_INVALID_DATA);
            }
            auto key = open_cleanup_outcome_key(
                KEY_QUERY_VALUE | KEY_SET_VALUE | WRITE_DAC);
            protect_cleanup_outcome_key(key.get());
            FILETIME fileTime{};
            GetSystemTimeAsFileTime(&fileTime);
            ULARGE_INTEGER timestamp{};
            timestamp.LowPart = fileTime.dwLowDateTime;
            timestamp.HighPart = fileTime.dwHighDateTime;
            const auto setString = [&](const wchar_t* name, std::wstring_view value) {
                const DWORD bytes =
                    static_cast<DWORD>((value.size() + 1) * sizeof(wchar_t));
                const LSTATUS result = RegSetValueExW(
                    key.get(),
                    name,
                    0,
                    REG_SZ,
                    reinterpret_cast<const BYTE*>(value.data()),
                    bytes);
                if (result != ERROR_SUCCESS)
                {
                    throw ptlsmr::win32_error(
                        "RegSetValueExW(cleanup outcome string)",
                        result);
                }
            };
            setString(ptlsmr::CleanupNonceRegistryValue, nonce);
            setString(ptlsmr::CleanupStageRegistryValue, stage);
            LSTATUS result = RegSetValueExW(
                key.get(),
                ptlsmr::CleanupTimestampRegistryValue,
                0,
                REG_QWORD,
                reinterpret_cast<const BYTE*>(&timestamp.QuadPart),
                sizeof(timestamp.QuadPart));
            if (result != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "RegSetValueExW(cleanup outcome timestamp)",
                    result);
            }
            result = RegSetValueExW(
                key.get(),
                ptlsmr::CleanupStatusRegistryValue,
                0,
                REG_DWORD,
                reinterpret_cast<const BYTE*>(&status),
                sizeof(status));
            if (result != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "RegSetValueExW(cleanup outcome status)",
                    result);
            }
            result = RegFlushKey(key.get());
            if (result != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "RegFlushKey(cleanup outcome)",
                    result);
            }
        }

        void remove_endpoint_registry_tree()
        {
            LSTATUS status = RegDeleteTreeW(
                HKEY_LOCAL_MACHINE,
                ptlsmr::ControlPlaneRegistryKey);
            if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND &&
                status != ERROR_PATH_NOT_FOUND)
            {
                throw ptlsmr::win32_error(
                    "RegDeleteTreeW(control-plane endpoint)",
                    status);
            }
            status = RegDeleteKeyExW(
                HKEY_LOCAL_MACHINE,
                ptlsmr::ControlPlaneRegistryKey,
                KEY_WOW64_64KEY,
                0);
            if (status != ERROR_SUCCESS && status != ERROR_FILE_NOT_FOUND &&
                status != ERROR_PATH_NOT_FOUND)
            {
                throw ptlsmr::win32_error(
                    "RegDeleteKeyExW(control-plane endpoint)",
                    status);
            }
            HKEY remaining = nullptr;
            status = RegOpenKeyExW(
                HKEY_LOCAL_MACHINE,
                ptlsmr::ControlPlaneRegistryKey,
                0,
                KEY_QUERY_VALUE | KEY_WOW64_64KEY,
                &remaining);
            if (status == ERROR_SUCCESS)
            {
                RegCloseKey(remaining);
                throw ptlsmr::win32_error(
                    "control-plane endpoint registry key absence",
                    ERROR_DIR_NOT_EMPTY);
            }
            if (status != ERROR_FILE_NOT_FOUND && status != ERROR_PATH_NOT_FOUND)
            {
                throw ptlsmr::win32_error(
                    "RegOpenKeyExW(control-plane endpoint absence)",
                    status);
            }
        }

        [[nodiscard]] DWORD run_commit_cleanup() noexcept
        {
            std::wstring nonce;
            DWORD outcome = ERROR_SUCCESS;
            std::wstring stage = L"starting";
            try
            {
                nonce = ptlsmr::random_hex_identifier(16);
            }
            catch (const ptlsmr::win32_error& error)
            {
                outcome = error.code();
            }
            catch (...)
            {
                outcome = ERROR_UNHANDLED_EXCEPTION;
            }
            if (nonce.size() != 32)
            {
                return outcome == ERROR_SUCCESS ? ERROR_INVALID_DATA : outcome;
            }
            try
            {
                write_cleanup_outcome(nonce, ERROR_IO_PENDING, stage);
            }
            catch (const ptlsmr::win32_error& error)
            {
                if (outcome == ERROR_SUCCESS)
                {
                    outcome = error.code();
                    stage = L"outcome-start-write-failed";
                }
            }
            catch (...)
            {
                if (outcome == ERROR_SUCCESS)
                {
                    outcome = ERROR_UNHANDLED_EXCEPTION;
                    stage = L"outcome-start-write-failed";
                }
            }

            const auto runStep = [&](std::wstring_view currentStage, const auto& operation) {
                try
                {
                    operation();
                }
                catch (const ptlsmr::win32_error& error)
                {
                    if (outcome == ERROR_SUCCESS)
                    {
                        outcome = error.code();
                        stage.assign(currentStage);
                    }
                }
                catch (const std::filesystem::filesystem_error& error)
                {
                    if (outcome == ERROR_SUCCESS)
                    {
                        outcome = static_cast<DWORD>(error.code().value());
                        stage.assign(currentStage);
                    }
                }
                catch (...)
                {
                    if (outcome == ERROR_SUCCESS)
                    {
                        outcome = ERROR_UNHANDLED_EXCEPTION;
                        stage.assign(currentStage);
                    }
                }
            };
            runStep(L"install-root-cleanup-failed", [] {
                remove_exact_root_via_tombstone(ptlsmr::installation_root());
            });
            runStep(L"program-data-root-cleanup-failed", [] {
                remove_exact_root_via_tombstone(ptlsmr::program_data_root());
            });
            runStep(L"endpoint-registry-cleanup-failed", [] {
                remove_endpoint_registry_tree();
            });
            if (outcome == ERROR_SUCCESS)
            {
                stage = L"complete";
            }
            try
            {
                write_cleanup_outcome(nonce, outcome, stage);
            }
            catch (const ptlsmr::win32_error& error)
            {
                return error.code();
            }
            catch (...)
            {
                return ERROR_UNHANDLED_EXCEPTION;
            }
            return outcome;
        }

    int uninstall_operation(bool cleanup)
    {
        if (!process_is_elevated_administrator())
        {
            return ERROR_ELEVATION_REQUIRED;
        }
        if (!cleanup)
        {
            validate_nontransactional_protected_state();
            require_zero_leases();
            require_zero_inventory();
            require_no_runtime_services();
            require_no_pending_journals();
        }
        if (cleanup)
        {
            return static_cast<int>(run_commit_cleanup());
        }
        return ERROR_SUCCESS;
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
            ptlsmr::HostServiceName,
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
                throw ptlsmr::win32_error("host LocalSystem token policy", ERROR_ACCESS_DENIED);
            }
            ptlsmr::protect_system_directory(ptlsmr::installation_root());
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            ptlsmr::protect_system_directory(ptlsmr::requests_root());
            protect_msi_owned_bootstrap_files();
            initialize_local_fixed_drive_mask();
            if (!equal_path(module_path(), ptlsmr::host_executable_path()))
            {
                throw ptlsmr::win32_error("host fixed execution path policy", ERROR_ACCESS_DENIED);
            }
            const std::wstring codePin = ptlsmr::read_code_signer_pin();
            (void)ptlsmr::validate_host_candidate(module_path(), codePin);
            validate_installed_policy();
            (void)ptlsmr::validate_engine_candidate(
                ptlsmr::engine_executable_path(
                    ptlsmr::parse_version(ptlsmr::InitialEngineVersion)),
                codePin);
            recover_mutable_state_replacements();
            initialize_or_validate_mutable_state();
            recover_engine_activation();
            cleanup_abandoned_release_stages();
            g_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!g_stopEvent)
            {
                throw ptlsmr::win32_error("CreateEventW(host stop)", GetLastError());
            }
            g_dispatchMutex.reset(CreateMutexW(nullptr, FALSE, nullptr));
            if (!g_dispatchMutex)
            {
                throw ptlsmr::win32_error("CreateMutexW(host dispatch)", GetLastError());
            }
            clear_published_endpoint();
            std::wstring endpoint;
            auto pipes = create_host_pipes(endpoint);
            publish_endpoint(endpoint);
            write_host_evidence();
            std::mutex workerFailureMutex;
            std::exception_ptr workerFailure;
            std::vector<std::thread> workers;
            workers.reserve(pipes.size());
            try
            {
                for (auto& pipe : pipes)
                {
                    workers.emplace_back([&workerFailureMutex, &workerFailure, handle = pipe.get()] {
                        try
                        {
                            pipe_server(handle);
                        }
                        catch (...)
                        {
                            {
                                std::lock_guard lock(workerFailureMutex);
                                if (!workerFailure)
                                {
                                    workerFailure = std::current_exception();
                                }
                            }
                            (void)SetEvent(g_stopEvent.get());
                        }
                    });
                }
            }
            catch (...)
            {
                (void)SetEvent(g_stopEvent.get());
                for (auto& worker : workers)
                {
                    worker.join();
                }
                throw;
            }
            report_status(SERVICE_RUNNING);
            (void)WaitForSingleObject(g_stopEvent.get(), INFINITE);
            report_status(SERVICE_STOP_PENDING);
            for (auto& worker : workers)
            {
                worker.join();
            }
            clear_published_endpoint();
            {
                std::lock_guard lock(g_activeConnectionMutex);
                if (!g_activeConnections.empty())
                {
                    throw ptlsmr::win32_error(
                        "host active connection drain",
                        ERROR_BUSY);
                }
            }
            if (workerFailure)
            {
                std::rethrow_exception(workerFailure);
            }
            report_status(SERVICE_STOPPED);
        }
        catch (const ptlsmr::win32_error& error)
        {
            clear_published_endpoint_noexcept();
            report_status(SERVICE_STOPPED, error.code());
        }
        catch (...)
        {
            clear_published_endpoint_noexcept();
            report_status(SERVICE_STOPPED, ERROR_UNHANDLED_EXCEPTION);
        }
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if (arguments.size() == 2 && arguments[1] == L"--msi-uninstall-check")
        {
            return uninstall_operation(false);
        }
        if (arguments.size() == 2 && arguments[1] == L"--msi-uninstall-cleanup")
        {
            return uninstall_operation(true);
        }
        if (arguments.size() != 1)
        {
            return ERROR_INVALID_PARAMETER;
        }
        wchar_t serviceName[] = L"PtPuvrHost";
        SERVICE_TABLE_ENTRYW table[] = {
            { serviceName, service_main },
            { nullptr, nullptr },
        };
        if (!StartServiceCtrlDispatcherW(table))
        {
            return static_cast<int>(GetLastError());
        }
        return ERROR_SUCCESS;
    }
    catch (const ptlsmr::win32_error& error)
    {
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
