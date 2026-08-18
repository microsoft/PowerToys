#include "../Common/LsmrCommon.h"

#include <algorithm>
#include <filesystem>
#include <iostream>

namespace
{
    enum class command : uint16_t
    {
        provision_v1 = 1,
        upgrade_v2 = 2,
        status = 3,
        cleanup = 4,
    };

#pragma pack(push, 1)
    struct request
    {
        uint32_t magic{};
        uint16_t version{};
        uint16_t command{};
        wchar_t ownerSid[ptlsmr::MaxOwnerSidChars]{};
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

    [[nodiscard]] std::filesystem::path module_directory()
    {
        std::wstring path(32768, L'\0');
        const DWORD characters = GetModuleFileNameW(
            nullptr,
            path.data(),
            static_cast<DWORD>(path.size()));
        if (characters == 0 || characters >= path.size())
        {
            throw ptlsmr::win32_error("GetModuleFileNameW(controller)", GetLastError());
        }
        path.resize(characters);
        return std::filesystem::path(path).parent_path();
    }

    void copy_fixed_file(
        const std::filesystem::path& source,
        const std::filesystem::path& destination)
    {
        if (!std::filesystem::is_regular_file(source))
        {
            throw ptlsmr::win32_error("bootstrap source policy", ERROR_FILE_NOT_FOUND);
        }
        std::filesystem::copy_file(source, destination, std::filesystem::copy_options::overwrite_existing);
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

    void bootstrap_install()
    {
        if (!elevated())
        {
            throw ptlsmr::win32_error("bootstrap elevation policy", ERROR_ELEVATION_REQUIRED);
        }
        const auto binaryDirectory = module_directory();
        const auto root = binaryDirectory.parent_path().parent_path().parent_path().parent_path();
        const auto packageDirectory = root / L"artifacts\\packages";
        const auto installDirectory = ptlsmr::installed_updater_root();
        std::filesystem::create_directories(installDirectory);
        copy_fixed_file(binaryDirectory / L"PtLsmrUpdater.exe", installDirectory / L"PtLsmrUpdater.exe");
        std::filesystem::create_directories(installDirectory / L"Packages");
        copy_fixed_file(packageDirectory / L"PtLsmrRuntime-v1.msix", installDirectory / L"Packages\\PtLsmrRuntime-v1.msix");
        copy_fixed_file(packageDirectory / L"PtLsmrRuntime-v2.msix", installDirectory / L"Packages\\PtLsmrRuntime-v2.msix");
        ptlsmr::protect_system_directory(installDirectory);

        service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(bootstrap)", GetLastError());
        }
        const std::wstring executable = ptlsmr::quote_argument(
            (installDirectory / L"PtLsmrUpdater.exe").wstring());
        service_handle service(CreateServiceW(
            scm.get(),
            ptlsmr::UpdaterServiceName,
            ptlsmr::UpdaterServiceName,
            SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP | DELETE,
            SERVICE_WIN32_OWN_PROCESS,
            SERVICE_AUTO_START,
            SERVICE_ERROR_NORMAL,
            executable.c_str(),
            nullptr,
            nullptr,
            nullptr,
            nullptr,
            nullptr));
        if (!service)
        {
            if (GetLastError() != ERROR_SERVICE_EXISTS)
            {
                throw ptlsmr::win32_error("CreateServiceW(updater)", GetLastError());
            }
            service = service_handle(OpenServiceW(
                scm.get(),
                ptlsmr::UpdaterServiceName,
                SERVICE_QUERY_STATUS | SERVICE_START | SERVICE_STOP | DELETE));
            if (!service)
            {
                throw ptlsmr::win32_error("OpenServiceW(updater)", GetLastError());
            }
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
            "QueryServiceStatusEx(bootstrap)");
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

    [[nodiscard]] reply send_command(command operation, std::wstring_view owner)
    {
        request input{};
        input.magic = ptlsmr::ProtocolMagic;
        input.version = ptlsmr::ProtocolVersion;
        input.command = static_cast<uint16_t>(operation);
        copy_bounded(input.ownerSid, ARRAYSIZE(input.ownerSid), owner);
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

    int invoke(command operation, std::wstring_view owner)
    {
        const auto response = send_command(operation, owner);
        print_reply(response);
        return (response.win32Status == ERROR_SUCCESS && response.hresult == S_OK) ? 0 : 1;
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if (std::find(arguments.begin(), arguments.end(), L"--bootstrap-install") != arguments.end())
        {
            bootstrap_install();
            std::wcout << L"updater bootstrap installed and running\n";
            return 0;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--provision-v1") != arguments.end())
        {
            return invoke(
                command::provision_v1,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")));
        }
        if (std::find(arguments.begin(), arguments.end(), L"--upgrade-v2") != arguments.end())
        {
            return invoke(command::upgrade_v2, L"");
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
        std::wcerr << L"usage: --bootstrap-install | --provision-v1 --owner-sid S-1-5-21-... | "
                      L"--upgrade-v2 | --status --owner-sid ... | --cleanup --owner-sid ...\n";
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
