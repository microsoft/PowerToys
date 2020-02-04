#include "pch.h"
#include "BackgroundHandler.h"
#include "BackgroundHandler.g.cpp"

#include "handler_functions.h"

namespace winrt::PowerToysNotifications::implementation
{
    using Windows::ApplicationModel::Background::IBackgroundTaskInstance;
    using Windows::UI::Notifications::ToastNotificationActionTriggerDetail;
    using Windows::Foundation::WwwFormUrlDecoder;

    void BackgroundHandler::Run(IBackgroundTaskInstance bti)
    {
        const auto details = bti.TriggerDetails().try_as<ToastNotificationActionTriggerDetail>();
        if (!details)
        {
            return;
        }

        WwwFormUrlDecoder decoder{details.Argument()};

        const size_t button_id = std::stoi(decoder.GetFirstValueByName(L"button_id").c_str());
        auto handler = decoder.GetFirstValueByName(L"handler");
        dispatch_to_backround_handler(std::move(handler), std::move(bti), button_id);
    }
}
