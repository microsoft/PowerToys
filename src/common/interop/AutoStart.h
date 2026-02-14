#pragma once
#include "pch.h"
#include "AutoStart.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct AutoStart : AutoStartT<AutoStart>
    {
        static bool CreateAutoStartTaskForThisUser(bool runElevated);
        static bool IsAutoStartTaskActiveForThisUser();
        static bool DeleteAutoStartTaskForThisUser();
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct AutoStart : AutoStartT<AutoStart, implementation::AutoStart>
    {
    };
}
