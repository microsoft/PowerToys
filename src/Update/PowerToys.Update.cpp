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

#include <common/utils/elevation.h>
#include <common/utils/HttpClient.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

#include <common/SettingsAPI/settings_helpers.h>

#include <common/logger/logger.h>

#include <winrt/Windows.ApplicationModel.h>
#include <winrt/Windows.Storage.h>
#include <Msi.h>

#include "../runner/tray_icon.h"
#include "../runner/UpdateUtils.h"

using namespace cmdArg;

namespace fs = std::filesystem;

std::optional<fs::path> CopySelfToTempDir()
{
    std::error_code error;
    auto dst_path = fs::temp_directory_path() / "PowerToys.Update.exe";
    fs::copy_file(get_module_filename(), dst_path, fs::copy_options::overwrite_existing, error);
    if (error)
    {
        return std::nullopt;
    }

    return std::move(dst_path);
}

std::optional<fs::path> ObtainInstaller(bool& isUpToDate)
{
    using namespace updating;

    isUpToDate = false;

    auto state = UpdateState::read();

    const auto new_version_info = get_github_version_info_async().get();
    if (std::holds_alternative<version_up_to_date>(*new_version_info))
    {
        isUpToDate = true;
        Logger::error("Invoked with -update_now argument, but no update was available");
        return std::nullopt;
    }

    if (state.state == UpdateState::readyToDownload || state.state == UpdateState::errorDownloading)
    {
        if (!new_version_info)
        {
            Logger::error(L"Couldn't obtain github version info: {}", new_version_info.error());
            return std::nullopt;
        }

        // Cleanup old updates before downloading the latest
        updating::cleanup_updates();

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
    else if (state.state == UpdateState::upToDate)
    {
        isUpToDate = true;
        return std::nullopt;
    }

    Logger::error("Invoked with -update_now argument, but update state was invalid");
    return std::nullopt;
}

bool InstallNewVersionStage1(fs::path installer)
{
    if (auto copy_in_temp = CopySelfToTempDir())
    {
        // Detect if PT was running
        const auto pt_main_window = FindWindowW(pt_tray_icon_window_class, nullptr);

        if (pt_main_window != nullptr)
        {
            SendMessageW(pt_main_window, WM_CLOSE, 0, 0);
        }

        std::wstring arguments{ UPDATE_NOW_LAUNCH_STAGE2 };
        arguments += L" \"";
        arguments += installer.c_str();
        arguments += L"\"";
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

bool InstallNewVersionStage2(std::wstring installer_path)
{
    std::transform(begin(installer_path), end(installer_path), begin(installer_path), ::towlower);

    bool success = true;

    if (installer_path.ends_with(L".msi"))
    {
        success = MsiInstallProductW(installer_path.data(), nullptr) == ERROR_SUCCESS;
    }
    else
    {
        // If it's not .msi, then it's a wix bootstrapper
        SHELLEXECUTEINFOW sei{ sizeof(sei) };
        sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS | SEE_MASK_NO_CONSOLE };
        sei.lpFile = installer_path.c_str();
        sei.nShow = SW_SHOWNORMAL;
        std::wstring parameters = L"/passive /norestart";
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

    UpdateState::store([&](UpdateState& state) {
        state = {};
        state.githubUpdateLastCheckedDate.emplace(timeutil::now());
        state.state = UpdateState::upToDate;
    });

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
    logFilePath.append(LogSettings::updateLogPath);
    Logger::init(LogSettings::updateLoggerName, logFilePath.wstring(), PTSettingsHelper::get_log_settings_file_location());

    if (action == UPDATE_NOW_LAUNCH_STAGE1)
    {
        bool isUpToDate = false;
        auto installerPath = ObtainInstaller(isUpToDate);
        bool failed = !installerPath.has_value();
        failed = failed || !InstallNewVersionStage1(std::move(*installerPath));
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = isUpToDate ? UpdateState::upToDate : UpdateState::errorDownloading;
            });
        }
        return failed;
    }
    else if (action == UPDATE_NOW_LAUNCH_STAGE2)
    {
        using namespace std::string_view_literals;
        const bool failed = !InstallNewVersionStage2(args[2]);
        if (failed)
        {
            UpdateState::store([&](UpdateState& state) {
                state = {};
                state.githubUpdateLastCheckedDate.emplace(timeutil::now());
                state.state = UpdateState::errorDownloading;
            });
        }
        return failed;
    }

    return 0;
}
