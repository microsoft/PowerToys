#include "pch.h"

#include "Generated Files/resource.h"

#include "ActionRunnerUtils.h"
#include "general_settings.h"
#include "UpdateUtils.h"

#include <common/logger/logger.h>
#include <common/updating/installer.h>
#include <common/updating/http_client.h>
#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

auto Strings = create_notifications_strings();

namespace
{
    constexpr int64_t UPDATE_CHECK_INTERVAL_MINUTES = 60 * 24;
    constexpr int64_t UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES = 60 * 2;
}

SHELLEXECUTEINFOW LaunchPowerToysUpdate(const wchar_t* cmdline)
{
    std::wstring action_runner_path;
    action_runner_path = get_module_folderpath();

    action_runner_path += L"\\PowerToys.Update.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS };
    sei.lpFile = action_runner_path.c_str();
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = cmdline;
    ShellExecuteExW(&sei);
    return sei;
}

using namespace updating;

bool CouldBeCostlyConnection()
{
    using namespace winrt::Windows::Networking::Connectivity;
    ConnectionProfile internetConnectionProfile = NetworkInformation::GetInternetConnectionProfile();
    return internetConnectionProfile && internetConnectionProfile.IsWwanConnectionProfile();
}

void ProcessNewVersionInfo(const github_version_info& version_info,
                              UpdateState& state,
                              const bool download_update,
                              const bool show_notifications)
{
    state.githubUpdateLastCheckedDate.emplace(timeutil::now());
    if (std::holds_alternative<version_up_to_date>(version_info))
    {
        state.state = UpdateState::upToDate;
        state.releasePageUrl = {};
        state.downloadedInstallerFilename = {};
        Logger::trace(L"Version is up to date");
        return;
    }
    const auto new_version_info = std::get<new_version_download_info>(version_info);
    state.releasePageUrl = new_version_info.release_page_uri.ToString().c_str();
    Logger::trace(L"Discovered new version {}", new_version_info.version.toWstring());

    const bool already_downloaded = state.state == UpdateState::readyToInstall && state.downloadedInstallerFilename == new_version_info.installer_filename;
    if (already_downloaded)
    {
        Logger::trace(L"New version is already downloaded");
        return;
    }

    if (download_update)
    {
        Logger::trace(L"Downloading installer for a new version");
        if (download_new_version(new_version_info).get())
        {
            state.state = UpdateState::readyToInstall;
            state.downloadedInstallerFilename = new_version_info.installer_filename;
            if (show_notifications)
            {
                notifications::show_new_version_available(new_version_info, Strings);
            }
        }
        else
        {
            state.state = UpdateState::errorDownloading;
            state.downloadedInstallerFilename = {};
            Logger::error("Couldn't download new installer");
        }
    }
    else
    {
        Logger::trace(L"New version is ready to download, showing notification");
        state.state = UpdateState::readyToDownload;
        state.downloadedInstallerFilename = {};
        if (show_notifications)
        {
            notifications::show_open_settings_for_update(Strings);
        }
    }
}

void PeriodicUpdateWorker()
{
    for (;;)
    {
        auto state = UpdateState::read();
        int64_t sleep_minutes_till_next_update = 0;
        if (state.githubUpdateLastCheckedDate.has_value())
        {
            int64_t last_checked_minutes_ago = timeutil::diff::in_minutes(timeutil::now(), *state.githubUpdateLastCheckedDate);
            if (last_checked_minutes_ago < 0)
            {
                last_checked_minutes_ago = UPDATE_CHECK_INTERVAL_MINUTES;
            }
            sleep_minutes_till_next_update = max(0, UPDATE_CHECK_INTERVAL_MINUTES - last_checked_minutes_ago);
        }

        std::this_thread::sleep_for(std::chrono::minutes{ sleep_minutes_till_next_update });

        const bool download_update = !CouldBeCostlyConnection() && get_general_settings().downloadUpdatesAutomatically;
        bool version_info_obtained = false;
        try
        {
            const auto new_version_info = get_github_version_info_async(Strings).get();
            if (new_version_info.has_value())
            {
                version_info_obtained = true;
                ProcessNewVersionInfo(*new_version_info, state, download_update, true);
            }
            else
            {
                Logger::error(L"Couldn't obtain version info from github: {}", new_version_info.error());
            }
        }
        catch (...)
        {
            Logger::error("periodic_update_worker: error while processing version info");
        }

        if (version_info_obtained)
        {
            UpdateState::store([&](UpdateState& v) {
                v = std::move(state);
            });
        }
        else
        {
            std::this_thread::sleep_for(std::chrono::minutes{ UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES });
        }
    }
}

void CheckForUpdatesSettingsCallback()
{
    Logger::trace(L"Check for updates callback invoked");
    auto state = UpdateState::read();
    try
    {
        auto new_version_info = get_github_version_info_async(Strings).get();
        if (!new_version_info)
        {
            // If we couldn't get a new version from github for some reason, assume we're up to date, but also log error
            new_version_info = version_up_to_date{};
            Logger::error(L"Couldn't obtain version info from github: {}", new_version_info.error());
        }
        const bool download_update = !CouldBeCostlyConnection() && get_general_settings().downloadUpdatesAutomatically;
        ProcessNewVersionInfo(*new_version_info, state, download_update, false);
        UpdateState::store([&](UpdateState& v) {
            v = std::move(state);
        });
    }
    catch (...)
    {
        Logger::error("CheckForUpdatesSettingsCallback: error while processing version info");
    }
}
