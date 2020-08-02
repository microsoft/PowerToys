#pragma once

#include "BackgroundHandler.g.h"

namespace winrt::PowerToysNotifications::implementation
{
    struct BackgroundHandler : BackgroundHandlerT<BackgroundHandler>
    {
        BackgroundHandler() = default;

        void Run(winrt::Windows::ApplicationModel::Background::IBackgroundTaskInstance);
    };
}

namespace winrt::PowerToysNotifications::factory_implementation
{
    struct BackgroundHandler : BackgroundHandlerT<BackgroundHandler, implementation::BackgroundHandler>
    {
    };
}
