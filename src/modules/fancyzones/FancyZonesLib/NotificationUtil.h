#pragma once

#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/utils/resources.h>

namespace FancyZonesNotifications
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const wchar_t FancyZonesRunAsAdminInfoPage[] = L"https://aka.ms/powertoysDetectedElevatedHelp";
        const wchar_t ToastNotificationButtonUrl[] = L"powertoys://cant_drag_elevated_disable/";
    }

    inline void WarnIfElevationIsRequired()
    {
        using namespace notifications;
        using namespace NonLocalizable;

        auto settings = PTSettingsHelper::load_general_settings();
        auto enableWarningsElevatedApps = settings.GetNamedBoolean(L"enable_warnings_elevated_apps", true);

        static bool warning_shown = false;
        if (enableWarningsElevatedApps && !warning_shown && !is_toast_disabled(CantDragElevatedDontShowAgainRegistryPath, 0))
        {
            std::vector<action_t> actions = {
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), FancyZonesRunAsAdminInfoPage },
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), ToastNotificationButtonUrl }
            };
            show_toast_with_activations(GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED),
                                        GET_RESOURCE_STRING(IDS_FANCYZONES),
                                        {},
                                        std::move(actions));
        }
    }
}
