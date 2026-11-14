#pragma once

#include <Windows.h>
#include <sddl.h>

#include <algorithm>
#include <optional>
#include <string>
#include <vector>

#include <wil/resource.h>

namespace auto_start_task
{
    struct RegisteredTask
    {
        std::wstring executablePath;
        bool runElevated = false;
        std::optional<std::wstring> logonTriggerDelay;
        std::wstring securityDescriptor;
    };

    inline std::optional<std::vector<std::wstring>> get_explicit_trustees(const std::wstring& securityDescriptor)
    {
        wil::unique_hlocal_security_descriptor descriptor;
        if (!ConvertStringSecurityDescriptorToSecurityDescriptorW(securityDescriptor.c_str(), SDDL_REVISION_1, descriptor.put(), nullptr))
        {
            return std::nullopt;
        }

        BOOL daclPresent = FALSE;
        BOOL daclDefaulted = FALSE;
        PACL dacl = nullptr;
        if (!GetSecurityDescriptorDacl(descriptor.get(), &daclPresent, &dacl, &daclDefaulted) || !daclPresent || !dacl)
        {
            return std::nullopt;
        }

        std::vector<std::wstring> trustees;
        for (DWORD i = 0; i < dacl->AceCount; ++i)
        {
            void* aceData = nullptr;
            if (!GetAce(dacl, i, &aceData))
            {
                return std::nullopt;
            }

            const auto ace = static_cast<ACCESS_ALLOWED_ACE*>(aceData);
            if (ace->Header.AceFlags & INHERITED_ACE)
            {
                continue;
            }

            wil::unique_hlocal_string sid;
            if (ace->Header.AceType != ACCESS_ALLOWED_ACE_TYPE || !ConvertSidToStringSidW(&ace->SidStart, sid.put()))
            {
                return std::nullopt;
            }

            trustees.emplace_back(sid.get());
        }

        return trustees;
    }

    inline bool grants_access_only_to(const std::wstring& securityDescriptor, const std::wstring& expectedSecurityDescriptor)
    {
        const auto trustees = get_explicit_trustees(securityDescriptor);
        const auto expectedTrustees = get_explicit_trustees(expectedSecurityDescriptor);
        return trustees && expectedTrustees && std::ranges::all_of(*trustees, [&](const std::wstring& sid) {
                   return std::ranges::find(*expectedTrustees, sid) != expectedTrustees->end();
               });
    }

    inline bool matches(const RegisteredTask& task, const std::wstring& executablePath, bool runElevated)
    {
        return task.logonTriggerDelay.has_value() &&
               task.runElevated == runElevated &&
               CompareStringOrdinal(task.executablePath.c_str(), -1, executablePath.c_str(), -1, TRUE) == CSTR_EQUAL;
    }
}
