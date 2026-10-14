#pragma once
#include "CommonManaged.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct CommonManaged : CommonManagedT<CommonManaged>
    {
        CommonManaged() = default;

        static hstring GetProductVersion();
        static hstring GetProductVersionChannel();
        static hstring GetProductVersionSourceCommit();
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct CommonManaged : CommonManagedT<CommonManaged, implementation::CommonManaged>
    {
    };
}
