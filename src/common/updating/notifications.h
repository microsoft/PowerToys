#pragma once

#include <string>

namespace updating
{
    struct new_version_download_info;

    namespace notifications
    {
        struct strings
        {
            std::wstring GITHUB_NEW_VERSION_AVAILABLE;
            std::wstring GITHUB_NEW_VERSION_MORE_INFO;
            std::wstring GITHUB_NEW_VERSION_UPDATE_NOW;

            std::wstring OFFER_UNINSTALL_MSI;
            std::wstring OFFER_UNINSTALL_MSI_TITLE;

            std::wstring NOTIFICATION_TITLE;

            std::wstring UNINSTALLATION_UNKNOWN_ERROR;
        };

        void show_new_version_available(const new_version_download_info& info, const strings& strings);
        void show_open_settings_for_update(const strings& strings);
    }
}

#define create_notifications_strings()                                                                                     \
    ::updating::notifications::strings                                                                                     \
    {                                                                                                                      \
        .GITHUB_NEW_VERSION_AVAILABLE = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_AVAILABLE),                             \
        .GITHUB_NEW_VERSION_MORE_INFO = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_MORE_INFO),                             \
        .GITHUB_NEW_VERSION_UPDATE_NOW = GET_RESOURCE_STRING(IDS_GITHUB_NEW_VERSION_UPDATE_NOW),                           \
        .OFFER_UNINSTALL_MSI = GET_RESOURCE_STRING(IDS_OFFER_UNINSTALL_MSI),                                               \
        .OFFER_UNINSTALL_MSI_TITLE = GET_RESOURCE_STRING(IDS_OFFER_UNINSTALL_MSI_TITLE),                                   \
        .NOTIFICATION_TITLE = GET_RESOURCE_STRING(IDS_TOAST_TITLE),                                                        \
        .UNINSTALLATION_UNKNOWN_ERROR = GET_RESOURCE_STRING(IDS_UNINSTALLATION_UNKNOWN_ERROR)                              \
    }
