#pragma once
#include "Elevation.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct Elevation : ElevationT<Elevation>
    {
        static void RunNonElevated(const winrt::hstring& file, const winrt::hstring& params);
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct Elevation : ElevationT<Elevation, implementation::Elevation>
    {
    };
}

