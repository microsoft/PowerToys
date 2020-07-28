#define WIN32_LEAN_AND_MEAN
#include <Windows.h>
#include <shellapi.h>

#include <string_view>
#include <optional>
#include <fstream>

#include <common/common.h>
#include <common/notifications.h>
#include <common/RcResource.h>
#include <common/updating/updating.h>
#include <common/updating/dotnet_installation.h>
#include <common/version.h>
#include <common/appMutex.h>
#include <common/processApi.h>

#include <wil/resource.h>

#include <winrt/base.h>
#include "resource.h"
#include <Msi.h>

#include <runner/action_runner_utils.h>

extern "C" IMAGE_DOS_HEADER __ImageBase;

namespace
{
    const wchar_t APPLICATION_ID[] = L"PowerToysInstaller";
    const wchar_t TOAST_TAG[] = L"PowerToysInstallerProgress";
}

namespace localized_strings
{
    const wchar_t INSTALLER_EXTRACT_ERROR[] = L"Couldn't extract MSI installer!";
    const wchar_t TOAST_TITLE[] = L"PowerToys Installation";
    const wchar_t EXTRACTING_INSTALLER[] = L"Extracting PowerToys MSI...";
    const wchar_t UNINSTALLING_PREVIOUS_VERSION[] = L"Uninstalling previous PowerToys version...";
    const wchar_t UNINSTALL_PREVIOUS_VERSION_ERROR[] = L"Couldn't uninstall previous PowerToys version!";
    const wchar_t INSTALLING_DOTNET[] = L"Installing dotnet...";
    const wchar_t DOTNET_INSTALL_ERROR[] = L"Couldn't install dotnet!";
    const wchar_t INSTALLING_NEW_VERSION[] = L"Installing new PowerToys version...";
    const wchar_t NEW_VERSION_INSTALLATION_DONE[] = L"PowerToys installation complete!";
    const wchar_t NEW_VERSION_INSTALLATION_ERROR[] = L"Couldn't install new PowerToys version.";
}

namespace fs = std::filesystem;

std::optional<fs::path> extractEmbeddedInstaller()
{
    auto executableRes = RcResource::create(IDR_BIN_MSIINSTALLER, L"BIN");
    if (!executableRes)
    {
        return std::nullopt;
    }
    auto installerPath = fs::temp_directory_path() / L"PowerToysBootstrappedInstaller-" PRODUCT_VERSION_STRING L".msi";
    return executableRes->saveAsFile(installerPath) ? std::make_optional(std::move(installerPath)) : std::nullopt;
}

std::optional<fs::path> extractIcon()
{
    auto iconRes = RcResource::create(IDR_BIN_ICON, L"BIN");
    if (!iconRes)
    {
        return std::nullopt;
    }
    auto icoPath = fs::temp_directory_path() / L"PowerToysBootstrappedInstaller.ico";
    return iconRes->saveAsFile(icoPath) ? std::make_optional(std::move(icoPath)) : std::nullopt;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    using namespace localized_strings;
    winrt::init_apartment();

    // Try killing PowerToys and prevent future processes launch
    for (auto& handle : getProcessHandlesByName(L"PowerToys.exe", PROCESS_TERMINATE))
    {
        TerminateProcess(handle.get(), 0);
    }
    auto powerToysMutex = createAppMutex(POWERTOYS_MSI_MUTEX_NAME);

    int n_cmd_args = 0;
    LPWSTR* cmd_arg_list = CommandLineToArgvW(GetCommandLineW(), &n_cmd_args);
    const bool silent = n_cmd_args > 1 && std::wstring_view{ L"-silent" } == cmd_arg_list[1];

    auto instanceMutex = createAppMutex(POWERTOYS_BOOTSTRAPPER_MUTEX_NAME);
    if (!instanceMutex)
    {
        return 1;
    }
    notifications::set_application_id(APPLICATION_ID);

    fs::path iconPath{ L"C:\\" };
    if (auto extractedIcon = extractIcon())
    {
        iconPath = std::move(*extractedIcon);
    }

    notifications::register_application_id(TOAST_TITLE, iconPath.c_str());

    auto removeShortcut = wil::scope_exit([&] {
        notifications::unregister_application_id();
    });

    // Check if there's a newer version installed, and launch its installer if so.
    const VersionHelper myVersion(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);
    if (const auto installedVersion = updating::get_installed_powertoys_version(); installedVersion && *installedVersion >= myVersion)
    {
        auto msi_path = updating::get_msi_package_path();
        if (!msi_path.empty())
        {
            MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
            MsiInstallProductW(msi_path.c_str(), nullptr);
            return 0;
        }
    }

    std::mutex progressLock;
    notifications::progress_bar_params progressParams;
    progressParams.progress = 0.0f;
    progressParams.progress_title = EXTRACTING_INSTALLER;
    notifications::toast_params params{ TOAST_TAG, false, std::move(progressParams) };
    notifications::show_toast_with_activations({}, TOAST_TITLE, {}, {}, std::move(params));

    auto processToasts = wil::scope_exit([&] {
        run_message_loop(true, 2);
    });

    // Worker thread to periodically increase progress and keep the progress toast from losing focus
    std::thread{ [&] {
        for (;; Sleep(3000))
        {
            std::scoped_lock lock{ progressLock };
            if (progressParams.progress == 1.f)
            {
                break;
            }
            progressParams.progress = min(0.99f, progressParams.progress + 0.001f);
            notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
        }
    } }.detach();

    auto updateProgressBar = [&](const float value, const wchar_t* title) {
        std::scoped_lock lock{ progressLock };
        progressParams.progress = value;
        progressParams.progress_title = title;
        notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    };

    const auto installerPath = extractEmbeddedInstaller();
    if (!installerPath)
    {
        notifications::show_toast(INSTALLER_EXTRACT_ERROR, TOAST_TITLE);
        return 1;
    }
    auto removeExtractedInstaller = wil::scope_exit([&] {
        std::error_code _;
        fs::remove(*installerPath, _);
    });

    updateProgressBar(.25f, UNINSTALLING_PREVIOUS_VERSION);
    const auto package_path = updating::get_msi_package_path();
    if (!package_path.empty() && !updating::uninstall_msi_version(package_path))
    {
        notifications::show_toast(UNINSTALL_PREVIOUS_VERSION_ERROR, TOAST_TITLE);
    }

    updateProgressBar(.5f, INSTALLING_DOTNET);
    if (!updating::dotnet_is_installed() && !updating::install_dotnet())
    {
        notifications::show_toast(DOTNET_INSTALL_ERROR, TOAST_TITLE);
    }

    updateProgressBar(.75f, INSTALLING_NEW_VERSION);
    if (!silent)
    {
        MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
    }
    const bool installationDone = MsiInstallProductW(installerPath->c_str(), nullptr) == ERROR_SUCCESS;

    updateProgressBar(1.f, installationDone ? NEW_VERSION_INSTALLATION_DONE : NEW_VERSION_INSTALLATION_ERROR);

    if (!installationDone)
    {
        return 1;
    }
    auto newPTPath = updating::get_msi_package_installed_path();
    if (!newPTPath)
    {
        return 1;
    }
    // Do not launch PowerToys, if we're launched from the action_runner
    if (!silent)
    {
        *newPTPath += L"\\PowerToys.exe";
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NO_CONSOLE };
        sei.lpFile = newPTPath->c_str();
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = UPDATE_REPORT_SUCCESS;
        ShellExecuteExW(&sei);
    }

    return 0;
}
