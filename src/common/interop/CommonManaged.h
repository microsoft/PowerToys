#pragma once
#include "CommonManaged.g.h"

namespace winrt::PowerToys::Interop::implementation
{
    struct CommonManaged : CommonManagedT<CommonManaged>
    {
        CommonManaged() = default;

        static hstring GetProductVersion();
        static winrt::Windows::Foundation::Collections::IVector<hstring> GetAllActiveMicrophoneDeviceNames();
        static winrt::Windows::Foundation::Collections::IVector<hstring> GetAllVideoCaptureDeviceNames();
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct CommonManaged : CommonManagedT<CommonManaged, implementation::CommonManaged>
    {
    };
}
