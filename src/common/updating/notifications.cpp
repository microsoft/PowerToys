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
        std::wstring current_version_to_next_version(const new_version_download_info& info)
        {
            auto current_version_to_next_version = VersionHelper{ VERSION_MAJOR, VERSION_MINOR, VERSION_REVISION }.toWstring();
            current_version_to_next_version += L" -> ";
            current_version_to_next_version += info.version.toWstring();
            return current_version_to_next_version;
        }

        void show_new_version_available(const new_version_download_info& info, const strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };
            std::wstring contents = strings.GITHUB_NEW_VERSION_AVAILABLE;
            contents += L'\n';
            contents += current_version_to_next_version(info);

            show_toast_with_activations(std::move(contents),
                                        strings.NOTIFICATION_TITLE,
                                        {},
                                        { link_button{ strings.GITHUB_NEW_VERSION_UPDATE_NOW,
                                                       L"powertoys://update_now/" },
                                          link_button{ strings.GITHUB_NEW_VERSION_MORE_INFO,
                                                       L"powertoys://open_settings/" } },
                                        std::move(toast_params));
        }

        void show_open_settings_for_update(const strings& strings)
        {
            remove_toasts_by_tag(UPDATING_PROCESS_TOAST_TAG);

            toast_params toast_params{ UPDATING_PROCESS_TOAST_TAG, false };

            std::vector<action_t> actions = {
                link_button{ strings.GITHUB_NEW_VERSION_MORE_INFO,
                             L"powertoys://open_settings/" },
            };
            show_toast_with_activations(strings.GITHUB_NEW_VERSION_AVAILABLE,
                                        strings.NOTIFICATION_TITLE,
                                        {},
                                        std::move(actions),
                                        std::move(toast_params));
        }
    }
}
