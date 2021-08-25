#pragma once
#include "App.g.h"
#include "App.base.h"
namespace winrt::PowerRenameUI_new::implementation
{
    class App : public AppT2<App>
    {
    public:
        App();
        ~App();
    };
}
namespace winrt::PowerRenameUI_new::factory_implementation
{
    class App : public AppT<App, implementation::App>
    {
    };
}
