#pragma once
#include "Notifications.g.h"

#include "../notifications/notifications.h"

namespace winrt::PowerToys::Interop::implementation
{
    using namespace winrt::Windows::Foundation;

    struct Notifications : NotificationsT<Notifications>
    {
        static void ShowToast(hstring const& title, hstring const& content);
        static void RemoveToastsByTag(hstring const& tag);
        static void RemoveAllScheduledToasts();
        static void ShowToastWithActivation(hstring const& title, hstring const& content, hstring const& tag);
        static void RunDesktopAppActivatorLoop();
        static void ShowUpdateAvailableNotification(hstring const& title, hstring const& content, hstring const& tag, hstring const& updateNowString, hstring const& openOverviewString);
    };
}

namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Notifications : NotificationsT<Notifications, implementation::Notifications>
    {
    };
}
