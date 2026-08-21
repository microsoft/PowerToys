#include "../Common/LsmrCommon.h"

#include <shellapi.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/base.h>

#include <algorithm>
#include <array>
#include <filesystem>
#include <iostream>

namespace
{
    constexpr wchar_t PackageCleanupServicePrefix[] = L"PtPuvrPackageCleanup_";
    constexpr std::wstring_view LegacyUpdaterPackageFullName =
        L"Microsoft.PowerToys.WsPuvr.Updater_5.0.0.0_x64__t8ed0av59w5q6";
    constexpr std::wstring_view LegacyRuntime1PackageFullName =
        L"Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__t8ed0av59w5q6";
    constexpr std::wstring_view LegacyRuntime2PackageFullName =
        L"Microsoft.PowerToys.WsPuvr.Runtime2_2.0.0.0_x64__t8ed0av59w5q6";
    constexpr std::wstring_view PreviousRuntime1PackageFullName =
        L"Microsoft.PowerToys.WsPuvr.Runtime1_1.0.0.0_x64__fcbv3b023fanj";
    constexpr std::wstring_view PreviousRuntime2PackageFullName =
        L"Microsoft.PowerToys.WsPuvr.Runtime2_2.0.0.0_x64__fcbv3b023fanj";

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

    [[nodiscard]] SERVICE_STATUS_PROCESS query_service_status(SC_HANDLE service)
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
        return status;
    }

    void stop_service(SC_HANDLE service)
    {
        const auto initial = query_service_status(service);
        if (initial.dwCurrentState == SERVICE_STOPPED)
        {
            return;
        }
        SERVICE_STATUS status{};
        if (!ControlService(service, SERVICE_CONTROL_STOP, &status))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_SERVICE_NOT_ACTIVE)
            {
                throw ptlsmr::win32_error("ControlService(updater stop)", error);
            }
        }
        wait_for_service(service, SERVICE_STOPPED);
    }

    [[nodiscard]] std::filesystem::path stage_updater_package(
        const std::filesystem::path& suppliedPackage,
        uint16_t major)
    {
        if (major != 5 && major != 6)
        {
            throw ptlsmr::win32_error(
                "updater package version argument",
                ERROR_INVALID_PARAMETER);
        }
        const auto packagePath = std::filesystem::weakly_canonical(suppliedPackage);
        if (!std::filesystem::is_regular_file(packagePath))
        {
            throw ptlsmr::win32_error("updater MSIX missing", ERROR_FILE_NOT_FOUND);
        }
        if (_wcsicmp(packagePath.extension().c_str(), L".msix") != 0)
        {
            throw ptlsmr::win32_error(
                "updater package extension policy",
                ERROR_INVALID_NAME);
        }

        winrt::init_apartment();
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto dependencies =
            winrt::single_threaded_vector<winrt::Windows::Foundation::Uri>().GetView();
        const auto deployment = manager.StagePackageAsync(
            winrt::Windows::Foundation::Uri(ptlsmr::file_uri(packagePath)),
            dependencies,
            winrt::Windows::Management::Deployment::DeploymentOptions::
                ForceUpdateFromAnyVersion)
                                    .get();
        const HRESULT result = deployment.ExtendedErrorCode();
        if (FAILED(result))
        {
            throw winrt::hresult_error(result, L"StagePackageAsync(updater)");
        }

        const auto executable =
            ptlsmr::updater_package_directory(major) / ptlsmr::UpdaterExe;
        if (!std::filesystem::is_regular_file(executable))
        {
            throw ptlsmr::win32_error(
                "staged updater executable",
                ERROR_FILE_NOT_FOUND);
        }
        return std::filesystem::weakly_canonical(executable);
    }

    [[nodiscard]] bool is_allowed_updater_executable(
        const std::filesystem::path& executable)
    {
        if (_wcsicmp(executable.filename().c_str(), ptlsmr::UpdaterExe) != 0)
        {
            return false;
        }
        for (const uint16_t major : std::array<uint16_t, 2>{ 5, 6 })
        {
            if (_wcsicmp(
                    executable.parent_path().filename().c_str(),
                    ptlsmr::expected_updater_package_full_name(major).c_str()) != 0)
            {
                continue;
            }
            const auto expected = ptlsmr::updater_package_directory(major) /
                ptlsmr::UpdaterExe;
            if (_wcsicmp(
                    std::filesystem::absolute(executable).lexically_normal().c_str(),
                    std::filesystem::absolute(expected).lexically_normal().c_str()) == 0)
            {
                return true;
            }
        }
        return false;
    }

    void bootstrap_install(
        const std::filesystem::path& suppliedPackage,
        uint16_t major)
    {
        if (!elevated())
        {
            throw ptlsmr::win32_error("bootstrap elevation policy", ERROR_ELEVATION_REQUIRED);
        }

        service_handle scm(OpenSCManagerW(
            nullptr,
            nullptr,
            SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(bootstrap)", GetLastError());
        }
        service_handle service(OpenServiceW(
            scm.get(),
            ptlsmr::UpdaterServiceName,
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_START |
                SERVICE_STOP | SERVICE_CHANGE_CONFIG | DELETE));
        const bool serviceExists = static_cast<bool>(service);
        if (!serviceExists && GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
        {
            throw ptlsmr::win32_error("OpenServiceW(updater bootstrap)", GetLastError());
        }

        std::filesystem::path executablePath;
        DWORD existingStartType = SERVICE_AUTO_START;
        DWORD existingErrorControl = SERVICE_ERROR_NORMAL;
        if (serviceExists)
        {
            DWORD configBytes = 0;
            QueryServiceConfigW(service.get(), nullptr, 0, &configBytes);
            if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
            {
                throw ptlsmr::win32_error(
                    "QueryServiceConfigW(updater size)",
                    GetLastError());
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
                 _wcsicmp(
                     config->lpServiceStartName,
                     L"NT AUTHORITY\\SYSTEM") == 0);
            int argumentCount = 0;
            if (!config->lpBinaryPathName)
            {
                throw ptlsmr::win32_error(
                    "raw updater ImagePath",
                    ERROR_INVALID_DATA);
            }
            LPWSTR* rawArguments =
                CommandLineToArgvW(config->lpBinaryPathName, &argumentCount);
            if (!rawArguments)
            {
                throw ptlsmr::win32_error(
                    "CommandLineToArgvW(updater ImagePath)",
                    GetLastError());
            }
            ptlsmr::local_memory arguments(rawArguments);
            executablePath = rawArguments[0];
            if (!isLocalSystem ||
                argumentCount != 1 ||
                config->dwServiceType != SERVICE_WIN32_OWN_PROCESS ||
                _wcsicmp(
                    executablePath.filename().c_str(),
                    ptlsmr::UpdaterExe) != 0 ||
                !is_allowed_updater_executable(executablePath))
            {
                throw ptlsmr::win32_error(
                    "raw updater SCM policy",
                    ERROR_ACCESS_DENIED);
            }
            existingStartType = config->dwStartType;
            existingErrorControl = config->dwErrorControl;
        }

        const auto installedUpdater =
            stage_updater_package(suppliedPackage, major);
        const std::wstring imagePath =
            ptlsmr::quote_argument(installedUpdater.wstring());
        if (!serviceExists)
        {
            service = service_handle(CreateServiceW(
                scm.get(),
                ptlsmr::UpdaterServiceName,
                ptlsmr::UpdaterServiceName,
                SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_START |
                    SERVICE_STOP | SERVICE_CHANGE_CONFIG | DELETE,
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
                throw ptlsmr::win32_error(
                    "CreateServiceW(updater)",
                    GetLastError());
            }
            executablePath = installedUpdater;
        }

        const bool repath =
            _wcsicmp(
                std::filesystem::absolute(executablePath).lexically_normal().c_str(),
                installedUpdater.c_str()) != 0;
        if (repath)
        {
            stop_service(service.get());
        }
        if (repath ||
            existingStartType != SERVICE_AUTO_START ||
            existingErrorControl != SERVICE_ERROR_NORMAL)
        {
            ptlsmr::check_bool(
                ChangeServiceConfigW(
                    service.get(),
                    SERVICE_NO_CHANGE,
                    SERVICE_AUTO_START,
                    SERVICE_ERROR_NORMAL,
                    repath ? imagePath.c_str() : nullptr,
                    nullptr,
                    nullptr,
                    nullptr,
                    nullptr,
                    nullptr,
                    nullptr),
                "ChangeServiceConfigW(updater startup policy)");
        }
        const auto status = query_service_status(service.get());
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

    [[nodiscard]] bool is_allowed_cleanup_package(std::wstring_view packageFullName)
    {
        return ptlsmr::is_allowed_updater_package_full_name(packageFullName) ||
            ptlsmr::is_allowed_runtime_package_full_name(packageFullName) ||
            packageFullName == LegacyUpdaterPackageFullName ||
            packageFullName == LegacyRuntime1PackageFullName ||
            packageFullName == LegacyRuntime2PackageFullName ||
            packageFullName == PreviousRuntime1PackageFullName ||
            packageFullName == PreviousRuntime2PackageFullName;
    }

    void remove_exact_package_as_system(std::wstring_view packageFullName)
    {
        if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("package cleanup SYSTEM policy", ERROR_ACCESS_DENIED);
        }
        if (!is_allowed_cleanup_package(packageFullName))
        {
            throw ptlsmr::win32_error("package cleanup identity policy", ERROR_ACCESS_DENIED);
        }
        winrt::init_apartment();
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto operation =
            packageFullName == LegacyUpdaterPackageFullName ?
            manager.RemovePackageAsync(
                packageFullName,
                winrt::Windows::Management::Deployment::RemovalOptions::RemoveForAllUsers) :
            manager.RemovePackageAsync(packageFullName);
        const auto deployment = operation.get();
        const HRESULT result = deployment.ExtendedErrorCode();
        if (FAILED(result) &&
            result != HRESULT_FROM_WIN32(ERROR_INSTALL_PACKAGE_NOT_FOUND))
        {
            throw winrt::hresult_error(result, L"RemovePackageAsync(LocalSystem)");
        }
    }

    void WINAPI package_cleanup_control_handler(DWORD)
    {
    }

    void WINAPI package_cleanup_service_main(DWORD argumentCount, LPWSTR* arguments) noexcept
    {
        SERVICE_STATUS status{};
        status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        status.dwCurrentState = SERVICE_START_PENDING;
        status.dwWaitHint = 30000;
        const wchar_t* serviceName =
            argumentCount > 0 ? arguments[0] : PackageCleanupServicePrefix;
        const SERVICE_STATUS_HANDLE handle =
            RegisterServiceCtrlHandlerW(serviceName, package_cleanup_control_handler);
        if (!handle)
        {
            return;
        }
        SetServiceStatus(handle, &status);

        DWORD result = ERROR_SUCCESS;
        try
        {
            if (argumentCount != 2)
            {
                throw ptlsmr::win32_error(
                    "package cleanup service arguments",
                    ERROR_INVALID_PARAMETER);
            }
            remove_exact_package_as_system(arguments[1]);
        }
        catch (const ptlsmr::win32_error& error)
        {
            result = error.code();
        }
        catch (const winrt::hresult_error& error)
        {
            result = HRESULT_CODE(error.code());
            if (result == ERROR_SUCCESS)
            {
                result = ERROR_INSTALL_FAILURE;
            }
        }
        catch (...)
        {
            result = ERROR_UNHANDLED_EXCEPTION;
        }

        status.dwCurrentState = SERVICE_STOPPED;
        status.dwWin32ExitCode = result;
        status.dwWaitHint = 0;
        SetServiceStatus(handle, &status);
    }

    [[nodiscard]] std::filesystem::path current_module_path()
    {
        std::array<wchar_t, 32768> path{};
        const DWORD length =
            GetModuleFileNameW(nullptr, path.data(), static_cast<DWORD>(path.size()));
        if (length == 0 || length >= path.size())
        {
            throw ptlsmr::win32_error("GetModuleFileNameW(controller)", GetLastError());
        }
        return std::filesystem::path(std::wstring_view(path.data(), length));
    }

    void remove_exact_package(std::wstring_view packageFullName)
    {
        if (!elevated())
        {
            throw ptlsmr::win32_error("package cleanup elevation policy", ERROR_ELEVATION_REQUIRED);
        }
        if (!is_allowed_cleanup_package(packageFullName))
        {
            throw ptlsmr::win32_error("package cleanup identity policy", ERROR_ACCESS_DENIED);
        }

        const std::wstring serviceName =
            std::wstring(PackageCleanupServicePrefix) +
            std::to_wstring(GetCurrentProcessId());
        const std::wstring imagePath =
            ptlsmr::quote_argument(current_module_path().wstring()) +
            L" --package-cleanup-service";
        service_handle scm(OpenSCManagerW(
            nullptr,
            nullptr,
            SC_MANAGER_CONNECT | SC_MANAGER_CREATE_SERVICE));
        if (!scm)
        {
            throw ptlsmr::win32_error("OpenSCManagerW(package cleanup)", GetLastError());
        }
        service_handle service(CreateServiceW(
            scm.get(),
            serviceName.c_str(),
            serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_START | DELETE,
            SERVICE_WIN32_OWN_PROCESS,
            SERVICE_DEMAND_START,
            SERVICE_ERROR_NORMAL,
            imagePath.c_str(),
            nullptr,
            nullptr,
            nullptr,
            nullptr,
            nullptr));
        if (!service)
        {
            throw ptlsmr::win32_error("CreateServiceW(package cleanup)", GetLastError());
        }

        try
        {
            const wchar_t* startArguments[] = { packageFullName.data() };
            ptlsmr::check_bool(
                StartServiceW(service.get(), ARRAYSIZE(startArguments), startArguments),
                "StartServiceW(package cleanup)");
            wait_for_service(service.get(), SERVICE_STOPPED);
            SERVICE_STATUS_PROCESS status{};
            DWORD bytes = 0;
            ptlsmr::check_bool(
                QueryServiceStatusEx(
                    service.get(),
                    SC_STATUS_PROCESS_INFO,
                    reinterpret_cast<BYTE*>(&status),
                    sizeof(status),
                    &bytes),
                "QueryServiceStatusEx(package cleanup result)");
            if (status.dwWin32ExitCode != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "LocalSystem package cleanup",
                    status.dwWin32ExitCode);
            }
        }
        catch (...)
        {
            DeleteService(service.get());
            throw;
        }
        ptlsmr::check_bool(
            DeleteService(service.get()),
            "DeleteService(package cleanup)");
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
        if (std::find(
                arguments.begin(),
                arguments.end(),
                L"--package-cleanup-service") != arguments.end())
        {
            wchar_t dispatchServiceName[] = L"PtPuvrPackageCleanup";
            SERVICE_TABLE_ENTRYW dispatchTable[] = {
                {
                    dispatchServiceName,
                    package_cleanup_service_main,
                },
                { nullptr, nullptr },
            };
            ptlsmr::check_bool(
                StartServiceCtrlDispatcherW(dispatchTable),
                "StartServiceCtrlDispatcherW(package cleanup)");
            return 0;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--bootstrap-install") != arguments.end())
        {
            const auto majorText =
                ptlsmr::argument_value(arguments, L"--updater-major");
            if (majorText != L"5" && majorText != L"6")
            {
                throw ptlsmr::win32_error(
                    "updater major argument",
                    ERROR_INVALID_PARAMETER);
            }
            bootstrap_install(
                ptlsmr::argument_value(arguments, L"--updater-package"),
                static_cast<uint16_t>(majorText[0] - L'0'));
            std::wcout
                << L"raw LocalSystem updater is running from staged MSIX payload\n";
            return 0;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--remove-package") != arguments.end())
        {
            remove_exact_package(
                ptlsmr::argument_value(arguments, L"--package-full-name"));
            std::wcout << L"exact package removal completed\n";
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
        std::wcerr << L"usage: --bootstrap-install --updater-package path.msix "
                      L"--updater-major 5|6 | "
                      L"--remove-package --package-full-name full-name | "
                      L"--provision --owner-sid S-1-5-21-... "
                      L"--runtime-track 1|2 --runtime-package path.msix | "
                      L"--status --owner-sid ... | --cleanup --owner-sid ...\n";
        return ERROR_INVALID_PARAMETER;
    }
    catch (const ptlsmr::win32_error& error)
    {
        std::wcerr << L"win32 error=" << error.code() << L" operation=" << error.what() << L"\n";
        return static_cast<int>(error.code());
    }
    catch (const winrt::hresult_error& error)
    {
        std::wcerr << L"hresult error=0x"
                   << std::hex << static_cast<uint32_t>(error.code())
                   << L" message=" << error.message().c_str() << L"\n";
        return static_cast<int>(HRESULT_CODE(error.code()));
    }
    catch (...)
    {
        std::wcerr << L"unexpected controller failure\n";
        return ERROR_UNHANDLED_EXCEPTION;
    }
}
