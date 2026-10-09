#include "comUtils.h"

#include <Windows.h>
#pragma warning(push)
#pragma warning(disable : 4067)
#include <Sddl.h>
#pragma warning(pop)

#include <memory>
#include <wil/resource.h>

// Helper class for various COM-related APIs, e.g working with security descriptors
template<typename T>
struct typed_storage
{
    std::unique_ptr<char[]> _buffer;
    inline explicit typed_storage(const DWORD size) :
        _buffer{ std::make_unique<char[]>(size) }
    {
    }
    
    inline operator T*()
    {
        return reinterpret_cast<T*>(_buffer.get());
    }
};

bool initializeCOMSecurity(const wchar_t* securityDescriptor)
{
    PSECURITY_DESCRIPTOR self_relative_sd{};
    if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(securityDescriptor, SDDL_REVISION_1, &self_relative_sd, nullptr))
    {
        return false;
    }

    auto free_relative_sd = wil::scope_exit([&] {
        LocalFree(self_relative_sd);
    });

    DWORD absolute_sd_size = 0;
    DWORD dacl_size = 0;
    DWORD group_size = 0;
    DWORD owner_size = 0;
    DWORD sacl_size = 0;

    if (!MakeAbsoluteSD(self_relative_sd, nullptr, &absolute_sd_size, nullptr, &dacl_size, nullptr, &sacl_size, nullptr, &owner_size, nullptr, &group_size))
    {
        if (GetLastError() != ERROR_INSUFFICIENT_BUFFER)
        {
            return false;
        }
    }

    typed_storage<SECURITY_DESCRIPTOR> absolute_sd{ absolute_sd_size };
    typed_storage<ACL> dacl{ dacl_size };
    typed_storage<ACL> sacl{ sacl_size };
    typed_storage<SID> owner{ owner_size };
    typed_storage<SID> group{ group_size };

    if (!MakeAbsoluteSD(self_relative_sd,
                        absolute_sd,
                        &absolute_sd_size,
                        dacl,
                        &dacl_size,
                        sacl,
                        &sacl_size,
                        owner,
                        &owner_size,
                        group,
                        &group_size))
    {
        return false;
    }

    return !FAILED(CoInitializeSecurity(
        absolute_sd,
        -1,
        nullptr,
        nullptr,
        RPC_C_AUTHN_LEVEL_PKT_PRIVACY,
        RPC_C_IMP_LEVEL_IDENTIFY,
        nullptr,
        EOAC_DYNAMIC_CLOAKING | EOAC_DISABLE_AAA,
        nullptr));
}
