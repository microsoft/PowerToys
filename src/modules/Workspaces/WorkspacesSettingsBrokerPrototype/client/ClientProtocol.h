// Copyright (c) Microsoft Corporation
// Licensed under the MIT license.

#pragma once

#include <windows.h>

#include <cstdint>
#include <string>
#include <vector>

#include "../common/Protocol.h"

namespace SettingsBrokerPrototype
{
    struct ClientResponse
    {
        ResponseHeader header{};
        std::vector<BYTE> payload;
    };

    bool ConnectToBroker(HANDLE& pipe, DWORD& verifiedServerPid, std::wstring& error);
    bool SendRequest(HANDLE pipe,
                     const RequestHeader& request,
                     const std::vector<BYTE>& payload,
                     ClientResponse& response,
                     std::wstring& error,
                     DWORD responseReadDelayMs = 0);
    const wchar_t* StatusName(Status status);
}
