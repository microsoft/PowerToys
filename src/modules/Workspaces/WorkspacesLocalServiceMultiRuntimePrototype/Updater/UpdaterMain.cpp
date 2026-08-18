#include "../Common/LsmrCommon.h"

#include <sddl.h>
#include <shellapi.h>
#include <shlobj_core.h>
#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/Windows.Storage.h>
#include <winrt/base.h>

#include <algorithm>
#include <array>
#include <cstring>
#include <filesystem>
#include <sstream>
#include <thread>

#pragma comment(lib, "advapi32.lib")
#pragma comment(lib, "shell32.lib")

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
        uint32_t magic{ ptlsmr::ProtocolMagic };
        uint16_t version{ ptlsmr::ProtocolVersion };
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
        service_handle() = default;
        explicit service_handle(SC_HANDLE value) :
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

    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    SERVICE_STATUS g_status{};
    ptlsmr::unique_handle g_stopEvent;

    void copy_bounded(wchar_t* destination, size_t capacity, std::wstring_view source)
    {
        if (source.size() >= capacity)
        {
            throw ptlsmr::win32_error("bounded output", ERROR_BUFFER_OVERFLOW);
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

    [[nodiscard]] std::wstring owners_path()
    {
        return (ptlsmr::program_data_root() / L"instances.txt").wstring();
    }

    [[nodiscard]] std::vector<std::wstring> read_owners()
    {
        const std::filesystem::path path(owners_path());
        if (!std::filesystem::exists(path))
        {
            return {};
        }
        const std::wstring contents = ptlsmr::read_utf8_file(path, 16 * 1024);
        std::vector<std::wstring> owners;
        size_t start = 0;
        while (start < contents.size())
        {
            const size_t end = contents.find_first_of(L"\r\n", start);
            const std::wstring_view line(
                contents.data() + start,
                (end == std::wstring::npos ? contents.size() : end) - start);
            if (!line.empty())
            {
                const auto owner = ptlsmr::canonical_owner_sid(line);
                if (std::find(owners.begin(), owners.end(), owner) == owners.end())
                {
                    if (owners.size() >= 32)
                    {
                        throw ptlsmr::win32_error("instance list limit", ERROR_TOO_MANY_NAMES);
                    }
                    owners.push_back(owner);
                }
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
        return owners;
    }

    void write_owners(const std::vector<std::wstring>& owners)
    {
        std::wstringstream output;
        for (const auto& owner : owners)
        {
            output << ptlsmr::canonical_owner_sid(owner) << L"\r\n";
        }
        ptlsmr::write_utf8_file_atomic(owners_path(), output.str());
    }

    void add_owner(const std::wstring& owner)
    {
        auto owners = read_owners();
        if (std::find(owners.begin(), owners.end(), owner) == owners.end())
        {
            owners.push_back(owner);
            write_owners(owners);
        }
    }

    void remove_owner(const std::wstring& owner)
    {
        auto owners = read_owners();
        owners.erase(std::remove(owners.begin(), owners.end(), owner), owners.end());
        write_owners(owners);
    }

    [[nodiscard]] std::wstring service_binary_path(
        const std::filesystem::path& packageDirectory,
        const ptlsmr::InstanceNames& names)
    {
        const auto executable = packageDirectory / ptlsmr::RuntimeExe;
        return ptlsmr::quote_argument(executable.wstring()) +
            L" --service-name " + ptlsmr::quote_argument(names.serviceName) +
            L" --owner-sid " + ptlsmr::quote_argument(names.ownerSid);
    }

    [[nodiscard]] bool equal_path(
        const std::filesystem::path& left,
        const std::filesystem::path& right)
    {
        const std::wstring canonicalLeft = std::filesystem::weakly_canonical(left).wstring();
        const std::wstring canonicalRight = std::filesystem::weakly_canonical(right).wstring();
        return CompareStringOrdinal(
                   canonicalLeft.c_str(),
                   static_cast<int>(canonicalLeft.size()),
                   canonicalRight.c_str(),
                   static_cast<int>(canonicalRight.size()),
                   TRUE) == CSTR_EQUAL;
    }

    [[nodiscard]] bool matches_runtime_service_command(
        const std::wstring& imagePath,
        const std::filesystem::path& expectedExecutable,
        const ptlsmr::InstanceNames& names)
    {
        int count = 0;
        LPWSTR* rawArguments = CommandLineToArgvW(imagePath.c_str(), &count);
        if (!rawArguments)
        {
            return false;
        }
        ptlsmr::local_memory arguments(rawArguments);
        return count == 5 &&
            equal_path(rawArguments[0], expectedExecutable) &&
            rawArguments[1] == std::wstring_view(L"--service-name") &&
            rawArguments[2] == names.serviceName &&
            rawArguments[3] == std::wstring_view(L"--owner-sid") &&
            rawArguments[4] == names.ownerSid;
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
        if (!QueryServiceConfigW(
                service,
                reinterpret_cast<QUERY_SERVICE_CONFIGW*>(buffer.data()),
                bytes,
                &bytes))
        {
            throw ptlsmr::win32_error("QueryServiceConfigW", GetLastError());
        }
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
            "QueryServiceStatusEx");
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
                throw ptlsmr::win32_error("runtime service exited", status.dwWin32ExitCode);
            }
            Sleep(200);
        }
        throw ptlsmr::win32_error("runtime service state timeout", ERROR_TIMEOUT);
    }

    void stop_service(SC_HANDLE service)
    {
        const auto initial = query_status(service);
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
                throw ptlsmr::win32_error("ControlService(STOP)", error);
            }
        }
        wait_for_state(service, SERVICE_STOPPED);
    }

    void stage_exact_package(uint16_t major)
    {
        const std::filesystem::path packagePath =
            ptlsmr::installed_updater_root() / L"Packages" /
            (major == 1 ? L"PtLsmrRuntime-v1.msix" : L"PtLsmrRuntime-v2.msix");
        if (!std::filesystem::is_regular_file(packagePath))
        {
            throw ptlsmr::win32_error("fixed MSIX missing", ERROR_FILE_NOT_FOUND);
        }
        std::wstring uriText = L"file:///";
        uriText += packagePath.wstring();
        std::replace(uriText.begin(), uriText.end(), L'\\', L'/');
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto dependencies = winrt::single_threaded_vector<winrt::Windows::Foundation::Uri>().GetView();
        const auto result = manager.StagePackageAsync(
            winrt::Windows::Foundation::Uri(uriText),
            dependencies,
            winrt::Windows::Management::Deployment::DeploymentOptions::None)
                                .get();
        if (FAILED(result.ExtendedErrorCode()))
        {
            throw winrt::hresult_error(result.ExtendedErrorCode(), L"StagePackageAsync");
        }
    }

    [[nodiscard]] std::filesystem::path staged_package_directory(const std::wstring& fullName)
    {
        if (!ptlsmr::is_allowed_package_full_name(fullName))
        {
            throw ptlsmr::win32_error("staged package identity policy", ERROR_INVALID_DATA);
        }
        PWSTR programFiles = nullptr;
        const HRESULT result = SHGetKnownFolderPath(FOLDERID_ProgramFiles, 0, nullptr, &programFiles);
        if (FAILED(result))
        {
            throw ptlsmr::win32_error("SHGetKnownFolderPath(FOLDERID_ProgramFiles)", HRESULT_CODE(result));
        }
        ptlsmr::local_memory memory(programFiles);
        const std::filesystem::path location =
            std::filesystem::path(programFiles) / L"WindowsApps" / fullName;
        const std::filesystem::path executable = location / ptlsmr::RuntimeExe;
        if (!std::filesystem::is_regular_file(executable))
        {
            throw ptlsmr::win32_error("registered runtime executable", ERROR_FILE_NOT_FOUND);
        }
        std::wstring expectedPrefix = std::filesystem::weakly_canonical(
            std::filesystem::path(programFiles) / L"WindowsApps").wstring();
        if (!expectedPrefix.ends_with(L"\\"))
        {
            expectedPrefix += L"\\";
        }
        const std::wstring actual = std::filesystem::weakly_canonical(executable).wstring();
        if (actual.size() <= expectedPrefix.size() ||
            CompareStringOrdinal(
                actual.c_str(),
                static_cast<int>(expectedPrefix.size()),
                expectedPrefix.c_str(),
                static_cast<int>(expectedPrefix.size()),
                TRUE) != CSTR_EQUAL)
        {
            throw ptlsmr::win32_error("WindowsApps executable path policy", ERROR_ACCESS_DENIED);
        }
        return location;
    }

    [[nodiscard]] service_handle create_or_open_runtime_service(
        const service_handle& scm,
        const ptlsmr::InstanceNames& names,
        const std::wstring& binaryPath,
        bool& created)
    {
        SC_HANDLE raw = CreateServiceW(
            scm.get(),
            names.serviceName.c_str(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG |
                SERVICE_START | SERVICE_STOP | DELETE,
            SERVICE_WIN32_OWN_PROCESS,
            SERVICE_DEMAND_START,
            SERVICE_ERROR_NORMAL,
            binaryPath.c_str(),
            nullptr,
            nullptr,
            nullptr,
            nullptr,
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

    void verify_or_repath_runtime_service(
        SC_HANDLE service,
        const std::filesystem::path& expectedCurrentExecutable,
        const std::filesystem::path& desiredExecutable,
        const ptlsmr::InstanceNames& names,
        bool allowRepath)
    {
        const auto buffer = query_service_config(service);
        const auto* config = reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(buffer.data());
        const bool isLocalSystem =
            config->lpServiceStartName &&
            (_wcsicmp(config->lpServiceStartName, L"LocalSystem") == 0 ||
             _wcsicmp(config->lpServiceStartName, L"NT AUTHORITY\\SYSTEM") == 0);
        if (config->dwServiceType != SERVICE_WIN32_OWN_PROCESS ||
            !isLocalSystem)
        {
            throw ptlsmr::win32_error("runtime service account policy", ERROR_ACCESS_DENIED);
        }
        const std::wstring currentPath(config->lpBinaryPathName ? config->lpBinaryPathName : L"");
        if (matches_runtime_service_command(currentPath, desiredExecutable, names))
        {
            return;
        }
        if (!allowRepath ||
            !matches_runtime_service_command(currentPath, expectedCurrentExecutable, names))
        {
            throw ptlsmr::win32_error("runtime ImagePath policy", ERROR_ACCESS_DENIED);
        }
        const std::wstring desiredPath = service_binary_path(desiredExecutable.parent_path(), names);
        if (!ChangeServiceConfigW(
                service,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                SERVICE_NO_CHANGE,
                desiredPath.c_str(),
                nullptr,
                nullptr,
                nullptr,
                nullptr,
                nullptr,
                nullptr))
        {
            throw ptlsmr::win32_error("ChangeServiceConfigW(runtime ImagePath)", GetLastError());
        }
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

    void start_runtime_service(SC_HANDLE service)
    {
        const auto initial = query_status(service);
        if (initial.dwCurrentState == SERVICE_RUNNING)
        {
            return;
        }
        if (!StartServiceW(service, 0, nullptr))
        {
            const DWORD error = GetLastError();
            if (error != ERROR_SERVICE_ALREADY_RUNNING)
            {
                throw ptlsmr::win32_error("StartServiceW(runtime)", error);
            }
        }
        wait_for_state(service, SERVICE_RUNNING);
    }

    void ensure_package_staged(uint16_t major)
    {
        const auto fullName = ptlsmr::expected_package_full_name(major);
        stage_exact_package(major);
        (void)staged_package_directory(fullName);
    }

    void remove_exact_package(uint16_t major)
    {
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto result = manager.RemovePackageAsync(
            ptlsmr::expected_package_full_name(major))
                                .get();
        const HRESULT error = result.ExtendedErrorCode();
        if (FAILED(error) &&
            error != HRESULT_FROM_WIN32(ERROR_INSTALL_PACKAGE_NOT_FOUND))
        {
            throw winrt::hresult_error(error, L"RemovePackageAsync");
        }
    }

    void provision(const std::wstring& owner)
    {
        ensure_package_staged(1);
        const auto names = ptlsmr::instance_names(owner);
        const auto packageDirectory = staged_package_directory(ptlsmr::expected_package_full_name(1));
        auto scm = open_scm();
        bool created = false;
        auto service = create_or_open_runtime_service(
            scm,
            names,
            service_binary_path(packageDirectory, names),
            created);
        try
        {
            verify_or_repath_runtime_service(
                service.get(),
                packageDirectory / ptlsmr::RuntimeExe,
                packageDirectory / ptlsmr::RuntimeExe,
                names,
                false);
            configure_service_sid(service.get());
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            ptlsmr::protect_directory_for_service(
                names.storeDirectory,
                ptlsmr::service_sid(names.serviceName));
            start_runtime_service(service.get());
            add_owner(owner);
        }
        catch (...)
        {
            if (created)
            {
                const auto status = query_status(service.get());
                if (status.dwCurrentState != SERVICE_STOPPED)
                {
                    stop_service(service.get());
                }
            }
            throw;
        }
    }

    void upgrade()
    {
        ensure_package_staged(2);
        const auto currentPackageDirectory =
            staged_package_directory(ptlsmr::expected_package_full_name(1));
        const auto targetPackageDirectory =
            staged_package_directory(ptlsmr::expected_package_full_name(2));
        auto scm = open_scm();
        const auto owners = read_owners();
        if (owners.empty())
        {
            throw ptlsmr::win32_error("upgrade has no managed runtime instances", ERROR_NOT_FOUND);
        }
        for (const auto& owner : owners)
        {
            const auto names = ptlsmr::instance_names(owner);
            service_handle service(OpenServiceW(
                scm.get(),
                names.serviceName.c_str(),
                SERVICE_QUERY_STATUS | SERVICE_QUERY_CONFIG | SERVICE_CHANGE_CONFIG |
                    SERVICE_START | SERVICE_STOP));
            if (!service)
            {
                throw ptlsmr::win32_error("OpenServiceW(upgrade runtime)", GetLastError());
            }
            stop_service(service.get());
            verify_or_repath_runtime_service(
                service.get(),
                currentPackageDirectory / ptlsmr::RuntimeExe,
                targetPackageDirectory / ptlsmr::RuntimeExe,
                names,
                true);
        }
        for (const auto& owner : owners)
        {
            const auto names = ptlsmr::instance_names(owner);
            service_handle service(OpenServiceW(
                scm.get(),
                names.serviceName.c_str(),
                SERVICE_QUERY_STATUS | SERVICE_START));
            if (!service)
            {
                throw ptlsmr::win32_error("OpenServiceW(restart runtime)", GetLastError());
            }
            start_runtime_service(service.get());
        }
    }

    void fill_status(const std::wstring& owner, reply& response)
    {
        const auto names = ptlsmr::instance_names(owner);
        auto scm = open_scm();
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS));
        if (!service)
        {
            throw ptlsmr::win32_error("OpenServiceW(status)", GetLastError());
        }
        const auto status = query_status(service.get());
        response.scmState = status.dwCurrentState;
        response.processId = status.dwProcessId;
        response.serviceExit = status.dwWin32ExitCode;
        const std::filesystem::path evidence = names.evidencePath;
        if (std::filesystem::exists(evidence))
        {
            copy_bounded(response.detail, ARRAYSIZE(response.detail), ptlsmr::read_utf8_file(evidence, 8192));
        }
    }

    void cleanup(const std::wstring& owner)
    {
        const auto names = ptlsmr::instance_names(owner);
        auto scm = open_scm();
        service_handle service(OpenServiceW(
            scm.get(),
            names.serviceName.c_str(),
            SERVICE_QUERY_STATUS | SERVICE_STOP | DELETE));
        if (service)
        {
            stop_service(service.get());
            ptlsmr::check_bool(DeleteService(service.get()), "DeleteService(runtime)");
        }
        else if (GetLastError() != ERROR_SERVICE_DOES_NOT_EXIST)
        {
            throw ptlsmr::win32_error("OpenServiceW(cleanup runtime)", GetLastError());
        }
        if (std::filesystem::exists(names.storeDirectory))
        {
            std::filesystem::remove_all(names.storeDirectory);
        }
        remove_owner(owner);
        if (read_owners().empty())
        {
            remove_exact_package(1);
            remove_exact_package(2);
        }
    }

    [[nodiscard]] bool is_request_admin(HANDLE pipe)
    {
        if (!ImpersonateNamedPipeClient(pipe))
        {
            return false;
        }
        HANDLE raw = nullptr;
        const bool opened = OpenThreadToken(GetCurrentThread(), TOKEN_QUERY, TRUE, &raw) != FALSE;
        ptlsmr::unique_handle token(raw);
        bool admin = false;
        if (opened)
        {
            try
            {
                admin = ptlsmr::token_is_administrator(token.get());
            }
            catch (...)
            {
                admin = false;
            }
        }
        RevertToSelf();
        return admin;
    }

    void handle_request(const request& input, reply& output)
    {
        output.command = input.command;
        if (input.magic != ptlsmr::ProtocolMagic ||
            input.version != ptlsmr::ProtocolVersion)
        {
            throw ptlsmr::win32_error("pipe request protocol", ERROR_INVALID_DATA);
        }
        if (input.ownerSid[ARRAYSIZE(input.ownerSid) - 1] != L'\0')
        {
            throw ptlsmr::win32_error("pipe owner SID length", ERROR_INVALID_SID);
        }
        switch (static_cast<command>(input.command))
        {
        case command::provision_v1:
        {
            const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
            provision(owner);
            copy_bounded(output.packageFullName, ARRAYSIZE(output.packageFullName), ptlsmr::expected_package_full_name(1));
            fill_status(owner, output);
            break;
        }
        case command::upgrade_v2:
            if (input.ownerSid[0] != L'\0')
            {
                throw ptlsmr::win32_error("upgrade owner argument policy", ERROR_INVALID_PARAMETER);
            }
            upgrade();
            copy_bounded(output.packageFullName, ARRAYSIZE(output.packageFullName), ptlsmr::expected_package_full_name(2));
            break;
        case command::status:
        {
            const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
            fill_status(owner, output);
            break;
        }
        case command::cleanup:
        {
            const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
            cleanup(owner);
            break;
        }
        default:
            throw ptlsmr::win32_error("pipe command policy", ERROR_INVALID_FUNCTION);
        }
    }

    void set_failure_service_status(const request& input, reply& output) noexcept
    {
        try
        {
            if (input.ownerSid[0] == L'\0')
            {
                return;
            }
            const auto names = ptlsmr::instance_names(input.ownerSid);
            service_handle scm(OpenSCManagerW(nullptr, nullptr, SC_MANAGER_CONNECT));
            if (!scm)
            {
                return;
            }
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

    enum class pipe_operation_result
    {
        completed,
        stopped,
        failed,
    };

    [[nodiscard]] pipe_operation_result wait_for_pipe_operation(
        HANDLE pipe,
        OVERLAPPED& operation,
        DWORD& transferred)
    {
        const HANDLE events[] = { g_stopEvent.get(), operation.hEvent };
        const DWORD wait = WaitForMultipleObjects(ARRAYSIZE(events), events, FALSE, INFINITE);
        if (wait == WAIT_OBJECT_0)
        {
            if (!CancelIoEx(pipe, &operation) && GetLastError() != ERROR_NOT_FOUND)
            {
                return pipe_operation_result::failed;
            }
            if (!GetOverlappedResult(pipe, &operation, &transferred, TRUE) &&
                GetLastError() != ERROR_OPERATION_ABORTED)
            {
                return pipe_operation_result::failed;
            }
            return pipe_operation_result::stopped;
        }
        if (wait != WAIT_OBJECT_0 + 1 ||
            !GetOverlappedResult(pipe, &operation, &transferred, FALSE))
        {
            return pipe_operation_result::failed;
        }
        return pipe_operation_result::completed;
    }

    [[nodiscard]] pipe_operation_result connect_pipe(HANDLE pipe)
    {
        ptlsmr::unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            return pipe_operation_result::failed;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        if (ConnectNamedPipe(pipe, &operation))
        {
            return pipe_operation_result::completed;
        }
        const DWORD error = GetLastError();
        if (error == ERROR_PIPE_CONNECTED)
        {
            return pipe_operation_result::completed;
        }
        if (error != ERROR_IO_PENDING)
        {
            return pipe_operation_result::failed;
        }
        DWORD transferred = 0;
        return wait_for_pipe_operation(pipe, operation, transferred);
    }

    [[nodiscard]] pipe_operation_result read_pipe_message(
        HANDLE pipe,
        void* buffer,
        DWORD size,
        DWORD& transferred)
    {
        ptlsmr::unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            return pipe_operation_result::failed;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        if (ReadFile(pipe, buffer, size, nullptr, &operation))
        {
            return GetOverlappedResult(pipe, &operation, &transferred, FALSE)
                ? pipe_operation_result::completed
                : pipe_operation_result::failed;
        }
        if (GetLastError() != ERROR_IO_PENDING)
        {
            return pipe_operation_result::failed;
        }
        return wait_for_pipe_operation(pipe, operation, transferred);
    }

    [[nodiscard]] pipe_operation_result write_pipe_message(
        HANDLE pipe,
        const void* buffer,
        DWORD size,
        DWORD& transferred)
    {
        ptlsmr::unique_handle event(CreateEventW(nullptr, TRUE, FALSE, nullptr));
        if (!event)
        {
            return pipe_operation_result::failed;
        }
        OVERLAPPED operation{};
        operation.hEvent = event.get();
        if (WriteFile(pipe, buffer, size, nullptr, &operation))
        {
            return GetOverlappedResult(pipe, &operation, &transferred, FALSE)
                ? pipe_operation_result::completed
                : pipe_operation_result::failed;
        }
        if (GetLastError() != ERROR_IO_PENDING)
        {
            return pipe_operation_result::failed;
        }
        return wait_for_pipe_operation(pipe, operation, transferred);
    }

    void serve_client(HANDLE pipe)
    {
        request input{};
        reply output{};
        DWORD transferred = 0;
        if (read_pipe_message(pipe, &input, sizeof(input), transferred) !=
                pipe_operation_result::completed ||
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
        catch (const winrt::hresult_error& error)
        {
            output.hresult = static_cast<int32_t>(error.code());
            set_failure_service_status(input, output);
        }
        catch (...)
        {
            output.win32Status = ERROR_UNHANDLED_EXCEPTION;
            set_failure_service_status(input, output);
        }
        (void)write_pipe_message(pipe, &output, sizeof(output), transferred);
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
            return;
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
                sizeof(reply),
                sizeof(request),
                0,
                &attributes));
            if (!pipe)
            {
                return;
            }
            if (connect_pipe(pipe.get()) != pipe_operation_result::completed)
            {
                continue;
            }
            serve_client(pipe.get());
            DisconnectNamedPipe(pipe.get());
        }
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
            ptlsmr::protect_system_directory(ptlsmr::installed_updater_root());
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            g_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!g_stopEvent)
            {
                throw ptlsmr::win32_error("CreateEventW(updater stop)", GetLastError());
            }
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
    wchar_t updaterName[] = L"PtLsmrUpdater";
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
