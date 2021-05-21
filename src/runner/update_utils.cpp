#include "pch.h"

#include "Generated Files/resource.h"

#include "action_runner_utils.h"
#include "general_settings.h"
#include "update_utils.h"

#include <common/logger/logger.h>
#include <common/updating/installer.h>
#include <common/updating/http_client.h>
#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>

auto Strings = create_notifications_strings();

namespace
{
    constexpr int64_t UPDATE_CHECK_INTERVAL_MINUTES = 60 * 24;
    constexpr int64_t UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES = 60 * 2;
}

bool start_msi_uninstallation_sequence()
{
    const auto package_path = updating::get_msi_package_path();

    if (package_path.empty())
    {
        // No MSI version detected
        return true;
    }

    if (!updating::offer_msi_uninstallation(Strings))
    {
        // User declined to uninstall or opted for "Don't show again"
        return false;
    }
    auto sei = launch_action_runner(L"-uninstall_msi");

    WaitForSingleObject(sei.hProcess, INFINITE);
    DWORD exit_code = 0;
    GetExitCodeProcess(sei.hProcess, &exit_code);
    CloseHandle(sei.hProcess);
    return exit_code == 0;
}

using namespace updating;

bool could_be_costly_connection()
{
    using namespace winrt::Windows::Networking::Connectivity;
    ConnectionProfile internetConnectionProfile = NetworkInformation::GetInternetConnectionProfile();
    return internetConnectionProfile && internetConnectionProfile.IsWwanConnectionProfile();
}

void process_new_version_info(const github_version_info& version_info,
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

void periodic_update_worker()
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

        const bool download_update = !could_be_costly_connection() && get_general_settings().downloadUpdatesAutomatically;
        bool version_info_obtained = false;
        try
        {
            const auto new_version_info = get_github_version_info_async(Strings).get();
            if (new_version_info.has_value())
            {
                version_info_obtained = true;
                process_new_version_info(*new_version_info, state, download_update, true);
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

void check_for_updates_settings_callback()
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
        const bool download_update = !could_be_costly_connection() && get_general_settings().downloadUpdatesAutomatically;
        process_new_version_info(*new_version_info, state, download_update, false);
        UpdateState::store([&](UpdateState& v) {
            v = std::move(state);
        });
    }
    catch (...)
    {
        Logger::error("check_for_updates_settings_callback: error while processing version info");
    }
}
