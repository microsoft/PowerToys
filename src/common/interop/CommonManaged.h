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
        static bool AuthenticateNamedPipeServer(
            uint64_t pipeHandle,
            hstring const& expectedProcessName,
            hstring const& trustedDirectory,
            hstring const& referenceBinaryPath,
            winrt::PowerToys::Interop::NamedPipePeerValidation validation);
    };
}
namespace winrt::PowerToys::Interop::factory_implementation
{
    struct CommonManaged : CommonManagedT<CommonManaged, implementation::CommonManaged>
    {
    };
}
