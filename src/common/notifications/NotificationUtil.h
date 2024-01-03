#pragma once

#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/utils/resources.h>
#include <common/SettingsAPI/settings_helpers.h>

#include "Generated Files/resource.h"

namespace notifications
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const wchar_t RunAsAdminInfoPage[] = L"https://aka.ms/powertoysDetectedElevatedHelp";
        const wchar_t ToastNotificationButtonUrl[] = L"powertoys://cant_drag_elevated_disable/";
    }

    inline void WarnIfElevationIsRequired(std::wstring title, std::wstring message, std::wstring button1, std::wstring button2)
    {
        using namespace NonLocalizable;

        auto settings = PTSettingsHelper::load_general_settings();
        auto enableWarningsElevatedApps = settings.GetNamedBoolean(L"enable_warnings_elevated_apps", true);

        static bool warning_shown = false;
        if (enableWarningsElevatedApps && !warning_shown && !is_toast_disabled(ElevatedDontShowAgainRegistryPath, ElevatedDisableIntervalInDays))
        {
            std::vector<action_t> actions = {
                link_button{ button1, RunAsAdminInfoPage },
                link_button{ button2, ToastNotificationButtonUrl }
            };
            show_toast_with_activations(message,
                                        title,
                                        {},
                                        std::move(actions));
            warning_shown = true;
        }
    }
}