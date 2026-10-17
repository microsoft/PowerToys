// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <optional>
#include <vector>
#include <Windows.h>

namespace light_switch_cli
{
    struct TokenIdentity
    {
        std::optional<DWORD> sessionId;
        std::vector<BYTE> userSid;
        std::vector<BYTE> logonSid;
    };

    // All three fields are required. UAC's linked tokens share a logon SID even
    // though their TOKEN_STATISTICS.AuthenticationId values differ.
    TokenIdentity ReadTokenIdentity(HANDLE token);
    bool IsSameLogon(const TokenIdentity& first, const TokenIdentity& second) noexcept;
}
