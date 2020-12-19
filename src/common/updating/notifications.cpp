#include "pch.h"

#include "notifications.h"

#include <common/notifications/notifications.h>

#include "updating.h"

#include <common/version/helper.h>
#include <common/version/version.h>

namespace updating
{
    namespace notifications
    {
        using namespace ::notifications;
        std::wstring current_version_to_next_version(const updating::new_version_download_info& info)
        {
            auto current_version_to_next_version = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
            current_version_to_next_version += L" -> ";
            current_version_to_next_version += info.version.toWstring();
            return current_version_to_next_version;
        }

        void show_unavailable(const notifications::strings& strings, std::wstring reason)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            show_toast(std::move(reason), strings.TOAST_TITLE, std::move(toast_params));
        }

        void show_available(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            std::wstring contents = strings.GITHUB_NEW_VERSION_AVAILABLE;
            contents += L'\n';
            contents += current_version_to_next_version(info);

            show_toast_with_activations(std::move(contents),
                                        strings.TOAST_TITLE,
                                        {},
                                        { link_button{ strings.GITHUB_NEW_VERSION_UPDATE_NOW,
                                                       L"powertoys://download_and_install_update/" },
                                          link_button{ strings.GITHUB_NEW_VERSION_MORE_INFO,
                                                       info.release_page_uri.ToString().c_str() } },
                                        std::move(toast_params));
        }

        void show_download_start(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            progress_bar_params progress_bar_params;
            std::wstring progress_title{ info.version.toWstring() };
            progress_title += L' ';
            progress_title += strings.DOWNLOAD_IN_PROGRESS;

            progress_bar_params.progress_title = progress_title;
            progress_bar_params.progress = .0f;
            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false, std::move(progress_bar_params) };
            show_toast_with_activations(strings.GITHUB_NEW_VERSION_DOWNLOAD_STARTED,
                                        strings.TOAST_TITLE,
                                        {},
                                        {},
                                        std::move(toast_params));
        }

        void show_visit_github(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            std::wstring contents = strings.GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT;
            contents += L'\n';
            contents += current_version_to_next_version(info);
            show_toast_with_activations(std::move(contents),
                                        strings.TOAST_TITLE,
                                        {},
                                        { link_button{ strings.GITHUB_NEW_VERSION_VISIT,
                                                       info.release_page_uri.ToString().c_str() } },
                                        std::move(toast_params));
        }

        void show_install_error(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            std::wstring contents = strings.GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR;
            contents += L'\n';
            contents += current_version_to_next_version(info);
            show_toast_with_activations(std::move(contents),
                                        strings.TOAST_TITLE,
                                        {},
                                        { link_button{ strings.GITHUB_NEW_VERSION_VISIT, info.release_page_uri.ToString().c_str() } },
                                        std::move(toast_params));
        }

        void show_version_ready(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            std::wstring new_version_ready{ strings.GITHUB_NEW_VERSION_READY_TO_INSTALL };
            new_version_ready += L'\n';
            new_version_ready += current_version_to_next_version(info);

            show_toast_with_activations(std::move(new_version_ready),
                                        strings.TOAST_TITLE,
                                        {},
                                        { link_button{ strings.GITHUB_NEW_VERSION_UPDATE_NOW,
                                                       L"powertoys://update_now/" + info.installer_filename },
                                          link_button{ strings.GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART,
                                                       L"powertoys://schedule_update/" + info.installer_filename },
                                          snooze_button{
                                              strings.GITHUB_NEW_VERSION_SNOOZE_TITLE,
                                              { { strings.GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D, 24 * 60 },
                                                { strings.GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D, 120 * 60 } },
                                              strings.SNOOZE_BUTTON } },
                                        std::move(toast_params));
        }

        void show_uninstallation_error(const notifications::strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            show_toast(strings.UNINSTALLATION_UNKNOWN_ERROR, strings.TOAST_TITLE);
        }

        void update_download_progress(const updating::new_version_download_info& info, float progress, const notifications::strings& strings)
        {
            progress_bar_params progress_bar_params;

            std::wstring progress_title{ info.version.toWstring() };
            progress_title += L' ';
            progress_title += progress < 1 ? strings.DOWNLOAD_IN_PROGRESS : strings.DOWNLOAD_COMPLETE;
            progress_bar_params.progress_title = progress_title;
            progress_bar_params.progress = progress;
            update_toast_progress_bar(UPDATING_PROCESS_TOAST_TAG, progress_bar_params);
        }
    }
}
