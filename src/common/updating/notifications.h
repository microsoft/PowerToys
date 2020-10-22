#pragma once

namespace updating
{
    struct new_version_download_info;

    namespace notifications
    {
        struct strings
        {
            std::wstring GITHUB_NEW_VERSION_AVAILABLE;
            std::wstring GITHUB_NEW_VERSION_DOWNLOAD_STARTED;
            std::wstring GITHUB_NEW_VERSION_READY_TO_INSTALL;
            std::wstring GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR;
            std::wstring GITHUB_NEW_VERSION_UPDATE_NOW;
            std::wstring GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART;
            std::wstring UNINSTALLATION_UNKNOWN_ERROR;
            std::wstring GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT;
            std::wstring GITHUB_NEW_VERSION_UNAVAILABLE;
            std::wstring GITHUB_NEW_VERSION_VISIT;
            std::wstring GITHUB_NEW_VERSION_MORE_INFO;
            std::wstring GITHUB_NEW_VERSION_ABORT;
            std::wstring GITHUB_NEW_VERSION_SNOOZE_TITLE;
            std::wstring SNOOZE_BUTTON;
            std::wstring GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D;
            std::wstring GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D;
            std::wstring DOWNLOAD_IN_PROGRESS;
            std::wstring DOWNLOAD_COMPLETE;
            std::wstring TOAST_TITLE;
            std::wstring OFFER_UNINSTALL_MSI;
            std::wstring OFFER_UNINSTALL_MSI_TITLE;
            template<typename = void>
            static strings create()
            {
                return strings{
                    .GITHUB_NEW_VERSION_AVAILABLE = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE),
                    .GITHUB_NEW_VERSION_DOWNLOAD_STARTED = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_DOWNLOAD_STARTED),
                    .GITHUB_NEW_VERSION_READY_TO_INSTALL = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_READY_TO_INSTALL),
                    .GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR),
                    .GITHUB_NEW_VERSION_UPDATE_NOW = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_NOW),
                    .GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_AFTER_RESTART),
                    .UNINSTALLATION_UNKNOWN_ERROR = GET_RESOURCE_STRING(IDS_UNINSTALLATION_UNKNOWN_ERROR),
                    .GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT),
                    .GITHUB_NEW_VERSION_UNAVAILABLE = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UNAVAILABLE),
                    .GITHUB_NEW_VERSION_VISIT = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_VISIT),
                    .GITHUB_NEW_VERSION_MORE_INFO = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),
                    .GITHUB_NEW_VERSION_ABORT = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_ABORT),
                    .GITHUB_NEW_VERSION_SNOOZE_TITLE = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_SNOOZE_TITLE),
                    .SNOOZE_BUTTON = GET_RESOURCE_STRING(IDS_SNOOZE_BUTTON),
                    .GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_SNOOZE_1D),
                    .GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_SNOOZE_5D),
                    .DOWNLOAD_IN_PROGRESS = GET_RESOURCE_STRING(IDS_DOWNLOAD_IN_PROGRESS),
                    .DOWNLOAD_COMPLETE = GET_RESOURCE_STRING(IDS_DOWNLOAD_COMPLETE),
                    .TOAST_TITLE = GET_RESOURCE_STRING(IDS_TOAST_TITLE),
                    .OFFER_UNINSTALL_MSI = GET_RESOURCE_STRING(IDS_OFFER_UNINSTALL_MSI),
                    .OFFER_UNINSTALL_MSI_TITLE = GET_RESOURCE_STRING(IDS_OFFER_UNINSTALL_MSI_TITLE)
                };
            }
        };

        void show_unavailable(const notifications::strings& strings);
        void show_available(const updating::new_version_download_info& info, const strings&);
        void show_download_start(const updating::new_version_download_info& info, const strings&);
        void show_visit_github(const updating::new_version_download_info& info, const strings&);
        void show_install_error(const updating::new_version_download_info& info, const strings&);
        void show_version_ready(const updating::new_version_download_info& info, const strings&);
        void show_uninstallation_error(const notifications::strings& strings);

        void update_download_progress(const updating::new_version_download_info& info, float progress, const notifications::strings& strings);
    }
}