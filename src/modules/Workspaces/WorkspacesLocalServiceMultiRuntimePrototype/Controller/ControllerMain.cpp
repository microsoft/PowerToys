#include "../Common/LsmrCommon.h"

#include <shellapi.h>

#include <algorithm>
#include <filesystem>
#include <iostream>

namespace
{
    enum class command : uint16_t
    {
        provision = 1,
        status = 2,
        cleanup = 3,
    };

#pragma pack(push, 1)
    struct request
    {
        uint32_t magic{};
        uint16_t version{};
        uint16_t command{};
        uint16_t runtimeTrack{};
        uint16_t reserved{};
        wchar_t ownerSid[ptlsmr::MaxOwnerSidChars]{};
        wchar_t packagePath[ptlsmr::MaxPackagePathChars]{};
    };

    struct reply
    {
        uint32_t magic{};
        uint16_t version{};
        uint16_t command{};
        uint32_t win32Status{};
        int32_t hresult{};
        uint32_t scmState{};
        uint32_t processId{};
        uint32_t serviceExit{};
        wchar_t packageFullName[256]{};
        wchar_t detail[2048]{};
    };
#pragma pack(pop)

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
            m_value(other.m_value)
        {
            other.m_value = nullptr;
        }
        service_handle& operator=(service_handle&& other) noexcept
        {
            if (this != &other)
            {
                if (m_value)
                {
                    CloseServiceHandle(m_value);
                }
                m_value = other.m_value;
                other.m_value = nullptr;
            }
            return *this;
        }
        [[nodiscard]] SC_HANDLE get() const noexcept
        {
            return m_value;
        }
        explicit operator bool() const noexcept
        {
            return m_value != nullptr;
        }

    private:
        SC_HANDLE m_value;
    };

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw ptlsmr::win32_error("controller bounded input", ERROR_BUFFER_OVERFLOW);
        }
        std::copy(source.begin(), source.end(), destination);
        destination[source.size()] = L'\0';
    }

    [[nodiscard]] bool elevated()
    {
        HANDLE raw = nullptr;
        ptlsmr::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &raw),
            "OpenProcessToken(elevation)");
        ptlsmr::unique_handle token(raw);
        TOKEN_ELEVATION elevation{};
        DWORD bytes = 0;
        ptlsmr::check_bool(
            GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &bytes),
            "GetTokenInformation(TokenElevation)");
        return elevation.TokenIsElevated != 0;
    }

    void wait_for_service(SC_HANDLE service, DWORD expected)
    {
        for (DWORD elapsed = 0; elapsed < 30000; elapsed += 200)
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
                "QueryServiceStatusEx(updater)");
            if (status.dwCurrentState == expected)
            {
                return;
            }
            if (status.dwCurrentState == SERVICE_STOPPED && expected != SERVICE_STOPPED)
            {
                throw ptlsmr::win32_error("updater service exit", status.dwWin32ExitCode);
            }
            Sleep(200);
        }
        throw ptlsmr::win32_error("updater service start timeout", ERROR_TIMEOUT);
    }

    void start_manifest_updater()
    {
        if (!elevated())
        {
            throw ptlsmr::win32_error("updater start elevation policy", ERROR_ELEVATION_REQUIRED);
        }
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(updater start)", GetLastError());
        }
        service_handle service(OpenServiceW(
            scm.get(),
            ptlsmr::UpdaterServiceName,
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_START));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(manifest updater)", GetLastError());
        }
        DWORD configBytes = 0;
        QueryServiceConfigW(service.get(), nullptr, 0, &configBytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("QueryServiceConfigW(updater size)", GetLastError());
        }
        std::vector<BYTE> configBuffer(configBytes);
        ptlsmr::check_bool(
            QueryServiceConfigW(
                service.get(),
                reinterpret_cast<QUERY_SERVICE_CONFIGW*>(configBuffer.data()),
                configBytes,
                &configBytes),
            "QueryServiceConfigW(updater)");
        const auto* config =
            reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(configBuffer.data());
        const bool isLocalSystem =
            config->lpServiceStartName &&
            (_wcsicmp(config->lpServiceStartName, L"LocalSystem") == 0 ||
             _wcsicmp(config->lpServiceStartName, L"NT AUTHORITY\\SYSTEM") == 0);
        int argumentCount = 0;
        if (!config->lpBinaryPathName)
        {
            throw ptlsmr::win32_error("manifest updater ImagePath", ERROR_INVALID_DATA);
        }
        LPWSTR* rawArguments = CommandLineToArgvW(config->lpBinaryPathName, &argumentCount);
        if (!rawArguments)
        {
            throw ptlsmr::win32_error("CommandLineToArgvW(updater ImagePath)", GetLastError());
        }
        ptlsmr::local_memory arguments(rawArguments);
        const std::filesystem::path executablePath(rawArguments[0]);
        if (!isLocalSystem ||
            argumentCount != 1 ||
            _wcsicmp(executablePath.filename().c_str(), ptlsmr::UpdaterExe) != 0 ||
            _wcsicmp(
                executablePath.parent_path().filename().c_str(),
                ptlsmr::expected_updater_package_full_name().c_str()) != 0)
        {
            throw ptlsmr::win32_error("manifest updater SCM policy", ERROR_ACCESS_DENIED);
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
            "QueryServiceStatusEx(updater start)");
        if (status.dwCurrentState != SERVICE_RUNNING)
        {
            if (!StartServiceW(service.get(), 0, nullptr) &&
                GetLastError() != ERROR_SERVICE_ALREADY_RUNNING)
            {
                throw ptlsmr::win32_error("StartServiceW(updater)", GetLastError());
            }
            wait_for_service(service.get(), SERVICE_RUNNING);
        }
    }

    [[nodiscard]] DWORD updater_process_id()
    {
        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(pipe binding)", GetLastError());
        }
        service_handle service(OpenServiceW(scm.get(), ptlsmr::UpdaterServiceName, SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(pipe binding)", GetLastError());
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
            "QueryServiceStatusEx(pipe binding)");
        if (status.dwCurrentState != SERVICE_RUNNING || status.dwProcessId == 0)
        {
            throw ptlsmr::win32_error("updater service running policy", ERROR_SERVICE_NOT_ACTIVE);
        }
        return status.dwProcessId;
    }

    [[nodiscard]] ptlsmr::unique_handle connect_bound_pipe()
    {
        const DWORD updaterPid = updater_process_id();
        for (DWORD attempt = 0; attempt < 100; ++attempt)
        {
            HANDLE raw = CreateFileW(
                ptlsmr::UpdaterPipeName,
                GENERIC_READ | GENERIC_WRITE,
                0,
                nullptr,
                OPEN_EXISTING,
                FILE_ATTRIBUTE_NORMAL,
                nullptr);
            if (raw != INVALID_HANDLE_VALUE)
            {
                ptlsmr::unique_handle pipe(raw);
                DWORD serverPid = 0;
                if (!GetNamedPipeServerProcessId(pipe.get(), &serverPid) || serverPid != updaterPid)
                {
                    throw ptlsmr::win32_error("updater pipe server PID binding", ERROR_ACCESS_DENIED);
                }
                DWORD mode = PIPE_READMODE_MESSAGE;
                ptlsmr::check_bool(SetNamedPipeHandleState(pipe.get(), &mode, nullptr, nullptr), "SetNamedPipeHandleState");
                return pipe;
            }
            const DWORD error = GetLastError();
            if (error == ERROR_FILE_NOT_FOUND)
            {
                Sleep(300);
                continue;
            }
            if (error != ERROR_PIPE_BUSY)
            {
                throw ptlsmr::win32_error("CreateFileW(updater pipe)", error);
            }
            if (!WaitNamedPipeW(ptlsmr::UpdaterPipeName, 300))
            {
                const DWORD waitError = GetLastError();
                if (waitError == ERROR_FILE_NOT_FOUND)
                {
                    Sleep(300);
                    continue;
                }
                throw ptlsmr::win32_error("WaitNamedPipeW(updater pipe)", waitError);
            }
        }
        throw ptlsmr::win32_error("updater pipe connect timeout", ERROR_TIMEOUT);
    }

    [[nodiscard]] reply send_command(
        command operation,
        std::wstring_view owner,
        uint16_t runtimeTrack,
        std::wstring_view packagePath)
    {
        request input{};
        input.magic = ptlsmr::ProtocolMagic;
        input.version = ptlsmr::ProtocolVersion;
        input.command = static_cast<uint16_t>(operation);
        input.runtimeTrack = runtimeTrack;
        copy_bounded(input.ownerSid, ARRAYSIZE(input.ownerSid), owner);
        copy_bounded(input.packagePath, ARRAYSIZE(input.packagePath), packagePath);
        auto pipe = connect_bound_pipe();
        DWORD transferred = 0;
        ptlsmr::check_bool(
            WriteFile(pipe.get(), &input, sizeof(input), &transferred, nullptr) &&
                transferred == sizeof(input),
            "WriteFile(updater request)");
        reply output{};
        ptlsmr::check_bool(
            ReadFile(pipe.get(), &output, sizeof(output), &transferred, nullptr) &&
                transferred == sizeof(output),
            "ReadFile(updater reply)");
        if (output.magic != ptlsmr::ProtocolMagic ||
            output.version != ptlsmr::ProtocolVersion ||
            output.command != static_cast<uint16_t>(operation))
        {
            throw ptlsmr::win32_error("updater reply protocol", ERROR_INVALID_DATA);
        }
        return output;
    }

    void print_reply(const reply& response)
    {
        std::wcout << L"win32=" << response.win32Status
                   << L" hresult=0x" << std::hex << static_cast<uint32_t>(response.hresult)
                   << std::dec
                   << L" scmState=" << response.scmState
                   << L" pid=" << response.processId
                   << L" serviceExit=" << response.serviceExit << L"\n";
        if (response.packageFullName[0] != L'\0')
        {
            std::wcout << L"packageFullName=" << response.packageFullName << L"\n";
        }
        if (response.detail[0] != L'\0')
        {
            std::wcout << response.detail;
            if (response.detail[wcslen(response.detail) - 1] != L'\n')
            {
                std::wcout << L"\n";
            }
        }
    }

    int invoke(
        command operation,
        std::wstring_view owner,
        uint16_t runtimeTrack = 0,
        std::wstring_view packagePath = L"")
    {
        const auto response = send_command(operation, owner, runtimeTrack, packagePath);
        print_reply(response);
        return (response.win32Status == ERROR_SUCCESS && response.hresult == S_OK) ? 0 : 1;
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if (std::find(arguments.begin(), arguments.end(), L"--start-updater") != arguments.end())
        {
            start_manifest_updater();
            std::wcout << L"manifest-owned updater is running\n";
            return 0;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--provision") != arguments.end())
        {
            const auto trackText = ptlsmr::argument_value(arguments, L"--runtime-track");
            if (trackText != L"1" && trackText != L"2")
            {
                throw ptlsmr::win32_error("runtime track argument", ERROR_INVALID_PARAMETER);
            }
            const uint16_t runtimeTrack = static_cast<uint16_t>(trackText[0] - L'0');
            const auto suppliedPath = std::filesystem::weakly_canonical(
                ptlsmr::argument_value(arguments, L"--runtime-package"));
            if (!std::filesystem::is_regular_file(suppliedPath))
            {
                throw ptlsmr::win32_error("runtime package argument", ERROR_FILE_NOT_FOUND);
            }
            return invoke(
                command::provision,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")),
                runtimeTrack,
                suppliedPath.wstring());
        }
        if (std::find(arguments.begin(), arguments.end(), L"--status") != arguments.end())
        {
            return invoke(
                command::status,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")));
        }
        if (std::find(arguments.begin(), arguments.end(), L"--cleanup") != arguments.end())
        {
            return invoke(
                command::cleanup,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")));
        }
        std::wcerr << L"usage: --start-updater | --provision --owner-sid S-1-5-21-... "
                      L"--runtime-track 1|2 --runtime-package path.msix | "
                      L"--status --owner-sid ... | --cleanup --owner-sid ...\n";
        return ERROR_INVALID_PARAMETER;
    }
    catch (const ptlsmr::win32_error& error)
    {
        std::wcerr << L"win32 error=" << error.code() << L" operation=" << error.what() << L"\n";
        return static_cast<int>(error.code());
    }
    catch (...)
    {
        std::wcerr << L"unexpected controller failure\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
