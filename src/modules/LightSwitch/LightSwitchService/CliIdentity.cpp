// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "CliIdentity.h"

#include <winrt/base.h>

namespace light_switch_cli
{
    namespace
    {
        std::vector<BYTE> QueryToken(HANDLE token, TOKEN_INFORMATION_CLASS informationClass)
        {
            DWORD size = 0;
            GetTokenInformation(token, informationClass, nullptr, 0, &size);
            if (GetLastError() != ERROR_INSUFFICIENT_BUFFER || size == 0)
            {
                winrt::throw_last_error();
            }
            std::vector<BYTE> result(size);
            winrt::check_bool(GetTokenInformation(token, informationClass, result.data(), size, &size));
            return result;
        }

        std::vector<BYTE> CopyIdentitySid(PSID sid)
        {
            if (!sid || !IsValidSid(sid))
            {
                throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_INVALID_SID));
            }
            std::vector<BYTE> result(GetLengthSid(sid));
            winrt::check_bool(CopySid(static_cast<DWORD>(result.size()), result.data(), sid));
            return result;
        }
    }

    TokenIdentity ReadTokenIdentity(HANDLE token)
    {
        TokenIdentity identity;
        const auto user = QueryToken(token, TokenUser);
        identity.userSid = CopyIdentitySid(reinterpret_cast<const TOKEN_USER*>(user.data())->User.Sid);

        DWORD sessionId = 0;
        DWORD size = 0;
        winrt::check_bool(GetTokenInformation(token, TokenSessionId, &sessionId, sizeof(sessionId), &size));
        identity.sessionId = sessionId;

        const auto groupsBuffer = QueryToken(token, TokenLogonSid);
        const auto groups = reinterpret_cast<const TOKEN_GROUPS*>(groupsBuffer.data());
        if (groups->GroupCount != 1 || (groups->Groups[0].Attributes & SE_GROUP_LOGON_ID) != SE_GROUP_LOGON_ID)
        {
            throw winrt::hresult_error(HRESULT_FROM_WIN32(ERROR_NO_SUCH_LOGON_SESSION));
        }
        identity.logonSid = CopyIdentitySid(groups->Groups[0].Sid);
        return identity;
    }

    bool IsSameLogon(const TokenIdentity& first, const TokenIdentity& second) noexcept
    {
        return first.sessionId.has_value() && second.sessionId.has_value() &&
               !first.userSid.empty() && !second.userSid.empty() &&
               !first.logonSid.empty() && !second.logonSid.empty() &&
               first.sessionId == second.sessionId &&
               first.userSid == second.userSid && first.logonSid == second.logonSid;
    }
}
