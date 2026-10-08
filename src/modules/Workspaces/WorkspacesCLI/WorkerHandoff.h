// Copyright (c) Microsoft Corporation. Licensed under the MIT license.
#pragma once

#include <array>
#include <string>
#include <wil/resource.h>

namespace WorkspacesCli
{
    struct WorkerHandles
    {
        HANDLE snapshot = nullptr;
        HANDLE cancel = nullptr;
        HANDLE input = nullptr;
        HANDLE result = nullptr;
        HANDLE approvalRequests = nullptr;
        HANDLE approvalReplies = nullptr;
    };

    struct ReceivedWorkerHandles
    {
        wil::unique_handle snapshot;
        wil::unique_handle cancel;
        wil::unique_handle input;
        wil::unique_handle result;
        wil::unique_handle approvalRequests;
        wil::unique_handle approvalReplies;
        wil::unique_handle ownerLifetime;
        DWORD ownerPid = 0;

        void BindStandardStreams() const;
    };

    // Uses Explorer's process-creation context, then verifies the suspended child's actual token.
    wil::unique_process_information StartMediumWorker(const std::wstring& executable, const std::wstring& operationId, const WorkerHandles& handles, ULONGLONG deadline);
    ReceivedWorkerHandles ReceiveWorkerHandles(const std::wstring& operationId, DWORD ownerPid);
    bool SameUserSession(HANDLE first, HANDLE second);
    bool IsMediumProcess(HANDLE process);
}
