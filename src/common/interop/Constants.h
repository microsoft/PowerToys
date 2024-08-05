#pragma once
#include "Constants.g.h"
namespace winrt::interop::implementation
{
    struct Constants : ConstantsT<Constants>
    {
        Constants() = default;

        static hstring AppDataPath();
        static hstring PowerLauncherSharedEvent();
    };
}

namespace winrt::interop::factory_implementation
{
    struct Constants : ConstantsT<Constants, implementation::Constants>
    {
    };
}
