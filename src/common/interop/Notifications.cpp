#include "pch.h"
#include "Notifications.h"
#include "Notifications.g.cpp"
#include "../notifications/notifications.h"

namespace winrt::PowerToys::Interop::implementation
{
    using namespace winrt::Windows::Foundation;
    void Notifications::ShowToast(hstring const& title, hstring const& content)
    {
        notifications::show_toast(title.c_str(), content.c_str());
    }

    void Notifications::RemoveToastsByTag(hstring const& tag)
    {
        notifications::remove_toasts_by_tag(tag.c_str());
    }

    void Notifications::RemoveAllScheduledToasts()
    {
        notifications::remove_all_scheduled_toasts();
    }

    void Notifications::ShowToastWithActivation(hstring const& title, hstring const& content, hstring const& tag)
    {
        notifications::toast_params params;
        params.tag = tag.c_str();
        notifications::show_toast(title.c_str(), content.c_str(), params);
    }

    void Notifications::ShowUpdateAvailableNotification(hstring const& title, hstring const& content, hstring const& tag, hstring const& updateNowString, hstring const& openOverviewString) {
        notifications::toast_params params;
        params.tag = tag.c_str();

        notifications::show_toast_with_activations(std::move(content.c_str()),
                                                   title.c_str(),
                                    {},
                                    { notifications::link_button{ updateNowString.c_str(),
                                                   L"powertoys://update_now/" },
                                      notifications::link_button{ openOverviewString.c_str(),
                                                   L"powertoys://open_overview/" } },
                                    std::move(params),
                                    L"powertoys://open_overview/");
    }

    void Notifications::RunDesktopAppActivatorLoop()
    {
        notifications::run_desktop_app_activator_loop();
    }
}
