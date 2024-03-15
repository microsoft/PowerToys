#include "pch.h"

#include "Generated Files/resource.h"

#include "ActionRunnerUtils.h"
#include "general_settings.h"
#include "UpdateUtils.h"

#include <common/utils/gpo.h>
#include <common/logger/logger.h>
#include <common/notifications/notifications.h>
#include <common/updating/installer.h>
#include <common/updating/updating.h>
#include <common/updating/updateState.h>
#include <common/utils/HttpClient.h>
#include <common/utils/process_path.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>
#include <common/version/version.h>

namespace
{
    constexpr int64_t UPDATE_CHECK_INTERVAL_MINUTES = 60 * 24;
    constexpr int64_t UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES = 60 * 2;

    // How many minor versions to suspend the toast notification (example: installed=0.60.0, suspend=2, next notification=0.63.*)
    // Attention: When changing this value please update the ADML file to.
    const int UPDATE_NOTIFICATION_TOAST_SUSPEND_MINOR_VERSION_COUNT = 2;
}
using namespace notifications;
using namespace updating;

std::wstring CurrentVersionToNextVersion(const new_version_download_info& info)
{
    auto result = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
    result += L" \u2192 "; // Right arrow
    result += info.version.toWstring();
    return result;
}

void ShowNewVersionAvailable(const new_version_download_info& info)
{
    remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

    toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
    std::wstring contents = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE);
    contents += L'\n';
    contents += CurrentVersionToNextVersion(info);

    show_toast_with_activations(std::move(contents),
                                GET_RESOURCE_STRING(IDS_TOAST_TITLE),
                                {},
                                { link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_NOW),
                                               L"powertoys://update_now/" },
                                  link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),
                                               L"powertoys://open_settings/" } },
                                std::move(toast_params));
}

void ShowOpenSettingsForUpdate()
{
    remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

    toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };

    std::vector<action_t> actions = {
        link_button{ GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),
                     L"powertoys://open_settings/" },
    };
    show_toast_with_activations(GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE),
                                GET_RESOURCE_STRING(IDS_TOAST_TITLE),
                                {},
                                std::move(actions),
                                std::move(toast_params));
}

SHELLEXECUTEINFOW LaunchPowerToysUpdate(const wchar_t* cmdline)
{
    std::wstring powertoysUpdaterPath;
    powertoysUpdaterPath = get_module_folderpath();

    powertoysUpdaterPath += L"\\PowerToys.Update.exe";
    SHELLEXECUTEINFOW sei{ sizeof(sei) };
    sei.fMask = { SEE_MASK_FLAG_NO_UI | SEE_MASK_NOASYNC | SEE_MASK_NOCLOSEPROCESS };
    sei.lpFile = powertoysUpdaterPath.c_str();
    sei.nShow = SW_SHOWNORMAL;
    sei.lpParameters = cmdline;
    ShellExecuteExW(&sei);
    return sei;
}

bool IsMeteredConnection()
{
    using namespace winrt::Windows::Networking::Connectivity;
    ConnectionProfile internetConnectionProfile = NetworkInformation::GetInternetConnectionProfile();
    if (!internetConnectionProfile)
    {
        return false;
    }

    if (internetConnectionProfile.IsWwanConnectionProfile())
    {
        return true;
    }

    ConnectionCost connectionCost = internetConnectionProfile.GetConnectionCost();
    if (connectionCost.Roaming()
        || connectionCost.OverDataLimit()
        || connectionCost.NetworkCostType() == NetworkCostType::Fixed
        || connectionCost.NetworkCostType() == NetworkCostType::Variable)
    {
        return true;
    }

    return false;
}

void ProcessNewVersionInfo(const github_version_info& version_info,
                           UpdateState& state,
                           const bool download_update,
                           bool show_notifications)
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

    // Check toast notification GPOs and settings. (We check only if notifications are allowed. This is the case if we are triggered by the periodic check.)
    // Disable notification GPO or setting
    bool disable_notification_setting = get_general_settings().showNewUpdatesToastNotification == false;
    if (show_notifications && (disable_notification_setting || powertoys_gpo::getDisableNewUpdateToastValue() == powertoys_gpo::gpo_rule_configured_enabled))
    {
        Logger::info(L"There is a new update available or ready to install. But the toast notification is disabled by setting or GPO.");
        show_notifications = false;
    }
    // Suspend notification GPO
    else if (show_notifications && powertoys_gpo::getSuspendNewUpdateToastValue() == powertoys_gpo::gpo_rule_configured_enabled)
    {
        Logger::info(L"GPO to suspend new update toast notification is enabled.");
        if (new_version_info.version.major <= VERSION_MAJOR && new_version_info.version.minor - VERSION_MINOR <= UPDATE_NOTIFICATION_TOAST_SUSPEND_MINOR_VERSION_COUNT)
        {
            Logger::info(L"The difference between the installed version and the newer version is within the allowed period. The toast notification is not shown.");
            show_notifications = false;
        }
        else
        {
            Logger::info(L"The installed version is older than allowed for suspending the toast notification. The toast notification is shown.");
        }
    }

    if (download_update)
    {
        Logger::trace(L"Downloading installer for a new version");

        // Cleanup old updates before downloading the latest
        updating::cleanup_updates();

        if (download_new_version(new_version_info).get())
        {
            state.state = UpdateState::readyToInstall;
            state.downloadedInstallerFilename = new_version_info.installer_filename;
            if (show_notifications)
            {
                ShowNewVersionAvailable(new_version_info);
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
            ShowOpenSettingsForUpdate();
        }
    }
}

void PeriodicUpdateWorker()
{
    for (;;)
    {
        auto state = UpdateState::read();
        int64_t sleep_minutes_till_next_update = UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES;
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

        // Auto download setting.
        bool download_update = !IsMeteredConnection() && get_general_settings().downloadUpdatesAutomatically;
        if (powertoys_gpo::getDisableAutomaticUpdateDownloadValue() == powertoys_gpo::gpo_rule_configured_enabled)
        {
            Logger::info(L"Automatic download of updates is disabled by GPO.");
            download_update = false;
        }

        bool version_info_obtained = false;
        try
        {
            const auto new_version_info = get_github_version_info_async().get();
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

void CheckForUpdatesCallback()
{
    Logger::trace(L"Check for updates callback invoked");
    auto state = UpdateState::read();
    try
    {
        auto new_version_info = get_github_version_info_async().get();
        if (!new_version_info)
        {
            // We couldn't get a new version from github for some reason, log error
            state.state = UpdateState::networkError;
            Logger::error(L"Couldn't obtain version info from github: {}", new_version_info.error());
        }
        else
        {
            // Auto download setting
            bool download_update = !IsMeteredConnection() && get_general_settings().downloadUpdatesAutomatically;
            if (powertoys_gpo::getDisableAutomaticUpdateDownloadValue() == powertoys_gpo::gpo_rule_configured_enabled)
            {
                Logger::info(L"Automatic download of updates is disabled by GPO.");
                download_update = false;
            }

            ProcessNewVersionInfo(*new_version_info, state, download_update, false);
        }

        UpdateState::store([&](UpdateState& v) {
            v = std::move(state);
        });
    }
    catch (...)
    {
        Logger::error("CheckForUpdatesCallback: error while processing version info");
    }
}
