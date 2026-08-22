#pragma once
#include "ThemeHelper.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct ThemeHelper : ThemeHelperT<ThemeHelper>
    {
        ThemeHelper() = default;

        static void SetSystemTheme(bool dark);
        static void SetAppsTheme(bool dark);
        static bool GetCurrentSystemTheme();
        static bool GetCurrentAppsTheme();
    };
}

namespace winrt::PowerToys::Interop::factory_implementation
{
    struct ThemeHelper : ThemeHelperT<ThemeHelper, implementation::ThemeHelper>
    {
    };
}

