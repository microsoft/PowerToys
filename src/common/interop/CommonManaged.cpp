#include "pch.h"
#include "CommonManaged.h"
#include "CommonManaged.g.cpp"
#include <common/version/version.h>

namespace winrt::PowerToys::Interop::implementation
{
    hstring CommonManaged::GetProductVersion()
    {
        return hstring{ get_product_version() };
    }

    hstring CommonManaged::GetProductVersionChannel()
    {
        return hstring{ get_product_version_channel() };
    }

    hstring CommonManaged::GetProductVersionSourceCommit()
    {
        return hstring{ get_product_version_source_commit() };
    }
}
