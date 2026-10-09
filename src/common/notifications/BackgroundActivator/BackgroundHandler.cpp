#include "pch.h"
#include "BackgroundHandler.h"
#include "BackgroundHandler.g.cpp"

#include "handler_functions.h"

namespace winrt::BackgroundActivator::implementation
{
    using Windows::ApplicationModel::Background::IBackgroundTaskInstance;
    using Windows::UI::Notifications::ToastNotificationActionTriggerDetail;

    void BackgroundHandler::Run(IBackgroundTaskInstance bti)
    {
        const auto details = bti.TriggerDetails().try_as<ToastNotificationActionTriggerDetail>();
        if (!details)
        {
            return;
        }

        dispatch_to_background_handler(details.Argument());
    }
}
