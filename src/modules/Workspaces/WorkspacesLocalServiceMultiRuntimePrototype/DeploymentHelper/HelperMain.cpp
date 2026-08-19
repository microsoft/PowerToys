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

    void write_evidence(std::wstring_view operation, bool identityPresent)
    {
        std::wstringstream evidence;
        evidence << L"operation=" << operation << L"\r\n";
        evidence << L"processId=" << GetCurrentProcessId() << L"\r\n";
        evidence << L"tokenUserSid=" << ptlsmr::current_token_user_sid() << L"\r\n";
        evidence << L"packageIdentityPresent=" << (identityPresent ? L"true" : L"false") << L"\r\n";
        evidence << L"executablePath=" << module_path().wstring() << L"\r\n";
        ptlsmr::write_utf8_file_atomic(
            ptlsmr::program_data_root() / L"deployment-helper-evidence.txt",
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
}

int wmain()
{
    try
    {
        if (ptlsmr::current_token_user_sid() != L"S-1-5-18")
        {
            throw ptlsmr::win32_error("deployment helper LocalSystem policy", ERROR_ACCESS_DENIED);
        }
        const bool identityPresent = package_identity_present();
        write_evidence(L"starting", identityPresent);
        if (identityPresent)
        {
            throw ptlsmr::win32_error(
                "deployment helper must be an unpackaged process",
                ERROR_INVALID_STATE);
        }
        const auto executable = module_path();
        const auto expectedExecutable =
            ptlsmr::program_data_root() /
            L"DeploymentHelper" /
            L"5.0.0.0" /
            ptlsmr::DeploymentHelperExe;
        if (_wcsicmp(executable.filename().c_str(), L"PtPuvrDeploymentHelper.exe") != 0 ||
            !std::filesystem::equivalent(executable, expectedExecutable))
        {
            throw ptlsmr::win32_error(
                "deployment helper protected-cache policy",
                ERROR_ACCESS_DENIED);
        }
        const auto arguments = ptlsmr::command_line_arguments();
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
            write_evidence(L"stage", false);
            return ERROR_SUCCESS;
        }
        if (std::find(arguments.begin(), arguments.end(), L"--remove") != arguments.end())
        {
            remove_package(runtimeTrack);
            write_evidence(L"remove", false);
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
