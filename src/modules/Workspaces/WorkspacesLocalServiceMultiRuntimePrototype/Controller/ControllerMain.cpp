#include "../Common/LsmrCommon.h"

#include <shellapi.h>

#include <algorithm>
#include <filesystem>
#include <iostream>
#include <optional>
#include <vector>

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
            "QueryServiceStatusEx(controller)");
        return status;
    }

    void wait_for_service(SC_HANDLE service, DWORD expected)
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
                throw ptlsmr::win32_error("updater service exit", status.dwWin32ExitCode);
            }
            Sleep(200);
        }
        throw ptlsmr::win32_error("updater service state timeout", ERROR_TIMEOUT);
    }

    void stop_service(SC_HANDLE service)
    {
        const auto status = query_status(service);
        if (status.dwCurrentState == SERVICE_STOPPED)
        {
            return;
        }
        SERVICE_STATUS ignored{};
        if (!ControlService(service, SERVICE_CONTROL_STOP, &ignored) &&
            GetLastError() != ERROR_SERVICE_NOT_ACTIVE)
        {
            throw ptlsmr::win32_error("ControlService(updater stop)", GetLastError());
        }
        wait_for_service(service, SERVICE_STOPPED);
    }

    [[nodiscard]] std::vector<BYTE> query_service_config(SC_HANDLE service)
    {
        DWORD bytes = 0;
        QueryServiceConfigW(service, nullptr, 0, &bytes);
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("QueryServiceConfigW(updater size)", GetLastError());
        }
        std::vector<BYTE> buffer(bytes);
        ptlsmr::check_bool(
            QueryServiceConfigW(
                service,
                reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.data()),
                bytes,
                &bytes),
            "QueryServiceConfigW(updater)");
        return buffer;
    }

    [[nodiscard]] bool current_updater_path_matches(
        SC_HANDLE service,
        const std::filesystem::path& expected)
    {
        const auto configBuffer = query_service_config(service);
        const auto* config = reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(configBuffer.data());
        if (!config->lpBinaryPathName ||
            !config->lpServiceStartName ||
            (_wcsicmp(config->lpServiceStartName, L"LocalSystem") != 0 &&
             _wcsicmp(config->lpServiceStartName, L"NT AUTHORITY\\SYSTEM") != 0) ||
            config->dwServiceType != SERVICE_WIN32_OWN_PROCESS)
        {
            return false;
        }
        int count = 0;
        LPWSTR* raw = CommandLineToArgvW(config->lpBinaryPathName, &count);
        if (!raw)
        {
            return false;
        }
        ptlsmr::local_memory arguments(raw);
        return count == 1 &&
            std::filesystem::equivalent(std::filesystem::path(raw[0]), expected);
    }

    [[nodiscard]] std::filesystem::path updater_inventory_path()
    {
        return ptlsmr::program_data_root() / L"updater-version.txt";
    }

    [[nodiscard]] std::optional<ptlsmr::file_version> installed_updater_version()
    {
        const auto path = updater_inventory_path();
        if (!std::filesystem::exists(path))
        {
            return std::nullopt;
        }
        return ptlsmr::parse_version(ptlsmr::read_utf8_file(path, 64));
    }

    void bootstrap_install(
        const std::filesystem::path& suppliedUpdater,
        std::wstring_view trustedSignerPin)
    {
        // This controller is a test-only simulation of a trusted installer bootstrap.
        // A loose user-writable controller is not a production trust anchor.
        if (!elevated())
        {
            throw ptlsmr::win32_error("trusted bootstrap elevation policy", ERROR_ELEVATION_REQUIRED);
        }
        if (_wcsicmp(suppliedUpdater.filename().c_str(), ptlsmr::UpdaterExe) != 0)
        {
            throw ptlsmr::win32_error("updater source filename policy", ERROR_INVALID_NAME);
        }
        const auto expectedSignerPin = ptlsmr::canonical_signer_sha256(trustedSignerPin);

        ptlsmr::protect_system_directory(ptlsmr::program_data_root());
        ptlsmr::protect_runtime_directory(ptlsmr::installation_root());
        const auto stagedDirectory = ptlsmr::create_protected_staging_directory(
            ptlsmr::installation_root() / L"Staging",
            L"updater");
        const auto stagedUpdater = stagedDirectory / ptlsmr::UpdaterExe;
        try
        {
            ptlsmr::copy_file_to_protected_stage(suppliedUpdater, stagedUpdater);
            const auto candidateVersion = ptlsmr::validate_updater_candidate(
                stagedUpdater,
                expectedSignerPin);
            ptlsmr::write_trusted_signer_pin(expectedSignerPin);
            const auto previousVersion = installed_updater_version();
            if (previousVersion && candidateVersion < *previousVersion)
            {
                throw ptlsmr::win32_error("updater anti-downgrade policy", ERROR_REVISION_MISMATCH);
            }

            const auto updaterDirectory = ptlsmr::updater_install_directory(candidateVersion);
            ptlsmr::protect_system_directory(updaterDirectory);
            const auto installedUpdater = updaterDirectory / ptlsmr::UpdaterExe;
            if (std::filesystem::exists(installedUpdater))
            {
                (void)ptlsmr::validate_updater_candidate(installedUpdater, expectedSignerPin);
                if (!ptlsmr::files_are_identical(stagedUpdater, installedUpdater))
                {
                    throw ptlsmr::win32_error("updater version collision policy", ERROR_FILE_EXISTS);
                }
                std::filesystem::remove(stagedUpdater);
            }
            else
            {
                ptlsmr::move_file_atomically(stagedUpdater, installedUpdater);
            }
            std::filesystem::remove_all(stagedDirectory);

            service_handle scm(OpenSCManagerW(
                nullptr,
                nullptr,
                SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE));
            if (!scm)
            {
                throw ptlsmr::win32_error("OpenSCManagerW(bootstrap)", GetLastError());
            }
            const std::wstring imagePath = ptlsmr::quote_argument(installedUpdater.wstring());
            service_handle service(CreateServiceW(
                scm.get(),
                ptlsmr::UpdaterServiceName,
                ptlsmr::UpdaterServiceName,
                SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_START |
                    SERVICE_STOP | SERVICE_CHANGE_CONFIG,
                SERVICE_WIN32_OWN_PROCESS,
                SERVICE_AUTO_START,
                SERVICE_ERROR_NORMAL,
                imagePath.c_str(),
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
                    SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_START |
                        SERVICE_STOP | SERVICE_CHANGE_CONFIG));
                if (!service)
                {
                    throw ptlsmr::win32_error("OpenServiceW(updater)", GetLastError());
                }
                if (!current_updater_path_matches(service.get(), installedUpdater))
                {
                    stop_service(service.get());
                    ptlsmr::check_bool(
                        ChangeServiceConfigW(
                            service.get(),
                            SERVICE_WIN32_OWN_PROCESS,
                            SERVICE_AUTO_START,
                            SERVICE_ERROR_NORMAL,
                            imagePath.c_str(),
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr,
                            nullptr),
                        "ChangeServiceConfigW(updater protected ImagePath)");
                }
            }
            if (!current_updater_path_matches(service.get(), installedUpdater))
            {
                throw ptlsmr::win32_error("updater SCM path policy", ERROR_ACCESS_DENIED);
            }
            if (query_status(service.get()).dwCurrentState != SERVICE_RUNNING)
            {
                if (!StartServiceW(service.get(), 0, nullptr) &&
                    GetLastError() != ERROR_SERVICE_ALREADY_RUNNING)
                {
                    throw ptlsmr::win32_error("StartServiceW(updater)", GetLastError());
                }
                wait_for_service(service.get(), SERVICE_RUNNING);
            }
            ptlsmr::write_utf8_file_atomic(
                updater_inventory_path(),
                ptlsmr::format_version(candidateVersion));
        }
        catch (...)
        {
            std::filesystem::remove_all(stagedDirectory);
            throw;
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
        const auto status = query_status(service.get());
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
                ptlsmr::check_bool(
                    SetNamedPipeHandleState(pipe.get(), &mode, nullptr, nullptr),
                    "SetNamedPipeHandleState");
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
            if (!WaitNamedPipeW(ptlsmr::UpdaterPipeName, 300) &&
                GetLastError() != ERROR_FILE_NOT_FOUND)
            {
                throw ptlsmr::win32_error("WaitNamedPipeW(updater pipe)", GetLastError());
            }
        }
        throw ptlsmr::win32_error("updater pipe connect timeout", ERROR_TIMEOUT);
    }

    [[nodiscard]] ptlsmr::reply send_command(
        ptlsmr::command operation,
        std::wstring_view owner,
        uint16_t runtimeTrack,
        std::wstring_view candidatePath,
        std::wstring_view crashPhase)
    {
        ptlsmr::request input{};
        input.magic = ptlsmr::ProtocolMagic;
        input.version = ptlsmr::ProtocolVersion;
        input.command = static_cast<uint16_t>(operation);
        input.runtimeTrack = runtimeTrack;
        copy_bounded(input.ownerSid, ARRAYSIZE(input.ownerSid), owner);
        copy_bounded(input.candidatePath, ARRAYSIZE(input.candidatePath), candidatePath);
        copy_bounded(input.crashPhase, ARRAYSIZE(input.crashPhase), crashPhase);
        auto pipe = connect_bound_pipe();
        DWORD transferred = 0;
        ptlsmr::check_bool(
            WriteFile(pipe.get(), &input, sizeof(input), &transferred, nullptr) &&
                transferred == sizeof(input),
            "WriteFile(updater request)");
        ptlsmr::reply output{};
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

    int invoke(
        ptlsmr::command operation,
        std::wstring_view owner,
        uint16_t runtimeTrack = 0,
        std::wstring_view candidatePath = L"",
        std::wstring_view crashPhase = L"")
    {
        const auto response = send_command(
            operation,
            owner,
            runtimeTrack,
            candidatePath,
            crashPhase);
        std::wcout << L"win32=" << response.win32Status
                   << L" scmState=" << response.scmState
                   << L" pid=" << response.processId
                   << L" serviceExit=" << response.serviceExit << L"\n";
        if (response.runtimeVersion[0] != L'\0')
        {
            std::wcout << L"runtimeVersion=" << response.runtimeVersion << L"\n";
        }
        if (response.detail[0] != L'\0')
        {
            std::wcout << response.detail;
            if (response.detail[wcslen(response.detail) - 1] != L'\n')
            {
                std::wcout << L"\n";
            }
        }
        return response.win32Status == ERROR_SUCCESS ? 0 : 1;
    }

    [[nodiscard]] std::wstring crash_phase(const std::vector<std::wstring>& arguments)
    {
        if (!ptlsmr::has_argument(arguments, L"--crash-phase"))
        {
            return {};
        }
        const auto value = ptlsmr::argument_value(arguments, L"--crash-phase");
        if (value != L"after-journal-prepared" &&
            value != L"after-target-directory-created" &&
            value != L"after-final-install" &&
            value != L"after-scm-repath" &&
            value != L"after-inventory-before-sync" &&
            value != L"after-unreferenced-runtime-delete" &&
            value != L"after-cleanup-service-delete" &&
            value != L"after-cleanup-inventory" &&
            value != L"fail-after-cleanup-service-delete")
        {
            throw ptlsmr::win32_error("crash phase policy", ERROR_INVALID_PARAMETER);
        }
        return value;
    }
}

int wmain()
{
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        if (ptlsmr::has_argument(arguments, L"--bootstrap-install"))
        {
            bootstrap_install(
                ptlsmr::argument_value(arguments, L"--updater-binary"),
                ptlsmr::argument_value(arguments, L"--signer-sha256"));
            std::wcout << L"Trusted-bootstrap simulation installed the ordinary LocalSystem updater.\n";
            return 0;
        }
        if (ptlsmr::has_argument(arguments, L"--provision"))
        {
            const auto trackText = ptlsmr::argument_value(arguments, L"--runtime-track");
            if (trackText != L"1" && trackText != L"2")
            {
                throw ptlsmr::win32_error("runtime track argument", ERROR_INVALID_PARAMETER);
            }
            return invoke(
                ptlsmr::command::provision,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")),
                static_cast<uint16_t>(trackText[0] - L'0'),
                ptlsmr::argument_value(arguments, L"--runtime-binary"),
                crash_phase(arguments));
        }
        if (ptlsmr::has_argument(arguments, L"--status"))
        {
            return invoke(
                ptlsmr::command::status,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")));
        }
        if (ptlsmr::has_argument(arguments, L"--cleanup"))
        {
            return invoke(
                ptlsmr::command::cleanup,
                ptlsmr::canonical_owner_sid(ptlsmr::argument_value(arguments, L"--owner-sid")),
                0,
                L"",
                crash_phase(arguments));
        }
        std::wcerr << L"usage: --bootstrap-install --updater-binary path.exe "
                      L"--signer-sha256 64-hex-fingerprint | "
                      L"--provision --owner-sid S-1-5-21-... "
                      L"--runtime-track 1|2 --runtime-binary path.exe "
                      L"[--crash-phase after-journal-prepared|after-target-directory-created|"
                      L"after-final-install|"
                      L"after-scm-repath|after-inventory-before-sync|"
                      L"after-unreferenced-runtime-delete] | "
                      L"--status --owner-sid ... | --cleanup --owner-sid ... "
                      L"[--crash-phase after-cleanup-service-delete|after-cleanup-inventory|"
                      L"fail-after-cleanup-service-delete]\n";
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
