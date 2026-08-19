#include "../Common/LsmrCommon.h"

#include <aclapi.h>
#include <appmodel.h>
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

    [[nodiscard]] std::wstring current_package_value(
        LONG(WINAPI* getter)(UINT32*, PWSTR),
        const char* operation)
    {
        UINT32 characters = 0;
        LONG result = getter(&characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error(operation, static_cast<DWORD>(result));
        }
        std::wstring value(characters, L'\0');
        result = getter(&characters, value.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error(operation, static_cast<DWORD>(result));
        }
        value.resize(characters - 1);
        return value;
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

    void write_updater_evidence()
    {
        const std::wstring packageFullName =
            current_package_value(GetCurrentPackageFullName, "GetCurrentPackageFullName(updater)");
        const std::wstring packageFamilyName =
            current_package_value(GetCurrentPackageFamilyName, "GetCurrentPackageFamilyName(updater)");
        const std::filesystem::path packagePath(
            current_package_value(GetCurrentPackagePath, "GetCurrentPackagePath(updater)"));
        const std::filesystem::path executablePath = module_path();
        if (packageFullName != ptlsmr::expected_updater_package_full_name() ||
            packageFamilyName != ptlsmr::expected_updater_package_family_name() ||
            !std::filesystem::equivalent(
                executablePath,
                packagePath / ptlsmr::UpdaterExe))
        {
            throw ptlsmr::win32_error("updater package identity policy", ERROR_INVALID_DATA);
        }
        DWORD sessionId = 0;
        ptlsmr::check_bool(
            ProcessIdToSessionId(GetCurrentProcessId(), &sessionId),
            "ProcessIdToSessionId(updater)");
        std::wstringstream evidence;
        evidence << L"serviceName=" << ptlsmr::UpdaterServiceName << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"sessionId=" << sessionId << L"\r\n";
        evidence << L"tokenUserSid=" << ptlsmr::current_token_user_sid() << L"\r\n";
        evidence << L"packageIdentityPresent=true\r\n";
        evidence << L"packageFullName=" << packageFullName << L"\r\n";
        evidence << L"packageFamilyName=" << packageFamilyName << L"\r\n";
        evidence << L"packageVersion=" << ptlsmr::package_version_string(packageFullName) << L"\r\n";
        evidence << L"fileVersion=5.0.0.0\r\n";
        evidence << L"protocolVersion=" << ptlsmr::ProtocolVersion << L"\r\n";
        evidence << L"packageInstalledLocation=" << packagePath.wstring() << L"\r\n";
        evidence << L"executablePath=" << executablePath.wstring() << L"\r\n";
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::program_data_root() / L"updater-evidence.txt",
            evidence.str());
    }

    [[nodiscard]] std::wstring owners_path()
    {
        return (ptlsmr::program_data_root() / L"instances.txt").wstring();
    }

    struct managed_instance
    {
        std::wstring ownerSid;
        uint16_t runtimeTrack{};
    };

    [[nodiscard]] std::vector<managed_instance> read_instances()
    {
        const std::filesystem::path path(owners_path());
        if (!std::filesystem::exists(path))
        {
            return {};
        }
        const std::wstring contents = ptlsmr::read_utf8_file(path, 16 * 1024);
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
                const size_t separator = line.find(L'|');
                if (separator == std::wstring_view::npos ||
                    separator + 2 != line.size() ||
                    (line[separator + 1] != L'1' && line[separator + 1] != L'2'))
                {
                    throw ptlsmr::win32_error("instance inventory format", ERROR_INVALID_DATA);
                }
                managed_instance instance{
                    ptlsmr::canonical_owner_sid(line.substr(0, separator)),
                    static_cast<uint16_t>(line[separator + 1] - L'0'),
                };
                const auto duplicate = std::find_if(
                    instances.begin(),
                    instances.end(),
                    [&](const managed_instance& value) {
                        return value.ownerSid == instance.ownerSid;
                    });
                if (duplicate != instances.end())
                {
                    throw ptlsmr::win32_error("duplicate owner inventory", ERROR_INVALID_DATA);
                }
                if (instances.size() >= 32)
                {
                    throw ptlsmr::win32_error("instance list limit", ERROR_TOO_MANY_NAMES);
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
        return instances;
    }

    void write_instances(const std::vector<managed_instance>& instances)
    {
        std::wstringstream output;
        for (const auto& instance : instances)
        {
            if (instance.runtimeTrack != 1 && instance.runtimeTrack != 2)
            {
                throw ptlsmr::win32_error("runtime track inventory policy", ERROR_INVALID_DATA);
            }
            output << ptlsmr::canonical_owner_sid(instance.ownerSid)
                   << L"|" << instance.runtimeTrack << L"\r\n";
        }
        ptlsmr::write_utf8_file_atomic(owners_path(), output.str());
    }

    [[nodiscard]] uint16_t inventory_track_for_owner(const std::wstring& owner)
    {
        const auto instances = read_instances();
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& value) {
                return value.ownerSid == owner;
            });
        return found == instances.end() ? 0 : found->runtimeTrack;
    }

    void upsert_instance(const std::wstring& owner, uint16_t runtimeTrack)
    {
        auto instances = read_instances();
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& value) {
                return value.ownerSid == owner;
            });
        if (found == instances.end())
        {
            instances.push_back({ owner, runtimeTrack });
        }
        else
        {
            found->runtimeTrack = runtimeTrack;
        }
        write_instances(instances);
    }

    uint16_t remove_instance(const std::wstring& owner)
    {
        auto instances = read_instances();
        const auto found = std::find_if(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& value) {
                return value.ownerSid == owner;
            });
        if (found == instances.end())
        {
            return 0;
        }
        const uint16_t runtimeTrack = found->runtimeTrack;
        instances.erase(found);
        write_instances(instances);
        return runtimeTrack;
    }

    [[nodiscard]] bool track_is_in_use(uint16_t runtimeTrack)
    {
        const auto instances = read_instances();
        return std::any_of(
            instances.begin(),
            instances.end(),
            [&](const managed_instance& value) {
                return value.runtimeTrack == runtimeTrack;
            });
    }

    [[nodiscard]] std::wstring service_binary_path(
        const std::filesystem::path& packageDirectory,
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack)
    {
        const auto executable = packageDirectory / ptlsmr::RuntimeExe;
        return ptlsmr::quote_argument(executable.wstring()) +
            L" --service-name " + ptlsmr::quote_argument(names.serviceName) +
            L" --owner-sid " + ptlsmr::quote_argument(names.ownerSid) +
            L" --runtime-track " + std::to_wstring(runtimeTrack);
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
        const ptlsmr::InstanceNames& names,
        uint16_t runtimeTrack)
    {
        int count = 0;
        LPWSTR* rawArguments = CommandLineToArgvW(imagePath.c_str(), &count);
        if (!rawArguments)
        {
            return false;
        }
        ptlsmr::local_memory arguments(rawArguments);
        return count == 7 &&
            equal_path(rawArguments[0], expectedExecutable) &&
            rawArguments[1] == std::wstring_view(L"--service-name") &&
            rawArguments[2] == names.serviceName &&
            rawArguments[3] == std::wstring_view(L"--owner-sid") &&
            rawArguments[4] == names.ownerSid &&
            rawArguments[5] == std::wstring_view(L"--runtime-track") &&
            rawArguments[6] == std::to_wstring(runtimeTrack);
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

    [[nodiscard]] std::filesystem::path cached_deployment_helper()
    {
        const std::filesystem::path sourcePath =
            module_path().parent_path() / ptlsmr::DeploymentHelperExe;
        if (!std::filesystem::is_regular_file(sourcePath))
        {
            throw ptlsmr::win32_error("deployment helper missing", ERROR_FILE_NOT_FOUND);
        }
        const std::filesystem::path helperDirectory =
            ptlsmr::program_data_root() / L"DeploymentHelper" / L"5.0.0.0";
        ptlsmr::protect_system_directory(helperDirectory);
        const std::filesystem::path helperPath =
            helperDirectory / ptlsmr::DeploymentHelperExe;
        if (!std::filesystem::is_regular_file(helperPath))
        {
            std::filesystem::copy_file(
                sourcePath,
                helperPath,
                std::filesystem::copy_options::overwrite_existing);
        }
        return helperPath;
    }

    void run_deployment_helper(std::wstring_view arguments)
    {
        const std::filesystem::path helperPath = cached_deployment_helper();
        std::wstring commandLine =
            ptlsmr::quote_argument(helperPath.wstring()) + L" " + std::wstring(arguments);
        std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
        mutableCommand.push_back(L'\0');
        STARTUPINFOW startup{ sizeof(startup) };
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(
                helperPath.c_str(),
                mutableCommand.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW,
                nullptr,
                helperPath.parent_path().c_str(),
                &startup,
                &process))
        {
            throw ptlsmr::win32_error("CreateProcessW(deployment helper)", GetLastError());
        }
        ptlsmr::unique_handle processHandle(process.hProcess);
        ptlsmr::unique_handle threadHandle(process.hThread);
        const DWORD wait = WaitForSingleObject(processHandle.get(), 120000);
        if (wait == WAIT_TIMEOUT)
        {
            TerminateProcess(processHandle.get(), ERROR_TIMEOUT);
            WaitForSingleObject(processHandle.get(), 30000);
            throw ptlsmr::win32_error("deployment helper timeout", ERROR_TIMEOUT);
        }
        if (wait != WAIT_OBJECT_0)
        {
            throw ptlsmr::win32_error("WaitForSingleObject(deployment helper)", GetLastError());
        }
        DWORD exitCode = ERROR_UNHANDLED_EXCEPTION;
        ptlsmr::check_bool(
            GetExitCodeProcess(processHandle.get(), &exitCode),
            "GetExitCodeProcess(deployment helper)");
        if (exitCode == ERROR_SUCCESS)
        {
            return;
        }
        if ((exitCode & 0x80000000U) != 0)
        {
            throw winrt::hresult_error(
                static_cast<HRESULT>(exitCode),
                L"deployment helper failed");
        }
        throw ptlsmr::win32_error("deployment helper failed", exitCode);
    }

    void stage_exact_package(
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedPackagePath)
    {
        if (runtimeTrack != 1 && runtimeTrack != 2)
        {
            throw ptlsmr::win32_error("runtime track policy", ERROR_INVALID_PARAMETER);
        }
        const std::filesystem::path packagePath =
            std::filesystem::weakly_canonical(suppliedPackagePath);
        if (!std::filesystem::is_regular_file(packagePath))
        {
            throw ptlsmr::win32_error("runtime MSIX missing", ERROR_FILE_NOT_FOUND);
        }
        if (_wcsicmp(packagePath.extension().c_str(), L".msix") != 0)
        {
            throw ptlsmr::win32_error("runtime package extension policy", ERROR_INVALID_NAME);
        }
        run_deployment_helper(
            L"--stage --runtime-track " + std::to_wstring(runtimeTrack) +
            L" --runtime-package " + ptlsmr::quote_argument(packagePath.wstring()));
    }

    [[nodiscard]] std::filesystem::path package_directory(const std::wstring& fullName)
    {
        if (!ptlsmr::is_allowed_runtime_package_full_name(fullName))
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
        return std::filesystem::path(programFiles) / L"WindowsApps" / fullName;
    }

    [[nodiscard]] std::filesystem::path staged_package_directory(const std::wstring& fullName)
    {
        const std::filesystem::path location = package_directory(fullName);
        const std::filesystem::path executable = location / ptlsmr::RuntimeExe;
        if (!std::filesystem::is_regular_file(executable))
        {
            throw ptlsmr::win32_error("registered runtime executable", ERROR_FILE_NOT_FOUND);
        }
        std::wstring expectedPrefix = std::filesystem::weakly_canonical(
            location.parent_path()).wstring();
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
            binaryPath.c_str(),
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

    void verify_or_repath_runtime_service(
        SC_HANDLE service,
        const std::filesystem::path& expectedCurrentExecutable,
        const std::filesystem::path& desiredExecutable,
        const ptlsmr::InstanceNames& names,
        bool allowRepath)
    {
        const auto buffer = query_service_config(service);
        const auto* config = reinterpret_cast<const QUERY_SERVICE_CONFIGW*>(buffer.data());
        const std::wstring expectedAccount = L"NT SERVICE\\" + names.serviceName;
        if (config->dwServiceType != SERVICE_WIN32_OWN_PROCESS ||
            !config->lpServiceStartName ||
            _wcsicmp(config->lpServiceStartName, expectedAccount.c_str()) != 0)
        {
            throw ptlsmr::win32_error("runtime service account policy", ERROR_ACCESS_DENIED);
        }
        const std::wstring currentPath(config->lpBinaryPathName ? config->lpBinaryPathName : L"");
        const uint16_t desiredTrack =
            ptlsmr::runtime_track_from_package_full_name(desiredExecutable.parent_path().filename().wstring());
        const uint16_t currentTrack =
            ptlsmr::runtime_track_from_package_full_name(expectedCurrentExecutable.parent_path().filename().wstring());
        if (matches_runtime_service_command(currentPath, desiredExecutable, names, desiredTrack))
        {
            return;
        }
        if (!allowRepath ||
            !matches_runtime_service_command(
                currentPath,
                expectedCurrentExecutable,
                names,
                currentTrack))
        {
            throw ptlsmr::win32_error("runtime ImagePath policy", ERROR_ACCESS_DENIED);
        }
        const std::wstring desiredPath =
            service_binary_path(desiredExecutable.parent_path(), names, desiredTrack);
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

    void grant_runtime_package_access(
        const std::filesystem::path& packageDirectory,
        std::wstring_view serviceSid)
    {
        std::wstring sidText(serviceSid);
        PSID sid = nullptr;
        if (!ConvertStringSidToSidW(sidText.c_str(), &sid))
        {
            throw ptlsmr::win32_error(
                "ConvertStringSidToSidW(runtime package ACL)",
                GetLastError());
        }
        ptlsmr::local_memory sidMemory(sid);
        for (const auto& [path, inheritance] : {
                 std::pair{ packageDirectory, static_cast<DWORD>(SUB_CONTAINERS_AND_OBJECTS_INHERIT) },
                 std::pair{ packageDirectory / ptlsmr::RuntimeExe, static_cast<DWORD>(NO_INHERITANCE) } })
        {
            PACL existingDacl = nullptr;
            PSECURITY_DESCRIPTOR descriptor = nullptr;
            std::wstring mutablePath = path.wstring();
            const DWORD queryResult = GetNamedSecurityInfoW(
                mutablePath.data(),
                SE_FILE_OBJECT,
                DACL_SECURITY_INFORMATION,
                nullptr,
                nullptr,
                &existingDacl,
                nullptr,
                &descriptor);
            if (queryResult != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "GetNamedSecurityInfoW(runtime package ACL)",
                    queryResult);
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
            const DWORD aclResult =
                SetEntriesInAclW(1, &access, existingDacl, &updatedDacl);
            if (aclResult != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "SetEntriesInAclW(runtime package ACL)",
                    aclResult);
            }
            ptlsmr::local_memory aclMemory(updatedDacl);
            const DWORD setResult = SetNamedSecurityInfoW(
                mutablePath.data(),
                SE_FILE_OBJECT,
                DACL_SECURITY_INFORMATION,
                nullptr,
                nullptr,
                updatedDacl,
                nullptr);
            if (setResult != ERROR_SUCCESS)
            {
                throw ptlsmr::win32_error(
                    "SetNamedSecurityInfoW(runtime package ACL)",
                    setResult);
            }
        }
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

    void ensure_package_staged(
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedPackagePath)
    {
        const auto fullName = ptlsmr::expected_runtime_package_full_name(runtimeTrack);
        stage_exact_package(runtimeTrack, suppliedPackagePath);
        (void)staged_package_directory(fullName);
    }

    void remove_exact_package(uint16_t runtimeTrack)
    {
        run_deployment_helper(
            L"--remove --runtime-track " + std::to_wstring(runtimeTrack));
    }

    void provision(
        const std::wstring& owner,
        uint16_t runtimeTrack,
        const std::filesystem::path& suppliedPackagePath)
    {
        ensure_package_staged(runtimeTrack, suppliedPackagePath);
        const auto names = ptlsmr::instance_names(owner);
        const auto packageDirectory = staged_package_directory(
            ptlsmr::expected_runtime_package_full_name(runtimeTrack));
        const uint16_t previousTrack = inventory_track_for_owner(owner);
        auto scm = open_scm();
        bool created = false;
        auto service = create_or_open_runtime_service(
            scm,
            names,
            service_binary_path(packageDirectory, names, runtimeTrack),
            created);
        try
        {
            if (previousTrack != 0 && previousTrack != runtimeTrack)
            {
                const auto previousExecutable =
                    package_directory(
                        ptlsmr::expected_runtime_package_full_name(previousTrack)) /
                    ptlsmr::RuntimeExe;
                stop_service(service.get());
                verify_or_repath_runtime_service(
                    service.get(),
                    previousExecutable,
                    packageDirectory / ptlsmr::RuntimeExe,
                    names,
                    true);
            }
            else
            {
                verify_or_repath_runtime_service(
                    service.get(),
                    packageDirectory / ptlsmr::RuntimeExe,
                    packageDirectory / ptlsmr::RuntimeExe,
                    names,
                    false);
            }
            configure_service_sid(service.get());
            const std::wstring runtimeServiceSid =
                ptlsmr::service_sid(names.serviceName);
            grant_runtime_package_access(packageDirectory, runtimeServiceSid);
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            ptlsmr::protect_directory_for_service(
                names.storeDirectory,
                runtimeServiceSid);
            start_runtime_service(service.get());
            upsert_instance(owner, runtimeTrack);
            if (previousTrack != 0 &&
                previousTrack != runtimeTrack &&
                !track_is_in_use(previousTrack))
            {
                remove_exact_package(previousTrack);
            }
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
                DeleteService(service.get());
            }
            throw;
        }
    }

    void fill_status(const std::wstring& owner, reply& response)
    {
        const uint16_t runtimeTrack = inventory_track_for_owner(owner);
        if (runtimeTrack == 0)
        {
            throw ptlsmr::win32_error("owner inventory lookup", ERROR_NOT_FOUND);
        }
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
        copy_bounded(
            response.packageFullName,
            ARRAYSIZE(response.packageFullName),
            ptlsmr::expected_runtime_package_full_name(runtimeTrack));
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
        const uint16_t runtimeTrack = remove_instance(owner);
        if (runtimeTrack != 0 && !track_is_in_use(runtimeTrack))
        {
            remove_exact_package(runtimeTrack);
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
        if (input.packagePath[ARRAYSIZE(input.packagePath) - 1] != L'\0' ||
            input.reserved != 0)
        {
            throw ptlsmr::win32_error("pipe request bounds", ERROR_INVALID_DATA);
        }
        switch (static_cast<command>(input.command))
        {
        case command::provision:
        {
            const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
            if ((input.runtimeTrack != 1 && input.runtimeTrack != 2) ||
                input.packagePath[0] == L'\0')
            {
                throw ptlsmr::win32_error("provision request policy", ERROR_INVALID_PARAMETER);
            }
            provision(owner, input.runtimeTrack, input.packagePath);
            copy_bounded(
                output.packageFullName,
                ARRAYSIZE(output.packageFullName),
                ptlsmr::expected_runtime_package_full_name(input.runtimeTrack));
            fill_status(owner, output);
            break;
        }
        case command::status:
        {
            if (input.runtimeTrack != 0 || input.packagePath[0] != L'\0')
            {
                throw ptlsmr::win32_error("status request policy", ERROR_INVALID_PARAMETER);
            }
            const auto owner = ptlsmr::canonical_owner_sid(input.ownerSid);
            fill_status(owner, output);
            break;
        }
        case command::cleanup:
        {
            if (input.runtimeTrack != 0 || input.packagePath[0] != L'\0')
            {
                throw ptlsmr::win32_error("cleanup request policy", ERROR_INVALID_PARAMETER);
            }
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
            ptlsmr::protect_system_directory(ptlsmr::program_data_root());
            write_updater_evidence();
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
