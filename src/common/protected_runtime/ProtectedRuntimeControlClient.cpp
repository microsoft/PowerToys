#include "ProtectedRuntimeControlClient.h"

#include <windows.h>
#include <winsvc.h>

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
#include <aclapi.h>
#include <sddl.h>
#endif

#include <algorithm>
#include <array>
#include <utility>

namespace
{
    class unique_handle
    {
    public:
        explicit unique_handle(HANDLE value = nullptr) noexcept :
            m_value(value)
        {
        }

        ~unique_handle()
        {
            if (m_value && m_value != INVALID_HANDLE_VALUE)
            {
                CloseHandle(m_value);
            }
        }

        unique_handle(const unique_handle&) = delete;
        unique_handle& operator=(const unique_handle&) = delete;
        unique_handle(unique_handle&& other) noexcept :
            m_value(std::exchange(other.m_value, nullptr))
        {
        }

        [[nodiscard]] HANDLE get() const noexcept
        {
            return m_value;
        }

    private:
        HANDLE m_value{};
    };

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

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
    class local_memory
    {
    public:
        explicit local_memory(void* value = nullptr) noexcept :
            m_value(value)
        {
        }

        ~local_memory()
        {
            if (m_value)
            {
                LocalFree(m_value);
            }
        }

        local_memory(const local_memory&) = delete;
        local_memory& operator=(const local_memory&) = delete;

    private:
        void* m_value{};
    };
#endif

    void check_bool(BOOL result, const char* operation)
    {
        if (!result)
        {
            throw powertoys::protected_runtime::control_error(operation, GetLastError());
        }
    }

    void check_transfer(
        BOOL result,
        DWORD transferred,
        size_t expected,
        const char* operation)
    {
        if (!result)
        {
            throw powertoys::protected_runtime::control_error(operation, GetLastError());
        }
        if (transferred != expected)
        {
            throw powertoys::protected_runtime::control_error(operation, ERROR_INVALID_DATA);
        }
    }

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw powertoys::protected_runtime::control_error(
                "protected runtime request field",
                ERROR_BUFFER_OVERFLOW);
        }
        std::copy(source.begin(), source.end(), destination);
        destination[source.size()] = L'\0';
    }

    [[nodiscard]] DWORD host_service_pid()
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw powertoys::protected_runtime::control_error(
                "OpenSCManagerW(protected runtime client)",
                GetLastError());
        }
        service_handle service(OpenServiceW(
            scm.get(),
            powertoys::protected_runtime::protocol::host_service_name,
            SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw powertoys::protected_runtime::control_error(
                "OpenServiceW(protected runtime client)",
                GetLastError());
        }
        SERVICE_STATUS_PROCESS status{};
        DWORD bytes = 0;
        check_bool(
            QueryServiceStatusEx(
                service.get(),
                SC_STATUS_PROCESS_INFO,
                reinterpret_cast<BYTE*>(&status),
                sizeof(status),
                &bytes),
            "QueryServiceStatusEx(protected runtime client)");
        if (status.dwCurrentState != SERVICE_RUNNING || status.dwProcessId == 0)
        {
            throw powertoys::protected_runtime::control_error(
                "protected runtime host service state",
                ERROR_SERVICE_NOT_ACTIVE);
        }
        return status.dwProcessId;
    }

    [[nodiscard]] std::wstring read_host_endpoint()
    {
        std::array<wchar_t, 256> value{};
        DWORD bytes = static_cast<DWORD>(value.size() * sizeof(wchar_t));
        const LSTATUS status = RegGetValueW(
            HKEY_LOCAL_MACHINE,
            powertoys::protected_runtime::protocol::control_plane_registry_key,
            powertoys::protected_runtime::protocol::host_endpoint_registry_value,
            RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY,
            nullptr,
            value.data(),
            &bytes);
        if (status != ERROR_SUCCESS)
        {
            throw powertoys::protected_runtime::control_error(
                "RegGetValueW(protected runtime endpoint)",
                status);
        }
        if (bytes < sizeof(wchar_t) ||
            bytes > value.size() * sizeof(wchar_t) ||
            bytes % sizeof(wchar_t) != 0 ||
            value[bytes / sizeof(wchar_t) - 1] != L'\0')
        {
            throw powertoys::protected_runtime::control_error(
                "protected runtime endpoint registry value",
                ERROR_INVALID_DATA);
        }

        const std::wstring endpoint(value.data());
        const std::wstring_view prefix(
            powertoys::protected_runtime::protocol::host_pipe_prefix);
        if (!endpoint.starts_with(prefix) ||
            endpoint.size() != prefix.size() + 32 ||
            !std::all_of(
                endpoint.begin() + prefix.size(),
                endpoint.end(),
                [](wchar_t character) {
                    return (character >= L'0' && character <= L'9') ||
                        (character >= L'a' && character <= L'f');
                }))
        {
            throw powertoys::protected_runtime::control_error(
                "protected runtime endpoint name",
                ERROR_INVALID_DATA);
        }
        return endpoint;
    }

    [[nodiscard]] unique_handle connect_bound_pipe(DWORD additional_access = 0)
    {
        for (DWORD attempt = 0; attempt < 100; ++attempt)
        {
            std::wstring endpoint;
            DWORD host_pid = 0;
            try
            {
                endpoint = read_host_endpoint();
                host_pid = host_service_pid();
            }
            catch (const powertoys::protected_runtime::control_error& error)
            {
                if (error.code() == ERROR_FILE_NOT_FOUND ||
                    error.code() == ERROR_SERVICE_NOT_ACTIVE)
                {
                    Sleep(100);
                    continue;
                }
                throw;
            }

            HANDLE raw_pipe = CreateFileW(
                endpoint.c_str(),
                FILE_READ_DATA | FILE_WRITE_DATA | SYNCHRONIZE | additional_access,
                0,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (raw_pipe != INVALID_HANDLE_VALUE)
            {
                unique_handle pipe(raw_pipe);
                DWORD server_pid = 0;
                if (!GetNamedPipeServerProcessId(pipe.get(), &server_pid) ||
                    server_pid != host_pid)
                {
                    throw powertoys::protected_runtime::control_error(
                        "protected runtime host pipe PID binding",
                        ERROR_ACCESS_DENIED);
                }
                return pipe;
            }

            const DWORD error = GetLastError();
            if (error == ERROR_FILE_NOT_FOUND)
            {
                Sleep(100);
                continue;
            }
            if (error != ERROR_PIPE_BUSY)
            {
                throw powertoys::protected_runtime::control_error(
                    "CreateFileW(protected runtime pipe)",
                    error);
            }
            if (!WaitNamedPipeW(endpoint.c_str(), 500))
            {
                const DWORD wait_error = GetLastError();
                if (wait_error != ERROR_FILE_NOT_FOUND &&
                    wait_error != ERROR_SEM_TIMEOUT)
                {
                    throw powertoys::protected_runtime::control_error(
                        "WaitNamedPipeW(protected runtime pipe)",
                        wait_error);
                }
            }
        }
        throw powertoys::protected_runtime::control_error(
            "protected runtime host pipe connect timeout",
            ERROR_TIMEOUT);
    }

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
    [[nodiscard]] powertoys::protected_runtime::pipe_inspection inspect_pipe(
        HANDLE pipe)
    {
        powertoys::protected_runtime::pipe_inspection result{};
        DWORD flags = 0;
        DWORD out_buffer_size = 0;
        DWORD in_buffer_size = 0;
        check_bool(
            GetNamedPipeInfo(
                pipe,
                &flags,
                &out_buffer_size,
                &in_buffer_size,
                &result.maximum_instances),
            "GetNamedPipeInfo(protected runtime test)");

        PACL dacl = nullptr;
        PSECURITY_DESCRIPTOR raw_descriptor = nullptr;
        const DWORD security_status = GetSecurityInfo(
            pipe,
            SE_KERNEL_OBJECT,
            DACL_SECURITY_INFORMATION,
            nullptr,
            nullptr,
            &dacl,
            nullptr,
            &raw_descriptor);
        local_memory descriptor(raw_descriptor);
        if (security_status != ERROR_SUCCESS)
        {
            throw powertoys::protected_runtime::control_error(
                "GetSecurityInfo(protected runtime test)",
                security_status);
        }
        if (!dacl)
        {
            throw powertoys::protected_runtime::control_error(
                "protected runtime pipe DACL",
                ERROR_INVALID_SECURITY_DESCR);
        }

        PSID raw_authenticated_users = nullptr;
        check_bool(
            ConvertStringSidToSidW(L"S-1-5-11", &raw_authenticated_users),
            "ConvertStringSidToSidW(protected runtime test)");
        local_memory authenticated_users(raw_authenticated_users);
        TRUSTEEW trustee{};
        BuildTrusteeWithSidW(&trustee, raw_authenticated_users);
        const DWORD rights_status = GetEffectiveRightsFromAclW(
            dacl,
            &trustee,
            &result.authenticated_users_rights);
        if (rights_status != ERROR_SUCCESS)
        {
            throw powertoys::protected_runtime::control_error(
                "GetEffectiveRightsFromAclW(protected runtime test)",
                rights_status);
        }
        return result;
    }
#endif

    [[nodiscard]] powertoys::protected_runtime::control_reply invoke_impl(
        powertoys::protected_runtime::control_command command,
        std::wstring_view release_id
#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
        ,
        DWORD hold_milliseconds,
        bool hold_before_preface,
        powertoys::protected_runtime::pipe_inspection_callback callback,
        void* callback_context
#endif
    )
    {
        using namespace powertoys::protected_runtime;
        if (command == control_command::acquire)
        {
            if (!valid_release_id(release_id))
            {
                throw control_error("protected runtime release ID", ERROR_INVALID_PARAMETER);
            }
        }
        else if (!release_id.empty())
        {
            throw control_error("protected runtime release ID policy", ERROR_INVALID_PARAMETER);
        }

        protocol::control_request request{};
        request.command = static_cast<uint16_t>(command);
        copy_bounded(
            request.releaseId,
            std::size(request.releaseId),
            release_id);
        auto pipe = connect_bound_pipe(
#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
            hold_milliseconds == 0 ? 0 : READ_CONTROL
#else
            0
#endif
        );

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
        if (hold_milliseconds != 0 && hold_before_preface)
        {
            const auto inspection = inspect_pipe(pipe.get());
            if (callback)
            {
                callback(inspection, callback_context);
            }
            Sleep(hold_milliseconds);
        }
#endif

        protocol::authentication_preface preface{};
        DWORD transferred = 0;
        const BOOL wrote_preface = WriteFile(
            pipe.get(),
            &preface,
            sizeof(preface),
            &transferred,
            nullptr);
        check_transfer(
            wrote_preface,
            transferred,
            sizeof(preface),
            "WriteFile(protected runtime authentication preface)");

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
        if (hold_milliseconds != 0 && !hold_before_preface)
        {
            const auto inspection = inspect_pipe(pipe.get());
            if (callback)
            {
                callback(inspection, callback_context);
            }
            Sleep(hold_milliseconds);
        }
#endif

        transferred = 0;
        const BOOL wrote_request = WriteFile(
            pipe.get(),
            &request,
            sizeof(request),
            &transferred,
            nullptr);
        check_transfer(
            wrote_request,
            transferred,
            sizeof(request),
            "WriteFile(protected runtime request)");

        protocol::control_reply wire_reply{};
        transferred = 0;
        const BOOL read_reply = ReadFile(
            pipe.get(),
            &wire_reply,
            sizeof(wire_reply),
            &transferred,
            nullptr);
        check_transfer(
            read_reply,
            transferred,
            sizeof(wire_reply),
            "ReadFile(protected runtime response)");
        if (wire_reply.magic != protocol::magic ||
            wire_reply.version != protocol::version ||
            wire_reply.command != request.command ||
            wire_reply.runtimeVersion[std::size(wire_reply.runtimeVersion) - 1] != L'\0' ||
            wire_reply.activeEngineVersion[
                std::size(wire_reply.activeEngineVersion) - 1] != L'\0' ||
            wire_reply.detail[std::size(wire_reply.detail) - 1] != L'\0')
        {
            throw control_error("protected runtime response protocol", ERROR_INVALID_DATA);
        }

        control_reply reply;
        reply.win32_status = wire_reply.win32Status;
        reply.scm_state = wire_reply.scmState;
        reply.process_id = wire_reply.processId;
        reply.lease_count = wire_reply.leaseCount;
        reply.runtime_version = wire_reply.runtimeVersion;
        reply.active_engine_version = wire_reply.activeEngineVersion;
        reply.detail = wire_reply.detail;
        return reply;
    }
}

namespace powertoys::protected_runtime
{
    control_error::control_error(const char* operation, DWORD error) :
        std::runtime_error(operation),
        m_code(error)
    {
    }

    DWORD control_error::code() const noexcept
    {
        return m_code;
    }

    bool valid_release_id(std::wstring_view value) noexcept
    {
        if (value.size() < 11 ||
            value.size() >= protocol::max_release_id_chars ||
            !value.starts_with(L"release-"))
        {
            return false;
        }
        bool digit_seen = false;
        for (size_t index = 8; index < value.size(); ++index)
        {
            const wchar_t character = value[index];
            if (character >= L'0' && character <= L'9')
            {
                digit_seen = true;
            }
            else if (!((character >= L'a' && character <= L'z') ||
                       character == L'-'))
            {
                return false;
            }
        }
        return digit_seen && value.back() != L'-';
    }

    control_reply invoke(control_command command, std::wstring_view release_id)
    {
        return invoke_impl(
            command,
            release_id
#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
            ,
            0,
            false,
            nullptr,
            nullptr
#endif
        );
    }

#ifdef POWERTOYS_PROTECTED_RUNTIME_TEST_HOOKS
    control_reply invoke_with_test_hold(
        control_command command,
        std::wstring_view release_id,
        DWORD hold_milliseconds,
        bool hold_before_preface,
        pipe_inspection_callback callback,
        void* callback_context)
    {
        if (hold_milliseconds == 0 || hold_milliseconds > 30000)
        {
            throw control_error(
                "protected runtime test hold duration",
                ERROR_INVALID_PARAMETER);
        }
        return invoke_impl(
            command,
            release_id,
            hold_milliseconds,
            hold_before_preface,
            callback,
            callback_context);
    }
#endif
}
