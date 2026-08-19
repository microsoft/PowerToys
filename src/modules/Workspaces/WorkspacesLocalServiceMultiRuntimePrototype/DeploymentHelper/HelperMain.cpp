#include "../Common/LsmrCommon.h"

#include <appmodel.h>
#include <shlobj_core.h>
#include <winrt/Windows.Foundation.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Management.Deployment.h>
#include <winrt/base.h>

#include <algorithm>
#include <filesystem>
#include <sstream>

namespace
{
    [[nodiscard]] std::filesystem::path module_path()
    {
        std::wstring path(32768, L'\0');
        const DWORD characters = GetModuleFileNameW(
            nullptr,
            path.data(),
            static_cast<DWORD>(path.size()));
        if (characters == 0 || characters >= path.size())
        {
            throw ptlsmr::win32_error("GetModuleFileNameW(deployment helper)", GetLastError());
        }
        path.resize(characters);
        return path;
    }

    [[nodiscard]] bool package_identity_present()
    {
        UINT32 characters = 0;
        const LONG result = GetCurrentPackageFullName(&characters, nullptr);
        if (result == APPMODEL_ERROR_NO_PACKAGE)
        {
            return false;
        }
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error(
                "GetCurrentPackageFullName(deployment helper)",
                static_cast<DWORD>(result));
        }
        return true;
    }

    [[nodiscard]] std::wstring current_package_full_name()
    {
        UINT32 characters = 0;
        LONG result = GetCurrentPackageFullName(&characters, nullptr);
        if (result != ERROR_INSUFFICIENT_BUFFER)
        {
            throw ptlsmr::win32_error(
                "GetCurrentPackageFullName(deployment helper identity)",
                static_cast<DWORD>(result));
        }
        std::wstring value(characters, L'\0');
        result = GetCurrentPackageFullName(&characters, value.data());
        if (result != ERROR_SUCCESS)
        {
            throw ptlsmr::win32_error(
                "GetCurrentPackageFullName(deployment helper value)",
                static_cast<DWORD>(result));
        }
        value.resize(characters - 1);
        return value;
    }

    [[nodiscard]] std::filesystem::path expected_packaged_helper()
    {
        PWSTR programFiles = nullptr;
        const HRESULT result = SHGetKnownFolderPath(
            FOLDERID_ProgramFiles,
            0,
            nullptr,
            &programFiles);
        if (FAILED(result))
        {
            throw ptlsmr::win32_error(
                "SHGetKnownFolderPath(FOLDERID_ProgramFiles)",
                HRESULT_CODE(result));
        }
        ptlsmr::local_memory memory(programFiles);
        return std::filesystem::path(programFiles) /
            L"WindowsApps" /
            ptlsmr::expected_updater_package_full_name() /
            ptlsmr::DeploymentHelperExe;
    }

    [[nodiscard]] std::filesystem::path expected_cached_helper()
    {
        return ptlsmr::program_data_root() /
            L"DeploymentHelper" /
            L"5.0.0.0" /
            ptlsmr::DeploymentHelperExe;
    }

    void verify_helper_path(const std::filesystem::path& expectedExecutable)
    {
        const auto executable = module_path();
        if (_wcsicmp(executable.filename().c_str(), ptlsmr::DeploymentHelperExe) != 0 ||
            !std::filesystem::equivalent(executable, expectedExecutable))
        {
            throw ptlsmr::win32_error(
                "deployment helper path policy",
                ERROR_ACCESS_DENIED);
        }
    }

    void write_evidence(
        const std::filesystem::path& fileName,
        std::wstring_view operation,
        bool identityPresent,
        std::wstring_view launchMode)
    {
        std::wstringstream evidence;
        evidence << L"operation=" << operation << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"tokenUserSid=" << ptlsmr::current_token_user_sid() << L"\r\n";
        evidence << L"packageIdentityPresent=" << (identityPresent ? L"true" : L"false") << L"\r\n";
        evidence << L"launchMode=" << launchMode << L"\r\n";
        evidence << L"executablePath=" << module_path().wstring() << L"\r\n";
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::program_data_root() / fileName,
            evidence.str());
    }

    void stage_package(uint16_t runtimeTrack, const std::filesystem::path& suppliedPath)
    {
        if (runtimeTrack != 1 && runtimeTrack != 2)
        {
            throw ptlsmr::win32_error("runtime track policy", ERROR_INVALID_PARAMETER);
        }
        const std::filesystem::path packagePath = std::filesystem::weakly_canonical(suppliedPath);
        if (!std::filesystem::is_regular_file(packagePath) ||
            _wcsicmp(packagePath.extension().c_str(), L".msix") != 0)
        {
            throw ptlsmr::win32_error("runtime MSIX source policy", ERROR_FILE_NOT_FOUND);
        }
        std::wstring uriText = L"file:///";
        uriText += packagePath.wstring();
        std::replace(uriText.begin(), uriText.end(), L'\\', L'/');
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto dependencies =
            winrt::single_threaded_vector<winrt::Windows::Foundation::Uri>().GetView();
        const auto result = manager.StagePackageAsync(
            winrt::Windows::Foundation::Uri(uriText),
            dependencies,
            winrt::Windows::Management::Deployment::DeploymentOptions::None)
                                .get();
        if (FAILED(result.ExtendedErrorCode()))
        {
            throw winrt::hresult_error(result.ExtendedErrorCode(), L"StagePackageAsync(helper)");
        }
    }

    void remove_package(uint16_t runtimeTrack)
    {
        winrt::Windows::Management::Deployment::PackageManager manager;
        const auto result = manager.RemovePackageAsync(
            ptlsmr::expected_runtime_package_full_name(runtimeTrack))
                                .get();
        const HRESULT error = result.ExtendedErrorCode();
        if (FAILED(error) &&
            error != HRESULT_FROM_WIN32(ERROR_INSTALL_PACKAGE_NOT_FOUND))
        {
            throw winrt::hresult_error(error, L"RemovePackageAsync(helper)");
        }
    }

    [[nodiscard]] DWORD run_breakaway_child(
        const std::vector<std::wstring>& arguments)
    {
        const auto executable = module_path();
        std::wstring commandLine = ptlsmr::quote_argument(executable.wstring());
        for (size_t index = 1; index < arguments.size(); ++index)
        {
            if (arguments[index] == L"--launch-breakaway-child")
            {
                continue;
            }
            commandLine += L" ";
            commandLine += ptlsmr::quote_argument(arguments[index]);
        }
        std::vector<wchar_t> mutableCommand(commandLine.begin(), commandLine.end());
        mutableCommand.push_back(L'\0');
        STARTUPINFOW startup{ sizeof(startup) };
        PROCESS_INFORMATION process{};
        if (!CreateProcessW(
                executable.c_str(),
                mutableCommand.data(),
                nullptr,
                nullptr,
                FALSE,
                CREATE_NO_WINDOW,
                nullptr,
                executable.parent_path().c_str(),
                &startup,
                &process))
        {
            throw ptlsmr::win32_error(
                "CreateProcessW(breakaway descendant)",
                GetLastError());
        }
        ptlsmr::unique_handle processHandle(process.hProcess);
        ptlsmr::unique_handle threadHandle(process.hThread);
        const DWORD wait = WaitForSingleObject(processHandle.get(), 120000);
        if (wait == WAIT_TIMEOUT)
        {
            TerminateProcess(processHandle.get(), ERROR_TIMEOUT);
            WaitForSingleObject(processHandle.get(), 30000);
            throw ptlsmr::win32_error(
                "breakaway descendant timeout",
                ERROR_TIMEOUT);
        }
        if (wait != WAIT_OBJECT_0)
        {
            throw ptlsmr::win32_error(
                "WaitForSingleObject(breakaway descendant)",
                GetLastError());
        }
        DWORD exitCode = ERROR_UNHANDLED_EXCEPTION;
        ptlsmr::check_bool(
            GetExitCodeProcess(processHandle.get(), &exitCode),
            "GetExitCodeProcess(breakaway descendant)");
        return exitCode;
    }
}

int wmain()
{
    try
    {
        if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("deployment helper LocalSystem policy", ERROR_ACCESS_DENIED);
        }
        const auto arguments = ptlsmr::command_line_arguments();
        const bool identityPresent = package_identity_present();
        if (std::find(
                arguments.begin(),
                arguments.end(),
                L"--probe-inherited-package-identity") != arguments.end())
        {
            verify_helper_path(expected_packaged_helper());
            if (!identityPresent ||
                current_package_full_name() != ptlsmr::expected_updater_package_full_name())
            {
                throw ptlsmr::win32_error(
                    "deployment helper inherited package identity control",
                    ERROR_INVALID_STATE);
            }
            write_evidence(
                L"deployment-helper-inherited-evidence.txt",
                L"identity-control",
                true,
                L"default-child");
            return ERROR_SUCCESS;
        }
        if (std::find(
                arguments.begin(),
                arguments.end(),
                L"--launch-breakaway-child") != arguments.end())
        {
            verify_helper_path(expected_packaged_helper());
            if (!identityPresent ||
                current_package_full_name() != ptlsmr::expected_updater_package_full_name())
            {
                throw ptlsmr::win32_error(
                    "deployment helper breakaway bridge identity",
                    ERROR_INVALID_STATE);
            }
            write_evidence(
                L"deployment-helper-breakaway-bridge-evidence.txt",
                L"breakaway-bridge",
                true,
                L"desktop-app-breakaway-enable-process-tree");
            return static_cast<int>(run_breakaway_child(arguments));
        }
        const auto launchMode = ptlsmr::argument_value(arguments, L"--launch-mode");
        std::filesystem::path evidenceFile;
        if (launchMode == L"desktop-app-breakaway")
        {
            verify_helper_path(expected_packaged_helper());
            evidenceFile = L"deployment-helper-breakaway-evidence.txt";
        }
        else if (launchMode == L"protected-cache")
        {
            verify_helper_path(expected_cached_helper());
            if (identityPresent)
            {
                throw ptlsmr::win32_error(
                    "cached deployment helper package identity policy",
                    ERROR_INVALID_STATE);
            }
            evidenceFile = L"deployment-helper-evidence.txt";
        }
        else
        {
            throw ptlsmr::win32_error(
                "deployment helper launch-mode policy",
                ERROR_INVALID_PARAMETER);
        }
        write_evidence(
            evidenceFile,
            L"starting",
            identityPresent,
            launchMode);
        const auto trackText = ptlsmr::argument_value(arguments, L"--runtime-track");
        if (trackText != L"1" && trackText != L"2")
        {
            throw ptlsmr::win32_error("deployment helper runtime track", ERROR_INVALID_PARAMETER);
        }
        const uint16_t runtimeTrack = static_cast<uint16_t>(trackText[0] - L'0');
        if (std::find(arguments.begin(), arguments.end(), L"--stage") != arguments.end())
        {
            stage_package(
                runtimeTrack,
                ptlsmr::argument_value(arguments, L"--runtime-package"));
            write_evidence(
                evidenceFile,
                L"stage",
                identityPresent,
                launchMode);
            return ERROR_SUCCESS;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--remove") != arguments.end())
        {
            remove_package(runtimeTrack);
            write_evidence(
                evidenceFile,
                L"remove",
                identityPresent,
                launchMode);
            return ERROR_SUCCESS;
        }
        throw ptlsmr::win32_error("deployment helper command", ERROR_INVALID_FUNCTION);
    }
    catch (const winrt::hresult_error& error)
    {
        return static_cast<int>(error.code());
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
