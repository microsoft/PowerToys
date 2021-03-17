#include "pch.h"
#include "Generated Files/resource.h"

#include "RcResource.h"
#include <common/updating/dotnet_installation.h>
#include <common/updating/installer.h>
#include <common/updating/notifications.h>
#include <common/version/version.h>
#include <common/utils/appMutex.h>
#include <common/utils/elevation.h>
#include <common/utils/processApi.h>
#include <common/utils/resources.h>
#include <common/utils/window.h>
#include <common/utils/winapi_error.h>

#include <runner/action_runner_utils.h>

#include "progressbar_window.h"

auto Strings = create_notifications_strings();
static bool g_Silent = false;

#define STR_HELPER(x) #x
#define STR(x) STR_HELPER(x)

namespace // Strings in this namespace should not be localized
{
    const wchar_t APPLICATION_ID[] = L"PowerToysInstaller";
    const char EXE_LOG_FILENAME[] = "powertoys-bootstrapper-exe-" STR(VERSION_MAJOR) "." STR(VERSION_MINOR) "." STR(VERSION_REVISION) ".log";
    const char MSI_LOG_FILENAME[] = "powertoys-bootstrapper-msi-" STR(VERSION_MAJOR) "." STR(VERSION_MINOR) "." STR(VERSION_REVISION) ".log";
}

#undef STR
#undef STR_HELPER

namespace fs = std::filesystem;

std::optional<fs::path> ExtractEmbeddedInstaller(const fs::path extractPath)
{
    auto executableRes = RcResource::create(IDR_BIN_MSIINSTALLER, L"BIN");
    if (!executableRes)
    {
        return std::nullopt;
    }

    auto installerPath = extractPath / L"PowerToysBootstrappedInstaller-" PRODUCT_VERSION_STRING L".msi";
    return executableRes->saveAsFile(installerPath) ? std::make_optional(std::move(installerPath)) : std::nullopt;
}

void SetupLogger(fs::path directory, const spdlog::level::level_enum severity)
{
    std::shared_ptr<spdlog::logger> logger;
    auto nullLogger = spdlog::null_logger_mt("null");
    try
    {
        if (severity != spdlog::level::off)
        {
            logger = spdlog::basic_logger_mt("file", (directory / EXE_LOG_FILENAME).wstring());

            std::error_code _;
            const DWORD msiSev = severity == spdlog::level::debug ? INSTALLLOGMODE_VERBOSE : INSTALLLOGMODE_ERROR;
            const auto msiLogPath = directory / MSI_LOG_FILENAME;
            MsiEnableLogW(msiSev, msiLogPath.c_str(), INSTALLLOGATTRIBUTES_APPEND);
        }
        else
        {
            logger = nullLogger;
        }

        logger->set_pattern("[%L][%d-%m-%C-%T] %v");
        logger->set_level(severity);
        spdlog::set_default_logger(std::move(logger));
        spdlog::set_level(severity);
        spdlog::flush_every(std::chrono::seconds(5));
    }
    catch (...)
    {
        spdlog::set_default_logger(nullLogger);
    }
}

void ShowMessageBoxError(const wchar_t* message)
{
    if (!g_Silent)
    {
        MessageBoxW(nullptr,
            message,
            GET_RESOURCE_STRING(IDS_BOOTSTRAPPER_PROGRESS_TITLE).c_str(),
            MB_OK | MB_ICONERROR);
    }
}

void ShowMessageBoxError(const UINT messageId)
{
    ShowMessageBoxError(GET_RESOURCE_STRING(messageId).c_str());
}

int Bootstrapper(HINSTANCE hInstance)
{
    winrt::init_apartment();
    char* programFilesDir = nullptr;
    size_t size = 0;
    std::string defaultInstallDir;

    if (!_dupenv_s(&programFilesDir, &size, "PROGRAMFILES"))
    {
        defaultInstallDir += programFilesDir;
        defaultInstallDir += "\\PowerToys";
    }

    cxxopts::Options options{ "PowerToysBootstrapper" };

    // clang-format off
    options.add_options()
        ("h,help", "Show help")
        ("no_full_ui", "Use reduced UI for MSI")
        ("s,silent", "Suppress MSI UI and notifications")
        ("no_start_pt", "Do not launch PowerToys after the installation is complete")
        ("skip_dotnet_install", "Skip dotnet 3.X installation even if it's not detected")
        ("log_level", "Log level. Possible values: off|debug|error", cxxopts::value<std::string>()->default_value("off"))
        ("log_dir", "Log directory", cxxopts::value<std::string>()->default_value("."))
        ("install_dir", "Installation directory", cxxopts::value<std::string>()->default_value(defaultInstallDir))
        ("extract_msi", "Extract MSI to the working directory and exit. Use only if you must access MSI directly.");
    // clang-format on

    cxxopts::ParseResult cmdArgs;
    bool showHelp = false;
    try
    {
        cmdArgs = options.parse(__argc, const_cast<const char**>(__argv));
    }
    catch (...)
    {
        showHelp = true;
    }

    showHelp = showHelp || cmdArgs["help"].as<bool>();
    if (showHelp)
    {
        std::ostringstream helpMsg;
        helpMsg << options.help();
        MessageBoxA(nullptr, helpMsg.str().c_str(), "Help", MB_OK | MB_ICONINFORMATION);
        return 0;
    }

    g_Silent = cmdArgs["silent"].as<bool>();
    const bool noFullUI = cmdArgs["no_full_ui"].as<bool>();
    const bool skipDotnetInstall = cmdArgs["skip_dotnet_install"].as<bool>();
    const bool noStartPT = cmdArgs["no_start_pt"].as<bool>();
    const auto logLevel = cmdArgs["log_level"].as<std::string>();
    const auto logDirArg = cmdArgs["log_dir"].as<std::string>();
    const auto installDirArg = cmdArgs["install_dir"].as<std::string>();
    const bool extract_msi_only = cmdArgs["extract_msi"].as<bool>();

    std::wstring installFolderProp;
    if (!installDirArg.empty())
    {
        std::string installDir;
        if (installDirArg.find(' ') != std::string::npos)
        {
            installDir = "\"" + installDirArg + "\"";
        }
        else
        {
            installDir = installDirArg;
        }

        installFolderProp = std::wstring(installDir.length(), L' ');
        std::copy(installDir.begin(), installDir.end(), installFolderProp.begin());
        installFolderProp = L"INSTALLFOLDER=" + installFolderProp;
    }

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

    spdlog::level::level_enum severity = spdlog::level::off;
    if (logLevel == "debug")
    {
        severity = spdlog::level::debug;
    }
    else if (logLevel == "error")
    {
        severity = spdlog::level::err;
    }

    SetupLogger(logDir, severity);
    spdlog::debug("PowerToys Bootstrapper is launched\nnoFullUI: {}\nsilent: {}\nno_start_pt: {}\nskip_dotnet_install: {}\nlog_level: {}\ninstall_dir: {}\nextract_msi: {}\n", noFullUI, g_Silent, noStartPT, skipDotnetInstall, logLevel, installDirArg, extract_msi_only);
    
    const VersionHelper myVersion(VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION);

    // Do not support installing on Windows < 1903
    if (myVersion >= VersionHelper{0, 36, 0} && updating::is_old_windows_version())
    {
      ShowMessageBoxError(IDS_OLD_WINDOWS_ERROR);
      spdlog::error("PowerToys {} requires at least Windows 1903 to run.", myVersion.toString());
      return 1;
    }

    // If a user requested an MSI -> extract it and exit
    if (extract_msi_only)
    {
        if (const auto installerPath = ExtractEmbeddedInstaller(fs::current_path()))
        {
            spdlog::debug("MSI installer extracted to {}", installerPath->string());
        }
        else
        {
            spdlog::error("MSI installer couldn't be extracted");
        }
        return 0;
    }

    // Check if there's a newer version installed
    const auto installedVersion = updating::get_installed_powertoys_version();
    if (installedVersion && *installedVersion >= myVersion)
    {
        spdlog::error(L"Detected a newer version {} vs {}", (*installedVersion).toWstring(), myVersion.toWstring());
        ShowMessageBoxError(IDS_NEWER_VERSION_ERROR);
        return 0;
    }

    // Setup MSI UI visibility and restart as elevated if required
    if (!noFullUI)
    {
        MsiSetInternalUI(INSTALLUILEVEL_FULL, nullptr);
    }

    if (g_Silent)
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
                if (std::wstring_view{ argList[i] }.find(L' ') != std::wstring_view::npos)
                {
                    params += L'"';
                    params += argList[i];
                    params += L'"';
                }
                else
                {
                    params += argList[i];
                }

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

    // Try killing PowerToys and prevent future processes launch by acquiring app mutex
    for (auto& handle : getProcessHandlesByName(L"PowerToys.exe", PROCESS_TERMINATE))
    {
        TerminateProcess(handle.get(), 0);
    }

    auto powerToysMutex = createAppMutex(POWERTOYS_MSI_MUTEX_NAME);
    auto instanceMutex = createAppMutex(POWERTOYS_BOOTSTRAPPER_MUTEX_NAME);
    if (!instanceMutex)
    {
        spdlog::error("Couldn't acquire PowerToys global mutex. Setup couldn't terminate PowerToys.exe process");
        return 1;
    }

    spdlog::debug("Extracting embedded MSI installer");
    const auto installerPath = ExtractEmbeddedInstaller(fs::temp_directory_path());
    if (!installerPath)
    {
        ShowMessageBoxError(IDS_INSTALLER_EXTRACT_ERROR);
        spdlog::error("Couldn't install the MSI installer ({})", GetLastError());
        return 1;
    }

    auto removeExtractedInstaller = wil::scope_exit([&] {
        std::error_code _;
        fs::remove(*installerPath, _);
    });

    spdlog::debug("Acquiring existing MSI package path if exists");
    const auto package_path = updating::get_msi_package_path();
    if (!package_path.empty())
    {
        spdlog::debug(L"Existing MSI package path found: {}", package_path);
    }
    else
    {
        spdlog::debug("Existing MSI package path not found");
    }

    if (!package_path.empty() && !updating::uninstall_msi_version(package_path, Strings))
    {
        spdlog::error("Couldn't install the existing MSI package ({})", GetLastError());
        ShowMessageBoxError(IDS_UNINSTALL_PREVIOUS_VERSION_ERROR);
    }

    const bool installDotnet = !skipDotnetInstall;
    if (!g_Silent)
    {
        OpenProgressBarDialog(hInstance, 0, GET_RESOURCE_STRING(IDS_BOOTSTRAPPER_PROGRESS_TITLE).c_str(), GET_RESOURCE_STRING(IDS_DOWNLOADING_DOTNET).c_str());
    }

    try
    {
        if (installDotnet)
        {
            spdlog::debug("Detecting if dotnet is installed");
            const bool dotnetInstalled = updating::dotnet_is_installed();
            spdlog::debug("Dotnet is already installed: {}", dotnetInstalled);
            if (!dotnetInstalled)
            {
                bool installedSuccessfully = false;
                if (const auto dotnet_installer_path = updating::download_dotnet())
                {
                    // Dotnet installer has its own progress bar
                    CloseProgressBarDialog();
                    installedSuccessfully = updating::install_dotnet(*dotnet_installer_path, g_Silent);
                    if (!installedSuccessfully)
                    {
                        spdlog::error("Couldn't install dotnet");
                    }
                }
                else
                {
                    spdlog::error("Couldn't download dotnet");
                }

                if (!installedSuccessfully)
                {
                    ShowMessageBoxError(IDS_DOTNET_INSTALL_ERROR);
                }
            }
        }
    }
    catch (...)
    {
        spdlog::error("Unknown exception during dotnet installation");
        ShowMessageBoxError(IDS_DOTNET_INSTALL_ERROR);
    }

    // At this point, there's no reason to show progress bar window, since MSI installers have their own
    CloseProgressBarDialog();

    const std::wstring msiProps = installFolderProp;
    spdlog::debug("Launching MSI installation for new package {}", installerPath->string());
    const bool installationDone = MsiInstallProductW(installerPath->c_str(), msiProps.c_str()) == ERROR_SUCCESS;
    if (!installationDone)
    {
        spdlog::error("Couldn't install new MSI package ({})", GetLastError());
        return 1;
    }

    spdlog::debug("Installation completed");

    if (!noStartPT && !g_Silent)
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

int WINAPI WinMain(HINSTANCE hi, HINSTANCE, LPSTR, int)
{
    try
    {
        return Bootstrapper(hi);
    }
    catch (const std::exception& ex)
    {
        std::string messageA{ "Unhandled std exception encountered\n" };
        messageA.append(ex.what());

            spdlog::error(messageA.c_str());

        std::wstring messageW{};
        std::copy(messageA.begin(), messageA.end(), messageW.begin());
        ShowMessageBoxError(messageW.c_str());
    }
    catch (winrt::hresult_error const& ex)
    {
        std::wstring message{ L"Unhandled winrt exception encountered\n" };
        message.append(ex.message().c_str());

        spdlog::error(message.c_str());

        ShowMessageBoxError(message.c_str());
    }
    catch (...)
    {
        auto lastErrorMessage = get_last_error_message(GetLastError());
        std::wstring message{ L"Unknown exception encountered\n" };
        message.append(lastErrorMessage ? std::move(*lastErrorMessage) : L"");

        spdlog::error(message.c_str());

        ShowMessageBoxError(message.c_str());
    }
    return 0;
}
