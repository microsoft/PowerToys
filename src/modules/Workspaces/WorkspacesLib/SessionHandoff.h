// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <array>
#include <string>
#include <vector>
#include <wil/resource.h>

namespace WorkspacesSession
{
    struct WorkerHandles
    {
        HANDLE snapshot = nullptr;
        HANDLE cancel = nullptr;
        HANDLE result = nullptr;
    };

    struct ReceivedWorkerHandles
    {
        wil::unique_handle snapshot;
        wil::unique_handle cancel;
        wil::unique_handle result;
        wil::unique_handle ownerLifetime;
        DWORD ownerPid = 0;
    };

    // Uses Explorer's process-creation context, then verifies the suspended child's actual token.
    wil::unique_process_information StartMediumWorker(const std::wstring& executable, const std::wstring& operationId, const WorkerHandles& handles, ULONGLONG deadline);
    ReceivedWorkerHandles ReceiveWorkerHandles(const std::wstring& operationId, DWORD ownerPid, const std::vector<std::wstring>& allowedOwners);
    bool SameUserSession(HANDLE first, HANDLE second);
    bool IsMediumProcess(HANDLE process);
}
