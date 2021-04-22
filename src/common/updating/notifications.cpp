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
            MessageBoxW(nullptr, reason.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_OK | MB_ICONWARNING);
        }

        void show_visit_github(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            std::wstring contents = strings.GITHUB_NEW_VERSION_AVAILABLE_OFFER_VISIT;
            contents += L'\n';
            contents += current_version_to_next_version(info);

            const bool openURL = IDYES == MessageBoxW(nullptr, contents.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_ICONQUESTION | MB_YESNO);
            if (openURL)
            {
                winrt::Windows::System::Launcher::LaunchUriAsync(info.release_page_uri).get();
            }
        }

        void show_install_error(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            std::wstring contents = strings.GITHUB_NEW_VERSION_DOWNLOAD_INSTALL_ERROR;
            contents += L'\n';
            contents += current_version_to_next_version(info);
            MessageBoxW(nullptr, contents.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_OK | MB_ICONERROR);
        }

        bool show_confirm_update(const updating::new_version_download_info& info, const notifications::strings& strings)
        {
            std::wstring new_version_ready{ strings.GITHUB_NEW_VERSION_READY_TO_INSTALL };
            new_version_ready += L'\n';
            new_version_ready += current_version_to_next_version(info);

            return IDYES == MessageBoxW(nullptr, new_version_ready.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_ICONQUESTION | MB_YESNO);
        }

        void show_uninstallation_error(const notifications::strings& strings)
        {
            MessageBoxW(nullptr, strings.UNINSTALLATION_UNKNOWN_ERROR.c_str(), strings.NOTIFICATION_TITLE.c_str(), MB_OK | MB_ICONERROR);
        }
    }
}
