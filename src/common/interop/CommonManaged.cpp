#include "pch.h"
#include "CommonManaged.h"
#include "CommonManaged.g.cpp"
#include <common/version/version.h>
#include <common/utils/named_pipe_peer_auth.h>

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

    bool CommonManaged::AuthenticateNamedPipeServer(
        uint64_t pipeHandle,
        hstring const& expectedProcessName,
        hstring const& trustedDirectory,
        hstring const& referenceBinaryPath,
        winrt::PowerToys::Interop::NamedPipePeerValidation validation)
    {
        named_pipe_peer_auth::Policy policy{
            expectedProcessName.c_str(),
            trustedDirectory.c_str(),
            referenceBinaryPath.c_str(),
            validation == winrt::PowerToys::Interop::NamedPipePeerValidation::WindowsSystemHost ?
                named_pipe_peer_auth::Validation::WindowsSystemHost :
                named_pipe_peer_auth::Validation::PowerToysPeer,
        };
        return named_pipe_peer_auth::authenticate(
            reinterpret_cast<HANDLE>(pipeHandle),
            named_pipe_peer_auth::Peer::Server,
            policy);
    }
}
