#include "pch.h"

#include "toast_notifications_helper.h"

#include <common/notifications.h>

#include "updating.h"

#include "VersionHelper.h"
#include "version.h"

namespace
{
    const wchar_t UPDATE_NOTIFY_TOAST_TAG[] = L"PTUpdateNotifyTag";
    const wchar_t UPDATE_READY_TOAST_TAG[] = L"PTUpdateReadyTag";
}

namespace localized_strings
{
    const wchar_t GITHUB_NEW_VERSION_AVAILABLE[] = L"An update to PowerToys is available.\n";
    const wchar_t GITHUB_NEW_VERSION_DOWNLOAD_STARTED[] = L"PowerToys download started.\n";
    const wchar_t GITHUB_NEW_VERSION_READY_TO_INSTALL[] = L"An update to PowerToys is ready to install.\n";
    const wchar_t GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR[] = L"Error: couldn't download PowerToys installer. Visit our GitHub page to update.\n";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_NOW[] = L"Update now";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART[] = L"At next launch";
    
    const wchar_t UNINSTALLATION_SUCCESS[] = L"Previous version of PowerToys was uninstalled successfully.";
    const wchar_t UNINSTALLATION_UNKNOWN_ERROR[] = L"Error: please uninstall the previous version of PowerToys manually.";

    const wchar_t GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT[] = L"An update to PowerToys is available. Visit our GitHub page to update.\n";
    const wchar_t GITHUB_NEW_VERSION_UNAVAILABLE[] = L"PowerToys is up to date.\n";
    const wchar_t GITHUB_NEW_VERSION_VISIT[] = L"Visit";
    const wchar_t GITHUB_NEW_VERSION_MORE_INFO[] = L"More info...";
    const wchar_t GITHUB_NEW_VERSION_ABORT[] = L"Abort";
    const wchar_t GITHUB_NEW_VERSION_SNOOZE_TITLE[] = L"Click Snooze to be reminded in:";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D[] = L"1 day";
    const wchar_t GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D[] = L"5 days";
}

namespace updating
{
    namespace notifications
    {
        using namespace localized_strings;

        std::wstring current_version_to_next_version(const updating::new_version_download_info& info)
        {
            auto current_version_to_next_version = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
            current_version_to_next_version += L" -> ";
            current_version_to_next_version += info.version_string;
            return current_version_to_next_version;
        }

        void show_unavailable()
        {
            ::notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
            std::wstring contents = GITHUB_NEW_VERSION_UNAVAILABLE;
            ::notifications::show_toast(std::move(contents), std::move(toast_params));
        }

        void show_available(const updating::new_version_download_info& info)
        {
            ::notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
            std::wstring contents = GITHUB_NEW_VERSION_AVAILABLE;
            contents += current_version_to_next_version(info);

            ::notifications::show_toast_with_activations(std::move(contents), {}, 
                { 
                    ::notifications::link_button{ GITHUB_NEW_VERSION_UPDATE_NOW, L"powertoys://download_and_install_update/" },
                    ::notifications::link_button{ GITHUB_NEW_VERSION_MORE_INFO, info.release_page_uri.ToString().c_str() } 
                }, 
                std::move(toast_params));
        }

        void show_download_start(const updating::new_version_download_info& info)
        {
            ::notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false, 0.0f, info.version_string };
            ::notifications::show_toast_with_activations(localized_strings::GITHUB_NEW_VERSION_DOWNLOAD_STARTED, {}, {}, std::move(toast_params));
        }

        void show_visit_github(const updating::new_version_download_info& info)
        {
            ::notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
            std::wstring contents = GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT;
            contents += current_version_to_next_version(info);
            ::notifications::show_toast_with_activations(std::move(contents), {}, { ::notifications::link_button{ GITHUB_NEW_VERSION_VISIT, info.release_page_uri.ToString().c_str() } }, std::move(toast_params));
        }

        void show_install_error(const updating::new_version_download_info& info)
        {
            ::notifications::toast_params toast_params{ UPDATE_NOTIFY_TOAST_TAG, false };
            std::wstring contents = GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR;
            contents += current_version_to_next_version(info);
            ::notifications::show_toast_with_activations(std::move(contents), {}, { ::notifications::link_button{ GITHUB_NEW_VERSION_VISIT, info.release_page_uri.ToString().c_str() } }, std::move(toast_params));
        }

        void show_version_ready(const updating::new_version_download_info& info)
        {
            ::notifications::toast_params toast_params{ UPDATE_READY_TOAST_TAG, false };
            std::wstring new_version_ready{ GITHUB_NEW_VERSION_READY_TO_INSTALL };
            new_version_ready += current_version_to_next_version(info);

            ::notifications::show_toast_with_activations(std::move(new_version_ready),
                                                       {},
                                                         { ::notifications::link_button{ GITHUB_NEW_VERSION_UPDATE_NOW, L"powertoys://update_now/" + info.installer_filename },
                                                           ::notifications::link_button{ GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART, L"powertoys://schedule_update/" + info.installer_filename },
                                                           ::notifications::snooze_button{ GITHUB_NEW_VERSION_SNOOZE_TITLE, { { GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D, 24 * 60 }, { GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D, 120 * 60 } } } },
                                                       std::move(toast_params));
        }

        void show_uninstallation_success()
        {
            ::notifications::show_toast(localized_strings::UNINSTALLATION_SUCCESS);
        }

        void show_uninstallation_error()
        {
            ::notifications::show_toast(localized_strings::UNINSTALLATION_UNKNOWN_ERROR);
        }

        void update_download_progress(float progress)
        {
            ::notifications::toast_params toast_params { UPDATE_NOTIFY_TOAST_TAG, false, progress };
            ::notifications::update_progress_bar_toast(localized_strings::GITHUB_NEW_VERSION_DOWNLOAD_STARTED, std::move(toast_params));
        }
    }
}