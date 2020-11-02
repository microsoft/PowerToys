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

auto Strings = updating::notifications::strings::create();

#define STR_HELPER(x) #x
#define STR(x) STR_HELPER(x)
namespace // Strings in this namespace should not be localized
{
    const wchar_t APPLICATION_ID[] = L"PowerToysInstaller";
    const wchar_t INSTALLATION_TOAST_TITLE[] = L"PowerToys Installation";
    const wchar_t TOAST_TAG[] = L"PowerToysInstallerProgress";
    const char LOG_FILENAME[] = "powertoys-bootstrapper-" STR(VERSION_MAJOR) "." STR(VERSION_MINOR) "." STR(VERSION_REVISION) ".log";
    const char MSI_LOG_FILENAME[] = "powertoys-bootstrapper-msi-" STR(VERSION_MAJOR) "." STR(VERSION_MINOR) "." STR(VERSION_REVISION) ".log";

}
#undef STR
#undef STR_HELPER

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

void setup_log(fs::path directory, const spdlog::level::level_enum severity)
{
    try
    {
        std::shared_ptr<spdlog::logger> logger;
        if (severity != spdlog::level::off)
        {
            logger = spdlog::basic_logger_mt("file", (directory / LOG_FILENAME).string());

            std::error_code _;
            const DWORD msiSev = severity == spdlog::level::debug ? INSTALLLOGMODE_VERBOSE : INSTALLLOGMODE_ERROR;
            const auto msiLogPath = directory / MSI_LOG_FILENAME;
            MsiEnableLogW(msiSev, msiLogPath.c_str(), INSTALLLOGATTRIBUTES_APPEND);
        }
        else
        {
            logger = spdlog::null_logger_mt("null");
        }
        logger->set_pattern("[%L][%d-%m-%C-%T] %v");
        logger->set_level(severity);
        spdlog::set_default_logger(std::move(logger));
        spdlog::set_level(severity);
        spdlog::flush_every(std::chrono::seconds(5));
    }
    catch (...)
    {
    }
}

int bootstrapper()
{
    winrt::init_apartment();
    cxxopts::Options options{ "PowerToysBootstrapper" };
    // clang-format off
    options.add_options()
        ("h,help", "Show help")
        ("no_full_ui", "Use reduced UI for MSI")
        ("s,silent", "Suppress MSI UI and notifications")
        ("no_start_pt", "Do not launch PowerToys after the installation is complete")
        ("skip_dotnet_install", "Skip dotnet 3.X installation even if it's not detected")
        ("log_level", "Log level. Possible values: off|debug|error", cxxopts::value<std::string>()->default_value("off"))
        ("log_dir", "Log directory.", cxxopts::value<std::string>()->default_value("."));
    // clang-format on
    cxxopts::ParseResult cmdArgs;
    bool showHelp = false;
    try
    {
        cmdArgs = options.parse(__argc, const_cast<const char**>(__argv));
    }
    catch (cxxopts::option_has_no_value_exception&)
    {
        showHelp = true;
    }
    catch (cxxopts::option_not_exists_exception&)
    {
        showHelp = true;
    }
    catch (cxxopts::option_not_present_exception&)
    {
        showHelp = true;
    }
    catch (cxxopts::option_not_has_argument_exception&)
    {
        showHelp = true;
    }
    catch (cxxopts::option_required_exception&)
    {
        showHelp = true;
    }
    catch (cxxopts::option_requires_argument_exception&)
    {
        showHelp = true;
    }
    catch (...)
    {
    }

    showHelp = showHelp || cmdArgs["help"].as<bool>();
    if (showHelp)
    {
        std::ostringstream helpMsg;
        helpMsg << options.help();
        MessageBoxA(nullptr, helpMsg.str().c_str(), "Help", MB_OK | MB_ICONINFORMATION);
        return 0;
    }
    const bool noFullUI = cmdArgs["no_full_ui"].as<bool>();
    const bool silent = cmdArgs["silent"].as<bool>();
    const bool skipDotnetInstall = cmdArgs["skip_dotnet_install"].as<bool>();
    const bool noStartPT = cmdArgs["no_start_pt"].as<bool>();
    const auto logLevel = cmdArgs["log_level"].as<std::string>();
    const auto logDirArg = cmdArgs["log_dir"].as<std::string>();
    spdlog::level::level_enum severity = spdlog::level::off;

    fs::path logDir = ".";
    try
    {
        fs::path logDirArgPath = logDirArg;
        if (fs::exists(logDirArgPath) && fs::is_directory(logDirArgPath))
        {
            logDir = logDirArgPath;
        }
    }
    catch (...)
    {
    }

    if (logLevel == "debug")
    {
        severity = spdlog::level::debug;
    }
    else if (logLevel == "error")
    {
        severity = spdlog::level::err;
    }
    setup_log(logDir, severity);
    spdlog::debug("PowerToys Bootstrapper is launched!\nnoFullUI: {}\nsilent: {}\nno_start_pt: {}\nskip_dotnet_install: {}\nlog_level: {}", noFullUI, silent, noStartPT, skipDotnetInstall, logLevel);

    if (!noFullUI)
    {
        MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
    }
    if (silent)
    {
        if (is_process_elevated())
        {
            MsiSetInternalUI(INSTALLUILEVEL_NONE, nullptr);
        }
        else
        {
            spdlog::debug("MSI doesn't support silent mode without elevation => restarting elevated");
            // MSI fails to run in silent mode due to a suppressed UAC w/o elevation,
            // so we restart ourselves elevated with the same args
            std::wstring params;
            int nCmdArgs = 0;
            LPWSTR* argList = CommandLineToArgvW(GetCommandLineW(), &nCmdArgs);
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
                spdlog::error("Couldn't restart elevated to enable silent mode! ({})", GetLastError());
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
                spdlog::error("Elevated setup process timed out after 60m => using basic MSI UI ({})", GetLastError());
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
        spdlog::error("Couldn't acquire PowerToys global mutex. That means setup couldn't kill PowerToys.exe process");
        return 1;
    }
    notifications::override_application_id(APPLICATION_ID);
    spdlog::debug("Extracting icon for toast notifications");
    fs::path iconPath{ L"C:\\" };
    if (auto extractedIcon = extractIcon())
    {
        iconPath = std::move(*extractedIcon);
    }
    spdlog::debug("Registering app id for toast notifications");
    notifications::register_application_id(INSTALLATION_TOAST_TITLE, iconPath.c_str());

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
            spdlog::error(L"Detected a newer {} version => launching its installer", installedVersion->toWstring());
            MsiInstallProductW(msi_path.c_str(), nullptr);
            return 0;
        }
    }

    std::mutex progressLock;
    notifications::progress_bar_params progressParams;
    progressParams.progress = 0.0f;
    progressParams.progress_title = GET_RESOURCE_STRING(IDS_EXTRACTING_INSTALLER);
    notifications::toast_params params{ TOAST_TAG, false, std::move(progressParams) };
    if (!silent)
    {
        spdlog::debug("Launching progress toast notification");
        notifications::show_toast_with_activations({}, INSTALLATION_TOAST_TITLE, {}, {}, std::move(params));
    }

    auto processToasts = wil::scope_exit([&] {
        spdlog::debug("Processing HWND messages for 2s so toast have time to show up");
        run_message_loop(true, 2);
    });

    if (!silent)
    {
        // Worker thread to periodically increase progress and keep the progress toast from losing focus
        std::thread{ [&] {
            spdlog::debug("Started worker thread for progress bar update");
            for (;; Sleep(3000))
            {
                std::scoped_lock lock{ progressLock };
                if (progressParams.progress == 1.f)
                {
                    break;
                }
                progressParams.progress = std::min(0.99f, progressParams.progress + 0.001f);
                notifications::update_toast_progress_bar(TOAST_TAG, progressParams);
            }
        } }.detach();
    }

    auto updateProgressBar = [&](const float value, const wchar_t* title) {
        if (silent)
        {
            return;
        }
        std::scoped_lock lock{ progressLock };
        progressParams.progress = value;
        progressParams.progress_title = title;
        notifications::update_toast_progress_bar(TOAST_TAG, progressParams);
    };

    spdlog::debug("Extracting embedded MSI installer");
    const auto installerPath = extractEmbeddedInstaller();
    if (!installerPath)
    {
        if (!silent)
        {
            notifications::show_toast(GET_RESOURCE_STRING(IDS_INSTALLER_EXTRACT_ERROR), INSTALLATION_TOAST_TITLE);
        }
        spdlog::error("Couldn't install the MSI installer ({})", GetLastError());
        return 1;
    }
    auto removeExtractedInstaller = wil::scope_exit([&] {
        std::error_code _;
        fs::remove(*installerPath, _);
    });

    updateProgressBar(.25f, GET_RESOURCE_STRING(IDS_UNINSTALLING_PREVIOUS_VERSION).c_str());
    spdlog::debug("Acquiring existing MSI package path");
    const auto package_path = updating::get_msi_package_path();
    if (!package_path.empty())
    {
        spdlog::debug(L"Existing MSI package path: {}", package_path);
    }
    else
    {
        spdlog::debug("Existing MSI package path not found");
    }
    if (!package_path.empty() && !updating::uninstall_msi_version(package_path, Strings) && !silent)
    {
        spdlog::error("Couldn't install the existing MSI package ({})", GetLastError());
        notifications::show_toast(GET_RESOURCE_STRING(IDS_UNINSTALL_PREVIOUS_VERSION_ERROR), INSTALLATION_TOAST_TITLE);
    }
    const bool installDotnet = !skipDotnetInstall;
    if (installDotnet)
    {
        updateProgressBar(.5f, GET_RESOURCE_STRING(IDS_INSTALLING_DOTNET).c_str());
    }

    try
    {
        if (installDotnet)
        {
            spdlog::debug("Detecting if dotnet is installed");
            const bool dotnetInstalled = updating::dotnet_is_installed();
            spdlog::debug("Dotnet is installed: {}", dotnetInstalled);
            if (!dotnetInstalled &&
                !updating::install_dotnet(silent) &&
                !silent)
            {
                notifications::show_toast(GET_RESOURCE_STRING(IDS_DOTNET_INSTALL_ERROR), INSTALLATION_TOAST_TITLE);
            }
        }
    }
    catch (...)
    {
        spdlog::error("Unknown exception during dotnet installation");
        MessageBoxW(nullptr, L".NET Core installation", L"Unknown exception encountered!", MB_OK | MB_ICONERROR);
    }

    updateProgressBar(.75f, GET_RESOURCE_STRING(IDS_INSTALLING_NEW_VERSION).c_str());

    // Always skip dotnet install, because we should've installed it from here earlier
    std::wstring msiProps = L"SKIPDOTNETINSTALL=1 ";
    spdlog::debug("Launching MSI installation for new package {}", installerPath->string());
    const bool installationDone = MsiInstallProductW(installerPath->c_str(), msiProps.c_str()) == ERROR_SUCCESS;
    updateProgressBar(1.f,
                      installationDone ? GET_RESOURCE_STRING(IDS_NEW_VERSION_INSTALLATION_DONE).c_str() : GET_RESOURCE_STRING(IDS_NEW_VERSION_INSTALLATION_ERROR).c_str());
    if (!installationDone)
    {
        spdlog::error("Couldn't install new MSI package ({})", GetLastError());
        return 1;
    }
    spdlog::debug("Installation completed");

    if (!noStartPT && !silent)
    {
        spdlog::debug("Starting the newly installed PowerToys.exe");
        auto newPTPath = updating::get_msi_package_installed_path();
        if (!newPTPath)
        {
            spdlog::error("Couldn't determine new MSI package install location ({})", GetLastError());
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
        MessageBoxA(nullptr, ex.what(), "Unhandled std exception encountered!", MB_OK | MB_ICONERROR);
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
