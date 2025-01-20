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
}
