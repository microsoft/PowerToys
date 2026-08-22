#include "../Common/LsmrCommon.h"

#include <filesystem>
#include <optional>
#include <sstream>

#ifndef PT_RUNTIME_TRACK
#define PT_RUNTIME_TRACK 1
#endif
#ifndef PT_RUNTIME_VERSION_MAJOR
#define PT_RUNTIME_VERSION_MAJOR 1
#endif
#ifndef PT_RUNTIME_VERSION_MINOR
#define PT_RUNTIME_VERSION_MINOR 0
#endif
#ifndef PT_RUNTIME_VERSION_BUILD
#define PT_RUNTIME_VERSION_BUILD 0
#endif
#ifndef PT_RUNTIME_VERSION_REVISION
#define PT_RUNTIME_VERSION_REVISION 0
#endif
#ifndef PT_RUNTIME_FAIL_READINESS
#define PT_RUNTIME_FAIL_READINESS 0
#endif
#ifndef PT_RUNTIME_PAYLOAD_VARIANT
#define PT_RUNTIME_PAYLOAD_VARIANT 0
#endif

namespace
{
    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    SERVICE_STATUS g_status{};
    ptlsmr::unique_handle g_stopEvent;
    std::wstring g_ownerSid;
    std::wstring g_serviceName;
    uint16_t g_runtimeTrack = 0;
    ptlsmr::file_version g_runtimeVersion{};
    std::optional<std::wstring> g_siblingOwner;

    void report_status(DWORD state, DWORD win32ExitCode = NO_ERROR)
    {
        g_status.dwServiceType = SERVICE_WIN32_OWN_PROCESS;
        g_status.dwCurrentState = state;
        g_status.dwWin32ExitCode = win32ExitCode;
        g_status.dwServiceSpecificExitCode = 0;
        g_status.dwControlsAccepted =
            state == SERVICE_RUNNING ? SERVICE_ACCEPT_STOP | SERVICE_ACCEPT_SHUTDOWN : 0;
        g_status.dwWaitHint = state == SERVICE_START_PENDING ? 10000 : 0;
        g_status.dwCheckPoint = state == SERVICE_START_PENDING ? 1 : 0;
        if (g_statusHandle)
        {
            SetServiceStatus(g_statusHandle, &g_status);
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
            throw ptlsmr::win32_error("GetModuleFileNameW(runtime)", GetLastError());
        }
        path.resize(characters);
        return path;
    }

    void require_denied_binary_write(const std::filesystem::path& executable)
    {
        HANDLE raw = CreateFileW(
            executable.c_str(),
            FILE_WRITE_DATA,
            FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
            nullptr,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (raw != INVALID_HANDLE_VALUE)
        {
            CloseHandle(raw);
            throw ptlsmr::win32_error("runtime self-binary write protection", ERROR_ACCESS_DENIED);
        }
        if (GetLastError() != ERROR_ACCESS_DENIED)
        {
            throw ptlsmr::win32_error("runtime self-binary write probe", GetLastError());
        }
    }

    void require_denied_sibling_store_write()
    {
        if (!g_siblingOwner)
        {
            return;
        }
        const auto sibling = ptlsmr::instance_names(*g_siblingOwner);
        const auto probe = sibling.storeDirectory / L"runtime-write-probe.txt";
        HANDLE raw = CreateFileW(
            probe.c_str(),
            GENERIC_WRITE,
            0,
            nullptr,
            CREATE_NEW,
            FILE_ATTRIBUTE_NORMAL,
            nullptr);
        if (raw != INVALID_HANDLE_VALUE)
        {
            CloseHandle(raw);
            DeleteFileW(probe.c_str());
            throw ptlsmr::win32_error("runtime sibling-store write protection", ERROR_ACCESS_DENIED);
        }
        if (GetLastError() != ERROR_ACCESS_DENIED)
        {
            throw ptlsmr::win32_error("runtime sibling-store write probe", GetLastError());
        }
    }

    void write_evidence(bool ready)
    {
        const auto names = ptlsmr::instance_names(g_ownerSid);
        if (!std::filesystem::is_directory(names.storeDirectory))
        {
            throw ptlsmr::win32_error("runtime store missing", ERROR_PATH_NOT_FOUND);
        }
        const std::wstring tokenUserSid = ptlsmr::current_token_user_sid();
        const std::wstring expectedServiceSid = ptlsmr::service_sid(g_serviceName);
        if (tokenUserSid != expectedServiceSid)
        {
            throw ptlsmr::win32_error("runtime virtual-account token policy", ERROR_ACCESS_DENIED);
        }
        HANDLE rawToken = nullptr;
        ptlsmr::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken),
            "OpenProcessToken(runtime)");
        ptlsmr::unique_handle token(rawToken);
        if (!ptlsmr::token_contains_sid(token.get(), expectedServiceSid))
        {
            throw ptlsmr::win32_error("runtime service SID token policy", ERROR_ACCESS_DENIED);
        }

        const auto executable = module_path();
        const auto expectedExecutable = ptlsmr::runtime_executable_path(
            g_runtimeTrack,
            g_runtimeVersion);
        if (!std::filesystem::equivalent(executable, expectedExecutable))
        {
            throw ptlsmr::win32_error("runtime protected execution path policy", ERROR_ACCESS_DENIED);
        }
        const auto packageFullNameResult = ptlsmr::require_no_package_identity();
        require_denied_binary_write(executable);
        require_denied_sibling_store_write();

        std::wstringstream evidence;
        evidence << L"serviceName=" << g_serviceName << L"\r\n";
        evidence << L"ownerSid=" << g_ownerSid << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"tokenUserSid=" << tokenUserSid << L"\r\n";
        evidence << L"virtualAccountName=NT SERVICE\\" << g_serviceName << L"\r\n";
        evidence << L"serviceSid=" << expectedServiceSid << L"\r\n";
        evidence << L"serviceSidPresent=true\r\n";
        evidence << L"runtimeTrack=" << g_runtimeTrack << L"\r\n";
        evidence << L"runtimeVersion=" << ptlsmr::format_version(g_runtimeVersion) << L"\r\n";
        evidence << L"payloadVariant=" << PT_RUNTIME_PAYLOAD_VARIANT << L"\r\n";
        evidence << L"packageFullNameResult=" << packageFullNameResult << L"\r\n";
        evidence << L"packageIdentityPresent=false\r\n";
        evidence << L"executablePath=" << executable.wstring() << L"\r\n";
        evidence << L"selfBinaryWriteProbe=denied\r\n";
        evidence << L"siblingStoreWriteProbe=" <<
            (g_siblingOwner ? L"denied" : L"not-configured") << L"\r\n";
        if (g_siblingOwner)
        {
            evidence << L"siblingOwnerSid=" << *g_siblingOwner << L"\r\n";
        }
        evidence << L"readiness=" << (ready ? L"ready" : L"intentional-failure") << L"\r\n";
        ptlsmr::write_utf8_file_atomic(names.evidencePath, evidence.str());
    }

    DWORD WINAPI service_control_handler(
        DWORD control,
        DWORD,
        void*,
        void*)
    {
        if (control == SERVICE_CONTROL_STOP || control == SERVICE_CONTROL_SHUTDOWN)
        {
            report_status(SERVICE_STOP_PENDING);
            SetEvent(g_stopEvent.get());
        }
        return NO_ERROR;
    }

    void WINAPI service_main(DWORD, LPWSTR*)
    {
        g_statusHandle = RegisterServiceCtrlHandlerExW(
            g_serviceName.c_str(),
            service_control_handler,
            nullptr);
        if (!g_statusHandle)
        {
            return;
        }
        report_status(SERVICE_START_PENDING);
        try
        {
            g_stopEvent.reset(CreateEventW(nullptr, TRUE, FALSE, nullptr));
            if (!g_stopEvent)
            {
                throw ptlsmr::win32_error("CreateEventW(runtime stop)", GetLastError());
            }
            constexpr bool failReadiness = PT_RUNTIME_FAIL_READINESS != 0;
            write_evidence(!failReadiness);
            if (failReadiness)
            {
                throw ptlsmr::win32_error(
                    "intentional runtime readiness failure",
                    ERROR_SERVICE_NOT_ACTIVE);
            }
            report_status(SERVICE_RUNNING);
            const DWORD wait = WaitForSingleObject(g_stopEvent.get(), INFINITE);
            if (wait != WAIT_OBJECT_0)
            {
                throw ptlsmr::win32_error("WaitForSingleObject(runtime stop)", GetLastError());
            }
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
    try
    {
        const auto arguments = ptlsmr::command_line_arguments();
        const auto compiledVersion = ptlsmr::file_version{
            PT_RUNTIME_VERSION_MAJOR,
            PT_RUNTIME_VERSION_MINOR,
            PT_RUNTIME_VERSION_BUILD,
            PT_RUNTIME_VERSION_REVISION,
        };
        g_ownerSid = ptlsmr::canonical_owner_sid(
            ptlsmr::argument_value(arguments, L"--owner-sid"));
        g_serviceName = ptlsmr::argument_value(arguments, L"--service-name");
        const auto trackText = ptlsmr::argument_value(arguments, L"--runtime-track");
        const auto versionText = ptlsmr::argument_value(arguments, L"--runtime-version");
        if ((arguments.size() != 9 && arguments.size() != 11) ||
            (trackText != L"1" && trackText != L"2"))
        {
            return ERROR_INVALID_PARAMETER;
        }
        g_runtimeTrack = static_cast<uint16_t>(trackText[0] - L'0');
        g_runtimeVersion = ptlsmr::parse_version(versionText);
        if (g_runtimeTrack != PT_RUNTIME_TRACK ||
            !(g_runtimeVersion == compiledVersion) ||
            g_runtimeVersion.major != g_runtimeTrack)
        {
            return ERROR_REVISION_MISMATCH;
        }
        const auto names = ptlsmr::instance_names(g_ownerSid);
        if (g_serviceName != names.serviceName || g_serviceName.size() > 128)
        {
            return ERROR_INVALID_NAME;
        }
        if (arguments.size() == 11)
        {
            if (arguments[9] != L"--sibling-owner-sid")
            {
                return ERROR_INVALID_PARAMETER;
            }
            g_siblingOwner = ptlsmr::canonical_owner_sid(arguments[10]);
            if (*g_siblingOwner == g_ownerSid)
            {
                return ERROR_INVALID_PARAMETER;
            }
        }
        SERVICE_TABLE_ENTRYW table[] = {
            { g_serviceName.data(), service_main },
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
