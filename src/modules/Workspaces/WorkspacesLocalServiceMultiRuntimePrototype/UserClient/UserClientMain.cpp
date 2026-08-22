#include "../Common/LsmrCommon.h"

#include <aclapi.h>
#include <shellapi.h>
#include <sddl.h>

#include <algorithm>
#include <array>
#include <iostream>

namespace
{
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

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw ptlsmr::win32_error("user-client bounded request field", ERROR_BUFFER_OVERFLOW);
        }
        std::copy(source.begin(), source.end(), destination);
        destination[source.size()] = L'\0';
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
            }
            else if (!((character >= L'a' && character <= L'z') || character == L'-'))
            {
                return false;
            }
        }
        return digitSeen && value.back() != L'-';
    }

    [[nodiscard]] DWORD host_service_pid()
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(user client)", GetLastError());
        }
        service_handle service(OpenServiceW(
            scm.get(),
            ptlsmr::HostServiceName,
            SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(user client)", GetLastError());
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
            "QueryServiceStatusEx(user client)");
        if (status.dwCurrentState != SERVICE_RUNNING || status.dwProcessId == 0)
        {
            throw ptlsmr::win32_error("host service running policy", ERROR_SERVICE_NOT_ACTIVE);
        }
        return status.dwProcessId;
    }

    [[nodiscard]] std::wstring read_host_endpoint()
    {
        std::array<wchar_t, 256> value{};
        DWORD bytes = static_cast<DWORD>(value.size() * sizeof(wchar_t));
        const LSTATUS status = RegGetValueW(
            HKEY_LOCAL_MACHINE,
            ptlsmr::ControlPlaneRegistryKey,
            ptlsmr::HostEndpointRegistryValue,
            RRF_RT_REG_SZ | RRF_SUBKEY_WOW6464KEY,
            nullptr,
            value.data(),
            &bytes);
        if (status != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("RegGetValueW(host endpoint)", status);
        }
        if (bytes < sizeof(wchar_t) || bytes > value.size() * sizeof(wchar_t) ||
            bytes % sizeof(wchar_t) != 0 ||
            value[bytes / sizeof(wchar_t) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("host endpoint registry size policy", ERROR_INVALID_DATA);
        }
        value[value.size() - 1] = L'\0';
        const std::wstring endpoint(value.data());
        const std::wstring_view prefix(ptlsmr::HostPipePrefix);
        if (!endpoint.starts_with(prefix) || endpoint.size() != prefix.size() + 32 ||
            !std::all_of(endpoint.begin() + prefix.size(), endpoint.end(), [](wchar_t character) {
                return (character >= L'0' && character <= L'9') ||
                    (character >= L'a' && character <= L'f');
            }))
        {
            throw ptlsmr::win32_error("host endpoint registry value policy", ERROR_INVALID_DATA);
        }
        return endpoint;
    }

    struct pipe_inspection
    {
        DWORD maximumInstances{};
        ACCESS_MASK authenticatedUsersRights{};
    };

    [[nodiscard]] pipe_inspection inspect_pipe(HANDLE pipe)
    {
        pipe_inspection result{};
        DWORD flags = 0;
        DWORD outBufferSize = 0;
        DWORD inBufferSize = 0;
        ptlsmr::check_bool(
            GetNamedPipeInfo(
                pipe,
                &flags,
                &outBufferSize,
                &inBufferSize,
                &result.maximumInstances),
            "GetNamedPipeInfo(user client test)");

        PACL dacl = nullptr;
        PSECURITY_DESCRIPTOR rawDescriptor = nullptr;
        const DWORD securityStatus = GetSecurityInfo(
            pipe,
            SE_KERNEL_OBJECT,
            DACL_SECURITY_INFORMATION,
            nullptr,
            nullptr,
            &dacl,
            nullptr,
            &rawDescriptor);
        ptlsmr::local_memory descriptor(rawDescriptor);
        if (securityStatus != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error(
                "GetSecurityInfo(user client test)",
                securityStatus);
        }
        if (!dacl)
        {
            throw ptlsmr::win32_error(
                "host pipe DACL presence",
                ERROR_INVALID_SECURITY_DESCR);
        }

        PSID rawAuthenticatedUsers = nullptr;
        ptlsmr::check_bool(
            ConvertStringSidToSidW(L"S-1-5-11", &rawAuthenticatedUsers),
            "ConvertStringSidToSidW(user client test)");
        ptlsmr::local_memory authenticatedUsers(rawAuthenticatedUsers);
        TRUSTEEW trustee{};
        BuildTrusteeWithSidW(&trustee, rawAuthenticatedUsers);
        const DWORD rightsStatus = GetEffectiveRightsFromAclW(
            dacl,
            &trustee,
            &result.authenticatedUsersRights);
        if (rightsStatus != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error(
                "GetEffectiveRightsFromAclW(user client test)",
                rightsStatus);
        }
        return result;
    }

    [[nodiscard]] ptlsmr::unique_handle connect_bound_pipe(
        DWORD additionalAccess = 0)
    {
        for (DWORD attempt = 0; attempt < 100; ++attempt)
        {
            std::wstring endpoint;
            DWORD hostPid = 0;
            try
            {
                endpoint = read_host_endpoint();
                hostPid = host_service_pid();
            }
            catch (const ptlsmr::win32_error& error)
            {
                if (error.code() == ERROR_FILE_NOT_FOUND ||
                    error.code() == ERROR_SERVICE_NOT_ACTIVE)
                {
                    Sleep(100);
                    continue;
                }
                throw;
            }
            HANDLE raw = CreateFileW(
                endpoint.c_str(),
                FILE_READ_DATA | FILE_WRITE_DATA | SYNCHRONIZE | additionalAccess,
                0,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (raw != INVALID_HANDLE_VALUE)
            {
                ptlsmr::unique_handle pipe(raw);
                DWORD serverPid = 0;
                if (!GetNamedPipeServerProcessId(pipe.get(), &serverPid) || serverPid != hostPid)
                {
                    throw ptlsmr::win32_error("host pipe server PID binding", ERROR_ACCESS_DENIED);
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
                throw ptlsmr::win32_error("CreateFileW(host pipe)", error);
            }
            if (!WaitNamedPipeW(endpoint.c_str(), 500))
            {
                const DWORD waitError = GetLastError();
                if (waitError != ERROR_FILE_NOT_FOUND &&
                    waitError != ERROR_SEM_TIMEOUT)
                {
                    throw ptlsmr::win32_error("WaitNamedPipeW(host pipe)", waitError);
                }
            }
        }
        throw ptlsmr::win32_error("host pipe connect timeout", ERROR_TIMEOUT);
    }

    [[nodiscard]] ptlsmr::public_reply invoke(
        ptlsmr::public_command command,
        std::wstring_view releaseId,
        DWORD testHoldMilliseconds = 0,
        bool holdBeforePreface = false)
    {
        ptlsmr::public_request request{};
        request.magic = ptlsmr::ProtocolMagic;
        request.version = ptlsmr::ProtocolVersion;
        request.command = static_cast<uint16_t>(command);
        copy_bounded(request.releaseId, ARRAYSIZE(request.releaseId), releaseId);
        auto pipe = connect_bound_pipe(
            testHoldMilliseconds == 0 ? 0 : READ_CONTROL);
        if (testHoldMilliseconds != 0 && holdBeforePreface)
        {
            const auto inspection = inspect_pipe(pipe.get());
            std::wcout << L"testPipeConnected=true\n";
            std::wcout << L"testPipeMaximumInstances=" <<
                inspection.maximumInstances << L"\n";
            std::wcout << L"testPipeAuthenticatedUsersRights=" <<
                inspection.authenticatedUsersRights << L"\n";
            std::wcout << L"testPipeInspectionReady=true\n";
            std::wcout.flush();
            Sleep(testHoldMilliseconds);
        }
        ptlsmr::pipe_authentication_preface preface{};
        DWORD transferred = 0;
        ptlsmr::check_bool(
            WriteFile(pipe.get(), &preface, sizeof(preface), &transferred, nullptr) &&
                transferred == sizeof(preface),
            "WriteFile(user client authentication preface)");
        if (testHoldMilliseconds != 0 && !holdBeforePreface)
        {
            const auto inspection = inspect_pipe(pipe.get());
            std::wcout << L"testPipeConnected=true\n";
            std::wcout << L"testPipeMaximumInstances=" <<
                inspection.maximumInstances << L"\n";
            std::wcout << L"testPipeAuthenticatedUsersRights=" <<
                inspection.authenticatedUsersRights << L"\n";
            std::wcout << L"testPipeInspectionReady=true\n";
            std::wcout.flush();
            Sleep(testHoldMilliseconds);
        }
        transferred = 0;
        ptlsmr::check_bool(
            WriteFile(pipe.get(), &request, sizeof(request), &transferred, nullptr) &&
                transferred == sizeof(request),
            "WriteFile(user client request)");
        ptlsmr::public_reply reply{};
        ptlsmr::check_bool(
            ReadFile(pipe.get(), &reply, sizeof(reply), &transferred, nullptr) &&
                transferred == sizeof(reply),
            "ReadFile(user client response)");
        if (reply.magic != ptlsmr::ProtocolMagic ||
            reply.version != ptlsmr::ProtocolVersion ||
            reply.command != request.command ||
            reply.runtimeVersion[ARRAYSIZE(reply.runtimeVersion) - 1] != L'\0' ||
            reply.activeEngineVersion[ARRAYSIZE(reply.activeEngineVersion) - 1] != L'\0' ||
            reply.detail[ARRAYSIZE(reply.detail) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("host reply protocol", ERROR_INVALID_DATA);
        }
        return reply;
    }

    [[nodiscard]] DWORD parse_test_hold(std::wstring_view value)
    {
        if (value.empty() || value.size() > 5 ||
            !std::all_of(value.begin(), value.end(), [](wchar_t character) {
                return character >= L'0' && character <= L'9';
            }))
        {
            throw ptlsmr::win32_error(
                "test pipe hold milliseconds",
                ERROR_INVALID_PARAMETER);
        }
        const auto milliseconds = std::stoul(std::wstring(value));
        if (milliseconds == 0 || milliseconds > 30000)
        {
            throw ptlsmr::win32_error(
                "test pipe hold milliseconds range",
                ERROR_INVALID_PARAMETER);
        }
        return milliseconds;
    }

    int print_reply(const ptlsmr::public_reply& reply)
    {
        std::wcout << L"win32=" << reply.win32Status << L"\n";
        std::wcout << L"scmState=" << reply.scmState << L"\n";
        std::wcout << L"processId=" << reply.processId << L"\n";
        std::wcout << L"leaseCount=" << reply.leaseCount << L"\n";
        std::wcout << L"runtimeVersion=" << reply.runtimeVersion << L"\n";
        std::wcout << L"activeEngineVersion=" << reply.activeEngineVersion << L"\n";
        std::wstring detail(reply.detail);
        for (size_t index = 0; index < detail.size(); ++index)
        {
            if (detail[index] == L'\r' || detail[index] == L'\n')
            {
                const bool pair =
                    detail[index] == L'\r' &&
                    index + 1 < detail.size() &&
                    detail[index + 1] == L'\n';
                detail.replace(index, pair ? 2 : 1, L"\\n");
                ++index;
            }
        }
        std::wcout << L"detail=" << detail << L"\n";
        return reply.win32Status == ERROR_SUCCESS ? ERROR_SUCCESS : static_cast<int>(reply.win32Status);
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if ((arguments.size() == 4 &&
             (arguments[1] == L"--acquire" || arguments[1] == L"--ensure") &&
             arguments[2] == L"--release-id" &&
             valid_release_id(arguments[3])))
        {
            return print_reply(invoke(ptlsmr::public_command::acquire, arguments[3]));
        }
        if (arguments.size() == 2 && arguments[1] == L"--status")
        {
            return print_reply(invoke(ptlsmr::public_command::status, L""));
        }
        if (arguments.size() == 2 && arguments[1] == L"--release")
        {
            return print_reply(invoke(ptlsmr::public_command::release, L""));
        }
        if (arguments.size() == 4 &&
            arguments[1] == L"--test-hold-before-request" &&
            arguments[3] == L"--status")
        {
            return print_reply(invoke(
                ptlsmr::public_command::status,
                L"",
                parse_test_hold(arguments[2])));
        }
        if (arguments.size() == 4 &&
            arguments[1] == L"--test-hold-before-preface" &&
            arguments[3] == L"--status")
        {
            return print_reply(invoke(
                ptlsmr::public_command::status,
                L"",
                parse_test_hold(arguments[2]),
                true));
        }
        std::wcerr << L"usage: --acquire|--ensure --release-id release-NNN | --status | --release\n";
        return ERROR_INVALID_PARAMETER;
    }
    catch (const ptlsmr::win32_error& error)
    {
        std::wcerr << L"win32 error=" << error.code() << L" operation=" << error.what() << L"\n";
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        std::wcerr << L"unexpected user-client failure\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
