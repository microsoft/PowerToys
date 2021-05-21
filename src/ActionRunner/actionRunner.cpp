// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#define WIN32_LEAN_AND_MEAN
#include "Generated Files/resource.h"

#include <Windows.h>
#include <shellapi.h>

#include <filesystem>
#include <string_view>

#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/updating/installer.h>
#include <common/updating/http_client.h>
#include <common/updating/dotnet_installation.h>

#include <common/utils/elevation.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

#include <common/SettingsAPI/settings_helpers.h>

#include <common/logger/logger.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>
#include <Msi.h>

#include "../runner/tray_icon.h"
#include "../runner/action_runner_utils.h"

auto Strings = create_notifications_strings();

using namespace cmdArg;

int UninstallMsiAction()
{
    const auto package_path = updating::get_msi_package_path();
    if (package_path.empty())
    {
        return 0;
    }

    if (!updating::uninstall_msi_version(package_path, Strings))
    {
        return -1;
    }

    // Launch PowerToys again, since it's been terminated by the MSI uninstaller
    std::wstring runner_path{ winrt::Windows::ApplicationModel::Package::Current().InstalledLocation().Path() };
    runner_path += L"\\PowerToys.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
    sei.lpFile = runner_path.c_str();
    sei.nShow = SW_SHOWNORMAL;
    ShellExecuteExW(&sei);

    return 0;
}

namespace fs = std::filesystem;

std::optional<fs::path> CopySelfToTempDir()
{
    std::error_code error;
    auto dst_path = fs::temp_directory_path() / "PowerToys.ActionRunner.exe";
    fs::copy_file(get_module_filename(), dst_path, fs::copy_options::overwrite_existing, error);
    if (error)
    {
        return std::nullopt;
    }

    return std::move(dst_path);
}

std::optional<fs::path> ObtainInstallerPath()
{
    using namespace updating;

    auto state = UpdateState::read();
    if (state.state == UpdateState::readyToDownload || state.state == UpdateState::errorDownloading)
    {
        const auto new_version_info = get_github_version_info_async(Strings).get();
        if (!new_version_info)
        {
            Logger::error(L"Couldn't obtain github version info: {}", new_version_info.error());
            return std::nullopt;
        }

        if (!std::holds_alternative<new_version_download_info>(*new_version_info))
        {
            Logger::error("Invoked with -update_now argument, but no update was available");
            return std::nullopt;
        }

        auto downloaded_installer = download_new_version(std::get<new_version_download_info>(*new_version_info)).get();
        if (!downloaded_installer)
        {
            Logger::error("Couldn't download new installer");
        }

        return downloaded_installer;
    }
    else if (state.state == UpdateState::readyToInstall)
    {
        fs::path installer{ get_pending_updates_path() / state.downloadedInstallerFilename };
        if (fs::is_regular_file(installer))
        {
            return std::move(installer);
        }
        else
        {
            Logger::error(L"Couldn't find a downloaded installer {}", installer.native());
            return std::nullopt;
        }
    }
    else
    {
        Logger::error("Invoked with -update_now argument, but update state was invalid");
        return std::nullopt;
    }
}

bool InstallNewVersionStage1()
{
    const auto installer = ObtainInstallerPath();
    if (!installer)
    {
        return false;
    }

    if (auto copy_in_temp = CopySelfToTempDir())
    {
        // Detect if PT was running
        const auto pt_main_window = FindWindowW(pt_tray_icon_window_class, nullptr);
        const bool launch_powertoys = pt_main_window != nullptr;
        if (pt_main_window != nullptr)
        {
            SendMessageW(pt_main_window, WM_CLOSE, 0, 0);
        }

        std::wstring arguments{ UPDATE_NOW_LAUNCH_STAGE2 };
        arguments += L" \"";
        arguments += installer->c_str();
        arguments += L"\" \"";
        arguments += get_module_folderpath();
        arguments += L"\" ";
        arguments += launch_powertoys ? UPDATE_STAGE2_RESTART_PT : UPDATE_STAGE2_DONT_START_PT;
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
        sei.lpFile = copy_in_temp->c_str();
        sei.nShow = SW_SHOWNORMAL;

        sei.lpParameters = arguments.c_str();
        return ShellExecuteExW(&sei) == TRUE;
    }
    else
    {
        return false;
    }
}

bool InstallNewVersionStage2(std::wstring installer_path, std::wstring_view install_path, bool launch_powertoys)
{
    std::transform(begin(installer_path), end(installer_path), begin(installer_path), ::towlower);

    bool success = true;

    if (installer_path.ends_with(L".msi"))
    {
        success = MsiInstallProductW(installer_path.data(), nullptr) == ERROR_SUCCESS;
    }
    else
    {
        // If it's not .msi, then it's our .exe installer
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
        sei.lpFile = installer_path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        std::wstring parameters = L"--no_full_ui";
        if (launch_powertoys)
        {
            // .exe installer launches the main app by default
            launch_powertoys = false;
        }
        else
        {
            parameters += L"--no_start_pt";
        }

        sei.lpParameters = parameters.c_str();

        success = ShellExecuteExW(&sei) == TRUE;

        // Wait for the install completion
        if (success)
        {
            WaitForSingleObject(sei.hProcess, INFINITE);
            DWORD exitCode = 0;
            GetExitCodeProcess(sei.hProcess, &exitCode);
            success = exitCode == 0;
            CloseHandle(sei.hProcess);
        }
    }

    if (!success)
    {
        return false;
    }

    std::error_code _;
    fs::remove(installer_path, _);

    UpdateState::store([&](UpdateState& state) {
        state = {};
        state.githubUpdateLastCheckedDate.emplace(timeutil::now());
        state.state = UpdateState::upToDate;
    });

    if (launch_powertoys)
    {
        std::wstring new_pt_path{ install_path };
        new_pt_path += L"\\PowerToys.exe";
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC };
        sei.lpFile = new_pt_path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        sei.lpParameters = UPDATE_REPORT_SUCCESS;
        return ShellExecuteExW(&sei) == TRUE;
    }

    return true;
}

int WINAPI WinMain(HINSTANCE, HINSTANCE, LPSTR, int)
{
    int nArgs = 0;
    LPWSTR* args = CommandLineToArgvW(GetCommandLineW(), &nArgs);
    if (!args || nArgs < 2)
    {
        return 1;
    }

    std::wstring_view action{ args[1] };

    std::filesystem::path logFilePath(PTSettingsHelper::get_root_save_folder_location());
    logFilePath.append(LogSettings::actionRunnerLogPath);
    Logger::init(LogSettings::actionRunnerLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    if (action == RUN_NONELEVATED)
    {
        int nextArg = 2;

        std::wstring_view target;
        std::wstring_view pidFile;
        std::wstring params;

        while (nextArg < nArgs)
        {
            if (std::wstring_view(args[nextArg]) == L"-target" && nextArg + 1 < nArgs)
            {
                target = args[nextArg + 1];
                nextArg += 2;
            }
            else if (std::wstring_view(args[nextArg]) == L"-pidFile" && nextArg + 1 < nArgs)
            {
                pidFile = args[nextArg + 1];
                nextArg += 2;
            }
            else
            {
                params += args[nextArg];
                params += L' ';
                nextArg++;
            }
        }

        HANDLE hMapFile = NULL;
        PDWORD pidBuffer = NULL;

        if (!pidFile.empty())
        {
            hMapFile = OpenFileMappingW(FILE_MAP_WRITE, FALSE, pidFile.data());
            if (hMapFile)
            {
                pidBuffer = reinterpret_cast<PDWORD>(MapViewOfFile(hMapFile, FILE_MAP_ALL_ACCESS, 0, 0, sizeof(DWORD)));
                if (pidBuffer)
                {
                    *pidBuffer = 0;
                }
            }
        }

        run_same_elevation(target.data(), params, pidBuffer);

        if (!pidFile.empty())
        {
            if (pidBuffer)
            {
                FlushViewOfFile(pidBuffer, sizeof(DWORD));
                UnmapViewOfFile(pidBuffer);
            }

            if (hMapFile)
            {
                FlushFileBuffers(hMapFile);
                CloseHandle(hMapFile);
            }
        }
    }
    else if (action == UNINSTALL_MSI)
    {
        return UninstallMsiAction();
    }
    else if (action == UPDATE_NOW_LAUNCH_STAGE1)
    {
        const bool failed = !InstallNewVersionStage1();
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state.downloadedInstallerFilename = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = UpdateState::errorDownloading;
            });
        }
        return failed;
    }
    else if (action == UPDATE_NOW_LAUNCH_STAGE2)
    {
        using namespace std::string_view_literals;
        const bool failed = !InstallNewVersionStage2(args[2], args[3], args[4] == std::wstring_view{ UPDATE_STAGE2_RESTART_PT });
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state.downloadedInstallerFilename = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = UpdateState::errorDownloading;
            });
        }
        return failed;
    }

    return 0;
}
