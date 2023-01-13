#pragma once

#include "BackgroundHandler.g.h"

namespace winrt::BackgroundActivator::implementation
{
    struct BackgroundHandler : BackgroundHandlerT<BackgroundHandler>
    {
        BackgroundHandler() = default;

        void Run(winrt::Windows::ApplicationModel::Background::IBackgroundTaskInstance);
    };
}

namespace winrt::BackgroundActivator::factory_implementation
{
    struct BackgroundHandler : BackgroundHandlerT<BackgroundHandler, implementation::BackgroundHandler>
    {
    };
}
