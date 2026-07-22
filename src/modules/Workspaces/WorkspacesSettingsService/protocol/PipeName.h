// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.
//
// Per-user pipe naming.
//
// Each user gets their OWN service instance keyed by SID
// (PTSettingsSvc_<SID>), reachable over the pipe
//   \\.\pipe\PTSettingsSvc_<SID>
// The server builds the name from the owner SID it was registered for; every
// client derives it deterministically from its OWN token SID, so a caller can
// only ever reach its own user's service instance and never discovers another
// user's.  This is what dissolves the multi-user / multi-version problem and
// lets caller-auth keep a clean exact version-match.

#pragma once

#include <windows.h>
#include <sddl.h>
#include <string>
#include <vector>

namespace PTSettingsSvc
{
    // Fixed prefix; the per-user instance appends the owner SID string.
    constexpr const wchar_t* kPipeNamePrefix = L"\\\\.\\pipe\\PTSettingsSvc_";

    // Builds the full pipe path for a given owner SID string.
    inline std::wstring BuildPipeName(const std::wstring& sidString)
    {
        return std::wstring(kPipeNamePrefix) + sidString;
    }

    // Current process token's user SID in string form (S-1-5-...).  Empty on
    // failure.  Clients use this to reach THEIR OWN user's service; the service
    // uses it as a fallback owner SID for console/dev runs (no SID argument).
    inline std::wstring CurrentProcessUserSidString()
    {
        HANDLE token = nullptr;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, &token))
        {
            return {};
        }

        std::wstring result;
        DWORD size = 0;
        GetTokenInformation(token, TokenUser, nullptr, 0, &size);
        if (size > 0)
        {
            std::vector<BYTE> buf(size);
            if (GetTokenInformation(token, TokenUser, buf.data(), size, &size))
            {
                PSID sid = reinterpret_cast<TOKEN_USER*>(buf.data())->User.Sid;
                LPWSTR s = nullptr;
                if (ConvertSidToStringSidW(sid, &s))
                {
                    result = s;
                    LocalFree(s);
                }
            }
        }

        CloseHandle(token);
        return result;
    }
}
