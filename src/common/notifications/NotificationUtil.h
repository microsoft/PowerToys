#pragma once

#include <common/notifications/notifications.h>
#include <common/notifications/dont_show_again.h>
#include <common/utils/resources.h>

#include "Generated Files/resource.h"

namespace notifications
{
    // Non-Localizable strings
    namespace NonLocalizable
    {
        const wchar_t RunAsAdminInfoPage[] = L"https://aka.ms/powertoysDetectedElevatedHelp";
        const wchar_t ToastNotificationButtonUrl[] = L"powertoys://cant_drag_elevated_disable/";
    }

    inline void WarnIfElevationIsRequired(std::wstring title)
    {
        using namespace NonLocalizable;

        static bool warning_shown = false;
        if (!warning_shown && !is_toast_disabled(ElevatedDontShowAgainRegistryPath, 0))
        {
            std::vector<action_t> actions = {
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_LEARN_MORE), RunAsAdminInfoPage },
                link_button{ GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED_DIALOG_DONT_SHOW_AGAIN), ToastNotificationButtonUrl }
            };
            show_toast_with_activations(GET_RESOURCE_STRING(IDS_CANT_DRAG_ELEVATED),
                                        title,
                                        {},
                                        std::move(actions));
        }
    }
}