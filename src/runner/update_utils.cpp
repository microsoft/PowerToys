#include "pch.h"

#include "Generated Files/resource.h"

#include "action_runner_utils.h"
#include "update_state.h"
#include "update_utils.h"

#include <common/updating/installer.h>
#include <common/updating/updating.h>
#include <common/utils/resources.h>
#include <common/utils/timeutil.h>
#include <runner/general_settings.h>

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

void github_update_worker()
{
    for (;;)
    {
        auto state = UpdateState::read();
        int64_t sleep_minutes_till_next_update = 0;
        if (state.github_update_last_checked_date.has_value())
        {
            int64_t last_checked_minutes_ago = timeutil::diff::in_minutes(timeutil::now(), *state.github_update_last_checked_date);
            if (last_checked_minutes_ago < 0)
            {
                last_checked_minutes_ago = UPDATE_CHECK_INTERVAL_MINUTES;
            }
            sleep_minutes_till_next_update = max(0, UPDATE_CHECK_INTERVAL_MINUTES - last_checked_minutes_ago);
        }

        std::this_thread::sleep_for(std::chrono::minutes{ sleep_minutes_till_next_update });
        const bool download_updates_automatically = get_general_settings().downloadUpdatesAutomatically;
        bool update_check_ok = false;
        try
        {
            update_check_ok = updating::try_autoupdate(download_updates_automatically, Strings).get();
        }
        catch (...)
        {
            // Couldn't autoupdate
            update_check_ok = false;
        }

        if (update_check_ok)
        {
            UpdateState::store([](UpdateState& state) {
                state.github_update_last_checked_date.emplace(timeutil::now());
            });
        }
        else
        {
            std::this_thread::sleep_for(std::chrono::minutes{ UPDATE_CHECK_AFTER_FAILED_INTERVAL_MINUTES });
        }
    }
}

std::optional<updating::github_version_info> check_for_updates()
{
    try
    {
        auto version_check_result = updating::get_github_version_info_async(Strings).get();
        if (!version_check_result)
        {
            updating::notifications::show_unavailable(Strings, std::move(version_check_result.error()));
            return std::nullopt;
        }

        if (std::holds_alternative<updating::version_up_to_date>(*version_check_result))
        {
            updating::notifications::show_unavailable(Strings, Strings.GITHUB_NEW_VERSION_UP_TO_DATE);
            return std::move(*version_check_result);
        }

        auto new_version = std::get<updating::new_version_download_info>(*version_check_result);
        updating::notifications::show_available(new_version, Strings);
        return std::move(new_version);
    }
    catch (...)
    {
        // Couldn't autoupdate
    }
    return std::nullopt;
}

bool launch_pending_update()
{
    try
    {
        auto update_state = UpdateState::read();
        if (update_state.pending_update)
        {
            UpdateState::store([](UpdateState& state) {
                state.pending_update = false;
                state.pending_installer_filename = {};
            });
            std::wstring args{ UPDATE_NOW_LAUNCH_STAGE1_START_PT_CMDARG };
            args += L' ';
            args += update_state.pending_installer_filename;

            launch_action_runner(args.c_str());
            return true;
        }
    }
    catch (...)
    {
    }
    return false;
}
