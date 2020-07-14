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
#include <span>
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
    std::fstream installerFile{ installerPath, std::ios_base::binary | std::ios_base::out | std::ios_base::trunc };
    if (!installerFile.is_open())
    {
        return std::nullopt;
    }
    installerFile.write(reinterpret_cast<const char*>(executableRes->_memory.data()), executableRes->_memory.size());
    return std::move(installerPath);
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
    notifications::initialize_application_id(APPLICATION_ID);
    notifications::register_activatable_shortcut(TOAST_TITLE);

    auto removeShortcut = wil::scope_exit([&] {
        notifications::remove_activatable_shortcut(TOAST_TITLE);
    });

    // Hack to allow for shortcut to be picked by Windows
    // TODO(yuyoyuppe): use registry instead of shortcut to avoid this.
    Sleep(3000);

    notifications::progress_bar_params progressParams;
    progressParams.progress = 0.0f;
    progressParams.progress_title = EXTRACTING_INSTALLER;
    notifications::toast_params params{ TOAST_TAG, false, std::move(progressParams) };
    notifications::show_toast_with_activations({}, TOAST_TITLE, {}, {}, std::move(params));

    auto processToasts = wil::scope_exit([&] {
        run_message_loop(true, 2);
    });

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

    progressParams.progress = 0.25f;
    progressParams.progress_title = UNINSTALLING_PREVIOUS_VERSION;
    notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    const auto package_path = updating::get_msi_package_path();
    if (!package_path.empty() && !updating::uninstall_msi_version(package_path))
    {
        notifications::show_toast(UNINSTALL_PREVIOUS_VERSION_ERROR, TOAST_TITLE);
    }

    progressParams.progress = 0.5f;
    progressParams.progress_title = INSTALLING_DOTNET;
    notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    if (!updating::dotnet_is_installed() && !updating::install_dotnet())
    {
        notifications::show_toast(DOTNET_INSTALL_ERROR, TOAST_TITLE);
    }

    progressParams.progress = 0.75f;
    progressParams.progress_title = INSTALLING_NEW_VERSION;
    notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    if (!silent)
    {
        MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
    }
    const bool installationDone = MsiInstallProductW(installerPath->c_str(), nullptr) == ERROR_SUCCESS;

    progressParams.progress = 1.f;
    progressParams.progress_title = installationDone ? NEW_VERSION_INSTALLATION_DONE : NEW_VERSION_INSTALLATION_ERROR;
    notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    
    return 0;
}
