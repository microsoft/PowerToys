#include "../Common/LsmrCommon.h"

#include <appmodel.h>

#include <atomic>
#include <filesystem>
#include <sstream>

namespace
{
    SERVICE_STATUS_HANDLE g_statusHandle = nullptr;
    SERVICE_STATUS g_status{};
    ptlsmr::unique_handle g_stopEvent;
    std::wstring g_ownerSid;
    std::wstring g_serviceName;

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

    [[nodiscard]] std::wstring current_package_full_name()
    {
        UINT32 characters = 0;
        LONG result = GetCurrentPackageFullName(&characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("GetCurrentPackageFullName(size)", static_cast<DWORD>(result));
        }
        std::wstring fullName(characters, L'\0');
        result = GetCurrentPackageFullName(&characters, fullName.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("GetCurrentPackageFullName", static_cast<DWORD>(result));
        }
        fullName.resize(characters - 1);
        return fullName;
    }

    [[nodiscard]] std::wstring current_package_path()
    {
        UINT32 characters = 0;
        LONG result = GetCurrentPackagePath(&characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("GetCurrentPackagePath(size)", static_cast<DWORD>(result));
        }
        std::wstring path(characters, L'\0');
        result = GetCurrentPackagePath(&characters, path.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("GetCurrentPackagePath", static_cast<DWORD>(result));
        }
        path.resize(characters - 1);
        return path;
    }

    [[nodiscard]] std::wstring current_package_family_name()
    {
        UINT32 characters = 0;
        LONG result = GetCurrentPackageFamilyName(&characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error("GetCurrentPackageFamilyName(size)", static_cast<DWORD>(result));
        }
        std::wstring familyName(characters, L'\0');
        result = GetCurrentPackageFamilyName(&characters, familyName.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error("GetCurrentPackageFamilyName", static_cast<DWORD>(result));
        }
        familyName.resize(characters - 1);
        return familyName;
    }

    [[nodiscard]] std::wstring module_path()
    {
        std::wstring path(32768, L'\0');
        const DWORD characters = GetModuleFileNameW(
            nullptr,
            path.data(),
            static_cast<DWORD>(path.size()));
        if (characters == 0 || characters >= path.size())
        {
            throw ptlsmr::win32_error("GetModuleFileNameW", GetLastError());
        }
        path.resize(characters);
        return path;
    }

    [[nodiscard]] bool path_is_under(
        const std::filesystem::path& child,
        const std::filesystem::path& parent)
    {
        const auto canonicalChild = std::filesystem::weakly_canonical(child).wstring();
        std::wstring canonicalParent = std::filesystem::weakly_canonical(parent).wstring();
        if (!canonicalParent.ends_with(L"\\"))
        {
            canonicalParent += L"\\";
        }
        if (canonicalChild.size() <= canonicalParent.size())
        {
            return false;
        }
        return CompareStringOrdinal(
                   canonicalChild.c_str(),
                   static_cast<int>(canonicalParent.size()),
                   canonicalParent.c_str(),
                   static_cast<int>(canonicalParent.size()),
                   TRUE) == CSTR_EQUAL;
    }

    void write_evidence()
    {
        const auto names = ptlsmr::instance_names(g_ownerSid);
        if (!std::filesystem::is_directory(names.storeDirectory))
        {
            throw ptlsmr::win32_error("runtime store missing", ERROR_PATH_NOT_FOUND);
        }
        const std::wstring tokenUserSid = ptlsmr::current_token_user_sid();
        if (tokenUserSid != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("runtime LocalSystem token policy", ERROR_ACCESS_DENIED);
        }
        const std::wstring expectedServiceSid = ptlsmr::service_sid(g_serviceName);
        HANDLE rawToken = nullptr;
        ptlsmr::check_bool(
            OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &rawToken),
            "OpenProcessToken(runtime)");
        ptlsmr::unique_handle token(rawToken);
        const bool hasServiceSid = ptlsmr::token_contains_sid(token.get(), expectedServiceSid);
        if (!hasServiceSid)
        {
            throw ptlsmr::win32_error("runtime service SID token policy", ERROR_ACCESS_DENIED);
        }

        const auto executablePath = std::filesystem::path(module_path());
        std::wstring packageFullName;
        std::wstring packageFamilyName;
        std::filesystem::path packagePath;
        bool packageIdentityPresent = false;
        try
        {
            packageFullName = current_package_full_name();
            packageFamilyName = current_package_family_name();
            packagePath = std::filesystem::path(current_package_path());
            packageIdentityPresent = true;
        }
        catch (const ptlsmr::win32_error& error)
        {
            if (error.code() != APPMODEL_ERROR_NO_PACKAGE)
            {
                throw;
            }
            packagePath = executablePath.parent_path();
            packageFullName = packagePath.filename().wstring();
        }
        if (!ptlsmr::is_allowed_package_full_name(packageFullName))
        {
            throw ptlsmr::win32_error("runtime package path identity policy", ERROR_INVALID_DATA);
        }
        if (!path_is_under(executablePath, packagePath) ||
            !std::filesystem::equivalent(executablePath, packagePath / ptlsmr::RuntimeExe))
        {
            throw ptlsmr::win32_error("runtime direct package executable policy", ERROR_ACCESS_DENIED);
        }

        DWORD sessionId = 0;
        ptlsmr::check_bool(
            ProcessIdToSessionId(GetCurrentProcessId(), &sessionId),
            "ProcessIdToSessionId(runtime)");
        std::wstringstream evidence;
        evidence << L"serviceName=" << g_serviceName << L"\r\n";
        evidence << L"ownerSid=" << g_ownerSid << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"sessionId=" << sessionId << L"\r\n";
        evidence << L"tokenUserSid=" << tokenUserSid << L"\r\n";
        evidence << L"serviceSid=" << expectedServiceSid << L"\r\n";
        evidence << L"serviceSidPresent=" << (hasServiceSid ? L"true" : L"false") << L"\r\n";
        evidence << L"packageIdentityPresent=" << (packageIdentityPresent ? L"true" : L"false") << L"\r\n";
        evidence << L"packageFullName=" << packageFullName << L"\r\n";
        evidence << L"packageFamilyName=" << packageFamilyName << L"\r\n";
        evidence << L"packageVersion=" << ptlsmr::package_major_version(packageFullName) << L".0.0.0\r\n";
        evidence << L"packageInstalledLocation=" << packagePath.wstring() << L"\r\n";
        evidence << L"executablePath=" << executablePath.wstring() << L"\r\n";
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
            write_evidence();
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
        g_ownerSid = ptlsmr::canonical_owner_sid(
            ptlsmr::argument_value(arguments, L"--owner-sid"));
        g_serviceName = ptlsmr::argument_value(arguments, L"--service-name");
        const auto names = ptlsmr::instance_names(g_ownerSid);
        if (g_serviceName != names.serviceName || g_serviceName.size() > 128)
        {
            return ERROR_INVALID_NAME;
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
