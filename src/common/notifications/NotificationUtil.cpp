#include "pch.h"
#include "NotificationUtil.h"

#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/utils/resources.h>
#include <common/SettingsAPI/settings_helpers.h>

// Non-Localizable strings
namespace NonLocalizable
{
    const wchar_t RunAsAdminInfoPage[] = L"https://aka.ms/powertoysDetectedElevatedHelp";
    const wchar_t ToastNotificationButtonUrl[] = L"powertoys://cant_drag_elevated_disable/";
}

namespace notifications
{
    NotificationUtil::NotificationUtil()
    {
        ReadSettings();
        auto settingsFileName = PTSettingsHelper::get_powertoys_general_save_file_location();

        m_settingsFileWatcher = std::make_unique<FileWatcher>(settingsFileName, [this]() {
            ReadSettings();
        });
    }

    NotificationUtil::~NotificationUtil()
    {
        m_settingsFileWatcher.reset();
    }

    void NotificationUtil::WarnIfElevationIsRequired(std::wstring title, std::wstring message, std::wstring button1, std::wstring button2)
    {
        if (m_warningsElevatedApps && !m_warningShown && !is_toast_disabled(ElevatedDontShowAgainRegistryPath, ElevatedDisableIntervalInDays))
        {
            std::vector<action_t> actions = {
                link_button{ button1, NonLocalizable::RunAsAdminInfoPage },
                link_button{ button2, NonLocalizable::ToastNotificationButtonUrl }
            };

            show_toast_with_activations(message,
                                        title,
                                        {},
                                        std::move(actions));

            m_warningShown = true;
        }
    }

    void NotificationUtil::ReadSettings()
    {
        auto settings = PTSettingsHelper::load_general_settings();
        m_warningsElevatedApps = settings.GetNamedBoolean(L"enable_warnings_elevated_apps", true);
    }
}
