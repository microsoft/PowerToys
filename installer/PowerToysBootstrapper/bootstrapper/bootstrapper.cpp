#include "pch.h"
#include "Generated Files/resource.h"

#include <common/common.h>
#include <common/notifications.h>
#include <common/RcResource.h>
#include <common/updating/updating.h>
#include <common/updating/dotnet_installation.h>
#include <common/version.h>
#include <common/appMutex.h>
#include <common/processApi.h>


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

enum class CmdArgs
{
    silent,
    noFullUI,
    noStartPT,
    skipDotnetInstall,
    showHelp
};

namespace
{
    const std::unordered_map<std::wstring_view, CmdArgs> knownArgs = {
        { L"--help", CmdArgs::showHelp },
        { L"--no_full_ui", CmdArgs::noFullUI },
        { L"--silent", CmdArgs::silent },
        { L"--no_start_pt", CmdArgs::noStartPT },
        { L"--skip_dotnet_install", CmdArgs::skipDotnetInstall }
    };
}

std::unordered_set<CmdArgs> parseCmdArgs(const int nCmdArgs, LPWSTR* argList)
{
    std::unordered_set<CmdArgs> result;
    for (size_t i = 1; i < nCmdArgs; ++i)
    {
        if (auto it = knownArgs.find(argList[i]); it != end(knownArgs))
        {
            result.emplace(it->second);
        }
    }
    return result;
}

int bootstrapper()
{
    using namespace localized_strings;
    winrt::init_apartment();

    int nCmdArgs = 0;
    LPWSTR* argList = CommandLineToArgvW(GetCommandLineW(), &nCmdArgs);
    const auto cmdArgs = parseCmdArgs(nCmdArgs, argList);
    std::wostringstream oss;
    if (cmdArgs.contains(CmdArgs::showHelp))
    {
        oss << "Supported arguments:\n\n";
        for (auto [arg, _] : knownArgs)
        {
            oss << arg << '\n';
        }
        MessageBoxW(nullptr, oss.str().c_str(), L"Help", MB_OK | MB_ICONINFORMATION);
        return 0;
    }
    if (!cmdArgs.contains(CmdArgs::noFullUI))
    {
        MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
    }
    if (cmdArgs.contains(CmdArgs::silent))
    {
        if (is_process_elevated())
        {
            MsiSetInternalUI(INSTALLUILEVEL_NONE, nullptr);
        }
        else
        {
            // MSI fails to run in silent mode due to a suppressed UAC w/o elevation, so we restart elevated
            std::wstring params;
            for (int i = 1; i < nCmdArgs; ++i)
            {
                params += argList[i];
                if (i != nCmdArgs - 1)
                {
                    params += L' ';
                }
            }
            const auto processHandle = run_elevated(argList[0], params.c_str());
            if (!processHandle)
            {
                return 1;
            }
            if (WaitForSingleObject(processHandle, 3600000) == WAIT_OBJECT_0)
            {
                DWORD exitCode = 0;
                GetExitCodeProcess(processHandle, &exitCode);
                return exitCode;
            }
            else
            {
                // Couldn't install using the completely silent mode in an hour, use basic UI.
                TerminateProcess(processHandle, 0);
                MsiSetInternalUI(INSTALLUILEVEL_BASIC, nullptr);
            }
        }
    }

    // Try killing PowerToys and prevent future processes launch
    for (auto& handle : getProcessHandlesByName(L"PowerToys.exe", PROCESS_TERMINATE))
    {
        TerminateProcess(handle.get(), 0);
    }
    auto powerToysMutex = createAppMutex(POWERTOYS_MSI_MUTEX_NAME);

    auto instanceMutex = createAppMutex(POWERTOYS_BOOTSTRAPPER_MUTEX_NAME);
    if (!instanceMutex)
    {
        return 1;
    }
    notifications::override_application_id(APPLICATION_ID);

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
            MsiInstallProductW(msi_path.c_str(), nullptr);
            return 0;
        }
    }

    std::mutex progressLock;
    notifications::progress_bar_params progressParams;
    progressParams.progress = 0.0f;
    progressParams.progress_title = EXTRACTING_INSTALLER;
    notifications::toast_params params{ TOAST_TAG, false, std::move(progressParams) };
    if (!cmdArgs.contains(CmdArgs::silent))
    {
        notifications::show_toast_with_activations({}, TOAST_TITLE, {}, {}, std::move(params));
    }

    auto processToasts = wil::scope_exit([&] {
        run_message_loop(true, 2);
    });

    if (!cmdArgs.contains(CmdArgs::silent))
    {
        // Worker thread to periodically increase progress and keep the progress toast from losing focus
        std::thread{ [&] {
            for (;; Sleep(3000))
            {
                std::scoped_lock lock{ progressLock };
                if (progressParams.progress == 1.f)
                {
                    break;
                }
                progressParams.progress = std::min(0.99f, progressParams.progress + 0.001f);
                notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
            }
        } }.detach();
    }

    auto updateProgressBar = [&](const float value, const wchar_t* title) {
        if (cmdArgs.contains(CmdArgs::silent))
        {
            return;
        }
        std::scoped_lock lock{ progressLock };
        progressParams.progress = value;
        progressParams.progress_title = title;
        notifications::update_progress_bar_toast(TOAST_TAG, progressParams);
    };

    const auto installerPath = extractEmbeddedInstaller();
    if (!installerPath)
    {
        if (!cmdArgs.contains(CmdArgs::silent))
        {
            notifications::show_toast(INSTALLER_EXTRACT_ERROR, TOAST_TITLE);
        }
        return 1;
    }
    auto removeExtractedInstaller = wil::scope_exit([&] {
        std::error_code _;
        fs::remove(*installerPath, _);
    });

    updateProgressBar(.25f, UNINSTALLING_PREVIOUS_VERSION);
    const auto package_path = updating::get_msi_package_path();
    if (!package_path.empty() && !updating::uninstall_msi_version(package_path) && !cmdArgs.contains(CmdArgs::silent))
    {
        notifications::show_toast(UNINSTALL_PREVIOUS_VERSION_ERROR, TOAST_TITLE);
    }
    const bool installDotnet = !cmdArgs.contains(CmdArgs::skipDotnetInstall);
    if (installDotnet)
    {
        updateProgressBar(.5f, INSTALLING_DOTNET);
    }

    try
    {
        if (installDotnet &&
            !updating::dotnet_is_installed() &&
            !updating::install_dotnet(cmdArgs.contains(CmdArgs::silent)) &&
            !cmdArgs.contains(CmdArgs::silent))
        {
            notifications::show_toast(DOTNET_INSTALL_ERROR, TOAST_TITLE);
        }
    }
    catch (...)
    {
        MessageBoxW(nullptr, L".NET Core installation", L"Unknown exception encountered!", MB_OK | MB_ICONERROR);
    }

    updateProgressBar(.75f, INSTALLING_NEW_VERSION);

    // Always skip dotnet install, because we should've installed it from here earlier
    std::wstring msiProps = L"SKIPDOTNETINSTALL=1 ";
    const bool installationDone = MsiInstallProductW(installerPath->c_str(), msiProps.c_str()) == ERROR_SUCCESS;
    updateProgressBar(1.f, installationDone ? NEW_VERSION_INSTALLATION_DONE : NEW_VERSION_INSTALLATION_ERROR);
    if (!installationDone)
    {
        return 1;
    }

    if (!cmdArgs.contains(CmdArgs::noStartPT) && !cmdArgs.contains(CmdArgs::silent))
    {
        auto newPTPath = updating::get_msi_package_installed_path();
        if (!newPTPath)
        {
            return 1;
        }
        *newPTPath += L"\\PowerToys.exe";
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NO_CONSOLE };
        sei.lpFile = newPTPath->c_str();
        sei.nShow = SW_SHOWNORMAL;
        ShellExecuteExW(&sei);
    }

    return 0;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    try
    {
        return bootstrapper();
    }
    catch (const std::exception& ex)
    {
        MessageBoxA(nullptr, ex.what(), "Unhandled stdexception encountered!", MB_OK | MB_ICONERROR);
    }
    catch (winrt::hresult_error const& ex)
    {
        winrt::hstring message = ex.message();
        MessageBoxW(nullptr, message.c_str(), L"Unhandled winrt exception encountered!", MB_OK | MB_ICONERROR);
    }
    catch (...)
    {
        auto lastErrorMessage = get_last_error_message(GetLastError());
        std::wstring message = lastErrorMessage ? std::move(*lastErrorMessage) : L"";
        MessageBoxW(nullptr, message.c_str(), L"Unknown exception encountered!", MB_OK | MB_ICONERROR);
    }
    return 0;
}
